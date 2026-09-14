#include "roomlistmodel.h"

RoomListModel::RoomListModel(QObject* parent) : QAbstractListModel(parent) {
}

int RoomListModel::rowCount(const QModelIndex& parent) const {
	if(parent.isValid())
		return 0;
	return rooms.count();
}

QVariant RoomListModel::data(const QModelIndex& index, int role) const {
	if(!index.isValid() || index.row() < 0 || index.row() >= rooms.count())
		return QVariant();

	const RoomEntry& room = rooms.at(index.row());
	switch(role) {
	case ThreadIdRole: return room.threadId;
	case TitleRole: return room.title;
	case ParticipantCountRole: return room.participantCount;
	default: return QVariant();
	}
}

QHash<int, QByteArray> RoomListModel::roleNames() const {
	QHash<int, QByteArray> roles;
	roles[ThreadIdRole] = "threadId";
	roles[TitleRole] = "title";
	roles[ParticipantCountRole] = "participantCount";
	return roles;
}

int RoomListModel::indexOfRoom(const QString& threadId) const {
	for(int i = 0; i < rooms.count(); i++) {
		if(rooms.at(i).threadId == threadId)
			return i;
	}
	return -1;
}

void RoomListModel::addOrUpdateRoom(const QString& threadId, const QString& title, int participantCount) {
	int row = indexOfRoom(threadId);
	if(row >= 0) {
		rooms[row].title = title;
		rooms[row].participantCount = participantCount;
		QModelIndex idx = index(row);
		emit dataChanged(idx, idx);
		return;
	}

	RoomEntry room;
	room.threadId = threadId;
	room.title = title;
	room.participantCount = participantCount;

	beginInsertRows(QModelIndex(), rooms.count(), rooms.count());
	rooms.append(room);
	endInsertRows();
}

void RoomListModel::removeRoom(const QString& threadId) {
	int row = indexOfRoom(threadId);
	if(row < 0)
		return;

	beginRemoveRows(QModelIndex(), row, row);
	rooms.remove(row);
	endRemoveRows();
}
