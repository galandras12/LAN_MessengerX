#-------------------------------------------------
#
# LAN Messenger Core project file
#
# Platform-independent networking/protocol/crypto/settings/history layer,
# built as a static library so both the Windows (Qt Widgets) client and the
# planned Android (Qt Quick/QML) client can link the same code instead of
# each carrying its own copy. Deliberately has no QtGui/QtWidgets
# dependency (see settings.h's QT_WIDGETS_LIB guard) so it stays usable
# from a Widgets-free, QML-based consumer.
#
#-------------------------------------------------

QT += core network xml
QT -= gui

TARGET = lmccore
TEMPLATE = lib
CONFIG += staticlib c++17
CONFIG -= app_bundle

DESTDIR = $$PWD/lib

SOURCES += \
    src/crypto.cpp \
    src/datagram.cpp \
    src/filemessagingproc.cpp \
    src/history.cpp \
    src/message.cpp \
    src/messaging.cpp \
    src/messagingproc.cpp \
    src/netstreamer.cpp \
    src/network.cpp \
    src/settings.cpp \
    src/shared.cpp \
    src/strings.cpp \
    src/tcpnetwork.cpp \
    src/trace.cpp \
    src/udpnetwork.cpp \
    src/webnetwork.cpp \
    src/xmlmessage.cpp

HEADERS += \
    src/chatdefinitions.h \
    src/crypto.h \
    src/datagram.h \
    src/definitions.h \
    src/history.h \
    src/message.h \
    src/messaging.h \
    src/netstreamer.h \
    src/network.h \
    src/settings.h \
    src/shared.h \
    src/stdlocation.h \
    src/strings.h \
    src/tcpnetwork.h \
    src/trace.h \
    src/udpnetwork.h \
    src/webnetwork.h \
    src/xmlmessage.h

# OpenSSL: crypto.cpp calls it directly (RSA/AES key exchange, kept
# bit-compatible with the original LAN Messenger wire protocol - see
# Core/README.md). Only the headers are needed to build this static
# library; the actual libcrypto is linked in by whichever executable
# ultimately links libCore/lmccore (Windows/lmc/src/lmc.pro or
# Android/Android.pro) - a static library archive step does not itself
# link against libcrypto. Windows and Android need different header
# trees here: opensslconf.h/configuration.h encode platform-specific
# values (word size, endianness) baked in per target at OpenSSL's own
# build/configure time, so a Windows x64 OpenSSL's headers are not
# interchangeable with an Android (per-ABI) OpenSSL's headers, even
# though almost all of the tree is identical portable C.
win32: INCLUDEPATH += $$PWD/../openssl/include
win32: DEPENDPATH += $$PWD/../openssl/include

# Android OpenSSL headers (KDAB android_openssl ssl_3 package - see
# Android/Android.pro for the matching per-ABI LIBS/ANDROID_EXTRA_LIBS
# and Android/README.md for where this package came from). All four
# target ABIs' prebuilt headers are identical (the actual per-ABI
# difference lives in the compiled .so files, linked in Android.pro,
# not in these headers), so one shared include/ tree covers all of
# them - no $$ANDROID_TARGET_ARCH branching needed here.
android: INCLUDEPATH += $$PWD/../openssl-android/include
android: DEPENDPATH += $$PWD/../openssl-android/include

# NOTE: crypto.cpp intentionally keeps calling the classic
# RSA_*/PEM_*RSAPublicKey API, which OpenSSL 3.0 marks deprecated but
# still fully implements - see crypto.cpp's own comment for why, and
# for how those deprecation warnings are silenced (a targeted #pragma
# in crypto.cpp itself, not a project-wide DEFINES here - an earlier
# attempt at DEFINES += OPENSSL_API_COMPAT=<value> guessed at OpenSSL's
# internal version-number encoding without a real OpenSSL install to
# test against, and turned out to be flatly invalid: OpenSSL's own
# headers rejected it with a hard #error ("impossible API compatibility
# level"), breaking the build worse than the warnings ever did).
