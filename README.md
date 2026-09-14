# LAN Messenger X

Az eredeti, megszűnt támogatású **LAN Messenger** (QualiaTech / Dilip
Radhakrishnan, GPLv3) modernizált, folyamatban lévő újrakiadása —
Windows 11-kompatibilis asztali kliens és egy tervezett, modern
felületű Android kliens, közös hálózati/protokoll magkóddal.

## Mappa-elrendezés

| Mappa | Tartalom |
|---|---|
| [`/Windows`](Windows) | A Qt Widgets-alapú asztali kliens (a modernizált `lmc` + `lmcapp` projekt) |
| [`/Android`](Android) | A tervezett Qt Quick/QML Android kliens (jelenleg tervezési állapotban, lásd a mappa README-jét) |
| [`/Core`](Core) | A két kliens által megosztott hálózati/protokoll/titkosítási/előzmény réteg |

## Státusz

Ez a repó a felhasználóval egyeztetett, jóváhagyott modernizációs terv
alapján fejlődik, fázisokban:

- **Fázis 0 — Repó-struktúra**: ✅ kész (ez a munkamenet)
- **Fázis 1 — Stabilizálás**: ✅ kész (ez a munkamenet) — konkrét
  összeomlás-okok javítva a hálózati/titkosítási rétegben, lásd
  [`Core/README.md`](Core/README.md) és [`Windows/README.md`](Windows/README.md)
- **Fázis 2 — Windows kliens Qt 6 portolása**: ✅ forráskód-szinten kész
  (elavult Qt5/Qt4 API-k lecserélve — lásd [`Windows/README.md`](Windows/README.md)),
  ⏳ tényleges Qt6+OpenSSL3 build-bel még nincs ellenőrizve (nincs Qt telepítve
  ebben a fejlesztői környezetben)
- **Fázis 3 — Core kiemelése önálló, mindkét platform által linkelhető
  library-vé**: ⏳ tervezve
- **Fázis 4 — Android kliens (Qt Quick/QML)**: ⏳ tervezve
- **Fázis 5 — Cross-platform interop tesztelés**: ⏳ tervezve

A döntések, amik a tervet alakították:
- Android UI: Qt Quick/QML, megosztott C++ maggal (nem teljesen natív Kotlin)
- Windows: Qt 6 LTS célzás
- A régi, eredeti LAN Messenger kliensekkel a hálózati protokoll szintjén
  **megmarad a kompatibilitás** — ez korlátozza, mennyire lehet a
  protokollt/titkosítást modernizálni (lásd a Core README-jét).

## Licenc

GPLv3, az eredeti projekt licencét megtartva (lásd [`LICENSE`](LICENSE)).
