#include "chatmodel.h"

ChatModel::ChatModel(QObject* parent) : QAbstractListModel(parent) {
}

int ChatModel::rowCount(const QModelIndex& parent) const {
	if(parent.isValid())
		return 0;
	return entries.count();
}

QVariant ChatModel::data(const QModelIndex& index, int role) const {
	if(!index.isValid() || index.row() < 0 || index.row() >= entries.count())
		return QVariant();

	const ChatEntry& entry = entries.at(index.row());
	switch(role) {
	case SenderNameRole: return entry.senderName;
	case TextRole: return entry.text;
	case TimeRole: return entry.timestamp;
	case OutgoingRole: return entry.outgoing;
	default: return QVariant();
	}
}

QHash<int, QByteArray> ChatModel::roleNames() const {
	QHash<int, QByteArray> roles;
	roles[SenderNameRole] = "senderName";
	roles[TextRole] = "text";
	roles[TimeRole] = "timestamp";
	roles[OutgoingRole] = "outgoing";
	return roles;
}

void ChatModel::appendMessage(const QString& senderName, const QString& text, const QDateTime& timestamp, bool outgoing) {
	beginInsertRows(QModelIndex(), entries.count(), entries.count());
	entries.append({senderName, text, timestamp, outgoing});
	endInsertRows();
}
