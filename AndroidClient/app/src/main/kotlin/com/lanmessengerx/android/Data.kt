package com.lanmessengerx.android

import android.content.ContentValues
import android.content.Context
import android.database.Cursor
import android.net.Uri
import android.os.Build
import android.provider.DocumentsContract
import android.provider.MediaStore
import android.provider.OpenableColumns
import android.webkit.MimeTypeMap
import com.lanmessengerx.core.FileSink
import com.lanmessengerx.core.FolderEntry
import com.lanmessengerx.core.FolderSource
import com.lanmessengerx.core.FileSource
import com.lanmessengerx.core.TransferState
import java.io.File
import java.io.InputStream
import java.io.OutputStream

enum class Delivery { SENDING, DELIVERED, FAILED }

sealed class ChatItem(val time: Long) {
    class Msg(
        val text: String, val outgoing: Boolean, val sender: String, time: Long,
        val msgId: Long, var state: Delivery = Delivery.DELIVERED,
    ) : ChatItem(time)

    class FileItem(
        val fileId: String, var name: String, var size: Long, val outgoing: Boolean,
        var state: TransferState, var bytes: Long, var location: String?, time: Long,
        val isFolder: Boolean = false, val fileCount: Int = 0,
    ) : ChatItem(time)

    class Notice(val text: String, time: Long) : ChatItem(time)
    class Broadcast(val text: String, val sender: String, val outgoing: Boolean, time: Long) : ChatItem(time)
}

class Chat(val userId: String, var name: String) {
    val items = ArrayList<ChatItem>()
    var unread = 0
    var typing = false
    var lastTime = 0L
}

class Room(val threadId: String) {
    var participants: List<String> = emptyList()
    val items = ArrayList<ChatItem>()
    var unread = 0
    var lastTime = 0L
}

/** Plain SharedPreferences wrapper. */
class Prefs(ctx: Context) {
    private val sp = ctx.getSharedPreferences("lmx", Context.MODE_PRIVATE)

    var machineId: String
        get() = sp.getString("machineId", null) ?: run {
            val id = (1..12).joinToString("") { "0123456789ABCDEF"[java.security.SecureRandom().nextInt(16)].toString() }
            sp.edit().putString("machineId", id).apply()
            id
        }
        set(v) = sp.edit().putString("machineId", v).apply()

    var name: String
        get() = sp.getString("name", "") ?: ""
        set(v) = sp.edit().putString("name", v).apply()
    var note: String
        get() = sp.getString("note", "") ?: ""
        set(v) = sp.edit().putString("note", v).apply()
    var status: String
        get() = sp.getString("status", "chat") ?: "chat"
        set(v) = sp.edit().putString("status", v).apply()
    var saveHistory: Boolean
        get() = sp.getBoolean("history", true)
        set(v) = sp.edit().putBoolean("history", v).apply()
    var dynamicColor: Boolean
        get() = sp.getBoolean("dynamic", true)
        set(v) = sp.edit().putBoolean("dynamic", v).apply()
    var runInBackground: Boolean
        get() = sp.getBoolean("background", true)
        set(v) = sp.edit().putBoolean("background", v).apply()
    var notifications: Boolean
        get() = sp.getBoolean("notify", true)
        set(v) = sp.edit().putBoolean("notify", v).apply()
    /** "system" or a code from Loc.languages. */
    var language: String
        get() = sp.getString("language", "system") ?: "system"
        set(v) = sp.edit().putString("language", v).apply()
    /** The profile picture was chosen (or removed) by the user; until then a random stock picture is used. */
    var avatarChosen: Boolean
        get() = sp.getBoolean("avatarChosen", false)
        set(v) = sp.edit().putBoolean("avatarChosen", v).apply()
    var askedNotificationPermission: Boolean
        get() = sp.getBoolean("askedNotif", false)
        set(v) = sp.edit().putBoolean("askedNotif", v).apply()
}

/** One tab separated line per message: time, direction, peer, sender, escaped text. */
class HistoryStore(ctx: Context) {
    private val dir = File(ctx.filesDir, "history").also { it.mkdirs() }

    class Entry(val time: Long, val outgoing: Boolean, val sender: String, val text: String)
    class Conversation(val key: String, val title: String, val lastTime: Long, val count: Int)

    private fun file(key: String) = File(dir, key.filter { it.isLetterOrDigit() || it == '_' || it == '-' }.take(80) + ".tsv")

    @Synchronized
    fun append(key: String, title: String, e: Entry) {
        try {
            file(key).appendText("${e.time}\t${if (e.outgoing) 1 else 0}\t${esc(title)}\t${esc(e.sender)}\t${esc(e.text)}\n")
        } catch (_: Exception) {
        }
    }

    @Synchronized
    fun conversations(): List<Conversation> {
        val out = ArrayList<Conversation>()
        for (f in dir.listFiles().orEmpty()) {
            if (!f.name.endsWith(".tsv")) continue
            var title = ""
            var last = 0L
            var n = 0
            f.forEachLine { line ->
                val p = line.split('\t')
                if (p.size >= 5) {
                    n++
                    last = p[0].toLongOrNull() ?: last
                    title = unesc(p[2])
                }
            }
            if (n > 0) out.add(Conversation(f.nameWithoutExtension, title, last, n))
        }
        return out.sortedByDescending { it.lastTime }
    }

    @Synchronized
    fun read(key: String): List<Entry> {
        val f = file(key)
        if (!f.isFile) return emptyList()
        val out = ArrayList<Entry>()
        f.forEachLine { line ->
            val p = line.split('\t')
            if (p.size >= 5) out.add(Entry(p[0].toLongOrNull() ?: 0L, p[1] == "1", unesc(p[3]), unesc(p[4])))
        }
        return out
    }

    @Synchronized
    fun delete(key: String) {
        file(key).delete()
    }

    @Synchronized
    fun clear() {
        dir.listFiles()?.forEach { it.delete() }
    }

    companion object {
        fun esc(s: String) = s.replace("\\", "\\\\").replace("\n", "\\n").replace("\t", "\\t").replace("\r", "\\r")
        fun unesc(s: String): String {
            val sb = StringBuilder(s.length)
            var i = 0
            while (i < s.length) {
                val c = s[i]
                if (c == '\\' && i + 1 < s.length) {
                    when (s[i + 1]) {
                        'n' -> sb.append('\n')
                        't' -> sb.append('\t')
                        'r' -> sb.append('\r')
                        else -> sb.append(s[i + 1])
                    }
                    i += 2
                } else {
                    sb.append(c)
                    i++
                }
            }
            return sb.toString()
        }
    }
}

/** An outgoing file picked through the system file chooser (content:// Uri). */
class UriFileSource(private val ctx: Context, private val uri: Uri) : FileSource {
    override val name: String
    override val size: Long

    init {
        var n = "file"
        var s = -1L
        var c: Cursor? = null
        try {
            c = ctx.contentResolver.query(uri, null, null, null, null)
            if (c != null && c.moveToFirst()) {
                val ni = c.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                val si = c.getColumnIndex(OpenableColumns.SIZE)
                if (ni >= 0) n = c.getString(ni) ?: n
                if (si >= 0 && !c.isNull(si)) s = c.getLong(si)
            }
        } catch (_: Exception) {
        } finally {
            c?.close()
        }
        if (s < 0) {
            s = try {
                ctx.contentResolver.openAssetFileDescriptor(uri, "r")?.use { it.length } ?: 0L
            } catch (_: Exception) {
                0L
            }
        }
        name = n
        size = s
    }

    override fun open(): InputStream = ctx.contentResolver.openInputStream(uri) ?: throw java.io.IOException("cannot open $uri")
}

/** Incoming file: written to a temp file, then published to Downloads/LAN Messenger X. */
class ReceivedFileSink(private val ctx: Context, private val name: String, private val subdir: String = "") : FileSink {
    private val tmp = File(ctx.cacheDir, "rx_${System.nanoTime()}.part")

    override fun open(): OutputStream = tmp.outputStream()

    override fun commit(): String? {
        val mime = MimeTypeMap.getSingleton().getMimeTypeFromExtension(name.substringAfterLast('.', "").lowercase())
            ?: "application/octet-stream"
        return try {
            if (Build.VERSION.SDK_INT >= 29) {
                val values = ContentValues().apply {
                    put(MediaStore.Downloads.DISPLAY_NAME, name)
                    put(MediaStore.Downloads.MIME_TYPE, mime)
                    put(MediaStore.Downloads.RELATIVE_PATH, "Download/LAN Messenger X" + if (subdir.isEmpty()) "" else "/$subdir")
                    put(MediaStore.Downloads.IS_PENDING, 1)
                }
                val uri = ctx.contentResolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
                    ?: return fallback()
                ctx.contentResolver.openOutputStream(uri)?.use { out -> tmp.inputStream().use { it.copyTo(out) } }
                val done = ContentValues().apply { put(MediaStore.Downloads.IS_PENDING, 0) }
                ctx.contentResolver.update(uri, done, null, null)
                tmp.delete()
                uri.toString()
            } else fallback()
        } catch (e: Exception) {
            fallback()
        }
    }

    private fun fallback(): String? {
        val dir = File(File(ctx.getExternalFilesDir(android.os.Environment.DIRECTORY_DOWNLOADS), "LAN Messenger X"), subdir).also { it.mkdirs() }
        var target = File(dir, name)
        var n = 1
        while (target.exists()) {
            val base = name.substringBeforeLast('.', name)
            val ext = name.substringAfterLast('.', "")
            target = File(dir, "$base ($n)" + if (ext.isEmpty()) "" else ".$ext")
            n++
        }
        return if (tmp.renameTo(target) || (try { tmp.copyTo(target); tmp.delete(); true } catch (_: Exception) { false })) target.absolutePath else null
    }

    override fun discard() {
        tmp.delete()
    }
}

/** A folder picked with the system folder chooser (a document tree); all files below it are offered. */
class TreeFolderSource(private val ctx: Context, private val tree: Uri) : FolderSource {
    private val rootId = DocumentsContract.getTreeDocumentId(tree)
    override val name: String = queryName(DocumentsContract.buildDocumentUriUsingTree(tree, rootId)) ?: "folder"

    private fun queryName(doc: Uri): String? = try {
        ctx.contentResolver.query(doc, arrayOf(DocumentsContract.Document.COLUMN_DISPLAY_NAME), null, null, null)?.use { c ->
            if (c.moveToFirst()) c.getString(0) else null
        }
    } catch (_: Exception) {
        null
    }

    override fun entries(): List<FolderEntry> {
        val out = ArrayList<FolderEntry>()
        walk(rootId, "", out, 0)
        return out
    }

    private fun walk(docId: String, prefix: String, out: MutableList<FolderEntry>, depth: Int) {
        if (depth > 24) return
        val children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, docId)
        val cols = arrayOf(
            DocumentsContract.Document.COLUMN_DOCUMENT_ID, DocumentsContract.Document.COLUMN_DISPLAY_NAME,
            DocumentsContract.Document.COLUMN_MIME_TYPE, DocumentsContract.Document.COLUMN_SIZE,
        )
        val found = ArrayList<Array<Any?>>()
        ctx.contentResolver.query(children, cols, null, null, null)?.use { c ->
            while (c.moveToNext()) found.add(arrayOf(c.getString(0), c.getString(1), c.getString(2), if (c.isNull(3)) -1L else c.getLong(3)))
        }
        for (f in found) {
            val id = f[0] as String
            val n = (f[1] as String?) ?: continue
            if (f[2] == DocumentsContract.Document.MIME_TYPE_DIR) walk(id, "$prefix$n/", out, depth + 1)
            else {
                val uri = DocumentsContract.buildDocumentUriUsingTree(tree, id)
                val rel = prefix + n
                val size = (f[3] as Long).let { if (it >= 0) it else ctx.contentResolver.openAssetFileDescriptor(uri, "r")?.use { d -> d.length } ?: 0L }
                out.add(object : FolderEntry {
                    override val relPath = rel
                    override val size = size
                    override fun open(): InputStream = ctx.contentResolver.openInputStream(uri) ?: throw java.io.IOException("cannot open $uri")
                })
            }
        }
    }
}
