package com.lanmessengerx.android

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Path
import android.graphics.Typeface
import android.util.LruCache
import android.view.View
import com.lanmessengerx.android.M3.dp
import com.lanmessengerx.android.M3.dpf
import com.lanmessengerx.core.Proto
import java.io.File

object Avatars {
    private val cache = LruCache<String, Bitmap>(64)
    private val missing = HashSet<String>()

    const val STOCK_COUNT = 20

    /** The 20 stock pictures of the original LAN Messenger (assets/avatars/N.png). */
    fun stock(ctx: Context, index: Int): Bitmap? {
        val key = "stock$index"
        synchronized(cache) { cache.get(key)?.let { return it } }
        val bmp = try {
            ctx.assets.open("avatars/$index.png").use { BitmapFactory.decodeStream(it) }
        } catch (_: Exception) {
            null
        }
        if (bmp != null) synchronized(cache) { cache.put(key, bmp) }
        return bmp
    }

    fun invalidate(key: String) {
        synchronized(cache) {
            cache.remove(key)
            missing.remove(key)
        }
    }

    /** Decodes [path] (a PNG/JPEG file) down to roughly [size] pixels; null if unreadable. */
    fun load(key: String, path: String?, size: Int): Bitmap? {
        if (path == null) return null
        synchronized(cache) {
            cache.get(key)?.let { return it }
            if (key in missing) return null
        }
        val f = File(path)
        var bmp: Bitmap? = null
        if (f.isFile) {
            try {
                val o = BitmapFactory.Options().apply { inJustDecodeBounds = true }
                BitmapFactory.decodeFile(path, o)
                var sample = 1
                while (o.outWidth / (sample * 2) >= size && o.outHeight / (sample * 2) >= size) sample *= 2
                bmp = BitmapFactory.decodeFile(path, BitmapFactory.Options().apply { inSampleSize = sample })
            } catch (_: Throwable) {
            }
        }
        synchronized(cache) {
            if (bmp != null) cache.put(key, bmp) else missing.add(key)
        }
        return bmp
    }

    /** Square centre crop scaled to [px] and written as PNG, as the original client expects. */
    fun saveSquarePng(source: Bitmap, target: File, px: Int = 128): Boolean {
        val side = minOf(source.width, source.height)
        val x = (source.width - side) / 2
        val y = (source.height - side) / 2
        val square = Bitmap.createBitmap(source, x, y, side, side)
        val scaled = Bitmap.createScaledBitmap(square, px, px, true)
        return try {
            target.parentFile?.mkdirs()
            target.outputStream().use { scaled.compress(Bitmap.CompressFormat.PNG, 100, it) }
            true
        } catch (_: Exception) {
            false
        }
    }
}

/** Round avatar: picture if there is one, otherwise the initial on a tonal colour. Optional presence dot. */
class AvatarView(ctx: Context, private val sizeDp: Int = 40) : View(ctx) {
    private var bitmap: Bitmap? = null
    private var initial = "?"
    private var seed = 0
    private var presence: String? = null
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val clip = Path()

    fun bind(name: String, bitmap: Bitmap?, status: String? = null) {
        this.initial = name.trim().firstOrNull()?.uppercaseChar()?.toString() ?: "?"
        this.seed = name.hashCode()
        this.bitmap = bitmap
        this.presence = status
        invalidate()
    }

    override fun onMeasure(w: Int, h: Int) {
        setMeasuredDimension(context.dp(sizeDp), context.dp(sizeDp))
    }

    override fun onDraw(canvas: Canvas) {
        val c = M3.colors
        val r = width / 2f
        val b = bitmap
        if (b != null) {
            clip.reset()
            clip.addCircle(r, r, r, Path.Direction.CW)
            canvas.save()
            canvas.clipPath(clip)
            val side = minOf(b.width, b.height)
            val src = android.graphics.Rect((b.width - side) / 2, (b.height - side) / 2, (b.width + side) / 2, (b.height + side) / 2)
            canvas.drawBitmap(b, src, android.graphics.RectF(0f, 0f, width.toFloat(), height.toFloat()), paint)
            canvas.restore()
        } else {
            val (bg, fg) = when (Math.floorMod(seed, 4)) {
                0 -> c.primaryContainer to c.onPrimaryContainer
                1 -> c.secondaryContainer to c.onSecondaryContainer
                2 -> c.tertiaryContainer to c.onTertiaryContainer
                else -> c.errorContainer to c.onErrorContainer
            }
            paint.style = Paint.Style.FILL
            paint.color = bg
            canvas.drawCircle(r, r, r, paint)
            paint.color = fg
            paint.textAlign = Paint.Align.CENTER
            paint.typeface = Typeface.create("sans-serif-medium", Typeface.NORMAL)
            paint.textSize = width * 0.42f
            val y = r - (paint.descent() + paint.ascent()) / 2
            canvas.drawText(initial, r, y, paint)
        }
        val st = presence
        if (st != null) {
            val dot = width * 0.28f
            val cx = width - dot / 2 - 1f
            val cy = height - dot / 2 - 1f
            paint.style = Paint.Style.FILL
            paint.color = c.surface
            canvas.drawCircle(cx, cy, dot / 2 + context.dpf(1.5f), paint)
            paint.color = presenceColor(st)
            canvas.drawCircle(cx, cy, dot / 2, paint)
        }
    }

    companion object {
        fun presenceColor(status: String): Int = when {
            Proto.isOffline(status) -> 0xFF9E9E9E.toInt()
            Proto.isBusy(status) -> 0xFFD32F2F.toInt()
            Proto.isAway(status) -> 0xFFF9A825.toInt()
            else -> 0xFF2E9E5B.toInt()
        }
    }
}
