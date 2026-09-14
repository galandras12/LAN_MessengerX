#include "androidforegroundservice.h"

#ifdef Q_OS_ANDROID
#include <QJniObject>
#include <QNativeInterface>

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
