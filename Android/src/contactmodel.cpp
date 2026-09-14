#include "contactmodel.h"

ContactModel::ContactModel(QObject* parent) : QAbstractListModel(parent) {
}

int ContactModel::rowCount(const QModelIndex& parent) const {
	if(parent.isValid())
		return 0;
	return userList.count();
}

QVariant ContactModel::data(const QModelIndex& index, int role) const {
	if(!index.isValid() || index.row() < 0 || index.row() >= userList.count())
		return QVariant();

	const User& user = userList.at(index.row());
	switch(role) {
	case UserIdRole: return user.id;
	case NameRole: return user.name;
	case StatusRole: return user.status;
	case NoteRole: return user.note;
	case GroupRole: return user.group;
	default: return QVariant();
	}
}

QHash<int, QByteArray> ContactModel::roleNames() const {
	QHash<int, QByteArray> roles;
	roles[UserIdRole] = "userId";
	roles[NameRole] = "name";
	roles[StatusRole] = "status";
	roles[NoteRole] = "note";
	roles[GroupRole] = "group";
	return roles;
}

void ContactModel::setUsers(const QList<User>& users) {
	beginResetModel();
	userList = users;
	endResetModel();
}
