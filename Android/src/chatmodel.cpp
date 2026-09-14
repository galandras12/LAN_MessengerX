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
	case IsFileRole: return entry.isFile;
	case FileIdRole: return entry.fileId;
	case FileNameRole: return entry.fileName;
	case FileSizeRole: return entry.fileSize;
	case PositionRole: return entry.position;
	case ProgressRole: return entry.fileSize > 0 ? double(entry.position) / double(entry.fileSize) : 0.0;
	case StateRole: return entry.state;
	default: return QVariant();
	}
}

QHash<int, QByteArray> ChatModel::roleNames() const {
	QHash<int, QByteArray> roles;
	roles[SenderNameRole] = "senderName";
	roles[TextRole] = "text";
	roles[TimeRole] = "timestamp";
	roles[OutgoingRole] = "outgoing";
	roles[IsFileRole] = "isFile";
	roles[FileIdRole] = "fileId";
	roles[FileNameRole] = "fileName";
	roles[FileSizeRole] = "fileSize";
	roles[PositionRole] = "position";
	roles[ProgressRole] = "progress";
	roles[StateRole] = "state";
	return roles;
}

void ChatModel::appendMessage(const QString& senderName, const QString& text, const QDateTime& timestamp, bool outgoing) {
	ChatEntry entry;
	entry.isFile = false;
	entry.outgoing = outgoing;
	entry.timestamp = timestamp;
	entry.senderName = senderName;
	entry.text = text;
	entry.fileSize = 0;
	entry.position = 0;

	beginInsertRows(QModelIndex(), entries.count(), entries.count());
	entries.append(entry);
	endInsertRows();
}

int ChatModel::indexOfFile(const QString& fileId) const {
	for(int i = 0; i < entries.count(); i++) {
		if(entries.at(i).isFile && entries.at(i).fileId == fileId)
			return i;
	}
	return -1;
}

void ChatModel::upsertFileEntry(const QString& fileId, const QString& fileName, qint64 fileSize,
		qint64 position, bool outgoing, const QString& state) {
	int row = indexOfFile(fileId);
	if(row >= 0) {
		entries[row].fileName = fileName;
		entries[row].fileSize = fileSize;
		entries[row].position = position;
		entries[row].state = state;
		QModelIndex idx = index(row);
		emit dataChanged(idx, idx);
		return;
	}

	ChatEntry entry;
	entry.isFile = true;
	entry.outgoing = outgoing;
	entry.timestamp = QDateTime::currentDateTime();
	entry.fileId = fileId;
	entry.fileName = fileName;
	entry.fileSize = fileSize;
	entry.position = position;
	entry.state = state;

	beginInsertRows(QModelIndex(), entries.count(), entries.count());
	entries.append(entry);
	endInsertRows();
}

void ChatModel::updateFileState(const QString& fileId, const QString& state) {
	int row = indexOfFile(fileId);
	if(row < 0)
		return;
	entries[row].state = state;
	QModelIndex idx = index(row);
	emit dataChanged(idx, idx);
}

void ChatModel::updateFileProgress(const QString& fileId, qint64 position) {
	int row = indexOfFile(fileId);
	if(row < 0)
		return;
	entries[row].position = position;
	entries[row].state = QStringLiteral("transferring");
	QModelIndex idx = index(row);
	emit dataChanged(idx, idx);
}

bool ChatModel::isOutgoingFile(const QString& fileId) const {
	int row = indexOfFile(fileId);
	return row >= 0 ? entries.at(row).outgoing : false;
}
