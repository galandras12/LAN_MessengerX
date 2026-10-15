# Kiadások – 3.1.0

| Fájl | Mi | SHA-256 |
|---|---|---|
| `LANMessengerX-3.1.0.apk` | Android 8.0+ (min SDK 26, target 34), release, R8-zsugorított, v2/v3 aláírással | `b33c747b8a570d7200acf9f9a9b8f26324b96bc9436dfb5783df76f484e0d86a` |
| `LanmessengerX-Windows.exe` | Windows 10/11 x64, önálló (self-contained, .NET nem kell hozzá), egyetlen fájl, ~46 MB | `e2b0e4727058d9deb789b9536c01bffb6c98ce6c759d2ba5ed6f438c27258c60` |

Az APK az `AndroidClient/tools/build-apk.sh` kimenete (lásd [`BUILD.md`](../BUILD.md)). Az aláírókulcs a repóban van (`AndroidClient/signing`), tehát *nyilvános*:
saját terjesztéshez generálj újat. Az exe a `DesktopClient/publish.sh win-x64` / `publish-windows.bat` kimenete. A Linux csomagok (`.deb`, `.tar.gz`) és a Windows-telepítő nincsenek a repóban, a `publish.sh` készíti őket. Az exe **nincs kódaláírva**, ezért a Windows SmartScreen figyelmeztethet („További információ → Futtatás mindenképp"); első indításkor a tűzfal engedélyt kér – privát hálózaton engedélyezd.

**Ellenőrzöttség:** az exe Windowson még nem futott (ugyanaz a C# kód, amit Linuxon végig kipróbáltam); az APK csak fordítási/csomagolási szinten ellenőrzött, telefonon még nem futott – lásd `AndroidClient/README.md`.

**Kompatibilitás:** a protokolltesztek lefutottak az eredeti LAN Messenger **2.x** (Qt 6) ellen is, nem csak a korábbi 1.x kódbázis ellen; mindkét irányban, mindkét indulási sorrenddel és nem 50000-es TCP-porttal is (lásd `Protocol/PROTOCOL.md`, `tcpport`).
