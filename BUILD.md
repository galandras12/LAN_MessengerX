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
   mellé kell majd másolni (lásd 1.4. lépés). A fenti `include`/`lib`
   csak a *fordításhoz* kell, a DLL a *futtatáshoz* — és **nincs benne
   abban, amit eddig a repóba másoltál**, mert csak az `include`-ot és
   a `lib`-et kértük bemásolni, a DLL-ek egy harmadik, `bin` nevű
   mappában vannak, amit még nem érintettünk. Hogy pontosan hol, az
   attól függ, honnan szerezted az OpenSSL-t (lásd az 1. pontot):

   - **Előre csomagolt bináris disztribúció** (pl. egy telepítőt
     futtató "Win64 OpenSSL" jellegű csomag): a DLL-ek a telepítés
     gyökerében vagy egy `bin\` alkönyvtárában vannak, pl.
     `C:\Program Files\OpenSSL-Win64\` vagy
     `C:\Program Files\OpenSSL-Win64\bin\` — attól függően, melyik
     telepítőt használtad, nézd meg, hová telepített.
   - **Saját `nmake install`-lal fordítva**: a build a `Configure`-nek
     megadott `--prefix`-hez telepít, és ott **`bin\`, `lib\`,
     `include\` egymás melletti testvérmappák** — vagyis ha az
     `include`-ot és a `lib`-et onnan másoltad be a repó `openssl/`
     mappájába, ugyanannak a mappának a `bin\` alkönyvtárában vannak a
     DLL-ek is (nem a `lib\VC\x64\MD\` alatt, az csak az import
     library-ket/`.pdb`-ket tartalmazza).
   - **`vcpkg install openssl:x64-windows`**: a DLL-ek a
     `<vcpkg gyökere>\installed\x64-windows\bin\` mappában vannak.
   - **Ha egyik sem stimmel**, vagy nem emlékszel pontosan, hová
     telepítettél/build-eltél: keresd meg egy Windows-keresővel, vagy
     egy parancssorból (a diszk gyökeréből, vagy onnan, ahonnan az
     OpenSSL-t letöltötted/fordítottad):
     ```bat
     dir /s /b libcrypto-3-x64.dll
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
4. Az eredmény: `lanmessengerx-2.0.3-win32-setup.exe` a
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

### 2.6 Opcionális: parancssor nélkül, Qt Creator-ral és/vagy Android Studio-val

A 2.3–2.5. lépések (Core ABI-nkénti fordítása, az Android kliens
fordítása, aláírás) parancssor helyett teljes egészében GUI-ból is
elvégezhetők. A 2.4-ben ez már röviden szerepelt — itt egy részletesebb,
lépésről lépésre változat, plusz tisztázva, hol jön (ha jön) a képbe az
Android Studio.

⚠️ Emlékeztető: a [2.2-ben](#22-a-blokkoló-openssl-androidra) leírt
OpenSSL-Android blokkoló ettől a GUI-s úttól **függetlenül fennáll** —
GUI-ból ugyanúgy meg kell előbb oldanod, különben a `Core` Android
ABI-nkénti fordítása (1. lépés lent) linker-hibával elszáll.

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

Ha ezeken túl más hibába ütközöl, nézd meg a
[`Windows/README.md`](Windows/README.md) és
[`Android/README.md`](Android/README.md) részletesebb, funkciónkénti
"Amit ez nem old meg" / "⚠️" szakaszait — lehet, hogy egy már ismert,
dokumentált korlátozásba futottál bele.
