# Build útmutató

Három dolgot lehet építeni: az **Android APK-t**, a **Windows/Linux asztali klienst**, és lefuttatni a **teszteket**. Egyikhez sem kell Qt.

## 1. Android APK

### A) Gradle nélkül – `AndroidClient/tools/build-apk.sh` (Linux/WSL)

Ez az a lánc, amivel a `Releases/` alatti APK készült. Nem kell hozzá Android SDK, Android Studio vagy Google Maven – csak nyilvános források:

| Mi | Honnan |
|---|---|
| Android keretrendszer-osztályok (API 34, `android.jar` helyett) | Maven Central: `org.robolectric:android-all` (SHA-1 ellenőrzéssel) |
| R8 / D8 | `storage.googleapis.com/r8-releases` |
| Kotlin fordító | Maven Central |
| `aapt`, `apksigner`, `zipalign`, `zip`, JDK 17+ | disztribúciós csomagok (`apt install aapt apksigner zipalign zip openjdk-17-jdk-headless`) |

```bash
cd AndroidClient
./tools/setup-toolchain.sh        # egyszer: letölti az eszközöket ($LMX_TOOLCHAIN, alapból /opt/lmx-android)
./tools/build-apk.sh              # -> build/LANMessengerX-<verzió>.apk
```

A lépések: `aapt` (erőforrások + `R.java`) → Kotlin → **R8** (csak zsugorítás, nincs obfuszkálás) → `aapt` + `classes.dex` → `zipalign` → `apksigner`.
A verzió a `AndroidClient/version.properties`-ben van (`versionName`, `versionCode`).

**Aláírás.** A `signing/lanmessengerx-release.jks` a repóban van, jelszó: `lanmessengerx` (felülírható: `LMX_KEYSTORE`, `LMX_KS_PASS`, `LMX_KEY_ALIAS`).
Ez egy *nyilvános* kulcs, így bárki aláírhat vele – kényelmi megoldás, hogy az egymás utáni kiadások frissíthessék egymást. **Saját terjesztéshez generálj sajátot**
(`keytool -genkeypair …`), és ne told be a repóba.

### B) Android Studio / Gradle

Az `AndroidClient/` mappa megnyitható Android Studióban (`settings.gradle.kts`, `app/`, `core/`). Ugyanazok a forrásfájlok, csak az AGP építi őket.
**Ezt a Gradle-es utat a fejlesztői környezetben nem lehetett kipróbálni** (a Google Maven nem volt elérhető); a `build.gradle.kts` fájlok szokásos, rövid
AGP 8.x beállítások – ha a Studio újabb AGP-t javasol, fogadd el. Release APK: *Build → Generate Signed App Bundle / APK*, vagy `./gradlew :app:assembleRelease`.

## 2. Asztali kliens (Windows / Linux)

Kell: a **.NET 8 SDK**. (Futtatáshoz a kész, önálló kiadásnak nem kell .NET.)

```bash
cd DesktopClient
dotnet run --project src/LanMessengerX.App          # fejlesztői futtatás
```

**Windows kiadás:** `publish-windows.bat` (dupla kattintás; az ablak a végén nyitva marad) → `dist\win-x64\LanMessengerX.exe` (önálló, ~45 MB) és
`dist\LANMessengerX-win-x64.zip`. Ha az **Inno Setup 6** telepítve van, a script telepítőt is készít (`installer\LANMessengerX.iss`: Start menü, opcionális
asztali ikon/automatikus indítás, tűzfalszabály a 50000-es UDP/TCP portra privát hálózaton).

**Linux/macOS shellből:** `./publish.sh` (alapból `linux-x64` + `win-x64`), vagy `./publish.sh linux-arm64`. Eredmény a `dist/`-ben: `.tar.gz`, `.deb` (Debian/Ubuntu, `libnotify-bin` ajánlott az értesítésekhez), `.zip`.

Tálca-ikon és rendszerértesítés: Linuxon `notify-send` (libnotify), Windowson PowerShell-lel indított toast (jó szándékú próbálkozás; ha nem jelenik meg, a
programon belüli jelzések – olvasatlan-számláló, ablakcím – akkor is működnek).

## 3. Tesztek

```bash
# C#  (23 teszt)
cd DesktopClient && dotnet test

# Kotlin (16 teszt) – Gradle-lel:   cd AndroidClient && ./gradlew :core:test
# Gradle nélkül (JUnit jarok a Maven Centralból):
#   kotlinc a core/src/main + core/src/test fájlokra, majd
#   java -Dlmx.vectors=../Protocol/testvectors org.junit.runner.JUnitCore com.lanmessengerx.core.XmlTests …CryptoTests …ProtocolTests

# Interop az eredeti klienssel (Linux, root) – lásd Protocol/interop/README.md
sudo Protocol/interop/run-all.sh
```

A felület képernyőképei fejetlenül renderelhetők (valódi ablakok, bemutató adatokkal, világos és sötét témában):
`cd DesktopClient/tools/LanMessengerX.UiShots && dotnet run -c Release -- ../../shots`.
