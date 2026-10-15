# LAN Messenger X – Android kliens (Kotlin)

Qt-mentes, tiszta Kotlin. Két modul:

* **`core/`** – a protokoll teljes megvalósítása, **nem függ Androidtól** (sima JVM-en fut és tesztelhető): XML boríték, RSA/AES kézfogás, UDP felfedezés, TCP keretezés,
  felhasználólista, nyugta/újraküldés, csoportos chat, fájl- és mappaátvitel, avatar, alhálózat-átvizsgálás. Az `Messenger` osztály az egyetlen belépési pont; az eseményeket (`MessengerEvent`) egy dedikált
  motorszálon kapod. A leírás: [`../Protocol/PROTOCOL.md`](../Protocol/PROTOCOL.md).
* **`app/`** – az Android alkalmazás, csak a keretrendszer API-jait használja (nincs AndroidX, Compose vagy Material Components – a Google Maven a fejlesztői környezetből nem volt elérhető, és így az
  APK ~160 KB).

## Material 3 felület

A Material 3-at a Material Components könyvtár nélkül, a specifikáció szerint építettem meg ([`M3.kt`](app/src/main/kotlin/com/lanmessengerx/android/M3.kt)): színszerepek (világos/sötét sémák,
**Android 12+ dinamikus színek** a háttérképből), típusskála, alakok (28 dp-s dialógus, pill gombok, 16 dp-s FAB), navigációs sáv (kapszula-indikátorral), „tonal” gombok, chipek,
kapcsoló, folyamatjelző, kártyák, saját menü és dialógus. Az ikonok Material vektorok, a megjelenésük külön ellenőrizve.

| Képernyő | |
|---|---|
| People | saját profilsáv (kép – a 20 beépített kép közül választható, első indításkor véletlenszerű –, állapot és állapotüzenet), online emberek (avatar, jelenlét-pötty, jegyzet, olvasatlan jelvény), Broadcast FAB |
| Groups | Public chat (mindenki a hálózaton), csoportos szobák, „New group” FAB |
| History | mentett beszélgetések, részletes nézet, törlés |
| Settings | profil (név, jegyzet, kép), állapot-chipek, beállítások, **nyelv** (19 nyelv, magyarral), hálózat, névjegy |
| Chat / Group chat | buborékok, kézbesítési jelzés, „ír…”, fájl- és mappakártyák (Elfogadás/Elutasítás/Megszakítás/Megnyitás), a gemkapocs menüje: fájl vagy mappa küldése, hivatkozások, hosszú nyomás = másolás |

## Háttérműködés

`MessengerService` előtér-szolgáltatás (`dataSync`), ami Wi-Fi multicast zárat, Wi-Fi zárat és részleges ébren tartást fog, hogy a felfedezés és az üzenetek képernyő-kikapcsolt
állapotban is megjöjjenek. Beállítható, hogy az alkalmazás bezárásakor leálljon-e. Értesítési csatornák: „Connection” (alacsony), „Messages” (magas).

## Build

[`../BUILD.md`](../BUILD.md) – `./tools/setup-toolchain.sh && ./tools/build-apk.sh`, vagy Android Studio.

## Ellenőrzöttség – őszintén

* ✔ A `core` a JVM-en az **eredeti LAN Messenger klienssel szemben** tesztelt (lásd [`../Protocol/interop`](../Protocol/interop)), plus 16 egységteszt.
* ✔ Az APK lefordul, az erőforrások összekapcsolódnak, R8 lefut, az aláírás érvényes (`apksigner verify`), a manifest/engedélyek/SDK-szintek (min 26, target 34) az `aapt dump badging` szerint helyesek.
* ✘ **Eszközön/emulátoron nem futott.** A fejlesztői környezetben nem volt Android SDK/emulátor, ezért a felület és az Android-specifikus részek (szolgáltatás, értesítések, fájlválasztó,
  MediaStore mentés) csak fordítási és statikus szinten ellenőrzöttek. Az első telefonos próbán előkerülhet felületi vagy életciklus-hiba – ha igen, a `adb logcat -s LMX` kimenete a kiindulópont
  (a mag a „LMX” címkével naplóz).
* A **Gradle-es** build (`build.gradle.kts`) ugyanezeket a forrásokat építi, de nem lett kipróbálva.
