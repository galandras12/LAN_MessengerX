// This file is part of LAN Messenger X.
//
// A minimal Android foreground service whose only job is to keep this
// app's process alive and (mostly) exempt from Doze-mode network
// throttling while the app is backgrounded, and to hold the Wi-Fi
// multicast lock the LAN discovery protocol (/Core's udpnetwork.cpp)
// needs to reliably receive UDP broadcast/multicast packets. It does not
// do any work of its own - the actual networking (lmcMessaging, its
// sockets and timers) keeps running in this same process, in the C++/Qt
// side of the app; without some component in the "foreground" state,
// Android will eventually suspend or kill that process while the app is
// not visible, silently stopping LAN discovery and message/file delivery.
//
// Started/stopped from C++ via JNI - see Android/src/androidforegroundservice.h/.cpp.
// See Android/README.md for what this does and does not cover.

package org.qualiatech.lanmessengerx;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.net.wifi.WifiManager;
import android.os.Build;
import android.os.IBinder;

public class MessengerForegroundService extends Service {

    private static final String CHANNEL_ID = "lanmessengerx_running";
    private static final int NOTIFICATION_ID = 1;

    private static WifiManager.MulticastLock multicastLock;

    public static void start(Context context) {
        Intent intent = new Intent(context, MessengerForegroundService.class);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.startForegroundService(intent);
        } else {
            context.startService(intent);
        }
    }

    public static void stop(Context context) {
        context.stopService(new Intent(context, MessengerForegroundService.class));
    }

    // Held for the app's whole lifetime (not just while backgrounded) -
    // some Wi-Fi chipsets/drivers drop multicast frames outright unless a
    // multicast lock is held, regardless of foreground/background state.
    // Safe to call more than once; WifiManager reference-counts acquire()
    // against release() itself.
    public static synchronized void acquireMulticastLock(Context context) {
        if (multicastLock != null && multicastLock.isHeld())
            return;
        WifiManager wifiManager = (WifiManager) context.getApplicationContext()
                .getSystemService(Context.WIFI_SERVICE);
        if (wifiManager == null)
            return;
        multicastLock = wifiManager.createMulticastLock("lanmessengerx");
        multicastLock.setReferenceCounted(true);
        multicastLock.acquire();
    }

    public static synchronized void releaseMulticastLock() {
        if (multicastLock != null && multicastLock.isHeld())
            multicastLock.release();
    }

    @Override
    public void onCreate() {
        super.onCreate();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_ID,
                    "LAN Messenger X",
                    NotificationManager.IMPORTANCE_LOW);
            channel.setDescription("Keeps LAN discovery and messaging active in the background.");
            channel.setShowBadge(false);
            NotificationManager manager = getSystemService(NotificationManager.class);
            if (manager != null)
                manager.createNotificationChannel(channel);
        }
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        Notification notification = buildNotification();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC);
        } else {
            startForeground(NOTIFICATION_ID, notification);
        }
        return START_STICKY;
    }

    private Notification buildNotification() {
        PendingIntent contentIntent = null;
        Intent launchIntent = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (launchIntent != null) {
            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M)
                flags |= PendingIntent.FLAG_IMMUTABLE;
            contentIntent = PendingIntent.getActivity(this, 0, launchIntent, flags);
        }

        Notification.Builder builder = (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O)
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);

        // NOTE: reusing the launcher icon as the notification's small icon
        // works but is not ideal - a notification icon should be a simple
        // white-on-transparent silhouette. Cosmetic polish item, not
        // functional - see Android/README.md.
        builder.setContentTitle("LAN Messenger X")
                .setContentText("Running in the background - reachable for messages and file transfers.")
                .setSmallIcon(getApplicationInfo().icon)
                .setOngoing(true);
        if (contentIntent != null)
            builder.setContentIntent(contentIntent);

        return builder.build();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}
