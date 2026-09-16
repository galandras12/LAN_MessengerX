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
# ultimately links libCore/lmccore (see Windows/lmc/src/lmc.pro) - a
# static library archive step does not itself link against libcrypto.
INCLUDEPATH += $$PWD/../openssl/include
DEPENDPATH += $$PWD/../openssl/include

# crypto.cpp intentionally keeps calling the classic RSA_*/PEM_*RSAPublicKey
# API (RSA_new/RSA_free/RSA_size/RSA_generate_key/RSA_public_encrypt/
# RSA_private_decrypt/PEM_write_bio_RSAPublicKey/PEM_read_bio_RSAPublicKey)
# rather than the newer EVP_PKEY-based API - see the comment above and
# Core/README.md: the wire format must stay byte-compatible with the
# original LAN Messenger protocol, and a switch to EVP_PKEY is a real
# behavioral rewrite this fork isn't taking on speculatively. OpenSSL 3.0
# marks that classic API deprecated (0.9.8 for RSA_generate_key, 3.0 for
# the rest) but still fully implements it. OPENSSL_API_COMPAT tells
# OpenSSL's headers which API version this code targets; declaring an
# older target than either deprecation point (0.9.8) suppresses the
# deprecation warnings for symbols deprecated at or after it, without
# hiding or disabling the functions themselves (that would be
# OPENSSL_NO_DEPRECATED, which is not set here).
DEFINES += OPENSSL_API_COMPAT=0x00800000L
