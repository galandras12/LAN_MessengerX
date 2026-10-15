# Kiadások – 3.1.0

Változások: [`CHANGELOG.md`](CHANGELOG.md) (angolul, folyamatosan vezetve).

| Fájl | Mi | SHA-256 |
|---|---|---|
| `LANMessengerX-3.1.0.apk` | Android 8.0+ (min SDK 26, target 34), release, R8-zsugorított, v2/v3 aláírással | `628719411cddf30555fd46df317b09b6b078b020683fdb42b1b99d5971b66dd0` |
| `LanmessengerX-Windows.exe` | Windows 10/11 x64, önálló (self-contained, .NET nem kell hozzá), egyetlen fájl, ~46 MB | `9eae7d2fc0c06f29e0e19bef65835e624911efed49fe304c497bdcb8b282889f` |

Az APK az `AndroidClient/tools/build-apk.sh` kimenete (lásd [`BUILD.md`](../BUILD.md)). Az aláírókulcs a repóban van (`AndroidClient/signing`), tehát *nyilvános*:
saját terjesztéshez generálj újat. Az exe a `DesktopClient/publish.sh win-x64` / `publish-windows.bat` kimenete. A Linux csomagok (`.deb`, `.tar.gz`) és a Windows-telepítő nincsenek a repóban, a `publish.sh` készíti őket. Az exe **nincs kódaláírva**, ezért a Windows SmartScreen figyelmeztethet („További információ → Futtatás mindenképp"); első indításkor a tűzfal engedélyt kér – privát hálózaton engedélyezd.

**Ellenőrzöttség:** az exe Windowson még nem futott (ugyanaz a C# kód, amit Linuxon végig kipróbáltam); az APK csak fordítási/csomagolási szinten ellenőrzött, telefonon még nem futott – lásd `AndroidClient/README.md`.

**Kompatibilitás:** a protokolltesztek lefutottak az eredeti LAN Messenger **2.x** (Qt 6) ellen is, nem csak a korábbi 1.x kódbázis ellen; mindkét irányban, mindkét indulási sorrenddel és nem 50000-es TCP-porttal is (lásd `Protocol/PROTOCOL.md`, `tcpport`).
