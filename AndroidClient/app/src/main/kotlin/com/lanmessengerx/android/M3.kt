package com.lanmessengerx.android

import android.animation.ValueAnimator
import android.app.Dialog
import android.content.Context
import android.content.res.ColorStateList
import android.content.res.Configuration
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Outline
import android.graphics.Paint
import android.graphics.RectF
import android.graphics.Typeface
import android.graphics.drawable.Drawable
import android.graphics.drawable.GradientDrawable
import android.graphics.drawable.RippleDrawable
import android.graphics.drawable.StateListDrawable
import android.os.Build
import android.text.InputType
import android.text.TextUtils
import android.util.TypedValue
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.view.ViewOutlineProvider
import android.view.Window
import android.view.WindowManager
import android.view.animation.DecelerateInterpolator
import android.widget.EditText
import android.widget.FrameLayout
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.PopupWindow
import android.widget.TextView

/**
 * A small Material 3 toolkit built from plain framework views (the official Material
 * Components library is not used). Colour roles, type scale, shapes and component
 * dimensions follow the Material 3 specification; on Android 12+ the colour scheme is
 * derived from the wallpaper ("dynamic colour") exactly like the real library does.
 */
object M3 {
    class Scheme(
        val primary: Int, val onPrimary: Int, val primaryContainer: Int, val onPrimaryContainer: Int,
        val secondary: Int, val onSecondary: Int, val secondaryContainer: Int, val onSecondaryContainer: Int,
        val tertiary: Int, val tertiaryContainer: Int, val onTertiaryContainer: Int,
        val surface: Int, val onSurface: Int, val surfaceVariant: Int, val onSurfaceVariant: Int,
        val surfaceContainerLowest: Int, val surfaceContainerLow: Int, val surfaceContainer: Int,
        val surfaceContainerHigh: Int, val surfaceContainerHighest: Int,
        val outline: Int, val outlineVariant: Int,
        val error: Int, val onError: Int, val errorContainer: Int, val onErrorContainer: Int,
        val inverseSurface: Int, val inverseOnSurface: Int, val inversePrimary: Int,
        val isDark: Boolean,
    )

    // Material Theme Builder output for a teal seed (#006A6A).
    private val lightTeal = Scheme(
        primary = 0xFF006A6A.toInt(), onPrimary = 0xFFFFFFFF.toInt(),
        primaryContainer = 0xFF6FF7F6.toInt(), onPrimaryContainer = 0xFF002020.toInt(),
        secondary = 0xFF4A6363.toInt(), onSecondary = 0xFFFFFFFF.toInt(),
        secondaryContainer = 0xFFCCE8E7.toInt(), onSecondaryContainer = 0xFF051F1F.toInt(),
        tertiary = 0xFF4B607C.toInt(), tertiaryContainer = 0xFFD3E4FF.toInt(), onTertiaryContainer = 0xFF041C35.toInt(),
        surface = 0xFFF4FBFA.toInt(), onSurface = 0xFF161D1D.toInt(),
        surfaceVariant = 0xFFDAE5E4.toInt(), onSurfaceVariant = 0xFF3F4948.toInt(),
        surfaceContainerLowest = 0xFFFFFFFF.toInt(), surfaceContainerLow = 0xFFEEF5F4.toInt(),
        surfaceContainer = 0xFFE9EFEE.toInt(), surfaceContainerHigh = 0xFFE3E9E8.toInt(),
        surfaceContainerHighest = 0xFFDDE4E3.toInt(),
        outline = 0xFF6F7979.toInt(), outlineVariant = 0xFFBEC9C8.toInt(),
        error = 0xFFBA1A1A.toInt(), onError = 0xFFFFFFFF.toInt(),
        errorContainer = 0xFFFFDAD6.toInt(), onErrorContainer = 0xFF410002.toInt(),
        inverseSurface = 0xFF2B3231.toInt(), inverseOnSurface = 0xFFECF2F1.toInt(), inversePrimary = 0xFF4CDADA.toInt(),
        isDark = false,
    )

    private val darkTeal = Scheme(
        primary = 0xFF4CDADA.toInt(), onPrimary = 0xFF003737.toInt(),
        primaryContainer = 0xFF004F4F.toInt(), onPrimaryContainer = 0xFF6FF7F6.toInt(),
        secondary = 0xFFB0CCCB.toInt(), onSecondary = 0xFF1B3534.toInt(),
        secondaryContainer = 0xFF324B4B.toInt(), onSecondaryContainer = 0xFFCCE8E7.toInt(),
        tertiary = 0xFFB3C8E8.toInt(), tertiaryContainer = 0xFF334863.toInt(), onTertiaryContainer = 0xFFD3E4FF.toInt(),
        surface = 0xFF0E1514.toInt(), onSurface = 0xFFDDE4E3.toInt(),
        surfaceVariant = 0xFF3F4948.toInt(), onSurfaceVariant = 0xFFBEC9C8.toInt(),
        surfaceContainerLowest = 0xFF090F0F.toInt(), surfaceContainerLow = 0xFF161D1D.toInt(),
        surfaceContainer = 0xFF1A2121.toInt(), surfaceContainerHigh = 0xFF252B2B.toInt(),
        surfaceContainerHighest = 0xFF303636.toInt(),
        outline = 0xFF899392.toInt(), outlineVariant = 0xFF3F4948.toInt(),
        error = 0xFFFFB4AB.toInt(), onError = 0xFF690005.toInt(),
        errorContainer = 0xFF93000A.toInt(), onErrorContainer = 0xFFFFDAD6.toInt(),
        inverseSurface = 0xFFDDE4E3.toInt(), inverseOnSurface = 0xFF2B3231.toInt(), inversePrimary = 0xFF006A6A.toInt(),
        isDark = true,
    )

    var colors: Scheme = lightTeal
        private set

    fun isNight(ctx: Context) =
        (ctx.resources.configuration.uiMode and Configuration.UI_MODE_NIGHT_MASK) == Configuration.UI_MODE_NIGHT_YES

    /** Picks the colour scheme for the current night mode, using dynamic colour when asked and available. */
    fun update(ctx: Context, dynamic: Boolean) {
        val dark = isNight(ctx)
        colors = if (dynamic && Build.VERSION.SDK_INT >= 31) dynamicScheme(ctx, dark) else if (dark) darkTeal else lightTeal
    }

    private fun dynamicScheme(ctx: Context, dark: Boolean): Scheme {
        fun c(id: Int) = ctx.getColor(id)
        val r = android.R.color::class.java
        fun sys(name: String): Int = c(r.getField(name).getInt(null))
        fun a1(t: Int) = sys("system_accent1_$t")
        fun a2(t: Int) = sys("system_accent2_$t")
        fun a3(t: Int) = sys("system_accent3_$t")
        fun n1(t: Int) = sys("system_neutral1_$t")
        fun n2(t: Int) = sys("system_neutral2_$t")
        val base = if (dark) darkTeal else lightTeal
        return try {
            if (!dark) Scheme(
                a1(600), a1(0), a1(100), a1(900),
                a2(600), a2(0), a2(100), a2(900),
                a3(600), a3(100), a3(900),
                n1(10), n1(900), n2(100), n2(700),
                n1(0), n1(50), n1(50), n1(100), n1(100),
                n2(500), n2(200),
                base.error, base.onError, base.errorContainer, base.onErrorContainer,
                n1(800), n1(50), a1(200), false,
            ) else Scheme(
                a1(200), a1(800), a1(700), a1(100),
                a2(200), a2(800), a2(700), a2(100),
                a3(200), a3(700), a3(100),
                n1(900), n1(100), n2(700), n2(200),
                n1(1000), n1(900), n1(900), n1(800), n1(800),
                n2(400), n2(700),
                base.error, base.onError, base.errorContainer, base.onErrorContainer,
                n1(100), n1(800), a1(600), true,
            )
        } catch (e: Exception) {
            base
        }
    }

    // ---- type scale ---------------------------------------------------------------------------

    enum class Type(val sp: Float, val medium: Boolean, val spacing: Float) {
        HEADLINE_SMALL(24f, false, 0f),
        TITLE_LARGE(22f, false, 0f),
        TITLE_MEDIUM(16f, true, 0.009f),
        TITLE_SMALL(14f, true, 0.007f),
        BODY_LARGE(16f, false, 0.03f),
        BODY_MEDIUM(14f, false, 0.018f),
        BODY_SMALL(12f, false, 0.033f),
        LABEL_LARGE(14f, true, 0.007f),
        LABEL_MEDIUM(12f, true, 0.042f),
        LABEL_SMALL(11f, true, 0.045f),
    }

    private val regular: Typeface by lazy { Typeface.create("sans-serif", Typeface.NORMAL) }
    private val mediumFace: Typeface by lazy { Typeface.create("sans-serif-medium", Typeface.NORMAL) }

    fun TextView.style(t: Type, color: Int = colors.onSurface): TextView {
        setTextSize(TypedValue.COMPLEX_UNIT_SP, t.sp)
        typeface = if (t.medium) mediumFace else regular
        letterSpacing = t.spacing
        setTextColor(color)
        includeFontPadding = false
        return this
    }

    fun text(ctx: Context, s: CharSequence, t: Type, color: Int = colors.onSurface): TextView =
        TextView(ctx).also { it.text = s; it.style(t, color) }

    // ---- shapes / drawables -------------------------------------------------------------------

    fun Context.dp(v: Int): Int = (v * resources.displayMetrics.density + 0.5f).toInt()
    fun Context.dpf(v: Float): Float = v * resources.displayMetrics.density
    fun View.dp(v: Int): Int = context.dp(v)

    fun roundRect(ctx: Context, color: Int, radiusDp: Float, strokeColor: Int = 0, strokeDp: Float = 0f): GradientDrawable =
        GradientDrawable().apply {
            setColor(color)
            cornerRadius = ctx.dpf(radiusDp)
            if (strokeDp > 0f) setStroke(maxOf(1, ctx.dpf(strokeDp).toInt()), strokeColor)
        }

    fun circle(color: Int): GradientDrawable = GradientDrawable().apply { shape = GradientDrawable.OVAL; setColor(color) }

    fun withAlpha(color: Int, alpha: Float): Int = (color and 0x00FFFFFF) or ((alpha * 255).toInt() shl 24)

    /** Material state layer: a ripple that tints [content] with [stateColor] (hover/press 10-12%). */
    fun ripple(ctx: Context, content: Drawable?, radiusDp: Float, stateColor: Int = colors.onSurface): Drawable {
        val mask = GradientDrawable().apply { setColor(Color.WHITE); cornerRadius = ctx.dpf(radiusDp) }
        return RippleDrawable(ColorStateList.valueOf(withAlpha(stateColor, 0.12f)), content, mask)
    }

    fun circleRipple(content: Drawable?, stateColor: Int = colors.onSurface): Drawable {
        val mask = GradientDrawable().apply { shape = GradientDrawable.OVAL; setColor(Color.WHITE) }
        return RippleDrawable(ColorStateList.valueOf(withAlpha(stateColor, 0.12f)), content, mask)
    }

    fun roundOutline(radiusPx: Float): ViewOutlineProvider = object : ViewOutlineProvider() {
        override fun getOutline(view: View, outline: Outline) {
            outline.setRoundRect(0, 0, view.width, view.height, radiusPx)
        }
    }

    // ---- icons --------------------------------------------------------------------------------

    fun icon(ctx: Context, res: Int, tint: Int = colors.onSurfaceVariant, sizeDp: Int = 24): ImageView =
        ImageView(ctx).apply {
            setImageResource(res)
            imageTintList = ColorStateList.valueOf(tint)
            layoutParams = LinearLayout.LayoutParams(ctx.dp(sizeDp), ctx.dp(sizeDp))
            scaleType = ImageView.ScaleType.FIT_CENTER
        }

    /** 48dp touch target icon button. */
    fun iconButton(ctx: Context, res: Int, desc: String, tint: Int = colors.onSurfaceVariant, onClick: (View) -> Unit): FrameLayout {
        val box = FrameLayout(ctx)
        box.layoutParams = LinearLayout.LayoutParams(ctx.dp(48), ctx.dp(48))
        box.background = circleRipple(null)
        box.isClickable = true
        box.contentDescription = desc
        val img = icon(ctx, res, tint)
        img.layoutParams = FrameLayout.LayoutParams(ctx.dp(24), ctx.dp(24), Gravity.CENTER)
        box.addView(img)
        box.setOnClickListener(onClick)
        return box
    }

    // ---- buttons ------------------------------------------------------------------------------

    enum class ButtonKind { FILLED, TONAL, OUTLINED, TEXT, ERROR }

    fun button(ctx: Context, label: String, kind: ButtonKind = ButtonKind.FILLED, iconRes: Int = 0, onClick: (View) -> Unit): LinearLayout {
        val c = colors
        val (bg, fg) = when (kind) {
            ButtonKind.FILLED -> c.primary to c.onPrimary
            ButtonKind.TONAL -> c.secondaryContainer to c.onSecondaryContainer
            ButtonKind.OUTLINED -> 0 to c.primary
            ButtonKind.TEXT -> 0 to c.primary
            ButtonKind.ERROR -> c.error to c.onError
        }
        val row = LinearLayout(ctx)
        row.orientation = LinearLayout.HORIZONTAL
        row.gravity = Gravity.CENTER
        row.minimumHeight = ctx.dp(40)
        val padStart = if (iconRes != 0) 16 else if (kind == ButtonKind.TEXT) 12 else 24
        row.setPadding(ctx.dp(padStart), 0, ctx.dp(if (kind == ButtonKind.TEXT) 12 else 24), 0)
        val shape = when (kind) {
            ButtonKind.OUTLINED -> roundRect(ctx, 0, 20f, c.outline, 1f)
            ButtonKind.TEXT -> null
            else -> roundRect(ctx, bg, 20f)
        }
        row.background = ripple(ctx, shape, 20f, fg)
        row.isClickable = true
        if (iconRes != 0) {
            val ic = icon(ctx, iconRes, fg, 18)
            (ic.layoutParams as LinearLayout.LayoutParams).marginEnd = ctx.dp(8)
            row.addView(ic)
        }
        row.addView(text(ctx, label, Type.LABEL_LARGE, fg))
        row.setOnClickListener(onClick)
        row.layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ctx.dp(40))
        return row
    }

    fun fab(ctx: Context, iconRes: Int, desc: String, extendedLabel: String? = null, onClick: (View) -> Unit): View {
        val c = colors
        val row = LinearLayout(ctx)
        row.orientation = LinearLayout.HORIZONTAL
        row.gravity = Gravity.CENTER
        row.contentDescription = desc
        row.background = ripple(ctx, roundRect(ctx, c.primaryContainer, 16f), 16f, c.onPrimaryContainer)
        row.elevation = ctx.dpf(6f)
        row.outlineProvider = roundOutline(ctx.dpf(16f))
        row.clipToOutline = false
        row.isClickable = true
        row.minimumHeight = ctx.dp(56)
        row.setPadding(ctx.dp(16), 0, ctx.dp(if (extendedLabel != null) 20 else 16), 0)
        row.addView(icon(ctx, iconRes, c.onPrimaryContainer))
        if (extendedLabel != null) {
            val t = text(ctx, extendedLabel, Type.LABEL_LARGE, c.onPrimaryContainer)
            t.setPadding(ctx.dp(12), 0, 0, 0)
            row.addView(t)
        }
        row.setOnClickListener(onClick)
        row.layoutParams = FrameLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ctx.dp(56)).also {
            it.gravity = Gravity.BOTTOM or Gravity.END
            it.setMargins(0, 0, ctx.dp(16), ctx.dp(16))
        }
        return row
    }

    // ---- chips ---------------------------------------------------------------------------------

    fun chip(ctx: Context, label: String, selected: Boolean, onClick: (View) -> Unit): TextView {
        val c = colors
        val t = text(ctx, label, Type.LABEL_LARGE, if (selected) c.onSecondaryContainer else c.onSurfaceVariant)
        t.gravity = Gravity.CENTER
        t.setPadding(ctx.dp(16), 0, ctx.dp(16), 0)
        val shape = if (selected) roundRect(ctx, c.secondaryContainer, 8f) else roundRect(ctx, 0, 8f, c.outline, 1f)
        t.background = ripple(ctx, shape, 8f)
        t.isClickable = true
        t.setOnClickListener(onClick)
        t.layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ctx.dp(32))
        return t
    }

    // ---- text field ----------------------------------------------------------------------------

    /** Outlined text field (label shown as hint). */
    fun outlinedField(ctx: Context, hint: String, multiline: Boolean = false, text: String = ""): EditText {
        val c = colors
        val e = EditText(ctx)
        e.hint = hint
        e.setText(text)
        e.style(Type.BODY_LARGE, c.onSurface)
        e.setHintTextColor(c.onSurfaceVariant)
        e.setPadding(ctx.dp(16), ctx.dp(16), ctx.dp(16), ctx.dp(16))
        e.minimumHeight = ctx.dp(56)
        e.inputType = if (multiline) InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_FLAG_MULTI_LINE or InputType.TYPE_TEXT_FLAG_CAP_SENTENCES
        else InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_FLAG_CAP_SENTENCES
        if (!multiline) e.setSingleLine(true)
        val sl = StateListDrawable()
        sl.addState(intArrayOf(android.R.attr.state_focused), roundRect(ctx, 0, 4f, c.primary, 2f))
        sl.addState(intArrayOf(), roundRect(ctx, 0, 4f, c.outline, 1f))
        e.background = sl
        e.highlightColor = withAlpha(c.primary, 0.3f)
        return e
    }

    // ---- switch --------------------------------------------------------------------------------

    class M3Switch(ctx: Context) : View(ctx) {
        var checked = false
            private set
        var onChange: ((Boolean) -> Unit)? = null
        private var progress = 0f
        private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
        private val rect = RectF()

        init {
            isClickable = true
            setOnClickListener { set(!checked, animate = true, notify = true) }
        }

        fun set(value: Boolean, animate: Boolean = false, notify: Boolean = false) {
            if (checked == value) return
            checked = value
            if (animate) animateTo(if (value) 1f else 0f) else { progress = if (value) 1f else 0f; invalidate() }
            if (notify) onChange?.invoke(value)
        }

        private fun animateTo(target: Float) {
            ValueAnimator.ofFloat(progress, target).apply {
                duration = 150
                interpolator = DecelerateInterpolator()
                addUpdateListener { progress = it.animatedValue as Float; invalidate() }
                start()
            }
        }

        override fun onMeasure(w: Int, h: Int) {
            setMeasuredDimension(context.dp(52), context.dp(32))
        }

        private fun mix(a: Int, b: Int, t: Float): Int = Color.argb(
            (Color.alpha(a) + (Color.alpha(b) - Color.alpha(a)) * t).toInt(),
            (Color.red(a) + (Color.red(b) - Color.red(a)) * t).toInt(),
            (Color.green(a) + (Color.green(b) - Color.green(a)) * t).toInt(),
            (Color.blue(a) + (Color.blue(b) - Color.blue(a)) * t).toInt(),
        )

        override fun onDraw(canvas: Canvas) {
            val c = colors
            val w = width.toFloat()
            val h = height.toFloat()
            rect.set(0f, 0f, w, h)
            paint.style = Paint.Style.FILL
            paint.color = mix(c.surfaceContainerHighest, c.primary, progress)
            canvas.drawRoundRect(rect, h / 2, h / 2, paint)
            if (progress < 1f) {
                paint.style = Paint.Style.STROKE
                paint.strokeWidth = context.dpf(2f)
                paint.color = withAlpha(c.outline, 1f - progress)
                rect.inset(context.dpf(1f), context.dpf(1f))
                canvas.drawRoundRect(rect, h / 2, h / 2, paint)
            }
            val r = context.dpf(8f + 4f * progress)
            val cx = context.dpf(16f) + (w - context.dpf(32f)) * progress
            paint.style = Paint.Style.FILL
            paint.color = mix(c.outline, c.onPrimary, progress)
            canvas.drawCircle(cx, h / 2, r, paint)
        }
    }

    // ---- progress ------------------------------------------------------------------------------

    class LinearProgress(ctx: Context) : View(ctx) {
        var fraction = 0f
            set(v) { field = v.coerceIn(0f, 1f); invalidate() }
        private val paint = Paint(Paint.ANTI_ALIAS_FLAG)

        override fun onMeasure(w: Int, h: Int) {
            setMeasuredDimension(MeasureSpec.getSize(w), context.dp(4))
        }

        override fun onDraw(canvas: Canvas) {
            val h = height.toFloat()
            paint.color = colors.secondaryContainer
            canvas.drawRoundRect(0f, 0f, width.toFloat(), h, h / 2, h / 2, paint)
            paint.color = colors.primary
            val w = width * fraction
            if (w > 0f) canvas.drawRoundRect(0f, 0f, maxOf(w, h), h, h / 2, h / 2, paint)
        }
    }

    // ---- popup menu ----------------------------------------------------------------------------

    fun showMenu(anchor: View, items: List<Pair<String, () -> Unit>>) {
        val ctx = anchor.context
        val c = colors
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        col.setPadding(0, ctx.dp(8), 0, ctx.dp(8))
        col.background = roundRect(ctx, c.surfaceContainer, 4f)
        val pw = PopupWindow(col, ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT, true)
        pw.elevation = ctx.dpf(8f)
        for ((label, action) in items) {
            val t = text(ctx, label, Type.BODY_LARGE, c.onSurface)
            t.gravity = Gravity.CENTER_VERTICAL
            t.minWidth = ctx.dp(112)
            t.setPadding(ctx.dp(12), 0, ctx.dp(12), 0)
            t.background = ripple(ctx, null, 0f)
            t.setOnClickListener { pw.dismiss(); action() }
            col.addView(t, LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ctx.dp(48)))
        }
        pw.showAsDropDown(anchor, 0, -anchor.height, Gravity.END)
    }

    // ---- dialog --------------------------------------------------------------------------------

    /** Basic M3 dialog: title, optional supporting text / custom body, right aligned text buttons. */
    fun dialog(
        ctx: Context, title: String, message: String? = null, body: View? = null,
        confirm: String? = null, onConfirm: (() -> Unit)? = null,
        dismiss: String? = null, onDismiss: (() -> Unit)? = null,
    ): Dialog {
        val c = colors
        val d = Dialog(ctx)
        d.requestWindowFeature(Window.FEATURE_NO_TITLE)
        val col = LinearLayout(ctx)
        col.orientation = LinearLayout.VERTICAL
        col.setPadding(ctx.dp(24), ctx.dp(24), ctx.dp(24), ctx.dp(24))
        col.background = roundRect(ctx, c.surfaceContainerHigh, 28f)
        col.addView(text(ctx, title, Type.HEADLINE_SMALL, c.onSurface))
        if (message != null) {
            val m = text(ctx, message, Type.BODY_MEDIUM, c.onSurfaceVariant)
            m.setPadding(0, ctx.dp(16), 0, 0)
            col.addView(m)
        }
        if (body != null) {
            body.setPadding(body.paddingLeft, body.paddingTop + ctx.dp(16), body.paddingRight, body.paddingBottom)
            col.addView(body)
        }
        val buttons = LinearLayout(ctx)
        buttons.orientation = LinearLayout.HORIZONTAL
        buttons.gravity = Gravity.END
        buttons.setPadding(0, ctx.dp(24), 0, 0)
        if (dismiss != null) {
            buttons.addView(button(ctx, dismiss, ButtonKind.TEXT) { d.dismiss(); onDismiss?.invoke() })
        }
        if (confirm != null) {
            val b = button(ctx, confirm, ButtonKind.TEXT) { d.dismiss(); onConfirm?.invoke() }
            (b.layoutParams as LinearLayout.LayoutParams).marginStart = ctx.dp(8)
            buttons.addView(b)
        }
        col.addView(buttons)
        d.setContentView(col)
        d.window?.let { w ->
            w.setBackgroundDrawableResource(android.R.color.transparent)
            val dm = ctx.resources.displayMetrics
            val width = minOf(dm.widthPixels - ctx.dp(48), ctx.dp(560))
            w.setLayout(width, WindowManager.LayoutParams.WRAP_CONTENT)
            w.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE or WindowManager.LayoutParams.SOFT_INPUT_STATE_VISIBLE)
        }
        return d
    }

    fun divider(ctx: Context, insetStartDp: Int = 0): View = View(ctx).apply {
        setBackgroundColor(colors.outlineVariant)
        layoutParams = LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, maxOf(1, ctx.dp(1))).also {
            it.marginStart = ctx.dp(insetStartDp)
        }
    }

    fun ellipsize(t: TextView, lines: Int = 1): TextView {
        t.maxLines = lines
        t.ellipsize = TextUtils.TruncateAt.END
        return t
    }
}
