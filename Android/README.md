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
- ✅ `src/chatmodel.h/.cpp` — `QAbstractListModel`, beszélgetésenként (és
  csoportos szobánként) egy példány, a bejövő/kimenő üzeneteket, fájlátvitel-
  bejegyzéseket és (szobáknál) join/leave rendszerüzeneteket tárolja.
- ✅ `src/roomlistmodel.h/.cpp` — a csoportos chat szobák listája (lásd
  külön szakasz lent).
- ✅ `qml/Main.qml`, `qml/ContactListPage.qml`, `qml/ChatPage.qml`,
  `qml/NewGroupChatPage.qml`, `qml/GroupChatPage.qml`,
  `qml/SettingsPage.qml` — Qt Quick Controls + Material stílusú
  kontaktlista, chat-buborék, csoportos chat és profil-beállítások
  nézetek, `StackView`-val közöttük.
- ✅ `Android.pro` — a `lmccore` (`/Core`) statikus library-t linkeli,
  `QT += quick qml`, Android target beállítások.
- ✅ `android/AndroidManifest.xml` — a szükséges engedélyekkel
  (`INTERNET`, `ACCESS_WIFI_STATE`, `ACCESS_NETWORK_STATE`,
  `CHANGE_WIFI_MULTICAST_STATE`, `POST_NOTIFICATIONS`,
  `FOREGROUND_SERVICE`/`FOREGROUND_SERVICE_DATA_SYNC`).
- ✅ `src/androidforegroundservice.h/.cpp` + `android/src/.../MessengerForegroundService.java`
  — háttérbeli működés (lásd külön szakasz lent).

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

- Üzenetelőzmény-perzisztencia (a `ChatModel` csak memóriában tárol,
  `/Core/src/history.cpp` már létezik erre, de nincs bekötve).

## Beállítások képernyő (profil szerkesztése)

A kontaktlista fejlécének ⚙ gombja a `SettingsPage.qml`-t nyitja meg:
megjelenítendő név, állapot (Elérhető/Foglalt/Ne zavarjanak/Mindjárt jövök/
Távol/Láthatatlan), és egy szabad szöveges megjegyzés ("note"). A Windows
kliens **három különálló, egymástól független** frissítési útvonalát
követtem le (nincs egységes "beállítás megváltozott" üzenet a
protokollban):

- ✅ **Név** (`messenger.setLocalName()`): csak elmenti az `IDS_USERNAME`
  beállítást, majd meghívja a `lmcMessaging::settingsChanged()`-et
  (`/Core`) — ez a metódus **már eleve tartalmazza** a névváltozás
  felismerését és az `MT_UserName` szétküldését (lásd
  `Core/src/messaging.cpp`), pontosan úgy, ahogy a Windows-os
  `settingsdialog.cpp` is csak elmenti a beállítást és a `lmc.cpp`
  hívja meg ugyanezt a metódust — nem kellett újraírnom ezt a logikát,
  csak a meglévőt hívni.
- ✅ **Állapot** (`messenger.setLocalStatus()`) és **megjegyzés**
  (`messenger.setLocalNote()`): közvetlenül módosítják a
  `lmcMessaging::localUser` mezőit, elmentik a beállítást, és
  `MT_Status`/`MT_Note` üzenetet küldenek — pontosan úgy, ahogy
  `lmcMainWindow::statusAction_triggered()`/`txtNote_lostFocus()` teszi
  Windowson (a `NULL` címzettet is lekövetve: a Core
  `MT_Status`/`MT_Note` ága eleve figyelmen kívül hagyja a paraméterként
  kapott userId-t, mindig minden online felhasználónak szétküldi).
- ✅ `messenger.statusCodes()`/`statusLabels()` — a `/Core`-beli
  `statusCode[]`/`lmcStrings::statusDesc()` tömböket adja át a QML
  állapot-választójának (azonos sorrendben).

### Amit ez **nem** tesz

- **Avatar szerkesztése** — ehhez fájlválasztó kellene, ami ugyanabba a
  `content://` URI-problémába ütközik, mint a fájlküldés (lásd fent), plusz
  egy `FT_Avatar`-típusú fájlátvitel-folyamat elindítása; szándékosan nincs
  bekötve.
- Hálózati/kapcsolati beállítások (portok, multicast cím, stb.) — ezek
  ritkán módosított, technikai jellegű beállítások, nincsenek a mostani
  képernyőn.

## Csoportos chat (group chat room)

A kontaktlista fejlécének 👥+ gombja csoportos beszélgetést indít.
A protokollt **nem találtam ki**, hanem lekövettem, hogyan működik
ténylegesen a Windows kliens `chatroomwindow.cpp`-je:

- ✅ `messenger.createGroupChat(userIds)` — egy `Helper::getUuid()`-vel
  generált `threadId` (szál-azonosító) mellett `MT_GroupMessage`
  üzenetet küld **közvetlenül, egyenként** minden meghívott
  kontaktnak, `XN_THREAD` + `XN_GROUPMSGOP: "request"` mezőkkel.
- ✅ A meghívott oldalon a `MT_GroupMessage`/`GMO_Request` fogadása
  létrehozza a szoba helyi állapotát, és — pontosan úgy, ahogy a Windows
  kliens `lmcChatRoomWindow::init()`-je is teszi — **mindenkinek**
  (`NULL` címzett, azaz az összes online felhasználónak) szétküldi a
  saját csatlakozását (`GMO_Join`). Ezt csak azok a kliensek dolgozzák
  fel ténylegesen, akiknek már nyitva van ugyanez a `threadId`-jú szoba
  — mindenki más egyszerűen figyelmen kívül hagyja. Ez a protokoll saját,
  szerver nélküli módja annak, hogy a résztvevők megtudják, ki csatlakozott,
  anélkül hogy bárki nyilvántartaná a szoba teljes tagságát.
- ✅ Az üzenetküldés (`GMO_Message`) viszont **célzottan**, résztvevőnként
  megy ki (nem broadcast), ugyanúgy mint a Windows kliensben.
- ✅ Kilépéskor (`messenger.leaveGroupChat()`) `GMO_Leave` megy ki
  mindenkinek (`NULL` címzett), a helyi szoba-állapot törlődik.
- ✅ `src/roomlistmodel.h/.cpp` — a jelenleg aktív szobák listája
  (kontaktlista fölött, "Group Chats" szakasz).
- ✅ `qml/NewGroupChatPage.qml` — kontakt-kiválasztó (checkbox lista),
  `qml/GroupChatPage.qml` — a szoba nézete: résztvevőszám, join/leave
  rendszerüzenetek az idővonalon (szürke, buborék nélküli felirat),
  normál buborékok a tartalmi üzeneteknek, feladó neve feltüntetve
  (mivel több résztvevő is van, ellentétben az 1:1 chattel).
- ✅ Verzió-ellenőrzés replikálva: 1.2.10-es vagy régebbi kliens-verziót
  jelentő partnerek nem kerülnek be a szoba résztvevői közé (ők még nem
  ismerik ezt a funkciót) — pontosan úgy, ahogy
  `lmcChatRoomWindow::addUser()` is teszi.

### Amit ez **nem** tesz

- **Windows "Public Chat"** (egy mindig aktív, minden csatlakozó
  felhasználót automatikusan tartalmazó szoba, `MT_PublicMessage`) — ez
  egy külön, jelentősen másképp viselkedő funkció a Windows kliensben,
  szándékosan nincs portolva; a most implementált "csoportos chat" a
  Windows `lmcChatRoomWindow` *ad hoc, meghívásos* módja.
- Már létrehozott szobához **később** további kontaktok hozzáadása
  (`lmcChatRoomWindow::addContactAction_triggered()` Windows-on) — most
  csak a létrehozáskori meghívás működik.
- Fájlátvitel csoportos szobán belül — a `ChatModel` már támogatja a
  fájl-bejegyzéseket (lásd fent), de a szoba-kezelő kód nem indít
  `MT_File`-t szobakontextusban.

## Háttérbeli működés (foreground service)

Most már bekötve:

- ✅ `android/src/org/qualiatech/lanmessengerx/MessengerForegroundService.java`
  — egy minimális Android foreground service, aminek egyetlen feladata,
  hogy életben tartsa az alkalmazás **teljes folyamatát** (így a benne futó
  `lmcMessaging`-et, a socketjeit és időzítőit is), amíg az app
  háttérben van — enélkül Android idővel felfüggeszti/kilövi a háttérbe
  került folyamatot, ami csendben leállítaná a discovery-t és az
  üzenet-/fájlkézbesítést. Emellett megszerzi és az app teljes
  élettartamára tartja a Wi-Fi multicast lockot is (`CHANGE_WIFI_MULTICAST_STATE`
  engedélyt korábban már deklaráltuk a manifestben, de **ténylegesen nem
  volt megszerezve sehol** — ezt a hibát is ez a munkamenet javította ki:
  bizonyos Wi-Fi chipek/driverek eldobják a multicast csomagokat ez
  nélkül, függetlenül attól, hogy az app előtérben van-e).
- ✅ `src/androidforegroundservice.h/.cpp` — vékony JNI wrapper C++ oldalról
  (`QJniObject`/`QNativeInterface::QAndroidApplication::context()`), amit
  a `main.cpp` hív: a multicast lockot induláskor egyszer megszerzi (az
  app teljes élettartamára), a foreground service-t pedig
  `QGuiApplication::applicationStateChanged`-re hallgatva csak akkor
  indítja el, amikor az app ténylegesen háttérbe kerül
  (`Qt::ApplicationHidden`), és leállítja, amint visszatér előtérbe
  (`Qt::ApplicationActive`) — így az értesítés nem látszik feleslegesen,
  amíg a felhasználó ténylegesen használja az appot.
- ✅ `AndroidManifest.xml`: regisztrálva a service (`foregroundServiceType="dataSync"`),
  hozzáadva az Android 14+ által megkövetelt típus-specifikus engedély
  (`FOREGROUND_SERVICE_DATA_SYNC`) a már meglévő általános
  `FOREGROUND_SERVICE` mellé.
- ✅ **Futásidejű `POST_NOTIFICATIONS` engedélykérés** Android 13+ (API 33+)
  alatt: `AndroidForegroundService::requestNotificationPermission()`
  (`src/androidforegroundservice.h/.cpp`) egyszer, induláskor
  (`main.cpp`) meghívja a Java oldali
  `MessengerForegroundService.requestNotificationPermission(Activity)`-t,
  ami `Activity.requestPermissions()`-t hív, ha az engedély még nincs
  megadva. Nincs egyedi `Activity` alosztály ebben az appban, ami az
  `onRequestPermissionsResult()`-öt fogadná, így a válasz (elfogadva/
  elutasítva) forráskód-szinten nem kerül feldolgozásra — Android maga
  megjegyzi a döntést, és a service/appfolyamat mindkét esetben ugyanúgy
  fut tovább, csak az engedély hiányában egyik értesítés (se a "fut a
  háttérben", se az új-üzenet, lásd lent) sem jelenik meg ténylegesen.
- ✅ **Push-jellegű új-üzenet/fájlkérés-értesítés**:
  `MessengerForegroundService.showMessageNotification(Context, int
  notificationId, String title, String text)` egy külön, magas
  fontosságú (`IMPORTANCE_HIGH`) csatornát (`lanmessengerx_messages`)
  használ — szándékosan **nem** ugyanazt, mint a fenti, alacsony
  fontosságú, néma "fut a háttérben" értesítés. A `main.cpp` feliratkozik
  a `MessengerBridge::incomingMessage`/`incomingFileRequest` jelekre, és
  csak akkor hív értesítést (`AndroidForegroundService::
  showMessageNotification()`), ha `QGuiApplication::applicationState() !=
  Qt::ApplicationActive` — vagyis az app épp nincs előtérben. A
  `notificationId` a küldő `userId`-jának hash-e, nem az üzenet/fájl
  azonosítójáé, így ugyanattól a küldőtől érkező több üzenet a *saját*
  értesítését frissíti/cseréli, nem halmozódik végtelenül; egy fájlkérés
  ugyanattól a küldőtől ugyanígy felülírja egy függőben lévő
  chat-értesítését — ez egy tudatosan vállalt egyszerűsítés (nem külön
  azonosító-tér fajtánként).

### Amit ez **nem** old meg teljesen

- **OEM-specifikus agresszív akkumulátor-kezelés** (pl. Xiaomi/MIUI,
  Huawei, egyes Samsung-beállítások) sok esetben a hivatalos Android
  foreground service védelmet is felülbírálja, hacsak a felhasználó
  kézzel ki nem veszi az appot az adott gyártó saját
  "akkumulátor-optimalizálás" listájából. Ez platform-szintű korlát, nem
  ezen a kódon múlik.
- A `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS` engedély (amivel az app
  kérhetné, hogy Android saját Doze-listájáról is levegye) szándékosan
  nincs bekötve — ez egy invázívabb, Play Store-felülvizsgálatot igénylő
  engedély, külön felhasználói döntést igényelne.
- Az értesítés kis ikonja jelenleg az app launcher-ikonját használja
  (működik, de nem a szokásos fehér-sziluett stílus) — kozmetikai
  hiányosság, lásd a Java fájl megjegyzését.

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
├── src/               - main.cpp, MessengerBridge, ContactModel, ChatModel,
│                         RoomListModel, AndroidForegroundService (JNI wrapper)
├── qml/                - Main.qml, ContactListPage.qml, ChatPage.qml,
│                         NewGroupChatPage.qml, GroupChatPage.qml,
│                         SettingsPage.qml, qml.qrc
└── android/
    ├── AndroidManifest.xml
    └── src/org/qualiatech/lanmessengerx/
        └── MessengerForegroundService.java
```

(`android/src/` is where `ANDROID_PACKAGE_SOURCE_DIR` - set in `Android.pro`
- tells Qt's Android build to pick up extra Java sources; no separate
build step needed for it beyond the normal Android build.)
