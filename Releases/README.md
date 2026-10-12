# Kiadások

| Fájl | Mi | SHA-256 |
|---|---|---|
| `LANMessengerX-3.0.0.apk` | Android 8.0+ (min SDK 26, target 34), release, R8-zsugorított, v2/v3 aláírással | `e75a60e7e508e42f7d430a7fe8a89ae8ac494e3d39eb76fa7524a520d5e7886b` |

Az APK az `AndroidClient/tools/build-apk.sh` kimenete (lásd [`BUILD.md`](../BUILD.md)). Az aláírókulcs a repóban van (`AndroidClient/signing`), tehát *nyilvános*:
saját terjesztéshez generálj újat. A Windows/Linux kiadásokat a `DesktopClient/publish-windows.bat` és `publish.sh` építi (nagyok, ezért nincsenek a repóban).

**Ellenőrzöttség:** az APK csak fordítási/csomagolási szinten ellenőrzött, telefonon még nem futott – lásd `AndroidClient/README.md`.
