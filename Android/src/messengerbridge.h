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
** File transfer (single files only, not folders yet) is wired the same
** way: sendFile()/acceptFile()/declineFile()/cancelFile() just send the
** appropriate MT_File XmlMessage through lmcMessaging::sendMessage(), the
** same as sendMessage() does for chat text. Core's own state machine
** (Core/src/filemessagingproc.cpp) echoes every request/accept/progress/
** complete/error transition - including ones *we* initiated - back
** through messageReceived(), so ChatModel's file entries are driven
** entirely from there rather than being updated optimistically when we
** call these methods (the one exception is decline, which Core does not
** echo back to the decliner - see messaging_messageReceived()).
**
** Group chat rooms mirror Windows/lmc/src/chatroomwindow.cpp's protocol
** exactly (traced from there, not guessed): a room is a threadId (UUID)
** plus MT_GroupMessage traffic tagged with XN_THREAD + XN_GROUPMSGOP
** (GMO_Request/Join/Message/Leave). GMO_Request is sent point-to-point
** to each invitee; GMO_Join and GMO_Leave are sent with a NULL recipient
** (lmcMessaging::sendMessage()'s MT_GroupMessage/no-userId branch fans
** those to every online user), and every recipient - invited or not -
** just ignores any thread it doesn't already know about, which is how
** membership converges without any server tracking room rosters. Only
** GMO_Message (the actual chat text) is targeted per-participant. Not
** ported: Windows' "Public Chat" (an always-on room auto-joining every
** connected user, MT_PublicMessage) and adding participants to an
** already-created room - see /Android/README.md.
**
** Profile settings (display name/status/note) mirror the three separate,
** independent update paths Windows uses - there is no single "settings
** changed" message on the wire, each field has its own:
** - name: setLocalName() only writes IDS_USERNAME and calls
**   lmcMessaging::settingsChanged() (Core/src/messaging.cpp), which
**   already detects the change itself (comparing against getUserName())
**   and broadcasts MT_UserName - the same thing
**   Windows/lmc/src/settingsdialog.cpp relies on, so no need to duplicate
**   that compare-and-broadcast logic here.
** - status/note: setLocalStatus()/setLocalNote() mutate
**   lmcMessaging::localUser directly and broadcast MT_Status/MT_Note
**   themselves, matching lmcMainWindow::statusAction_triggered()/
**   txtNote_lostFocus() in Windows/lmc/src/mainwindow.cpp exactly (right
**   down to reusing NULL as the recipient - Core's MT_Status/MT_Note
**   handling in lmcMessaging::sendMessage() already ignores the passed
**   userId and always fans out to every online user regardless).
** Not ported: avatar editing (a file picker, on top of the sendFile()
** content:// URI caveat above - see Android/README.md).
**
** Scope note: folder transfer is not wired up yet - see
** /Android/README.md, including its unresolved-storage-access caveat for
** sendFile() on Android.
**
****************************************************************************/

#ifndef MESSENGERBRIDGE_H
#define MESSENGERBRIDGE_H

#include <QObject>
#include <QMap>
#include <QUrl>
#include "messaging.h"
#include "strings.h"
#include "contactmodel.h"
#include "chatmodel.h"
#include "roomlistmodel.h"

class MessengerBridge : public QObject {
	Q_OBJECT
	Q_PROPERTY(QString localUserId READ localUserId NOTIFY startedChanged)
	Q_PROPERTY(QString localUserName READ localUserName NOTIFY localProfileChanged)
	Q_PROPERTY(QString localStatus READ localStatus NOTIFY localProfileChanged)
	Q_PROPERTY(QString localNote READ localNote NOTIFY localProfileChanged)
	Q_PROPERTY(bool connected READ isConnected NOTIFY connectedChanged)
	Q_PROPERTY(ContactModel* contacts READ contacts CONSTANT)
	Q_PROPERTY(RoomListModel* rooms READ rooms CONSTANT)

public:
	explicit MessengerBridge(QObject* parent = nullptr);
	~MessengerBridge(void);

	QString localUserId(void) const;
	QString localUserName(void) const;
	QString localStatus(void) const;
	QString localNote(void) const;
	bool isConnected(void) const;
	ContactModel* contacts(void) const { return pContactModel; }
	RoomListModel* rooms(void) const { return pRoomListModel; }

	//	Starts the network engine. Called once from main.cpp after the QML
	//	engine is set up, not from the constructor, so QML bindings exist
	//	before any signal can fire.
	Q_INVOKABLE void start(void);

	Q_INVOKABLE void sendMessage(const QString& userId, const QString& text);
	Q_INVOKABLE void sendBroadcast(const QString& text);

	//	Returns (creating if needed) the conversation model for a given
	//	peer, for a QML chat page to bind its ListView model to.
	Q_INVOKABLE ChatModel* chatModelFor(const QString& userId);

	//	fileUrl is expected to be a local file:// URL (QML's FileDialog
	//	selectedFile). See the Android/README.md caveat: a content:// URL
	//	from Android's Storage Access Framework (e.g. a Downloads or
	//	cloud-storage picker) will not resolve to a readable local path.
	Q_INVOKABLE void sendFile(const QString& userId, const QUrl& fileUrl);
	Q_INVOKABLE void acceptFile(const QString& userId, const QString& fileId);
	Q_INVOKABLE void declineFile(const QString& userId, const QString& fileId);
	Q_INVOKABLE void cancelFile(const QString& userId, const QString& fileId);

	//	Creates a new group chat room with the given participants (not
	//	including the local user - added automatically), returning its
	//	threadId so QML can navigate to it immediately.
	Q_INVOKABLE QString createGroupChat(const QStringList& userIds);
	Q_INVOKABLE void sendGroupMessage(const QString& threadId, const QString& text);
	Q_INVOKABLE void leaveGroupChat(const QString& threadId);
	Q_INVOKABLE ChatModel* roomMessages(const QString& threadId);
	Q_INVOKABLE ContactModel* roomParticipants(const QString& threadId);
	//	Comma-joined participant names (excluding the local user) - a
	//	deliberate small deviation from Windows' getWindowTitle(), which
	//	includes "you" in its own title too; that reads oddly on your own
	//	screen and doesn't affect the wire protocol at all.
	Q_INVOKABLE QString roomTitle(const QString& threadId) const;

	//	Status codes ("chat"/"busy"/"dnd"/"brb"/"away"/"gone", the wire
	//	values) and their matching human-readable, translated labels
	//	("Available"/"Busy"/...) - same order as Core's statusCode[]/
	//	lmcStrings::statusDesc(), for a QML status picker to zip together.
	Q_INVOKABLE QStringList statusCodes(void) const;
	Q_INVOKABLE QStringList statusLabels(void) const;

	Q_INVOKABLE void setLocalName(const QString& name);
	Q_INVOKABLE void setLocalStatus(const QString& status);
	Q_INVOKABLE void setLocalNote(const QString& note);

signals:
	void startedChanged(void);
	void localProfileChanged(void);
	void connectedChanged(void);
	void incomingMessage(const QString& userId, const QString& senderName, const QString& text);
	void incomingFileRequest(const QString& userId, const QString& peerName, const QString& fileId, const QString& fileName, qint64 fileSize);
	//	Emitted whenever a room's participant list (and therefore its
	//	title) changes - QML pages bind to roomTitle()/roomParticipants()
	//	imperatively and refresh on this rather than relying on implicit
	//	property-binding reactivity through a non-NOTIFYing model call.
	void roomUpdated(const QString& threadId);

private slots:
	void messaging_messageReceived(MessageType type, QString* lpszUserId, XmlMessage* pMessage);
	void messaging_connectionStateChanged(void);

private:
	void refreshContacts(void);
	ChatModel* ensureChatModel(const QString& userId);

	void createLocalRoom(const QString& threadId);
	void addRoomParticipant(const QString& threadId, const QString& userId);
	void removeRoomParticipant(const QString& threadId, const QString& userId);
	void refreshRoomParticipantModel(const QString& threadId);
	User* userById(const QString& userId) const;

	lmcMessaging* pMessaging;
	ContactModel* pContactModel;
	RoomListModel* pRoomListModel;
	QMap<QString, ChatModel*> chatModels;

	QMap<QString, ChatModel*> roomMessageModels;
	QMap<QString, ContactModel*> roomParticipantModels;
	//	Participant ids per room, in join order - the fan-out list for
	//	GMO_Message sends and the source used to rebuild
	//	roomParticipantModels/the room title after each add/remove.
	QMap<QString, QStringList> roomPeerIds;
};

#endif // MESSENGERBRIDGE_H
