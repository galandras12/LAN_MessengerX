# LAN Messenger — Android kliens

Qt Quick/QML felület a megosztott [`/Core`](../Core) (`lmccore`) hálózati/
protokoll/titkosítási rétegre építve — ugyanaz a kód kezeli a LAN
discovery-t, TCP/UDP üzenetküldést és RSA/AES titkosítást, mint a
[`/Windows`](../Windows) kliensben, ezért ugyanazon a hálózaton a régi,
eredeti LAN Messenger kliensekkel és az új Windows kliens is kompatibilis
marad vele.

## Jelenlegi állapot

Ez a munkamenet egy **valódi, a `/Core` üzleti logikájára ténylegesen
bekötött** első verziót írt meg, nem csak UI-vázat:

- ✅ `src/messengerbridge.h/.cpp` — a Windows kliens `lmc.cpp`
  (`lmcCore`) orchestrátorának Android-megfelelője: létrehozza és
  elindítja a `lmcMessaging`-et (`/Core`), és feliratkozik az egyetlen,
  egységesített `messageReceived(MessageType, ...)` jelére. **Fontos
  felismerés a `/Core` vizsgálata közben**: ugyanezen a csatornán érkezik
  mind a jelenlét-forgalom (`MT_Announce`/`MT_Depart`/`MT_Status`/
  `MT_UserName`/`MT_Note`/`MT_Avatar`), mind a csevegés
  (`MT_Message`/`MT_GroupMessage`/`MT_Broadcast`) — pontosan úgy, ahogy a
  Windows-os `lmc.cpp` is kezeli. A `lmc.cpp` maga viszont **nem
  újrahasználható** Androidon: közvetlenül példányosítja és vezérli az
  összes QWidgets ablakot (`mainwindow.h`, `chatwindow.h` stb. közvetlen
  include-ja), ezért a `MessengerBridge` egy önálló, Widgets-mentes
  megfelelője, ami helyette QML-barát `Q_PROPERTY`/`Q_INVOKABLE`
  felületet ad.
- ✅ `src/contactmodel.h/.cpp` — `QAbstractListModel`, a `lmcMessaging::userList`-et
  tükrözi QML `ListView` számára (`userId`, `name`, `status`, `note`, `group` role-ok).
- ✅ `src/chatmodel.h/.cpp` — `QAbstractListModel`, beszélgetésenként egy
  példány, a bejövő/kimenő üzeneteket tárolja.
- ✅ `qml/Main.qml`, `qml/ContactListPage.qml`, `qml/ChatPage.qml` — Qt
  Quick Controls + Material stílusú kontaktlista és chat-buborék nézet,
  `StackView`-val a kettő között.
- ✅ `Android.pro` — a `lmccore` (`/Core`) statikus library-t linkeli,
  `QT += quick qml`, Android target beállítások.
- ✅ `android/AndroidManifest.xml` — a szükséges engedélyekkel
  (`INTERNET`, `ACCESS_WIFI_STATE`, `ACCESS_NETWORK_STATE`,
  `CHANGE_WIFI_MULTICAST_STATE`, `POST_NOTIFICATIONS`).

### Amit ez az első verzió tud

Indításkor csatlakozik a hálózathoz (`lmcMessaging::start()`), megjeleníti
a felfedezett kontaktokat élőben frissülő listában, és lehetővé teszi
egy kontakttal 1:1 szöveges üzenetváltást (mindkét irányban), valamint
broadcast üzenet küldését (`MessengerBridge::sendBroadcast()`, a QML
egyelőre nem hív rá UI-t).

### Amit ez az első verzió *nem* tud (nincs bekötve)

- Fájl-/mappaátvitel (`MT_File`/`MT_Folder`) — a `/Core` réteg
  (`filemessagingproc.cpp`) támogatja, a bridge egyelőre nem hívja.
- Csoportos csevegés/chat room UI (a `MT_GroupMessage` adatot már a
  chat-modell kezeli, de nincs hozzá csoport-létrehozó/kezelő QML nézet).
- Beállítások képernyő (felhasználónév/avatar/állapot szerkesztése —
  jelenleg a `lmcMessaging::init()` automatikusan generált alapértékeket
  használ: bejelentkezési név + gépnév alapján képzett user id).
- Értesítések, foreground service a háttérbeli elérhetőséghez (Android
  Doze-kezelés) — enélkül a discovery/üzenetfogadás valószínűleg leáll,
  ha az alkalmazás sokáig háttérben van. A manifestben egy
  `FOREGROUND_SERVICE` engedély placeholder van előkészítve.
- Üzenetelőzmény-perzisztencia (a `ChatModel` csak memóriában tárol,
  `/Core/src/history.cpp` már létezik erre, de nincs bekötve).

## ⚠️ Kritikus, ellenőrizetlen pont: OpenSSL Androidon

A `/Core`-beli `crypto.cpp` közvetlenül OpenSSL-t hív (`libcrypto`). A
Windows kliens ehhez egy Windows-os előre fordított `libcrypto.lib`-et
linkel a repó gyökerén (`/openssl`) — ez **Androidon nem használható**,
ott Android ABI-nkénti (arm64-v8a / armeabi-v7a / x86_64 / x86)
keresztfordított `libcrypto.so`/`.a` kell (pl. a közösségi
`android_openssl` csomag). Ez **nincs bekötve** az `Android.pro`-ba — lásd
az ottani kommenteket. Enélkül a `lmccore` linkelése Android célra
valószínűleg hibát fog adni. Ez a legnagyobb, még megoldatlan technikai
akadály a tényleges Android build előtt.

## Build előfeltételek (még nem ellenőrizve — nincs Android SDK/NDK/Qt ebben a környezetben)

- Qt 6 LTS Android kit (SDK + NDK Qt Creatorral telepítve).
- A fenti OpenSSL-Androidra probléma megoldása.
- `Core/Core.pro`-t Android ABI-nkénti target-ekkel kell buildelni, mielőtt
  az `Android.pro` linkelni tudja.
- Az `android/AndroidManifest.xml`-t érdemes végigfuttatni a Qt Creator
  "Add Android support" varázslóján a konkrét telepített Qt verzió ellen,
  hogy a `<meta-data>` kulcsok biztosan egyezzenek (ezek verziónként
  finoman változhatnak) — lásd a fájl tetején lévő megjegyzést.

## Mappa-elrendezés

```
Android/
├── Android.pro
├── src/               - main.cpp, MessengerBridge, ContactModel, ChatModel
├── qml/                - Main.qml, ContactListPage.qml, ChatPage.qml, qml.qrc
└── android/
    └── AndroidManifest.xml
```
