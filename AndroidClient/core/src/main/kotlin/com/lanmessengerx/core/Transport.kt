package com.lanmessengerx.core

import java.io.DataInputStream
import java.io.EOFException
import java.io.IOException
import java.net.DatagramPacket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.MulticastSocket
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors
import java.util.concurrent.ThreadFactory
import java.util.concurrent.atomic.AtomicInteger

internal object Threads {
    private val counter = AtomicInteger()
    val factory = ThreadFactory { r ->
        Thread(r, "lmx-io-${counter.incrementAndGet()}").also { it.isDaemon = true }
    }

    fun newPool(): ExecutorService = Executors.newCachedThreadPool(factory)
}

/** One framed TCP message stream ("MSG" connection) to a peer. */
internal class Conn(val socket: Socket, val incoming: Boolean, var peerId: String) {
    val peerAddress: String = (socket.inetAddress?.hostAddress) ?: ""

    @Volatile
    var cipher: SessionCipher? = null

    @Volatile
    var closed = false

    /** Opened by the subnet scan: the peer's user id is not known until its first message arrives. */
    @Volatile
    var probe = false

    private val input = DataInputStream(socket.getInputStream().buffered())
    private val output = socket.getOutputStream()
    private val writer = Executors.newSingleThreadExecutor(Threads.factory)

    /** Raw, unframed bytes (only used for the leading "MSG"+id packet). */
    fun sendRaw(data: ByteArray) {
        writer.execute {
            try {
                synchronized(output) {
                    output.write(data)
                    output.flush()
                }
            } catch (e: IOException) {
                close()
            }
        }
    }

    /** 4 byte big endian length, then 6 byte type tag, then data. */
    fun sendFrame(type: DatagramType, data: ByteArray) {
        val tag = type.tag.toByteArray(Charsets.US_ASCII)
        val len = tag.size + data.size
        val buf = ByteArray(4 + len)
        buf[0] = (len ushr 24).toByte()
        buf[1] = (len ushr 16).toByte()
        buf[2] = (len ushr 8).toByte()
        buf[3] = len.toByte()
        System.arraycopy(tag, 0, buf, 4, tag.size)
        System.arraycopy(data, 0, buf, 4 + tag.size, data.size)
        sendRaw(buf)
    }

    /** Blocks until a whole frame arrived; returns null on EOF or protocol violation. */
    fun readFrame(): ByteArray? {
        return try {
            val len = input.readInt()
            if (len < 0 || len > Proto.MAX_FRAME_SIZE) return null
            val buf = ByteArray(len)
            input.readFully(buf)
            buf
        } catch (e: EOFException) {
            null
        } catch (e: IOException) {
            null
        }
    }

    fun close() {
        if (closed) return
        closed = true
        try {
            socket.close()
        } catch (_: IOException) {
        }
        writer.shutdown()
    }
}

/** UDP discovery socket pair: a wildcard bound receiver and an interface bound sender. */
internal class UdpService(
    private val lan: LanInterface,
    private val port: Int,
    private val group: InetAddress,
    private val onDatagram: (fromIp: String, text: String) -> Unit,
    private val log: (String) -> Unit,
) {
    private var receiver: MulticastSocket? = null
    private var sender: MulticastSocket? = null
    private var thread: Thread? = null

    @Volatile
    private var running = false
    var canReceive = false
        private set

    fun start() {
        running = true
        try {
            val r = MulticastSocket(null)
            r.reuseAddress = true
            r.bind(InetSocketAddress(port))
            try {
                r.joinGroup(InetSocketAddress(group, 0), lan.iface)
            } catch (e: IOException) {
                log("Multicast join failed: ${e.message} (broadcast discovery still works)")
            }
            receiver = r
            canReceive = true
        } catch (e: IOException) {
            log("UDP bind to port $port failed: ${e.message}")
            canReceive = false
        }
        try {
            val s = MulticastSocket(null)
            s.reuseAddress = true
            s.bind(InetSocketAddress(lan.address, 0))
            try {
                s.networkInterface = lan.iface
            } catch (_: IOException) {
            }
            s.broadcast = true
            s.timeToLive = 1
            sender = s
        } catch (e: IOException) {
            log("UDP sender setup failed: ${e.message}")
        }
        receiver?.let { r ->
            thread = Thread({
                val buf = ByteArray(65535)
                while (running) {
                    try {
                        val p = DatagramPacket(buf, buf.size)
                        r.receive(p)
                        val text = String(p.data, p.offset, p.length, Charsets.UTF_8)
                        onDatagram(p.address.hostAddress ?: "", text)
                    } catch (e: IOException) {
                        if (running) log("UDP receive error: ${e.message}")
                        break
                    }
                }
            }, "lmx-udp").also { it.isDaemon = true; it.start() }
        }
    }

    fun send(data: ByteArray, targets: Collection<InetAddress>) {
        val s = sender ?: return
        for (t in targets) {
            try {
                s.send(DatagramPacket(data, data.size, t, port))
            } catch (e: IOException) {
                log("UDP send to ${t.hostAddress} failed: ${e.message}")
            }
        }
    }

    fun stop() {
        running = false
        try {
            receiver?.let {
                try {
                    it.leaveGroup(InetSocketAddress(group, 0), lan.iface)
                } catch (_: IOException) {
                }
                it.close()
            }
        } catch (_: Exception) {
        }
        try {
            sender?.close()
        } catch (_: Exception) {
        }
        receiver = null
        sender = null
    }
}

internal class TcpServer(
    private val port: Int,
    private val onSocket: (Socket) -> Unit,
    private val log: (String) -> Unit,
) {
    private var server: ServerSocket? = null

    @Volatile
    private var running = false

    /** The port actually listened on (differs from the requested one if 0 = any free port was asked for). */
    var boundPort = port
        private set

    /** @return true if the port could be bound */
    fun start(): Boolean {
        return try {
            val s = ServerSocket()
            s.reuseAddress = true
            // inherited by accepted sockets: a roomy window keeps the original client's file sender from stalling
            s.receiveBufferSize = 1 shl 20
            s.bind(InetSocketAddress(port), 64)
            boundPort = s.localPort
            server = s
            running = true
            Thread({
                while (running) {
                    try {
                        val c = s.accept()
                        c.tcpNoDelay = true
                        c.keepAlive = true
                        onSocket(c)
                    } catch (e: IOException) {
                        if (running) log("TCP accept error: ${e.message}")
                        break
                    }
                }
            }, "lmx-accept").also { it.isDaemon = true; it.start() }
            true
        } catch (e: IOException) {
            log("TCP listen on port $port failed: ${e.message}")
            false
        }
    }

    fun stop() {
        running = false
        try {
            server?.close()
        } catch (_: IOException) {
        }
        server = null
    }
}
