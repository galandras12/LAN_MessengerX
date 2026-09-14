/****************************************************************************
**
** This file is part of LAN Messenger.
**
** Part of the Android client: lists the group chat rooms (threads) the
** local user is currently a participant of, for a "Group Chats" section
** in the QML UI. See MessengerBridge for how rooms are created/joined/
** left (mirroring Windows/lmc/src/chatroomwindow.cpp's GMO_Request/Join/
** Message/Leave protocol over MT_GroupMessage).
**
****************************************************************************/

#ifndef ROOMLISTMODEL_H
#define ROOMLISTMODEL_H

#include <QAbstractListModel>
#include <QVector>

struct RoomEntry {
	QString threadId;
	QString title;
	int participantCount;
};

class RoomListModel : public QAbstractListModel {
	Q_OBJECT

public:
	enum Roles {
		ThreadIdRole = Qt::UserRole + 1,
		TitleRole,
		ParticipantCountRole
	};

	explicit RoomListModel(QObject* parent = nullptr);

	int rowCount(const QModelIndex& parent = QModelIndex()) const override;
	QVariant data(const QModelIndex& index, int role) const override;
	QHash<int, QByteArray> roleNames() const override;

	void addOrUpdateRoom(const QString& threadId, const QString& title, int participantCount);
	void removeRoom(const QString& threadId);

private:
	int indexOfRoom(const QString& threadId) const;

	QVector<RoomEntry> rooms;
};

#endif // ROOMLISTMODEL_H
