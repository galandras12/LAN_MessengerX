package com.lanmessengerx.android

import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.net.wifi.WifiManager
import android.os.Build
import android.os.IBinder
import android.os.PowerManager

/**
 * Foreground service that keeps the messenger engine alive while the app is in the
 * background: holds the Wi-Fi multicast lock (needed to receive discovery broadcasts),
 * a Wi-Fi lock and a partial wake lock so messages arrive with the screen off.
 */
class MessengerService : Service() {
    private var multicastLock: WifiManager.MulticastLock? = null
    private var wifiLock: WifiManager.WifiLock? = null
    private var wakeLock: PowerManager.WakeLock? = null

    override fun onCreate() {
        super.onCreate()
        Hub.init(applicationContext)
        val n = Notifier.serviceNotification(this, getString(R.string.ch_service_desc))
        if (Build.VERSION.SDK_INT >= 29) {
            startForeground(Notifier.SERVICE_ID, n, ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)
        } else {
            startForeground(Notifier.SERVICE_ID, n)
        }
        val wm = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        multicastLock = wm.createMulticastLock("lmx-multicast").apply { setReferenceCounted(false); acquire() }
        val mode = if (Build.VERSION.SDK_INT >= 29) WifiManager.WIFI_MODE_FULL_LOW_LATENCY else WifiManager.WIFI_MODE_FULL_HIGH_PERF
        wifiLock = wm.createWifiLock(mode, "lmx-wifi").apply { setReferenceCounted(false); acquire() }
        val pm = getSystemService(Context.POWER_SERVICE) as PowerManager
        wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "lmx:engine").apply { setReferenceCounted(false); acquire() }
        Hub.startEngine()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int = START_STICKY

    override fun onTaskRemoved(rootIntent: Intent?) {
        if (!Hub.prefs.runInBackground) {
            Hub.shutdown()
            stopSelf()
        }
        super.onTaskRemoved(rootIntent)
    }

    override fun onDestroy() {
        multicastLock?.let { if (it.isHeld) it.release() }
        wifiLock?.let { if (it.isHeld) it.release() }
        wakeLock?.let { if (it.isHeld) it.release() }
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    companion object {
        fun start(ctx: Context) {
            ctx.startForegroundService(Intent(ctx, MessengerService::class.java))
        }

        fun stop(ctx: Context) {
            ctx.stopService(Intent(ctx, MessengerService::class.java))
        }
    }
}
