# Test-only: wraps the ORIGINAL (Qt) LAN Messenger core in a tiny headless command line program so the new
# Kotlin/C# clients can be tested against the real thing. Not part of any product.
# The original sources live in legacy-qt/Core (or, once that folder is deleted, in this repository's history
# before the rewrite).  Build with:  qmake6 harness.pro && make   (needs qt6-base-dev and libssl-dev)
QT += core network xml
QT -= gui
CONFIG += console c++17
CONFIG -= app_bundle
TEMPLATE = app
TARGET = legacyd
LEGACY = $$PWD/../../../legacy-qt/Core/src
INCLUDEPATH += $$LEGACY
SOURCES += main.cpp \
  $$LEGACY/crypto.cpp $$LEGACY/datagram.cpp $$LEGACY/filemessagingproc.cpp $$LEGACY/history.cpp \
  $$LEGACY/message.cpp $$LEGACY/messaging.cpp $$LEGACY/messagingproc.cpp $$LEGACY/netstreamer.cpp \
  $$LEGACY/network.cpp $$LEGACY/settings.cpp $$LEGACY/shared.cpp $$LEGACY/lmcstrings.cpp \
  $$LEGACY/tcpnetwork.cpp $$LEGACY/trace.cpp $$LEGACY/udpnetwork.cpp $$LEGACY/webnetwork.cpp $$LEGACY/xmlmessage.cpp
HEADERS += $$LEGACY/*.h harness.h
LIBS += -lssl -lcrypto
