package com.lanmessengerx.core

import java.io.File

/**
 * Headless command line client used for interoperability testing against the
 * original LAN Messenger (see Protocol/interop). Commands on stdin:
 *   msg <userId> <text...> | file <userId> <path> | accept <userId> <fileId> <savePath>
 *   avatar <png> | status <code> | group <userId,userId,..> | gsay <thread> <text...>
 *   query <userId> | users | quit
 */
fun main(args: Array<String>) {
    val name = args.getOrNull(0) ?: "kotlin"
    val mac = args.getOrNull(1) ?: "AABBCCDDEE01"
    val dir = File(System.getProperty("java.io.tmpdir"), "lmx-cli-$name").also { it.mkdirs() }
    val cfg = MessengerConfig(
        userName = name, machineId = mac, logonName = name, hostName = "cli-$name", osName = "Linux CLI",
        cacheDir = File(dir, "cache"),
    )
    val out = { s: String -> println(s); System.out.flush() }
    val m = Messenger(cfg, log = { out("LOG $it") }) { e -> out("EVT $e") }
    m.start()
    out("READY id=${m.localId}")
    val sinks = HashMap<String, File>()
    while (true) {
        val line = readLine() ?: break
        val p = line.trim().split(' ')
        when (p.firstOrNull()) {
            "msg" -> out("SENT id=" + m.sendText(p[1], p.drop(2).joinToString(" ")))
            "file" -> out("FILEID " + m.sendFile(p[1], LocalFileSource(File(p[2]))))
            "accept" -> m.acceptFile(p[1], p[2], LocalFileSink(File(p[3])))
            "avatar" -> m.setLocalAvatar(File(p[1]))
            "status" -> m.setStatus(p[1])
            "group" -> out("THREAD " + m.createGroupChat(p[1].split(',')))
            "gsay" -> m.sendGroupText(p[1], p.drop(2).joinToString(" "))
            "query" -> m.requestUserInfo(p[1])
            "users" -> out("USERS " + m.onlineUsers.joinToString { it.id + "=" + it.name })
            "quit" -> { m.stop(); return }
        }
    }
    m.stop()
}
