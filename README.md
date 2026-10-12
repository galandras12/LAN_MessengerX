# LAN Messenger X

**Verzió:** 3.0.0 · **Licenc:** GNU GPLv3

Az eredeti, megszűnt támogatású **LAN Messenger** (Dilip Radhakrishnan / Qualia Digital Solutions) teljes újraírása **Qt nélkül**:
szerver és fiók nélküli csevegés és fájlküldés a helyi hálózaton, **az eredeti protokollal kompatibilisen** – az új kliensek és a régi LAN Messenger
kliensek ugyanazon a hálózaton látják egymást, és mindkét irányban tudnak üzenetet, fájlt, csoportos beszélgetést váltani.

| Kliens | Nyelv / UI | Mappa |
|---|---|---|
| **Android** (8.0+) | Kotlin, Material 3 (saját, AndroidX/Compose nélküli komponensekkel, Android 12+ dinamikus színekkel) | [`AndroidClient/`](AndroidClient) |
| **Windows** (10/11) és **Linux** | C# (.NET 8) + [Avalonia](https://avaloniaui.net) – egyetlen kódbázis, két célplatform | [`DesktopClient/`](DesktopClient) |
| Protokoll-leírás, tesztvektorok, interop tesztek | – | [`Protocol/`](Protocol) |

Funkciók mindhárom kliensben: automatikus felfedezés (UDP broadcast + multicast), 1:1 chat kézbesítési visszajelzéssel és „ír…” jelzéssel, csoportos
chat (meghívás, csatlakozás, kilépés), broadcast üzenet, fájlküldés/fogadás folyamatjelzéssel, profilkép, állapot (elérhető / foglalt / ne zavarj /
mindjárt jövök / távol / offline) és jegyzet, előzmények, értesítések. A mappaátvitelt a kliensek nem fogadják (udvariasan elutasítják, hogy a
küldő ne ragadjon be).

## Telepítés

* **Android:** a [`Releases/`](Releases) mappában lévő `LANMessengerX-3.0.0.apk` telepítése (ismeretlen forrásból telepítés engedélyezése kell).
* **Windows:** `DesktopClient\publish-windows.bat` (dupla kattintás) → `dist\win-x64\LanMessengerX.exe`, egyetlen, önálló fájl; opcionálisan telepítő is
  (Inno Setup). Első indításkor a Windows tűzfal engedélyt kér – privát hálózaton engedélyezd.
* **Linux:** `DesktopClient/publish.sh` → `dist/` alatt `.deb` és `.tar.gz`.

Részletek, fejlesztői build, tesztek: [`BUILD.md`](BUILD.md). Gyakori kérdések: [`FAQ.md`](FAQ.md).

## Hogyan lett ellenőrizve

* **Protokoll-kompatibilitás az eredeti klienssel – valódi forgalommal.** Az eredeti Qt-s klienst fej nélkül futtatva, két hálózati névtérben, mindkét új
  implementáció (Kotlin, C#) ellen lefutnak: felfedezés, kézfogás, szöveg (unicode, escape), nyugta, állapot, adatlap, fájl mindkét irányban (40 MB-ig,
  több száz ismétlés), profilkép és csoportos chat – lásd [`Protocol/interop`](Protocol/interop). A Kotlin ↔ C# páros is átmegy.
* **Egységtesztek** (Kotlin: 16, C#: 23), köztük OpenSSL-lel előállított kripto-tesztvektorok ([`Protocol/testvectors`](Protocol/testvectors)).
* **A C# asztali kliens** valódi, publikált Linux-bináris formájában, virtuális kijelzőn indítva, egérrel/billentyűzettel vezérelve működött az eredeti klienssel
  szemben (felfedezés, üzenet oda-vissza, állapotváltozás, fájl elfogadása); a felület képernyőképei fejetlen Skia-renderelésből ellenőrizve (világos/sötét téma).
* **Az Android alkalmazás:** a protokollmag a JVM-en az fenti módon tesztelt; az APK lefordul, R8-cal zsugorított, aláírt és az `aapt`/`apksigner` ellenőrzi.
  **Eszközön vagy emulátoron nem futott** (a fejlesztői környezetben nincs Android SDK/emulátor) – az első valódi telefonos próba még hátravan, lásd
  [`AndroidClient/README.md`](AndroidClient/README.md).
* A Windows-kiadás ugyanaz a C# kód, amit Linuxon végig kipróbáltam; a Windows-specifikus részek (tűzfal, toast értesítés, telepítő) Windowson nem futottak.

## Szerkezet

```
AndroidClient/   Kotlin mag (core/) + Android alkalmazás (app/) + build eszközök (tools/)
DesktopClient/   C# mag (src/LanMessengerX.Core) + Avalonia alkalmazás (src/LanMessengerX.App) + tesztek + csomagoló scriptek
Protocol/        PROTOCOL.md, tesztvektorok, interop tesztek (az eredeti klienssel szemben)
Releases/        kész, aláírt Android APK
legacy-qt/       a korábbi, Qt-s forráskód (Windows + Android) – már nem része a terméknek, törölhető
```

## Szerzőség / Credits

* **Eredeti LAN Messenger:** Dilip Radhakrishnan (Qualia Digital Solutions / QualiaTech), GPLv3.
* **LAN Messenger X** – modernizálás és továbbfejlesztés: [galandras12](https://github.com/galandras12/LAN_MessengerX).
