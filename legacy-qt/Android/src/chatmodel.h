/****************************************************************************
**
** This file is part of LAN Messenger.
**
** Part of the Android client: a per-conversation list model of chat
** messages, appended to as MessengerBridge receives/sends MT_Message /
** MT_GroupMessage traffic from lmcMessaging (/Core). Also carries file
** transfer entries (MT_File) inline in the same timeline, WhatsApp/
** Telegram-style, rather than a separate global transfer window - see
** MessengerBridge for how the request/accept/progress/complete/error
** state machine (mirroring Core/src/filemessagingproc.cpp) drives these.
** Also doubles as the message timeline for group chat rooms (see
** MessengerBridge's createGroupChat()/GMO_ handling), which additionally
** use appendSystemMessage() for join/leave notices.
** Purely an in-memory, per-open-conversation view - it does not itself
** read from or write to disk. Message history persistence is a separate,
** parallel write path (MessengerBridge::saveMessageToHistory(), via
** /Core's History class) triggered alongside appendMessage() calls, not
** something this model reads back from - reopening a conversation starts
** with an empty model regardless of what's in history. See
** Android/README.md.
**
****************************************************************************/

#ifndef CHATMODEL_H
#define CHATMODEL_H

#include <QAbstractListModel>
#include <QDateTime>
#include <QVector>

struct ChatEntry {
	bool isFile;
	bool isSystem;
	bool outgoing;
	QDateTime timestamp;

	//	text message / system notice (isSystem == true: text only, no sender)
	QString senderName;
	QString text;

	//	file transfer (isFile == true)
	QString fileId;
	QString fileName;
	qint64 fileSize;
	qint64 position;
	QString state;	//	"request" | "transferring" | "complete" | "declined" | "cancelled" | "error"
};

class ChatModel : public QAbstractListModel {
	Q_OBJECT

public:
	enum Roles {
		SenderNameRole = Qt::UserRole + 1,
		TextRole,
		TimeRole,
		OutgoingRole,
		IsFileRole,
		FileIdRole,
		FileNameRole,
		FileSizeRole,
		PositionRole,
		ProgressRole,
		StateRole,
		IsSystemRole
	};

	explicit ChatModel(QObject* parent = nullptr);

	int rowCount(const QModelIndex& parent = QModelIndex()) const override;
	QVariant data(const QModelIndex& index, int role) const override;
	QHash<int, QByteArray> roleNames() const override;

	void appendMessage(const QString& senderName, const QString& text, const QDateTime& timestamp, bool outgoing);

	//	Join/leave notices in a group chat room ("Alice joined", "Bob left") -
	//	rendered centered, without a bubble, in the QML delegate.
	void appendSystemMessage(const QString& text, const QDateTime& timestamp);

	//	Adds a new file transfer entry, or - if fileId is already known
	//	(the common case: Core echoes the same fileId back through every
	//	subsequent accept/progress/complete/error event for it) - updates
	//	the existing one in place instead of appending a duplicate.
	void upsertFileEntry(const QString& fileId, const QString& fileName, qint64 fileSize,
		qint64 position, bool outgoing, const QString& state);
	void updateFileState(const QString& fileId, const QString& state);
	void updateFileProgress(const QString& fileId, qint64 position);

	//	Used by MessengerBridge to fill in FileMode correctly when the user
	//	cancels a transfer (Core needs to know whether the local side is
	//	the sender or the receiver of that specific transfer).
	bool isOutgoingFile(const QString& fileId) const;

private:
	int indexOfFile(const QString& fileId) const;

	QVector<ChatEntry> entries;
};

#endif // CHATMODEL_H
