package com.lanmessengerx.android

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.BitmapFactory
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.view.View
import android.view.WindowInsetsController
import android.widget.FrameLayout
import android.widget.Toast

class MainActivity : Activity(), Hub.Listener {
    private lateinit var root: FrameLayout
    private val stack = ArrayList<Screen>()
    private var resultHandler: ((Uri?) -> Unit)? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        Hub.init(applicationContext)
        applyTheme()
        root = FrameLayout(this)
        root.setBackgroundColor(M3.colors.surface)
        setContentView(root)

        MessengerService.start(this)
        requestNotificationPermission()

        val saved = savedInstanceState?.getStringArrayList("stack")
        if (saved.isNullOrEmpty()) {
            push(HomeScreen(this), animate = false)
        } else {
            for (spec in saved) restore(spec)
            if (stack.isEmpty()) push(HomeScreen(this), animate = false)
        }
        handleIntent(intent)
    }

    private fun applyTheme() {
        M3.update(this, Hub.prefs.dynamicColor)
        val c = M3.colors
        window.statusBarColor = c.surface
        window.navigationBarColor = c.surfaceContainer
        val light = !c.isDark
        if (Build.VERSION.SDK_INT >= 30) {
            val flags = WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS or WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS
            window.insetsController?.setSystemBarsAppearance(if (light) flags else 0, flags)
        } else {
            var v = window.decorView.systemUiVisibility
            v = if (light) v or View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR else v and View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR.inv()
            if (Build.VERSION.SDK_INT >= 26) {
                v = if (light) v or View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR else v and View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR.inv()
            }
            window.decorView.systemUiVisibility = v
        }
    }

    /** Recreates the whole UI (used when colour related settings change). */
    fun restyle() {
        recreate()
    }

    private fun restore(spec: String) {
        val parts = spec.split('|', limit = 2)
        when (parts[0]) {
            "home" -> push(HomeScreen(this, parts.getOrNull(1)?.toIntOrNull() ?: 0), animate = false)
            "chat" -> push(ChatScreen(this, parts[1]), animate = false)
            "room" -> push(RoomScreen(this, parts[1]), animate = false)
            "history" -> push(HistoryDetailScreen(this, parts[1]), animate = false)
        }
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        outState.putStringArrayList("stack", ArrayList(stack.map { it.spec }.filter { !it.startsWith("new-group") }))
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        handleIntent(intent)
    }

    private fun handleIntent(i: Intent?) {
        i ?: return
        i.getStringExtra("user")?.let { id ->
            while (stack.size > 1) pop(animate = false)
            push(ChatScreen(this, id))
            i.removeExtra("user")
        }
        i.getStringExtra("room")?.let { id ->
            while (stack.size > 1) pop(animate = false)
            push(RoomScreen(this, id))
            i.removeExtra("room")
        }
    }

    // ---- screen stack -----------------------------------------------------------------------

    fun push(s: Screen, animate: Boolean = true) {
        stack.lastOrNull()?.let { it.onHide(); root.removeView(root.getChildAt(root.childCount - 1)) }
        stack.add(s)
        val v = s.createView()
        root.addView(v, FrameLayout.LayoutParams(-1, -1))
        if (animate) {
            v.translationX = resources.displayMetrics.widthPixels * 0.15f
            v.alpha = 0f
            v.animate().translationX(0f).alpha(1f).setDuration(180).start()
        }
        s.onShow()
    }

    fun pop(animate: Boolean = true): Boolean {
        if (stack.size <= 1) return false
        val old = stack.removeAt(stack.size - 1)
        old.onHide()
        root.removeAllViews()
        val top = stack.last()
        val v = top.createView()
        root.addView(v, FrameLayout.LayoutParams(-1, -1))
        if (animate) {
            v.alpha = 0f
            v.animate().alpha(1f).setDuration(150).start()
        }
        top.onShow()
        return true
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() {
        val top = stack.lastOrNull()
        if (top != null && top.onBack()) return
        if (!pop()) {
            @Suppress("DEPRECATION")
            super.onBackPressed()
        }
    }

    // ---- lifecycle ----------------------------------------------------------------------------

    override fun onStart() {
        super.onStart()
        Hub.appVisible = true
        Hub.addListener(this)
        stack.lastOrNull()?.refresh()
    }

    override fun onStop() {
        Hub.appVisible = false
        Hub.removeListener(this)
        super.onStop()
    }

    override fun onDestroy() {
        if (isFinishing && !Hub.prefs.runInBackground) {
            Hub.shutdown()
            MessengerService.stop(this)
        }
        super.onDestroy()
    }

    override fun onHubChanged() {
        stack.lastOrNull()?.refresh()
    }

    // ---- system pickers and permissions ------------------------------------------------------

    /** A folder (document tree) to send. */
    fun pickTree(handler: (Uri?) -> Unit) {
        resultHandler = handler
        @Suppress("DEPRECATION")
        startActivityForResult(Intent(Intent.ACTION_OPEN_DOCUMENT_TREE), REQ_TREE)
    }

    fun pickDocument(mime: String, handler: (Uri?) -> Unit) {
        resultHandler = handler
        val i = Intent(Intent.ACTION_OPEN_DOCUMENT)
        i.addCategory(Intent.CATEGORY_OPENABLE)
        i.type = mime
        @Suppress("DEPRECATION")
        startActivityForResult(i, REQ_PICK)
    }

    @Deprecated("Deprecated in Java")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode == REQ_TREE) {
            val h = resultHandler
            resultHandler = null
            val uri = if (resultCode == RESULT_OK) data?.data else null
            if (uri != null) {
                try {
                    contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
                } catch (_: Exception) {
                }
            }
            h?.invoke(uri)
            return
        }
        if (requestCode == REQ_PICK) {
            val h = resultHandler
            resultHandler = null
            val uri = if (resultCode == RESULT_OK) data?.data else null
            if (uri != null) {
                try {
                    contentResolver.takePersistableUriPermission(uri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
                } catch (_: Exception) {
                }
            }
            h?.invoke(uri)
        }
    }

    private fun requestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= 33 && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
            && !Hub.prefs.askedNotificationPermission
        ) {
            Hub.prefs.askedNotificationPermission = true
            requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 7)
        }
    }

    fun toast(msg: String) = Toast.makeText(this, msg, Toast.LENGTH_SHORT).show()

    /** Reads an image chosen by the user, crops it to a square PNG and publishes it as our avatar. */
    fun setAvatarFrom(uri: Uri) {
        try {
            val opts = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            contentResolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, opts) }
            var sample = 1
            while (opts.outWidth / (sample * 2) >= 512 && opts.outHeight / (sample * 2) >= 512) sample *= 2
            val bmp = contentResolver.openInputStream(uri)?.use {
                BitmapFactory.decodeStream(it, null, BitmapFactory.Options().apply { inSampleSize = sample })
            }
            if (bmp != null && Avatars.saveSquarePng(bmp, Hub.avatarFile)) {
                Hub.avatarSaved()
            } else toast(Loc.t("Could not read that image"))
        } catch (e: Exception) {
            toast(Loc.t("Could not read that image"))
        }
    }

    fun openReceived(location: String?) {
        if (location == null) return
        try {
            val uri = if (location.startsWith("content://")) Uri.parse(location) else {
                toast(location)
                return
            }
            val mime = contentResolver.getType(uri) ?: "*/*"
            val i = Intent(Intent.ACTION_VIEW).setDataAndType(uri, mime).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            startActivity(Intent.createChooser(i, null))
        } catch (e: Exception) {
            toast(Loc.t("No app can open this file"))
        }
    }

    companion object {
        const val REQ_PICK = 11
        const val REQ_TREE = 12
    }
}
