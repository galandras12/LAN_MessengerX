#include <QDateTime>
#include "messengerbridge.h"

MessengerBridge::MessengerBridge(QObject* parent) : QObject(parent) {
	pMessaging = new lmcMessaging();
	pContactModel = new ContactModel(this);
	pRoomListModel = new RoomListModel(this);

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

void MessengerBridge::sendFile(const QString& userId, const QUrl& fileUrl) {
	if(userId.isEmpty() || fileUrl.isEmpty())
		return;

	//	QUrl::toLocalFile() returns an empty string for anything that
	//	isn't a plain file:// URL - notably a content:// URL, which is
	//	what Android's Storage Access Framework often hands back from a
	//	native file picker (e.g. for Downloads or cloud storage). See the
	//	class comment and Android/README.md.
	QString filePath = fileUrl.toLocalFile();
	if(filePath.isEmpty())
		return;

	//	Field shape matches lmcChatWindow::sendFile()/sendObject() in
	//	Windows/lmc/src/chatwindow.cpp. Deliberately does NOT touch
	//	ChatModel here - Core echoes this request back to us (with the
	//	fileId it assigns) through messageReceived(), which is what
	//	actually adds the entry - see the class comment.
	XmlMessage xmlMessage;
	xmlMessage.addData(XN_FILETYPE, FileTypeNames[FT_Normal]);
	xmlMessage.addData(XN_FILEOP, FileOpNames[FO_Request]);
	xmlMessage.addData(XN_FILEPATH, filePath);

	QString id = userId;
	pMessaging->sendMessage(MT_File, &id, &xmlMessage);
}

void MessengerBridge::acceptFile(const QString& userId, const QString& fileId) {
	if(userId.isEmpty() || fileId.isEmpty())
		return;

	//	Core fills in the actual save path/filename itself from the
	//	transfer it recorded when the request first arrived (see
	//	lmcMessaging::updateFileTransfer's FO_Accept/FM_Receive branch in
	//	Core/src/filemessagingproc.cpp) - we only need to echo the id back.
	XmlMessage xmlMessage;
	xmlMessage.addData(XN_MODE, FileModeNames[FM_Receive]);
	xmlMessage.addData(XN_FILETYPE, FileTypeNames[FT_Normal]);
	xmlMessage.addData(XN_FILEOP, FileOpNames[FO_Accept]);
	xmlMessage.addData(XN_FILEID, fileId);

	QString id = userId;
	pMessaging->sendMessage(MT_File, &id, &xmlMessage);
}

void MessengerBridge::declineFile(const QString& userId, const QString& fileId) {
	if(userId.isEmpty() || fileId.isEmpty())
		return;

	XmlMessage xmlMessage;
	xmlMessage.addData(XN_MODE, FileModeNames[FM_Receive]);
	xmlMessage.addData(XN_FILETYPE, FileTypeNames[FT_Normal]);
	xmlMessage.addData(XN_FILEOP, FileOpNames[FO_Decline]);
	xmlMessage.addData(XN_FILEID, fileId);

	QString id = userId;
	pMessaging->sendMessage(MT_File, &id, &xmlMessage);

	//	Unlike accept/cancel, Core does not echo a decline back to the
	//	side that declined (see lmcMessaging::updateFileTransfer's
	//	FO_Decline branch - it just drops the transfer from its list, no
	//	emit) - so update the model directly here.
	ensureChatModel(userId)->updateFileState(fileId, QStringLiteral("declined"));
}

void MessengerBridge::cancelFile(const QString& userId, const QString& fileId) {
	if(userId.isEmpty() || fileId.isEmpty())
		return;

	//	Core needs to know which side of the transfer we are to route the
	//	cancel correctly - read it back from the entry we already have.
	bool outgoing = ensureChatModel(userId)->isOutgoingFile(fileId);
	FileMode mode = outgoing ? FM_Send : FM_Receive;

	XmlMessage xmlMessage;
	xmlMessage.addData(XN_MODE, FileModeNames[mode]);
	xmlMessage.addData(XN_FILETYPE, FileTypeNames[FT_Normal]);
	xmlMessage.addData(XN_FILEOP, FileOpNames[FO_Cancel]);
	xmlMessage.addData(XN_FILEID, fileId);

	QString id = userId;
	pMessaging->sendMessage(MT_File, &id, &xmlMessage);
}

QString MessengerBridge::createGroupChat(const QStringList& userIds) {
	QString threadId = Helper::getUuid();
	//	Matches lmcChatRoomWindow::init()'s unconditional addUser(
	//	pLocalUser) when groupMode is true: creating a room announces our
	//	own membership the same way joining one does (see
	//	messaging_messageReceived()'s GMO_Request handling) - harmless at
	//	creation time since nobody else has this threadId yet to react to it.
	createLocalRoom(threadId);

	XmlMessage xmlMessage;
	xmlMessage.addData(XN_THREAD, threadId);
	xmlMessage.addData(XN_GROUPMSGOP, GroupMsgOpNames[GMO_Request]);
	for(const QString& userId : userIds) {
		if(userId == localUserId())
			continue;
		QString id = userId;
		pMessaging->sendMessage(MT_GroupMessage, &id, &xmlMessage);
	}

	return threadId;
}

void MessengerBridge::sendGroupMessage(const QString& threadId, const QString& text) {
	if(!roomPeerIds.contains(threadId) || text.isEmpty())
		return;

	//	Field shape matches lmcChatRoomWindow::sendMessage() in
	//	Windows/lmc/src/chatroomwindow.cpp, minus the font/color cosmetic
	//	fields, same as sendMessage() above.
	XmlMessage xmlMessage;
	xmlMessage.addHeader(XN_TIME, QString::number(QDateTime::currentDateTime().toMSecsSinceEpoch()));
	xmlMessage.addData(XN_THREAD, threadId);
	xmlMessage.addData(XN_GROUPMSGOP, GroupMsgOpNames[GMO_Message]);
	xmlMessage.addData(XN_MESSAGE, text);

	//	Unlike GMO_Request/Join/Leave, actual message content is targeted
	//	per-participant, not broadcast to everyone.
	const QStringList peerIds = roomPeerIds.value(threadId);
	for(const QString& peerId : peerIds) {
		if(peerId == localUserId())
			continue;
		QString id = peerId;
		pMessaging->sendMessage(MT_GroupMessage, &id, &xmlMessage);
	}

	roomMessageModels[threadId]->appendMessage(localUserName(), text, QDateTime::currentDateTime(), true);
}

void MessengerBridge::leaveGroupChat(const QString& threadId) {
	if(!roomPeerIds.contains(threadId))
		return;

	XmlMessage xmlMessage;
	xmlMessage.addData(XN_THREAD, threadId);
	xmlMessage.addData(XN_GROUPMSGOP, GroupMsgOpNames[GMO_Leave]);
	//	NULL recipient: fans out to every online user, same as GMO_Join -
	//	only the other room participants (who have this threadId open)
	//	will act on it.
	pMessaging->sendMessage(MT_GroupMessage, nullptr, &xmlMessage);

	roomMessageModels.take(threadId)->deleteLater();
	roomParticipantModels.take(threadId)->deleteLater();
	roomPeerIds.remove(threadId);
	pRoomListModel->removeRoom(threadId);
}

ChatModel* MessengerBridge::roomMessages(const QString& threadId) {
	if(!roomPeerIds.contains(threadId))
		return nullptr;
	return roomMessageModels.value(threadId);
}

ContactModel* MessengerBridge::roomParticipants(const QString& threadId) {
	if(!roomPeerIds.contains(threadId))
		return nullptr;
	return roomParticipantModels.value(threadId);
}

QString MessengerBridge::roomTitle(const QString& threadId) const {
	if(!roomPeerIds.contains(threadId))
		return QString();

	QStringList names;
	const QStringList peerIds = roomPeerIds.value(threadId);
	for(const QString& peerId : peerIds) {
		if(peerId == localUserId())
			continue;
		User* pUser = userById(peerId);
		names.append(pUser ? pUser->name : peerId);
	}
	return names.isEmpty() ? tr("Group Chat") : names.join(", ");
}

void MessengerBridge::createLocalRoom(const QString& threadId) {
	roomMessageModels.insert(threadId, new ChatModel(this));
	roomParticipantModels.insert(threadId, new ContactModel(this));
	roomPeerIds.insert(threadId, QStringList());

	addRoomParticipant(threadId, localUserId());

	XmlMessage xmlMessage;
	xmlMessage.addData(XN_THREAD, threadId);
	xmlMessage.addData(XN_GROUPMSGOP, GroupMsgOpNames[GMO_Join]);
	pMessaging->sendMessage(MT_GroupMessage, nullptr, &xmlMessage);
}

void MessengerBridge::addRoomParticipant(const QString& threadId, const QString& userId) {
	if(!roomPeerIds.contains(threadId))
		return;

	User* pUser = userById(userId);
	//	Matches lmcChatRoomWindow::addUser()'s version gate in
	//	Windows/lmc/src/chatroomwindow.cpp: versions <= 1.2.10 predate the
	//	room/public-chat feature and would not understand this traffic.
	if(pUser && userId != localUserId() && Helper::compareVersions(pUser->version, "1.2.10") <= 0)
		return;

	QStringList& ids = roomPeerIds[threadId];
	if(ids.contains(userId))
		return;
	ids.append(userId);

	refreshRoomParticipantModel(threadId);
}

void MessengerBridge::removeRoomParticipant(const QString& threadId, const QString& userId) {
	if(!roomPeerIds.contains(threadId))
		return;

	roomPeerIds[threadId].removeAll(userId);
	refreshRoomParticipantModel(threadId);
}

void MessengerBridge::refreshRoomParticipantModel(const QString& threadId) {
	QList<User> users;
	const QStringList ids = roomPeerIds.value(threadId);
	for(const QString& id : ids) {
		User* pUser = userById(id);
		if(pUser)
			users.append(*pUser);
	}
	roomParticipantModels[threadId]->setUsers(users);
	pRoomListModel->addOrUpdateRoom(threadId, roomTitle(threadId), ids.count());
	emit roomUpdated(threadId);
}

User* MessengerBridge::userById(const QString& userId) const {
	if(userId == localUserId())
		return pMessaging->localUser;
	QString id = userId;
	return pMessaging->getUser(&id);
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

	case MT_Message: {
		if(!lpszUserId || !pMessage)
			break;
		User* pUser = pMessaging->getUser(lpszUserId);
		QString senderName = pUser ? pUser->name : *lpszUserId;
		QString text = pMessage->data(XN_MESSAGE);
		ensureChatModel(*lpszUserId)->appendMessage(senderName, text, QDateTime::currentDateTime(), false);
		emit incomingMessage(*lpszUserId, senderName, text);
		break;
	}

	//	Group chat room protocol - see the class comment in
	//	messengerbridge.h for the full picture (traced from
	//	Windows/lmc/src/chatroomwindow.cpp, not guessed).
	case MT_GroupMessage: {
		if(!lpszUserId || !pMessage)
			break;
		QString threadId = pMessage->data(XN_THREAD);
		if(threadId.isEmpty())
			break;
		int op = Helper::indexOf(GroupMsgOpNames, GMO_Max, pMessage->data(XN_GROUPMSGOP));
		bool roomExists = roomPeerIds.contains(threadId);

		if(op == GMO_Request) {
			//	Someone is inviting us into thread threadId. If we do not
			//	already know it, create it - which (matching
			//	lmcChatRoomWindow::init()'s unconditional addUser(pLocalUser)
			//	when groupMode is true) announces our own join to every
			//	online user via a NULL-recipient broadcast; only the other
			//	invitees/the inviter, who already have this threadId open,
			//	will act on it.
			if(!roomExists)
				createLocalRoom(threadId);
			addRoomParticipant(threadId, *lpszUserId);
		} else if(!roomExists) {
			//	Not a thread we know about - per the protocol, every
			//	uninvited bystander just ignores this (see the class
			//	comment), since GMO_Join/GMO_Message/GMO_Leave for a room
			//	we were never invited to reach us too (Join/Leave are sent
			//	to everyone, not just room members).
			break;
		} else {
			User* pUser = userById(*lpszUserId);
			QString senderName = pUser ? pUser->name : *lpszUserId;
			switch(op) {
			case GMO_Join:
				addRoomParticipant(threadId, *lpszUserId);
				roomMessageModels[threadId]->appendSystemMessage(
					tr("%1 joined").arg(senderName), QDateTime::currentDateTime());
				break;
			case GMO_Message:
				roomMessageModels[threadId]->appendMessage(senderName, pMessage->data(XN_MESSAGE),
					QDateTime::currentDateTime(), false);
				break;
			case GMO_Leave:
				removeRoomParticipant(threadId, *lpszUserId);
				roomMessageModels[threadId]->appendSystemMessage(
					tr("%1 left").arg(senderName), QDateTime::currentDateTime());
				break;
			default:
				break;
			}
		}
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

	//	Single-file transfer only (MT_Folder/folder transfer is not wired
	//	up - see Android/README.md). Every state transition below - for
	//	transfers we initiated as well as ones a peer initiated - reaches
	//	us through this same signal (see the class comment in
	//	messengerbridge.h for why), so ChatModel is the single place file
	//	transfer state lives; there is no separate transfer list here.
	case MT_File: {
		if(!lpszUserId || !pMessage)
			break;
		QString fileId = pMessage->data(XN_FILEID);
		if(fileId.isEmpty())
			break;

		//	Core sets this to "send" only on the echo of a transfer *we*
		//	requested (see lmcMessaging::addFileTransfer's FM_Send branch
		//	in Core/src/filemessagingproc.cpp); an incoming request from a
		//	peer has already been flipped to "receive" by the time it
		//	reaches us (see lmcMessaging::processFile).
		bool outgoing = pMessage->data(XN_MODE) == FileModeNames[FM_Send];
		int fileOp = Helper::indexOf(FileOpNames, FO_Max, pMessage->data(XN_FILEOP));
		ChatModel* pChat = ensureChatModel(*lpszUserId);

		switch(fileOp) {
		case FO_Request: {
			QString fileName = pMessage->data(XN_FILENAME);
			qint64 fileSize = pMessage->data(XN_FILESIZE).toLongLong();
			pChat->upsertFileEntry(fileId, fileName, fileSize, 0, outgoing, QStringLiteral("request"));
			if(!outgoing) {
				User* pUser = pMessaging->getUser(lpszUserId);
				emit incomingFileRequest(*lpszUserId, pUser ? pUser->name : *lpszUserId, fileId, fileName, fileSize);
			}
			break;
		}
		case FO_Accept:
			pChat->updateFileState(fileId, QStringLiteral("transferring"));
			break;
		case FO_Progress:
			pChat->updateFileProgress(fileId, pMessage->data(XN_FILESIZE).toLongLong());
			break;
		case FO_Complete:
			pChat->updateFileState(fileId, QStringLiteral("complete"));
			break;
		case FO_Decline:
			pChat->updateFileState(fileId, QStringLiteral("declined"));
			break;
		case FO_Cancel:
		case FO_Abort:
			pChat->updateFileState(fileId, QStringLiteral("cancelled"));
			break;
		case FO_Error:
			pChat->updateFileState(fileId, QStringLiteral("error"));
			break;
		default:
			break;
		}
		break;
	}

	default:
		//	Everything else (folder transfer, chat state, queries, ...) is
		//	not wired up in this first pass - see Android/README.md.
		break;
	}
}
