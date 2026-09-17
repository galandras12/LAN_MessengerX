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

import android.Manifest;
import android.app.Activity;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ServiceInfo;
import android.net.wifi.WifiManager;
import android.os.Build;
import android.os.IBinder;

public class MessengerForegroundService extends Service {

    private static final String CHANNEL_ID = "lanmessengerx_running";
    private static final int NOTIFICATION_ID = 1;

    // Separate, higher-importance channel from the always-on "running in
    // background" one above - this one is for actual new-message/incoming-
    // file-request notifications the user should be interrupted for, so it
    // must not share the low-importance silent channel of the persistent
    // service notification.
    private static final String MESSAGE_CHANNEL_ID = "lanmessengerx_messages";
    private static final int NOTIFICATION_PERMISSION_REQUEST_CODE = 1001;

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

    // Called from AndroidForegroundService::showMessageNotification (C++)
    // whenever MessengerBridge::incomingMessage/incomingFileRequest fires
    // while the app is not in the foreground - this is a distinct,
    // dismissible, high-importance notification per new message/file
    // request, not the permanent low-importance "running" one above.
    // notificationId is chosen by the caller (C++ hashes the sender's
    // userId) so later messages from the same sender update/replace their
    // own notification instead of stacking indefinitely.
    public static void showMessageNotification(Context context, int notificationId, String title, String text) {
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null)
            return;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU
                && context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
            return;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                && manager.getNotificationChannel(MESSAGE_CHANNEL_ID) == null) {
            NotificationChannel channel = new NotificationChannel(
                    MESSAGE_CHANNEL_ID,
                    "New messages",
                    NotificationManager.IMPORTANCE_HIGH);
            channel.setDescription("New chat messages and incoming file requests received while LAN Messenger X is in the background.");
            manager.createNotificationChannel(channel);
        }

        PendingIntent contentIntent = null;
        Intent launchIntent = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
        if (launchIntent != null) {
            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M)
                flags |= PendingIntent.FLAG_IMMUTABLE;
            contentIntent = PendingIntent.getActivity(context, notificationId, launchIntent, flags);
        }

        // minSdkVersion is 28, always >= O (26), so the pre-channel
        // Notification.Builder(Context) constructor is unreachable dead
        // code here - real javac warning: "[deprecation] Builder(Context)
        // in Builder has been deprecated". Likewise setPriority() is
        // redundant/deprecated once a channel exists: MESSAGE_CHANNEL_ID
        // above is already created with IMPORTANCE_HIGH, which governs
        // priority on O+.
        Notification.Builder builder = new Notification.Builder(context, MESSAGE_CHANNEL_ID);

        builder.setContentTitle(title)
                .setContentText(text)
                .setSmallIcon(context.getApplicationInfo().icon)
                .setAutoCancel(true);
        if (contentIntent != null)
            builder.setContentIntent(contentIntent);

        manager.notify(notificationId, builder.build());
    }

    // Called once at startup (see AndroidForegroundService::
    // requestNotificationPermission). Android 13+ (API 33+) requires this
    // runtime-granted permission before either this service's persistent
    // notification or showMessageNotification() above can actually be
    // shown to the user - without it, both silently post nothing (the
    // service/process itself still runs fine either way). There is no
    // custom Activity subclass in this app to receive
    // onRequestPermissionsResult, so the outcome isn't observed here; the
    // system remembers the user's choice regardless.
    public static void requestNotificationPermission(Activity activity) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU)
            return;
        if (activity.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED)
            return;
        activity.requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS}, NOTIFICATION_PERMISSION_REQUEST_CODE);
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

        // See the same dead-code note in showMessageNotification() above -
        // minSdkVersion 28 guarantees the O+ branch always applies.
        Notification.Builder builder = new Notification.Builder(this, CHANNEL_ID);

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
