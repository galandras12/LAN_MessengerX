package com.lanmessengerx.android

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.graphics.drawable.Icon

/** Notification channels and builders. Minimum SDK is 26, so channels always exist. */
object Notifier {
    const val CH_SERVICE = "service"
    const val CH_MESSAGES = "messages"
    const val SERVICE_ID = 1

    fun ensureChannels(ctx: Context) {
        val nm = ctx.getSystemService(NotificationManager::class.java)
        if (nm.getNotificationChannel(CH_SERVICE) == null) {
            val ch = NotificationChannel(CH_SERVICE, ctx.getString(R.string.ch_service), NotificationManager.IMPORTANCE_LOW)
            ch.description = ctx.getString(R.string.ch_service_desc)
            ch.setShowBadge(false)
            nm.createNotificationChannel(ch)
        }
        if (nm.getNotificationChannel(CH_MESSAGES) == null) {
            val ch = NotificationChannel(CH_MESSAGES, ctx.getString(R.string.ch_messages), NotificationManager.IMPORTANCE_HIGH)
            ch.description = ctx.getString(R.string.ch_messages_desc)
            nm.createNotificationChannel(ch)
        }
    }

    private fun openIntent(ctx: Context, key: String?, value: String?, requestCode: Int): PendingIntent {
        val i = Intent(ctx, MainActivity::class.java)
        i.flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_NEW_TASK
        if (key != null) i.putExtra(key, value)
        return PendingIntent.getActivity(ctx, requestCode, i, PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
    }

    fun serviceNotification(ctx: Context, text: String): Notification {
        ensureChannels(ctx)
        return Notification.Builder(ctx, CH_SERVICE)
            .setSmallIcon(Icon.createWithResource(ctx, R.drawable.ic_chat))
            .setContentTitle(ctx.getString(R.string.app_name))
            .setContentText(text)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setCategory(Notification.CATEGORY_SERVICE)
            .setContentIntent(openIntent(ctx, null, null, 0))
            .build()
    }

    fun updateService(ctx: Context, text: String) {
        val nm = ctx.getSystemService(NotificationManager::class.java)
        nm.notify(SERVICE_ID, serviceNotification(ctx, text))
    }

    fun message(ctx: Context, key: String, openKey: String, title: String, text: String) {
        ensureChannels(ctx)
        val id = 1000 + (key.hashCode() and 0xFFFF)
        val n = Notification.Builder(ctx, CH_MESSAGES)
            .setSmallIcon(Icon.createWithResource(ctx, R.drawable.ic_chat))
            .setContentTitle(title)
            .setContentText(text)
            .setStyle(Notification.BigTextStyle().bigText(text))
            .setAutoCancel(true)
            .setCategory(Notification.CATEGORY_MESSAGE)
            .setContentIntent(openIntent(ctx, openKey, key, id))
            .build()
        ctx.getSystemService(NotificationManager::class.java).notify(id, n)
    }

    fun cancelFor(ctx: Context, key: String) {
        ctx.getSystemService(NotificationManager::class.java).cancel(1000 + (key.hashCode() and 0xFFFF))
    }
}
