package com.lanmessengerx.core

import java.io.File
import java.io.InputStream
import java.io.OutputStream

data class User(
    val id: String,
    val version: String,
    val address: String,
    val name: String,
    val status: String,
    val note: String,
    val caps: Int,
    val avatarPath: String? = null,
) {
    val isOffline get() = Proto.isOffline(status)
}

enum class TransferState { REQUESTED, TRANSFERRING, COMPLETE, DECLINED, CANCELLED, ERROR }

data class TransferInfo(
    val id: String,
    val userId: String,
    val name: String,
    val size: Long,
    val bytes: Long,
    val outgoing: Boolean,
    val state: TransferState,
    /** Final local location of a completed incoming transfer, if the sink reports one. */
    val location: String? = null,
)

data class RoomInfo(val threadId: String, val participants: List<String>)

/** Where an outgoing file comes from (a java.io.File, an Android content Uri, ...). */
interface FileSource {
    val name: String
    val size: Long
    fun open(): InputStream
}

class LocalFileSource(private val file: File) : FileSource {
    override val name: String get() = file.name
    override val size: Long get() = file.length()
    override fun open(): InputStream = file.inputStream()
}

/** Where an incoming file is written. [commit] returns a user visible location. */
interface FileSink {
    fun open(): OutputStream
    fun commit(): String?
    fun discard()
}

class LocalFileSink(private val target: File) : FileSink {
    private val partial = File(target.parentFile, target.name + ".part")
    override fun open(): OutputStream {
        target.parentFile?.mkdirs()
        return partial.outputStream()
    }

    override fun commit(): String? {
        if (target.exists()) target.delete()
        partial.renameTo(target)
        return target.absolutePath
    }

    override fun discard() {
        partial.delete()
    }
}

sealed interface MessengerEvent {
    data class ConnectionChanged(val connected: Boolean, val ip: String?) : MessengerEvent
    data class UserOnline(val user: User) : MessengerEvent
    data class UserUpdated(val user: User) : MessengerEvent
    data class UserOffline(val userId: String, val name: String) : MessengerEvent
    data class TextMessage(val userId: String, val text: String, val timeMs: Long, val messageId: Long) : MessengerEvent
    data class MessageDelivered(val userId: String, val messageId: Long) : MessengerEvent
    data class MessageFailed(val userId: String, val messageId: Long) : MessengerEvent
    data class BroadcastMessage(val userId: String, val text: String, val timeMs: Long) : MessengerEvent
    data class PublicMessage(val userId: String, val text: String, val timeMs: Long) : MessengerEvent
    data class ChatStateChanged(val userId: String, val state: ChatStateKind, val threadId: String) : MessengerEvent
    data class UserInfo(val userId: String, val info: Map<String, String>) : MessengerEvent

    /** A peer's picture finished downloading (the file at [path] may have been replaced in place). */
    data class AvatarUpdated(val userId: String, val path: String?) : MessengerEvent

    /** A group chat room was created, joined or its participant list changed. */
    data class RoomChanged(val room: RoomInfo) : MessengerEvent
    data class RoomClosed(val threadId: String) : MessengerEvent
    data class RoomMessage(val threadId: String, val userId: String, val text: String, val timeMs: Long) : MessengerEvent
    data class RoomNotice(val threadId: String, val userId: String, val joined: Boolean, val timeMs: Long) : MessengerEvent

    data class Transfer(val info: TransferInfo) : MessengerEvent
}
