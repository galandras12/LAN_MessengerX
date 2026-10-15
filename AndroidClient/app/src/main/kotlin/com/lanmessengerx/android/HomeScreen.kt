package com.lanmessengerx.android

import android.content.Context
import android.os.Build
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import com.lanmessengerx.android.M3.Type
import com.lanmessengerx.android.M3.dp
import com.lanmessengerx.android.M3.style
import com.lanmessengerx.core.Proto
import com.lanmessengerx.core.User

/** Root screen: four tabs (People, Groups, History, Settings) behind a Material 3 navigation bar. */
class HomeScreen(act: MainActivity, private var tab: Int = 0) : Screen(act) {
    override val spec: String get() = "home|$tab"

    private abstract inner class Tab {
        abstract val label: String
        abstract val icon: Int
        abstract fun build(ctx: Context): View
        abstract fun refresh()
        open fun onSelected() {}
    }

    private val tabs = listOf<Tab>(PeopleTab(), GroupsTab(), HistoryTab(), SettingsTab())
    private val tabViews = arrayOfNulls<View>(4)
    private lateinit var content: FrameLayout
    private lateinit var navItems: List<NavItem>

    private class NavItem(val box: FrameLayout, val indicator: View, val icon: android.widget.ImageView, val label: TextView, val badge: TextView)

    override fun createView(): View {
        val ctx = act
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        col.setBackgroundColor(M3.colors.surface)
        content = FrameLayout(ctx)
        col.addView(content, lpWeight())
        val nav = LinearLayout(ctx)
        nav.orientation = LinearLayout.HORIZONTAL
        nav.setBackgroundColor(M3.colors.surfaceContainer)
        nav.minimumHeight = ctx.dp(80)
        val items = ArrayList<NavItem>()
        tabs.forEachIndexed { i, t ->
            val item = navItem(ctx, t)
            item.box.setOnClickListener { select(i) }
            items.add(item)
            nav.addView(item.box, LinearLayout.LayoutParams(0, ctx.dp(80), 1f))
        }
        navItems = items
        col.addView(nav, LinearLayout.LayoutParams(-1, ctx.dp(80)))
        tabViews.fill(null)
        select(tab)
        return col
    }

    private fun navItem(ctx: Context, t: Tab): NavItem {
        val box = FrameLayout(ctx)
        box.isClickable = true
        val indicator = View(ctx)
        val ic = M3.icon(ctx, t.icon, M3.colors.onSurfaceVariant)
        val label = M3.text(ctx, t.label, Type.LABEL_MEDIUM, M3.colors.onSurfaceVariant)
        val badge = Ui.badge(ctx, 0)
        badge.visibility = View.GONE
        val inner = LinearLayout(ctx)
        inner.orientation = LinearLayout.VERTICAL
        inner.gravity = Gravity.CENTER_HORIZONTAL
        val pill = FrameLayout(ctx)
        pill.addView(indicator, FrameLayout.LayoutParams(ctx.dp(64), ctx.dp(32), Gravity.CENTER))
        pill.addView(ic, FrameLayout.LayoutParams(ctx.dp(24), ctx.dp(24), Gravity.CENTER))
        val bl = FrameLayout.LayoutParams(-2, -2, Gravity.TOP or Gravity.END)
        bl.marginEnd = ctx.dp(8)
        pill.addView(badge, bl)
        inner.addView(pill, LinearLayout.LayoutParams(ctx.dp(64), ctx.dp(32)).also { it.topMargin = ctx.dp(12) })
        val ll = LinearLayout.LayoutParams(-2, -2)
        ll.topMargin = ctx.dp(4)
        inner.addView(label, ll)
        box.addView(inner, FrameLayout.LayoutParams(-1, -2, Gravity.CENTER_HORIZONTAL))
        return NavItem(box, indicator, ic, label, badge)
    }

    private fun select(i: Int) {
        tab = i
        val ctx = act
        content.removeAllViews()
        val v = tabViews[i] ?: tabs[i].build(ctx).also { tabViews[i] = it }
        content.addView(v, FrameLayout.LayoutParams(-1, -1))
        tabs[i].onSelected()
        tabs[i].refresh()
        styleNav()
    }

    private fun styleNav() {
        val c = M3.colors
        navItems.forEachIndexed { i, n ->
            val sel = i == tab
            n.indicator.background = if (sel) M3.roundRect(act, c.secondaryContainer, 16f) else null
            n.icon.imageTintList = android.content.res.ColorStateList.valueOf(if (sel) c.onSecondaryContainer else c.onSurfaceVariant)
            n.label.style(Type.LABEL_MEDIUM, if (sel) c.onSurface else c.onSurfaceVariant)
        }
        navItems[0].badge.apply {
            val chatUnread = Hub.chats.values.sumOf { it.unread }
            visibility = if (chatUnread > 0) View.VISIBLE else View.GONE
            text = if (chatUnread > 99) "99+" else chatUnread.toString()
        }
        navItems[1].badge.apply {
            val roomUnread = Hub.rooms.values.sumOf { it.unread }
            visibility = if (roomUnread > 0) View.VISIBLE else View.GONE
            text = if (roomUnread > 99) "99+" else roomUnread.toString()
        }
    }

    override fun refresh() {
        tabs[tab].refresh()
        styleNav()
    }

    // =======================================================================================
    // Profile: picture, status, status message (shared by the People and Settings tabs)
    // =======================================================================================

    /** The 20 stock pictures of the original LAN Messenger, or a picture of your own. */
    private fun photoDialog() {
        val ctx = act
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        val d = M3.dialog(ctx, Loc.t("Profile photo"), Loc.t("Other people see this picture next to your name."), body = col, dismiss = Loc.t("Close"))
        val perRow = 5
        for (r in 0 until Avatars.STOCK_COUNT / perRow) {
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.HORIZONTAL
            for (i in r * perRow until (r + 1) * perRow) {
                val iv = android.widget.ImageView(ctx)
                iv.scaleType = android.widget.ImageView.ScaleType.CENTER_CROP
                iv.setImageBitmap(Avatars.stock(ctx, i))
                iv.background = M3.roundRect(ctx, M3.colors.surfaceContainerHighest, 12f)
                iv.clipToOutline = true
                iv.outlineProvider = android.view.ViewOutlineProvider.BACKGROUND
                iv.setOnClickListener { d.dismiss(); Hub.useStockAvatar(i) }
                val lp = LinearLayout.LayoutParams(0, ctx.dp(52), 1f)
                lp.setMargins(ctx.dp(3), ctx.dp(3), ctx.dp(3), ctx.dp(3))
                row.addView(iv, lp)
            }
            col.addView(row)
        }
        val choose = M3.button(ctx, Loc.t("Choose a picture"), M3.ButtonKind.TONAL, R.drawable.ic_edit) {
            d.dismiss()
            act.pickDocument("image/*") { uri -> if (uri != null) act.setAvatarFrom(uri) }
        }
        (choose.layoutParams as? LinearLayout.LayoutParams)?.topMargin = ctx.dp(12)
        col.addView(choose)
        if (Hub.hasAvatar) {
            val rm = M3.button(ctx, Loc.t("Remove picture"), M3.ButtonKind.TEXT, R.drawable.ic_delete) { d.dismiss(); Hub.removeAvatar() }
            (rm.layoutParams as? LinearLayout.LayoutParams)?.topMargin = ctx.dp(4)
            col.addView(rm)
        }
        d.show()
    }

    /** Tap on the profile bar: status and status message in one place. */
    private fun quickProfileDialog() {
        val ctx = act
        var picked = Hub.prefs.status
        val note = M3.outlinedField(ctx, Loc.t("Type a status message"), multiline = false, text = Hub.note)
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        col.addView(note, LinearLayout.LayoutParams(-1, -2))
        val chips = ArrayList<TextView>()
        var row: LinearLayout? = null
        Proto.STATUS_CODES.forEachIndexed { i, code ->
            if (i % 2 == 0) {
                row = LinearLayout(ctx)
                row!!.orientation = LinearLayout.HORIZONTAL
                row!!.setPadding(0, ctx.dp(8), 0, 0)
                col.addView(row)
            }
            val chip = M3.chip(ctx, Ui.statusLabel(code), picked == code) { }
            chip.setOnClickListener {
                picked = code
                chips.forEachIndexed { j, ch -> M3.setChipSelected(ch, Proto.STATUS_CODES[j] == picked) }
            }
            (chip.layoutParams as? LinearLayout.LayoutParams)?.marginEnd = ctx.dp(8)
            chips.add(chip)
            row!!.addView(chip)
        }
        val pic = M3.button(ctx, Loc.t("Profile photo"), M3.ButtonKind.TEXT, R.drawable.ic_edit) { }
        val d = M3.dialog(
            ctx, Loc.t("Status message"), body = col,
            confirm = Loc.t("Share"), onConfirm = {
                if (picked != Hub.prefs.status) Hub.setStatus(picked)
                Hub.setNote(note.text.toString().trim())
            },
            dismiss = Loc.t("Cancel"),
        )
        pic.setOnClickListener { d.dismiss(); photoDialog() }
        col.addView(pic)
        d.show()
    }

    // =======================================================================================
    // People
    // =======================================================================================

    private inner class PeopleTab : Tab() {
        override val label get() = Loc.t("People")
        override val icon = R.drawable.ic_person
        private lateinit var adapter: RowAdapter<User>
        private lateinit var bar: Ui.AppBar
        private lateinit var empty: View
        private lateinit var banner: View
        private lateinit var list: android.widget.ListView

        override fun build(ctx: Context): View {
            val frame = FrameLayout(ctx)
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            bar = Ui.AppBar(ctx)
            bar.withLeadingPadding()
            bar.setTitle(Loc.t("LAN Messenger X"), Loc.t("Connecting…"))
            bar.action(R.drawable.ic_refresh, Loc.t("Refresh")) { Hub.refresh(); act.toast(Loc.t("Looking for people on your network…")) }
            col.addView(bar)
            col.addView(profileBar(ctx))
            banner = networkBanner(ctx)
            col.addView(banner)
            list = Ui.listView(ctx)
            adapter = RowAdapter(ctx, { Hub.contacts.toList() }, { u, convert, parent -> contactRow(ctx, u, convert) })
            list.adapter = adapter
            list.setOnItemClickListener { _, _, pos, _ -> act.push(ChatScreen(act, adapter.getItem(pos).id)) }
            empty = Ui.emptyState(
                ctx, R.drawable.ic_group, Loc.t("No one else is online"),
                Loc.t("Make sure this device is on the same Wi-Fi network as the people you want to reach."),
            )
            val listBox = FrameLayout(ctx)
            listBox.addView(list, FrameLayout.LayoutParams(-1, -1))
            listBox.addView(empty, FrameLayout.LayoutParams(-1, -2, Gravity.CENTER))
            col.addView(listBox, lpWeight())
            frame.addView(col, FrameLayout.LayoutParams(-1, -1))
            frame.addView(M3.fab(ctx, R.drawable.ic_campaign, Loc.t("Send a broadcast message"), Loc.t("Broadcast")) { broadcastDialog() })
            return frame
        }

        private lateinit var meAvatar: AvatarView
        private lateinit var meName: TextView
        private lateinit var meNote: TextView

        /** Your own picture, name and status message: tap the picture to change it, the rest to change status and message. */
        private fun profileBar(ctx: Context): View {
            val c = M3.colors
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.HORIZONTAL
            row.gravity = Gravity.CENTER_VERTICAL
            row.setPadding(ctx.dp(16), ctx.dp(4), ctx.dp(16), ctx.dp(8))
            meAvatar = AvatarView(ctx, 48)
            meAvatar.setOnClickListener { photoDialog() }
            row.addView(meAvatar, LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48)))
            val box = LinearLayout(ctx)
            box.orientation = LinearLayout.VERTICAL
            box.setPadding(ctx.dp(16), 0, 0, 0)
            meName = M3.ellipsize(M3.text(ctx, "", Type.TITLE_MEDIUM, c.onSurface))
            meNote = M3.ellipsize(M3.text(ctx, "", Type.BODY_MEDIUM, c.onSurfaceVariant))
            box.addView(meName)
            box.addView(meNote)
            row.addView(box, LinearLayout.LayoutParams(0, -2, 1f))
            row.background = M3.ripple(ctx, null, 0f)
            row.setOnClickListener { quickProfileDialog() }
            return row
        }

        private fun networkBanner(ctx: Context): View {
            val c = M3.colors
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.HORIZONTAL
            row.gravity = Gravity.CENTER_VERTICAL
            row.background = M3.roundRect(ctx, c.errorContainer, 12f)
            row.setPadding(ctx.dp(16), ctx.dp(12), ctx.dp(16), ctx.dp(12))
            row.addView(M3.icon(ctx, R.drawable.ic_wifi, c.onErrorContainer))
            val t = M3.text(ctx, Loc.t("Not connected to a network. Connect to Wi-Fi to find people."), Type.BODY_MEDIUM, c.onErrorContainer)
            t.setPadding(ctx.dp(12), 0, 0, 0)
            row.addView(t)
            val lp = LinearLayout.LayoutParams(-1, -2)
            lp.setMargins(ctx.dp(16), ctx.dp(4), ctx.dp(16), ctx.dp(8))
            row.layoutParams = lp
            return row
        }

        private fun contactRow(ctx: Context, u: User, convert: View?): View {
            val c = M3.colors
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.HORIZONTAL
            row.gravity = Gravity.CENTER_VERTICAL
            row.minimumHeight = ctx.dp(72)
            row.setPadding(ctx.dp(16), ctx.dp(8), ctx.dp(24), ctx.dp(8))
            row.background = M3.ripple(ctx, null, 0f)
            val avatar = AvatarView(ctx, 48)
            avatar.bind(u.name, Avatars.load(u.id, u.avatarPath, 96), u.status)
            row.addView(avatar, LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48)))
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            col.setPadding(ctx.dp(16), 0, ctx.dp(8), 0)
            col.addView(M3.ellipsize(M3.text(ctx, u.name, Type.BODY_LARGE, c.onSurface)))
            val chat = Hub.chats[u.id]
            val sub = when {
                chat?.typing == true -> Loc.t("typing…")
                u.note.isNotBlank() -> u.note
                else -> Ui.statusLabel(u.status)
            }
            val s = M3.ellipsize(M3.text(ctx, sub, Type.BODY_MEDIUM, if (chat?.typing == true) c.primary else c.onSurfaceVariant))
            s.setPadding(0, ctx.dp(2), 0, 0)
            col.addView(s)
            row.addView(col, LinearLayout.LayoutParams(0, -2, 1f))
            val unread = chat?.unread ?: 0
            if (unread > 0) row.addView(Ui.badge(ctx, unread))
            return row
        }

        override fun refresh() {
            adapter.reload()
            meAvatar.bind(Hub.localUserName, Avatars.load("local", if (Hub.hasAvatar) Hub.avatarFile.absolutePath else null, 96), Hub.prefs.status)
            meName.text = Hub.localUserName
            meNote.text = Hub.note.ifBlank { Ui.statusLabel(Hub.prefs.status) }
            bar.setTitle(
                "LAN Messenger X",
                when {
                    !Hub.connected -> Loc.t("Waiting for a network…")
                    Hub.contacts.isEmpty() -> Loc.f("Online · {0}", Hub.ip ?: "")
                    else -> Loc.f("{0} online · {1}", Hub.contacts.size, Hub.ip ?: "")
                },
            )
            banner.visibility = if (Hub.connected) View.GONE else View.VISIBLE
            empty.visibility = if (Hub.contacts.isEmpty()) View.VISIBLE else View.GONE
        }
    }

    private fun broadcastDialog() {
        val ctx = act
        if (Hub.contacts.none { !it.isOffline }) {
            act.toast(Loc.t("Nobody is online to receive it"))
            return
        }
        val field = M3.outlinedField(ctx, Loc.t("Message to everyone online"), multiline = true)
        field.minLines = 3
        field.gravity = Gravity.TOP
        val d = M3.dialog(
            ctx, Loc.t("Broadcast message"), Loc.f("Sent to {0} people who are online right now.", Hub.contacts.count { !it.isOffline }),
            body = LinearLayout(ctx).also { it.addView(field, LinearLayout.LayoutParams(-1, -2)) },
            confirm = Loc.t("Send"), onConfirm = {
                val t = field.text.toString().trim()
                if (t.isNotEmpty()) Hub.sendBroadcast(t)
            },
            dismiss = Loc.t("Cancel"),
        )
        d.show()
    }

    // =======================================================================================
    // Groups
    // =======================================================================================

    private inner class GroupsTab : Tab() {
        override val label get() = Loc.t("Groups")
        override val icon = R.drawable.ic_group
        private lateinit var adapter: RowAdapter<Room>
        private lateinit var empty: View

        override fun build(ctx: Context): View {
            val frame = FrameLayout(ctx)
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            val bar = Ui.AppBar(ctx)
            bar.withLeadingPadding()
            bar.setTitle(Loc.t("Group chats"))
            col.addView(bar)
            col.addView(publicRow(ctx))
            val list = Ui.listView(ctx)
            adapter = RowAdapter(ctx, { Hub.rooms.values.sortedByDescending { it.lastTime } }, { r, _, _ -> roomRow(ctx, r) })
            list.adapter = adapter
            list.setOnItemClickListener { _, _, pos, _ -> act.push(RoomScreen(act, adapter.getItem(pos).threadId)) }
            empty = Ui.emptyState(ctx, R.drawable.ic_group_add, Loc.t("No group chats yet"), Loc.t("Start one with the button below, or wait for an invitation."))
            val box = FrameLayout(ctx)
            box.addView(list, FrameLayout.LayoutParams(-1, -1))
            box.addView(empty, FrameLayout.LayoutParams(-1, -2, Gravity.CENTER))
            col.addView(box, lpWeight())
            frame.addView(col, FrameLayout.LayoutParams(-1, -1))
            frame.addView(M3.fab(ctx, R.drawable.ic_group_add, Loc.t("New group chat"), Loc.t("New group")) { act.push(NewGroupScreen(act, null)) })
            return frame
        }

        private lateinit var publicBadge: TextView

        /** The public chat: written to everyone on the network who has it open. */
        private fun publicRow(ctx: Context): View {
            val c = M3.colors
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.HORIZONTAL
            row.gravity = Gravity.CENTER_VERTICAL
            row.minimumHeight = ctx.dp(72)
            row.setPadding(ctx.dp(16), ctx.dp(8), ctx.dp(24), ctx.dp(8))
            row.background = M3.ripple(ctx, null, 0f)
            val circle = FrameLayout(ctx)
            circle.background = M3.circle(c.secondaryContainer)
            circle.addView(M3.icon(ctx, R.drawable.ic_public, c.onSecondaryContainer), FrameLayout.LayoutParams(ctx.dp(24), ctx.dp(24), Gravity.CENTER))
            row.addView(circle, LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48)))
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            col.setPadding(ctx.dp(16), 0, ctx.dp(8), 0)
            col.addView(M3.ellipsize(M3.text(ctx, Loc.t("Public chat"), Type.BODY_LARGE, c.onSurface)))
            col.addView(M3.ellipsize(M3.text(ctx, Loc.t("Everyone on the network"), Type.BODY_MEDIUM, c.onSurfaceVariant)))
            row.addView(col, LinearLayout.LayoutParams(0, -2, 1f))
            publicBadge = Ui.badge(ctx, 0)
            publicBadge.visibility = View.GONE
            row.addView(publicBadge)
            row.setOnClickListener { act.push(RoomScreen(act, "public")) }
            return row
        }

        private fun roomRow(ctx: Context, r: Room): View {
            val c = M3.colors
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.HORIZONTAL
            row.gravity = Gravity.CENTER_VERTICAL
            row.minimumHeight = ctx.dp(72)
            row.setPadding(ctx.dp(16), ctx.dp(8), ctx.dp(24), ctx.dp(8))
            row.background = M3.ripple(ctx, null, 0f)
            val circle = FrameLayout(ctx)
            circle.background = M3.circle(c.tertiaryContainer)
            circle.addView(M3.icon(ctx, R.drawable.ic_group, c.onTertiaryContainer), FrameLayout.LayoutParams(ctx.dp(24), ctx.dp(24), Gravity.CENTER))
            row.addView(circle, LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48)))
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            col.setPadding(ctx.dp(16), 0, ctx.dp(8), 0)
            col.addView(M3.ellipsize(M3.text(ctx, Hub.roomTitle(r), Type.BODY_LARGE, c.onSurface)))
            val last = r.items.lastOrNull { it is ChatItem.Msg } as ChatItem.Msg?
            val sub = if (last != null) "${last.sender}: ${last.text}" else Loc.f("{0} participants", r.participants.size)
            val s = M3.ellipsize(M3.text(ctx, sub, Type.BODY_MEDIUM, c.onSurfaceVariant))
            s.setPadding(0, ctx.dp(2), 0, 0)
            col.addView(s)
            row.addView(col, LinearLayout.LayoutParams(0, -2, 1f))
            if (r.unread > 0) row.addView(Ui.badge(ctx, r.unread))
            return row
        }

        override fun refresh() {
            adapter.reload()
            publicBadge.visibility = if (Hub.publicRoom.unread > 0) View.VISIBLE else View.GONE
            publicBadge.text = Hub.publicRoom.unread.toString()
            empty.visibility = if (Hub.rooms.isEmpty()) View.VISIBLE else View.GONE
        }
    }

    // =======================================================================================
    // History
    // =======================================================================================

    private inner class HistoryTab : Tab() {
        override val label get() = Loc.t("History")
        override val icon = R.drawable.ic_history
        private lateinit var adapter: RowAdapter<com.lanmessengerx.android.HistoryStore.Conversation>
        private lateinit var empty: View
        private var data: List<HistoryStore.Conversation> = emptyList()

        override fun build(ctx: Context): View {
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            val bar = Ui.AppBar(ctx)
            bar.withLeadingPadding()
            bar.setTitle(Loc.t("History"))
            bar.action(R.drawable.ic_delete, Loc.t("Clear all history")) {
                M3.dialog(
                    ctx, Loc.t("Clear history?"), Loc.t("All saved conversations will be deleted from this device."),
                    confirm = Loc.t("Clear"), onConfirm = { Hub.history.clear(); refresh() }, dismiss = Loc.t("Cancel"),
                ).show()
            }
            col.addView(bar)
            val list = Ui.listView(ctx)
            adapter = RowAdapter(ctx, { data }, { c, _, _ -> row(ctx, c) })
            list.adapter = adapter
            list.setOnItemClickListener { _, _, pos, _ -> act.push(HistoryDetailScreen(act, adapter.getItem(pos).key)) }
            empty = Ui.emptyState(ctx, R.drawable.ic_history, Loc.t("Nothing saved yet"), Loc.t("Conversations are stored on this device when history is turned on in Settings."))
            val box = FrameLayout(ctx)
            box.addView(list, FrameLayout.LayoutParams(-1, -1))
            box.addView(empty, FrameLayout.LayoutParams(-1, -2, Gravity.CENTER))
            col.addView(box, lpWeight())
            return col
        }

        private fun row(ctx: Context, conv: HistoryStore.Conversation): View {
            val c = M3.colors
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.HORIZONTAL
            row.gravity = Gravity.CENTER_VERTICAL
            row.minimumHeight = ctx.dp(72)
            row.setPadding(ctx.dp(16), ctx.dp(8), ctx.dp(24), ctx.dp(8))
            row.background = M3.ripple(ctx, null, 0f)
            val a = AvatarView(ctx, 48)
            a.bind(conv.title, null)
            row.addView(a, LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48)))
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            col.setPadding(ctx.dp(16), 0, 0, 0)
            col.addView(M3.ellipsize(M3.text(ctx, conv.title, Type.BODY_LARGE, c.onSurface)))
            val s = M3.text(ctx, Loc.f("{0} messages · {1}", conv.count, Ui.formatDateTime(conv.lastTime)), Type.BODY_MEDIUM, c.onSurfaceVariant)
            s.setPadding(0, ctx.dp(2), 0, 0)
            col.addView(s)
            row.addView(col, LinearLayout.LayoutParams(0, -2, 1f))
            return row
        }

        override fun onSelected() {
            data = Hub.history.conversations()
        }

        override fun refresh() {
            data = Hub.history.conversations()
            adapter.reload()
            empty.visibility = if (data.isEmpty()) View.VISIBLE else View.GONE
        }
    }

    // =======================================================================================
    // Settings
    // =======================================================================================

    private inner class SettingsTab : Tab() {
        override val label get() = Loc.t("Settings")
        override val icon = R.drawable.ic_settings
        private lateinit var profileName: TextView
        private lateinit var profileNote: TextView
        private lateinit var avatar: AvatarView
        private lateinit var statusRow: LinearLayout
        private lateinit var netText: TextView

        override fun build(ctx: Context): View {
            val c = M3.colors
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            val bar = Ui.AppBar(ctx)
            bar.withLeadingPadding()
            bar.setTitle(Loc.t("Settings"))
            col.addView(bar)
            val scroll = ScrollView(ctx)
            scroll.isVerticalScrollBarEnabled = false
            val body = LinearLayout(ctx)
            body.orientation = LinearLayout.VERTICAL
            body.setPadding(0, 0, 0, ctx.dp(24))
            scroll.addView(body, FrameLayout.LayoutParams(-1, -2))
            col.addView(scroll, lpWeight())

            // profile card
            val card = LinearLayout(ctx)
            card.orientation = LinearLayout.VERTICAL
            card.background = M3.roundRect(ctx, c.surfaceContainerHighest, 12f)
            card.setPadding(ctx.dp(16), ctx.dp(16), ctx.dp(16), ctx.dp(8))
            val top = LinearLayout(ctx)
            top.gravity = Gravity.CENTER_VERTICAL
            avatar = AvatarView(ctx, 72)
            top.addView(avatar, LinearLayout.LayoutParams(ctx.dp(72), ctx.dp(72)))
            val names = LinearLayout(ctx)
            names.orientation = LinearLayout.VERTICAL
            names.setPadding(ctx.dp(16), 0, 0, 0)
            profileName = M3.ellipsize(M3.text(ctx, Loc.t(""), Type.TITLE_LARGE, c.onSurface))
            profileNote = M3.ellipsize(M3.text(ctx, Loc.t(""), Type.BODY_MEDIUM, c.onSurfaceVariant), 2)
            profileNote.setPadding(0, ctx.dp(2), 0, 0)
            names.addView(profileName)
            names.addView(profileNote)
            top.addView(names, LinearLayout.LayoutParams(0, -2, 1f))
            card.addView(top)
            val actions = LinearLayout(ctx)
            actions.gravity = Gravity.END
            actions.setPadding(0, ctx.dp(8), 0, 0)
            actions.addView(M3.button(ctx, Loc.t("Photo"), M3.ButtonKind.TEXT, R.drawable.ic_edit) { photoDialog() })
            actions.addView(M3.button(ctx, Loc.t("Edit"), M3.ButtonKind.TEXT, R.drawable.ic_edit) { editProfileDialog() })
            card.addView(actions)
            body.addView(card, LinearLayout.LayoutParams(-1, -2).also { it.setMargins(ctx.dp(16), ctx.dp(8), ctx.dp(16), 0) })

            // status
            body.addView(Ui.sectionHeader(ctx, Loc.t("Status")))
            statusRow = LinearLayout(ctx)
            statusRow.orientation = LinearLayout.VERTICAL
            statusRow.setPadding(ctx.dp(16), 0, ctx.dp(16), 0)
            body.addView(statusRow)

            // preferences
            body.addView(Ui.sectionHeader(ctx, Loc.t("Preferences")))
            body.addView(switchRow(ctx, Loc.t("Save conversation history"), Loc.t("Keep chats on this device so you can read them later"), Hub.prefs.saveHistory) { Hub.prefs.saveHistory = it })
            body.addView(switchRow(ctx, Loc.t("Notifications"), Loc.t("Show alerts for messages, files and broadcasts"), Hub.prefs.notifications) { Hub.prefs.notifications = it })
            body.addView(switchRow(ctx, Loc.t("Keep running in background"), Loc.t("Stay reachable while the app is closed (shows a status notification)"), Hub.prefs.runInBackground) { Hub.prefs.runInBackground = it })
            if (Build.VERSION.SDK_INT >= 31) {
                body.addView(switchRow(ctx, Loc.t("Dynamic colour"), Loc.t("Use colours from your wallpaper"), Hub.prefs.dynamicColor) {
                    Hub.prefs.dynamicColor = it
                    act.restyle()
                })
            }

            // language
            body.addView(Ui.sectionHeader(ctx, Loc.t("Language")))
            val currentLang = Loc.languages.firstOrNull { it.first == Hub.prefs.language }?.second ?: Loc.t("System default")
            val langRow = actionRow(ctx, Loc.t("Language"), currentLang) { anchor ->
                val items = ArrayList<Pair<String, () -> Unit>>()
                items.add(Loc.t("System default") to { Hub.setLanguage("system"); act.restyle() })
                for ((code, name) in Loc.languages) items.add(name to { Hub.setLanguage(code); act.restyle() })
                M3.showMenu(anchor, items)
            }
            body.addView(langRow)

            // network
            body.addView(Ui.sectionHeader(ctx, Loc.t("Network")))
            netText = M3.text(ctx, Loc.t(""), Type.BODY_MEDIUM, c.onSurfaceVariant)
            netText.setPadding(ctx.dp(16), 0, ctx.dp(16), ctx.dp(8))
            body.addView(netText)
            val netBtn = M3.button(ctx, Loc.t("Look for people again"), M3.ButtonKind.TONAL, R.drawable.ic_refresh) { Hub.refresh(); act.toast(Loc.t("Looking for people on your network…")) }
            body.addView(netBtn, LinearLayout.LayoutParams(-2, ctx.dp(40)).also { it.marginStart = ctx.dp(16) })

            // about
            body.addView(Ui.sectionHeader(ctx, Loc.t("About")))
            val about = LinearLayout(ctx)
            about.orientation = LinearLayout.VERTICAL
            about.background = M3.roundRect(ctx, c.surfaceContainerLow, 12f, c.outlineVariant, 1f)
            about.setPadding(ctx.dp(16), ctx.dp(16), ctx.dp(16), ctx.dp(16))
            about.addView(M3.text(ctx, "LAN Messenger X ${Proto.VERSION}", Type.TITLE_MEDIUM, c.onSurface))
            val d = M3.text(
                ctx,
                Loc.t("Serverless messaging for local networks. Speaks the original LAN Messenger protocol, so it works with the Windows, Linux and older clients on the same network.") + "\n\n" +
                    Loc.t("Original LAN Messenger by Dilip Radhakrishnan (Qualia Digital Solutions). Modernised and continued by galandras12.") + "\n\n" +
                    Loc.t("Free software, GNU GPL version 3."),
                Type.BODY_MEDIUM, c.onSurfaceVariant,
            )
            d.setPadding(0, ctx.dp(8), 0, 0)
            about.addView(d)
            body.addView(about, LinearLayout.LayoutParams(-1, -2).also { it.setMargins(ctx.dp(16), 0, ctx.dp(16), 0) })
            return col
        }

        private fun actionRow(ctx: Context, title: String, value: String, onClick: (View) -> Unit): View {
            val c = M3.colors
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.VERTICAL
            row.minimumHeight = ctx.dp(64)
            row.gravity = Gravity.CENTER_VERTICAL
            row.setPadding(ctx.dp(16), ctx.dp(8), ctx.dp(24), ctx.dp(8))
            row.background = M3.ripple(ctx, null, 0f)
            row.addView(M3.text(ctx, title, Type.BODY_LARGE, c.onSurface))
            val s = M3.text(ctx, value, Type.BODY_MEDIUM, c.onSurfaceVariant)
            s.setPadding(0, ctx.dp(2), 0, 0)
            row.addView(s)
            row.setOnClickListener { onClick(row) }
            return row
        }

        private fun switchRow(ctx: Context, title: String, desc: String, checked: Boolean, onChange: (Boolean) -> Unit): View {
            val c = M3.colors
            val row = LinearLayout(ctx)
            row.orientation = LinearLayout.HORIZONTAL
            row.gravity = Gravity.CENTER_VERTICAL
            row.minimumHeight = ctx.dp(64)
            row.setPadding(ctx.dp(16), ctx.dp(8), ctx.dp(24), ctx.dp(8))
            row.background = M3.ripple(ctx, null, 0f)
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            col.addView(M3.text(ctx, title, Type.BODY_LARGE, c.onSurface))
            val s = M3.text(ctx, desc, Type.BODY_MEDIUM, c.onSurfaceVariant)
            s.setPadding(0, ctx.dp(2), 0, 0)
            col.addView(s)
            row.addView(col, LinearLayout.LayoutParams(0, -2, 1f))
            val sw = M3.M3Switch(ctx)
            sw.set(checked)
            sw.onChange = onChange
            row.addView(sw, LinearLayout.LayoutParams(ctx.dp(52), ctx.dp(32)).also { it.marginStart = ctx.dp(16) })
            row.setOnClickListener { sw.set(!sw.checked, animate = true, notify = true) }
            return row
        }

        private fun editProfileDialog() {
            val ctx = act
            val name = M3.outlinedField(ctx, Loc.t("Name"), text = Hub.localUserName)
            val note = M3.outlinedField(ctx, Loc.t("Note (optional)"), multiline = true, text = Hub.note)
            val col = LinearLayout(ctx)
            col.orientation = LinearLayout.VERTICAL
            col.addView(name, LinearLayout.LayoutParams(-1, -2))
            val lp = LinearLayout.LayoutParams(-1, -2)
            lp.topMargin = ctx.dp(12)
            col.addView(note, lp)
            M3.dialog(
                ctx, Loc.t("Edit profile"), body = col,
                confirm = Loc.t("Save"), onConfirm = {
                    Hub.setName(name.text.toString())
                    Hub.setNote(note.text.toString().trim())
                },
                dismiss = Loc.t("Cancel"),
            ).show()
        }

        override fun refresh() {
            profileName.text = Hub.localUserName
            profileNote.text = Hub.note.ifBlank { Ui.statusLabel(Hub.prefs.status) }
            avatar.bind(Hub.localUserName, Avatars.load("local", if (Hub.hasAvatar) Hub.avatarFile.absolutePath else null, 144), Hub.prefs.status)
            statusRow.removeAllViews()
            val ctx = act
            val flow = LinearLayout(ctx)
            flow.orientation = LinearLayout.VERTICAL
            var row: LinearLayout? = null
            Proto.STATUS_CODES.forEachIndexed { i, code ->
                if (i % 3 == 0) {
                    row = LinearLayout(ctx)
                    row!!.orientation = LinearLayout.HORIZONTAL
                    row!!.setPadding(0, 0, 0, ctx.dp(8))
                    flow.addView(row)
                }
                val chip = M3.chip(ctx, Ui.statusLabel(code), Hub.prefs.status == code) { Hub.setStatus(code) }
                val lp = chip.layoutParams as LinearLayout.LayoutParams
                lp.marginEnd = ctx.dp(8)
                row!!.addView(chip)
            }
            statusRow.addView(flow)
            netText.text = if (Hub.connected) Loc.f("Connected as {0}", Hub.ip ?: "") + "\n" + Loc.f("UDP/TCP port {0} · multicast {1}", Proto.DEFAULT_PORT, Proto.DEFAULT_MULTICAST)
            else Loc.t("Not connected. Join a Wi-Fi network to find other people.")
        }
    }
}
