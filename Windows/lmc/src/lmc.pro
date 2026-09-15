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

#	Every TRANSLATIONS entry needs to be compiled to resources/lang/*.qm
#	BEFORE qmake runs, to match what resource.qrc's hand-written /lang
#	prefix expects (see BUILD.md 1.3/1.6 - build_windows.bat and the
#	manual command-line steps run an explicit lrelease loop for exactly
#	this). This used to instead be CONFIG += lrelease +
#	QM_FILES_OUTPUT_DIR, letting qmake auto-generate that step - but
#	its Makefile rule computed the .qm dependency path relative to
#	OUT_PWD using what turned out to be the wrong number of ".." hops
#	(confirmed by a real build: "No rule to make target
#	'../../resources/lang/hu_HU.qm'"), which resource.qrc's own paths
#	(resolved relative to the .qrc file itself, not OUT_PWD) never had
#	a problem with in the first place. Pre-generating the .qm files
#	before qmake runs sidesteps that broken rule entirely rather than
#	trying to fix its path math from outside a working Qt install.
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

# OpenSSL 3.x. lmccore's crypto.cpp is what actually calls into it (see
# Core/Core.pro), but this project needs the headers too, not just the
# lib at link time: main.cpp pulls in Core/src/crypto.h transitively
# (lmc.h -> messaging.h -> network.h/udpnetwork.h -> crypto.h), and
# crypto.h's own #include <openssl/rand.h> has to resolve when *this*
# project's .cpp files are compiled - linking lmccore later doesn't
# retroactively hand its INCLUDEPATH to this project. Modern Windows
# builds/packages name the import library "libcrypto.lib" (older
# 1.0.2-era packages used "libeay32.lib" - adjust to match whatever
# OpenSSL 3.x distribution you install if this differs). Expected at
# repo-root /openssl, i.e. a sibling of /Core and /Windows.
INCLUDEPATH += $$PWD/../../../openssl/include
DEPENDPATH += $$PWD/../../../openssl/include
#
# An OpenSSL source build installed via "nmake install" (the standard
# MSVC build route - see BUILD.md) lays lib/ out as
# lib/VC/x64/{MD,MDd,MT,MTd}/, one per CRT-linkage/config combination,
# not a flat lib/libcrypto.lib. Qt's own MSVC kits (and therefore this
# app) link the CRT dynamically, so MD (release) / MDd (debug) - not the
# static MT/MTd pair - are the ones that are ABI-compatible with a Qt
# build here. A flat lib/libcrypto.lib (e.g. from a prebuilt slproweb-
# style distribution instead of a from-source nmake install) still works
# unchanged via the win32-but-not-msvc fallback below.
win32-msvc* {
    CONFIG(debug, debug|release) {
        LIBS += -L$$PWD/../../../openssl/lib/VC/x64/MDd -llibcrypto
    } else {
        LIBS += -L$$PWD/../../../openssl/lib/VC/x64/MD -llibcrypto
    }
} else:win32 {
    LIBS += -L$$PWD/../../../openssl/lib/ -llibcrypto
}
unix:!symbian: LIBS += -L$$PWD/../../../openssl/lib/ -lcrypto
