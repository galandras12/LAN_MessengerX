/****************************************************************************
**
** This file is part of LAN Messenger.
**
** Thin JNI wrapper around MessengerForegroundService (see
** android/src/org/qualiatech/lanmessengerx/MessengerForegroundService.java).
** A foreground service is the standard Android mechanism to keep an
** app's whole process - and therefore this app's lmcMessaging instance,
** its sockets and timers - alive and mostly exempt from Doze-mode
** network throttling while the app is not in the foreground. Without
** one, Android will eventually suspend or kill a backgrounded app's
** process, which would silently stop LAN discovery and message/file
** delivery. See Android/README.md for what this does and does not cover.
**
** No-ops on any platform other than Android (guarded internally), so it
** is safe to call unconditionally from main.cpp.
**
****************************************************************************/

#ifndef ANDROIDFOREGROUNDSERVICE_H
#define ANDROIDFOREGROUNDSERVICE_H

#include <QString>

class AndroidForegroundService {
public:
	//	Ties the foreground-service (and its visible "running in
	//	background" notification) to whether the app is actually in the
	//	background - call stop() when the app returns to the foreground.
	static void start(void);
	static void stop(void);

	//	Held for the app's whole lifetime once acquired, independent of
	//	start()/stop() above - some Wi-Fi chipsets/drivers drop multicast
	//	frames outright without this, in the foreground or not. Call once
	//	at startup; release() on shutdown.
	static void acquireMulticastLock(void);
	static void releaseMulticastLock(void);

	//	Posts a dismissible, high-importance notification (separate from
	//	the permanent low-importance "running" one above) - call this from
	//	MessengerBridge::incomingMessage/incomingFileRequest handlers, only
	//	while the app is not in the foreground. notificationId should be
	//	stable per sender (e.g. a hash of their userId) so later messages
	//	from the same sender replace their own notification instead of
	//	stacking indefinitely. No-op if POST_NOTIFICATIONS isn't granted.
	static void showMessageNotification(int notificationId, const QString& title, const QString& text);

	//	Triggers the Android 13+ (API 33+) system permission prompt for
	//	POST_NOTIFICATIONS, if not already granted. Call once at startup.
	//	A no-op below API 33 (no such runtime permission exists there).
	static void requestNotificationPermission(void);
};

#endif // ANDROIDFOREGROUNDSERVICE_H
