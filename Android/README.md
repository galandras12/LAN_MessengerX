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
- ✅ `src/historylistmodel.h/.cpp` — a mentett üzenetelőzmény-bejegyzések
  listája (lásd külön szakasz lent).
- ✅ `qml/Main.qml`, `qml/ContactListPage.qml`, `qml/ChatPage.qml`,
  `qml/NewGroupChatPage.qml`, `qml/GroupChatPage.qml`,
  `qml/SettingsPage.qml`, `qml/HistoryPage.qml`,
  `qml/HistoryDetailPage.qml` — Qt Quick Controls + Material stílusú
  kontaktlista, chat-buborék, csoportos chat, profil-beállítások és
  üzenetelőzmény nézetek, `StackView`-val közöttük.
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

## Üzenetelőzmény (History)

A kontaktlista fejlécének 📜 gombja a mentett beszélgetéseket listázza. A
`/Core`-beli `History` osztályt (`Core/src/history.cpp/.h`) **változtatás
nélkül** használja — ugyanazt a `messenger.db` fájlformátumot írja/olvassa,
amit a Windows kliens `lmcHistoryWindow`-ja is használ, tehát az egyik
kliens által mentett bejegyzés a másikban is megjelenik (persze csak akkor,
ha ugyanazt a fájlt éri el — Androidon ez az app saját, alkalmazás-specifikus
tárhelyén van, nem szinkronizálódik automatikusan a Windows géppel).

**Egy tudatos eltérés a Windows-modelltől**: a Windows kliens
(`Windows/lmc/src/chatwindow.cpp::stop()`) csak **egyszer**, a chat *ablak
bezárásakor* hívja a `History::save()`-t, a teljes addig felgyűlt,
formázott munkamenet-naplót egyetlen HTML-blobként mentve, a partner neve
alatt kulcsolva. Az Android kliensen nincs ehhez hasonló "ablak bezárása"
pillanat (egy QML chat-oldal szabadon bezárható/újranyitható anélkül, hogy
ez bármit jelentene), ezért itt minden egyes szöveges üzenet (1:1 és
csoportos chat, fájlátvitel-bejegyzések **nem**) azonnal, önállóan
elmentésre kerül, saját kis HTML-blobként, ugyanazzal a kulcsolási logikával
mint Windowson (`peerNames.value(peerId)` 1:1-hez, `tr("Group
Conversation")` csoportos chathez). A fájlformátum és a `History` API
érintetlen, csak a bejegyzés-granularitás más — lásd `messengerbridge.h`
osztály-kommentjét a pontos indoklásért.

- ✅ `messenger.history` (`HistoryListModel`) — a mentett bejegyzések
  listája (`name`/`date`/`offset` role-ok), `messenger.refreshHistory()`-val
  frissítve (a `HistoryPage.qml` a megnyitásakor hívja).
- ✅ `messenger.historyMessageHtml(offset)` — egy bejegyzés nyers HTML
  tartalma (`History::getMessage()`), a `HistoryDetailPage.qml` egy
  `TextEdit`-tel (`textFormat: TextEdit.RichText`) jeleníti meg — ez az
  egyenértékű a Windows-os `pMessageLog->setHtml(data)`-nak.
- ✅ `messenger.clearHistory()` — törli a teljes `messenger.db` fájlt
  (`History::historyFile()`), megegyezik a Windows-os
  `btnClearHistory_clicked()` viselkedésével.
- ✅ `messenger.historyEnabled` (kétirányú property, `IDS_HISTORY`
  beállításkulcs) — ki/be kapcsolható a `SettingsPage.qml`
  "Save message history" kapcsolójával; kikapcsolva egyetlen új üzenet sem
  kerül mentésre (de a már meglévő bejegyzések megmaradnak, ahogy
  Windowson is).

### Amit ez **nem** tesz

- Fájlátvitel-bejegyzések nem kerülnek be az előzménybe (csak szöveges
  üzenetek) — Windows saját munkamenet-naplója ezeket is tartalmazza egy
  session-en belül, itt szándékosan nincs replikálva.
- A `History::historyFile()` útvonala (`IDS_HISTORYPATH`/
  `IDS_SYSHISTORYPATH`) módosítására nincs UI — a rendszer-alapértelmezett
  útvonalat használja.

### Talált és javított Qt6-kompatibilitási hiba eközben

A `/Core`-beli `history.cpp` és `stdlocation.h` mindegyike
`QStandardPaths::DataLocation`-t hívott — ezt az enumértéket a Qt **eltávolította
Qt6-ban** (Qt 5.14 óta deprecated volt, Qt6-ban már nem is létezik), tehát ez
a két fájl **egyáltalán nem fordult volna** Qt6 alatt, sem Windowson, sem
Androidon. Lecserélve `QStandardPaths::AppLocalDataLocation`-re (a hivatalos
Qt-ajánlott megfelelő), plusz a hiányzó `#include <QStandardPaths>`
hozzáadva mindkét fájlhoz. Ez egy valódi, a fordítást megakasztó hiba volt,
nem csak stílus — a Fázis 2 (Qt6-portolás) korábbi, kézi API-audit köre nem
vette észre, mert `history.cpp`/`stdlocation.h` nem szerepelt a akkor
átvizsgált fájlok listájában.

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
- ✅ **Avatar (profilkép) szerkesztése** (`messenger.setAvatar(fileUrl)`):
  a `SettingsPage.qml` tetején lévő kör alakú kép megérintésével egy
  `FileDialog` nyílik; a kiválasztott képet 96×96-ra skálázva elmenti a
  `StdLocation::avatarFile()` (`/Core`-beli, mindig ugyanaz a fájl, csak a
  *tartalma* változik) útvonalra, elmenti az `IDS_AVATAR = -1` ("egyéni
  kép") beállítást, majd **egyetlen** `lmcMessaging::sendMessage(MT_Avatar,
  nullptr, ...)` hívást tesz — a `/Core` innentől mindent maga csinál: a
  `Windows/lmc/src/mainwindow.cpp::setAvatar()`/`sendAvatar()` és a
  `Core/src/filemessagingproc.cpp`/`messagingproc.cpp` gondos átvizsgálása
  alapján ez egyetlen hívás elindít egy saját magának küldött
  "frissült az avatarom" visszajelzést (UI-frissítéshez), majd minden
  online felhasználónak egy **automatikusan elfogadott**, felhasználói
  megerősítést nem igénylő fájlátvitelt (`FT_Avatar`) — a fogadó oldalon
  ez `<cache>/avt_<userId>.png`-ként mentődik és a `User::avatarPath`
  mezőt is frissíti, ami a meglévő `ContactModel`-en (új `avatarPath`
  role) és a kontaktlista körkép-megjelenítésén keresztül minden további
  Android-kódolás nélkül megjelenik. A helyi profilkép-előnézet
  (`messenger.localAvatarPath`) mindig ugyanaz az útvonal — csak a
  tartalma változik —, ezért a QML oldal egy `#v=N` URL-töredékkel
  kényszeríti ki a kép újratöltését kép­cserénél (lásd `SettingsPage.qml`
  kommentjét).

### Amit ez **nem** tesz

- **Beépített, számozott avatar-galéria** — Windowson a felhasználó egy
  előre csomagolt kép-készletből is választhat (`nAvatar` index,
  `avtPic[]`), nem csak egyéni képet tölthet fel; ehhez a Windows-os
  Widgets UI-specifikus erőforrás-fájlok (`uidefinitions.h`) kellenének,
  amik nincsenek portolva Androidra — csak az egyéni kép ("custom
  avatar", `nAvatar = -1`) útvonal van bekötve.
- **Kontakt-avatarok élő frissítése** — ha egy már látott kontakt
  lecseréli a képét ugyanabban a munkamenetben, a kontaktlista
  `Image`-je nem kényszeríti ki az újratöltést (nincs a helyi
  előnézetéhez hasonló verziószámláló bekötve rá) — az oldal
  újranyitásáig a régi kép látszódhat.
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
- ✅ **Már létrehozott szobához utólagos meghívás**
  (`messenger.addParticipantsToRoom(threadId, userIds)`): a
  `GroupChatPage.qml` fejlécének 👥+ gombja a `NewGroupChatPage.qml`-t
  nyitja meg (a már bent lévők kipipálva/letiltva), és a kiválasztottaknak
  ugyanazt a pont-pont `GMO_Request`-et küldi, mint az induló meghívás —
  pontosan lekövetve `lmcChatRoomWindow::selectContacts()`-ot
  (`chatroomwindow.cpp`): nincs szükség külön értesítésre a szoba már
  meglévő tagjai felé, mert az új meghívott saját `GMO_Join`-ja (amit a
  helyi szoba létrehozásakor amúgy is mindenkinek szétküld) éppúgy eléri
  őket, mint bármely más csatlakozást.

### Amit ez **nem** tesz

- **Windows "Public Chat"** (egy mindig aktív, minden csatlakozó
  felhasználót automatikusan tartalmazó szoba, `MT_PublicMessage`) — ez
  egy külön, jelentősen másképp viselkedő funkció a Windows kliensben,
  szándékosan nincs portolva; a most implementált "csoportos chat" a
  Windows `lmcChatRoomWindow` *ad hoc, meghívásos* módja.
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
│                         RoomListModel, HistoryListModel,
│                         AndroidForegroundService (JNI wrapper)
├── qml/                - Main.qml, ContactListPage.qml, ChatPage.qml,
│                         NewGroupChatPage.qml, GroupChatPage.qml,
│                         SettingsPage.qml, HistoryPage.qml,
│                         HistoryDetailPage.qml, qml.qrc
└── android/
    ├── AndroidManifest.xml
    └── src/org/qualiatech/lanmessengerx/
        └── MessengerForegroundService.java
```

(`android/src/` is where `ANDROID_PACKAGE_SOURCE_DIR` - set in `Android.pro`
- tells Qt's Android build to pick up extra Java sources; no separate
build step needed for it beyond the normal Android build.)
