#-------------------------------------------------
#
# LAN Messenger project file (Windows client)
#
# NOTE (modernization): the platform-independent networking / protocol /
# crypto / message-history layer now lives in ../../../Core/src and is
# compiled directly into this target (not yet a separate library - that
# split is planned for a later phase once the Android/Qt Quick client
# needs to link the same sources). Do not duplicate those files here.
#-------------------------------------------------

QT += core gui network xml widgets

unix: QT += multimedia
macx: QT += multimedia

win32: TARGET = lmc
unix: TARGET = lan-messenger
macx: TARGET  = "LAN-Messenger"
TEMPLATE = app

RESOURCES = resource.qrc

CORE_SRC = $$PWD/../../../Core/src

INCLUDEPATH += $$CORE_SRC
DEPENDPATH += $$CORE_SRC

# platform-independent core (networking, protocol, crypto, settings, history)
SOURCES += \
    $$CORE_SRC/udpnetwork.cpp \
    $$CORE_SRC/tcpnetwork.cpp \
    $$CORE_SRC/strings.cpp \
    $$CORE_SRC/shared.cpp \
    $$CORE_SRC/settings.cpp \
    $$CORE_SRC/network.cpp \
    $$CORE_SRC/netstreamer.cpp \
    $$CORE_SRC/messagingproc.cpp \
    $$CORE_SRC/messaging.cpp \
    $$CORE_SRC/message.cpp \
    $$CORE_SRC/history.cpp \
    $$CORE_SRC/datagram.cpp \
    $$CORE_SRC/crypto.cpp \
    $$CORE_SRC/xmlmessage.cpp \
    $$CORE_SRC/webnetwork.cpp \
    $$CORE_SRC/trace.cpp \
    $$CORE_SRC/filemessagingproc.cpp

HEADERS += \
    $$CORE_SRC/udpnetwork.h \
    $$CORE_SRC/tcpnetwork.h \
    $$CORE_SRC/strings.h \
    $$CORE_SRC/shared.h \
    $$CORE_SRC/settings.h \
    $$CORE_SRC/network.h \
    $$CORE_SRC/netstreamer.h \
    $$CORE_SRC/messaging.h \
    $$CORE_SRC/message.h \
    $$CORE_SRC/history.h \
    $$CORE_SRC/stdlocation.h \
    $$CORE_SRC/definitions.h \
    $$CORE_SRC/datagram.h \
    $$CORE_SRC/crypto.h \
    $$CORE_SRC/xmlmessage.h \
    $$CORE_SRC/chatdefinitions.h \
    $$CORE_SRC/webnetwork.h \
    $$CORE_SRC/trace.h

# Windows/desktop UI layer
SOURCES += \
    usertreewidget.cpp \
    transferwindow.cpp \
    transferlistview.cpp \
    soundplayer.cpp \
    settingsdialog.cpp \
    mainwindow.cpp \
    main.cpp \
    lmc.cpp \
    imagepickeraction.cpp \
    imagepicker.cpp \
    historywindow.cpp \
    helpwindow.cpp \
    filemodelview.cpp \
    chatwindow.cpp \
    broadcastwindow.cpp \
    aboutdialog.cpp \
    chathelper.cpp \
    theme.cpp \
    messagelog.cpp \
    updatewindow.cpp \
    userinfowindow.cpp \
    chatroomwindow.cpp \
    userselectdialog.cpp \
    subcontrols.cpp \
    qmessagebrowser.cpp

HEADERS  += \
    usertreewidget.h \
    uidefinitions.h \
    transferwindow.h \
    transferlistview.h \
    soundplayer.h \
    settingsdialog.h \
    resource.h \
    mainwindow.h \
    lmc.h \
    imagepickeraction.h \
    imagepicker.h \
    historywindow.h \
    historytreewidget.h \
    helpwindow.h \
    filemodelview.h \
    chatwindow.h \
    broadcastwindow.h \
    aboutdialog.h \
    chathelper.h \
    theme.h \
    messagelog.h \
    updatewindow.h \
    userinfowindow.h \
    chatroomwindow.h \
    userselectdialog.h \
    subcontrols.h \
    qmessagebrowser.h

FORMS += \
    transferwindow.ui \
    settingsdialog.ui \
    mainwindow.ui \
    historywindow.ui \
    helpwindow.ui \
    chatwindow.ui \
    broadcastwindow.ui \
    aboutdialog.ui \
    updatewindow.ui \
    userinfowindow.ui \
    chatroomwindow.ui \
    userselectdialog.ui

TRANSLATIONS += \
	en_US.ts \
	ml_IN.ts \
	fr_FR.ts \
	de_DE.ts \
	tr_TR.ts \
	es_ES.ts \
	ko_KR.ts \
	bg_BG.ts \
	ro_RO.ts \
	ar_SA.ts \
	sl_SI.ts \
        pt_BR.ts \
        ru_RU.ts \
        it_IT.ts \
        sv_SE.ts

win32: RC_FILE = lmcwin32.rc
macx: ICON = lmc.icns

win32-msvc* {
    QMAKE_LFLAGS_RELEASE += /MAP
    QMAKE_CFLAGS_RELEASE += /Zi
    QMAKE_CFLAGS_RELEASE += /FAcs
    QMAKE_CXXFLAGS_RELEASE += /Zi
    QMAKE_CXXFLAGS_RELEASE += /FAcs
    QMAKE_LFLAGS_RELEASE += /debug /opt:ref
}

win32: {
    CONFIG -= debug_and_release debug_and_release_target
    LMCAPP_PATH = $$replace(OUT_PWD, lmc, lmcapp)
    LIBS += -L$$LMCAPP_PATH -llmcapp
}
unix:!symbian: {
    CONFIG(debug, debug|release) {
        DESTDIR = ../debug
    } else {
        DESTDIR = ../release
    }
    LIBS += -L$$PWD/../../lmcapp/lib/ -llmcapp
}

INCLUDEPATH += $$PWD/../../lmcapp/include
DEPENDPATH += $$PWD/../../lmcapp/include

win32-msvc*: LIBS += advapi32.lib # for GetUserNameW(...) in Helper::getLogonName(..)

# OpenSSL 3.x: modern Windows builds/packages name the import library
# "libcrypto.lib" (older 1.0.2-era packages used "libeay32.lib" - adjust
# to match whatever OpenSSL 3.x distribution you install if this differs).
win32: LIBS += -L$$PWD/../../openssl/lib/ -llibcrypto
unix:!symbian: LIBS += -L$$PWD/../../openssl/lib/ -lcrypto

INCLUDEPATH += $$PWD/../../openssl/include
DEPENDPATH += $$PWD/../../openssl/include
