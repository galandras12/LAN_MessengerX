/****************************************************************************
**
** This file is part of LAN Messenger.
**
** Part of the Android client: a per-conversation list model of chat
** messages, appended to as MessengerBridge receives/sends MT_Message /
** MT_GroupMessage traffic from lmcMessaging (/Core). Kept intentionally
** simple (in-memory only, no persistence/history yet) for this first
** pass - see Android/README.md.
**
****************************************************************************/

#ifndef CHATMODEL_H
#define CHATMODEL_H

#include <QAbstractListModel>
#include <QDateTime>
#include <QVector>

struct ChatEntry {
	QString senderName;
	QString text;
	QDateTime timestamp;
	bool outgoing;
};

class ChatModel : public QAbstractListModel {
	Q_OBJECT

public:
	enum Roles {
		SenderNameRole = Qt::UserRole + 1,
		TextRole,
		TimeRole,
		OutgoingRole
	};

	explicit ChatModel(QObject* parent = nullptr);

	int rowCount(const QModelIndex& parent = QModelIndex()) const override;
	QVariant data(const QModelIndex& index, int role) const override;
	QHash<int, QByteArray> roleNames() const override;

	void appendMessage(const QString& senderName, const QString& text, const QDateTime& timestamp, bool outgoing);

private:
	QVector<ChatEntry> entries;
};

#endif // CHATMODEL_H
