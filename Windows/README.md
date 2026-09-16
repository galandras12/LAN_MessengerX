# LAN Messenger — Windows kliens

Ez a mappa az eredeti LAN Messenger (QualiaTech, lanmessenger-master) Windows/asztali
kliensének modernizált forrását tartalmazza. Az eredeti, változatlan README és
platform-specifikus build-jegyzetek itt maradtak referenciaként:
[`README-original.md`](README-original.md), [`PLATFORM_SPECIFIC.md`](PLATFORM_SPECIFIC.md).

## Jelenlegi állapot (lásd a teljes tervet: `/root/.claude/plans/reflective-herding-dolphin.md`
a fejlesztői munkamenetben, vagy kérd el a modernizációs terv összefoglalóját)

Ez a munkamenet a modernizációs terv **Fázis 0–1** részét valósította meg:

- ✅ A platformfüggetlen hálózati/protokoll/titkosítási réteg kiemelve a
  [`/Core`](../Core) mappába, ezt linkeli be ez a projekt (`lmc.pro`).
  Azóta (Fázis 3) a `/Core` tényleges, önálló statikus library-vé
  (`lmccore`) is épül — lásd lent.
- ✅ Javítva egy konkrét, éles összeomlás-forrás: a TCP üzenetkeretezés
  (`Core/src/netstreamer.cpp`, `MsgStream::readyRead`) nem kezelte helyesen,
  ha a 4 bájtos hosszfejléc vagy az üzenettörzs több hálózati olvasásra
  töredezett szét — ez időszakos, nehezen reprodukálható összeomlásokat
  okozhatott.
- ✅ Javítva egy **use-after-free/double-free hiba** a titkosítási rétegben
  (`Core/src/crypto.cpp`): az `encrypt()`/`decrypt()` minden hívása egy
  OpenSSL kontextus-wrappert **érték szerint** másolt ki egy `QMap`-ból, majd
  a helyi másolat destruktora felszabadította a wrapper által birtokolt
  `EVP_CIPHER_CTX*`-et — miközben a térképben tárolt eredeti még mindig
  ugyanarra a felszabadított memóriára mutatott. Ez OpenSSL 1.1+/3.x ellen
  szinte minden titkosított üzenetváltásnál heap-korrupciót, azaz
  időszakos összeomlást okozott volna. Most a térképek pointert tárolnak,
  másolás nélkül.
- ✅ Javítva egy memóriaszivárgás a fájlátvitelben (`FileSender`/`FileReceiver`
  sosem szabadította fel a belső puffert).
- ✅ A nem használt `sql` Qt modul és a Symbian/Maemo5-specifikus, holt
  `lmcapp.pro` kódágak eltávolítva.

Ez a munkamenet emellett elvégezte a **Fázis 2** (Qt 6 portolás) forráskód-szintű
részét is — a Qt5/Qt4-es, Qt6 alatt már nem létező API-k lecserélve:

- ✅ `QRegExp`/`QRegExpValidator` → `QRegularExpression`/`QRegularExpressionValidator`
  (`settingsdialog.h/.cpp`, `messagelog.cpp`, `lmcapp/qtlocalpeer.cpp`) — az
  `exactMatch()` hívások `QRegularExpression::anchoredPattern()` +
  `match().hasMatch()` mintára cserélve, mert a `QRegularExpression`-nek
  nincs `exactMatch()` metódusa.
- ✅ `foreach` (Qt6 alapból nem tartalmazza, opcionális Qt5Compat modult
  igényelne) → C++11 range-based `for` (`mainwindow.cpp`, `theme.cpp`,
  `chatwindow.cpp`, `lmcapp/application.cpp`, `lmcapp/qtlockedfile_win.cpp`).
- ✅ `QDesktopWidget` (Qt6-ban megszűnt) → `QGuiApplication::primaryScreen()`
  (`historywindow.cpp`, `helpwindow.cpp`, `transferwindow.cpp`,
  `updatewindow.cpp`; az `aboutdialog.cpp`-beli include használat nélkül
  holt volt, törölve).
- ✅ `qrand()`/`qsrand()` (Qt6-ban megszűnt) → `QRandomGenerator`
  (`mainwindow.cpp`, avatar véletlen kiválasztás).
- ✅ `QString::null` (Qt6-ban megszűnt statikus tag) → `QString()` — 60
  előfordulás a `/Core` és `/Windows` fákban.
- ✅ `QString::SkipEmptyParts` → `Qt::SkipEmptyParts` (`lmc.cpp`,
  `messagelog.cpp`, `Core/src/shared.cpp`, `Core/src/trace.cpp`).
- ✅ `Q_WS_WIN` (Qt4-es makró, Qt5/6 sosem definiálja) → `Q_OS_WIN`
  (`lmcapp/qtsingleapplication.h` mindkét példánya, `qtlockedfile.h`
  mindkét példánya) — enélkül a DLL export/import makrók örökre az
  üres ágra estek volna Windows alatt is.
- ✅ `Q_WS_X11`-re épülő `QApplication(Display*, ...)` konstruktorok
  törölve (`lmcapp/qtsingleapplication.h`/`.cpp`) — ezek a Qt5 QPA
  platform-absztrakciója óta nem léteznek a Qt-ban, a makró átnevezése
  helyett törölve, hogy ne maradjon csapda egy jövőbeli
  keresd-cseréld munkához.
- ✅ `QStandardPaths::DataLocation` (Qt 5.14 óta deprecated, **Qt6-ban
  ténylegesen megszűnt** — fordítási hiba lett volna) →
  `QStandardPaths::AppLocalDataLocation`, 7 előfordulás
  (`Core/src/history.cpp`, `Core/src/stdlocation.h`) — ezt az Android
  kliens üzenetelőzmény-funkciójának fejlesztése közben vettem észre,
  mert az `/Core` mindkét klienshez közös, tehát ez a hiba a Windows
  buildet is ugyanúgy megakasztotta volna, csak a korábbi Fázis 2-es
  audit kör nem terjedt ki erre a két fájlra.

### Valós build-bel talált és javított hibák

A felhasználó első tényleges (MinGW/Qt6) build-kísérlete az alábbi,
korábban észre nem vett fordítási hibákat hozta fel — ezek mind
javítva lettek:

- ✅ `Qt::WindowFlags flags = 0` (öt konstruktor-deklarációban:
  `historywindow.h`, `aboutdialog.h`, `mainwindow.h`, `chatwindow.h`,
  `settingsdialog.h`) → `Qt::WindowFlags flags = Qt::WindowFlags()`.
  Qt6 alatt a `0`-t `Qt::WindowType`-dá (majd `QFlags`-szá) alakító
  implicit konverzió `-fpermissive` nélkül fordítási hibát ad
  (`invalid conversion from 'int' to 'Qt::WindowType'`).
- ✅ Hívatlan (nem tagfüggvényként hívott) `elidedText(...)` három
  előfordulása (`usertreewidget.cpp`) → átírva
  `painter->fontMetrics().elidedText(...)` taghívásra, ami a
  dokumentált, mindig létező `QFontMetrics::elidedText()` API.
- ✅ `Qt::SystemLocaleDate`/`Qt::SystemLocaleShortDate` (5 előfordulás:
  `historywindow.cpp`, `messagelog.cpp`) — ezek a `Qt::DateFormat`
  enum-értékek Qt6-ban ténylegesen megszűntek (Qt 5.15 óta deprecated
  voltak) → `QLocale::system().toString(dátum/idő, QLocale::ShortFormat)`.

⚠️ **Az `openssl/rand.h: No such file or directory` hiba NEM kódhiba** —
ez azt jelzi, hogy a repó gyökerében még nincs létrehozva az
`openssl/include`+`openssl/lib` mappapár (lásd lent, "Build
előfeltételek"). Ez egy szükséges, egyszeri, manuális build-előfeltétel
(az OpenSSL 3.x fejlesztői csomagját nem tartalmazza a repó — se méret,
se licenc okokból nem íratnám bele), nem valami, amit ez a modernizáció
elmulasztott volna bekötni.

A felhasználó valós OpenSSL-elrendezése (`openssl/lib/VC/x64/{MD,MDd,MT,MTd}/`
— a hivatalos OpenSSL `nmake install` MSVC build kimenete, nem a
feltételezett "lapos" `lib/libcrypto.lib`) alapján a `lmc.pro` OpenSSL
linkelő sora mostantól MSVC kit esetén automatikusan a megfelelő
(Release: `MD`, Debug: `MDd` — a Qt saját MSVC kitjei dinamikusan
linkelik a CRT-t, ezért **nem** a statikus `MT`/`MTd` pár kell)
alkönyvtárat választja — lásd a [`BUILD.md`](../BUILD.md) OpenSSL
szakaszát a részletekért.

Egy további, **saját kezdeményezésű, folytatólagos Qt6-audit körben**
(nem a felhasználó build-logjából, mert ez a kódág jelenleg nem
Windows-specifikus, tehát a Windows build-et nem érinti) talált hiba:

- ✅ `QSound`/`QAudioDeviceInfo` (`soundplayer.cpp`, **csak a
  `unix`/`macx` kódágon** — Windows alatt a `sndPlaySoundA` WinAPI-t
  hívja közvetlenül, ezt nem érinti) — mindkettő megszűnt a Qt6-os
  QtMultimedia-ból. `QSound::play()` → `QSoundEffect` (élő `QObject`
  példányt igényel a lejátszás idejére, ezért egy heap-en létrehozott,
  lejátszás végén saját magát törlő példánnyal helyettesítve).
  `QAudioDeviceInfo::availableDevices(...)` → `QMediaDevices::audioOutputs()`.
  **Eközben egy önálló, a Qt6-migrációtól független logikai hibát is
  találtam**: az eredeti `isAvailable()` a `.isEmpty()` eredményét adta
  vissza *fordítva* (elérhetőnek jelezte, ha **nincs** hangeszköz, és
  nem-elérhetőnek, ha **van**) — ez a `settingsdialog.cpp`-ben a
  "Sounds" beállítás-csoportot tévesen inaktiválta olyan gépeken, ahol
  ténylegesen volt hangeszköz. Ez nem Qt6-fordítási hiba volt (mindkét
  irány lefordult volna), csak egy hibás futásidejű eredmény — a Qt6-os
  átírással egy menetben javítva.

Egy **teljes `lmc.cpp`/`main.cpp` átolvasás** (ezek a fájlok a build
sorrendjében közvetlenül az OpenSSL-linkelés utáni lépések — még nem
jutott el hozzájuk a felhasználó tényleges build-je, csak most, ebben a
folytatólagos audit körben derültek ki) az alábbi, szinte biztosan a
következő fordítási hibákat okozó problémákat találta és javította:

- ✅ `QList::toSet()`/`QSet::toList()` (`lmc.cpp`, két előfordulás:
  `init()` a parancssori argumentumok, `receiveAppMessage()` a
  single-instance IPC üzenetek deduplikálásánál) — ez a QList↔QSet
  konverziós kényelmi függvénypár **megszűnt Qt6-ban** (a konténer-
  könyvtár Qt6-os átalakításának része). Lecserélve a QSet/QList
  range-konstruktoraira: `QSet<QString>(lista.begin(), lista.end())`,
  majd vissza `QStringList(halmaz.begin(), halmaz.end())` — a duplikátum-
  szűrés viselkedése (sorrend nem garantált) változatlan.
- ✅ Hiányzó `#include <QSslSocket>` (`main.cpp`) — a
  `QSslSocket::supportsSsl()` hívás (egy induláskori "van-e SSL-
  támogatás" ellenőrzés) sehol máshol nincs használva/inklúdolva ebben a
  kódbázisban (a tényleges titkosítás `Core/src/crypto.cpp`-ban
  közvetlen OpenSSL-hívásokkal történik, `QTcpSocket` fölött, nem
  `QSslSocket`-en keresztül) — a hiányzó explicit include-ot
  valószínűleg egy másik fejléc transzitív include-ja fedte el Qt5
  alatt, Qt6 fejlécei viszont jóval kevésbé "szivárogtatnak" ilyet.

Emellett ez a munkamenet elvégezte a **Fázis 3** (Core kiemelése önálló
library-vé) érdemi részét is:

- ✅ [`/Core/Core.pro`](../Core/Core.pro) — a `/Core` mostantól egy önálló,
  `lmccore` nevű **statikus library** qmake-projekt (`TEMPLATE = lib`,
  `CONFIG += staticlib`), amit a `Windows/lmc/src/lmc.pro` most már
  linkel, nem pedig a forrásfájljait fordítja be közvetlenül.
- ✅ `Core.pro` szándékosan **nem** kér `QT += widgets`-et (sőt
  `QT -= gui`) — ellenőriztem, hogy a `/Core`-ban semmi nem használ
  QtGui/QtWidgets típust (`QColor`, `QFont`, `QPixmap`, `QWidget` stb.),
  **kivéve** a `settings.h`-ban lévő `IDS_FONT_VAL`/`IDS_COLOR_VAL`
  makrókat, amik `QApplication::font()`/`palette()`-et hívtak. Ezeket
  `#ifdef QT_WIDGETS_LIB`-fel körbevettem: a Windows kliens (ami
  `QT += widgets`-szel épül) változatlanul a `QApplication`-alapú
  alapértéket kapja, egy jövőbeli Widgets nélküli (QML) fogyasztó pedig
  üres stringet — ez teszi lehetővé, hogy a `/Core` ténylegesen
  Widgets-mentes maradjon anélkül, hogy a Windows-os viselkedés
  megváltozna.
- ✅ `settings.cpp`-ben az egyetlen közvetlen `QApplication`-hívás
  (`applicationFilePath()`, az `autostart` regisztrációs kulcsban) átírva
  `QCoreApplication`-re — ez a metódus ott van definiálva, a hívás nem is
  igényelt volna Widgets-et.
- ✅ Az OpenSSL keresési útvonal a repó gyökerébe költözött
  (`/openssl/include`, `/openssl/lib`, `Windows/openssl` helyett) — így
  mind a `Core.pro` (fordításhoz kellenek a fejlécek), mind a `lmc.pro`
  (a végleges linkeléshez kell a `libcrypto`) ugyanarra a helyre mutat.
- ✅ `build_windows.bat` frissítve: előbb a `Core`, utána a `lmcapp`, majd a
  `lmc` épül.

⏳ **Még nincs ellenőrizve valós build-bel** (ehhez a szandboxban nincs Qt6/
OpenSSL3 telepítve).

### Cross-platform (Windows ↔ Android) kompatibilitási audit

A felhasználó explicit kérésére ("fontos hogy működjenek együtt cross
platform-ba") átnéztem, hogy a Windows és Android kliens ténylegesen
együtt tud-e működni ugyanazon a hálózaton. Mivel mindkét platform
ugyanazt a `/Core` (`lmccore`) statikus library-t linkeli, a hálózati/
protokoll-réteg elvileg eleve közös — de ez két, valóban súlyos, közös
hibát nem véd ki:

- 🐞→✅ **`Helper::getOSName()` (`Core/src/shared.cpp`) nem fordult volna
  le Qt6 alatt** — a `QSysInfo::WindowsVersion`/`WV_*` és
  `QSysInfo::MacintoshVersion`/`MV_*` enumok **megszűntek Qt6-ban**. Ez
  nem "csak" egy hibás OS-név megjelenítés lett volna, hanem a teljes
  `/Core` library (tehát mindkét platform) build-jét megállította volna.
  Ráadásul Androidra (`Q_OS_ANDROID`) korábban egyáltalán nem volt ág,
  csak `Q_OS_X11`, ami Androidon soha nem igaz → `"Unknown"` esett volna
  ki. Lecserélve `QSysInfo::prettyProductName()`-re — ez az egyetlen
  hívás minden célplatformot (Windows, macOS, Linux, Android) helyesen
  lefed, platform-specifikus ág nélkül.
- 🐞→✅ **A Public/Group Chat funkció mindkét platformon, minden
  peer-re, örökre le volt tiltva.** A `chatroomwindow.cpp`
  `addUser()`-je (és az Android-oldali megfelelője,
  `messengerbridge.cpp`) szándékosan kizárja azokat a peereket, akiknek
  a verziója `<= 1.2.10` — ez az eredeti LAN Messenger azon régi
  kiadásait szűri ki, amik még nem támogatták a Public Chat-et. A
  probléma: ennek a fork-nak a saját `IDA_VERSION`-je
  (`Core/src/definitions.h`) **`"1.0.1"`** volt, ami *numerikusan
  kisebb*, mint `"1.2.10"` — tehát a `Helper::compareVersions()`
  minden egyes klienst (Windows-t **és** Android-ot egyaránt, hiszen
  ugyanazt a konstanst osztják meg) régi, nem-támogatott kliensként
  azonosított, és **mindig** kizárta a Public/Group Chatből — Windows↔
  Windows, Android↔Android és Windows↔Android esetén is, kivétel
  nélkül. Ugyanez a szám a `settings.cpp` beállítás-migrációs
  biztonsági ellenőrzését is elrontotta volna (a `IDA_VERSION < mentett
  verzió` esetén a teljes beállítás-fájlt törli, mert "jövőbeli
  formátumnak" véli) egy valódi régi telepítésről történő frissítéskor.
  **Javítás**: `IDA_VERSION` felemelve `"2.0.0"`-ra — ez biztosan
  nagyobb, mint a Core kódban ellenőrzött összes történeti küszöbérték
  (`1.2.10`, `1.2.25`, `1.2.28`, `1.2.30`), és egyben jelzi, hogy ez egy
  önálló, major fork verziószáma, nem az eredeti projekt folytatása. A
  verziószámot máshol is (telepítő szkriptek, `.rc` fájl,
  `AndroidManifest.xml`, `Android/src/main.cpp` egy külön, most már
  eltávolított duplikált literálja, dokumentáció) szinkronban
  frissítettem — ez a duplikáció volt pontosan az oka, hogy a szám
  egyáltalán szét tudott csúszni.

Ez a két hiba együtt azt jelentette, hogy jelenlegi állapotban (a
javítás előtt) a Public Chat/csoportos beszélgetés funkció **soha nem
működött volna** senkivel, semmilyen platform-kombinációban — miközben
a közvetlen (1-az-1) chat, fájlátvitel és discovery nem volt érintve
(azok nem mennek keresztül ezen a verzió-kapun).

### `lmcapp` hiányzó `CONFIG += staticlib` — az exe elindulna, de rögtön el is szállna

A `Windows/lmcapp/src/lmcapp.pro`-ban `TEMPLATE = lib` szerepelt, de
**nem** volt mellette `CONFIG += staticlib` (ellentétben a testvér
`Core/Core.pro`-val, ahol ez explicit ki van téve). A qmake
dokumentált alapértelmezése erre: `TEMPLATE = lib` `staticlib` nélkül
**megosztott library-t (DLL-t)** épít, nem statikus archívumot. Sem a
`build_windows.bat`, sem a `BUILD.md` `windeployqt`-lépése soha nem
másolt volna egy ilyen `lmcapp` DLL-t az `lmc.exe` mellé — tehát a
kliens **még a fejlesztő saját gépén is** hiányzó-DLL hibával szállt
volna el induláskor, közvetlenül egy egyébként "sikeres" build után.
**Javítás**: `CONFIG += staticlib` hozzáadva, ugyanúgy, ahogy a
`Core.pro` is teszi — ez egyben valószínűleg (bár ezt itt nem tudtam
ténylegesen leellenőrizni, mert nincs Qt6 ebben a szandboxban) meg is
szünteti a kimenet nevében lévő, korábban `move`-val kézzel kezelt "2"
verziószám-toldalékot is, mivel a statikus és dinamikus lib-ágak
eltérő qmake/mkspec kódúton mennek Windows alatt — az esetleges
átnevezési lépés a build szkriptekben/leírásban emiatt megmaradt
biztonsági hálóként (no-op, ha már nincs rá szükség).

### `Core`-ban felejtett `#include <QDesktopServices>` — a `Core` build meg sem indult volna

A felhasználó valós build-kísérlete a `Core` fordításánál (`datagram.o`,
`history.cpp`, `messagingproc.cpp`, `network.cpp` stb.) egységesen
`fatal error: QDesktopServices: No such file or directory` hibával
állt le, mindegyik a `trace.h` → `stdlocation.h` include-láncon
keresztül. **Ez nem is fordulhatott volna le** semmilyen Qt-verzióval:
a `Core.pro` szándékosan `QT -= gui`-t állít be (lásd fentebb, "Fázis
3" — a `/Core`-nak Widgets/GUI-mentesnek kell maradnia, hogy az Android
QML-kliens is tudja linkelni), a `QDesktopServices` viszont a QtGui
modulban van — enélkül a modul enélkül soha nem lett volna elérhető
ebben a fordítási egységben. Ellenőrizve: a `Core/src/stdlocation.h`-
ban és a `Core/src/history.cpp`-ban ez az include **sehol nem volt
ténylegesen használva** (nincs egyetlen `QDesktopServices::` hívás sem
egyik fájlban sem) — feleslegesen ottfelejtett, funkció nélküli sor
mindkét helyen. **Javítás**: mindkét include törölve, funkcionális
változás nélkül.

### `messaging.cpp`: `QString::append(QString*)` — sosem fordulhatott volna le

A `QDesktopServices`-hiba javítása után a felhasználó build-je egy
fordítási egységgel mélyebbre jutott, és egy új, **teljesen a Qt6-tól
független, eredeti kódbeli hibát** hozott fel:
`error: no matching function for call to 'QString::append(QString*&)'`
(`Core/src/messaging.cpp:260`, `lmcMessaging::createUserId()`-ben). A
kód egy `QString*` mutatót adott át közvetlenül `QString::append()`-nek
(`userId.append(lpszUserName)`), miközben a metódusnak nincs, és soha
nem is volt ilyen túlterhelt változata — ez egyszerűen **elfelejtett
dereferálás**. `git log`-gal ellenőrizve: ez a sor az eredeti,
legelső importált verzió óta változatlan, tehát nem ebben a
munkamenetben (sem a Qt6-migráció során) keletkezett hiba, hanem egy
mindig is jelen lévő, eredeti hiba, ami eddig egyszerűen sosem jutott
el a fordítóig (mert korábban minden build a `QDesktopServices`-nél
elakadt, mielőtt idáig ért volna). **Javítás**:
`userId.append(*lpszUserName)` — a mutató dereferálva.

### `lmc.pro`-ból hiányzott az OpenSSL `INCLUDEPATH` — `openssl/rand.h` hiba a `Core` javítása UTÁN is

A `Core` sikeres fordítása után a felhasználó build-je most már az
`lmc` (a tényleges Windows-kliens) projektnél állt le, **ugyanazzal**
a `fatal error: openssl/rand.h: No such file or directory` hibával,
amit korábban a `Core`-nál már megoldottunk (lásd az OpenSSL-elhelyezési
szakaszt fentebb) — ez most `main.cpp`-nél, majd `lmc.cpp`-nél jött elő,
annak ellenére, hogy az OpenSSL a helyén volt a repó gyökerében.

Az ok: a `Core/src/crypto.h` (`#include <openssl/rand.h>`) **nem
csak** a `Core.pro`-ból, hanem az `lmc.pro`-ból is transzitíven
be van húzva — `lmc.h` → `messaging.h` → `network.h`/`udpnetwork.h` →
`crypto.h` láncon keresztül, mert ezek a Core-fejlécek `#include
"crypto.h"`-t tartalmaznak. A `Core.pro`-nak megvan a saját
`INCLUDEPATH += $$PWD/../openssl/include` sora, ez azonban **csak a
`Core` fordítási egységeire vonatkozik** — a `lmccore` statikus
library-t linkelni (`-llmccore`) nem "örökölteti" automatikusan a
Core saját `INCLUDEPATH`-ját az `lmc.pro`-val, mert az két külön
qmake-projekt. Az `lmc.pro`-ban eddig csak a link-időben szükséges
`LIBS += -L.../openssl/lib -llibcrypto` sor szerepelt, a fordítási
időben szükséges `INCLUDEPATH` hiányzott — emiatt bármelyik `lmc`-beli
`.cpp` fájl, ami akár csak közvetve is eléri a `crypto.h`-t (ami
gyakorlatilag mindegyik, mivel a `messaging.h`/`network.h` szinte
mindenhonnan be van húzva), ugyanezzel a hibával állt volna le.
**Javítás**: `INCLUDEPATH += $$PWD/../../../openssl/include` (és a
hozzá tartozó `DEPENDPATH`) hozzáadva az `lmc.pro`-hoz, ugyanoda
mutatva, mint a már meglévő `LIBS`-sor.

### Négy további, valós Qt6-hiba az `lmc`-ben

A fenti javítás után a build tovább jutott, és négy újabb, klasszikus
Qt6-eltávolítási hibát hozott fel:

- ✅ `QTextStream::setCodec("UTF-8")` (3 előfordulás:
  `Core/src/settings.cpp`, `Windows/lmc/src/chatwindow.cpp`,
  `Windows/lmc/src/chatroomwindow.cpp` — mindhárom "beszélgetés
  mentése"/asztali parancsikon-írás művelet) — a `QTextCodec`-rendszer
  (és vele a `QTextStream::setCodec()`) kikerült a QtCore-ból Qt6-ban
  (az opcionális Qt5Compat modulba költözött); a `QTextStream` Qt6
  alatt mindig UTF-8-at használ, ami pontosan az volt, amit ez a sor
  amúgy is kért — egyszerűen törölve, funkcionális változás nélkül
  (a mellette lévő `setGenerateByteOrderMark()` hívás továbbra is
  érvényes Qt6 alatt, az megmaradt).
- ✅ `QPalette::foreground()` (`filemodelview.cpp`, a fájlátvitel-lista
  egyéni kirajzolásában) — ez már Qt5-ben is csak elavult alias volt a
  `windowText()`-hez, Qt6-ban véglegesen megszűnt → `windowText()`-re
  átírva.
- ✅ `qVariantFromValue(...)` (`filemodelview.cpp`, 2 előfordulás) — ez
  a szabad függvény már Qt5-ben is elavult volt a `QVariant::fromValue()`
  taghívás javára, Qt6-ban megszűnt → mindkét előfordulás
  `QVariant::fromValue(...)`-ra átírva.

### Két további hiányzó include (`messagelog.cpp`, `broadcastwindow.h`)

- ✅ Hiányzó `#include <QDesktopServices>` (`messagelog.cpp`, 3
  `QDesktopServices::openUrl(...)` hívás egy linkre kattintás
  kezelésében) — ugyanaz a mintázat, mint korábban a `main.cpp`-nél
  talált hiányzó `QSslSocket`-include: valószínűleg egy másik fejléc
  transzitív include-ja fedte el Qt5 alatt, Qt6 fejlécei viszont
  ezt jóval kevésbé "szivárogtatják".
- ✅ `invalid use of incomplete type 'class QActionGroup'` +
  ebből következően egy zavaróan hosszú, de másodlagos
  `connect(...)`-túlterhelés-feloldási hiba (`broadcastwindow.cpp`,
  `pFontGroup` tag) — Qt6-ban a `QAction`/`QActionGroup` a QtWidgets-ből
  a QtGui-ba költözött, és a `qaction.h` mostantól csak *előre
  deklarálja* a `QActionGroup`-ot, nem definiálja — a teljes típushoz
  explicit `#include <QActionGroup>` kell. A `mainwindow.h`-ban a
  hasonló `statusGroup` tagnál ez már helyesen szerepelt, csak a
  `broadcastwindow.h`-ból maradt ki — pótolva, ugyanúgy. (A `connect()`
  körüli hosszú túlterhelés-hiba csak a hiányzó definíció
  mellékhatása volt, nem önálló probléma — a fenti include-tól
  magától is eltűnik.)

### A futó program csak angolul jelent meg — a `.qm` fordítások soha nem lettek legenerálva/beágyazva

Első valós, sikeresen futó build után a felhasználó jelezte, hogy a 18
`.ts` fordítási forrás (`hu_HU.ts` is köztük) ellenére a program csak
angolul indult. Két, egymást erősítő okot találtam:

1. **`resource.qrc`-ben csak `en_US.qm` volt felsorolva** a `/lang`
   `qresource`-szekcióban — a másik 17 nyelv `.qm` fájlja, még ha
   létezett is a lemezen, **soha nem lett beágyazva** a végleges
   `.exe`-be, mert a Qt resource-rendszer csak azt ágyazza be, amit a
   `.qrc` explicit felsorol. Pótoltam mind a 18 bejegyzést.
2. **Semmi a build-folyamatban nem fordította le ténylegesen a `.ts`
   fájlokat `.qm`-mé.** A `TRANSLATIONS` lista a `lmc.pro`-ban önmagában
   **nem** vált ki automatikus `lrelease`-fordítást — ehhez explicit
   `CONFIG += lrelease` (vagy egy kézi `lrelease` hívás) kell, és
   egyik sem volt jelen. A `resources/lang/en_US.qm` csak azért
   létezett a repóban, mert valaki egy korábbi ponton kézzel
   lefuttatott egy `lrelease`-t rá (a fájl 23 bájtos — ami helyénvaló
   egy forrás-nyelvi, azaz önmagára "fordított" katalógusra, nem hiba
   jele) — a többi 17 nyelvhez **soha nem jött létre `.qm` fájl**, a
   `.qrc`-fix önmagában emiatt nem lett volna elég.

   **Első javítási kísérlet** (később visszavonva — lásd lent):
   `lmc.pro`-ba felvéve `CONFIG += lrelease` +
   `QM_FILES_OUTPUT_DIR = $$PWD/resources/lang` — ez a Qt saját
   Linguist Tools qmake-funkciója (a Qt telepítéssel együtt jön, nem
   igényel külön eszközt), aminek build közben minden `TRANSLATIONS`
   bejegyzést le kellett volna fordítania, a kimenetet pontosan oda
   irányítva, ahol a `resource.qrc` már eleve kereste őket.
   Szándékosan **nem** `CONFIG += embed_translations`-t használtam,
   mert az a Qt saját, automatikusan generált `:/i18n/` resource-ját
   hozná létre, ami ütközne/redundáns lenne az alkalmazás már meglévő,
   kézzel írt `:/lang` + `StdLocation::resLangDir()`/`sysLangDir()`/
   `userLangDir()` rendszerével (lásd `stdlocation.h`,
   `Windows/lmcapp/src/application.cpp`).

   ⚠️→🐞 Mivel ebben a szandboxban nincs elérhető Qt/`lrelease`, ezt
   **nem tudtam ténylegesen leellenőrizni** — és valós build-bel
   kiderült, hogy hibás volt: a `CONFIG += lrelease` automatikusan
   generált Makefile-szabálya **rossz relatív útvonalat** számolt ki a
   `.qm` fájlok helyére (`OUT_PWD`-hez képest, hibás "`../..`"
   mélységgel), ami
   `` :-1: error: No rule to make target '../../resources/lang/hu_HU.qm',
   needed by 'qrc_resource.cpp'. `` hibával állította le a build-et —
   pontosan azt a fajta, build-sorrendtől/relatív-útvonal-számítástól
   függő törékenységet igazolva, amit ez a figyelmeztetés eleve
   feltételezett, csak élesben, tényleges hibaként, nem csak
   elméletben. A `resource.qrc` saját, kézzel írt
   `resources/lang/XX_XX.qm` útvonalai ezzel szemben **soha nem voltak
   hibásak** — azok a `.qrc` fájl saját helyéhez képest oldódnak fel,
   nem `OUT_PWD`-hez (azaz shadow build-hez) képest, tehát nem
   ugyanattól a hibaforrástól függenek.

   **Második javítási kísérlet** (szintén visszavonva — lásd lent): a
   `CONFIG += lrelease`/`QM_FILES_OUTPUT_DIR` teljesen eltávolítva a
   `lmc.pro`-ból, helyette a `.qm` fájlokat egy explicit
   `lrelease`-hurok generálta, **a `qmake` lefutása előtt** — ez futott
   `build_windows.bat`-ban és a manuális parancssoros lépésekben,
   technikailag helyesen (a fájlok egyszerűen léteztek a helyükön, mire
   a `resource.qrc` beágyazásra került volna, nincs szükség semmilyen
   Makefile-szabályra közöttük).

   ⚠️→🐞 Ezt a lépést a Qt Creator-os és Visual Studio-s GUI-utakhoz is
   hozzáadtam — de a felhasználó jogosan szólt rá, hogy ez **ellentmond**
   annak, amit kifejezetten kért: egy **konzol nélküli** opcionális
   build-utat, és mégis egy terminálban futtatandó parancsra
   hivatkoztam mindkét GUI-s leírásban. Ez tervezési hiba volt, nem
   csak dokumentációs pontatlanság.

   **Végleges javítás**: a fordítás-generálás magába a `lmc.pro`
   fájlba építve, egy `system(lrelease ...)` hívással egy
   `for(ts_file, TRANSLATIONS) { ... }` cikluson belül — ez **minden
   `qmake`-lefutás mellékhatásaként automatikusan lefut**, amit Qt
   Creator és a Visual Studio/Qt VS Tools is magától elindít build
   előtt, tehát sem parancssorban, sem semmilyen IDE-ben nincs hozzá
   kézzel elvégzendő lépés — elég a megszokott Build/Run gombot
   megnyomni. Ez egyszerre kerüli el mindkét korábbi probléma okát: nem
   függ `lrelease.prf` törékeny, automatikusan generált
   Makefile-szabályától (a `system()` hívás abszolút útvonalakkal,
   explicit megadva fut le, nem qmake belső relatív-útvonal-számításától
   függően), és nem igényel kézi/terminálos lépést egyik build-úton sem
   — konzisztensen a `build_windows.bat`/manuális parancssoros útról is
   eltávolítottam a redundánssá vált explicit `lrelease`-hurkot,
   mostantól egyetlen, közös mechanizmus fedi le mind a négy build-utat
   (parancssor, szkript, Qt Creator, Visual Studio).

   ⚠️→🐞 A `system(lrelease ...)` hívás egy csupasz `lrelease`
   parancsnevet adott át, amit a `system()` által indított shell a
   folyamat `PATH`-ján keresztül oldott fel. Parancssorból és Qt
   Creator-ból ez működött, mert mindkettő felteszi a Qt `bin`
   mappáját a `PATH`-ra, mielőtt elindítaná a `qmake`-et. Valós
   Visual Studio + Qt VS Tools build viszont ismét elhasalt:
   `` [QtRunWork] .../rcc exited with code 1 `` — a Qt VS Tools
   `QtRunWork` MSBuild-feladata nem teszi fel ugyanígy a Qt `bin`
   mappáját a `PATH`-ra a `qmake` (és ezáltal a belőle induló
   `system()`-shell) számára, tehát a `lrelease` parancs
   megtalálhatatlan volt, a hívás csendben nem hozott létre semmilyen
   `.qm` fájlt a repóba már eleve bekerült `en_US.qm`-en kívül, és az
   `rcc` a `resource.qrc` másik 17, hiányzó `.qm` bejegyzésén bukott
   el — ugyanaz a tünet, mint a második kísérletnél, de más ok: nem
   hiányzó lépés, hanem `PATH`-függő parancsnév-feloldás.

   **Javítás**: a csupasz `lrelease` helyett `$$[QT_INSTALL_BINS]`-t
   használok — ez a qmake saját, éppen futó binárisának `bin`
   mappájára oldódik fel, tehát pontosan ugyanahhoz a Qt-telepítéshez
   tartozó `lrelease`-t hívja meg, teljesen függetlenül attól, hogy a
   `qmake`-et hívó folyamat (parancssor, Qt Creator, vagy a Qt VS
   Tools `QtRunWork` feladata) mit tett fel a saját `PATH`-jára.

## Magyar (hu_HU) fordítás

Hozzáadva [`lmc/src/hu_HU.ts`](lmc/src/hu_HU.ts) — a teljes UI mind a 284
egyedi forrásszövegét lefordítva (341 üzenet, 28 kontextus — pontosan
ugyanannyi, mint a meglévő `en_US.ts` forrás-katalógusban), a már meglévő 17
fordítás (`de_DE`, `fr_FR` stb.) formátumát és konvencióit követve. Bekötve
a `lmc.pro` `TRANSLATIONS` listájába — ettől kezdve minden normál
`qmake && make`/`nmake` build automatikusan lefordítja `.qm`-mé (`lrelease`),
és a nyelvválasztó (`SettingsDialog`) **kódmódosítás nélkül**, futásidőben
fel is veszi a listájára: a nyelvlista a `lang/` mappában talált `.qm`
fájlokból épül fel dinamikusan (`Application::loadTranslations()`,
`Windows/lmcapp/src/application.cpp`), a megjelenített névhez pedig
`QLocale::languageToString()`-ot hív a fájlnévből kiolvasott nyelvkódra —
tehát `hu_HU.qm` jelenléte esetén "Hungarian" néven automatikusan
megjelenik a listában, mint minden más nyelv esetén.

Emellett észrevettem és bekötöttem a `lmc.pro`-ba három, a lemezen már
meglévő, de a `TRANSLATIONS` listából korábban kimaradt fordítást is
(`ja_JP.ts`, `pl_PL.ts`, `sk_SK.ts`) — ezek eddig egyáltalán nem kerültek
be egyetlen buildbe sem, ez egy, a modernizációtól független, régebbi
mulasztás volt az eredeti projektben.

⚠️ **Nincs ellenőrizve valós `lupdate`/`lrelease`-szel** (ugyanaz a
korlátozás, mint a program többi részénél — nincs Qt ebben a
környezetben). A fordítás kézzel, az `en_US.ts` forrás-katalógus alapján
készült, XML-szinten ellenőrizve (jólformáltság, üzenetszám-egyezés az
`en_US.ts`-sel), de tényleges Qt Linguist-tel/futó alkalmazással nincs
kipróbálva.

⚠️ **A forrás-katalógus elavult a "LAN Messenger X" átnevezéshez képest**
— ez nem a hu_HU fordítás hibája, hanem egy már meglévő, minden nyelvi
fájlt érintő állapot: az `en_US.ts` (és a többi 17 nyelv) `<source>`
szövegei még a régi "LAN Messenger" nevet tartalmazzák néhány helyen (pl.
`lmcStrings` kontextus), mert senki nem futtatott `lupdate`-et az `X`
átnevezés óta. A hu_HU fordítás **szándékosan pontosan ugyanazokat a
forrásszövegeket** fordítja le, mint a többi 17 nyelv (a fordítási
infrastruktúra csak pontos szövegegyezésnél alkalmazza a fordítást), így
nem lóg ki a sorból — de ha valaki egyszer futtat egy valódi `lupdate`-et,
minden nyelv (a hu_HU is) frissítésre fog szorulni ott, ahol a forrásszöveg
változott.

## Szerzőség a Névjegyben

A Névjegy ("About") ablak "About" füle mostantól az eredeti `IDA_COPYRIGHT`
sor mellett explicit feltünteti az eredeti szerzőt (Dilip Radhakrishnan,
Qualia Digital Solutions) és ennek a verziónak a továbbfejlesztőjét
(galandras12), plusz egy kattintható linket a projekt GitHub oldalára
(`https://github.com/galandras12/LAN_MessengerX`) — lásd
`aboutdialog.cpp`/`.ui` és `Core/src/definitions.h`
(`IDA_ORIGINAL_AUTHOR`/`IDA_FORK_AUTHOR`/`IDA_REPOSITORY`). A "Súgó" menü
"LAN Messenger X online" akciója (`homePageAction_triggered()`,
`mainwindow.cpp`) is erre a linkre mutat mostantól — a `help.php`/
`faq.php`/`support.php` URL-ek (`IDA_DOMAIN`) továbbra is szándékosan
változatlanok maradtak, mivel azok az eredeti projekt saját, külön
infrastruktúrájára mutatnak, aminek nincs megfelelője ebben a repóban.

⚠️→✅ **Frissítés**: a felhasználó jelezte, hogy a "Frissítések
keresése" ("Check for Updates", `updateAction_triggered()`) menüpont
ténylegesen egy nem-létező/az eredeti projekthez tartozó célra mutatott
— ez a fenti "nincs megfelelője" döntés egyik konkrét, valós
következménye volt: a menüpont az `lmcUpdateWindow` ablakot nyitotta
meg, ami csendben egy `IDA_DOMAIN "/version"`
(`http://lanmessenger.github.io/version`) HTTP-kérést indított — ennek
a végpontnak nincs ehhez a fork-hoz tartozó, karbantartott
megfelelője, tehát a funkció soha nem tudott volna érdemben működni.
**Javítás**: `updateAction_triggered()` mostantól nem nyitja meg az
`lmcUpdateWindow`-t, hanem közvetlenül megnyitja ennek a repónak a
[Releases](https://github.com/galandras12/LAN_MessengerX/releases)
oldalát a böngészőben — ugyanaz a mintázat, mint a "LAN Messenger X
online" linknél. Az `lmcUpdateWindow`/`MT_Version`-alapú HTTP-check
gépezet (`Core/src/messagingproc.cpp`, `updatewindow.cpp`) érintetlenül
megmaradt a kódban, de a UI-ból mostantól sehonnan nem érhető el —
szándékosan nem lett teljesen kiszedve, nehogy egy nagyobb, kockázatosabb
refaktorálássá nőjön egy olyan változás, ami valójában csak egy
menüpont célját érinti.

## Telepítő: NSIS → Inno Setup

A régi `setup/win32/setup.nsi` (makensis) helyett most
[`setup/win32/setup.iss`](setup/win32/setup.iss) (Inno Setup 6) a
telepítő forrása — a régi `.nsi`/`setup.bat` referenciaként megmaradt a
mappában, nincs törölve. Az új szkript szakaszról szakaszra lekövetve
készült a régi viselkedése alapján (nem újratervezve), lásd az `.iss`
fájl fejléc-kommentjét a pontos indoklásért:

- ✅ Ugyanaz a telepítési hely (`Program Files\LAN Messenger X`),
  Start Menü parancsikonok (+ opcionális asztali ikon, amit a régi NSIS
  nem kínált — apró, szándékos többlet), admin jogosultság-kérés.
- ✅ **Windows Firewall kivétel**: a régi NSIS a harmadik féltől származó
  `nsisFirewall` plugint (`AddAuthorizedApplication`) használta; az új
  szkript beépített `netsh advfirewall firewall add rule` hívásokkal
  helyettesíti (be- és kimenő irányban is, mivel az
  `AddAuthorizedApplication` mindkettőt engedélyezte) — nincs
  plugin-függőség.
- ✅ Telepítés utáni `lmc.exe /silent /sync /quit` futtatás — pontosan
  ugyanaz, mint a régi szkript utolsó lépése (az indítópult-bejegyzés
  szinkronizálása a beállításokból).
- ✅ Eltávolításkor: az alkalmazás csendes bezárása (`/silent /term`),
  tűzfalszabály törlése, és **két Igen/Nem kérdés** ("Töröljem az
  üzenetelőzményt is?" / "Töröljem a mentett beállításokat is?") — a régi
  NSIS ezt egy egyedi `nsDialogs`-alapú checkbox-oldallal oldotta meg;
  az Inno Setup Pascal Scriptje nem ad ilyen egyszerűen újrahasználható
  egyedi wizard-oldal API-t az eltávolítóhoz, ezért itt két egyszerű
  `MsgBox`-kérdés adja ugyanazt a választási lehetőséget (kis
  UX-eltérés, ugyanaz a végeredmény: a törlési jelzőket ugyanúgy az
  alkalmazásnak magának adja át `/nohistory /nofilehistory`/`/noconfig`
  kapcsolókkal, nem a telepítő nyúl közvetlenül a fájlokhoz).
- ✅ A régi szkript két konkrét OpenSSL 1.0.2 DLL-t (`libeay32.dll`,
  `ssleay32.dll`) sorolt fel névre szólóan a `[Files]`-ban — ez a modern
  Qt6+OpenSSL3 build-nél **törékeny** lenne (más DLL-nevek, más Qt-modul
  DLL-ek), ezért az új szkript a teljes `windeployqt`-kimenetet
  helyettesíti be egy wildcard-os `Source: "{#SourceDir}\*"` sorral —
  lásd az `.iss` kommentjét a pontos build-előfeltételről (előbb
  `windeployqt`-t kell futtatni a Release `lmc.exe`-n).

### Amit ez **nem** old meg / nincs ellenőrizve

- **Nincs lefordítva/tesztelve valós Inno Setup fordítóval** — ebben a
  környezetben nincs `ISCC.exe` (sem `makensis`), tehát ez a szkript is
  csak kézi átvizsgálással, az Inno Setup dokumentált szintaxisa alapján
  készült, akárcsak a Core/Windows kód többi része. Az első tényleges
  build valószínűleg feltár apróbb hibákat (pl. útvonal-elgépelést).
- A régi NSIS-es fejléckép/banner (`header-r.bmp`, `banner.bmp`) nincs
  átvéve — az Inno Setup más pixelméretű wizard-képeket vár, ezekhez új,
  megfelelő méretű képek kellenének; kozmetikai hiányosság.
- MSIX csomagolás (a tervben eredetileg alternatívaként felmerült) nincs
  elkészítve — az Inno Setup-ra esett a választás, mert az közelebb áll a
  régi NSIS-szkript tényleges viselkedéséhez (egyedi telepítési útvonal,
  tűzfalszabály, egyedi eltávolítási logika), amit egy tiszta MSIX
  csomag natívan nem tesz lehetővé ugyanolyan egyszerűen.
- `[Registry]` szakasz csak a régi bookkeeping-kulcs megtartásáért van
  (semmilyen alkalmazáskód nem olvassa) — ellenőrizve, hogy
  `Windows/lmc/src` sehol nem olvas `HKLM\...\LAN Messenger X`-et.

## Build előfeltételek

- Qt 6 LTS — a forráskód immár nem használ olyan Qt5/Qt4-es API-t, ami Qt6
  alatt ismerten nem fordulna (lásd a fenti listát), de **ezt egy tényleges
  Qt6 + OpenSSL3 build-bel még nem ellenőriztük** (ez a szandbox-környezet
  nem tartalmaz Qt-t) — az első helyi build valószínűleg feltár még
  apróbb, itt észre nem vett hibákat is.
- OpenSSL 3.x fejlesztői csomag — az `include` és `lib` mappáit másold a
  repó gyökerébe: `openssl/include`, `openssl/lib` (az `openssl` mappa a
  `Core` és `Windows` melletti testvérmappa legyen). A Windows-os OpenSSL
  3.x disztribúciók általában `libcrypto.lib` néven adják az import
  library-t — ha a tiéd más néven csomagolja, igazítsd a
  `Windows/lmc/src/lmc.pro` végén lévő `LIBS +=` sort.
- Build sorrend: **előbb a `Core`** (`Core/Core.pro` → `lmccore` statikus
  library), utána a `lmcapp` alprojekt (egyedi single-instance könyvtár,
  lásd `PLATFORM_SPECIFIC.md`), végül a `lmc` (a tényleges Windows
  kliens). A `build_windows.bat` ezt a sorrendet automatizálja.

## Mappa-elrendezés

```
Windows/
├── lmc/src/       - a fő alkalmazás (UI: mainwindow, chatwindow, stb. + lmc.pro)
├── lmcapp/        - egyedi single-instance/singleapplication könyvtár
├── setup/         - telepítő-csomagoló szkriptek (win32/x11/mac);
│                    win32 alatt setup.iss (Inno Setup, aktuális) és a
│                    régi setup.nsi/setup.bat (referenciaként megtartva)
└── build_windows.bat
```

A hálózati/protokoll/titkosítási/előzmény kód a [`/Core`](../Core) mappában
van, önálló `lmccore` statikus library-ként épül, amit a `lmc.pro` linkel
(lásd [`Core/Core.pro`](../Core/Core.pro) és [`Core/README.md`](../Core/README.md)).
