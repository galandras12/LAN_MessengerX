# Kiadások

| Fájl | Mi | SHA-256 |
|---|---|---|
| `LANMessengerX-3.0.0.apk` | Android 8.0+ (min SDK 26, target 34), release, R8-zsugorított, v2/v3 aláírással | `e75a60e7e508e42f7d430a7fe8a89ae8ac494e3d39eb76fa7524a520d5e7886b` |
| `LanmessengerX-Windows.exe` | Windows 10/11 x64, önálló (self-contained, .NET nem kell hozzá), egyetlen fájl, ~45 MB | `5a033ec257c53d45b5f0ee3d9b388aced7b6746e0bebaa49f31b7ca482c0ad50` |

Az APK az `AndroidClient/tools/build-apk.sh` kimenete (lásd [`BUILD.md`](../BUILD.md)). Az aláírókulcs a repóban van (`AndroidClient/signing`), tehát *nyilvános*:
saját terjesztéshez generálj újat. Az exe a `DesktopClient/publish.sh win-x64` / `publish-windows.bat` kimenete. A Linux csomagok (`.deb`, `.tar.gz`) és a Windows-telepítő nincsenek a repóban, a `publish.sh` készíti őket. Az exe **nincs kódaláírva**, ezért a Windows SmartScreen figyelmeztethet („További információ → Futtatás mindenképp"); első indításkor a tűzfal engedélyt kér – privát hálózaton engedélyezd.

**Ellenőrzöttség:** az exe Windowson még nem futott (ugyanaz a C# kód, amit Linuxon végig kipróbáltam); az APK csak fordítási/csomagolási szinten ellenőrzött, telefonon még nem futott – lásd `AndroidClient/README.md`.
