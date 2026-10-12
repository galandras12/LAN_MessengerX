#pragma once
#include <QObject>
#include <QCoreApplication>
#include <QSocketNotifier>
#include <cstdio>
#include "messaging.h"
class Harness : public QObject {
  Q_OBJECT
public:
  lmcMessaging* m;
  Harness(lmcMessaging* mm) : m(mm) {
    connect(m, SIGNAL(messageReceived(MessageType, QString*, XmlMessage*)), this, SLOT(onMsg(MessageType, QString*, XmlMessage*)));
    auto* n = new QSocketNotifier(0, QSocketNotifier::Read, this);
    connect(n, &QSocketNotifier::activated, this, &Harness::onStdin);
  }
public slots:
  void onMsg(MessageType type, QString* user, XmlMessage* msg) {
    QString body = msg ? msg->toString().simplified() : QString();
    printf("EVT %s %s %s\n", MessageTypeNames[type].toUtf8().constData(), user ? user->toUtf8().constData() : "-", body.toUtf8().constData());
    fflush(stdout);
  }
  void onStdin() {
    char buf[4096];
    if (!fgets(buf, sizeof buf, stdin)) { QCoreApplication::quit(); return; }
    QString line = QString::fromUtf8(buf).trimmed();
    QStringList p = line.split(' ');
    if (p.value(0) == "msg") {
      XmlMessage x; x.addHeader(XN_TIME, "0");
      x.addData(XN_MESSAGE, p.mid(2).join(' '));
      x.addData(XN_FONT, "Arial,9,-1,5,50,0,0,0,0,0"); x.addData(XN_COLOR, "#112233");
      QString id = p.value(1); m->sendMessage(MT_Message, &id, &x);
    } else if (p.value(0) == "file") {
      XmlMessage x; x.addData(XN_FILETYPE, "normal"); x.addData(XN_FILEOP, "request"); x.addData(XN_FILEPATH, p.value(2));
      QString id = p.value(1); m->sendMessage(MT_File, &id, &x);
    } else if (p.value(0) == "accept") {
      XmlMessage x; x.addData(XN_MODE, "receive"); x.addData(XN_FILETYPE, "normal"); x.addData(XN_FILEOP, "accept"); x.addData(XN_FILEID, p.value(2)); x.addData(XN_FILESIZE, p.value(3));
      QString id = p.value(1); m->sendMessage(MT_File, &id, &x);
    } else if (p.value(0) == "status") {
      XmlMessage x; x.addData(XN_STATUS, p.value(1)); m->sendMessage(MT_Status, nullptr, &x);
    } else if (p.value(0) == "group") {
      XmlMessage x; x.addData(XN_THREAD, p.value(2)); x.addData(XN_GROUPMSGOP, p.value(3));
      if (p.value(3) == "message") { x.addData(XN_MESSAGE, p.mid(4).join(' ')); }
      QString id = p.value(1); m->sendMessage(MT_GroupMessage, &id, &x);
    } else if (p.value(0) == "query") {
      XmlMessage x; x.addData(XN_QUERYOP, "get"); QString id = p.value(1); m->sendMessage(MT_Query, &id, &x);
    } else if (p.value(0) == "avatar") {
      XmlMessage x; x.addData(XN_FILETYPE, "avatar"); x.addData(XN_FILEOP, "request"); x.addData(XN_FILEPATH, p.value(2));
      QString id = p.value(1); m->sendMessage(MT_Avatar, &id, &x);
    } else if (p.value(0) == "quit") { m->stop(); QCoreApplication::quit(); }
  }
};
