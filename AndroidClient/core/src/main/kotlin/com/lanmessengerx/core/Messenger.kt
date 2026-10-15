package com.lanmessengerx.core

import java.io.File
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.Socket
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.Executors
import java.util.concurrent.ScheduledExecutorService
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicLong

class MessengerConfig(
    var userName: String,
    var status: String = Proto.STATUS_ONLINE,
    var note: String = "",
    /** 12 hex digits identifying this device (the original uses the NIC's MAC address). */
    val machineId: String,
    val logonName: String,
    val hostName: String,
    val osName: String,
    val cacheDir: File,
    val udpPort: Int = Proto.DEFAULT_PORT,
    val tcpPort: Int = Proto.DEFAULT_PORT,
    val multicast: String = Proto.DEFAULT_MULTICAST,
    val extraBroadcasts: List<String> = emptyList(),
    val timeoutSec: Int = 10,
    val maxRetries: Int = 1,
    val preferredInterface: String? = null,
    /** Also look for people by connecting to the TCP port of every address in the subnet. */
    val scanSubnet: Boolean = true,
    val firstName: String = "",
    val lastName: String = "",
    val about: String = "",
)

/**
 * The LAN Messenger protocol engine. All protocol state lives on one engine thread;
 * public methods may be called from any thread. Events are delivered on the engine
 * thread - UI code has to hop to its own thread.
 */
class Messenger(
    private val cfg: MessengerConfig,
    private val log: (String) -> Unit = {},
    private val listener: (MessengerEvent) -> Unit,
) {
    // ---- engine thread --------------------------------------------------------------------
    private val engine: ScheduledExecutorService = Executors.newSingleThreadScheduledExecutor(Threads.factory)
    private val io = Threads.newPool()

    val localId: String = buildUserId(cfg.machineId, cfg.logonName)

    private val users = LinkedHashMap<String, User>()
    private val streams = HashMap<String, Conn>()
    private val keys = HashMap<String, SessionCipher>()
    private val connecting = HashSet<String>()
    private val rooms = LinkedHashMap<String, MutableList<String>>()
    private val pending = ArrayList<Pending>()
    private val received = LinkedHashSet<String>()
    private val transfers = ConcurrentHashMap<String, Transfer>()
    private val msgIdSeq = AtomicLong(1)

    private var rsa: LmxCrypto.RsaKeys? = null
    private var lan: LanInterface? = null
    private var udp: UdpService? = null
    private var tcp: TcpServer? = null
    private var started = false
    private var connected = false
    private var localAvatar: File? = null

    @Volatile
    private var snapshot: List<User> = emptyList()

    @Volatile
    var localUser: User = User(
        localId, Proto.VERSION, "", cfg.userName, cfg.status, cfg.note,
        Proto.UC_FILE or Proto.UC_GROUP_MESSAGE or Proto.UC_FOLDER,
    )
        private set

    val isConnected: Boolean get() = connected
    val onlineUsers: List<User> get() = snapshot

    private class Pending(
        val msgId: Long,
        val type: MessageType,
        val userId: String,
        val body: XmlMessage,
        var sentAt: Long,
        var retry: Int,
    )

    private class Transfer(
        val id: String,
        val userId: String,
        val name: String,
        val size: Long,
        val outgoing: Boolean,
        val type: FileType,
        var state: TransferState,
        var source: FileSource? = null,
        @Volatile var sink: FileSink? = null,
        var bytes: Long = 0,
        @Volatile var cancelled: Boolean = false,
        @Volatile var socket: Socket? = null,
        var folderId: String = "",
    )

    /** A folder is transferred as a series of ordinary file transfers (filetype "folder"), one after the other. */
    private class FolderTx(
        val id: String,
        val userId: String,
        val outgoing: Boolean,
        var name: String = "",
        var size: Long = 0,
        var count: Int = 0,
        var state: TransferState = TransferState.REQUESTED,
        val files: MutableList<FolderEntry> = ArrayList(), // outgoing
        var sinks: FolderSinkFactory? = null,              // incoming: where the files are written
        var location: String? = null,                      // incoming: shown to the user when done
        var done: Int = 0,
        var doneBytes: Long = 0,
        var currentFileId: String? = null,
        var accepted: Boolean = false,
    )

    private val folders = HashMap<String, FolderTx>()

    // ==========================================================================================
    // lifecycle
    // ==========================================================================================

    fun start() {
        engine.execute {
            if (started) return@execute
            started = true
            io.execute {
                val keys = LmxCrypto.generateRsa()
                post { rsa = keys; networkTick() }
            }
            engine.scheduleWithFixedDelay({ safe { networkTick() } }, 2, 2, TimeUnit.SECONDS)
            engine.scheduleWithFixedDelay({ safe { checkPending() } }, 1, 1, TimeUnit.SECONDS)
            engine.scheduleWithFixedDelay({ safe { keepAlive() } }, 30, 30, TimeUnit.SECONDS)
            engine.scheduleWithFixedDelay({ safe { sweepSubnet() } }, 60, 60, TimeUnit.SECONDS)
        }
    }

    fun stop() {
        engine.execute {
            if (!started) return@execute
            if (connected) broadcast(MessageType.DEPART)
            teardownNetwork()
            started = false
        }
        engine.shutdown()
        try {
            engine.awaitTermination(2, TimeUnit.SECONDS)
        } catch (_: InterruptedException) {
        }
        io.shutdownNow()
    }

    private inline fun safe(block: () -> Unit) {
        try {
            block()
        } catch (e: Exception) {
            log("engine error: $e")
        }
    }

    private fun emit(e: MessengerEvent) {
        try {
            listener(e)
        } catch (t: Throwable) {
            log("listener error: $t")
        }
    }

    private fun post(block: () -> Unit) {
        try {
            engine.execute { safe(block) }
        } catch (_: java.util.concurrent.RejectedExecutionException) {
        }
    }

    // ==========================================================================================
    // network up/down
    // ==========================================================================================

    private fun networkTick() {
        if (!started || rsa == null) return
        val found = NetUtil.findLanInterface(cfg.preferredInterface)
        if (found == null) {
            if (connected) {
                log("network lost")
                teardownNetwork()
                emit(MessengerEvent.ConnectionChanged(false, null))
            }
            return
        }
        if (connected && found.sameAs(lan)) return
        if (connected) teardownNetwork()
        lan = found
        log("network up: ${found.name} ${found.ip}/${found.prefixLength}")
        var server = TcpServer(cfg.tcpPort, { s -> io.execute { handleIncoming(s) } }, log)
        var tcpOk = server.start()
        if (!tcpOk) {
            // like the current original client: if the port is taken, take any free one and advertise it in the announce
            server = TcpServer(0, { s -> io.execute { handleIncoming(s) } }, log)
            tcpOk = server.start()
        }
        tcp = if (tcpOk) server else null
        if (tcpOk) log("TCP listening on port ${server.boundPort}")
        val u = UdpService(found, cfg.udpPort, InetAddress.getByName(cfg.multicast), { ip, text ->
            post { onBroadcastDatagram(ip, text) }
        }, log)
        u.start()
        udp = u
        connected = true
        localUser = localUser.copy(address = found.ip)
        emit(MessengerEvent.ConnectionChanged(true, found.ip))
        broadcast(MessageType.DEPART)
        broadcast(MessageType.ANNOUNCE)
        // UDP is lossy (especially on Wi-Fi): repeat the announce a little later, and scan the subnet as a second way to find people
        sweepRound = 0
        engine.schedule({ safe { broadcast(MessageType.ANNOUNCE) } }, 2, TimeUnit.SECONDS)
        engine.schedule({ safe { broadcast(MessageType.ANNOUNCE) } }, 6, TimeUnit.SECONDS)
        engine.schedule({ safe { sweepSubnet() } }, 3, TimeUnit.SECONDS)
    }

    private fun teardownNetwork() {
        udp?.stop(); udp = null
        tcp?.stop(); tcp = null
        for (c in streams.values.toList()) c.close()
        streams.clear()
        keys.clear()
        connecting.clear()
        probing.clear()
        val gone = users.values.toList()
        users.clear()
        pending.clear()
        refreshSnapshot()
        for (u in gone) emit(MessengerEvent.UserOffline(u.id, u.name))
        for (t in transfers.values) cancelTransferLocally(t, TransferState.ERROR)
        connected = false
        lan = null
    }

    private fun keepAlive() {
        if (!connected) return
        broadcast(MessageType.ANNOUNCE)
        for (u in users.values.toList()) {
            val id = msgIdSeq.getAndIncrement()
            val body = XmlMessage()
            pending.add(Pending(id, MessageType.PING, u.id, body, now(), 0))
            transmit(u.id, MessageType.PING, id, body)
        }
    }

    private fun now() = System.currentTimeMillis()

    // ==========================================================================================
    // UDP broadcast (announce / depart)
    // ==========================================================================================

    private fun broadcast(type: MessageType) {
        val l = lan ?: return
        val u = udp ?: return
        if (localId.isEmpty()) return
        val m = XmlMessage()
        m.addHeader(Xn.FROM, localId)
        m.addHeader(Xn.MESSAGE_ID, msgIdSeq.get().toString())
        m.addHeader(Xn.TYPE, type.wire)
        // The current original client (2.x) listens on a free port when 50000 is taken, and cannot reach a peer
        // that does not say where to connect: it would dial port 0.
        if (type == MessageType.ANNOUNCE) tcp?.let { m.addData(Xn.TCP_PORT, it.boundPort.toString()) }
        val targets = LinkedHashSet<InetAddress>()
        try {
            targets.add(InetAddress.getByName(cfg.multicast))
            targets.add(l.broadcast)
            targets.add(InetAddress.getByName("255.255.255.255"))
            for (b in cfg.extraBroadcasts) runCatching { targets.add(InetAddress.getByName(b)) }
        } catch (e: Exception) {
            log("broadcast target error: $e")
        }
        u.send(m.toXml().toByteArray(Charsets.UTF_8), targets)
    }

    private fun onBroadcastDatagram(fromIp: String, text: String) {
        val m = XmlMessage.parse(text) ?: return
        val from = m.header(Xn.FROM)
        if (from.isEmpty() || from == localId) return
        when (MessageType.fromWire(m.header(Xn.TYPE))) {
            MessageType.ANNOUNCE -> {
                if (!users.containsKey(from) && !streams.containsKey(from) && connecting.add(from)) {
                    connectTo(from, fromIp, peerPort(m))
                }
            }
            MessageType.DEPART -> removeUser(from)
            else -> {}
        }
    }

    // ==========================================================================================
    // TCP: connection establishment
    // ==========================================================================================

    // ==========================================================================================
    // subnet scan
    // ==========================================================================================

    private val probing = HashSet<String>()
    private val notMessenger = HashMap<String, Long>()
    private var sweepRound = 0

    /**
     * Announces are only sent when a client starts and every few minutes after that, so a client that was started
     * earlier is found late (or never, if broadcasts are filtered / incoming connections are blocked here).
     * Connecting to the messenger port of every address of the subnet finds them within seconds, and works with
     * outgoing connections only.
     */
    private fun sweepSubnet() {
        val l = lan ?: return
        if (!cfg.scanSubnet || !connected || rsa == null) return
        // first minutes: every minute; later: every five minutes
        if (sweepRound++ >= 10 && sweepRound % 5 != 0) return
        val known = HashSet<String>()
        users.values.forEach { known.add(it.address) }
        streams.values.forEach { known.add(it.peerAddress) }
        val now = System.currentTimeMillis()
        val todo = l.subnetHosts().filter { h ->
            h != l.ip && h !in known && h !in probing && (notMessenger[h]?.let { now - it >= 600_000 } ?: true)
        }
        if (todo.isEmpty()) return
        probing.addAll(todo)
        log("subnet scan #$sweepRound: ${todo.size} addresses")
        // submit from an io thread: waiting for a free slot must not block the engine thread
        io.execute {
            val gate = java.util.concurrent.Semaphore(48)
            for (h in todo) {
                gate.acquireUninterruptibly()
                io.execute {
                    try {
                        probeHost(h)
                    } finally {
                        gate.release()
                    }
                }
            }
        }
    }

    private fun probeHost(ip: String) {
        var conn: Conn? = null
        try {
            val s = Socket()
            s.tcpNoDelay = true
            try {
                s.connect(InetSocketAddress(ip, cfg.tcpPort), 800)
            } catch (e: Exception) {
                runCatching { s.close() }
                post { probing.remove(ip) }
                return
            }
            s.keepAlive = true
            val c = Conn(s, incoming = false, peerId = "probe@$ip")
            c.probe = true
            conn = c
            // something listens there; if it does not behave like a messenger within a few seconds, forget it for a while
            engine.schedule({
                safe {
                    if (c.probe && !c.closed) {
                        notMessenger[ip] = System.currentTimeMillis()
                        c.close()
                    }
                }
            }, 6, TimeUnit.SECONDS)
            c.sendRaw(("MSG$localId").toByteArray(Charsets.UTF_8))
            readLoop(c)
        } catch (e: Exception) {
            conn?.close()
            post { probing.remove(ip) }
        }
    }

    /** TCP port a peer advertised (announce / file accept), or our own configured port for older peers. */
    private fun peerPort(m: XmlMessage): Int = m.data(Xn.TCP_PORT).toIntOrNull()?.takeIf { it in 1..65535 } ?: cfg.tcpPort

    private fun connectTo(userId: String, ip: String, port: Int) {
        io.execute {
            var conn: Conn? = null
            try {
                val s = Socket()
                s.tcpNoDelay = true
                s.keepAlive = true
                s.connect(InetSocketAddress(ip, port), 5000)
                conn = Conn(s, incoming = false, peerId = userId)
                conn.sendRaw(("MSG$localId").toByteArray(Charsets.UTF_8))
                readLoop(conn)
            } catch (e: Exception) {
                log("connect to $userId@$ip failed: ${e.message}")
                conn?.close()
                post { connecting.remove(userId) }
            }
        }
    }

    private fun handleIncoming(s: Socket) {
        try {
            s.soTimeout = 5000
            val first = ByteArray(Proto.ID_PACKET_MAX)
            val raw = s.getInputStream()
            var n = raw.read(first)
            if (n <= 0) {
                s.close(); return
            }
            val head = String(first, 0, minOf(n, 4), Charsets.US_ASCII)
            if (head.startsWith("MSG")) {
                s.soTimeout = 0
                val peerId = String(first, 3, n - 3, Charsets.UTF_8)
                val conn = Conn(s, incoming = true, peerId = peerId)
                val key = rsa?.publicPem
                if (key == null) {
                    conn.close(); return
                }
                conn.sendFrame(DatagramType.PUBLIC_KEY, key)
                readLoop(conn)
            } else if (head == "FILE") {
                while (n < 4 + Proto.FILE_ID_LENGTH + 1) {
                    val more = raw.read(first, n, first.size - n)
                    if (more <= 0) break
                    n += more
                }
                if (n < 4 + Proto.FILE_ID_LENGTH) {
                    s.close(); return
                }
                val fileId = String(first, 4, Proto.FILE_ID_LENGTH, Charsets.US_ASCII)
                val userPrefix = String(first, 4 + Proto.FILE_ID_LENGTH, n - 4 - Proto.FILE_ID_LENGTH, Charsets.UTF_8)
                val t = transfers[fileId]
                if (t == null || t.outgoing || !(t.userId.startsWith(userPrefix) || userPrefix.startsWith(t.userId))) {
                    s.close(); return
                }
                s.soTimeout = 30000
                receiveFileLoop(t, s)
            } else {
                s.close()
            }
        } catch (e: Exception) {
            log("incoming connection error: ${e.message}")
            try { s.close() } catch (_: Exception) {}
        }
    }

    /** Runs on the connection's reader thread. */
    private fun readLoop(conn: Conn) {
        try {
            while (true) {
                val frame = conn.readFrame() ?: break
                post { onFrame(conn, frame) }
            }
        } finally {
            conn.close()
            post { onConnClosed(conn) }
        }
    }

    private fun registerStream(conn: Conn) {
        val old = streams.put(conn.peerId, conn)
        if (old != null && old !== conn) log("replacing stream of ${conn.peerId}")
        conn.cipher?.let { keys[conn.peerId] = it }
        connecting.remove(conn.peerId)
    }

    private fun onConnClosed(conn: Conn) {
        connecting.remove(conn.peerId)
        probing.remove(conn.peerAddress)
        if (conn.probe) return
        if (streams[conn.peerId] === conn) {
            streams.remove(conn.peerId)
            keys.remove(conn.peerId)
            removeUser(conn.peerId)
        }
    }

    private fun onFrame(conn: Conn, frame: ByteArray) {
        if (frame.size < 6) return
        val tag = String(frame, 0, 6, Charsets.US_ASCII)
        val data = frame.copyOfRange(6, frame.size)
        when (DatagramType.fromTag(tag)) {
            DatagramType.PUBLIC_KEY -> {
                // we connected out; peer sent its public key -> answer with the session key
                try {
                    val pub = LmxCrypto.publicKeyFromPem(data)
                    val material = LmxCrypto.newSessionMaterial()
                    conn.cipher = SessionCipher(material)
                    conn.sendFrame(DatagramType.HANDSHAKE, LmxCrypto.rsaEncrypt(pub, material))
                    if (!conn.probe) registerStream(conn)
                } catch (e: Exception) {
                    log("handshake with ${conn.peerId} failed: $e")
                    conn.close()
                }
            }
            DatagramType.HANDSHAKE -> {
                try {
                    val material = LmxCrypto.rsaDecrypt(rsa!!.privateKey, data)
                    conn.cipher = SessionCipher(material)
                    registerStream(conn)
                    sendUserData(conn.peerId, QueryOp.GET)
                } catch (e: Exception) {
                    log("handshake from ${conn.peerId} failed: $e")
                    conn.close()
                }
            }
            DatagramType.MESSAGE -> {
                val clear = conn.cipher?.decrypt(data) ?: keys[conn.peerId]?.decrypt(data)
                if (clear == null) {
                    log("could not decrypt message from ${conn.peerId}")
                    return
                }
                val m = XmlMessage.parse(String(clear, Charsets.UTF_8)) ?: return
                if (conn.probe) {
                    // the first message of a scanned peer tells who it is
                    val real = m.header(Xn.FROM)
                    if (real.isEmpty() || real == localId) { conn.close(); return }
                    log("subnet scan found $real @ ${conn.peerAddress}")
                    conn.probe = false
                    conn.peerId = real
                    probing.remove(conn.peerAddress)
                    registerStream(conn)
                }
                processMessage(conn, m)
            }
            null -> {}
        }
    }

    // ==========================================================================================
    // sending
    // ==========================================================================================

    private fun transmit(userId: String, type: MessageType, id: Long, body: XmlMessage): Boolean {
        val conn = streams[userId] ?: return false
        val cipher = conn.cipher ?: return false
        val m = XmlMessage()
        m.addHeader(Xn.FROM, localId)
        m.addHeader(Xn.TO, userId)
        m.addHeader(Xn.MESSAGE_ID, id.toString())
        m.addHeader(Xn.TYPE, type.wire)
        m.body.addAll(body.body)
        return try {
            conn.sendFrame(DatagramType.MESSAGE, cipher.encrypt(m.toXml().toByteArray(Charsets.UTF_8)))
            true
        } catch (e: Exception) {
            log("send failed: $e")
            false
        }
    }

    private fun sendUserData(userId: String, op: QueryOp) {
        val b = XmlMessage()
        b.addData(Xn.USER_ID, localId)
        b.addData(Xn.NAME, localUser.name)
        b.addData(Xn.ADDRESS, localUser.address)
        b.addData(Xn.VERSION, localUser.version)
        b.addData(Xn.STATUS, localUser.status)
        b.addData(Xn.NOTE, localUser.note)
        b.addData(Xn.USER_CAPS, localUser.caps.toString())
        b.addData(Xn.QUERY_OP, op.wire)
        transmit(userId, MessageType.USER_DATA, msgIdSeq.get(), b)
    }

    private fun ack(userId: String, id: Long) {
        val b = XmlMessage().addData(Xn.MESSAGE_ID, id.toString())
        transmit(userId, MessageType.ACKNOWLEDGE, msgIdSeq.getAndIncrement(), b)
    }

    private fun sendTracked(userId: String, type: MessageType, body: XmlMessage, id: Long): Boolean {
        pending.add(Pending(id, type, userId, body, now(), 0))
        return transmit(userId, type, id, body)
    }

    private fun checkPending() {
        val t = now()
        val it = pending.iterator()
        val failed = ArrayList<Pending>()
        while (it.hasNext()) {
            val p = it.next()
            if (t - p.sentAt <= cfg.timeoutSec * 1000L) continue
            if (p.retry < cfg.maxRetries) {
                p.retry++
                p.sentAt = t
                if (users.containsKey(p.userId)) transmit(p.userId, p.type, p.msgId, p.body)
            } else {
                it.remove()
                failed.add(p)
            }
        }
        for (p in failed) {
            when (p.type) {
                MessageType.MESSAGE -> emit(MessengerEvent.MessageFailed(p.userId, p.msgId))
                MessageType.PING -> removeUser(p.userId, close = true)
                else -> {}
            }
        }
    }

    // ---- public chat API --------------------------------------------------------------------

    /** @return the message id used to correlate [MessengerEvent.MessageDelivered]/[MessengerEvent.MessageFailed]. */
    fun sendText(userId: String, text: String): Long {
        val id = msgIdSeq.getAndIncrement()
        post {
            if (!users.containsKey(userId)) {
                emit(MessengerEvent.MessageFailed(userId, id)); return@post
            }
            val b = XmlMessage()
            b.addData(Xn.MESSAGE, text)
            b.addData(Xn.FONT, DEFAULT_FONT)
            b.addData(Xn.COLOR, DEFAULT_COLOR)
            if (!sendTracked(userId, MessageType.MESSAGE, b, id)) {
                pending.removeAll { it.msgId == id }
                emit(MessengerEvent.MessageFailed(userId, id))
            }
        }
        return id
    }

    fun sendBroadcastText(userIds: Collection<String>, text: String) {
        post {
            val id = msgIdSeq.getAndIncrement()
            for (uid in userIds) {
                val b = XmlMessage().addData(Xn.BROADCAST, text)
                transmit(uid, MessageType.BROADCAST, id, b)
            }
        }
    }

    fun sendPublicText(text: String) {
        post {
            val id = msgIdSeq.getAndIncrement()
            for (uid in users.keys.toList()) {
                val b = XmlMessage()
                b.addData(Xn.FONT, DEFAULT_FONT)
                b.addData(Xn.COLOR, DEFAULT_COLOR)
                b.addData(Xn.MESSAGE, text)
                transmit(uid, MessageType.PUBLIC_MESSAGE, id, b)
            }
        }
    }

    fun sendChatState(userId: String, state: ChatStateKind, threadId: String = "") {
        post {
            if (!users.containsKey(userId)) return@post
            val b = XmlMessage()
            if (threadId.isNotEmpty()) b.addData(Xn.THREAD, threadId)
            b.addData(Xn.CHAT_STATE, state.wire)
            transmit(userId, MessageType.CHAT_STATE, msgIdSeq.getAndIncrement(), b)
        }
    }

    fun requestUserInfo(userId: String) {
        post {
            val id = msgIdSeq.getAndIncrement()
            val b = XmlMessage().addData(Xn.QUERY_OP, QueryOp.GET.wire)
            sendTracked(userId, MessageType.QUERY, b, id)
        }
    }

    fun refresh() {
        post {
            broadcast(MessageType.ANNOUNCE)
            keepAlive()
        }
    }

    // ---- local profile ------------------------------------------------------------------------

    fun setStatus(status: String) {
        if (status !in Proto.STATUS_CODES) return
        post {
            localUser = localUser.copy(status = status)
            val id = msgIdSeq.getAndIncrement()
            for (uid in users.keys.toList()) {
                transmit(uid, MessageType.STATUS, id, XmlMessage().addData(Xn.STATUS, status))
            }
        }
    }

    fun setName(name: String) {
        val n = name.trim()
        if (n.isEmpty()) return
        post {
            localUser = localUser.copy(name = n)
            val id = msgIdSeq.getAndIncrement()
            for (uid in users.keys.toList()) {
                transmit(uid, MessageType.USER_NAME, id, XmlMessage().addData(Xn.NAME, n))
            }
        }
    }

    fun setNote(note: String) {
        post {
            localUser = localUser.copy(note = note)
            val id = msgIdSeq.getAndIncrement()
            for (uid in users.keys.toList()) {
                transmit(uid, MessageType.NOTE, id, XmlMessage().addData(Xn.NOTE, note))
            }
        }
    }

    /** [png] must be a PNG image file; it is announced to every online user. */
    fun setLocalAvatar(png: File?) {
        post {
            localAvatar = png
            localUser = localUser.copy(avatarPath = png?.absolutePath)
            if (png != null && png.isFile) for (uid in users.keys.toList()) sendAvatarTo(uid)
        }
    }

    // ==========================================================================================
    // receiving: messages
    // ==========================================================================================

    private fun processMessage(conn: Conn, m: XmlMessage) {
        val from = m.header(Xn.FROM)
        if (from.isEmpty()) return
        val type = MessageType.fromWire(m.header(Xn.TYPE)) ?: return
        val id = m.header(Xn.MESSAGE_ID).toLongOrNull() ?: 0L
        when (type) {
            MessageType.USER_DATA -> {
                if (m.data(Xn.QUERY_OP) == QueryOp.GET.wire) sendUserData(from, QueryOp.RESULT)
                addUser(
                    m.data(Xn.USER_ID).ifEmpty { from }, m.data(Xn.VERSION), m.data(Xn.ADDRESS).ifEmpty { conn.peerAddress },
                    m.data(Xn.NAME), m.data(Xn.STATUS), m.data(Xn.NOTE), m.data(Xn.USER_CAPS).toIntOrNull() ?: 0,
                )
            }
            MessageType.BROADCAST -> emit(MessengerEvent.BroadcastMessage(from, m.data(Xn.BROADCAST), now()))
            MessageType.STATUS -> updateUser(from) { it.copy(status = m.data(Xn.STATUS)) }
            MessageType.USER_NAME -> updateUser(from) { it.copy(name = m.data(Xn.NAME)) }
            MessageType.NOTE -> updateUser(from) { it.copy(note = m.data(Xn.NOTE)) }
            MessageType.MESSAGE -> {
                if (rememberReceived(id, from)) {
                    emit(MessengerEvent.TextMessage(from, m.data(Xn.MESSAGE), now(), id))
                }
                ack(from, id)
            }
            MessageType.GROUP_MESSAGE -> processGroupMessage(from, m)
            MessageType.PUBLIC_MESSAGE -> emit(MessengerEvent.PublicMessage(from, m.data(Xn.MESSAGE), now()))
            MessageType.PING -> ack(from, id)
            MessageType.QUERY -> {
                if (m.data(Xn.QUERY_OP) == QueryOp.GET.wire) {
                    val b = XmlMessage()
                    b.addData(Xn.MESSAGE_ID, id.toString())
                    b.addData(Xn.QUERY_OP, QueryOp.RESULT.wire)
                    b.addData(Xn.USER_ID, localId)
                    b.addData(Xn.NAME, localUser.name)
                    b.addData(Xn.ADDRESS, localUser.address)
                    b.addData(Xn.VERSION, localUser.version)
                    b.addData(Xn.STATUS, localUser.status)
                    b.addData(Xn.NOTE, localUser.note)
                    b.addData(Xn.LOGON, cfg.logonName)
                    b.addData(Xn.HOST, cfg.hostName)
                    b.addData(Xn.OS, cfg.osName)
                    b.addData(Xn.FIRST_NAME, cfg.firstName.ifEmpty { "N/A" })
                    b.addData(Xn.LAST_NAME, cfg.lastName.ifEmpty { "N/A" })
                    b.addData(Xn.ABOUT, cfg.about.ifEmpty { "N/A" })
                    transmit(from, MessageType.QUERY, msgIdSeq.getAndIncrement(), b)
                } else {
                    pending.removeAll { it.msgId == (m.data(Xn.MESSAGE_ID).toLongOrNull() ?: -1L) && it.userId == from }
                    emit(MessengerEvent.UserInfo(from, m.body.toMap()))
                }
            }
            MessageType.CHAT_STATE -> {
                val st = ChatStateKind.fromWire(m.data(Xn.CHAT_STATE)) ?: return
                emit(MessengerEvent.ChatStateChanged(from, st, m.data(Xn.THREAD)))
            }
            MessageType.ACKNOWLEDGE -> {
                val ackId = m.data(Xn.MESSAGE_ID).toLongOrNull() ?: return
                val p = pending.firstOrNull { it.msgId == ackId && it.userId == from } ?: return
                pending.remove(p)
                if (p.type == MessageType.MESSAGE) emit(MessengerEvent.MessageDelivered(from, ackId))
            }
            MessageType.FILE, MessageType.AVATAR -> processFile(from, type, m)
            MessageType.FOLDER -> processFolder(from, m)
            else -> {}
        }
    }

    private fun rememberReceived(id: Long, from: String): Boolean {
        val key = "$from#$id"
        if (!received.add(key)) return false
        if (received.size > 4000) {
            val iter = received.iterator()
            var drop = 1000
            while (drop-- > 0 && iter.hasNext()) { iter.next(); iter.remove() }
        }
        return true
    }

    // ---- users --------------------------------------------------------------------------------

    private fun refreshSnapshot() {
        snapshot = users.values.toList()
    }

    private fun addUser(id: String, version: String, address: String, name: String, status: String, note: String, caps: Int) {
        if (id == localId || users.containsKey(id)) return
        val st = if (status.isEmpty()) Proto.STATUS_ONLINE else status
        val avatar = File(cfg.cacheDir, "avt_${safeName(id)}.png").takeIf { it.isFile }?.absolutePath
        val u = User(id, version, address, name.ifEmpty { id }, st, note, caps, avatar)
        users[id] = u
        refreshSnapshot()
        log("user added: $id ($name) $version @$address")
        emit(MessengerEvent.UserOnline(u))
        if (localAvatar?.isFile == true) sendAvatarTo(id)
    }

    private fun updateUser(id: String, change: (User) -> User) {
        val old = users[id] ?: return
        val nu = change(old)
        if (nu == old) return
        users[id] = nu
        refreshSnapshot()
        emit(MessengerEvent.UserUpdated(nu))
    }

    private fun removeUser(id: String, close: Boolean = false) {
        val u = users.remove(id)
        if (close) {
            streams.remove(id)?.close()
            keys.remove(id)
        }
        pending.removeAll { it.userId == id }
        if (u != null) {
            refreshSnapshot()
            emit(MessengerEvent.UserOffline(id, u.name))
            for ((thread, parts) in rooms) {
                if (parts.remove(id)) {
                    emit(MessengerEvent.RoomNotice(thread, id, false, now()))
                    emit(MessengerEvent.RoomChanged(RoomInfo(thread, parts.toList())))
                }
            }
            for (t in transfers.values.toList()) if (t.userId == id && t.state != TransferState.COMPLETE) {
                cancelTransferLocally(t, TransferState.ERROR)
            }
        }
    }

    // ==========================================================================================
    // group chat
    // ==========================================================================================

    fun createGroupChat(userIds: Collection<String>): String {
        val thread = UUID.randomUUID().toString().replace("-", "").uppercase()
        post {
            createLocalRoom(thread)
            for (uid in userIds) if (uid != localId) sendGroupOp(uid, thread, GroupMsgOp.REQUEST)
        }
        return thread
    }

    fun inviteToRoom(threadId: String, userIds: Collection<String>) {
        post {
            val parts = rooms[threadId] ?: return@post
            for (uid in userIds) if (uid != localId && uid !in parts) sendGroupOp(uid, threadId, GroupMsgOp.REQUEST)
        }
    }

    fun sendGroupText(threadId: String, text: String) {
        post {
            val parts = rooms[threadId] ?: return@post
            val id = msgIdSeq.getAndIncrement()
            for (uid in parts.toList()) {
                if (uid == localId) continue
                val b = XmlMessage()
                b.addData(Xn.FONT, DEFAULT_FONT)
                b.addData(Xn.COLOR, DEFAULT_COLOR)
                b.addData(Xn.MESSAGE, text)
                b.addData(Xn.THREAD, threadId)
                b.addData(Xn.GROUP_MSG_OP, GroupMsgOp.MESSAGE.wire)
                transmit(uid, MessageType.GROUP_MESSAGE, id, b)
            }
        }
    }

    fun leaveGroupChat(threadId: String) {
        post {
            if (rooms.remove(threadId) == null) return@post
            for (uid in users.keys.toList()) sendGroupOp(uid, threadId, GroupMsgOp.LEAVE)
            emit(MessengerEvent.RoomClosed(threadId))
        }
    }

    fun roomSnapshot(): Map<String, List<String>> = synchronized(roomsSnapshot) { HashMap(roomsSnapshot) }
    private val roomsSnapshot = HashMap<String, List<String>>()

    private fun publishRoom(thread: String) {
        val parts = rooms[thread]
        synchronized(roomsSnapshot) { if (parts == null) roomsSnapshot.remove(thread) else roomsSnapshot[thread] = parts.toList() }
        if (parts != null) emit(MessengerEvent.RoomChanged(RoomInfo(thread, parts.toList())))
    }

    private fun sendGroupOp(userId: String, thread: String, op: GroupMsgOp) {
        val b = XmlMessage()
        b.addData(Xn.THREAD, thread)
        b.addData(Xn.GROUP_MSG_OP, op.wire)
        transmit(userId, MessageType.GROUP_MESSAGE, msgIdSeq.getAndIncrement(), b)
    }

    private fun createLocalRoom(thread: String) {
        rooms[thread] = mutableListOf(localId)
        publishRoom(thread)
        for (uid in users.keys.toList()) sendGroupOp(uid, thread, GroupMsgOp.JOIN)
    }

    private fun addRoomParticipant(thread: String, uid: String): Boolean {
        val parts = rooms[thread] ?: return false
        val u = users[uid]
        // peers older than 1.2.10 predate group chat and would not understand the traffic
        if (u != null && uid != localId && Proto.compareVersions(u.version, "1.2.10") <= 0) return false
        if (uid in parts) return false
        parts.add(uid)
        publishRoom(thread)
        return true
    }

    private fun processGroupMessage(from: String, m: XmlMessage) {
        val thread = m.data(Xn.THREAD)
        if (thread.isEmpty()) return
        val op = GroupMsgOp.fromWire(m.data(Xn.GROUP_MSG_OP)) ?: return
        if (op == GroupMsgOp.REQUEST) {
            if (!rooms.containsKey(thread)) createLocalRoom(thread)
            addRoomParticipant(thread, from)
            return
        }
        val parts = rooms[thread] ?: return
        when (op) {
            GroupMsgOp.JOIN -> if (addRoomParticipant(thread, from)) emit(MessengerEvent.RoomNotice(thread, from, true, now()))
            GroupMsgOp.MESSAGE -> emit(MessengerEvent.RoomMessage(thread, from, m.data(Xn.MESSAGE), now()))
            GroupMsgOp.LEAVE -> if (parts.remove(from)) {
                publishRoom(thread)
                emit(MessengerEvent.RoomNotice(thread, from, false, now()))
            }
            else -> {}
        }
    }

    // ==========================================================================================
    // file transfer (files, avatars) and folder decline
    // ==========================================================================================

    fun sendFile(userId: String, source: FileSource): String {
        val fileId = UUID.randomUUID().toString().replace("-", "").uppercase()
        post { requestSend(userId, fileId, source, FileType.NORMAL) }
        return fileId
    }

    private fun sendAvatarTo(userId: String) {
        val f = localAvatar ?: return
        if (!f.isFile) return
        requestSend(userId, UUID.randomUUID().toString().replace("-", "").uppercase(), LocalFileSource(f), FileType.AVATAR)
    }

    private fun requestSend(userId: String, fileId: String, source: FileSource, type: FileType, extra: ((XmlMessage) -> Unit)? = null, folderId: String = "") {
        if (!users.containsKey(userId)) return
        val t = Transfer(fileId, userId, source.name, source.size, true, type, TransferState.REQUESTED, source = source, folderId = folderId)
        transfers[fileId] = t
        val b = XmlMessage()
        b.addData(Xn.FILE_TYPE, type.wire)
        b.addData(Xn.FILE_OP, FileOp.REQUEST.wire)
        b.addData(Xn.FILE_ID, fileId)
        b.addData(Xn.MODE, FileMode.SEND.wire)
        b.addData(Xn.FILE_NAME, source.name)
        b.addData(Xn.FILE_SIZE, source.size.toString())
        extra?.invoke(b)
        val mt = if (type == FileType.AVATAR) MessageType.AVATAR else MessageType.FILE
        transmit(userId, mt, msgIdSeq.getAndIncrement(), b)
        if (type == FileType.NORMAL) emitTransfer(t)
    }

    /** Accept an incoming file offer; the data will be written through [sink]. */
    fun acceptFile(userId: String, fileId: String, sink: FileSink) {
        post {
            val t = transfers[fileId] ?: return@post
            if (t.outgoing || t.userId != userId) return@post
            t.sink = sink
            t.state = TransferState.TRANSFERRING
            val b = XmlMessage()
            b.addData(Xn.MODE, FileMode.RECEIVE.wire)
            b.addData(Xn.FILE_TYPE, t.type.wire)
            b.addData(Xn.FILE_OP, FileOp.ACCEPT.wire)
            b.addData(Xn.FILE_ID, fileId)
            b.addData(Xn.FILE_NAME, t.name)
            b.addData(Xn.FILE_SIZE, t.size.toString())
            tcp?.let { b.addData(Xn.TCP_PORT, it.boundPort.toString()) }
            transmit(userId, if (t.type == FileType.AVATAR) MessageType.AVATAR else MessageType.FILE, msgIdSeq.getAndIncrement(), b)
            emitTransfer(t)
        }
    }

    fun declineFile(userId: String, fileId: String) {
        post {
            val t = transfers.remove(fileId) ?: return@post
            val b = XmlMessage()
            b.addData(Xn.MODE, FileMode.RECEIVE.wire)
            b.addData(Xn.FILE_TYPE, t.type.wire)
            b.addData(Xn.FILE_OP, FileOp.DECLINE.wire)
            b.addData(Xn.FILE_ID, fileId)
            transmit(userId, MessageType.FILE, msgIdSeq.getAndIncrement(), b)
            t.state = TransferState.DECLINED
            emitTransfer(t)
        }
    }

    fun cancelFile(userId: String, fileId: String) {
        post {
            val t = transfers[fileId] ?: return@post
            val b = XmlMessage()
            b.addData(Xn.MODE, (if (t.outgoing) FileMode.SEND else FileMode.RECEIVE).wire)
            b.addData(Xn.FILE_TYPE, t.type.wire)
            b.addData(Xn.FILE_OP, FileOp.CANCEL.wire)
            b.addData(Xn.FILE_ID, fileId)
            transmit(userId, MessageType.FILE, msgIdSeq.getAndIncrement(), b)
            cancelTransferLocally(t, TransferState.CANCELLED)
        }
    }

    private fun emitTransfer(t: Transfer, location: String? = null) {
        if (t.type == FileType.FOLDER) {
            folders[t.folderId]?.let { emitFolder(it) }
            return
        }
        if (t.type != FileType.NORMAL) return
        emit(MessengerEvent.Transfer(TransferInfo(t.id, t.userId, t.name, t.size, t.bytes, t.outgoing, t.state, location)))
    }

    private fun cancelTransferLocally(t: Transfer, state: TransferState) {
        t.cancelled = true
        runCatching { t.socket?.close() }
        t.sink?.let { runCatching { it.discard() } }
        transfers.remove(t.id)
        if (t.state != TransferState.COMPLETE) {
            t.state = state
            emitTransfer(t)
        }
    }

    private fun processFile(from: String, type: MessageType, m: XmlMessage) {
        val ft = FileType.fromWire(m.data(Xn.FILE_TYPE)) ?: FileType.NORMAL
        val op = FileOp.fromWire(m.data(Xn.FILE_OP)) ?: return
        val fileId = m.data(Xn.FILE_ID)
        if (fileId.length != Proto.FILE_ID_LENGTH) return
        when (op) {
            FileOp.REQUEST -> {
                val size = m.data(Xn.FILE_SIZE).toLongLong()
                val name = sanitizeFileName(m.data(Xn.FILE_NAME))
                val t = Transfer(fileId, from, name, size, false, ft, TransferState.REQUESTED)
                if (ft == FileType.FOLDER) {
                    // one file of a folder we accepted: take it without asking again
                    val folderId = m.data(Xn.FOLDER_ID)
                    val folder = folders[folderId]
                    if (folder == null || folder.outgoing || !folder.accepted || folder.userId != from) {
                        val d = XmlMessage()
                        d.addData(Xn.MODE, FileMode.RECEIVE.wire)
                        d.addData(Xn.FILE_TYPE, FileType.FOLDER.wire)
                        d.addData(Xn.FILE_OP, FileOp.DECLINE.wire)
                        d.addData(Xn.FILE_ID, fileId)
                        transmit(from, MessageType.FILE, msgIdSeq.getAndIncrement(), d)
                        return
                    }
                    t.folderId = folderId
                    t.sink = folder.sinks!!.sink(folder.name, safeRelative(m.data(Xn.REL_PATH)).replace(File.separatorChar, '/'))
                    t.state = TransferState.TRANSFERRING
                    folder.currentFileId = fileId
                    transfers[fileId] = t
                    val a = XmlMessage()
                    a.addData(Xn.MODE, FileMode.RECEIVE.wire)
                    a.addData(Xn.FILE_TYPE, FileType.FOLDER.wire)
                    a.addData(Xn.FILE_OP, FileOp.ACCEPT.wire)
                    a.addData(Xn.FILE_ID, fileId)
                    a.addData(Xn.FILE_SIZE, size.toString())
                    tcp?.let { a.addData(Xn.TCP_PORT, it.boundPort.toString()) }
                    transmit(from, MessageType.FILE, msgIdSeq.getAndIncrement(), a)
                    return
                }
                transfers[fileId] = t
                if (ft == FileType.AVATAR) {
                    // avatars are accepted silently into the cache directory
                    if (size in 1..(2 * 1024 * 1024)) {
                        t.sink = LocalFileSink(File(cfg.cacheDir, "avt_${safeName(from)}.png"))
                        t.state = TransferState.TRANSFERRING
                        val b = XmlMessage()
                        b.addData(Xn.MODE, FileMode.RECEIVE.wire)
                        b.addData(Xn.FILE_TYPE, FileType.AVATAR.wire)
                        b.addData(Xn.FILE_OP, FileOp.ACCEPT.wire)
                        b.addData(Xn.FILE_ID, fileId)
                        b.addData(Xn.FILE_SIZE, size.toString())
                        tcp?.let { b.addData(Xn.TCP_PORT, it.boundPort.toString()) }
                        transmit(from, MessageType.AVATAR, msgIdSeq.getAndIncrement(), b)
                    } else transfers.remove(fileId)
                } else emitTransfer(t)
            }
            FileOp.ACCEPT -> {
                val t = transfers[fileId] ?: return
                if (!t.outgoing || t.userId != from) return
                t.state = TransferState.TRANSFERRING
                emitTransfer(t)
                val addr = streams[from]?.peerAddress ?: users[from]?.address ?: return
                // The original client's receiver reads at most 64 KiB per readyRead() and never
                // drains a backlog, so a sender that outruns it stalls the transfer on the last
                // chunk. Peers older than 3.0.0 therefore get a paced stream (see PROTOCOL.md).
                val paced = Proto.compareVersions(users[from]?.version ?: "0.0.0", "3.0.0") < 0
                val port = peerPort(m)
                io.execute { sendFileLoop(t, addr, port, paced) }
            }
            FileOp.DECLINE -> {
                val t = transfers.remove(fileId) ?: return
                t.state = TransferState.DECLINED
                emitTransfer(t)
            }
            FileOp.CANCEL, FileOp.ABORT -> {
                val t = transfers[fileId] ?: return
                if (t.userId != from) return
                if (t.type == FileType.FOLDER) folders[t.folderId]?.let { endFolder(it, TransferState.CANCELLED); return }
                cancelTransferLocally(t, TransferState.CANCELLED)
            }
            else -> {}
        }
    }

    // ---- folders ----------------------------------------------------------------------------------

    /** Offers a whole folder (all files below it) to [userId]. Returns the folder id. */
    fun sendFolder(userId: String, dir: File): String? = if (dir.isDirectory) sendFolder(userId, LocalFolderSource(dir)) else null

    fun sendFolder(userId: String, source: FolderSource): String {
        val folderId = UUID.randomUUID().toString().replace("-", "").uppercase()
        io.execute {
            // listing may take a while (document trees): off the engine thread
            val entries = try { source.entries() } catch (e: Exception) { log("folder ${source.name}: $e"); return@execute }
            post {
                val u = users[userId] ?: return@post
                if (u.caps and Proto.UC_FOLDER == 0) return@post
                val f = FolderTx(folderId, userId, true, name = source.name)
                // the original client's receiver cannot take an empty file (it aborts the whole folder), so older peers do not get them
                val skipEmpty = Proto.compareVersions(u.version, "3.0.0") < 0
                for (e in entries) {
                    if (e.size == 0L && skipEmpty) continue
                    f.files.add(e)
                    f.size += e.size
                }
                f.count = f.files.size
                if (f.count == 0) return@post
                folders[folderId] = f
                val b = XmlMessage()
                b.addData(Xn.FOLDER_ID, folderId)
                b.addData(Xn.FILE_ID, folderId)
                b.addData(Xn.FILE_TYPE, FileType.NORMAL.wire)
                b.addData(Xn.FILE_OP, FileOp.REQUEST.wire)
                b.addData(Xn.MODE, FileMode.SEND.wire)
                b.addData(Xn.FILE_NAME, f.name)
                b.addData(Xn.FILE_SIZE, f.size.toString())
                b.addData(Xn.FILE_COUNT, f.count.toString())
                transmit(userId, MessageType.FOLDER, msgIdSeq.getAndIncrement(), b)
                emitFolder(f)
            }
        }
        return folderId
    }

    /** Accept an offered folder; it is stored in a new sub-folder of [parentDir]. */
    fun acceptFolder(userId: String, folderId: String, parentDir: File) {
        post {
            val f = folders[folderId] ?: return@post
            val base = sanitizeFileName(f.name)
            var dir = File(parentDir, base)
            var i = 1
            while (dir.exists()) dir = File(parentDir, "$base [${i++}]")
            if (!dir.mkdirs()) { log("folder ${dir}: cannot create"); return@post }
            f.location = dir.absolutePath
            acceptFolderWith(userId, folderId) { _, rel -> LocalFileSink(File(dir, safeRelative(rel))) }
        }
    }

    /** Accept an offered folder; [sinks] decides where each file goes (Android: MediaStore). */
    fun acceptFolder(userId: String, folderId: String, sinks: FolderSinkFactory, location: String? = null) {
        post {
            folders[folderId]?.location = location
            acceptFolderWith(userId, folderId, sinks)
        }
    }

    private fun acceptFolderWith(userId: String, folderId: String, sinks: FolderSinkFactory) {
        val f = folders[folderId] ?: return
        if (f.outgoing || f.userId != userId || f.accepted) return
        f.sinks = sinks
        f.accepted = true
        f.state = TransferState.TRANSFERRING
        val b = XmlMessage()
        b.addData(Xn.MODE, FileMode.RECEIVE.wire)
        b.addData(Xn.FILE_TYPE, FileType.NORMAL.wire)
        b.addData(Xn.FILE_OP, FileOp.ACCEPT.wire)
        b.addData(Xn.FILE_ID, folderId)
        b.addData(Xn.FOLDER_ID, folderId)
        tcp?.let { b.addData(Xn.TCP_PORT, it.boundPort.toString()) }
        transmit(userId, MessageType.FOLDER, msgIdSeq.getAndIncrement(), b)
        emitFolder(f)
    }

    fun declineFolder(userId: String, folderId: String) {
        post {
            val f = folders[folderId] ?: return@post
            if (f.userId != userId) return@post
            sendFolderOp(f, FileOp.DECLINE, FileMode.RECEIVE)
            folders.remove(folderId)
            f.state = TransferState.DECLINED
            emitFolder(f)
        }
    }

    fun cancelFolder(userId: String, folderId: String) {
        post {
            val f = folders[folderId] ?: return@post
            if (f.userId != userId) return@post
            sendFolderOp(f, FileOp.CANCEL, if (f.outgoing) FileMode.SEND else FileMode.RECEIVE)
            endFolder(f, TransferState.CANCELLED)
        }
    }

    private fun sendFolderOp(f: FolderTx, op: FileOp, mode: FileMode) {
        val b = XmlMessage()
        b.addData(Xn.MODE, mode.wire)
        b.addData(Xn.FILE_TYPE, FileType.NORMAL.wire)
        b.addData(Xn.FILE_OP, op.wire)
        b.addData(Xn.FILE_ID, f.id)
        b.addData(Xn.FOLDER_ID, f.id)
        transmit(f.userId, MessageType.FOLDER, msgIdSeq.getAndIncrement(), b)
    }

    private fun endFolder(f: FolderTx, state: TransferState) {
        folders.remove(f.id)
        f.currentFileId?.let { id -> transfers[id]?.let { cancelTransferLocally(it, state) } }
        f.state = state
        emitFolder(f)
    }

    private fun emitFolder(f: FolderTx, location: String? = null) {
        val current = f.currentFileId?.let { transfers[it]?.bytes } ?: 0L
        emit(MessengerEvent.Transfer(TransferInfo(f.id, f.userId, f.name, f.size, f.doneBytes + current, f.outgoing, f.state, location ?: f.location, isFolder = true, fileCount = f.count)))
    }

    private fun processFolder(from: String, m: XmlMessage) {
        val op = FileOp.fromWire(m.data(Xn.FILE_OP)) ?: return
        val folderId = m.data(Xn.FOLDER_ID).ifEmpty { m.data(Xn.FILE_ID) }
        if (folderId.isEmpty()) return
        when (op) {
            FileOp.REQUEST -> {
                val f = FolderTx(folderId, from, false, name = sanitizeFileName(m.data(Xn.FILE_NAME)),
                    size = m.data(Xn.FILE_SIZE).toLongLong(), count = m.data(Xn.FILE_COUNT).toIntOrNull() ?: 0)
                folders[folderId] = f
                emitFolder(f)
            }
            FileOp.ACCEPT -> {
                val f = folders[folderId] ?: return
                if (!f.outgoing || f.userId != from) return
                f.accepted = true
                f.state = TransferState.TRANSFERRING
                emitFolder(f)
                sendNextFolderFile(f)
            }
            FileOp.DECLINE -> {
                val f = folders[folderId] ?: return
                if (f.userId != from) return
                folders.remove(folderId)
                f.state = TransferState.DECLINED
                emitFolder(f)
            }
            FileOp.CANCEL, FileOp.ABORT -> {
                val f = folders[folderId] ?: return
                if (f.userId != from) return
                endFolder(f, TransferState.CANCELLED)
            }
            else -> {}
        }
    }

    private fun sendNextFolderFile(f: FolderTx) {
        if (f.done >= f.count) {
            folders.remove(f.id)
            f.state = TransferState.COMPLETE
            emitFolder(f, f.location)
            return
        }
        val entry = f.files[f.done]
        val fileId = UUID.randomUUID().toString().replace("-", "").uppercase()
        f.currentFileId = fileId
        val rel = entry.relPath
        val source = object : FileSource {
            override val name = entry.relPath.substringAfterLast('/')
            override val size = entry.size
            override fun open() = entry.open()
        }
        requestSend(f.userId, fileId, source, FileType.FOLDER, { b ->
            b.addData(Xn.FOLDER_ID, f.id)
            b.addData(Xn.REL_PATH, rel)
        }, f.id)
    }

    /** A file of a folder finished (or failed): go on with the next one. */
    private fun onFolderFileFinished(t: Transfer, ok: Boolean) {
        val f = folders[t.folderId] ?: return
        f.currentFileId = null
        if (!ok) {
            if (f.state == TransferState.TRANSFERRING) {
                sendFolderOp(f, FileOp.ABORT, if (f.outgoing) FileMode.SEND else FileMode.RECEIVE)
                endFolder(f, TransferState.ERROR)
            }
            return
        }
        f.done++
        f.doneBytes += t.size
        if (f.outgoing) sendNextFolderFile(f)
        else if (f.done >= f.count) {
            folders.remove(f.id)
            f.state = TransferState.COMPLETE
            emitFolder(f, f.location)
        } else emitFolder(f)
    }

    /** The folder path a peer sent, made safe: no drive letters, no ".." and no characters Windows rejects. */
    private fun safeRelative(rel: String): String {
        val parts = rel.replace('\\', '/').split('/').filter { it.isNotEmpty() && it != "." && it != ".." }.map { sanitizeFileName(it) }
        return if (parts.isEmpty()) "file" else parts.joinToString(File.separator)
    }

    /** Dedicated thread: connect to the receiver, wait for START, stream the file raw. */
    private fun sendFileLoop(t: Transfer, ip: String, port: Int, paced: Boolean) {
        var ok = false
        try {
            val s = Socket()
            t.socket = s
            s.connect(InetSocketAddress(ip, port), 8000)
            s.soTimeout = 30000
            s.tcpNoDelay = true
            val out = s.getOutputStream()
            out.write(("FILE" + t.id + localId).toByteArray(Charsets.UTF_8))
            out.flush()
            val start = ByteArray(5)
            var got = 0
            val inp = s.getInputStream()
            while (got < 5) {
                val n = inp.read(start, got, 5 - got)
                if (n < 0) throw java.io.IOException("peer closed before START")
                got += n
            }
            if (String(start, Charsets.US_ASCII) != "START") throw java.io.IOException("bad START")
            val src = t.source ?: return
            src.open().use { fin ->
                // 65535, not 65536: the original receiver reads at most 65535 bytes per readyRead()
                val buf = ByteArray(FILE_CHUNK)
                var sent = 0L
                var lastReport = now()
                while (sent < t.size && !t.cancelled) {
                    val want = minOf(buf.size.toLong(), t.size - sent).toInt()
                    val n = fin.read(buf, 0, want)
                    if (n < 0) break
                    out.write(buf, 0, n)
                    sent += n
                    if (paced) {
                        out.flush()
                        Thread.sleep(PACED_DELAY_MS)
                    }
                    if (now() - lastReport >= 1500) {
                        lastReport = now()
                        val snap = sent
                        post { t.bytes = snap; emitTransfer(t) }
                    }
                }
                out.flush()
                ok = sent == t.size && !t.cancelled
                t.bytes = sent
            }
            // linger briefly so the receiver reads everything before the FIN
            if (ok) try { s.shutdownOutput(); inp.read() } catch (_: Exception) {}
            s.close()
        } catch (e: Exception) {
            log("send file ${t.name} failed: ${e.message}")
        }
        post {
            if (t.cancelled) return@post
            transfers.remove(t.id)
            if (ok) {
                t.state = TransferState.COMPLETE
                if (t.type == FileType.FOLDER) onFolderFileFinished(t, true) else emitTransfer(t)
            } else {
                t.state = TransferState.ERROR
                if (t.type == FileType.FOLDER) onFolderFileFinished(t, false) else emitTransfer(t)
                abortOnWire(t)
            }
        }
    }

    private fun abortOnWire(t: Transfer) {
        val b = XmlMessage()
        b.addData(Xn.MODE, (if (t.outgoing) FileMode.SEND else FileMode.RECEIVE).wire)
        b.addData(Xn.FILE_TYPE, t.type.wire)
        b.addData(Xn.FILE_OP, FileOp.ABORT.wire)
        b.addData(Xn.FILE_ID, t.id)
        transmit(t.userId, if (t.type == FileType.AVATAR) MessageType.AVATAR else MessageType.FILE, msgIdSeq.getAndIncrement(), b)
    }

    /** Runs on the accepted socket's thread. */
    private fun receiveFileLoop(t: Transfer, s: Socket) {
        t.socket = s
        var ok = false
        var location: String? = null
        val sink = t.sink
        if (sink == null) {
            s.close(); return
        }
        try {
            val out = sink.open()
            s.getOutputStream().write("START".toByteArray(Charsets.US_ASCII))
            s.getOutputStream().flush()
            val inp = s.getInputStream()
            val buf = ByteArray(64 * 1024)
            var got = 0L
            var lastReport = now()
            out.use {
                while (got < t.size && !t.cancelled) {
                    val n = inp.read(buf, 0, minOf(buf.size.toLong(), t.size - got).toInt())
                    if (n < 0) break
                    it.write(buf, 0, n)
                    got += n
                    if (now() - lastReport >= 1500) {
                        lastReport = now()
                        val snap = got
                        post { t.bytes = snap; emitTransfer(t) }
                    }
                }
            }
            t.bytes = got
            ok = got == t.size && !t.cancelled
            if (ok) location = sink.commit() else sink.discard()
        } catch (e: Exception) {
            log("receive file ${t.name} failed: ${e.message}")
            runCatching { sink.discard() }
        } finally {
            runCatching { s.close() }
        }
        post {
            if (t.cancelled) return@post
            transfers.remove(t.id)
            if (ok) {
                t.state = TransferState.COMPLETE
                if (t.type == FileType.AVATAR) {
                    updateUser(t.userId) { it.copy(avatarPath = location) }
                    emit(MessengerEvent.AvatarUpdated(t.userId, location))
                } else if (t.type == FileType.FOLDER) onFolderFileFinished(t, true)
                else emitTransfer(t, location)
            } else {
                t.state = TransferState.ERROR
                if (t.type == FileType.FOLDER) onFolderFileFinished(t, false) else emitTransfer(t)
                abortOnWire(t)
            }
        }
    }

    companion object {
        /** Qt-style font string old Windows clients can parse; our own UI ignores incoming fonts. */
        const val DEFAULT_FONT = "Segoe UI,9,-1,5,50,0,0,0,0,0"
        const val DEFAULT_COLOR = "#000000"
        const val PACED_DELAY_MS = 3L
        const val FILE_CHUNK = 65535

        fun buildUserId(machineId: String, logonName: String): String {
            val mac = machineId.filter { it.isLetterOrDigit() }.uppercase().take(12)
            val name = logonName.filter { it.isLetterOrDigit() || it == '_' || it == '-' }
            return (mac + name).take(Proto.MAX_USER_ID_LENGTH)
        }

        fun safeName(s: String) = s.filter { it.isLetterOrDigit() || it == '_' || it == '-' }.take(64).ifEmpty { "x" }

        fun sanitizeFileName(name: String): String {
            val base = name.substringAfterLast('/').substringAfterLast('\\')
                .filter { it >= ' ' && it !in "<>:\"|?*" }.trim().trim('.')
            return base.ifEmpty { "file" }.take(180)
        }

        private fun String.toLongLong(): Long = toLongOrNull() ?: 0L
    }
}
