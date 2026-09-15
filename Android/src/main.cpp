#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QCoreApplication>
#include <QHash>
#include "messengerbridge.h"
#include "contactmodel.h"
#include "chatmodel.h"
#include "roomlistmodel.h"
#include "historylistmodel.h"
#include "androidforegroundservice.h"

namespace {

//	Notification id for a per-sender message/file-request notification -
//	see the comment at the connect() calls below for why it's keyed by
//	sender, not per-message. Android's NotificationManager keys
//	notifications by (package, tag, id) with a null tag here, so this
//	must never collide with MessengerForegroundService's own fixed
//	NOTIFICATION_ID (1) for the persistent "running" notification -
//	otherwise one would silently replace the other. qHash()'s output
//	landing on such a small reserved value is astronomically unlikely,
//	but the guard is free, so there is no reason to rely on that.
int messageNotificationId(const QString& userId) {
	int id = int(qHash(userId) & 0x7fffffff);
	return id < 2 ? id + 2 : id;
}

}	//	anonymous namespace

int main(int argc, char* argv[]) {
	QGuiApplication app(argc, argv);
	QGuiApplication::setOrganizationName("LAN Messenger X");
	QGuiApplication::setOrganizationDomain("lanmessengerx");
	QGuiApplication::setApplicationName("LAN Messenger X");
	//	IDA_VERSION (Core/src/definitions.h), not a separate literal here -
	//	a second copy is exactly how this number drifted out of sync with
	//	the wire-protocol version gate in the first place.
	QGuiApplication::setApplicationVersion(IDA_VERSION);

	//	Held for the whole app lifetime, not just while backgrounded - see
	//	androidforegroundservice.h. A no-op on non-Android builds.
	AndroidForegroundService::acquireMulticastLock();
	QObject::connect(&app, &QCoreApplication::aboutToQuit, &AndroidForegroundService::releaseMulticastLock);

	//	Without this, Android 13+ silently drops every notification this
	//	app tries to post (the persistent "running" one and the per-
	//	message ones below alike) - ask for it once, up front. A no-op on
	//	non-Android builds and below API 33.
	AndroidForegroundService::requestNotificationPermission();

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
	qmlRegisterUncreatableType<RoomListModel>("LanMessenger", 1, 0, "RoomListModel", "Created by MessengerBridge");
	qmlRegisterUncreatableType<HistoryListModel>("LanMessenger", 1, 0, "HistoryListModel", "Created by MessengerBridge");

	MessengerBridge bridge;

	QQmlApplicationEngine engine;
	engine.rootContext()->setContextProperty("messenger", &bridge);

	QObject::connect(&engine, &QQmlApplicationEngine::objectCreationFailed, &app,
		[]() { QCoreApplication::exit(-1); }, Qt::QueuedConnection);
	engine.load(QUrl(QStringLiteral("qrc:/qml/Main.qml")));

	//	Push-style notifications for messages/file requests that arrive
	//	while the app isn't in the foreground - the app is otherwise
	//	silent about them, since the always-on foreground-service
	//	notification (see AndroidForegroundService::start/stop above) only
	//	says "running", not "you have a new message". notificationId is a
	//	hash of the sender's userId (not the message/file itself) so a
	//	burst of messages from the same sender replaces their own
	//	notification instead of stacking one per message; an incoming
	//	file request from the same sender will likewise replace a pending
	//	chat notification from them, which is an accepted simplification
	//	here rather than tracking a separate id space per kind.
	QObject::connect(&bridge, &MessengerBridge::incomingMessage, &app,
		[&app](const QString& userId, const QString& senderName, const QString& text) {
			if(QGuiApplication::applicationState() == Qt::ApplicationActive)
				return;
			AndroidForegroundService::showMessageNotification(messageNotificationId(userId), senderName, text);
		});
	QObject::connect(&bridge, &MessengerBridge::incomingFileRequest, &app,
		[&app](const QString& userId, const QString& peerName, const QString& /*fileId*/, const QString& fileName, qint64 /*fileSize*/) {
			if(QGuiApplication::applicationState() == Qt::ApplicationActive)
				return;
			AndroidForegroundService::showMessageNotification(messageNotificationId(userId), peerName,
				QGuiApplication::translate("main", "wants to send you \"%1\"").arg(fileName));
		});

	//	Started after the QML engine is loaded so every messageReceived-
	//	driven signal (connectedChanged, startedChanged, model resets) has
	//	real QML bindings to reach, rather than firing into an empty engine.
	bridge.start();

	return app.exec();
}
