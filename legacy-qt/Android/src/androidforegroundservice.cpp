#include "androidforegroundservice.h"

#ifdef Q_OS_ANDROID
#include <QJniObject>
// QNativeInterface::QAndroidApplication lives in qcoreapplication_platform.h
// in this Qt install (confirmed via findstr over the real installed
// headers), not in qnativeinterface.h - that header only declares the
// QNativeInterface namespace itself plus its platform-generic members, so
// it compiled but left QAndroidApplication undeclared ("no member named
// 'QAndroidApplication' in namespace 'QNativeInterface'").
#include <QtCore/qcoreapplication_platform.h>

namespace {

const char* kServiceClass = "org/qualiatech/lanmessengerx/MessengerForegroundService";

void callWithContext(const char* methodName) {
	QJniObject context = QNativeInterface::QAndroidApplication::context();
	if(!context.isValid())
		return;
	QJniObject::callStaticMethod<void>(kServiceClass, methodName, "(Landroid/content/Context;)V", context.object());
}

}	//	anonymous namespace
#endif

void AndroidForegroundService::start(void) {
#ifdef Q_OS_ANDROID
	callWithContext("start");
#endif
}

void AndroidForegroundService::stop(void) {
#ifdef Q_OS_ANDROID
	callWithContext("stop");
#endif
}

void AndroidForegroundService::acquireMulticastLock(void) {
#ifdef Q_OS_ANDROID
	callWithContext("acquireMulticastLock");
#endif
}

void AndroidForegroundService::releaseMulticastLock(void) {
#ifdef Q_OS_ANDROID
	QJniObject::callStaticMethod<void>(kServiceClass, "releaseMulticastLock", "()V");
#endif
}

void AndroidForegroundService::showMessageNotification(int notificationId, const QString& title, const QString& text) {
#ifdef Q_OS_ANDROID
	QJniObject context = QNativeInterface::QAndroidApplication::context();
	if(!context.isValid())
		return;
	QJniObject jTitle = QJniObject::fromString(title);
	QJniObject jText = QJniObject::fromString(text);
	QJniObject::callStaticMethod<void>(kServiceClass, "showMessageNotification",
		"(Landroid/content/Context;ILjava/lang/String;Ljava/lang/String;)V",
		context.object(), jint(notificationId), jTitle.object(), jText.object());
#else
	Q_UNUSED(notificationId);
	Q_UNUSED(title);
	Q_UNUSED(text);
#endif
}

void AndroidForegroundService::requestNotificationPermission(void) {
#ifdef Q_OS_ANDROID
	//	context() is, for this app's single QtActivity-derived activity,
	//	actually the running Activity instance - safe to pass where an
	//	Activity-typed JNI parameter is declared (requestPermissions() is
	//	an Activity/Context method, not available on a plain Context).
	QJniObject context = QNativeInterface::QAndroidApplication::context();
	if(!context.isValid())
		return;
	QJniObject::callStaticMethod<void>(kServiceClass, "requestNotificationPermission",
		"(Landroid/app/Activity;)V", context.object());
#endif
}
