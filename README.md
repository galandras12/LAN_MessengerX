# LAN Messenger X

**Alkalmazás neve:** LAN Messenger X · **Verzió:** 1.0.1

Az eredeti, megszűnt támogatású **LAN Messenger** (QualiaTech / Dilip
Radhakrishnan, GPLv3) modernizált, folyamatban lévő újrakiadása —
Windows 11-kompatibilis asztali kliens és egy modern felületű Android
kliens, közös hálózati/protokoll magkóddal. Az alkalmazás neve és a
verziószám egyetlen helyről ([`Core/src/definitions.h`](Core/src/definitions.h),
`IDA_TITLE`/`IDA_VERSION`) származik mindkét platformon.

## Mappa-elrendezés

| Mappa | Tartalom |
|---|---|
| [`/Windows`](Windows) | A Qt Widgets-alapú asztali kliens (a modernizált `lmc` + `lmcapp` projekt) |
| [`/Android`](Android) | A Qt Quick/QML Android kliens, a `/Core`-ra bekötve (kontaktlista, 1:1 chat, egyfájlos fájlátvitel működik forráskód-szinten; build még nincs ellenőrizve, lásd a mappa README-jét) |
| [`/Core`](Core) | A két kliens által megosztott hálózati/protokoll/titkosítási/előzmény réteg |

## Státusz

Ez a repó a felhasználóval egyeztetett, jóváhagyott modernizációs terv
alapján fejlődik, fázisokban:

- **Fázis 0 — Repó-struktúra**: ✅ kész (ez a munkamenet)
- **Fázis 1 — Stabilizálás**: ✅ kész (ez a munkamenet) — konkrét
  összeomlás-okok javítva a hálózati/titkosítási rétegben, lásd
  [`Core/README.md`](Core/README.md) és [`Windows/README.md`](Windows/README.md)
- **Fázis 2 — Windows kliens Qt 6 portolása**: ✅ forráskód-szinten kész
  (elavult Qt5/Qt4 API-k lecserélve, telepítő NSIS-ről Inno Setup-ra
  migrálva — lásd [`Windows/README.md`](Windows/README.md)),
  ⏳ tényleges Qt6+OpenSSL3 build-bel (és az Inno Setup szkript tényleges
  fordítóval) még nincs ellenőrizve (nincs Qt/Inno Setup telepítve
  ebben a fejlesztői környezetben)
- **Fázis 3 — Core kiemelése önálló, mindkét platform által linkelhető
  library-vé**: ✅ forráskód/build-rendszer szinten kész (`Core.pro`,
  Widgets-mentes — lásd [`Core/README.md`](Core/README.md)), ⏳ tényleges
  build-bel még nincs ellenőrizve, Android (NDK) célzás még nem indult
- **Fázis 4 — Android kliens (Qt Quick/QML)**: ✅ valóban a `/Core`-ra
  bekötött verzió — kontaktlista, 1:1 chat, broadcast küldés, egyfájlos
  fájlátvitel, csoportos chat szoba (meghívás/csatlakozás/üzenet/kilépés
  és utólagos meghívás is, a Windows `chatroomwindow.cpp` protokollját
  lekövetve), profil-beállítások (név/állapot/megjegyzés, avatar
  szerkesztése és fogadása), üzenetelőzmény (`/Core`-beli `History`
  fájlformátumra bekötve, Windowsszal kompatibilisen), foreground
  service + Wi-Fi multicast lock a háttérbeli működéshez, push-jellegű
  új-üzenet értesítés + `POST_NOTIFICATIONS` futásidejű engedélykérés —
  lásd [`Android/README.md`](Android/README.md). ⏳ build/futtatás
  ellenőrizetlen, néhány funkció (mappaátvitel, Public Chat, beépített
  avatar-galéria) még nincs bekötve, **az
  Android-specifikus OpenSSL linkelés megoldatlan** (lásd Android README
  "Kritikus" szakasza), a fájlküldés pedig csak `file://` elérési útra
  működik (`content://` SAF URI-kra még nem)
- **Fázis 5 — Cross-platform interop tesztelés**: ⏳ tervezve

A döntések, amik a tervet alakították:
- Android UI: Qt Quick/QML, megosztott C++ maggal (nem teljesen natív Kotlin)
- Windows: Qt 6 LTS célzás
- A régi, eredeti LAN Messenger kliensekkel a hálózati protokoll szintjén
  **megmarad a kompatibilitás** — ez korlátozza, mennyire lehet a
  protokollt/titkosítást modernizálni (lásd a Core README-jét).

## Licenc

GPLv3, az eredeti projekt licencét megtartva (lásd [`LICENSE`](LICENSE)).
