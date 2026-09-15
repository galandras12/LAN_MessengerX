#-------------------------------------------------
#
# LAN Messenger Application project file
#
#-------------------------------------------------

QT       += core network widgets

TARGET = lmcapp
TEMPLATE = lib
# Without this, qmake's default for TEMPLATE = lib is a *shared* library
# (a DLL on Windows) - but lmc.pro links lmcapp with a plain -llmcapp and
# nothing anywhere (build_windows.bat, BUILD.md's windeployqt step) ever
# deploys a companion lmcapp DLL next to lmc.exe. Without this flag,
# lmc.exe would build "successfully" and then fail to launch at all -
# even on the machine that built it - with a missing-DLL error. Matches
# Core.pro, which already builds lmccore as CONFIG += staticlib for the
# same reason.
CONFIG += staticlib
VERSION = 2.0.0

DEFINES += LMCAPP_LIBRARY

SOURCES += \
    qtsinglecoreapplication.cpp \
    qtsingleapplication.cpp \
    qtlockedfile_win.cpp \
    qtlockedfile_unix.cpp \
    qtlockedfile.cpp \
    qtlocalpeer.cpp \
    application.cpp

HEADERS += \
    qtsinglecoreapplication.h \
    qtsingleapplication.h \
    qtlockedfile.h \
    qtlocalpeer.h \
    application.h \
    ../../../Core/src/settings.h

win32: RC_FILE = lmcappwin32.rc

# NOTE: Symbian and Maemo5 target blocks (both discontinued platforms this
# project no longer targets) were removed here during modernization.

unix {
    target.path = /usr/local/lib
    INSTALLS += target
}

win32: {
    CONFIG -= debug_and_release debug_and_release_target
    CONFIG += skip_target_version_ext
}
unix {
    CONFIG(debug, debug|release) {
            DESTDIR = ../lib
            mac: TARGET = $$join(TARGET,,,_debug)
    } else {
            DESTDIR = ../lib
    }
}
