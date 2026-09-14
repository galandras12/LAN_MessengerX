# LAN Messenger — Android kliens (tervezés alatt)

**Ez a mappa egyelőre csak helyfoglaló.** A jóváhagyott modernizációs terv
szerint az Android kliens a Fázis 3–4 munkája, ami egy **külön munkamenetben**
készül, a [`/Windows`](../Windows) + [`/Core`](../Core) Fázis 0–1 stabilizálása
után. Ez a README azt rögzíti, mi a terv, hogy a munka bármikor onnan
folytatható legyen, ahol abbamaradt.

## Tervezett megközelítés

- **Qt for Android + Qt Quick/QML** felület, Material stílussal — nem
  teljesen natív Kotlin alkalmazás. Ezt a felhasználó választotta a
  tervezés során, mert így a [`/Core`](../Core) mappában lévő C++
  hálózati/protokoll/titkosítási kód **újrahasználható** változtatás
  nélkül, egy közös kódbázisból lehet karbantartani a Windows és Android
  klienst.
- A felület (chat, kontaktlista, fájlátvitel, beállítások nézetek)
  **újratervezésre kerül** QML-ben, modern, touch-optimalizált,
  Material-stílusú megjelenéssel — nem az asztali UI portolása.
- Ugyanaz a hálózati protokoll (UDP broadcast discovery + TCP XML
  üzenetküldés, RSA/AES titkosítás), mint a Windows kliens és az eredeti,
  régi LAN Messenger — tehát vegyes hálózaton (régi kliens + új Windows +
  Android) is kommunikálni tud.

## Android-specifikus tervezési pontok (lásd a teljes tervet)

- Manifest engedélyek: `INTERNET`, `ACCESS_WIFI_STATE`,
  `ACCESS_NETWORK_STATE`, `CHANGE_WIFI_MULTICAST_STATE` (multicast lock az
  UDP broadcast discoveryhez), `POST_NOTIFICATIONS` (Android 13+).
- Foreground service szükséges a megbízható háttérbeli elérhetőséghez és
  üzenetfogadáshoz (Android Doze/akkumulátor-optimalizálás miatt).
- Gradle + Qt Creator Android build célok, APK/AAB kimenet.
- Play Store terjesztés (ha később cél lenne) extra munkát igényel
  (scoped storage, target SDK megfelelés) — ez jelenleg nincs a tervben.

## Mit NEM tartalmaz ez a mappa most

Forráskódot, build fájlokat vagy futtatható projektet — ezek a következő
munkamenet(ek) eredménye lesznek, miután a `/Core` réteg ténylegesen
Qt-Widgets-független library-ként buildelhetővé válik (a tervben
"Fázis 3").
