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

**Fájlátvitel (egyetlen fájl, mappaátvitel még nem)**: a `ChatPage.qml`
csatolás-gombja (📎) file dialógust nyit, a küldés/fogadás/elfogadás/
elutasítás/megszakítás/haladás mind a `MessengerBridge::sendFile()`/
`acceptFile()`/`declineFile()`/`cancelFile()` metódusokon és a
`messageReceived(MT_File, ...)` jel feldolgozásán megy át — ugyanazt a
`Core/src/filemessagingproc.cpp`-beli állapotgépet használja, mint a
Windows kliens (`FO_Request`/`FO_Accept`/`FO_Decline`/`FO_Cancel`/
`FO_Progress`/`FO_Complete`/`FO_Error`). A fájlátvitel-bejegyzések
(fájlnév, méret, folyamatjelző, Elfogad/Elutasít/Megszakít gombok) a
csevegés idővonalán belül jelennek meg, WhatsApp/Telegram-stílusban, nem
egy külön "átvitelek" ablakban (mint a Windows kliens `transferwindow`-ja).
A fogadott fájlok mentési helye (`StdLocation::fileStorageDir()`,
`QStandardPaths::DocumentsLocation`) Androidon app-specifikus külső
tárhelyre mutat, ami **külön futásidejű engedély nélkül** írható —
ellenőriztem a `/Core` kódját, ez rendben van.

⚠️ **A küldés oldala korlátozott**: a `QUrl::toLocalFile()` csak valódi
`file://` URL-eket tud helyi elérési úttá alakítani. Ha Android natív
fájlválasztója (Storage Access Framework) egy `content://` URL-t ad vissza
— ami a Letöltések vagy egy felhő-tárhely esetén tipikus —, a küldés
csendben nem történik meg (`sendFile()` korán visszatér). Ez azért van,
mert a `/Core`-beli `crypto.cpp`/`netstreamer.cpp` sima `QFile`-alapú
fájl-hozzáférést vár, nem SAF `content://` URI-t — ennek a rendes
megoldása (pl. a tartalom App-specifikus/külső tárhelyre másolása küldés
előtt) még nincs implementálva.

### Amit ez az első verzió *nem* tud (nincs bekötve)

- Mappaátvitel (`MT_Folder`) — a `/Core` réteg (`filemessagingproc.cpp`)
  támogatja, a bridge egyelőre csak az `MT_File` (egyetlen fájl) ágat
  kezeli.
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
