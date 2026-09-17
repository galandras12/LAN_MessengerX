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
    src/chatmodel.cpp \
    src/roomlistmodel.cpp \
    src/historylistmodel.cpp \
    src/androidforegroundservice.cpp

HEADERS += \
    src/messengerbridge.h \
    src/contactmodel.h \
    src/chatmodel.h \
    src/roomlistmodel.h \
    src/historylistmodel.h \
    src/androidforegroundservice.h

RESOURCES += qml/qml.qrc

# OpenSSL headers: crypto.cpp itself is compiled in Core.pro (which has
# its own matching INCLUDEPATH), but messengerbridge.cpp/main.cpp here
# transitively include Core/src/crypto.h too (messengerbridge.h ->
# Core/messaging.h -> network.h -> udpnetwork.h -> crypto.h), and each
# .pro compiles its own sources against its own INCLUDEPATH regardless
# of what Core.pro already built into liblmccore.a - real build error
# without this: "crypto.h:27: fatal error: 'openssl/rand.h' file not
# found". See Core/Core.pro's own copy of this line for why one shared
# include/ tree covers every ABI.
android: INCLUDEPATH += $$PWD/../openssl-android/include

# OpenSSL libs: crypto.cpp (built into lmccore, see Core/Core.pro) needs
# libcrypto at final link time here too, same as the Windows client - but
# NOT the same prebuilt library. Android needs libcrypto cross-compiled
# for each target ABI (arm64-v8a / armeabi-v7a / x86_64 / x86); vendored
# here from the community "android_openssl" (KDAB) prebuilt package - see
# Android/README.md for where it came from and what's in it - this file
# only links the already-compiled lmccore static library against the
# final .so.
#
# -l:libcrypto_3.so (the "-l:<exact filename>" GNU ld/lld syntax, not the
# usual "-lname" -> "lib<name>.so" pattern) links the real file directly,
# rather than through a libcrypto.so -> libcrypto_3.so symlink - the
# vendored package ships that relationship as a real symlink upstream,
# but zip archives (and Windows checkouts without symlink support) can't
# reliably carry that, so this sidesteps depending on it.
android: LIBS += -L$$PWD/../openssl-android/$$ANDROID_TARGET_ARCH -l:libcrypto_3.so -l:libssl_3.so

# Linking against these isn't enough on its own - androiddeployqt only
# bundles .so files into the .apk that are explicitly listed here.
# Without this, the app links and builds fine but crashes at runtime on
# a real device/emulator (UnsatisfiedLinkError / dlopen failure), because
# libcrypto_3.so/libssl_3.so are simply missing from the installed .apk.
android: ANDROID_EXTRA_LIBS += \
    $$PWD/../openssl-android/$$ANDROID_TARGET_ARCH/libcrypto_3.so \
    $$PWD/../openssl-android/$$ANDROID_TARGET_ARCH/libssl_3.so

android {
    ANDROID_PACKAGE_SOURCE_DIR = $$PWD/android
}
