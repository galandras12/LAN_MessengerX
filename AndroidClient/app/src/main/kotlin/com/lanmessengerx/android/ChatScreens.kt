package com.lanmessengerx.android

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.os.Handler
import android.os.Looper
import android.text.Editable
import android.text.InputType
import android.text.TextWatcher
import android.text.method.LinkMovementMethod
import android.text.util.Linkify
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.CheckBox
import android.widget.EditText
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.ListView
import android.widget.TextView
import com.lanmessengerx.android.M3.Type
import com.lanmessengerx.android.M3.dp
import com.lanmessengerx.android.M3.dpf
import com.lanmessengerx.android.M3.style
import com.lanmessengerx.core.ChatStateKind
import com.lanmessengerx.core.TransferState

/** Shared chat UI: message list + composer. Subclasses supply the data and the send action. */
abstract class ConversationScreen(act: MainActivity) : Screen(act) {
    protected abstract val items: List<ChatItem>
    protected abstract fun title(): String
    protected abstract fun subtitle(): String?
    protected abstract fun send(text: String)
    protected abstract fun markRead()
    protected abstract fun bindBar(bar: Ui.AppBar)
    protected open val showSenderNames = false
    protected open fun onTyping(composing: Boolean) {}
    protected open val canAttach = false
    protected open fun attach() {}

    private lateinit var bar: Ui.AppBar
    private lateinit var list: ListView
    private lateinit var adapter: RowAdapter<ChatItem>
    protected lateinit var input: EditText
    private lateinit var sendBtn: FrameLayout
    private lateinit var sendIcon: android.widget.ImageView
    private val handler = Handler(Looper.getMainLooper())
    private var composing = false
    private val pausedRunnable = Runnable { if (composing) { composing = false; onTyping(false) } }

    override fun createView(): View {
        val ctx = act
        val c = M3.colors
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        col.setBackgroundColor(c.surface)

        bar = Ui.AppBar(ctx)
        bar.back { act.onBackPressed() }
        bindBar(bar)
        col.addView(bar)

        list = ListView(ctx)
        list.divider = null
        list.dividerHeight = 0
        list.setSelector(android.R.color.transparent)
        list.isVerticalScrollBarEnabled = false
        list.isStackFromBottom = true
        list.transcriptMode = ListView.TRANSCRIPT_MODE_NORMAL
        list.clipToPadding = false
        list.setPadding(0, ctx.dp(8), 0, ctx.dp(8))
        list.setBackgroundColor(c.surface)
        adapter = RowAdapter(ctx, { items.toList() }, { item, convert, parent -> buildRow(ctx, item) }, { typeOf(it) }, 4)
        list.adapter = adapter
        col.addView(list, lpWeight())

        col.addView(composer(ctx))
        return col
    }

    private fun typeOf(i: ChatItem) = when (i) {
        is ChatItem.Msg -> 0
        is ChatItem.FileItem -> 1
        is ChatItem.Notice -> 2
        is ChatItem.Broadcast -> 3
    }

    private fun composer(ctx: Context): View {
        val c = M3.colors
        val row = LinearLayout(ctx)
        row.orientation = LinearLayout.HORIZONTAL
        row.gravity = Gravity.BOTTOM
        row.setBackgroundColor(c.surface)
        row.setPadding(ctx.dp(4), ctx.dp(4), ctx.dp(8), ctx.dp(8))
        if (canAttach) {
            row.addView(M3.iconButton(ctx, R.drawable.ic_attach, "Send a file", c.onSurfaceVariant) { attach() },
                LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48)))
        } else {
            row.setPadding(ctx.dp(12), ctx.dp(4), ctx.dp(8), ctx.dp(8))
        }
        input = EditText(ctx)
        input.hint = "Message"
        input.style(Type.BODY_LARGE, c.onSurface)
        input.setHintTextColor(c.onSurfaceVariant)
        input.background = M3.roundRect(ctx, c.surfaceContainerHigh, 24f)
        input.setPadding(ctx.dp(20), ctx.dp(12), ctx.dp(20), ctx.dp(12))
        input.minHeight = ctx.dp(48)
        input.maxLines = 5
        input.inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_FLAG_MULTI_LINE or InputType.TYPE_TEXT_FLAG_CAP_SENTENCES
        input.highlightColor = M3.withAlpha(c.primary, 0.3f)
        input.addTextChangedListener(object : TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, st: Int, c: Int, a: Int) {}
            override fun onTextChanged(s: CharSequence?, st: Int, b: Int, c: Int) {}
            override fun afterTextChanged(s: Editable?) {
                styleSend()
                if (!s.isNullOrBlank()) {
                    if (!composing) { composing = true; onTyping(true) }
                    handler.removeCallbacks(pausedRunnable)
                    handler.postDelayed(pausedRunnable, 3000)
                }
            }
        })
        row.addView(input, LinearLayout.LayoutParams(0, -2, 1f))
        sendBtn = FrameLayout(ctx)
        sendBtn.isClickable = true
        sendBtn.contentDescription = "Send"
        sendIcon = M3.icon(ctx, R.drawable.ic_send, c.onPrimary)
        sendBtn.addView(sendIcon, FrameLayout.LayoutParams(ctx.dp(24), ctx.dp(24), Gravity.CENTER))
        sendBtn.setOnClickListener { doSend() }
        val lp = LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48))
        lp.marginStart = ctx.dp(8)
        row.addView(sendBtn, lp)
        styleSend()
        return row
    }

    private fun styleSend() {
        val c = M3.colors
        val on = input.text.isNotBlank()
        sendBtn.background = M3.circleRipple(M3.circle(if (on) c.primary else c.surfaceContainerHighest), if (on) c.onPrimary else c.onSurface)
        sendIcon.imageTintList = android.content.res.ColorStateList.valueOf(if (on) c.onPrimary else c.onSurfaceVariant)
    }

    private fun doSend() {
        val t = input.text.toString().trim()
        if (t.isEmpty()) return
        input.setText("")
        handler.removeCallbacks(pausedRunnable)
        if (composing) { composing = false; onTyping(false) }
        send(t)
    }

    override fun onShow() {
        markRead()
        refresh()
    }

    override fun onHide() {
        handler.removeCallbacks(pausedRunnable)
        if (composing) { composing = false; onTyping(false) }
    }

    override fun refresh() {
        if (!::adapter.isInitialized) return
        val atBottom = list.lastVisiblePosition >= adapter.count - 2
        adapter.reload()
        if (atBottom && adapter.count > 0) list.setSelection(adapter.count - 1)
        bar.setTitle(title(), subtitle())
        markRead()
    }

    // ---- message rows ---------------------------------------------------------------------

    private fun buildRow(ctx: Context, item: ChatItem): View {
        val wrap = LinearLayout(ctx)
        wrap.orientation = LinearLayout.VERTICAL
        val idx = items.indexOf(item)
        val prev = if (idx > 0) items[idx - 1] else null
        if (prev == null || !Ui.sameDay(prev.time, item.time)) {
            val d = M3.text(ctx, Ui.formatDay(item.time), Type.LABEL_MEDIUM, M3.colors.onSurfaceVariant)
            d.gravity = Gravity.CENTER
            d.setPadding(0, ctx.dp(12), 0, ctx.dp(8))
            wrap.addView(d, LinearLayout.LayoutParams(-1, -2))
        }
        wrap.addView(
            when (item) {
                is ChatItem.Msg -> messageBubble(ctx, item)
                is ChatItem.FileItem -> fileCard(ctx, item)
                is ChatItem.Notice -> notice(ctx, item)
                is ChatItem.Broadcast -> broadcastCard(ctx, item)
            },
        )
        return wrap
    }

    private fun messageBubble(ctx: Context, m: ChatItem.Msg): View {
        val c = M3.colors
        val row = LinearLayout(ctx)
        row.gravity = if (m.outgoing) Gravity.END else Gravity.START
        row.setPadding(ctx.dp(12), ctx.dp(2), ctx.dp(12), ctx.dp(2))
        val bubble = LinearLayout(ctx)
        bubble.orientation = LinearLayout.VERTICAL
        val bg = if (m.outgoing) c.primaryContainer else c.surfaceContainerHigh
        val fg = if (m.outgoing) c.onPrimaryContainer else c.onSurface
        val r = ctx.dpf(18f)
        val small = ctx.dpf(4f)
        val shape = android.graphics.drawable.GradientDrawable()
        shape.setColor(bg)
        shape.cornerRadii = if (m.outgoing) floatArrayOf(r, r, r, r, small, small, r, r) else floatArrayOf(r, r, r, r, r, r, small, small)
        bubble.background = shape
        bubble.setPadding(ctx.dp(14), ctx.dp(8), ctx.dp(14), ctx.dp(8))
        if (showSenderNames && !m.outgoing) {
            val n = M3.text(ctx, m.sender, Type.LABEL_MEDIUM, c.primary)
            n.setPadding(0, 0, 0, ctx.dp(2))
            bubble.addView(n)
        }
        val t = TextView(ctx)
        t.style(Type.BODY_LARGE, fg)
        t.text = m.text
        t.autoLinkMask = 0
        Linkify.addLinks(t, Linkify.WEB_URLS or Linkify.EMAIL_ADDRESSES)
        t.setLinkTextColor(if (m.outgoing) c.onPrimaryContainer else c.primary)
        t.movementMethod = LinkMovementMethod.getInstance()
        t.setOnLongClickListener {
            val cm = ctx.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
            cm.setPrimaryClip(ClipData.newPlainText("message", m.text))
            act.toast("Copied")
            true
        }
        bubble.addView(t)
        val meta = LinearLayout(ctx)
        meta.gravity = Gravity.END or Gravity.CENTER_VERTICAL
        meta.setPadding(0, ctx.dp(2), 0, 0)
        val time = M3.text(ctx, Ui.formatTime(m.time), Type.LABEL_SMALL, M3.withAlpha(fg, 0.7f))
        meta.addView(time)
        if (m.outgoing) {
            when (m.state) {
                Delivery.SENDING -> {
                    val s = M3.text(ctx, "  sending…", Type.LABEL_SMALL, M3.withAlpha(fg, 0.7f))
                    meta.addView(s)
                }
                Delivery.DELIVERED -> {
                    val i = M3.icon(ctx, R.drawable.ic_done_all, M3.withAlpha(fg, 0.8f), 14)
                    (i.layoutParams as LinearLayout.LayoutParams).marginStart = ctx.dp(4)
                    meta.addView(i)
                }
                Delivery.FAILED -> {
                    val s = M3.text(ctx, "  not delivered", Type.LABEL_SMALL, c.error)
                    meta.addView(s)
                }
            }
        }
        bubble.addView(meta)
        val lp = LinearLayout.LayoutParams(-2, -2)
        val maxW = (ctx.resources.displayMetrics.widthPixels * 0.82f).toInt()
        bubble.minimumWidth = 0
        row.addView(bubble, lp)
        t.maxWidth = maxW - ctx.dp(28)
        return row
    }

    private fun notice(ctx: Context, n: ChatItem.Notice): View {
        val t = M3.text(ctx, n.text, Type.LABEL_MEDIUM, M3.colors.onSurfaceVariant)
        t.gravity = Gravity.CENTER
        t.setPadding(ctx.dp(16), ctx.dp(8), ctx.dp(16), ctx.dp(8))
        return t
    }

    private fun broadcastCard(ctx: Context, b: ChatItem.Broadcast): View {
        val c = M3.colors
        val frame = FrameLayout(ctx)
        frame.setPadding(ctx.dp(16), ctx.dp(6), ctx.dp(16), ctx.dp(6))
        val card = LinearLayout(ctx)
        card.orientation = LinearLayout.VERTICAL
        card.background = M3.roundRect(ctx, c.tertiaryContainer, 16f)
        card.setPadding(ctx.dp(16), ctx.dp(12), ctx.dp(16), ctx.dp(12))
        val head = LinearLayout(ctx)
        head.gravity = Gravity.CENTER_VERTICAL
        head.addView(M3.icon(ctx, R.drawable.ic_campaign, c.onTertiaryContainer, 18))
        val label = if (b.outgoing) "You sent a broadcast" else "Broadcast from ${b.sender}"
        val h = M3.text(ctx, label, Type.LABEL_LARGE, c.onTertiaryContainer)
        h.setPadding(ctx.dp(8), 0, 0, 0)
        head.addView(h)
        card.addView(head)
        val t = M3.text(ctx, b.text, Type.BODY_LARGE, c.onTertiaryContainer)
        t.setPadding(0, ctx.dp(6), 0, 0)
        card.addView(t)
        frame.addView(card, FrameLayout.LayoutParams(-1, -2))
        return frame
    }

    private fun fileCard(ctx: Context, f: ChatItem.FileItem): View {
        val c = M3.colors
        val frame = FrameLayout(ctx)
        frame.setPadding(ctx.dp(16), ctx.dp(6), ctx.dp(16), ctx.dp(6))
        val card = LinearLayout(ctx)
        card.orientation = LinearLayout.VERTICAL
        card.background = M3.roundRect(ctx, if (f.outgoing) c.secondaryContainer else c.surfaceContainerHighest, 16f)
        card.setPadding(ctx.dp(16), ctx.dp(12), ctx.dp(16), ctx.dp(8))
        val top = LinearLayout(ctx)
        top.gravity = Gravity.CENTER_VERTICAL
        val circle = FrameLayout(ctx)
        circle.background = M3.circle(c.primaryContainer)
        circle.addView(M3.icon(ctx, R.drawable.ic_file, c.onPrimaryContainer), FrameLayout.LayoutParams(ctx.dp(24), ctx.dp(24), Gravity.CENTER))
        top.addView(circle, LinearLayout.LayoutParams(ctx.dp(44), ctx.dp(44)))
        val names = LinearLayout(ctx)
        names.orientation = LinearLayout.VERTICAL
        names.setPadding(ctx.dp(12), 0, 0, 0)
        names.addView(M3.ellipsize(M3.text(ctx, f.name, Type.BODY_LARGE, c.onSurface)))
        val status = when (f.state) {
            TransferState.REQUESTED -> if (f.outgoing) "Waiting for the other side to accept · ${Ui.formatSize(f.size)}" else "Wants to send you this file · ${Ui.formatSize(f.size)}"
            TransferState.TRANSFERRING -> "${Ui.formatSize(f.bytes)} of ${Ui.formatSize(f.size)}"
            TransferState.COMPLETE -> (if (f.outgoing) "Sent" else "Saved to Downloads/LAN Messenger X") + " · ${Ui.formatSize(f.size)}"
            TransferState.DECLINED -> "Declined"
            TransferState.CANCELLED -> "Cancelled"
            TransferState.ERROR -> "Transfer failed"
        }
        val failed = f.state == TransferState.ERROR
        names.addView(M3.text(ctx, status, Type.BODY_SMALL, if (failed) c.error else c.onSurfaceVariant).also { it.setPadding(0, ctx.dp(2), 0, 0) })
        top.addView(names, LinearLayout.LayoutParams(0, -2, 1f))
        card.addView(top)
        if (f.state == TransferState.TRANSFERRING) {
            val p = M3.LinearProgress(ctx)
            p.fraction = if (f.size > 0) f.bytes.toFloat() / f.size else 0f
            card.addView(p, LinearLayout.LayoutParams(-1, ctx.dp(4)).also { it.topMargin = ctx.dp(12) })
        }
        val actions = LinearLayout(ctx)
        actions.gravity = Gravity.END
        val userId = fileOwner(f)
        when {
            f.state == TransferState.REQUESTED && !f.outgoing -> {
                actions.addView(M3.button(ctx, "Decline", M3.ButtonKind.TEXT) { Hub.declineFile(userId, f.fileId) })
                actions.addView(M3.button(ctx, "Accept", M3.ButtonKind.TONAL, R.drawable.ic_download) { Hub.acceptFile(userId, f.fileId) })
            }
            f.state == TransferState.REQUESTED || f.state == TransferState.TRANSFERRING ->
                actions.addView(M3.button(ctx, "Cancel", M3.ButtonKind.TEXT) { Hub.cancelFile(userId, f.fileId) })
            f.state == TransferState.COMPLETE && !f.outgoing && f.location?.startsWith("content://") == true ->
                actions.addView(M3.button(ctx, "Open", M3.ButtonKind.TEXT) { act.openReceived(f.location) })
        }
        if (actions.childCount > 0) {
            actions.setPadding(0, ctx.dp(8), 0, 0)
            card.addView(actions)
        } else card.setPadding(ctx.dp(16), ctx.dp(12), ctx.dp(16), ctx.dp(12))
        frame.addView(card, FrameLayout.LayoutParams(-1, -2))
        return frame
    }

    protected open fun fileOwner(f: ChatItem.FileItem): String = ""
}

/** 1:1 conversation with one person. */
class ChatScreen(act: MainActivity, private val userId: String) : ConversationScreen(act) {
    override val spec get() = "chat|$userId"
    override val items: List<ChatItem> get() = Hub.chats[userId]?.items ?: emptyList()
    override val canAttach = true
    private val user get() = Hub.contacts.firstOrNull { it.id == userId }
    private val online get() = user != null && !(user?.isOffline ?: true)
    private var avatar: AvatarView? = null

    override fun title() = Hub.chats[userId]?.name ?: userId
    override fun subtitle(): String? {
        val u = user ?: return "Offline"
        if (Hub.chats[userId]?.typing == true) return "typing…"
        return if (u.note.isNotBlank()) u.note else Ui.statusLabel(u.status)
    }

    override fun bindBar(bar: Ui.AppBar) {
        val ctx = act
        val a = AvatarView(ctx, 40)
        avatar = a
        bar.titleBox.setPadding(ctx.dp(12), 0, 0, 0)
        bar.leading.addView(a, LinearLayout.LayoutParams(ctx.dp(40), ctx.dp(40)))
        bar.action(R.drawable.ic_info, "About this person") { infoDialog() }
        bindAvatar()
    }

    private fun bindAvatar() {
        val u = user
        avatar?.bind(title(), Avatars.load(userId, u?.avatarPath, 80), u?.status ?: "gone")
    }

    override fun refresh() {
        super.refresh()
        bindAvatar()
    }

    override fun send(text: String) {
        if (!online) {
            act.toast("${title()} is offline")
            return
        }
        Hub.sendText(userId, text)
    }

    override fun markRead() {
        Hub.openChat = userId
        if ((Hub.chats[userId]?.unread ?: 0) > 0) Hub.markRead(userId)
    }

    override fun onHide() {
        super.onHide()
        if (Hub.openChat == userId) Hub.openChat = null
    }

    override fun onTyping(composing: Boolean) {
        if (online) Hub.typing(userId, if (composing) ChatStateKind.COMPOSING else ChatStateKind.PAUSED)
    }

    override fun attach() {
        if (!online) {
            act.toast("${title()} is offline")
            return
        }
        act.pickDocument("*/*") { uri -> if (uri != null) Hub.sendFile(userId, uri) }
    }

    override fun fileOwner(f: ChatItem.FileItem) = userId

    private fun infoDialog() {
        val ctx = act
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        val body = M3.text(ctx, "Asking ${title()} for details…", Type.BODY_MEDIUM, M3.colors.onSurfaceVariant)
        col.addView(body)
        val d = M3.dialog(ctx, title(), body = col, dismiss = "Close")
        Hub.infoListener = { id, info ->
            if (id == userId) {
                val lines = listOf(
                    "Status" to Ui.statusLabel(info["status"] ?: ""),
                    "Note" to (info["note"] ?: ""),
                    "Logon name" to (info["logon"] ?: ""),
                    "Computer" to (info["host"] ?: ""),
                    "System" to (info["os"] ?: ""),
                    "Address" to (info["address"] ?: ""),
                    "Version" to (info["version"] ?: ""),
                ).filter { it.second.isNotBlank() && it.second != "N/A" }
                body.text = lines.joinToString("\n") { "${it.first}: ${it.second}" }
            }
        }
        d.setOnDismissListener { Hub.infoListener = null }
        d.show()
        Hub.requestInfo(userId)
    }
}

/** Group conversation. */
class RoomScreen(act: MainActivity, private val threadId: String) : ConversationScreen(act) {
    override val spec get() = "room|$threadId"
    override val items: List<ChatItem> get() = Hub.rooms[threadId]?.items ?: emptyList()
    override val showSenderNames = true

    override fun title() = Hub.rooms[threadId]?.let { Hub.roomTitle(it) } ?: "Group chat"
    override fun subtitle(): String? {
        val r = Hub.rooms[threadId] ?: return null
        return "${r.participants.size} participants"
    }

    override fun bindBar(bar: Ui.AppBar) {
        bar.titleBox.setPadding(act.dp(4), 0, 0, 0)
        bar.action(R.drawable.ic_group_add, "Invite people") { act.push(NewGroupScreen(act, threadId)) }
        bar.action(R.drawable.ic_more_vert, "More") { v ->
            M3.showMenu(v, listOf("Leave group" to { confirmLeave() }))
        }
    }

    private fun confirmLeave() {
        M3.dialog(
            act, "Leave this group?", "You will stop receiving its messages.",
            confirm = "Leave", onConfirm = { Hub.leaveRoom(threadId); act.pop() }, dismiss = "Stay",
        ).show()
    }

    override fun send(text: String) = Hub.sendRoomText(threadId, text)

    override fun markRead() {
        Hub.openRoom = threadId
        if ((Hub.rooms[threadId]?.unread ?: 0) > 0) Hub.markRoomRead(threadId)
    }

    override fun onHide() {
        super.onHide()
        if (Hub.openRoom == threadId) Hub.openRoom = null
    }
}

/** Pick people to start a new group chat with, or to invite into an existing one. */
class NewGroupScreen(act: MainActivity, private val inviteTo: String?) : Screen(act) {
    override val spec get() = "new-group|${inviteTo ?: ""}"
    private val selected = HashSet<String>()

    override fun createView(): View {
        val ctx = act
        val c = M3.colors
        val frame = FrameLayout(ctx)
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        val bar = Ui.AppBar(ctx)
        bar.back { act.onBackPressed() }
        bar.setTitle(if (inviteTo == null) "New group chat" else "Invite people", "Choose who to add")
        bar.titleBox.setPadding(ctx.dp(4), 0, 0, 0)
        col.addView(bar)
        val already = inviteTo?.let { Hub.rooms[it]?.participants?.toSet() } ?: emptySet()
        val candidates = Hub.contacts.filter { !it.isOffline && it.id !in already }
        if (candidates.isEmpty()) {
            col.addView(Ui.emptyState(ctx, R.drawable.ic_person, "Nobody to add", "Only people who are online right now can be invited."), lpWeight())
        } else {
            val list = Ui.listView(ctx)
            list.adapter = RowAdapter(ctx, { candidates }, { u, _, _ ->
                val row = LinearLayout(ctx)
                row.gravity = Gravity.CENTER_VERTICAL
                row.minimumHeight = ctx.dp(64)
                row.setPadding(ctx.dp(16), ctx.dp(8), ctx.dp(16), ctx.dp(8))
                row.background = M3.ripple(ctx, null, 0f)
                val a = AvatarView(ctx, 40)
                a.bind(u.name, Avatars.load(u.id, u.avatarPath, 80), u.status)
                row.addView(a, LinearLayout.LayoutParams(ctx.dp(40), ctx.dp(40)))
                val n = M3.ellipsize(M3.text(ctx, u.name, Type.BODY_LARGE, c.onSurface))
                n.setPadding(ctx.dp(16), 0, ctx.dp(8), 0)
                row.addView(n, LinearLayout.LayoutParams(0, -2, 1f))
                val cb = CheckBox(ctx)
                cb.isChecked = u.id in selected
                cb.buttonTintList = android.content.res.ColorStateList.valueOf(c.primary)
                cb.isClickable = false
                cb.isFocusable = false
                row.addView(cb)
                row
            })
            list.setOnItemClickListener { _, view, pos, _ ->
                val u = candidates[pos]
                if (!selected.add(u.id)) selected.remove(u.id)
                ((view as LinearLayout).getChildAt(2) as CheckBox).isChecked = u.id in selected
            }
            col.addView(list, lpWeight())
        }
        frame.addView(col, FrameLayout.LayoutParams(-1, -1))
        if (candidates.isNotEmpty()) {
            frame.addView(M3.fab(ctx, R.drawable.ic_check, "Done", if (inviteTo == null) "Create group" else "Invite") {
                if (selected.isEmpty()) {
                    act.toast("Select at least one person")
                } else if (inviteTo == null) {
                    val t = Hub.createRoom(selected)
                    act.pop(animate = false)
                    if (t != null) act.push(RoomScreen(act, t))
                } else {
                    Hub.invite(inviteTo, selected)
                    act.pop()
                }
            })
        }
        return frame
    }
}

/** Read-only view of one saved conversation. */
class HistoryDetailScreen(act: MainActivity, private val key: String) : Screen(act) {
    override val spec get() = "history|$key"

    override fun createView(): View {
        val ctx = act
        val c = M3.colors
        val entries = Hub.history.read(key)
        val title = Hub.history.conversations().firstOrNull { it.key == key }?.title ?: "History"
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        val bar = Ui.AppBar(ctx)
        bar.back { act.onBackPressed() }
        bar.setTitle(title, "${entries.size} messages")
        bar.titleBox.setPadding(ctx.dp(4), 0, 0, 0)
        bar.action(R.drawable.ic_delete, "Delete this conversation") {
            M3.dialog(
                ctx, "Delete conversation?", "This removes it from this device.",
                confirm = "Delete", onConfirm = { Hub.history.delete(key); act.pop() }, dismiss = "Cancel",
            ).show()
        }
        col.addView(bar)
        val list = Ui.listView(ctx)
        list.adapter = RowAdapter(ctx, { entries }, { e, _, _ ->
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.VERTICAL
            row.setPadding(ctx.dp(16), ctx.dp(10), ctx.dp(16), ctx.dp(10))
            val head = LinearLayout(ctx)
            head.addView(M3.text(ctx, e.sender, Type.LABEL_LARGE, if (e.outgoing) c.primary else c.onSurface))
            val t = M3.text(ctx, "  ·  " + Ui.formatDateTime(e.time), Type.LABEL_MEDIUM, c.onSurfaceVariant)
            head.addView(t)
            row.addView(head)
            val body = M3.text(ctx, e.text, Type.BODY_LARGE, c.onSurface)
            body.setPadding(0, ctx.dp(2), 0, 0)
            row.addView(body)
            row
        })
        col.addView(list, lpWeight())
        return col
    }
}
