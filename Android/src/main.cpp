#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QCoreApplication>
#include "messengerbridge.h"
#include "contactmodel.h"
#include "chatmodel.h"
#include "androidforegroundservice.h"

int main(int argc, char* argv[]) {
	QGuiApplication app(argc, argv);
	QGuiApplication::setOrganizationName("LAN Messenger X");
	QGuiApplication::setOrganizationDomain("lanmessengerx");
	QGuiApplication::setApplicationName("LAN Messenger X");
	QGuiApplication::setApplicationVersion("1.0.1");

	//	Held for the whole app lifetime, not just while backgrounded - see
	//	androidforegroundservice.h. A no-op on non-Android builds.
	AndroidForegroundService::acquireMulticastLock();
	QObject::connect(&app, &QCoreApplication::aboutToQuit, &AndroidForegroundService::releaseMulticastLock);

	//	The foreground service (and its visible notification) is only
	//	needed while the app isn't in the foreground itself - starting it
	//	unconditionally would show a permanent notification even while the
	//	user is actively looking at the app, which is neither necessary
	//	(Android does not suspend a foreground app's process) nor good UX.
	QObject::connect(&app, &QGuiApplication::applicationStateChanged, [](Qt::ApplicationState state) {
		if(state == Qt::ApplicationActive)
			AndroidForegroundService::stop();
		else if(state == Qt::ApplicationHidden)
			AndroidForegroundService::start();
	});

	//	Registered so QML can see ContactModel/ChatModel's Q_PROPERTY role
	//	data via the instances MessengerBridge hands out - QML never
	//	constructs these itself.
	qmlRegisterUncreatableType<ContactModel>("LanMessenger", 1, 0, "ContactModel", "Created by MessengerBridge");
	qmlRegisterUncreatableType<ChatModel>("LanMessenger", 1, 0, "ChatModel", "Created by MessengerBridge");

	MessengerBridge bridge;

	QQmlApplicationEngine engine;
	engine.rootContext()->setContextProperty("messenger", &bridge);

	QObject::connect(&engine, &QQmlApplicationEngine::objectCreationFailed, &app,
		[]() { QCoreApplication::exit(-1); }, Qt::QueuedConnection);
	engine.load(QUrl(QStringLiteral("qrc:/qml/Main.qml")));

	//	Started after the QML engine is loaded so every messageReceived-
	//	driven signal (connectedChanged, startedChanged, model resets) has
	//	real QML bindings to reach, rather than firing into an empty engine.
	bridge.start();

	return app.exec();
}
