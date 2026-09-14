#-------------------------------------------------
#
# LAN Messenger Android client project file
#
# A Qt Quick/QML front-end (MessengerBridge + ContactModel/ChatModel in
# src/) over the shared /Core (lmccore) business logic - the exact same
# networking/protocol/crypto code the Windows client links, so this
# client is wire-compatible with both the Windows client and the original
# LAN Messenger. See /Android/README.md for what is and is not done yet.
#
#-------------------------------------------------

QT += core gui network xml quick qml

TARGET = LANMessengerX
TEMPLATE = app
CONFIG += c++17

CORE_ROOT = $$PWD/../Core

INCLUDEPATH += $$CORE_ROOT/src
DEPENDPATH += $$CORE_ROOT/src
LIBS += -L$$CORE_ROOT/lib -llmccore

SOURCES += \
    src/main.cpp \
    src/messengerbridge.cpp \
    src/contactmodel.cpp \
    src/chatmodel.cpp

HEADERS += \
    src/messengerbridge.h \
    src/contactmodel.h \
    src/chatmodel.h

RESOURCES += qml/qml.qrc

# OpenSSL: crypto.cpp (built into lmccore, see Core/Core.pro) needs
# libcrypto at final link time here too, same as the Windows client - but
# NOT the same prebuilt library. Android needs libcrypto cross-compiled
# for each target ABI (arm64-v8a / armeabi-v7a / x86_64 / x86), e.g. via
# the community "android_openssl" prebuilt package, not the repo-root
# /openssl folder the Windows build uses (that one is a Windows-only
# import library). Not wired up yet - see Android/README.md. Once you have
# such a package, something like:
# android: INCLUDEPATH += $$PWD/../openssl-android/include
# android: LIBS += -L$$PWD/../openssl-android/lib/$$ANDROID_TARGET_ARCH -lcrypto_$$ANDROID_TARGET_ARCH

android {
    ANDROID_PACKAGE_SOURCE_DIR = $$PWD/android
}
