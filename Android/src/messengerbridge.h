/****************************************************************************
**
** This file is part of LAN Messenger.
**
** MessengerBridge is the Android client's equivalent of what lmc.cpp
** (lmcCore) is for the Windows client: it owns the lmcMessaging engine
** from /Core and reacts to its single unified messageReceived(MessageType,
** ...) signal, which - as in the original Windows client - carries both
** presence traffic (MT_Announce/MT_Depart/MT_Status/MT_UserName/MT_Note/
** MT_Avatar) and chat content (MT_Message/MT_GroupMessage/MT_Broadcast)
** over the same channel. Unlike lmc.cpp, this class has no knowledge of
** any UI toolkit - it only exposes Q_PROPERTY/Q_INVOKABLE members for QML
** to bind to, keeping /Core exactly as reusable as intended (see
** /Core/README.md).
**
** Scope note: file transfer, chat rooms, and settings are not wired up
** yet - only presence and 1:1 messaging. See /Android/README.md.
**
****************************************************************************/

#ifndef MESSENGERBRIDGE_H
#define MESSENGERBRIDGE_H

#include <QObject>
#include <QMap>
#include "messaging.h"
#include "contactmodel.h"
#include "chatmodel.h"

class MessengerBridge : public QObject {
	Q_OBJECT
	Q_PROPERTY(QString localUserId READ localUserId NOTIFY startedChanged)
	Q_PROPERTY(QString localUserName READ localUserName NOTIFY startedChanged)
	Q_PROPERTY(bool connected READ isConnected NOTIFY connectedChanged)
	Q_PROPERTY(ContactModel* contacts READ contacts CONSTANT)

public:
	explicit MessengerBridge(QObject* parent = nullptr);
	~MessengerBridge(void);

	QString localUserId(void) const;
	QString localUserName(void) const;
	bool isConnected(void) const;
	ContactModel* contacts(void) const { return pContactModel; }

	//	Starts the network engine. Called once from main.cpp after the QML
	//	engine is set up, not from the constructor, so QML bindings exist
	//	before any signal can fire.
	Q_INVOKABLE void start(void);

	Q_INVOKABLE void sendMessage(const QString& userId, const QString& text);
	Q_INVOKABLE void sendBroadcast(const QString& text);

	//	Returns (creating if needed) the conversation model for a given
	//	peer, for a QML chat page to bind its ListView model to.
	Q_INVOKABLE ChatModel* chatModelFor(const QString& userId);

signals:
	void startedChanged(void);
	void connectedChanged(void);
	void incomingMessage(const QString& userId, const QString& senderName, const QString& text);

private slots:
	void messaging_messageReceived(MessageType type, QString* lpszUserId, XmlMessage* pMessage);
	void messaging_connectionStateChanged(void);

private:
	void refreshContacts(void);
	ChatModel* ensureChatModel(const QString& userId);

	lmcMessaging* pMessaging;
	ContactModel* pContactModel;
	QMap<QString, ChatModel*> chatModels;
};

#endif // MESSENGERBRIDGE_H
