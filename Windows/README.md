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
