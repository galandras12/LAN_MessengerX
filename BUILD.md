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
> ténylegesen lefuttatva**. A korábbi, dokumentált OpenSSL-Android
> blokkoló (lásd lent, 2.2) azóta el van hárítva egy vendorelt előre
> fordított csomaggal, de ez önmagában nem jelenti azt, hogy a többi
> Android-lépés (build-eszközök, aláírás, tényleges `.apk`-futtatás) is
> valós build-bel ellenőrizve lett. Ha egy lépés itt nem egyezik a valósággal, az elsődleges,
> megbízhatóbb forrás mindig a `Windows/README.md` és az
> `Android/README.md` — ez a fájl azok tartalmát gyűjti össze egy
> gyakorlati, sorrendi útmutatóvá.

## Tartalom

- [Mappa-elrendezés emlékeztető](#mappa-elrendezés-emlékeztető)
- [1. Windows .exe build](#1-windows-exe-build)
  - [1.6 Opcionális: parancssor nélkül, Qt Creator-ral vagy Visual Studio-val](#16-opcionális-parancssor-nélkül-qt-creator-ral-vagy-visual-studio-val)
- [2. Android .apk build](#2-android-apk-build)
  - [2.6 Opcionális: parancssor nélkül, Qt Creator-ral és/vagy Android Studio-val](#26-opcionális-parancssor-nélkül-qt-creator-ral-ésvagy-android-studio-val)
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
   architektúrához (64-bit), vagy fordítsd le saját magad forrásból
   `nmake`-kel (MSVC-hez ez az OpenSSL saját, hivatalos build-módja —
   ha így csináltad, a végeredmény mappaszerkezete a 2. pontban leírt
   `VC\x64\...` alstruktúrát fogja adni). `vcpkg install
   openssl:x64-windows` is egy lehetőség.
2. Hozd létre a repó gyökerében az `openssl` mappát, és másold bele az
   `include`-ot és a `lib`-et úgy, hogy a `crypto.h` megtalálja a
   fejléceket. Kétféle `lib`-elrendezéssel találkozhatsz a
   disztribúciódtól függően:

   **A) "Lapos" elrendezés** (pl. előre csomagolt bináris disztribúciók
   esetén gyakori) — `libcrypto.lib` közvetlenül a `lib` mappában:
   ```
   LAN_MessengerX/
   └── openssl/
       ├── include/
       │   └── openssl/   (rand.h, rsa.h, pem.h, aes.h, evp.h, ...)
       └── lib/
           └── libcrypto.lib
   ```
   Ez a `lmc.pro`-ban a nem-MSVC (pl. MinGW) ágon számít alapból.

   **B) `VC\x64\{MD,MDd,MT,MTd}\` elrendezés** — ha az OpenSSL-t saját
   magad fordítottad `nmake`-kel (vagy egy olyan disztribúciót
   használsz, ami ezt a hivatalos MSVC build-elrendezést követi), akkor
   a `lib` mappa **négy alkönyvtárat** tartalmaz, mindegyikben egy
   teljes, önálló `libcrypto.lib`-bel (+ sok más fájllal — `.pdb`,
   statikus `.lib`-ek stb.):

   | Alkönyvtár | Mit jelent |
   |---|---|
   | **`MD`** | Release, dinamikusan linkelt CRT (`/MD`) — **ezt használd** |
   | `MDd` | Debug, dinamikusan linkelt CRT (`/MDd`) — csak Debug build-hez |
   | `MT` | Release, statikusan linkelt CRT (`/MT`) — **nem ezt** |
   | `MTd` | Debug, statikusan linkelt CRT (`/MTd`) — **nem ezt** |

   A Qt saját MSVC kitjei (és ezért ez az app is) a CRT-t **dinamikusan**
   linkelik — ezért kell az `MD` (Release build-hez) / `MDd` (Debug
   build-hez), **nem** az `MT`/`MTd` pár: ha statikusan linkelt CRT-jű
   OpenSSL-t linkelnél egy dinamikus-CRT-s Qt build-hez, az vagy
   linker-hibát, vagy (rosszabb esetben) futásidőben nehezen
   diagnosztizálható, kettős-CRT-állapotú összeomlást okozna.

   Ezt az elrendezést **nem kell szétbontanod/átmásolnod** — a
   `Windows/lmc/src/lmc.pro` már fel van készítve rá: MSVC kit esetén
   automatikusan az `openssl/lib/VC/x64/MD` (Release) vagy
   `openssl/lib/VC/x64/MDd` (Debug) alkönyvtárból linkel, a
   `CONFIG(debug, debug|release)` ág alapján. Tehát ha ilyen
   elrendezésű OpenSSL-ed van, elég az `openssl/lib/VC/x64/...`
   mappákat a helyükön hagyni, nincs szükség kézi átmásolásra vagy a
   `.pro` módosítására.
3. Ha a te disztribúciód az import library-t más néven adja (pl. a régebbi
   1.0.2-es csomagok `libeay32.lib` néven), igazítsd a
   `Windows/lmc/src/lmc.pro` OpenSSL-szakaszának megfelelő `-llibcrypto`
   sorát/sorait a saját elrendezésed alapján.
4. **Futásidőben** is kell a tényleges `libcrypto-3-x64.dll` (és
   `libssl-3-x64.dll`, ha a disztribúciód külön adja) — ezt a `lmc.exe`
   mellé kell majd másolni (lásd 1.4. lépés). Ez **nincs benne abban,
   amit eddig a repóba másoltál** (a fenti `include`/`lib` csak a
   *fordításhoz* kell, ez a fájl a *futtatáshoz*) — de nem kell
   kitalálnod, pontosan hol van, egyszerűen megkeresheted:

   1. Nyiss egy parancssort (`cmd.exe`), és add ki ezt (cseréld ki a
      `C:\`-t arra a meghajtóra, ahova Windows-t telepítetted, ha nem
      `C:\`):
      ```bat
      dir /s /b C:\libcrypto-3-x64.dll
      ```
      Ez kiírja a fájl teljes elérési útját, pl.:
      ```
      C:\Program Files\OpenSSL-Win64\libcrypto-3-x64.dll
      ```
      (Egy kicsit eltarthat, mert az egész `C:\` meghajtót átnézi — ez
      normális.) Ha nincs találat, próbáld meg egy másik meghajtón is,
      pl. `dir /s /b D:\libcrypto-3-x64.dll`.
   2. Ugyanígy keresd meg a `libssl-3-x64.dll`-t is:
      ```bat
      dir /s /b C:\libssl-3-x64.dll
      ```
      (Ha erre nincs találat, az azt jelenti, hogy a te OpenSSL-ed nem
      adja külön fájlként — ilyenkor elég csak a `libcrypto`-t
      bemásolni, ez nem hiba.)
   3. A megtalált fájl(oka)t másold be oda, ahol a `lmc.exe` lesz (lásd
      1.4. lépés) — Intézőben egyszerű másolás-beillesztés, vagy
      parancssorból (a saját, 1. pontban kapott elérési utaddal):
      ```bat
      copy "C:\Program Files\OpenSSL-Win64\libcrypto-3-x64.dll" Windows\build-release-deploy\
      copy "C:\Program Files\OpenSSL-Win64\libssl-3-x64.dll" Windows\build-release-deploy\
      ```

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

⚠️ **Előbb be kell állítani a `PATH`-ot**, különben a `qmake`/
`mingw32-make` parancsok "`'qmake' is not recognized as an internal or
external command`" hibával elszállnak — ez **nem** kódhiba, egyszerűen
egy sima `cmd.exe`-ben (nem a Qt saját "Qt 6.x (MinGW ...)" Start
menü-parancsikonjával nyitott konzolban) a Windows nem tudja, hol
keresse ezeket. Nyiss egy sima parancssort a repó gyökerében, és — a
saját Qt-telepítésed elérési útjára igazítva — fusd le ezt **minden**
munkamenet elején, mielőtt bármelyik `qmake`/`mingw32-make` parancsot
kiadnád:

```bat
set PATH=C:\Qt\6.8.0\mingw_64\bin;C:\Qt\Tools\mingw1310_64\bin;%PATH%
```

(A második mappa, a `Qt\Tools\mingw...\bin`, a MinGW fordító/
`mingw32-make` miatt kell — csak a Qt saját `mingw_64\bin`-t PATH-ra
tenni nem elég, mert az csak a Qt-eszközöket tartalmazza, a fordítót
nem.) Ez pontosan az a sor, amit a `build_windows.bat` is beállít saját
magának — ha az A) opciót használod, ezzel nem kell külön foglalkoznod.

```bat
cd Core
qmake Core.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make

cd ..\Windows\lmcapp\src
qmake lmcapp.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make
REM MinGW-nél a kimenet neve "liblmcapp2.a" - át kell nevezni. Fontos: a
REM jelenlegi könyvtárban (.\src), NEM egy "..\lib" alatt - a lmcapp.pro
REM csak a unix ágon állít be DESTDIR-t, win32-n nem, és a Windows\lmcapp\lib
REM mappa nem is létezik a repóban:
move liblmcapp2.a liblmcapp.a

cd ..\..\lmc\src
qmake lmc.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make
```

(A fenti `qmake lmc.pro ...` lépés a fordításokat is automatikusan
lefordítja `.qm`-mé — ez a `lmc.pro` fájl saját, beépített lépése,
nincs hozzá külön teendőd, lásd lent, "Csak angol nyelv jelenik meg
futáskor".)

**MSVC-vel** ugyanez `nmake`-kel, `-spec win32-g++` nélkül — de itt egy
sima `PATH`-bővítés **nem elég** (ellentétben a MinGW-ággal fent):
`nmake`/`cl.exe` nem a Qt-vel jön, hanem a Visual Studio-val, és nem
csak `PATH`-ra van szükségük, hanem a fordítóhoz/linkeléshez kellő
`INCLUDE`/`LIB` környezeti változókra is — ezeket egyszerű `set PATH=`
sorral nem lehet pótolni. Két lehetőség:

1. **Nyisd meg a Start menüből** a Visual Studio-hoz tartozó "**x64
   Native Tools Command Prompt for VS 2022**" parancsikont (ez már egy
   kész, mindent beállított konzol) — **ebben** futtasd a `qmake`/
   `nmake` parancsokat, ne egy sima `cmd.exe`-ben.
2. Vagy egy sima `cmd.exe`-ben hívd meg kézzel ugyanazt, amit a
   `build_windows.bat` `:msvc2022_64` ága is tesz, **a `qmake`/`nmake`
   parancsok előtt**:
   ```bat
   set PATH=C:\Qt\6.8.0\msvc2022_64\bin;%PATH%
   call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" x64
   ```
   (A `Community`-t igazítsd, ha Professional/Enterprise kiadásod van;
   a `vcvarsall.bat` pontos elérési útja VS-verziónként eltérhet.)

Mindkét esetben csak **ugyanabban a konzol-ablakban**, a `vcvarsall.bat`
lefutása/a Native Tools parancsikon megnyitása **után** fognak működni
a `qmake Core.pro CONFIG+=...` / `nmake` parancsok — egy újranyitott
sima `cmd.exe`-ben ismét `'nmake' is not recognized...` hibát fogsz
kapni, mert ez a beállítás nem tartós, csak az adott konzol-munkamenetre
vonatkozik.

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
   `windeployqt` ezekről nem tud, mivel nem Qt-modulok. Ezek **nem**
   ugyanott vannak, ahonnan az `include`-ot/`lib`-et a repó `openssl/`
   mappájába másoltad az 1.2. lépésben (azok csak a *fordításhoz*
   kellenek) — hol keresd őket pontosan, lásd az [1.2. lépés 4.
   pontját](#12-openssl-beszerzése-és-elhelyezése).
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
4. Az eredmény: `lanmessengerx-2.0.7-win32-setup.exe` a
   `Windows\setup\` mappában — **ez már egy önmagában átadható, kattints
   -és-települ telepítő**, amit bárkinek oda lehet adni.

Ha nincs szükséged telepítőre, csak egy hordozható mappára, a 1.4.
lépés eredménye (`Windows\build-release-deploy\`) önmagában is elég —
azt zippelve, bárhova kicsomagolva `lmc.exe`-vel elindítható.

### 1.6 Opcionális: parancssor nélkül, Qt Creator-ral vagy Visual Studio-val

Az 1.3. lépés (Core → lmcapp → lmc fordítása) parancssor helyett egy
GUI-s IDE-ből is elvégezhető, ha nem szeretnél `cmd.exe`-t használni.
Mindkét alábbi út ugyanazt a három projektet építi föl, csak
kattintásokkal; az 1.4–1.5. lépések (`windeployqt`, telepítő) ezután
változatlanul, ugyanúgy következnek.

**A) Qt Creator-ral** (a legegyszerűbb, ez a Qt saját hivatalos IDE-je):

1. Telepítsd/nyisd meg a Qt Creatort (a Qt Online Installer/Maintenance
   Tool telepíti, a Qt-vel együtt általában már megvan).
2. **File → Open File or Project...** → válaszd ki a `Core/Core.pro`-t.
3. A megjelenő "Configure Project" képernyőn pipáld ki a kívánt Qt 6
   kit(ek)et (MinGW vagy MSVC) — ha egyik sincs felkínálva, előbb
   **Edit → Preferences → Kits** alatt kell lennie egy Qt 6-os kitnek
   (a Qt telepítő ezt általában automatikusan létrehozza).
4. **Fontos**: itt (vagy utólag a bal oldali "Projects" mód → "Build &
   Run" → a kit → "Build Settings" alatt) **kapcsold ki a "Shadow
   build" opciót**. Ez biztosítja, hogy a build kimenete pontosan oda
   kerüljön, ahol a `.pro` fájlok egymásra mutató relatív útvonalai
   (pl. hogy `lmc.pro` megtalálja a lefordított `lmcapp`-ot) számítanak
   rá — shadow build bekapcsolva hagyva a build lefuthat, de a lenti
   lépések csak kikapcsolt shadow build mellett garantáltan működnek.
5. Válaszd a bal alsó sarokban a **"Release"** build módot (ne
   "Debug"-ot, ha végleges .exe-t akarsz).
6. Kattints a bal oldali kalapács ikonra (Build Project), vagy
   **Ctrl+B**. Ez legyártja a `Core/lib/lmccore.a` (vagy `.lib`) fájlt.
   ⚠️ **Ne** a zöld "Run" (▶) gombot nyomd meg — a `Core` (és lent a
   `lmcapp` is) egy **statikus library**, nincs mit futtatni rajta.
   Run-ra kattintva Qt Creator "`No executable configured in the
   custom run configuration`" hibát ad, mert egy library-projekthez
   nincs is mit futtatnia — ez nem hiba a kódban, egyszerűen ezeken a
   projekteken csak a **Build** gombot használd. Csak a lenti, 8.
   pontban megnyitott `lmc.pro` egy tényleges futtatható program.
7. Ismételd meg ugyanezt a `Windows/lmcapp/src/lmcapp.pro`-val
   (ugyanaz a kit, Release mód, shadow build kikapcsolva).
   - Ha a kimenet neve `liblmcapp2.a`/`lmcapp2.lib` lett (nem
     `liblmcapp.a`/`lmcapp.lib`) — nyisd meg Intézőben a
     `Windows\lmcapp\src` mappát, és nevezd át kézzel. Ez egy ismert,
     dokumentált qmake-viselkedés (lásd a
     [Gyakori hibák](#gyakori-hibák) "cannot find -llmcapp" pontját) —
     előfordulhat, hogy nálad a friss `CONFIG += staticlib` javítás
     után már eleve a helyes néven jön létre, ekkor ezt a pontot
     hagyd ki.
8. Nyisd meg a `Windows/lmc/src/lmc.pro`-t Qt Creator-ban, ugyanígy
   (kit, Release, shadow build ki), és buildeld. A fordítások
   (`.ts` → `.qm`) automatikusan lefordulnak ennek a build-nek a
   részeként — a `lmc.pro` maga gondoskodik erről (`qmake` lefutásakor
   fut le, amit Qt Creator amúgy is automatikusan megtesz build előtt),
   nincs hozzá külön teendőd.
9. A kész `lmc.exe` a `Windows\lmc\src\` mappában jelenik meg (jobb
   klikk a projekten → "Open Containing Folder", vagy nézd meg a
   "Compile Output" panel alján kiírt pontos útvonalat).
10. Innentől ugyanaz az [1.4](#14-futtatható-átadható-mappa-összeállítása-windeployqt)
    (`windeployqt`) és [1.5](#15-telepítő-készítése-inno-setup) lépés
    következik, mint a parancssoros útnál — ezekhez nincs Qt Creator-
    beli GUI-gomb, a `windeployqt <exe útvonala>` parancsot egyszer
    még ki kell adni (vagy felvehető Qt Creator "Tools → External"
    egyéni eszközként, ha teljesen kattintás-only utat szeretnél).

**B) Visual Studio-val** (ha inkább azt használnád — csak MSVC kit-hez,
a **Qt Visual Studio Tools** bővítménnyel):

1. Visual Studio → **Extensions → Manage Extensions** → keress rá "Qt
   Visual Studio Tools"-ra, telepítsd, indítsd újra a Visual Studio-t.
2. **Extensions → Qt VS Tools → Qt Versions...** → "Add" gombbal add
   hozzá a telepített Qt verzió `qmake.exe`-jének útvonalát (pl.
   `C:\Qt\6.8.0\msvc2022_64\bin\qmake.exe`), adj neki egy nevet.
3. **Extensions → Qt VS Tools → Open Qt Project File (.pro)...** →
   válaszd ki a `Core/Core.pro`-t. Ez legenerál egy `.vcxproj`-t, és
   hozzáadja egy (új vagy meglévő) Solution-höz.
4. Ismételd meg a `Windows/lmcapp/src/lmcapp.pro` és
   `Windows/lmc/src/lmc.pro` fájlokkal — mindhármat **ugyanabba a
   Solution-be**.
5. Solution Explorer-ben jobb klikk a Solution-ön →
   **"Project Dependencies..."** → állítsd be, hogy `lmcapp` függjön a
   `Core`-tól, és `lmc` függjön mindkettőtől — enélkül Visual Studio
   rossz sorrendben próbálná linkelni őket.
6. A felső eszköztár konfiguráció-választójában állítsd **"Release" +
   "x64"**-re.
7. **Fontos**: Solution Explorer-ben jobb klikk a `lmc` projekten →
   **"Set as Startup Project"**. A Solution-höz elsőként hozzáadott
   projekt (jellemzően `Core`) lesz alapból a startup project — a
   `Core`/`lmcapp` viszont statikus library-k (nincs `main()`-jük),
   ha ezeken hagyva nyomod meg a Run/Debug (F5) gombot, Visual Studio
   megpróbálja "elindítani" a `.lib` fájlt, és
   **"Unable to start program '...\lmccore.lib'... is not a valid
   Win32 application"** hibát ad — ez nem hiba a kódban, ugyanaz a
   jelenség, mint a Qt Creator-os A) útnál a "No executable configured"
   hiba: ezeken a projekteken csak buildelni lehet, futtatni nem. Csak
   a `lmc` (a tényleges kliens) futtatható.
8. **Build → Build Solution** (Ctrl+Shift+B) — ez buildeli mindhárom
   projektet az 5. pontban beállított függőségi sorrendben. A
   fordítások (`.ts` → `.qm`) automatikusan lefordulnak ennek a
   build-nek a részeként (a `lmc.pro` maga gondoskodik erről, akkor is,
   amikor a Qt VS Tools a saját belső `qmake`-jét futtatja) — nincs
   hozzá külön teendőd. (A Run/Debug (F5) gomb a Startup Projectet
   buildeli **és** el is indítja — ha csak buildelni akarsz futtatás
   nélkül, a Build Solution a biztos választás.)
9. Az `lmc.exe` a Visual Studio saját kimeneti mappájában jön létre
   (jellemzően `Windows\lmc\src\x64\Release\` vagy hasonló — az
   "Output" panel alján pontosan kiírja).

⚠️ Ezt a B) Visual Studio-s utat **nem tudtam ténylegesen kipróbálni**
ebben a szandboxban (nincs Visual Studio/Qt VS Tools telepítve) — a
lépések a Qt VS Tools hivatalos dokumentációja és a projekt `.pro`
fájljainak ismerete alapján készültek. Ha valamelyik lépésnél eltérést
tapasztalsz, az A) Qt Creator-os út a jobban ellenőrzött/ajánlott
opció.

---

## 2. Android .apk build

### 2.1 Szükséges összetevők

| Összetevő | Megjegyzés |
|---|---|
| **Qt 6 LTS Android kit** | Lásd lent, a lépésről lépésre telepítést — ez **nem** települ fel automatikusan a Windows-os (MinGW/MSVC) kitekkel együtt, külön kell hozzá visszamenni a Qt telepítőbe |
| **Android SDK** (parancssori eszközök + platform + build-tools) | Lásd lent |
| **Android NDK** | A Qt adott verziójához **dokumentáltan illő** NDK-verziót kell használni (ellenőrizd a Qt telepítőben felajánlott/ajánlott NDK verziót a saját Qt verziódhoz — ez Qt-verziónként változik, ne feltételezz konkrét számot) |
| **JDK** (Java Development Kit) | Az Android Gradle Plugin/Qt Creator Android-varázslója jelzi, melyik JDK-major-verzió kell a te Qt/AGP kombinációdhoz |
| **Qt Creator** (ajánlott, nem kötelező) | Legegyszerűbb módja az Android kit beállításának és a build elindításának; parancssorból is megy (`qmake` + `androiddeployqt`), de Qt Creator sokkal kevesebb kézi konfigurációt igényel |
| **Android ABI-nkénti OpenSSL** | Lásd 2.2 — **már bekötve a repóba**, nincs hozzá teendőd |

Az `AndroidManifest.xml` `minSdkVersion="28"`, `targetSdkVersion="34"` —
ezekhez illő SDK platform-csomagokat is telepítened kell az Android
SDK Manager-ben. (A `minSdkVersion` eredetileg `24` volt, de egy valós
build-bel kiderült, hogy a Qt 6.11.2 Android kitje ennél magasabbat
követel meg — lásd a Gyakori hibák "API level set for the APK is less
than the minimum required by the kit" pontját.)

#### Az Android kit telepítése (ha eddig csak MinGW/MSVC van fent)

Ha a Qt Creator Kit-listájában eddig csak "Desktop Qt ... MinGW"/"...
MSVC..." szerepel, Android nem, az azért van, mert a Windows-os asztali
build **külön telepítési komponens** — a Qt Online Installer/Maintenance
Tool-lal eredetileg futtatott telepítés nem veszi fel automatikusan,
vissza kell menned hozzá:

1. **Nyisd meg a Qt Maintenance Tool-t** — a Qt telepítési mappádban van
   (pl. `C:\Qt\MaintenanceTool.exe`), nem ugyanaz, mint amivel eredetileg
   telepítettél (az a Qt Online Installer) — bár ha csak azt találod meg,
   az is felajánlja ugyanezt a komponens-kezelést.
2. Válaszd az **"Add or remove components"** (Összetevők hozzáadása/
   eltávolítása) opciót, Next.
3. A komponens-fában bontsd ki a már telepített Qt-verziódat (pl. "Qt" →
   "6.x.x"), és pipáld ki alatta az **"Android"** jelölőnégyzetet — ez
   telepíti a Qt for Android build-könyvtárakat minden ABI-hoz
   (`arm64-v8a`, `armeabi-v7a`, `x86`, `x86_64`).
4. Ugyanitt (a Qt-verziószám alatt, vagy egy külön "Developer and
   Designer Tools" ágban) keresd meg és pipáld ki, ha fel vannak kínálva:
   **"Android SDK Tools"**, **"Android NDK"**, **"OpenJDK"** — a pontos
   elnevezés és elérhetőség Qt telepítő-verziónként változhat.
5. Ha a Qt telepítő **nem** ajánlja fel az SDK/NDK/JDK komponenseket
   (előfordulhat régebbi vagy másképp csomagolt telepítőknél), telepítsd
   ezeket külön:
   - **Android Studio** — tartalmazza az SDK-t és egy beépített JDK-t is,
     a hivatalos [developer.android.com/studio](https://developer.android.com/studio)
     oldalról.
   - Az Android Studio SDK Manager-jéből telepítsd a te Qt-verziódhoz
     **dokumentáltan ajánlott NDK-verziót** (a Qt saját "Getting Started
     with Qt for Android" dokumentációja mondja meg pontosan, melyiket —
     ez Qt-verziónként változik, ne feltételezz konkrét számot).
6. Nyisd meg (vagy indítsd újra) a **Qt Creator-t**, és menj a
   Beállításokba: **Edit → Preferences** (újabb Qt Creator-verzióknál)
   vagy **Tools → Options** (régebbieknél). A bal oldali listában
   keress egy **"SDKs"** bejegyzést (Qt Creator 20.0.1-ben ez **külön**
   pont, nem a "Devices" alatt van — ha a tiéd más elrendezésű, keresd
   a "Devices → Android" fület helyette, régebbi verziókban ott
   szokott lenni), és azon belül az **"Android"** fület.
   ⚠️ **Ide menj be elsőnek** — **ne** a "Devices" fül "Add..."
   gombjával közvetlenül "Android Device"-ot indíts, mielőtt ez a
   panel zöld/kész nem lesz ("Android settings are OK."), mert az ``
   Android support is not yet configured. `` hibaüzenettel fog
   elszállni (valós Qt Creator 20.0.1-gyel megerősítve) — ez **nem**
   kódhiba, csak azt jelzi, hogy a wizard egy már beállított SDK-t vár,
   amit még nem adtál meg. A "Devices" fül egyébként is csak
   fizikai telefonok/emulátorok (AVD-k) listája — egy USB-n
   csatlakoztatott, hibakeresésre bekapcsolt fizikai telefonnak
   **magától** meg kell jelennie itt, ha az alábbi SDK-beállítás rendben
   van, nem kell hozzá az "Add..." varázsló.
7. Ezen a fülön töltsd ki (vagy ellenőrizd, hogy Qt Creator
   automatikusan megtalálta-e) a **JDK location**, **Android SDK
   location** és **Android NDK** mezőket.

   **Ha az "Android SDK location" mező üres, piros, vagy egyáltalán nem
   szerepel a listában** (ez okozza a fenti hibaüzenetet) — ez azt
   jelenti, hogy a Qt telepítő nem hozott létre neked kész SDK-t (lásd
   az 5. pontot), tehát ezt itt, Qt Creator-ban kell pótolnod:
   - **Ha van (vagy most telepítesz) Android Studio-t**: nyisd meg
     egyszer, hogy létrehozza a saját SDK-mappáját (Windows-on
     **alapból** valahol `...\AppData\Local\Android\Sdk` környékén —
     Android Studio-ban a Settings/Preferences → "Languages &
     Frameworks" → "Android SDK" mutatja a pontos elérési utat). **Ne
     írd felül ezt az alapértelmezett helyet** egy `C:\Program Files\`
     alatti mappára — lásd lent, miért. Ha megvan, ugyanezt a mappát
     tallózd be Qt Creator "Android SDK location" mezőjében.
   - **Ha nincs és nem is akarsz Android Studio-t telepíteni**: hozz
     létre egy üres mappát (pl. `C:\Android\Sdk`), és azt add meg
     "Android SDK location"-ként — Qt Creator (a 20.0.1 is) egy üres
     mappa esetén felajánlja a hiányzó parancssori eszközök
     (`cmdline-tools`) letöltését/telepítését ("Set Up SDK" gomb),
     utána pedig egy beépített Android SDK Manager panelen
     (checkbox-lista: SDK Platforms, SDK Tools, build-tools stb.) engedi
     kiválasztani és telepíteni a szükséges csomagokat egy "Apply"/
     "Install" gombbal — ehhez internetkapcsolat kell, de külön Android
     Studio nem.

     ⚠️ **Valós build-bel tapasztalt hiba**: a "Set Up SDK" gomb a
     `cmdline-tools`-t sikeresen telepíti, utána viszont a többi csomag
     (`platform-tools`, `ndk`, `emulator`, `system-images`,
     `extras;google;usb_driver`) mind `Failed`-del állhat le — a
     `platform-tools` esetén konkrétan egy `java.nio.file.
     AccessDeniedException` hibával, a Google saját, a `sdkmanager`-t
     leváltó, még új és nyilvánvalóan **kevésbé kiforrott "Android
     CLI" eszközében** (`com.android.cli.sdk...` a hibaüzenet
     verem-nyomkövetésében). Ez **nem ennek a repónak/a leírásnak a
     hibája**, hanem Google saját, Windows-on futó telepítő-eszközének
     egy valós, ebben a munkamenetben nem tovább diagnosztizálható
     problémája. Ha ezt kapod:
     1. Először ellenőrizd, hogy a választott SDK-mappa **nem**
        felhő-szinkronizált mappában van-e (OneDrive/Dropbox/Google
        Drive — ez klasszikus, gyakori oka pont az
        `AccessDeniedException`-nek, mert a szinkronizáló folyamat
        épp zárolja a frissen kicsomagolt fájlokat), és **semmiképp ne**
        legyen `C:\Program Files\` (se `Program Files (x86)`) alatt —
        lásd rögtön lent, miért **ez konkrétan** okoz egy másik,
        külön tünetet: végtelen újra-frissítési kört.
     2. Ha ez nem segít, **ne ezzel a beépített eszközzel küzdj
        tovább** — telepítsd inkább az **Android Studio-t**
        ([developer.android.com/studio](https://developer.android.com/studio)),
        és a benne lévő, jóval kiforrottabb, hagyományos SDK Manager-en
        keresztül telepítsd a platformot/platform-tools/build-tools/NDK
        csomagokat, majd — a fenti "Ha van Android Studio-d" pont
        szerint — azt az SDK-mappát add meg Qt Creator-ban. Ez
        megkerüli a hibázó új eszközt teljesen.

     ⚠️ **Ugyanígy valós build-bel tapasztalt, külön tünet**: ha az SDK
     mappáját (akár Android Studio telepítésekor, akár kézzel) egy
     `C:\Program Files\...` alá teszed, az SDK Manager (akár Android
     Studio-é, akár Qt Creator-é) **végtelen körben** akarja
     újratelepíteni **ugyanazokat** a csomagokat (`build-tools`,
     `cmdline-tools`, `emulator`, `usb_driver`, `ndk`, `platform-tools`,
     `platforms`, `system-images`) — a telepítés lefut, "sikerül", majd
     legközelebb megint ugyanezt a listát ajánlja fel, a végtelenségig.
     Ez **Windows saját UAC-fájlvédelme** miatt van: egy nem-rendszergazdai
     folyamat írása egy `Program Files` alá **nem a valódi helyre**
     kerül, hanem Windows csendben átirányítja egy rejtett
     `...\AppData\Local\VirtualStore\Program Files\...` másolatba (ez a
     "UAC virtualizáció" nevű, régóta létező Windows-kompatibilitási
     mechanizmus) — az SDK Manager UI viszont a *valódi* `Program
     Files`-beli mappát nézi vissza, ahol emiatt sosem látja a saját
     maga által (a virtualizált másolatba) írt fájlokat, ezért mindig
     "hiányzónak" gondolja ugyanazokat a csomagokat. **Az egyetlen
     megbízható javítás**: ne legyen az SDK mappája `Program Files`
     alatt — költöztesd (vagy telepítsd újra) egy sima, nem
     rendszer-védett helyre, pl. az Android Studio saját
     alapértelmezettjére (`...\AppData\Local\Android\Sdk`) vagy egy
     `C:\Android\Sdk`-hoz hasonló, gyökér-közeli mappára.
   - Legalább egy **platform** (az `AndroidManifest.xml` `minSdkVersion=
     "28"`/`targetSdkVersion="34"` alapján érdemes a 34-es platformot
     bepipálni), a **platform-tools**, és **build-tools** csomagokat
     mindenképp telepítsd.

   Az **"Android NDK list"** mezőt (valós Qt Creator 20.0.1-gyel
   megerősítve: ez gyakran **üresen** marad, még akkor is, ha minden
   más zöld) hasonlóan töltsd ki, ha a Qt Maintenance Tool 4. pontban
   telepített NDK-ja nem jelenik meg automatikusan:
   - Android Studio SDK Manager → "SDK Tools" fül → pipáld ki az
     **"NDK (Side by side)"**-t → Apply (telepíti pl. ide:
     `...\Sdk\ndk\<verziószám>\`).
   - Vissza Qt Creator-ban: az "Android NDK list" mellett **"Add..."**
     → tallózd be pontosan ezt a mappát.
   - Ez önmagában is javíthatja az alább leírt "All essential packages
     installed for all installed Qt versions." sort, mert az attól is
     függ, hogy van-e regisztrált NDK.

   ⚠️ **Valós build-bel megerősített, gyakori hibakép**: minden zöld
   **kivéve** ez a kettő:
   - `Android SDK Command-line Tools runs.` ✗
   - `Android Platform SDK (version) installed.` ✗

   — annak ellenére, hogy a `cmdline-tools\latest\bin\sdkmanager.bat`
   kézzel futtatva (`sdkmanager.bat --version`) **ténylegesen lefut** és
   ad vissza egy verziószámot, és a `platforms\` mappában valódi,
   szabályos platformok (pl. `android-34`, `android-36`) is megvannak.
   Ez **Qt Creator 20.0.1 és Google legújabb, a klasszikus
   `sdkmanager`-t leváltó "Android CLI" nevű eszköze közti valós
   inkompatibilitás** — Qt Creator nem ismeri fel/dolgozza fel ennek az
   új eszköznek a kimenetét, ezért folyamatosan "nem fut"/"nincs
   telepítve platform"-ként jelzi, holott az SDK ténylegesen rendben
   van. **Megerősítetten működő javítás**:
   1. Android Studio → Settings → Languages & Frameworks → Android SDK
      → **"SDK Tools"** fül → jobb alul pipáld ki **"Show Package
      Details"**-t.
   2. Bontsd ki az **"Android SDK Command-line Tools"** sort — több
      verziószám jelenik meg alatta (pl. `11.0`, `12.0`, `13.0`,
      esetleg `latest`). Pipáld ki telepítésre egy **régebbi, számozott**
      verziót (**ne** a legújabbat/"latest"-et — az hozza az
      inkompatibilis új eszközt) — egy `12.0` körüli revízió
      megerősítve működik. Apply — ez **külön** mappába települ
      (`cmdline-tools\<verziószám>\`), a jelenlegi `latest` mappát nem
      írja felül.
   3. Kézzel cseréld ki a mappákat, mert az eszközök kifejezetten a
      `latest` nevű mappát keresik: az Intézőben (`...\Sdk\
      cmdline-tools\`) nevezd át a jelenlegi `latest` mappát pl.
      `latest_old`-ra, majd az imént telepített, régebbi verziószámú
      mappát (pl. `12.0`) nevezd át **`latest`**-re.
   4. Zárd be és nyisd újra a Preferences ablakot (vagy csak az SDKs
      panelt) — ekkor mindkét pipa zöldre kell, hogy váltson, és a
      panel alján **"Android settings are OK. (SDK Version: 12.0)"**
      (vagy a te választott verziószámoddal) jelenik meg.

   Ez a két piros pipa közvetlenül okozza az `` Android build SDK
   version is not defined. Check Android settings. `` build-időben
   kapott hibát is (`Core`/`Android` projekt fordításakor) — ha ezt
   kaptad, ugyanez a javítás oldja meg.

   Zöld pipák/pipa-ikonok jelzik soronként, ha az adott mező helyesen
   van beállítva — csak akkor lépj tovább, ha mindegyik zöld.
8. Ha minden zöld: menj a **Kits** fülre. Itt egy vagy több új,
   automatikusan létrehozott **"Android Qt 6.x.x Clang \<abi\>"**
   kitnek kell megjelennie. Ha nem jelenik meg magától, kattints
   **"Add"** (Hozzáadás), és állítsd be kézzel (Qt version: a telepített
   Android Qt, Compiler: Android Clang, Device type: Android Device).
9. Csak **ezután** érdemes a Devices fülön "Add... → Android Device →
   Start Wizard"-dal egy konkrét emulátort/virtuális eszközt is
   létrehozni (ez opcionális — fizikai USB-n csatlakoztatott Android
   telefonnal fejlesztői opciók/USB-hibakeresés engedélyezése mellett is
   lehet tesztelni, emulátor nélkül).
10. Ha ez megvan, az `Android/Android.pro` (vagy `Core/Core.pro`)
    megnyitásakor a "Configure Project" képernyőn már megjelenik és
    kiválasztható ez az Android kit — innentől a [2.4](#24-az-android-kliens-fordítása)
    lépéstől folytatható a build.

### 2.2 OpenSSL Androidra — már bekötve

A `crypto.cpp` (a `/Core`-ban, mindkét kliens megosztja) közvetlenül
OpenSSL-t hív. A Windows build ehhez egy Windows-os `.lib`-et linkel — ez
**Androidon nem használható**, oda **ABI-nkénti** (`arm64-v8a`,
`armeabi-v7a`, `x86_64`, `x86`) keresztfordított `libcrypto.so`/`libssl.so`
kell.

Ez korábban ennek az útmutatónak egy dokumentált, megoldatlan blokkolója
volt. A [KDAB `android_openssl`](https://github.com/KDAB/android_openssl)
(`ssl_3` ág) közösségi előre fordított csomagja — a hozzá tartozó közös
fejléc-fává és mind a négy ABI (`arm64-v8a`, `armeabi-v7a`, `x86`,
`x86_64`) `.so`-jával — be van vezetve a repó gyökerébe, a
`openssl-android/` mappába (a Windows-os `/openssl/` mappával
ellentétben ez **be van csekkolva a git-be** — ~21 MB, elég kicsi hozzá,
és nincs egyszerű módja, hogy magad újra elő tudd állítani NDK/internet
nélkül). Sem az `Android.pro`, sem a `Core.pro` OpenSSL-bekötése nem
igényel tőled semmilyen kézi lépést vagy útvonal-igazítást — mindkettő
készen linkel/fordít a `openssl-android/`-ból.

Ha mégis a saját magad által fordított/frissebb csomagoddal akarod
lecserélni: a `Core.pro`-ban az `android: INCLUDEPATH +=`, az
`Android.pro`-ban az `android: LIBS +=`/`ANDROID_EXTRA_LIBS +=` sorok
mutatják, pontosan mit vár a build a `openssl-android/` alatt (közös
`include/`, és ABI-nkénti `libcrypto_3.so`/`libssl_3.so`) — cseréld le a
tartalmát ugyanerre a mappastruktúrára, a `.pro` fájlokat nem kell
módosítanod.

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

⚠️ Az alábbi `\` sortörés-jelek **Unix/bash-szintaxis** — egy sima
Windows `cmd.exe`-ben **nem** sortörésként, hanem szó szerint egy
külön, érvénytelen parancs kezdéseként értelmeződnek (pontosan ezt
kapod, ha bemásolod: az `androiddeployqt --input ...` és a `--output
...` két külön, mindkettő hibázó parancsként fut le). `cmd.exe`-ben a
sortörés jele a `^`, vagy egyszerűbb egy sorba írni az egészet — lent
mindkettőt mutatjuk. Emellett, ugyanúgy mint az 1.3-as Windows-lépésnél,
**előbb be kell tenned PATH-ra** a te konkrét Android Qt-kited saját
`bin` mappáját (ez **nem** ugyanaz, mint a MinGW/MSVC kit `bin`-je) —
a pontos mappanév Qt-verziónként/ABI-nként eltér (pl. valami
`C:\Qt\6.11.2\android_arm64_v8a\bin`-hez hasonló, de ezt a sajátodban
ellenőrizd, ne feltételezz konkrét nevet).

```bat
set PATH=C:\Qt\6.11.2\android_arm64_v8a\bin;%PATH%

cd Android
qmake Android.pro -spec android-clang ANDROID_ABIS="arm64-v8a"
make
androiddeployqt --input android-lmccore-deployment-settings.json --output android-build --release
```

**A legmegbízhatóbb módja ennek**, hogy ne kelljen a fenti PATH-ot és
`androiddeployqt`-hívást kitalálnod: építs **egyszer** Qt Creator-ral
(lásd fent), és a build lefutása után nézd meg a Qt Creator "Compile
Output" (vagy "Application Output") paneljét — ott, szó szerint,
karakterről karakterre látod a ténylegesen lefuttatott `qmake`/`make`/
`androiddeployqt` parancsokat (a pontos elérési utakkal, a generált
`.json` fájl valódi nevével), amit onnantól kimásolhatsz saját
szkriptbe — ez Qt-verziónként/kit-beállítástól változik, úgyhogy ez
megbízhatóbb, mint egy itt leírt, előre kitalált parancs.

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

### 2.6 Opcionális: parancssor nélkül, Qt Creator-ral és/vagy Android Studio-val

A 2.3–2.5. lépések (Core ABI-nkénti fordítása, az Android kliens
fordítása, aláírás) parancssor helyett teljes egészében GUI-ból is
elvégezhetők. A 2.4-ben ez már röviden szerepelt — itt egy részletesebb,
lépésről lépésre változat, plusz tisztázva, hol jön (ha jön) a képbe az
Android Studio.

ℹ️ A [2.2-ben](#22-openssl-androidra--már-bekötve) leírt Android OpenSSL
már be van kötve a repóba (`openssl-android/`) — a GUI-s úton sincs hozzá
külön teendőd, a `Core` Android ABI-nkénti fordítása (1. lépés lent)
készen megtalálja.

**Qt Creator-ral, lépésről lépésre:**

1. Nyisd meg a `Core/Core.pro`-t Qt Creator-ban, és a "Configure
   Project" képernyőn pipáld ki a telepített Android kit(ek)et (pl.
   "Android Qt 6.8.0 Clang arm64-v8a") — annyi ABI-t válassz ki, ahány
   architektúrára buildelni szeretnél (a Play Store-ba szánt kiadáshoz
   ma jellemzően legalább `arm64-v8a` + `armeabi-v7a` kell).
2. Válaszd a **Release** build módot, majd Build (Ctrl+B) — ez
   ABI-nként lefordítja a `lmccore`-t.
3. Nyisd meg az `Android/Android.pro`-t, **ugyanazokkal** az Android
   kit(ek)kel (a Configure Project képernyőn ugyanazokat pipáld ki,
   mint a Core-nál).
4. Ha az `android/AndroidManifest.xml` metaadatai nem egyeznek pontosan
   a telepített Qt verziód elvárásával (lásd a manifest tetején lévő
   megjegyzést), futtasd le a Qt Creator Android-varázslóját — jobb
   klikk a projekten a Projects panelen, vagy a Build beállítások
   "Android" szekciójában találod, Qt-verziónként kicsit eltérő helyen.
5. Válaszd a Release build módot, majd Build → a Qt Creator elkészíti
   a `.apk`-t (alapból **debug-aláírással** — lásd 6. pont a
   release-hez).
6. Az aláírt release APK-hoz: Build beállítások → "Build Android APK" →
   "Application Signature" fülön add meg a keystore fájlt, jelszavát és
   aliast (a keystore-t magát egyszer, a 2.5-ben leírt `keytool`
   paranccsal kell létrehozni — ehhez sajnos nincs GUI-s Qt Creator
   varázsló, ez az egyetlen konzol-parancs, ami ezen az úton is
   megmarad, de csak **egyszer** kell lefuttatni, utána a keystore fájl
   újra felhasználható).

**Az Android Studio szerepe (opcionális, kiegészítő — nem kötelező):**

Az Android Studio **nem tud közvetlenül Qt/QML C++ projektet
megnyitni/fordítani** — a tényleges C++ fordítást és a Gradle-projekt
generálását (`androiddeployqt`) a Qt Creator natív Android-integrációja
végzi. Az Android Studio szerepe emellett csak kiegészítő lehet:

- A Qt Creator/`androiddeployqt` által **már legenerált** Gradle-projekt
  (a build után az `Android/android-build/` mappában) megnyitható
  Android Studio-ban is — ez hasznos lehet, ha a saját, natívabb
  "**Build → Generate Signed Bundle / APK**" varázslóját szeretnéd
  használni az aláíráshoz a fenti 6. pont helyett, vagy ha a
  Gradle-beállításokat/`AndroidManifest.xml`-t egy ismertebb, natív
  Android-felületen szeretnéd átnézni/szerkeszteni fordítás után.
- Kényelmes GUI-eszköz lehet az **Android SDK/NDK/platform-csomagok
  telepítéséhez** is (SDK Manager), és egy virtuális eszköz (AVD)
  létrehozásához, ha emulátoron akarod tesztelni az elkészült APK-t —
  ezt a [2.1](#21-szükséges-összetevők) táblázat is említi.
- Magát a Qt/C++ **fordítást Android Studio-ból nem lehet elindítani** —
  ehhez mindenképp Qt Creator (vagy a 2.3–2.4-ben leírt parancssoros
  `qmake` + `androiddeployqt`) kell.

⚠️ Ezt az Android-oldali GUI-utat **szintén nem tudtam ténylegesen
kipróbálni** ebben a szandboxban (nincs Android SDK/NDK/Qt Creator/
Android Studio telepítve) — a lépések a Qt Creator és Android Studio
hivatalos, dokumentált Qt-for-Android munkafolyamata alapján készültek.

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
- `'qmake' is not recognized as an internal or external command` (vagy
  ugyanez `mingw32-make`-re) a manuális (B) fordítási lépéseknél → **nem
  kódhiba**, a `PATH` nincs beállítva egy sima `cmd.exe`-ben. Lásd az
  [1.3](#13-fordítás) szakasz elején a `set PATH=...` sort — ezt minden
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
  be. Lásd az [1.3](#13-fordítás) szakasz MSVC-alszakaszát a pontos
  parancsért.
- `cannot find -llmcapp` (linker-hiba az `lmc.exe` fordításánál) → már
  javítva: a `lmcapp` könyvtár átnevezését (`liblmcapp2.a`/`lmcapp2.lib`
  → `liblmcapp.a`/`lmcapp.lib`) korábban egy nem létező `..\lib\`
  mappára hivatkozva próbálta végrehajtani mind a `build_windows.bat`,
  mind ez a leírás — az `if exist` csendben nem talált semmit, így az
  átnevezés lefutott ugyan hiba nélkül, de valójában semmit sem tett, és
  az `lmc.exe` linkelése ezért a régi, "2"-es nevű fájlt nem találta.
  Most már a helyes, aktuális könyvtárban (`Windows\lmcapp\src`) nevezi
  át — lásd [1.3](#13-fordítás).
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
  az [1.6](#16-opcionális-parancssor-nélkül-qt-creator-ral-vagy-visual-studio-val)
  A) Qt Creator-os lépéseit.
- Visual Studio: `Unable to start program '...\lmccore.lib'... is not
  a valid Win32 application` → **nem hiba**, ugyanaz a jelenség, mint a
  fenti Qt Creator-os pont — a Solution "Startup Project"-je `Core`-ra
  (vagy `lmcapp`-ra) van állítva, ezek statikus library-k, nincs mit
  futtatni rajtuk. Solution Explorer-ben jobb klikk a `lmc` projekten →
  "Set as Startup Project", utána a Run/Debug (F5) már `lmc.exe`-t
  indítja. Lásd az [1.6](#16-opcionális-parancssor-nélkül-qt-creator-ral-vagy-visual-studio-val)
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
  a `crypto.h`-t. Lásd [1.2](#12-openssl-beszerzése-és-elhelyezése).
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
  telepítés](#21-szükséges-összetevők) 6-7. lépését, különösen ha az
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
  Android kit telepítés](#21-szükséges-összetevők) 7. lépését a teljes
  menetért.
- Qt Creator SDKs/Android panel: `Android NDK list` üres, még akkor is,
  ha minden más zöld → nem hiba, csak nincs regisztrálva — Android
  Studio SDK Manager "SDK Tools" fülén pipáld ki az "NDK (Side by
  side)"-t, majd Qt Creator-ban "Add..."-tal tallózd be a települt NDK
  mappát — lásd a [2.1-es Android kit
  telepítés](#21-szükséges-összetevők) 7. lépését.
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
  [2.1-es Android kit telepítés](#21-szükséges-összetevők) 7. lépését.
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
  telepítés](#21-szükséges-összetevők) 7. lépését.
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
  le erre a névtérre — átírva a mögötte álló, mindig létező valódi
  fejlécre: `#include <QtCore/qnativeinterface.h>`.

Ha ezeken túl más hibába ütközöl, nézd meg a
[`Windows/README.md`](Windows/README.md) és
[`Android/README.md`](Android/README.md) részletesebb, funkciónkénti
"Amit ez nem old meg" / "⚠️" szakaszait — lehet, hogy egy már ismert,
dokumentált korlátozásba futottál bele.
