# Build útmutató — Windows .exe és Android .apk

Ez az útmutató lépésről lépésre leírja, hogyan lehet ebből a
forráskódból egy telepíthető **Windows .exe telepítőt** és egy
**Android .apk-t** előállítani: milyen eszközöket kell hozzá telepíteni,
milyen sorrendben kell buildelni, és hogyan lesz a nyers build-eredményből
egy átadható, végleges program.

> ⚠️ **Ellenőrizetlenségi megjegyzés**: ez a repó nagyrészt egy olyan
> fejlesztői környezetben készült, ahol nem volt elérhető sem Qt, sem
> Android SDK/NDK, sem Inno Setup — az itt leírt lépések a projektfájlok
> (`Core.pro`, `lmc.pro`, `lmcapp.pro`, `Android.pro`, `setup.iss`,
> `build_windows.bat`) tartalma és a Qt/Android hivatalos build-folyamata
> alapján készültek, nem egy ténylegesen végigfuttatott build alapján.
> A Windows oldalt már **részben valós build igazolta vissza** (lásd a
> [`Windows/README.md`](Windows/README.md) "Valós build-bel talált és
> javított hibák" szakaszát) — az Android oldal **még senki által nem lett
> ténylegesen lefuttatva**, és van benne egy ismert, megoldatlan blokkoló
> (lásd lent). Ha egy lépés itt nem egyezik a valósággal, az elsődleges,
> megbízhatóbb forrás mindig a `Windows/README.md` és az
> `Android/README.md` — ez a fájl azok tartalmát gyűjti össze egy
> gyakorlati, sorrendi útmutatóvá.

## Tartalom

- [Mappa-elrendezés emlékeztető](#mappa-elrendezés-emlékeztető)
- [1. Windows .exe build](#1-windows-exe-build)
- [2. Android .apk build](#2-android-apk-build)
- [Gyakori hibák](#gyakori-hibák)

## Mappa-elrendezés emlékeztető

```
LAN_MessengerX/
├── Core/          - megosztott hálózati/protokoll/titkosítási réteg (lmccore statikus library)
├── Windows/       - Qt Widgets asztali kliens (lmc) + lmcapp (single-instance helper)
├── Android/       - Qt Quick/QML Android kliens
└── openssl/       - IDE HELYEZENDŐ, nincs a repóban (lásd lent)
```

Mindkét kliens a `Core`-ot linkeli be **statikus library-ként** — ezt
mindig **elsőként** kell lefordítani, a Windows/Android kliens buildje
előtt.

---

## 1. Windows .exe build

### 1.1 Szükséges összetevők

| Összetevő | Megjegyzés |
|---|---|
| **Qt 6 LTS** (pl. 6.8 vagy 6.9) | MinGW vagy MSVC kit — a `build_windows.bat` mindkettőt támogatja (`mingw64` / `msvc2022_64` paraméterrel) |
| **MinGW build esetén**: a Qt-hoz tartozó MinGW toolchain | A Qt Online Installer telepíti (`Qt\Tools\mingw...`) |
| **MSVC build esetén**: Visual Studio 2022 (Community elég) | A "Desktop development with C++" workload-dal |
| **OpenSSL 3.x fejlesztői csomag** (fejlécek + `.lib`) | **Nincs a repóban** — külön kell beszerezni, lásd 1.2 |
| **Inno Setup 6** (opcionális, csak a telepítő elkészítéséhez) | [jrsoftware.org](https://jrsoftware.org/isinfo.php) |

### 1.2 OpenSSL beszerzése és elhelyezése

A `crypto.cpp` közvetlenül OpenSSL-t hív (RSA/AES, a régi kliensekkel
való kompatibilitás miatt változatlan huzalprotokollal) — ehhez egy
Windows-os OpenSSL 3.x fejlesztői csomag kell (fejlécek + import
library).

1. Tölts le egy Windows-os OpenSSL 3.x disztribúciót a Qt kitedhez illő
   architektúrához (64-bit) — pl. a [slproweb.com Win64 OpenSSL
   csomagjai](https://slproweb.com/products/Win32OpenSSL.html) közül a
   **"Win64 OpenSSL … (nem light)"** verziót (a "Light" verzióból
   hiányozhatnak a fejlécek), vagy fordítsd le saját magad
   (`vcpkg install openssl:x64-windows` is egy lehetőség, ekkor a
   `vcpkg`-telepítés `include`/`lib` mappáit kell átmásolni/linkelni).
2. Hozd létre a repó gyökerében az `openssl` mappát, és másold bele úgy,
   hogy a végeredmény:
   ```
   LAN_MessengerX/
   └── openssl/
       ├── include/
       │   └── openssl/   (rand.h, rsa.h, pem.h, aes.h, evp.h, ...)
       └── lib/
           └── libcrypto.lib   (vagy más néven, lásd lent)
   ```
3. Ha a te disztribúciód az import library-t más néven adja (pl. a régebbi
   1.0.2-es csomagok `libeay32.lib` néven), igazítsd a
   `Windows/lmc/src/lmc.pro` legvégén lévő sort:
   ```
   win32: LIBS += -L$$PWD/../../../openssl/lib/ -llibcrypto
   ```
4. **Futásidőben** is kell a tényleges `libcrypto-3-x64.dll` (és
   `libssl-3-x64.dll`, ha a disztribúciód külön adja) — ezt a `lmc.exe`
   mellé kell majd másolni (lásd 1.4. lépés), a fenti `include`/`lib`
   csak a *fordításhoz* kell.

Enélkül a build az alábbi hibával fog leállni:
```
crypto.h:27: fatal error: openssl/rand.h: No such file or directory
```

### 1.3 Fordítás

**A) Automatikus szkripttel** (ajánlott):

```bat
cd Windows
build_windows.bat mingw64
REM  vagy:
build_windows.bat msvc2022_64
```

A szkript elején állítsd be a saját Qt-telepítésed elérési útját (a
`set PATH=C:\Qt\6.8.0\mingw_64\bin;...` sort) — ha nem `C:\Qt`-ba
telepítetted, vagy más verziót használsz, itt kell igazítani.

A szkript sorrendben lefordítja:
1. **`Core`** → `Core/lib/lmccore.a` (MinGW) vagy `lmccore.lib` (MSVC)
2. **`lmcapp`** → `Windows/lmcapp/lib/liblmcapp.a`/`lmcapp.lib`
3. **`lmc`** (a tényleges kliens) → `lmc.exe`

**B) Manuálisan**, ugyanezt a sorrendet betartva:

```bat
cd Core
qmake Core.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make

cd ..\Windows\lmcapp\src
qmake lmcapp.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make
REM MinGW-nél a kimenet neve "liblmcapp2.a" - át kell nevezni:
move ..\lib\liblmcapp2.a ..\lib\liblmcapp.a

cd ..\..\lmc\src
qmake lmc.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make
```

MSVC-vel ugyanez `nmake`-kel, `-spec win32-g++` nélkül (lásd a
`build_windows.bat` `:msvc2022_64` ágát).

A kész `lmc.exe` a `Windows\lmc\src\` mappában (vagy — Qt Creator-os
shadow build esetén — a Qt Creator által választott build-mappában)
jelenik meg. Ez önmagában **nem futtatható más gépen** — csak a te Qt/
OpenSSL DLL-jeiddel rendelkező fejlesztői gépeden, mert a Qt és OpenSSL
DLL-ek nincsenek mellette.

### 1.4 Futtatható, átadható mappa összeállítása (`windeployqt`)

Ez az a lépés, ami a nyers `lmc.exe`-ből egy **más gépen is futtatható**
mappát csinál:

1. Hozz létre egy üres mappát, pl. `Windows\build-release-deploy\`
   (ezt a nevet várja alapból a telepítő-szkript is, lásd 1.5).
2. Másold bele a frissen épített `lmc.exe`-t.
3. Futtasd rá a Qt saját `windeployqt` eszközét (a Qt `bin` mappájában
   van, pl. `C:\Qt\6.8.0\mingw_64\bin\windeployqt.exe`):
   ```bat
   windeployqt --release Windows\build-release-deploy\lmc.exe
   ```
   Ez bemásolja a szükséges `Qt6Core.dll`, `Qt6Gui.dll`, `Qt6Widgets.dll`,
   `Qt6Network.dll`, `Qt6Xml.dll`, a `platforms\qwindows.dll`-t és a
   többi futáshoz kellő Qt-plugint.
4. **Másold be kézzel az OpenSSL DLL-eket is** ugyanebbe a mappába
   (`libcrypto-3-x64.dll`, esetleg `libssl-3-x64.dll`) — a
   `windeployqt` ezekről nem tud, mivel nem Qt-modulok.
5. Másold be a `sounds`/`lang` mappákat is (ezeket a telepítő-szkript a
   forrásból tölti be automatikusan, de ha a mappát kézzel akarod
   tesztelni futtatás előtt, másold be őket a
   `Windows\lmc\src\resources\sounds` és `...\resources\lang`
   forrásokból).

Ellenőrzés: a `Windows\build-release-deploy\` mappa `lmc.exe`-je ekkor
egy másik, Qt-t nem tartalmazó Windows gépen is el kell, hogy induljon.

### 1.5 Telepítő készítése (Inno Setup)

A régi NSIS-alapú telepítő helyett most [Inno
Setup](https://jrsoftware.org/isinfo.php) 6-ot használ a projekt
(`Windows/setup/win32/setup.iss` — lásd a `Windows/README.md` "Telepítő:
NSIS → Inno Setup" szakaszát a részletekért).

1. Telepítsd az Inno Setup 6-ot.
2. Ha az 1.4. lépésben **nem** a `Windows\build-release-deploy\` nevet
   használtad, nyisd meg a `Windows\setup\win32\setup.iss`-t, és írd át
   a `SourceDir` alapértékét (vagy add meg fordításkor a
   `/DSourceDir=<a te mappád>` kapcsolóval).
3. Fordítsd le a telepítőt:
   ```bat
   cd Windows\setup\win32
   package-installer.bat
   ```
   (Ez az `ISCC.exe`-t hívja meg — ha máshova telepítetted az Inno
   Setup-ot, mint `C:\Program Files (x86)\Inno Setup 6\`, igazítsd az
   elérési utat a `.bat` fájlban.)
4. Az eredmény: `lanmessengerx-1.0.1-win32-setup.exe` a
   `Windows\setup\` mappában — **ez már egy önmagában átadható, kattints
   -és-települ telepítő**, amit bárkinek oda lehet adni.

Ha nincs szükséged telepítőre, csak egy hordozható mappára, a 1.4.
lépés eredménye (`Windows\build-release-deploy\`) önmagában is elég —
azt zippelve, bárhova kicsomagolva `lmc.exe`-vel elindítható.

---

## 2. Android .apk build

> ⚠️ **Ez az ág jelenleg blokkolva van** egy megoldatlan OpenSSL-Android
> linkelési problémán (lásd 2.2) — enélkül a `lmccore` Android célra
> valószínűleg nem fog linkelni. A többi lépés attól még helytálló, csak
> ez a rész igényel tőled egy kis utánajárást.

### 2.1 Szükséges összetevők

| Összetevő | Megjegyzés |
|---|---|
| **Qt 6 LTS Android kit** | Qt Online Installer / Qt Maintenance Tool → válaszd ki az "Android" komponenst a telepített Qt verzióhoz |
| **Android SDK** (parancssori eszközök + platform + build-tools) | A Qt Online Installer Android-kit telepítője ezt is felajánlja, vagy Android Studio-ból is telepíthető |
| **Android NDK** | A Qt adott verziójához **dokumentáltan illő** NDK-verziót kell használni (ellenőrizd a Qt telepítőben felajánlott/ajánlott NDK verziót a saját Qt verziódhoz — ez Qt-verziónként változik, ne feltételezz konkrét számot) |
| **JDK** (Java Development Kit) | Az Android Gradle Plugin/Qt Creator Android-varázslója jelzi, melyik JDK-major-verzió kell a te Qt/AGP kombinációdhoz |
| **Qt Creator** (ajánlott, nem kötelező) | Legegyszerűbb módja az Android kit beállításának és a build elindításának; parancssorból is megy (`qmake` + `androiddeployqt`), de Qt Creator sokkal kevesebb kézi konfigurációt igényel |
| **Android ABI-nkénti OpenSSL** | Lásd 2.2 — **ez a blokkoló** |

Az `AndroidManifest.xml` `minSdkVersion="24"`, `targetSdkVersion="34"` —
ezekhez illő SDK platform-csomagokat is telepítened kell az Android
SDK Manager-ben.

### 2.2 A blokkoló: OpenSSL Androidra

A `crypto.cpp` (a `/Core`-ban, mindkét kliens megosztja) közvetlenül
OpenSSL-t hív. A Windows build ehhez egy Windows-os `.lib`-et linkel — ez
**Androidon nem használható**. Androidhoz **ABI-nkénti** (`arm64-v8a`,
`armeabi-v7a`, `x86_64`, `x86`) keresztfordított `libcrypto.so`/`.a` kell.

Két reális út van ennek megoldására (egyiket sem végeztem el ebben a
munkamenetben, mert nem volt hozzá internet-elérésem/Android NDK-m):

**A) Közösségi előre fordított csomag** (gyorsabb, kevesebb munka):
   A Qt/Android közösségben elterjedt megoldás a
   [KDAB `android_openssl`](https://github.com/KDAB/android_openssl)
   (vagy hasonló, karbantartott) projekt — ez minden Android ABI-hoz ad
   kész `libcrypto.so`/`libssl.so`-t és a hozzájuk tartozó `.pri`
   include-fájlt, amit qmake-projektbe egy sornyi `include(...)`-tal be
   lehet húzni. Töltsd le/klónozd, és kövesd a saját README-jét az
   `Android.pro`-ba illesztéshez.

**B) OpenSSL saját fordítása Android NDK-val**: az OpenSSL hivatalos
   forrása tartalmaz Android cross-compile utasításokat (`Configure
   android-arm64`, `android-arm`, `android-x86_64`, `android-x86`,
   `ANDROID_NDK_ROOT` környezeti változóval) — ABI-nként külön kell
   lefuttatni, és a kimenetet a projekt saját mappastruktúrájába kell
   rendezni.

Amelyiket választod, a kimenetet helyezd el (vagy módosítsd az utat) úgy,
hogy illeszkedjen az `Android/Android.pro`-ban **már előkészített, jelenleg
kikommentezett** sorokhoz:

```qmake
# android: INCLUDEPATH += $$PWD/../openssl-android/include
# android: LIBS += -L$$PWD/../openssl-android/lib/$$ANDROID_TARGET_ARCH -lcrypto_$$ANDROID_TARGET_ARCH
```

Vedd ki a kommentet, és igazítsd az útvonalat/könyvtárnevet a ténylegesen
letöltött/fordított csomagodhoz (a fenti csak egy javasolt elrendezés,
nem egy elvárt fix útvonal).

### 2.3 A `Core` fordítása Android ABI-nként

A `Core.pro`-t **minden célzott ABI-ra külön** le kell fordítani, mielőtt
az `Android.pro` linkelni tudná — Qt Creator Android-kitje ezt
automatikusan megteszi, ha a `Core.pro`-t egy Android kit-tel nyitod meg
és build-eled (vagy ha az `Android.pro` egy `SUBDIRS` projektbe húzza be
a `Core.pro`-t — jelenleg **nem** így van bekötve, ez egy külön,
manuális lépés marad neked: nyisd meg/buildeld a `Core.pro`-t is az
Android kit(ek)kel, ugyanúgy, mint az `Android.pro`-t).

### 2.4 Az Android kliens fordítása

**Qt Creator-ral** (ajánlott):
1. Nyisd meg az `Android/Android.pro`-t Qt Creatorban.
2. Válaszd ki a projekt "Kit" fülén a telepített Android kit(ek)et (egy
   vagy több cél-ABI-hoz).
3. Futtasd le a Qt Creator "Add Android support" varázslóját, ha az
   `android/AndroidManifest.xml` metaadatai nem egyeznek pontosan a te
   telepített Qt verziód elvárásával (lásd a manifest tetején lévő
   megjegyzést) — ez felülírhatja/kiegészítheti a jelenlegi, kézzel írt
   manifestet.
4. Build → a Qt Creator elkészíti az `.apk`-t (alapból debug-aláírással,
   csak a saját gépeden/emulátorodon telepíthető állapotban).

**Parancssorból** (haladóbb, ha nem akarsz Qt Creatort használni):
```bash
cd Android
qmake Android.pro -spec android-clang ANDROID_ABIS="arm64-v8a"
make
androiddeployqt --input android-lmccore-deployment-settings.json \
                 --output android-build --release
```
(A pontos `androiddeployqt` hívás és a generált `.json` fájl neve
Qt-verziónként és kit-beállítástól függően változhat — ha bizonytalan
vagy, Qt Creator elvégzi ugyanezt kattintásra, és a "Compile Output"
panelen látod a pontos parancsokat, amiket lemásolhatsz saját szkriptbe.)

### 2.5 Az APK aláírása kiadásra

A Qt Creator/`androiddeployqt` alapból egy **debug-kulccsal** aláírt
APK-t készít — ez telepíthető közvetlenül eszközre (`adb install`), de
**nem alkalmas terjesztésre** (Play Store elutasítja, és minden újabb
debug-build más-más kulccsal aláírva "más alkalmazásnak" számít, ami
felülírásnál hibát ad).

Kiadásra szánt (release) APK-hoz:

1. Hozz létre egy saját aláíró kulcstárolót (**egyszer**, és őrizd meg
   biztonságosan — ha elveszik, soha többé nem tudsz frissítést kiadni
   ugyanahhoz a csomagnévhez):
   ```bash
   keytool -genkey -v -keystore lanmessengerx-release.keystore ^
       -alias lanmessengerx -keyalg RSA -keysize 2048 -validity 10000
   ```
2. Qt Creator Android build-beállításainál ("Build Settings" → "Build
   Android APK" → "Application Signature") add meg a keystore fájlt,
   jelszavát és az aliast — vagy parancssorból az
   `androiddeployqt --release --sign <keystore> <alias>` kapcsolókkal
   (a pontos kapcsolónevek Qt-verziónként kicsit eltérhetnek, `--help`-pel
   ellenőrizhető).
3. Az eredmény egy aláírt `.apk` (vagy célszerűbben `.aab`, ha Play
   Store-ba szánod) — ez telepíthető bármely, a `minSdkVersion`-nak
   megfelelő Android eszközre.

---

## Gyakori hibák

Ez a szakasz a projekt fejlesztése/auditálása során **már megtalált és
javított** hibákat sorolja fel — ha egy régebbi commit-ból dolgozol, és
ezekbe futsz bele, frissíts a legújabb `main`-re, mert ezek már meg
vannak oldva:

- `crypto.h:27: fatal error: openssl/rand.h: No such file or directory`
  → **nem kódhiba**, hiányzik az `openssl/include`+`openssl/lib` a repó
  gyökerében, lásd [1.2](#12-openssl-beszerzése-és-elhelyezése).
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

Ha ezeken túl más hibába ütközöl, nézd meg a
[`Windows/README.md`](Windows/README.md) és
[`Android/README.md`](Android/README.md) részletesebb, funkciónkénti
"Amit ez nem old meg" / "⚠️" szakaszait — lehet, hogy egy már ismert,
dokumentált korlátozásba futottál bele.
