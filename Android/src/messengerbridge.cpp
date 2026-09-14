#include <QDateTime>
#include "messengerbridge.h"

MessengerBridge::MessengerBridge(QObject* parent) : QObject(parent) {
	pMessaging = new lmcMessaging();
	pContactModel = new ContactModel(this);

	connect(pMessaging, SIGNAL(messageReceived(MessageType, QString*, XmlMessage*)),
		this, SLOT(messaging_messageReceived(MessageType, QString*, XmlMessage*)));
	connect(pMessaging, SIGNAL(connectionStateChanged()),
		this, SLOT(messaging_connectionStateChanged()));
}

MessengerBridge::~MessengerBridge(void) {
	pMessaging->stop();
}

void MessengerBridge::start(void) {
	//	Mirrors lmcCore::loadSettings()+init() in Windows/lmc/src/lmc.cpp,
	//	minus the command-line flag parsing (silent/trace/port/config) that
	//	only makes sense for a desktop process - a bare XmlMessage is a
	//	valid, fully-optional init params object (every field is read with
	//	dataExists()/falls back to a default in lmcMessaging::init()).
	XmlMessage initParams;
	pMessaging->init(&initParams);
	pMessaging->start();

	emit startedChanged();
	refreshContacts();
}

QString MessengerBridge::localUserId(void) const {
	return pMessaging->localUser ? pMessaging->localUser->id : QString();
}

QString MessengerBridge::localUserName(void) const {
	return pMessaging->localUser ? pMessaging->localUser->name : QString();
}

bool MessengerBridge::isConnected(void) const {
	return pMessaging->isConnected();
}

void MessengerBridge::sendMessage(const QString& userId, const QString& text) {
	if(userId.isEmpty() || text.isEmpty())
		return;

	//	Field shape matches lmcChatWindow::sendMessage() in
	//	Windows/lmc/src/chatwindow.cpp, minus the font/color cosmetic
	//	fields the HTML-themed Widgets message log uses - the QML chat
	//	page renders its own bubbles and does not need them.
	XmlMessage xmlMessage;
	xmlMessage.addHeader(XN_TIME, QString::number(QDateTime::currentDateTime().toMSecsSinceEpoch()));
	xmlMessage.addData(XN_MESSAGE, text);

	QString id = userId;
	pMessaging->sendMessage(MT_Message, &id, &xmlMessage);

	ensureChatModel(userId)->appendMessage(localUserName(), text, QDateTime::currentDateTime(), true);
}

void MessengerBridge::sendBroadcast(const QString& text) {
	if(text.isEmpty())
		return;

	XmlMessage xmlMessage;
	xmlMessage.addHeader(XN_TIME, QString::number(QDateTime::currentDateTime().toMSecsSinceEpoch()));
	xmlMessage.addData(XN_BROADCAST, text);
	pMessaging->sendBroadcast(MT_Broadcast, &xmlMessage);
}

ChatModel* MessengerBridge::chatModelFor(const QString& userId) {
	return ensureChatModel(userId);
}

ChatModel* MessengerBridge::ensureChatModel(const QString& userId) {
	ChatModel* pModel = chatModels.value(userId, nullptr);
	if(!pModel) {
		pModel = new ChatModel(this);
		chatModels.insert(userId, pModel);
	}
	return pModel;
}

void MessengerBridge::refreshContacts(void) {
	pContactModel->setUsers(pMessaging->userList);
}

void MessengerBridge::messaging_connectionStateChanged(void) {
	emit connectedChanged();
}

void MessengerBridge::messaging_messageReceived(MessageType type, QString* lpszUserId, XmlMessage* pMessage) {
	switch(type) {
	//	Presence traffic: lmcMessaging has already applied the change to
	//	its own userList by the time this signal fires (see
	//	Core/src/messagingproc.cpp) - just re-sync our copy from it rather
	//	than re-parsing pMessage ourselves.
	case MT_Announce:
	case MT_Depart:
	case MT_Status:
	case MT_UserName:
	case MT_Note:
	case MT_Avatar:
		refreshContacts();
		break;

	case MT_Message:
	case MT_GroupMessage: {
		if(!lpszUserId || !pMessage)
			break;
		User* pUser = pMessaging->getUser(lpszUserId);
		QString senderName = pUser ? pUser->name : *lpszUserId;
		QString text = (type == MT_GroupMessage) ? pMessage->data(XN_GROUPMESSAGE) : pMessage->data(XN_MESSAGE);
		ensureChatModel(*lpszUserId)->appendMessage(senderName, text, QDateTime::currentDateTime(), false);
		emit incomingMessage(*lpszUserId, senderName, text);
		break;
	}

	case MT_Broadcast: {
		if(!pMessage)
			break;
		User* pUser = lpszUserId ? pMessaging->getUser(lpszUserId) : nullptr;
		QString senderName = pUser ? pUser->name : (lpszUserId ? *lpszUserId : tr("Broadcast"));
		QString text = pMessage->data(XN_BROADCAST);
		emit incomingMessage(lpszUserId ? *lpszUserId : QString(), senderName, text);
		break;
	}

	default:
		//	Everything else (file transfer, chat state, queries, group
		//	management, ...) is not wired up in this first pass - see
		//	Android/README.md.
		break;
	}
}
