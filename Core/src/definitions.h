/****************************************************************************
**
** This file is part of LAN Messenger.
** 
** Copyright (c) 2010 - 2012 Qualia Digital Solutions.
** 
** Contact:  qualiatech@gmail.com
** 
** LAN Messenger is free software: you can redistribute it and/or modify
** it under the terms of the GNU General Public License as published by
** the Free Software Foundation, either version 3 of the License, or
** (at your option) any later version.
**
** LAN Messenger is distributed in the hope that it will be useful,
** but WITHOUT ANY WARRANTY; without even the implied warranty of
** MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
** GNU General Public License for more details.
**
** You should have received a copy of the GNU General Public License
** along with LAN Messenger.  If not, see <http://www.gnu.org/licenses/>.
**
****************************************************************************/


#ifndef DEFINITIONS_H
#define DEFINITIONS_H

//	Information about the application
#define IDA_TITLE		"LAN Messenger X"
#ifdef Q_OS_WIN
#define IDA_PRODUCT		"LAN Messenger X"
#define IDA_COMPANY		"LAN Messenger X"
#else
#define IDA_PRODUCT		"lanmessengerx"
#define IDA_COMPANY		"lanmessengerx"
#endif
//	Must compare (via Helper::compareVersions) as newer than every
//	historical version string the shared Core code checks it against:
//	the settings-migration chain in settings.cpp goes up to "1.2.30",
//	and chatroomwindow.cpp/messengerbridge.cpp gate Public/Group Chat
//	participation on "> 1.2.10". A fork version below either threshold
//	(the previous "1.0.1" was below both) is silently miscompared as an
//	old, pre-Public-Chat client: every peer - on both Windows and
//	Android, since they share this same constant - gets excluded from
//	group chat, and an upgrade over a real old install's settings file
//	looks like a downgrade and gets wiped by the safety check at
//	settings.cpp:267. "2.0.0" clears all of that with room to spare.
//	Bumped to "2.0.2" for the build-warning cleanup patch (OpenSSL 3.0/
//	Qt6 deprecated-API warnings, a couple of latent bugs they pointed
//	at), then to "2.0.3" for a follow-up fix to that same patch (an
//	invalid OPENSSL_API_COMPAT value broke the Core build outright -
//	see Core/Core.pro and Core/src/crypto.cpp), then to "2.0.4" for
//	vendoring a per-ABI Android OpenSSL package (openssl-android/,
//	Core.pro, Android.pro) that resolved BUILD.md's previously
//	documented Android build blocker, then to "2.0.5" for raising
//	AndroidManifest.xml's minSdkVersion to 28 (Qt 6.11.2's Android kit
//	itself refuses anything lower - confirmed by a real build) and an
//	Android-only unused-parameter warning in settings.cpp, then to
//	"2.0.6" for renaming Core/src/strings.h/.cpp to lmcstrings.h/.cpp
//	(see Core/src/lmcstrings.h) - the old name collided with the NDK's
//	own POSIX strings.h, which its Android build's INCLUDEPATH shadowed
//	and broke every Android.pro translation unit with cascading
//	"templates must have C++ linkage" errors - then to "2.0.7" for two
//	more real Android.pro build breaks past that fix: crypto.h's
//	<openssl/rand.h> not found (Android.pro's own sources transitively
//	include crypto.h too, but only Core.pro had the openssl-android
//	INCLUDEPATH - see Android/Android.pro) and
//	androidforegroundservice.cpp's <QNativeInterface> not found (this
//	Qt install doesn't generate that convenience header for this
//	namespace - switched to its real path, QtCore/qnativeinterface.h),
//	then to "2.0.8" because that qnativeinterface.h guess was only half
//	right: the namespace resolved but QAndroidApplication wasn't a
//	member of it there - confirmed via a real findstr over the
//	installed headers that it actually lives in
//	QtCore/qcoreapplication_platform.h, and fixed a real, separate
//	[-Wunused-lambda-capture] warning in main.cpp (two notification
//	lambdas captured &app but never used it - &app is still passed as
//	QObject::connect's receiver/context argument, untouched) - none of
//	these changed the wire protocol or settings format, so no new
//	version-gate concerns beyond what "2.0.0" already cleared.
#define IDA_VERSION		"2.0.8"
#define IDA_DESCRIPTION	"LAN Messenger X is a free peer-to-peer messaging application for intra-network communication "\
						"and does not require a server.\n"\
						"LAN Messenger X works on essentially every popular desktop platform."
#define IDA_COPYRIGHT	"Copyright (C) 2010-2016 Qualia Digital Solutions."
#define IDA_CONTACT		"lanmsngr@gmail.com"
//	Original upstream project's own site - still feeds help.php/
//	faq.php/support.php (see Windows/lmc/src/helpwindow.cpp) and the
//	legacy MT_Version HTTP version-check (Core/src/messagingproc.cpp,
//	Windows/lmc/src/updatewindow.cpp) that request has no server for.
//	This fork's own "Check for Updates" menu action no longer calls
//	into that legacy check at all - see mainwindow.cpp's
//	updateAction_triggered() - so it's dead code, reachable from
//	nowhere in the UI, left in place rather than torn out wholesale.
#define IDA_DOMAIN		"http://lanmessenger.github.io"

//	Credits shown in the About dialog/screen on both platforms (see
//	Windows/lmc/src/aboutdialog.cpp and Android/qml/SettingsPage.qml) -
//	kept here, alongside IDA_TITLE/IDA_VERSION, as the single place both
//	clients read app identity/attribution from. IDA_COPYRIGHT above is
//	the original project's own GPL-header copyright line and is left
//	untouched; these three are this fork's own additions.
#define IDA_ORIGINAL_AUTHOR	"Original LAN Messenger created by Dilip Radhakrishnan (Qualia Digital Solutions)."
#define IDA_FORK_AUTHOR		"LAN Messenger X modernization and continued development by galandras12."
#define IDA_REPOSITORY		"https://github.com/galandras12/LAN_MessengerX"

#if defined Q_OS_WIN
#define IDA_PLATFORM	"Windows"
#elif defined Q_OS_MAC
#define IDA_PLATFORM	"Macintosh"
#elif defined Q_OS_LINUX
#define IDA_PLATFORM	"Linux"
#endif

#define DELIMITER		"||"
#define DELIMITER_ESC	"\\|\\|"
#define APP_MARKER		"lmcmessage"

/****************************************************************************
**	Datagram type definitions
**	The enum and the string array should always be synced
****************************************************************************/
enum DatagramType {
	DT_None = 0,
	DT_Broadcast,
	DT_PublicKey,
	DT_Handshake,
	DT_Message,
	DT_Max
};

const QString DatagramTypeNames[] = {
	"",
	"BRDCST",
	"PUBKEY",
	"HNDSHK",
	"MESSAG"
};

/****************************************************************************
**	Message type definitions
**	The enum and the string array should always be synced
****************************************************************************/
enum MessageType {
	MT_Blank = 0,
	MT_Announce,
	MT_Depart,
	MT_UserData,
	MT_Broadcast,
	MT_Status,
	MT_Avatar,
	MT_UserName,
	MT_Ping,
	MT_Message,
	MT_GroupMessage,
	MT_PublicMessage,
	MT_File,
	MT_Acknowledge,
	MT_Failed,
	MT_Error,
	MT_OldVersion,
	MT_Query,
	MT_Info,
	MT_ChatState,
	MT_Note,
    MT_Folder,
	//	These are used only for local communication between layers
	MT_Group,
	MT_Version,
	MT_WebFailed,
	MT_Refresh,
	MT_Join,
    MT_Leave,
	MT_Max
};

const QString MessageTypeNames[] = {
	"",
	"announce",
	"depart",
	"userdata",
	"broadcast",
	"status",
	"avatar",
	"name",
	"ping",
	"message",
	"groupmessage",
	"publicmessage",
	"file",
	"acknowledge",
	"failed",
	"error",
	"oldversion",
	"query",
	"info",
	"chatstate",
	"note",
    "folder",
	//	These are used only for local communication between layers
	"group",
	"version",
	"webfailed",
	"refresh",
	"join",
    "leave"
};

enum FileMode {
	FM_Blank = 0,
	FM_Send,
	FM_Receive,
	FM_Max
};

const QString FileModeNames[] = {
	"",
	"send",
	"receive"
};

/****************************************************************************
**	File operation definitions
**	The enum and the string array should always be synced
****************************************************************************/
enum FileOp {
	FO_Blank = 0,
    FO_Init,    // Id assigned, but request not yet sent. Used for files that are part of a folder transfer
	FO_Request,
	FO_Accept,
	FO_Decline,
	FO_Cancel,
	FO_Progress,
	FO_Error,
	FO_Abort,
	FO_Complete,
    FO_Next,
	FO_Max
};

const QString FileOpNames[] = {
	"",
    "init",
	"request",
	"accept",
	"decline",
	"cancel",
	"progress",
	"error",
	"abort",
    "complete",
    "next"
};

/****************************************************************************
**	File operation definitions
**	The enum and the string array should always be synced
****************************************************************************/
enum FileType {
	FT_None = 0,
	FT_Normal,
	FT_Avatar,
    FT_Folder,
	FT_Max
};

const QString FileTypeNames[] = {
	"",
	"normal",
    "avatar",
    "folder"
};

/****************************************************************************
**	Query operation definitions
**	The enum and the string array should always be synced
****************************************************************************/
enum QueryOp {
	QO_None = 0,
	QO_Get,
	QO_Result,
	QO_Max
};

const QString QueryOpNames[] = {
	"",
	"get",
	"result"
};

/****************************************************************************
**	Group Message operation definitions
**	The enum and the string array should always be synced
****************************************************************************/
enum GroupMsgOp {
	GMO_None = 0,
	GMO_Request,
	GMO_Join,
	GMO_Message,
	GMO_Leave,
	GMO_Max
};

const QString GroupMsgOpNames[] = {
	"",
	"request",
	"join",
	"message",
	"leave"
};

enum GroupOp {
	GO_None = 0,
	GO_New,
	GO_Rename,
	GO_Move,
	GO_Delete,
	GO_Max
};

enum TrayMessageType {
	TM_Connection,
	TM_Status,
	TM_Transfer,
	TM_Minimize,
	TM_Max
};
enum TrayMessageIcon {
	TMI_Info,
	TMI_Warning,
	TMI_Error,
	TMI_Max
};

//	User status definitions
enum StatusType {
	StatusTypeOnline = 0,
	StatusTypeBusy,
	StatusTypeOffline,
	StatusTypeAway,
	StatusTypeMax
};

#define ST_COUNT	6
const QString statusCode[] = {
	"chat",
	"busy",
	"dnd",
	"brb",
	"away",
	"gone"
};
const int statusType[] = {
	StatusTypeOnline,
	StatusTypeBusy,
	StatusTypeBusy,
	StatusTypeAway,
	StatusTypeAway,
	StatusTypeOffline
};

enum UserCap {
    UC_None = 0x00000000,
    UC_File = 0x00000001,
    UC_GroupMessage = 0x00000002,
    UC_Folder = 0x00000004,
    UC_Max = 0xFFFFFFFF
};

#define GRP_DEFAULT		"General"
#define GRP_DEFAULT_ID	"1CD75C10048C4E65F6082539A32DC111"

#define AUTO_CONNECTION	"Auto"

#define LMC_TRUE	"true"
#define LMC_FALSE	"false"

#define PROGRESS_TIMEOUT    1500

#endif // DEFINITIONS_H
