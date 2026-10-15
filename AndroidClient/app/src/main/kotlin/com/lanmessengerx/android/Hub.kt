package com.lanmessengerx.android

import android.content.Context
import android.net.Uri
import android.os.Build
import android.os.Handler
import android.os.Looper
import com.lanmessengerx.core.ChatStateKind
import com.lanmessengerx.core.Messenger
import com.lanmessengerx.core.MessengerConfig
import com.lanmessengerx.core.MessengerEvent
import com.lanmessengerx.core.TransferInfo
import com.lanmessengerx.core.TransferState
import com.lanmessengerx.core.User
import java.io.File
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.Executors

/**
 * Process wide application state: owns the protocol engine, conversations, rooms and
 * profile. Everything that the UI reads is confined to the main thread; engine events are
 * marshalled over before they touch any state.
 */
object Hub {
    interface Listener {
        fun onHubChanged()
    }

    private lateinit var app: Context
    lateinit var prefs: Prefs
        private set
    lateinit var history: HistoryStore
        private set
    private val main = Handler(Looper.getMainLooper())
    private val io = Executors.newSingleThreadExecutor()
    private val listeners = CopyOnWriteArrayList<Listener>()
    private var initialised = false
    private var engine: Messenger? = null

    var connected = false
        private set
    var ip: String? = null
        private set
    val contacts = ArrayList<User>()
    /** The public chat: everybody on the network who has it open sees what is written here. */
    val publicRoom = Room("public")
    fun roomFor(id: String): Room? = if (id == "public") publicRoom else rooms[id]
    val chats = HashMap<String, Chat>()
    val rooms = LinkedHashMap<String, Room>()
    var appVisible = false
    var openChat: String? = null
    var openRoom: String? = null

    val localUserName: String get() = prefs.name
    val localId: String get() = engine?.localId ?: ""
    val avatarFile: File get() = File(app.filesDir, "avatar.png")
    val hasAvatar: Boolean get() = avatarFile.isFile

    fun init(ctx: Context) {
        if (initialised) return
        initialised = true
        app = ctx.applicationContext
        prefs = Prefs(app)
        Loc.use(app, prefs.language)
        history = HistoryStore(app)
        Notifier.ensureChannels(app)
        if (prefs.name.isBlank()) prefs.name = defaultName()
        // like the original: start with a random stock picture so nobody shows up as a blank circle
        if (!prefs.avatarChosen && !hasAvatar) useStockAvatar(kotlin.random.Random.nextInt(Avatars.STOCK_COUNT), chosen = false)
    }

    private fun defaultName(): String {
        val dev = try {
            android.provider.Settings.Global.getString(app.contentResolver, "device_name")
        } catch (_: Exception) {
            null
        }
        return (dev ?: Build.MODEL ?: "Android").take(24)
    }

    fun addListener(l: Listener) {
        if (!listeners.contains(l)) listeners.add(l)
    }

    fun removeListener(l: Listener) {
        listeners.remove(l)
    }

    private fun changed() {
        for (l in listeners) l.onHubChanged()
    }

    // ---- engine lifecycle -----------------------------------------------------------------------

    fun startEngine() {
        if (engine != null) return
        val cfg = MessengerConfig(
            userName = prefs.name,
            status = prefs.status,
            note = prefs.note,
            machineId = prefs.machineId,
            logonName = "android",
            hostName = Build.MODEL ?: "Android",
            osName = "Android ${Build.VERSION.RELEASE}",
            cacheDir = File(app.filesDir, "avatars").also { it.mkdirs() },
        )
        val m = Messenger(cfg, log = { android.util.Log.d("LMX", it) }) { e -> main.post { onEvent(e) } }
        engine = m
        m.start()
        if (hasAvatar) m.setLocalAvatar(avatarFile)
        changed()
    }

    fun shutdown() {
        engine?.stop()
        engine = null
        connected = false
        contacts.clear()
        changed()
    }

    // ---- events from the engine ---------------------------------------------------------------

    private fun chatFor(userId: String): Chat {
        val c = chats.getOrPut(userId) { Chat(userId, userId) }
        contacts.firstOrNull { it.id == userId }?.let { c.name = it.name }
        return c
    }

    private fun nameOf(userId: String): String =
        contacts.firstOrNull { it.id == userId }?.name ?: chats[userId]?.name ?: userId

    private fun sortContacts() {
        contacts.sortWith(compareBy<User> { it.isOffline }.thenBy { it.name.lowercase() })
    }

    private fun onEvent(e: MessengerEvent) {
        when (e) {
            is MessengerEvent.ConnectionChanged -> {
                connected = e.connected
                ip = e.ip
                Notifier.updateService(app, if (e.connected) Loc.f("Online · {0}", e.ip ?: "") else Loc.t("Waiting for a network…"))
            }
            is MessengerEvent.UserOnline -> {
                contacts.removeAll { it.id == e.user.id }
                contacts.add(e.user)
                sortContacts()
                chatFor(e.user.id)
            }
            is MessengerEvent.UserUpdated -> {
                val i = contacts.indexOfFirst { it.id == e.user.id }
                if (i >= 0) contacts[i] = e.user else contacts.add(e.user)
                chats[e.user.id]?.name = e.user.name
                sortContacts()
            }
            is MessengerEvent.UserOffline -> {
                contacts.removeAll { it.id == e.userId }
                chats[e.userId]?.let { it.typing = false }
            }
            is MessengerEvent.AvatarUpdated -> {
                val i = contacts.indexOfFirst { it.id == e.userId }
                if (i >= 0) contacts[i] = contacts[i].copy(avatarPath = e.path)
                Avatars.invalidate(e.userId)
            }
            is MessengerEvent.TextMessage -> {
                val c = chatFor(e.userId)
                c.typing = false
                c.items.add(ChatItem.Msg(e.text, false, c.name, e.timeMs, e.messageId))
                c.lastTime = e.timeMs
                saveHistory(e.userId, c.name, c.name, e.text, false, e.timeMs)
                if (openChat != e.userId || !appVisible) {
                    c.unread++
                    if (prefs.notifications) Notifier.message(app, e.userId, "user", c.name, e.text)
                }
            }
            is MessengerEvent.MessageDelivered -> {
                chats[e.userId]?.items?.forEach { if (it is ChatItem.Msg && it.msgId == e.messageId) it.state = Delivery.DELIVERED }
            }
            is MessengerEvent.MessageFailed -> {
                chats[e.userId]?.items?.forEach { if (it is ChatItem.Msg && it.msgId == e.messageId) it.state = Delivery.FAILED }
            }
            is MessengerEvent.BroadcastMessage -> {
                val c = chatFor(e.userId)
                c.items.add(ChatItem.Broadcast(e.text, c.name, false, e.timeMs))
                c.lastTime = e.timeMs
                saveHistory(e.userId, c.name, c.name, "[broadcast] ${e.text}", false, e.timeMs)
                if (openChat != e.userId || !appVisible) {
                    c.unread++
                    if (prefs.notifications) Notifier.message(app, e.userId, "user", Loc.f("{0} · broadcast", c.name), e.text)
                }
            }
            is MessengerEvent.ChatStateChanged -> {
                if (e.threadId.isEmpty()) {
                    val c = chatFor(e.userId)
                    c.typing = e.state == ChatStateKind.COMPOSING
                    if (c.typing) main.postDelayed({ c.typing = false; changed() }, 10000)
                }
            }
            is MessengerEvent.RoomChanged -> {
                val r = rooms.getOrPut(e.room.threadId) { Room(e.room.threadId) }
                r.participants = e.room.participants
            }
            is MessengerEvent.RoomClosed -> rooms.remove(e.threadId)
            is MessengerEvent.RoomNotice -> {
                val r = rooms[e.threadId] ?: return
                r.items.add(ChatItem.Notice(Loc.f(if (e.joined) "{0} joined" else "{0} left", nameOf(e.userId)), e.timeMs))
            }
            is MessengerEvent.RoomMessage -> {
                val r = rooms.getOrPut(e.threadId) { Room(e.threadId) }
                val who = nameOf(e.userId)
                r.items.add(ChatItem.Msg(e.text, false, who, e.timeMs, 0))
                r.lastTime = e.timeMs
                saveHistory("group", Loc.t("Group conversations"), who, e.text, false, e.timeMs)
                if (openRoom != e.threadId || !appVisible) {
                    r.unread++
                    if (prefs.notifications) Notifier.message(app, e.threadId, "room", Loc.f("Group · {0}", who), e.text)
                }
            }
            is MessengerEvent.PublicMessage -> {
                val who = nameOf(e.userId)
                publicRoom.items.add(ChatItem.Msg(e.text, false, who, e.timeMs, 0))
                publicRoom.lastTime = e.timeMs
                saveHistory("public", Loc.t("Public chat"), who, e.text, false, e.timeMs)
                if (openRoom != "public" || !appVisible) {
                    publicRoom.unread++
                    if (prefs.notifications) Notifier.message(app, "public", "room", Loc.f("Public chat · {0}", who), e.text)
                }
            }
            is MessengerEvent.Transfer -> onTransfer(e.info)
            is MessengerEvent.UserInfo -> infoListener?.invoke(e.userId, e.info)
            else -> {}
        }
        changed()
    }

    var infoListener: ((String, Map<String, String>) -> Unit)? = null

    private fun onTransfer(t: TransferInfo) {
        val c = chatFor(t.userId)
        val existing = c.items.firstOrNull { it is ChatItem.FileItem && it.fileId == t.id } as ChatItem.FileItem?
        if (existing == null) {
            c.items.add(ChatItem.FileItem(t.id, t.name, t.size, t.outgoing, t.state, t.bytes, t.location, System.currentTimeMillis(), t.isFolder, t.fileCount))
            c.lastTime = System.currentTimeMillis()
            if (!t.outgoing && t.state == TransferState.REQUESTED) {
                if (openChat != t.userId || !appVisible) {
                    c.unread++
                    if (prefs.notifications) Notifier.message(app, t.userId, "user", c.name, Loc.f("Wants to send you {0}", t.name))
                }
            }
        } else {
            existing.state = t.state
            existing.bytes = t.bytes
            if (t.location != null) existing.location = t.location
        }
    }

    private fun saveHistory(key: String, title: String, sender: String, text: String, outgoing: Boolean, time: Long) {
        if (!prefs.saveHistory) return
        io.execute { history.append(key, title, HistoryStore.Entry(time, outgoing, sender, text)) }
    }

    // ---- actions (main thread) ----------------------------------------------------------------

    val totalUnread: Int get() = chats.values.sumOf { it.unread } + rooms.values.sumOf { it.unread } + publicRoom.unread

    fun sendText(userId: String, text: String) {
        val m = engine ?: return
        val c = chatFor(userId)
        val now = System.currentTimeMillis()
        val id = m.sendText(userId, text)
        c.items.add(ChatItem.Msg(text, true, localUserName, now, id, Delivery.SENDING))
        c.lastTime = now
        saveHistory(userId, c.name, localUserName, text, true, now)
        changed()
    }

    fun sendBroadcast(text: String) {
        val m = engine ?: return
        val ids = contacts.filter { !it.isOffline }.map { it.id }
        m.sendBroadcastText(ids, text)
        val now = System.currentTimeMillis()
        for (id in ids) chatFor(id).items.add(ChatItem.Broadcast(text, localUserName, true, now))
        changed()
    }

    fun sendFile(userId: String, uri: Uri) {
        val m = engine ?: return
        m.sendFile(userId, UriFileSource(app, uri))
    }

    fun sendFolder(userId: String, tree: Uri) {
        val m = engine ?: return
        m.sendFolder(userId, TreeFolderSource(app, tree))
    }

    fun acceptFile(userId: String, fileId: String) {
        val item = chats[userId]?.items?.firstOrNull { it is ChatItem.FileItem && it.fileId == fileId } as ChatItem.FileItem? ?: return
        if (item.isFolder) {
            engine?.acceptFolder(userId, fileId, com.lanmessengerx.core.FolderSinkFactory { folder, rel ->
                val sub = rel.substringBeforeLast('/', "")
                ReceivedFileSink(app, rel.substringAfterLast('/'), if (sub.isEmpty()) folder else "$folder/$sub")
            }, "Downloads/LAN Messenger X/${item.name}")
        } else engine?.acceptFile(userId, fileId, ReceivedFileSink(app, item.name))
    }

    fun declineFile(userId: String, fileId: String) {
        if (isFolder(userId, fileId)) engine?.declineFolder(userId, fileId) else engine?.declineFile(userId, fileId)
    }

    fun cancelFile(userId: String, fileId: String) {
        if (isFolder(userId, fileId)) engine?.cancelFolder(userId, fileId) else engine?.cancelFile(userId, fileId)
    }

    private fun isFolder(userId: String, fileId: String) =
        (chats[userId]?.items?.firstOrNull { it is ChatItem.FileItem && it.fileId == fileId } as ChatItem.FileItem?)?.isFolder == true

    fun typing(userId: String, state: ChatStateKind) {
        engine?.sendChatState(userId, state)
    }

    fun requestInfo(userId: String) = engine?.requestUserInfo(userId)

    fun markRead(userId: String) {
        chats[userId]?.unread = 0
        Notifier.cancelFor(app, userId)
        changed()
    }

    fun markRoomRead(threadId: String) {
        roomFor(threadId)?.unread = 0
        Notifier.cancelFor(app, threadId)
        changed()
    }

    fun refresh() = engine?.refresh()

    // groups

    fun createRoom(userIds: Collection<String>): String? {
        val m = engine ?: return null
        val thread = m.createGroupChat(userIds)
        rooms.getOrPut(thread) { Room(thread) }
        changed()
        return thread
    }

    fun sendRoomText(threadId: String, text: String) {
        val m = engine ?: return
        val r = roomFor(threadId) ?: return
        val now = System.currentTimeMillis()
        if (threadId == "public") m.sendPublicText(text) else m.sendGroupText(threadId, text)
        r.items.add(ChatItem.Msg(text, true, localUserName, now, 0))
        r.lastTime = now
        if (threadId == "public") saveHistory("public", Loc.t("Public chat"), localUserName, text, true, now)
        else saveHistory("group", Loc.t("Group conversations"), localUserName, text, true, now)
        changed()
    }

    fun leaveRoom(threadId: String) {
        engine?.leaveGroupChat(threadId)
        rooms.remove(threadId)
        changed()
    }

    fun invite(threadId: String, userIds: Collection<String>) = engine?.inviteToRoom(threadId, userIds)

    fun roomTitle(r: Room): String {
        val names = r.participants.filter { it != localId }.map { nameOf(it) }
        return if (names.isEmpty()) Loc.t("Group chat") else names.joinToString(", ")
    }

    // profile

    fun setName(name: String) {
        val n = name.trim()
        if (n.isEmpty()) return
        prefs.name = n
        engine?.setName(n)
        changed()
    }

    fun setNote(note: String) {
        prefs.note = note
        engine?.setNote(note)
        changed()
    }

    fun setStatus(code: String) {
        prefs.status = code
        engine?.setStatus(code)
        changed()
    }

    /** One of the stock pictures becomes the profile picture. */
    fun useStockAvatar(index: Int, chosen: Boolean = true) {
        try {
            app.assets.open("avatars/$index.png").use { src -> avatarFile.outputStream().use { src.copyTo(it) } }
        } catch (_: Exception) {
            return
        }
        if (chosen) prefs.avatarChosen = true
        engine?.setLocalAvatar(avatarFile)
        Avatars.invalidate("local")
        changed()
    }

    fun setLanguage(code: String) {
        prefs.language = code
        Loc.use(app, code)
    }

    fun avatarSaved() {
        prefs.avatarChosen = true
        engine?.setLocalAvatar(avatarFile)
        Avatars.invalidate("local")
        changed()
    }

    fun removeAvatar() {
        prefs.avatarChosen = true
        avatarFile.delete()
        engine?.setLocalAvatar(null)
        Avatars.invalidate("local")
        changed()
    }

    val status: String get() = prefs.status
    val note: String get() = prefs.note
}
