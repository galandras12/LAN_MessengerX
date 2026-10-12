/****************************************************************************
**
** This file is part of LAN Messenger.
**
** Lists saved conversation entries from /Core's History class (see
** Core/src/history.h) - the on-disk "messenger.db" file the Windows
** client's lmcHistoryWindow reads/writes too, so entries either client
** creates are visible in both (see MessengerBridge's history comment for
** the one deliberate format deviation: Windows saves one HTML blob per
** closed chat *session*, this client saves one small HTML blob per
** individual message instead - both are just opaque HTML payloads to
** whichever client reads them back).
**
****************************************************************************/

#ifndef HISTORYLISTMODEL_H
#define HISTORYLISTMODEL_H

#include <QAbstractListModel>
#include <QVector>
#include "history.h"

class HistoryListModel : public QAbstractListModel {
	Q_OBJECT

public:
	enum Roles {
		NameRole = Qt::UserRole + 1,
		DateRole,
		OffsetRole
	};

	explicit HistoryListModel(QObject* parent = nullptr);

	int rowCount(const QModelIndex& parent = QModelIndex()) const override;
	QVariant data(const QModelIndex& index, int role) const override;
	QHash<int, QByteArray> roleNames() const override;

	void setEntries(const QList<MsgInfo>& entries);

private:
	QVector<MsgInfo> entries;
};

#endif // HISTORYLISTMODEL_H
