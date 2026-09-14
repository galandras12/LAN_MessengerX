#-------------------------------------------------
#
# LAN Messenger project file (Windows client)
#
# NOTE (modernization): the platform-independent networking / protocol /
# crypto / message-history layer lives in ../../../Core and builds as its
# own static library (lmccore, see Core/Core.pro) so the planned Android/Qt
# Quick client can link the exact same code. This project links that
# library rather than compiling Core's sources itself - do not duplicate
# those files here. Build Core first (see Windows/README.md).
#-------------------------------------------------

QT += core gui network xml widgets

unix: QT += multimedia
macx: QT += multimedia

win32: TARGET = lmc
unix: TARGET = lan-messenger
macx: TARGET  = "LAN-Messenger"
TEMPLATE = app
CONFIG += c++17

RESOURCES = resource.qrc

CORE_ROOT = $$PWD/../../../Core

INCLUDEPATH += $$CORE_ROOT/src
DEPENDPATH += $$CORE_ROOT/src
LIBS += -L$$CORE_ROOT/lib -llmccore

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
        sv_SE.ts \
        hu_HU.ts \
        ja_JP.ts \
        pl_PL.ts \
        sk_SK.ts

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

# OpenSSL 3.x: only needed at final link time here (lmccore's crypto.cpp is
# what actually calls into it - see Core/Core.pro). Modern Windows builds/
# packages name the import library "libcrypto.lib" (older 1.0.2-era
# packages used "libeay32.lib" - adjust to match whatever OpenSSL 3.x
# distribution you install if this differs). Expected at repo-root
# /openssl, i.e. a sibling of /Core and /Windows.
win32: LIBS += -L$$PWD/../../../openssl/lib/ -llibcrypto
unix:!symbian: LIBS += -L$$PWD/../../../openssl/lib/ -lcrypto
