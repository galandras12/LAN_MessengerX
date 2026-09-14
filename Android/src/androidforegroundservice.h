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
};

#endif // ANDROIDFOREGROUNDSERVICE_H
