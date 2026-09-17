# Gyakori hibák (FAQ)

Ez a fájl a [`BUILD.md`](BUILD.md) build-útmutató "Gyakori hibák"
szakaszának a helye — ide költözött, mert a lista a fejlesztés/valós
build-visszajelzések során akkorára nőtt, hogy megérdemelt egy önálló
fájlt a fő build-útmutatótól külön. A `BUILD.md`-ben lévő szám/betűjelű
hivatkozások (pl. `1.2`, `1.3`, `2.1`) erre a fájlra magára is
vonatkoznak — ha egy link ide vezetett, ez a szakasz.

Ez a szakasz a projekt fejlesztése/auditálása során **már megtalált és
javított** hibákat sorolja fel — ha egy régebbi commit-ból dolgozol, és
ezekbe futsz bele, frissíts a legújabb `main`-re, mert ezek már meg
vannak oldva:

- `crypto.h:27: fatal error: openssl/rand.h: No such file or directory`
  → **nem kódhiba**, hiányzik az `openssl/include`+`openssl/lib` a repó
  gyökerében, lásd [1.2](BUILD.md#12-openssl-beszerzése-és-elhelyezése).
- `invalid conversion from 'int' to 'Qt::WindowType'` → már javítva
  (`Qt::WindowFlags flags = 0` → `= Qt::WindowFlags()`).
- `'elidedText' was not declared in this scope` → már javítva
  (`usertreewidget.cpp`, tagfüggvény-hívásra átírva).
- `'SystemLocaleDate' is not a member of 'Qt'` → már javítva
  (`QLocale::system().toString(..., QLocale::ShortFormat)`-ra átírva).
- `QStandardPaths::DataLocation` fordítási hiba → már javítva
  (`QStandardPaths::AppLocalDataLocation`-re átírva).
- `QNetworkReply::error(QNetworkReply::NetworkError)` jel csendben nem
  kapcsolódik → már javítva (`errorOccurred`-re átnevezve).
- `'qmake' is not recognized as an internal or external command` (vagy
  ugyanez `mingw32-make`-re) a manuális (B) fordítási lépéseknél → **nem
  kódhiba**, a `PATH` nincs beállítva egy sima `cmd.exe`-ben. Lásd az
  [1.3](BUILD.md#13-fordítás) szakasz elején a `set PATH=...` sort — ezt minden
  új parancssor-ablakban le kell futtatni, mielőtt bármelyik `qmake`/
  `mingw32-make` parancsot kiadnád. Az A) automatikus szkript
  (`build_windows.bat`) ezt saját maga beállítja, ott nem kell vele
  külön foglalkozni.
- `'nmake' is not recognized as an internal or external command` (MSVC
  build esetén) → **nem kódhiba**, és **nem** ugyanaz a hiba, mint a
  fenti `qmake`-es — egy sima `set PATH=...` itt nem elég, mert az
  `nmake`/`cl.exe` a Visual Studio-val jön, nem a Qt-vel, és a
  fordításhoz/linkeléshez `INCLUDE`/`LIB` környezeti változók is
  kellenek, amiket csak a Visual Studio saját `vcvarsall.bat`-ja (vagy
  a "x64 Native Tools Command Prompt for VS 2022" parancsikon) állít
  be. Lásd az [1.3](BUILD.md#13-fordítás) szakasz MSVC-alszakaszát a pontos
  parancsért.
- `cannot find -llmcapp` (linker-hiba az `lmc.exe` fordításánál) → már
  javítva: a `lmcapp` könyvtár átnevezését (`liblmcapp2.a`/`lmcapp2.lib`
  → `liblmcapp.a`/`lmcapp.lib`) korábban egy nem létező `..\lib\`
  mappára hivatkozva próbálta végrehajtani mind a `build_windows.bat`,
  mind ez a leírás — az `if exist` csendben nem talált semmit, így az
  átnevezés lefutott ugyan hiba nélkül, de valójában semmit sem tett, és
  az `lmc.exe` linkelése ezért a régi, "2"-es nevű fájlt nem találta.
  Most már a helyes, aktuális könyvtárban (`Windows\lmcapp\src`) nevezi
  át — lásd [1.3](BUILD.md#13-fordítás).
- `fatal error: QDesktopServices: No such file or directory` a `Core`
  fordításánál (`stdlocation.h`-n vagy `history.cpp`-n keresztül,
  jellemzően több `.cpp` fájlban egyszerre, mert mindegyik a
  `trace.h`-n át húzza be) → már javítva: két felejtett,
  **ténylegesen sehol nem használt** `#include <QDesktopServices>` sor
  volt a `Core/src/stdlocation.h`-ban és a `Core/src/history.cpp`-ban.
  A `QDesktopServices` a QtGui modulban van, a `Core.pro` viszont
  szándékosan `QT -= gui`-t állít be (hogy az Android QML-kliens is
  linkelhesse) — ez a két sor emiatt soha nem is fordulhatott volna le,
  bármilyen Qt-verzióval. Mindkét include törölve, funkcionális
  változás nélkül.
- `no matching function for call to 'QString::append(QString*&)'`
  (`Core/src/messaging.cpp`, `createUserId()`) → már javítva — **nem
  Qt6-hiba**, egy eredeti, mindig is jelen lévő elgépelés
  (`userId.append(lpszUserName)` → `userId.append(*lpszUserName)`,
  hiányzó dereferálás). Csak azért nem került elő korábban, mert a
  build korábban a `QDesktopServices`-hibánál elakadt, mielőtt idáig
  ért volna.
- Qt Creator: `No executable configured in the custom run
  configuration` → **nem hiba**, csak a zöld "Run" (▶) gombot nyomtad
  meg egy library-projekten (`Core` vagy `lmcapp`) — ezeknek nincs mit
  futtatniuk, csak buildelni kell őket (kalapács ikon / Ctrl+B). Lásd
  az [1.6](BUILD.md#16-opcionális-parancssor-nélkül-qt-creator-ral-vagy-visual-studio-val)
  A) Qt Creator-os lépéseit.
- Visual Studio: `Unable to start program '...\lmccore.lib'... is not
  a valid Win32 application` → **nem hiba**, ugyanaz a jelenség, mint a
  fenti Qt Creator-os pont — a Solution "Startup Project"-je `Core`-ra
  (vagy `lmcapp`-ra) van állítva, ezek statikus library-k, nincs mit
  futtatni rajtuk. Solution Explorer-ben jobb klikk a `lmc` projekten →
  "Set as Startup Project", utána a Run/Debug (F5) már `lmc.exe`-t
  indítja. Lásd az [1.6](BUILD.md#16-opcionális-parancssor-nélkül-qt-creator-ral-vagy-visual-studio-val)
  B) Visual Studio-s lépéseit.
- Visual Studio/Qt Creator: `Unable to start program '...\lmc.exe'... A
  rendszer nem találja a megadott fájlt`, **egy `rcc exited with code 1`
  hibával együtt** az Error List-ben → **nem ugyanaz** a hiba, mint a
  fenti "not a valid Win32 application" — itt a `lmc.exe` fizikailag
  nem jött létre, mert a build maga elszállt a resource-compilálásnál,
  mert a `resource.qrc` 18 `.qm` fájlra hivatkozik, és az `rcc` hibával
  leáll, ha akár egy is hiányzik közülük. Már javítva: a `lmc.pro`
  korábban egy csupasz `lrelease` parancsnevet adott át a
  `system()`-nek, amit csak akkor talált meg, ha a `qmake`-et indító
  folyamat `PATH`-ja tartalmazta a Qt `bin` mappáját — parancssorból és
  Qt Creator-ból igen, de a Visual Studio-s Qt VS Tools `QtRunWork`
  build-feladatából nem, ezért ott a `lrelease`-hívás csendben nem
  hozott létre semmilyen `.qm` fájlt, és az `rcc` ezen bukott el
  (valós Visual Studio build-del megerősítve). A `.pro` fájl mostantól
  `$$[QT_INSTALL_BINS]/lrelease`-t hív a csupasz név helyett — ez
  mindig ugyanahhoz a Qt-telepítéshez tartozó `lrelease`-re oldódik
  fel, függetlenül attól, hogy melyik környezetből (parancssor, Qt
  Creator, Visual Studio) indult a `qmake`.
- `fatal error: openssl/rand.h: No such file or directory` az `lmc`
  (nem a `Core`) fordításánál, `main.cpp`-nél vagy `lmc.cpp`-nél, **annak
  ellenére, hogy az OpenSSL már a helyén van** és a `Core` már sikeresen
  lefordult → már javítva: az `lmc.pro`-ból hiányzott a saját
  `INCLUDEPATH` az `openssl/include`-hoz — a `Core.pro` saját
  `INCLUDEPATH`-ja nem "öröklődik át" az `lmc.pro`-ra csak azért, mert
  linkeli a `lmccore`-t, és az `lmc`-beli fájlok is transzitíven elérik
  a `crypto.h`-t. Lásd [1.2](BUILD.md#12-openssl-beszerzése-és-elhelyezése).
- `'class QTextStream' has no member named 'setCodec'` → már javítva —
  Qt6-ban a `QTextStream` mindig UTF-8, a `setCodec("UTF-8")` hívás
  feleslegessé vált, törölve.
- `'const class QPalette' has no member named 'foreground'` → már
  javítva, `windowText()`-re átírva.
- `'qVariantFromValue' was not declared in this scope` → már javítva,
  `QVariant::fromValue(...)`-ra átírva.
- `'QDesktopServices' has not been declared` (`messagelog.cpp`) → már
  javítva, hiányzó `#include <QDesktopServices>` pótolva.
- `invalid use of incomplete type 'class QActionGroup'`
  (`broadcastwindow.cpp`) — esetleg egy hosszú, zavaró
  `connect(...)`-túlterhelés-hibával együtt → már javítva, hiányzó
  `#include <QActionGroup>` pótolva a `broadcastwindow.h`-ban (Qt6-ban
  a `QAction`/`QActionGroup` a QtGui-ba költözött, és a fejléc
  mostantól csak előre deklarálja).
- **Csak angol nyelv jelenik meg futáskor**, annak ellenére, hogy 18
  `.ts` fordítás van a repóban → már javítva. Eredetileg két külön ok
  miatt volt hibás: (1) a `resource.qrc` `/lang` szakasza korábban
  **csak** `en_US.qm`-et sorolta fel — a többi 17 nyelv soha nem lett a
  végleges `.exe`-be beágyazva, még ha létezett is a `.qm` fájl,
  pótolva mind a 18 nyelvre; (2) semmi a build-folyamatban nem
  fordította le ténylegesen a `.ts` fájlokat `.qm`-mé — az `en_US.qm`
  csak azért létezett, mert egy korábbi, kézzel lefuttatott `lrelease`
  eredményeként be volt checkolva a repóba, a többi nyelvhez **soha
  nem is jött létre `.qm` fájl**. A végleges javítás: a `lmc.pro` maga
  fordítja le mind a 18 `.ts` fájlt `resources/lang/*.qm`-mé, egy
  `system($$[QT_INSTALL_BINS]/lrelease ...)` hívással, ami minden
  `qmake`-lefutáskor automatikusan lefut — sem parancssoron, sem Qt
  Creator-ban, sem Visual Studio-ban nincs hozzá külön, kézzel
  elvégzendő lépés, és nem függ attól, hogy az adott környezet PATH-ja
  tartalmazza-e a Qt `bin` mappáját. Korábban két másik megoldást is
  kipróbáltam, mindkettő okkal esett ki, és egy harmadik javítási
  kísérletnek (csupasz `lrelease` parancsnév) is volt egy valós
  Visual Studio-s hibája (lásd lent a következő pontot, illetve a
  fenti `rcc exited with code 1` pontot).
- `` :-1: error: No rule to make target '../../resources/lang/XX_XX.qm',
  needed by 'qrc_resource.cpp'.  Stop. `` → egy korábbi, azóta
  elvetett javítási kísérlet hibája volt (`lmc.pro`-ban
  `CONFIG += lrelease` + `QM_FILES_OUTPUT_DIR`, a Qt saját, erre szánt
  qmake-funkciója) — valós build-bel kiderült, hogy ennek a
  qmake-funkciónak az automatikusan generált Makefile-szabálya **rossz
  relatív útvonalat** számolt ki a `.qm` függőséghez (`OUT_PWD`-hez
  képest, hibás "`../..`" mélységgel), miközben a `resource.qrc` saját,
  kézzel írt `resources/lang/XX_XX.qm` útvonalai (amik a `.qrc` fájl
  saját helyéhez képest, nem `OUT_PWD`-hez képest oldódnak fel) sosem
  voltak hibásak. Egy második kísérlet (explicit `lrelease`-hurok
  parancssorból, `qmake` előtt) működött, de terminált igényelt Qt
  Creator-ban/Visual Studio-ban is, ami ellentmondott a konzol nélküli
  GUI-utak egész céljának. **A végleges megoldás** (lásd az előző
  pontot): a `.pro` fájlba írt `system(lrelease ...)` hívás — sem a
  törékeny, automatikusan generált Makefile-szabálytól nem függ, sem
  kézi lépést nem igényel.
- **A "Frissítések keresése" ("Check for Updates") menüpont egy nem
  létező (vagy az eredeti, megszűnt projekt) oldalára/repóba irányított
  a saját GitHub-repó helyett** → már javítva. A menüpont korábban egy
  csendes HTTP-lekérést indított az eredeti (nem ehhez a fork-hoz
  tartozó) `lanmessenger.github.io/version` végpontra — aminek nincs
  ehhez a fork-hoz tartozó, karbantartott megfelelője. Mostantól
  egyszerűen megnyitja ennek a repónak a
  [Releases](https://github.com/galandras12/LAN_MessengerX/releases)
  oldalát a böngészőben, ugyanúgy, ahogy a "LAN Messenger X online"
  link is teszi.
- `` #error "The requested API level higher than the configured API
  compatibility level" `` / `` #error "OPENSSL_API_COMPAT expresses an
  impossible API compatibility level" `` (`openssl/include/openssl/
  macros.h`-ból, `Core.pro` fordításánál) → már javítva. Egy korábbi,
  azóta visszavont javítási kísérlet (`Core.pro`-ban `DEFINES +=
  OPENSSL_API_COMPAT=<érték>`, a `crypto.cpp`-beli szándékosan
  megtartott, elavultnak jelölt RSA/PEM-API warningjainak némítására)
  egy nem létező OpenSSL verziószám-kódolást adott meg — az OpenSSL
  saját fejlécei ezt kemény hibával utasítják el, ami rosszabb, mint
  az eredeti warningok voltak. A végleges megoldás egy, kifejezetten a
  `crypto.cpp`-beli hívásokra szűkített fordító-pragma, ami build-hibát
  sosem tud okozni.
- Qt Creator: `Android Device Manager - Android support is not yet
  configured.` a Devices fül "Add... → Android Device → Start Wizard"
  lépésénél → **nem hiba**, csak azt jelzi, hogy a wizard előtt még be
  kell állítanod az SDKs (Qt Creator 20.0.1-ben) vagy Devices → Android
  (régebbi verziókban) fülön a JDK/Android SDK/NDK elérési útjait (a Qt
  Maintenance Tool "Android" komponense önmagában **nem** ad kész
  SDK-t, csak a Qt-könyvtárakat) — lásd a [2.1-es Android kit
  telepítés](BUILD.md#21-szükséges-összetevők) 6-7. lépését, különösen ha az
  "Android SDK location" mező üres vagy piros.
- Qt Creator SDKs/Android panel: minden zöld, **kivéve** `Android SDK
  Command-line Tools runs.` és `Android Platform SDK (version)
  installed.`, és/vagy build-időben `` Android build SDK version is
  not defined. Check Android settings. `` a `Core`/`Android` projekt
  fordításakor → **valós, megerősített** Qt Creator 20.0.1 ↔ Google
  legújabb, a klasszikus `sdkmanager`-t leváltó "Android CLI" eszköze
  közti inkompatibilitás, még akkor is, ha az SDK ténylegesen rendben
  van (a `sdkmanager.bat --version` kézzel lefuttatva működik, a
  platformok megvannak a lemezen). Megerősítetten működő javítás: telepíts
  egy **régebbi, számozott** "Android SDK Command-line Tools" revíziót
  (nem a legújabbat/"latest"-et) Android Studio SDK Manager-éből
  ("Show Package Details" bepipálva), majd kézzel cseréld a
  `cmdline-tools\latest` mappát erre a régebbire — lásd a [2.1-es
  Android kit telepítés](BUILD.md#21-szükséges-összetevők) 7. lépését a teljes
  menetért.
- Qt Creator SDKs/Android panel: `Android NDK list` üres, még akkor is,
  ha minden más zöld → nem hiba, csak nincs regisztrálva — Android
  Studio SDK Manager "SDK Tools" fülén pipáld ki az "NDK (Side by
  side)"-t, majd Qt Creator-ban "Add..."-tal tallózd be a települt NDK
  mappát — lásd a [2.1-es Android kit
  telepítés](BUILD.md#21-szükséges-összetevők) 7. lépését.
- `Android.pro` build: `` The API level set for the APK is less than
  the minimum required by the kit. The minimum API level required by
  the kit is 28. `` → már javítva. A Qt 6.11.2 Android kitje saját
  maga megkövetel egy minimum API-szintet (28), ami magasabb, mint az
  `AndroidManifest.xml`-ben korábban beállított `minSdkVersion="24"` —
  ez nem ennek az appnak a döntése volt, hanem a Qt toolchain saját
  alsó korlátja, valós build-bel megerősítve. Az
  `android/AndroidManifest.xml` `minSdkVersion`-je mostantól `"28"`. Ha
  egy újabb Qt-verzióval ismét hasonló hibát kapsz, emeld tovább
  ugyanígy — az error szövege mindig megmondja a pontos szükséges
  értéket.
- Qt Creator "Set Up SDK" gombja: a `cmdline-tools` telepítése
  sikeres, utána viszont `platform-tools`/`ndk`/`emulator`/
  `system-images`/`extras;google;usb_driver` mind `Failed`-del áll le,
  a `platform-tools`-nál konkrétan egy `java.nio.file.
  AccessDeniedException`-nel → **nem ennek a repónak a hibája**, hanem
  Google saját, a `sdkmanager`-t leváltó, új "Android CLI" nevű
  telepítő-eszközének egy valós Windows-os problémája. Ellenőrizd, hogy
  a választott SDK-mappa nincs-e felhő-szinkronizált mappában
  (OneDrive/Dropbox/stb. — gyakori ok), és hogy teljes írási jogod van
  rá; ha ez nem segít, kerüld meg a hibázó eszközt: telepítsd az
  Android Studio-t, és annak hagyományos SDK Manager-ével telepítsd a
  csomagokat, majd azt az SDK-mappát add meg Qt Creator-ban — lásd a
  [2.1-es Android kit telepítés](BUILD.md#21-szükséges-összetevők) 7. lépését.
- Android SDK Manager (akár Android Studio-é, akár Qt Creator-é)
  **ugyanazt a csomaglistát** (`build-tools`, `cmdline-tools`,
  `emulator`, `usb_driver`, `ndk`, `platform-tools`, `platforms`,
  `system-images`) **végtelen körben** újra és újra telepítésre
  ajánlja, minden "sikeres" telepítés után megint → az SDK mappája
  `C:\Program Files\...` alatt van. Windows saját UAC-fájlvédelme
  (virtualizáció) egy nem-rendszergazdai írást ilyenkor csendben egy
  rejtett `...\AppData\Local\VirtualStore\Program Files\...` másolatba
  irányít át a valódi hely helyett, így az SDK Manager UI (ami a valódi
  `Program Files`-beli mappát nézi) sosem látja a saját maga által írt
  fájlokat, és mindig hiányzónak gondolja őket. Javítás: költöztesd az
  SDK-t egy `Program Files`-en kívüli, sima mappába (pl. Android Studio
  saját alapértelmezettje, `...\AppData\Local\Android\Sdk`, vagy
  `C:\Android\Sdk`) — lásd a [2.1-es Android kit
  telepítés](BUILD.md#21-szükséges-összetevők) 7. lépését.
- `Android.pro` build: rengeteg, sok különböző `.cpp` fájlban jelentkező
  `error: templates must have C++ linkage` (jellemzően `qpair.h`,
  `qgenericatomic.h` és NDK-s `<atomic>`/`<optional>` fejlécekben, a
  hibaüzenet melletti jegyzet mindig egy `extern "C"` blokkra — a
  bionic `string.h` `__BEGIN_DECLS` makrójára — mutat vissza), utána
  `error: no template named '__cxx_atomic_base_impl'`,
  `error: unknown type name '__ptr_type'`, végül `fatal error: too many
  errors emitted, stopping now` → már javítva. **Nem elavult/piszkos
  build volt az ok** (bár egy tiszta rebuild is érdemes első lépésnek,
  ha ismeretlen hibába futsz) — a hibaüzenet include-lánca saját maga
  mutatta meg a valódi okot: az NDK bionic `string.h`-ja belülről
  `#include <strings.h>`-t (POSIX, "s" a végén) csinál, és mivel az
  `Android.pro` `INCLUDEPATH`-ja tartalmazza a `Core/src`-t, a
  fordító ezt a `Core/src/strings.h`-ra oldotta fel — egy, a repóban
  már régóta létező, a `lmcStrings` UI-szöveg osztályt tartalmazó
  fejlécre — az NDK saját, valódi `usr/include/strings.h`-ja helyett.
  Az így belehúzott Qt/C++ sablonkód a bionic `string.h` még nyitva
  lévő `extern "C" { ... }` blokkján belülre került, ami pontosan ezt a
  hibakaszkádot okozza. A Windows (MinGW/MSVC) build ugyanezt sosem
  látta, mert azok C futtatókönyvtárában nincs POSIX `strings.h`, így
  nem volt névütközés. Javítás: a `Core/src/strings.h`/`strings.cpp`
  átnevezve `lmcstrings.h`/`lmcstrings.cpp`-re (lásd
  `Core/src/lmcstrings.h` fejléc-kommentjét), minden `#include` és
  projektfájl (`Core.pro`, `Core.vcxproj`, `Core.vcxproj.filters`)
  frissítve az új névre.
- `Android.pro` build: `` crypto.h:27: fatal error: 'openssl/rand.h' file
  not found `` (`messengerbridge.cpp`/`main.cpp`/`moc_messengerbridge.cpp`
  fordításánál) → már javítva. A fenti `strings.h`-hiba után jelentkezett:
  a `crypto.cpp` maga a `Core.pro`-ban fordul, aminek megvolt a saját
  `openssl-android/include` `INCLUDEPATH`-ja — de az `Android.pro` saját
  forrásfájljai (`messengerbridge.cpp` stb.) a
  `messengerbridge.h → Core/messaging.h → network.h → udpnetwork.h →
  crypto.h` láncon át **szintén** behúzzák a `crypto.h`-t, és az
  `Android.pro`-nak saját, külön `INCLUDEPATH`-ja van — a `Core.pro`-ban
  beállított útvonal nem öröklődik át. Az `Android.pro` most már maga is
  tartalmazza az `openssl-android/include` utat.
- `Android.pro` build: `` androidforegroundservice.cpp:5: error:
  'QNativeInterface' file not found `` → már javítva. A `#include
  <QNativeInterface>` kényelmi fejlécet ez a Qt-telepítés nem generálja
  le erre a névtérre. Az első javítási kísérlet (`#include
  <QtCore/qnativeinterface.h>`) csak félig volt jó: a fordító utána már
  megtalálta magát a `QNativeInterface` névteret, de ``error: no member
  named 'QAndroidApplication' in namespace 'QNativeInterface'``-lel állt
  le, mert a `qnativeinterface.h` ebben a Qt-verzióban csak a
  platform-független tagokat deklarálja. Egy valós `findstr` kereséssel
  a ténylegesen települt fejlécek felett megerősítve: a
  `QNativeInterface::QAndroidApplication` a
  `QtCore/qcoreapplication_platform.h`-ban van — az `#include` erre
  átírva, és ez már a tényleges telepítésből ellenőrzött, nem tippelt
  útvonal.
- `Android.pro` build (figyelmeztetés, nem hiba): `main.cpp` két
  értesítés-lambdája `[-Wunused-lambda-capture]`-t adott a `[&app]`
  befogásra, mert egyik lambda törzse sem használja `app`-ot (csak
  statikus `QGuiApplication::applicationState()`-et és
  `AndroidForegroundService::showMessageNotification()`-t hív) → már
  javítva, `[&app]` → `[]`. A `&app` a `QObject::connect()` harmadik,
  kontextus-paraméterében (a kapcsolat élettartamához) továbbra is
  megvan és szükséges, csak a lambda saját befogási listájából tűnt el.
- `Android.pro` build: sikeres fordítás után `ld.lld: error: undefined
  symbol: ...` tucatjával, **a `Core` szinte összes osztályára**
  (`lmcMessaging`, `XmlMessage`, `History`, `lmcStrings`,
  `lmcSettingsBase`, `Helper` stb.) → már javítva. **Nem hiányzó/el nem
  készült `Core` build volt az ok** — a `Core\lib\` mappában a valós
  ellenőrzés szerint már ott volt egy friss `liblmccore_arm64-v8a.a`.
  A valódi ok: a Qt Android mkspec minden általa épített binárist
  automatikusan ABI-névvel lát el (`liblmccore_arm64-v8a.a`, nem
  `liblmccore.a`), hogy több ABI statikus libje elférjen egymás mellett
  a `Core.pro` egyetlen, ABI-független `DESTDIR`-jában — az
  `Android.pro` linker-sora viszont a csupasz `-llmccore`-t kereste,
  ami egy ottfelejtett, más platformról származó `liblmccore.a`-t talált
  meg a friss, helyes fájl helyett. Javítva: `-llmccore` → `-llmccore_
  $$ANDROID_TARGET_ARCH` az `Android/Android.pro`-ban.
- `Android.pro` build (a natív C++ fordítás/linkelés ezen a ponton már
  **teljesen sikeres** volt, a hiba a Gradle-alapú APK-csomagolásnál
  jött): `` Manifest merger failed: The <uses-sdk> tag was detected in
  your main AndroidManifest.xml file. ... no longer allowed for
  controlling SDK versions ... To fix: Remove <uses-sdk> from your
  AndroidManifest.xml. `` → már javítva. Ez egy valós, dokumentált
  Android Gradle Plugin 9.0+ viselkedésváltozás — onnantól kezdve a
  `minSdkVersion`/`targetSdkVersion` manifestből (`<uses-sdk>`) történő
  vezérlése egyszerűen tiltott, a Gradle manifest-merger hibával
  elutasítja. Az `android/AndroidManifest.xml`-ből eltávolítva a
  `<uses-sdk>` elem, a tényleges 28/34 értékek helyette az
  `Android.pro`-ban új `ANDROID_MIN_SDK_VERSION`/
  `ANDROID_TARGET_SDK_VERSION` qmake-változókban élnek — ezeket az
  `androiddeployqt` olvassa ki, és írja bele a generált
  `build.gradle`-be, a manifesttől függetlenül.
- `Android.pro` build (figyelmeztetés, nem hiba, ártalmatlan): ``
  Warning: QML import could not be resolved in any of the import
  paths: LanMessenger `` → **nem hiba**, nem is kódprobléma. A
  `LanMessenger` QML "modul" nem deklaratív (`qt_add_qml_module`/
  `QML_ELEMENT`), hanem klasszikus, futásidejű
  `qmlRegisterUncreatableType(...)` hívásokkal regisztrálódik
  (`Android/src/main.cpp`) — ezt a statikus `qmlimportscanner` (amit az
  `androiddeployqt` a becsomagolandó QML-modulok felderítésére futtat)
  nem tudja feloldani, mert nincs hozzá `qmldir` fájl. Futásidőben ez
  nem probléma, mert a C++ regisztráció a QML-motor indítása előtt
  lefut — ez a figyelmeztetés minden ilyen imperatív regisztrációjú
  Qt Quick projektnél megjelenik, ártalmatlan zaj.
- `Android.pro` build (figyelmeztetés, nem hiba, ártalmatlan): `` SDK
  processing. This version only understands SDK XML versions up to 3
  but an SDK XML file of version 4 was encountered. `` → **nem hiba**,
  ugyanaz a Qt Creator/Android CLI verzió-inkompatibilitás áll mögötte,
  mint a fenti, [2.1-es Android kit
  telepítés](BUILD.md#21-szükséges-összetevők) 7. lépésében leírt
  `cmdline-tools`-revízió témakör — a build ettől függetlenül lefut.
- `Android.pro` build (figyelmeztetés, nem hiba): `javac`
  `[deprecation]` figyelmeztetések a
  `MessengerForegroundService.java`-ban (`Builder(Context)`, `
  PRIORITY_HIGH`) → már javítva. Mivel a `minSdkVersion` immár `28`
  (lásd fent), a `Build.VERSION.SDK_INT >= Build.VERSION_CODES.O`
  ellenőrzés mindig igaz — az elavult, csatorna nélküli
  `Notification.Builder(Context)` ág és a `.setPriority()` hívás
  (amit a csatorna `IMPORTANCE_HIGH` értéke amúgy is felülír O+-on)
  soha nem futó, felesleges holt kód volt. Eltávolítva mindkét
  `buildNotification()`-szerű metódusból.
- `Android.pro` build (a manifest-merger-hiba a 2.0.10-es javítás után
  már nem jelentkezett — ez egy azutáni, új Gradle-hiba): `` Android
  resource linking failed ... ERROR: ... AndroidManifest.xml:52:5-83:19:
  AAPT: error: resource mipmap/ic_launcher (aka
  org.qualiatech.lanmessengerx:mipmap/ic_launcher) not found. `` → már
  javítva. A manifest mindig is `@mipmap/ic_launcher`-re hivatkozott, de
  az `Android/android/res/` mappában soha nem volt tényleges
  `mipmap-*/ic_launcher.png` fájl (a `res/` gyakorlatilag üres váz volt)
  — ez egy valódi, hiányzó erőforrás, nem build-állapot vagy
  konfigurációs hiba. Öt egyszerű, programmatikusan generált
  helyettesítő ikon hozzáadva minden szabvány denzitáshoz
  (`mipmap-mdpi/hdpi/xhdpi/xxhdpi/xxxhdpi`, 48–192 px) — lásd
  [`Android/README.md`](Android/README.md) a lecserélésükről valódi
  grafikára.
- `Android.pro` build (a hiányzó ikon miatti AAPT-hiba a 2.0.11-es
  javítás után már nem jelentkezett — ez egy azutáni, új Gradle-hiba):
  `` Execution failed for task ':packageDebug'. > android:extractNativeLibs
  is set to "true" in AndroidManifest.xml. Avoid setting
  android:extractNativeLibs="true" explicitly in AndroidManifest.xml,
  and instead set android.packagingOptions.jniLibs.useLegacyPackaging
  to true in the build script. `` → már javítva. Ugyanabba a családba
  tartozik, mint a `<uses-sdk>`-hiba: Android Gradle Plugin 9.0+ ezt a
  manifest-attribútumot is tiltja. Az `android:extractNativeLibs="true"`
  eltávolítva az `AndroidManifest.xml`-ből — **a tényleges viselkedés
  (a natív `.so`-k kicsomagolása lemezre, amire a Qt saját
  library-betöltése régóta támaszkodik) nem változott**, csak a
  manifestbeli, immár tiltott másolata tűnt el; ezt a Qt saját,
  `androiddeployqt` által generált `build.gradle`-je biztosítja
  tovább. ⚠️ Ha emiatt futásidőben natív library-betöltési hiba
  jelentkezne (ami arra utalna, hogy a Qt 6.11.2 saját sablonja *nem*
  állítja be a Gradle-oldali megfelelőt), jelezd — akkor egy saját,
  kézzel írt `Android/android/build.gradle`-re lenne szükség, ami
  felülírná a Qt beépített sablonját.

Ha ezeken túl más hibába ütközöl, nézd meg a
[`Windows/README.md`](Windows/README.md) és
[`Android/README.md`](Android/README.md) részletesebb, funkciónkénti
"Amit ez nem old meg" / "⚠️" szakaszait — lehet, hogy egy már ismert,
dokumentált korlátozásba futottál bele.
