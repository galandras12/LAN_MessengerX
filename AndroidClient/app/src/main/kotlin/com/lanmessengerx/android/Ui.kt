package com.lanmessengerx.android

import android.content.Context
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.BaseAdapter
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.ListView
import android.widget.TextView
import com.lanmessengerx.android.M3.Type
import com.lanmessengerx.android.M3.dp
import com.lanmessengerx.android.M3.style
import java.text.DateFormat
import java.util.Calendar
import java.util.Date

/** One full screen of the app, hosted by [MainActivity]. */
abstract class Screen(val act: MainActivity) {
    abstract val spec: String
    abstract fun createView(): View
    open fun onShow() {}
    open fun onHide() {}
    open fun refresh() {}
    open fun onBack(): Boolean = false
}

fun lpMatch() = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT)

fun lpWeight(w: Float = 1f, horizontal: Boolean = false): LinearLayout.LayoutParams =
    if (horizontal) LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, w)
    else LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, w)

/** Generic ListView adapter that rebuilds its rows from a snapshot. */
class RowAdapter<T>(
    private val ctx: Context,
    private val source: () -> List<T>,
    private val build: (T, View?, ViewGroup) -> View,
    private val viewTypeOf: (T) -> Int = { 0 },
    private val typeCount: Int = 1,
) : BaseAdapter() {
    private var snapshot: List<T> = emptyList()

    init {
        snapshot = source()
    }

    fun reload() {
        snapshot = source()
        notifyDataSetChanged()
    }

    override fun getCount() = snapshot.size
    override fun getItem(position: Int): T = snapshot[position]
    override fun getItemId(position: Int) = position.toLong()
    override fun getViewTypeCount() = typeCount
    override fun getItemViewType(position: Int) = viewTypeOf(snapshot[position])
    override fun getView(position: Int, convertView: View?, parent: ViewGroup): View = build(snapshot[position], convertView, parent)
}

object Ui {
    /** Small top app bar: optional back arrow, title + optional subtitle, trailing action buttons. */
    class AppBar(ctx: Context) : LinearLayout(ctx) {
        val titleView = TextView(ctx)
        val subtitleView = TextView(ctx)
        val leading = LinearLayout(ctx)
        val titleBox = LinearLayout(ctx)
        val actions = LinearLayout(ctx)

        init {
            orientation = HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            minimumHeight = ctx.dp(64)
            setBackgroundColor(M3.colors.surface)
            setPadding(ctx.dp(4), 0, ctx.dp(4), 0)
            leading.orientation = HORIZONTAL
            leading.gravity = Gravity.CENTER_VERTICAL
            addView(leading)
            titleBox.orientation = VERTICAL
            titleBox.gravity = Gravity.CENTER_VERTICAL
            titleView.style(Type.TITLE_LARGE, M3.colors.onSurface)
            M3.ellipsize(titleView)
            subtitleView.style(Type.BODY_SMALL, M3.colors.onSurfaceVariant)
            M3.ellipsize(subtitleView)
            subtitleView.visibility = View.GONE
            titleBox.addView(titleView)
            titleBox.addView(subtitleView)
            addView(titleBox, LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))
            actions.orientation = HORIZONTAL
            addView(actions)
        }

        fun setTitle(t: String, sub: String? = null) {
            titleView.text = t
            if (sub.isNullOrEmpty()) {
                subtitleView.visibility = View.GONE
            } else {
                subtitleView.text = sub
                subtitleView.visibility = View.VISIBLE
            }
        }

        fun back(onClick: () -> Unit) {
            leading.removeAllViews()
            leading.addView(M3.iconButton(context, R.drawable.ic_arrow_back, "Back", M3.colors.onSurface) { onClick() })
        }

        fun withLeadingPadding() {
            titleBox.setPadding(context.dp(12), 0, 0, 0)
        }

        fun action(res: Int, desc: String, onClick: (View) -> Unit): View {
            val b = M3.iconButton(context, res, desc, M3.colors.onSurfaceVariant, onClick)
            actions.addView(b)
            return b
        }
    }

    fun emptyState(ctx: Context, iconRes: Int, title: String, message: String): View {
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        col.gravity = Gravity.CENTER
        col.setPadding(ctx.dp(32), ctx.dp(32), ctx.dp(32), ctx.dp(32))
        val circle = FrameLayout(ctx)
        circle.background = M3.circle(M3.colors.secondaryContainer)
        val ic = M3.icon(ctx, iconRes, M3.colors.onSecondaryContainer, 36)
        circle.addView(ic, FrameLayout.LayoutParams(ctx.dp(36), ctx.dp(36), Gravity.CENTER))
        col.addView(circle, LinearLayout.LayoutParams(ctx.dp(80), ctx.dp(80)))
        val t = M3.text(ctx, title, Type.TITLE_MEDIUM, M3.colors.onSurface)
        t.gravity = Gravity.CENTER
        t.setPadding(0, ctx.dp(20), 0, ctx.dp(4))
        col.addView(t)
        val m = M3.text(ctx, message, Type.BODY_MEDIUM, M3.colors.onSurfaceVariant)
        m.gravity = Gravity.CENTER
        col.addView(m)
        return col
    }

    fun sectionHeader(ctx: Context, label: String): TextView {
        val t = M3.text(ctx, label, Type.LABEL_LARGE, M3.colors.primary)
        t.setPadding(ctx.dp(16), ctx.dp(16), ctx.dp(16), ctx.dp(8))
        return t
    }

    fun listView(ctx: Context): ListView = ListView(ctx).apply {
        divider = null
        dividerHeight = 0
        setSelector(android.R.color.transparent)
        isVerticalScrollBarEnabled = false
        setBackgroundColor(M3.colors.surface)
        cacheColorHint = 0
    }

    fun badge(ctx: Context, n: Int): TextView {
        val t = M3.text(ctx, if (n > 99) "99+" else n.toString(), Type.LABEL_SMALL, M3.colors.onPrimary)
        t.gravity = Gravity.CENTER
        t.minWidth = ctx.dp(20)
        t.minHeight = ctx.dp(20)
        t.setPadding(ctx.dp(6), ctx.dp(2), ctx.dp(6), ctx.dp(2))
        t.background = M3.roundRect(ctx, M3.colors.primary, 10f)
        return t
    }

    private val timeFmt: DateFormat get() = DateFormat.getTimeInstance(DateFormat.SHORT)
    private val dateFmt: DateFormat get() = DateFormat.getDateInstance(DateFormat.MEDIUM)

    fun formatTime(ms: Long): String = timeFmt.format(Date(ms))

    fun sameDay(a: Long, b: Long): Boolean {
        val ca = Calendar.getInstance().apply { timeInMillis = a }
        val cb = Calendar.getInstance().apply { timeInMillis = b }
        return ca.get(Calendar.YEAR) == cb.get(Calendar.YEAR) && ca.get(Calendar.DAY_OF_YEAR) == cb.get(Calendar.DAY_OF_YEAR)
    }

    fun formatDay(ms: Long): String {
        val now = System.currentTimeMillis()
        return when {
            sameDay(ms, now) -> "Today"
            sameDay(ms, now - 86_400_000L) -> "Yesterday"
            else -> dateFmt.format(Date(ms))
        }
    }

    fun formatDateTime(ms: Long): String = formatDay(ms) + ", " + formatTime(ms)

    fun formatSize(size: Long): String = when {
        size >= 1L shl 30 -> "%.2f GB".format(size / (1024.0 * 1024 * 1024))
        size >= 1L shl 20 -> "%.1f MB".format(size / (1024.0 * 1024))
        size >= 1L shl 10 -> "%.0f KB".format(size / 1024.0)
        else -> "$size B"
    }

    fun statusLabel(code: String): String = when (code) {
        "chat" -> "Available"
        "busy" -> "Busy"
        "dnd" -> "Do not disturb"
        "brb" -> "Be right back"
        "away" -> "Away"
        "gone" -> "Appear offline"
        else -> code
    }
}
