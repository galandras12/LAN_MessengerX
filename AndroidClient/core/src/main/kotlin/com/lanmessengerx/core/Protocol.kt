package com.lanmessengerx.core

/**
 * Constants of the LAN Messenger wire protocol. Everything here mirrors the
 * original (Qt based) LAN Messenger so that this client can talk to old
 * clients unchanged. See Protocol/PROTOCOL.md for the full description.
 */
object Proto {
    const val APP_MARKER = "lmcmessage"
    const val DEFAULT_PORT = 50000
    const val DEFAULT_MULTICAST = "239.255.100.100"

    /** Must compare as newer than 1.2.30 (old clients gate group chat on > 1.2.10). */
    const val VERSION = "3.1.0"

    /** Client sends this many raw bytes ("MSG" + id) when opening a message stream. */
    const val ID_PACKET_MAX = 64
    const val FILE_ID_LENGTH = 32

    /** Old clients read at most 64 bytes of "FILE" + fileId(32) + userId, so keep our id short. */
    const val MAX_USER_ID_LENGTH = 28

    const val MAX_FRAME_SIZE = 8 * 1024 * 1024

    const val GROUP_DEFAULT_ID = "1CD75C10048C4E65F6082539A32DC111"

    const val UC_FILE = 0x1
    const val UC_GROUP_MESSAGE = 0x2
    const val UC_FOLDER = 0x4

    /** Presence status codes in the order used by the original client. */
    val STATUS_CODES = listOf("chat", "busy", "dnd", "brb", "away", "gone")
    const val STATUS_ONLINE = "chat"
    const val STATUS_OFFLINE = "gone"

    fun isOffline(status: String) = status == STATUS_OFFLINE
    fun isBusy(status: String) = status == "busy" || status == "dnd"
    fun isAway(status: String) = status == "brb" || status == "away"

    /**
     * Returns <0 if [a] is older than [b], >0 if newer, 0 if equal. Only the first three
     * dot separated components are compared, as the original does.
     */
    fun compareVersions(a: String, b: String): Int {
        val pa = a.split('.').filter { it.isNotEmpty() }
        val pb = b.split('.').filter { it.isNotEmpty() }
        for (i in 0 until 3) {
            val x = pa.getOrNull(i)?.toIntOrNull() ?: 0
            val y = pb.getOrNull(i)?.toIntOrNull() ?: 0
            if (x != y) return x - y
        }
        return 0
    }
}

/** The six character tag in front of every framed TCP payload. */
enum class DatagramType(val tag: String) {
    PUBLIC_KEY("PUBKEY"),
    HANDSHAKE("HNDSHK"),
    MESSAGE("MESSAG");

    companion object {
        fun fromTag(tag: String): DatagramType? = entries.firstOrNull { it.tag == tag }
    }
}

enum class MessageType(val wire: String) {
    BLANK(""),
    ANNOUNCE("announce"),
    DEPART("depart"),
    USER_DATA("userdata"),
    BROADCAST("broadcast"),
    STATUS("status"),
    AVATAR("avatar"),
    USER_NAME("name"),
    PING("ping"),
    MESSAGE("message"),
    GROUP_MESSAGE("groupmessage"),
    PUBLIC_MESSAGE("publicmessage"),
    FILE("file"),
    ACKNOWLEDGE("acknowledge"),
    FAILED("failed"),
    ERROR("error"),
    OLD_VERSION("oldversion"),
    QUERY("query"),
    INFO("info"),
    CHAT_STATE("chatstate"),
    NOTE("note"),
    FOLDER("folder");

    companion object {
        fun fromWire(s: String): MessageType? = entries.firstOrNull { it.wire == s }
    }
}

object Xn {
    const val ROOT = Proto.APP_MARKER
    const val HEAD = "head"
    const val BODY = "body"
    const val FROM = "from"
    const val TO = "to"
    const val MESSAGE_ID = "messageid"
    const val TYPE = "type"
    const val ADDRESS = "address"
    const val USER_ID = "userid"
    const val NAME = "name"
    const val VERSION = "version"
    const val STATUS = "status"
    const val LOGON = "logon"
    const val HOST = "host"
    const val OS = "os"
    const val FIRST_NAME = "firstname"
    const val LAST_NAME = "lastname"
    const val ABOUT = "about"
    const val THREAD = "thread"
    const val MESSAGE = "message"
    const val BROADCAST = "broadcast"
    const val MODE = "mode"
    const val FILE_OP = "fileop"
    const val FILE_TYPE = "filetype"
    const val FILE_ID = "fileid"
    const val FILE_PATH = "filepath"
    const val FILE_NAME = "filename"
    const val FILE_SIZE = "filesize"
    const val CHAT_STATE = "chatstate"
    const val QUERY_OP = "queryop"
    const val FONT = "font"
    const val COLOR = "color"
    const val GROUP_MSG_OP = "groupmsgop"
    const val NOTE = "note"
    const val USER_CAPS = "usercaps"
    const val FOLDER_ID = "folderid"
    const val TCP_PORT = "tcpport"
    const val REL_PATH = "relpath"
    const val FILE_COUNT = "filecount"
}

enum class FileMode(val wire: String) {
    SEND("send"), RECEIVE("receive");

    fun reversed() = if (this == SEND) RECEIVE else SEND

    companion object {
        fun fromWire(s: String): FileMode? = entries.firstOrNull { it.wire == s }
    }
}

enum class FileOp(val wire: String) {
    INIT("init"),
    REQUEST("request"),
    ACCEPT("accept"),
    DECLINE("decline"),
    CANCEL("cancel"),
    PROGRESS("progress"),
    ERROR("error"),
    ABORT("abort"),
    COMPLETE("complete"),
    NEXT("next");

    companion object {
        fun fromWire(s: String): FileOp? = entries.firstOrNull { it.wire == s }
    }
}

enum class FileType(val wire: String) {
    NORMAL("normal"), AVATAR("avatar"), FOLDER("folder");

    companion object {
        fun fromWire(s: String): FileType? = entries.firstOrNull { it.wire == s }
    }
}

enum class QueryOp(val wire: String) {
    GET("get"), RESULT("result")
}

enum class GroupMsgOp(val wire: String) {
    REQUEST("request"), JOIN("join"), MESSAGE("message"), LEAVE("leave");

    companion object {
        fun fromWire(s: String): GroupMsgOp? = entries.firstOrNull { it.wire == s }
    }
}

enum class ChatStateKind(val wire: String) {
    ACTIVE("active"), COMPOSING("composing"), PAUSED("paused"), INACTIVE("inactive"), GONE("gone");

    companion object {
        fun fromWire(s: String): ChatStateKind? = entries.firstOrNull { it.wire == s }
    }
}
