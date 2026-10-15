# Test-only: same headless wrapper as harness.pro, but built from the CURRENT upstream LAN Messenger (2.x, lmc/src).
# Point LMC_UPSTREAM at the lmc/src folder of the upstream checkout:  LMC_UPSTREAM=/path/lmc/src qmake6 harness-upstream.pro
QT += core network xml
QT += gui
CONFIG += console c++17
CONFIG -= app_bundle
TEMPLATE = app
TARGET = upstreamd
UP = $$(LMC_UPSTREAM)
INCLUDEPATH += $$UP $$UP/core $$UP/network $$UP/messaging
SOURCES += main.cpp \
  $$UP/core/crypto.cpp $$UP/core/settings.cpp $$UP/core/shared.cpp $$UP/core/lmcstrings.cpp $$UP/core/trace.cpp \
  $$UP/network/datagram.cpp $$UP/network/netstreamer.cpp $$UP/network/network.cpp $$UP/network/tcpnetwork.cpp \
  $$UP/network/udpnetwork.cpp $$UP/network/webnetwork.cpp \
  $$UP/messaging/filemessagingproc.cpp $$UP/messaging/history.cpp $$UP/messaging/message.cpp $$UP/messaging/messaging.cpp \
  $$UP/messaging/messagingproc.cpp $$UP/messaging/xmlmessage.cpp
HEADERS += harness.h $$UP/network/netstreamer.h $$UP/network/network.h $$UP/network/tcpnetwork.h $$UP/network/udpnetwork.h $$UP/network/webnetwork.h $$UP/messaging/messaging.h $$UP/messaging/history.h
LIBS += -lssl -lcrypto
DEFINES += IDA_VERSION=\\\"2.0.12\\\"
