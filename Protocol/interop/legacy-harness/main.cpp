#include <QCoreApplication>
#include "harness.h"
#include "settings.h"
#include "trace.h"
int main(int argc, char** argv) {
  QCoreApplication app(argc, argv);
  app.setApplicationName("legacyd");
  lmcMessaging m;
  XmlMessage init;
  init.addData(XN_TRACEMODE, "true"); init.addData(XN_LOGFILE, qgetenv("LMC_LOG"));
  lmcTrace::init(&init);
  m.init(&init);
  Harness h(&m);
  m.start();
  printf("READY id=%s ip=%s\n", m.localUser ? m.localUser->id.toUtf8().constData() : "?", m.localUser ? m.localUser->address.toUtf8().constData() : "?");
  fflush(stdout);
  return app.exec();
}
