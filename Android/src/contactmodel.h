/****************************************************************************
**
** This file is part of LAN Messenger.
**
** Part of the Android client: exposes lmcMessaging::userList (from
** /Core) to QML as a list model. Deliberately holds a plain copy of the
** User structs rather than pointers into lmcMessaging's own list, since
** that list is rebuilt/reassigned internally by Core and QML should not
** hold references into it across event loop iterations.
**
****************************************************************************/

#ifndef CONTACTMODEL_H
#define CONTACTMODEL_H

#include <QAbstractListModel>
#include <QList>
#include "shared.h"

class ContactModel : public QAbstractListModel {
	Q_OBJECT

public:
	enum Roles {
		UserIdRole = Qt::UserRole + 1,
		NameRole,
		StatusRole,
		NoteRole,
		GroupRole
	};

	explicit ContactModel(QObject* parent = nullptr);

	int rowCount(const QModelIndex& parent = QModelIndex()) const override;
	QVariant data(const QModelIndex& index, int role) const override;
	QHash<int, QByteArray> roleNames() const override;

	void setUsers(const QList<User>& users);

private:
	QList<User> userList;
};

#endif // CONTACTMODEL_H
