#include "historylistmodel.h"

HistoryListModel::HistoryListModel(QObject* parent) : QAbstractListModel(parent) {
}

int HistoryListModel::rowCount(const QModelIndex& parent) const {
	if(parent.isValid())
		return 0;
	return entries.count();
}

QVariant HistoryListModel::data(const QModelIndex& index, int role) const {
	if(!index.isValid() || index.row() < 0 || index.row() >= entries.count())
		return QVariant();

	const MsgInfo& entry = entries.at(index.row());
	switch(role) {
	case NameRole: return entry.name;
	case DateRole: return entry.date.toString(QStringLiteral("yyyy-MM-dd HH:mm:ss"));
	case OffsetRole: return QVariant::fromValue(entry.offset);
	default: return QVariant();
	}
}

QHash<int, QByteArray> HistoryListModel::roleNames() const {
	QHash<int, QByteArray> roles;
	roles[NameRole] = "name";
	roles[DateRole] = "date";
	roles[OffsetRole] = "offset";
	return roles;
}

void HistoryListModel::setEntries(const QList<MsgInfo>& newEntries) {
	beginResetModel();
	entries = QVector<MsgInfo>(newEntries.cbegin(), newEntries.cend());
	endResetModel();
}
