#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QCoreApplication>
#include "messengerbridge.h"
#include "contactmodel.h"
#include "chatmodel.h"

int main(int argc, char* argv[]) {
	QGuiApplication app(argc, argv);
	QGuiApplication::setOrganizationName("LAN Messenger X");
	QGuiApplication::setOrganizationDomain("lanmessengerx");
	QGuiApplication::setApplicationName("LAN Messenger X");
	QGuiApplication::setApplicationVersion("1.0.1");

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
