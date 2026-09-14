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
		//	Everything else (folder transfer, chat state, queries, group
		//	management, ...) is not wired up in this first pass - see
		//	Android/README.md.
		break;
	}
}
