# LAN Messenger — megosztott Core

Ez a mappa a LAN Messenger platformfüggetlen hálózati/protokoll/titkosítási/
üzenetelőzmény rétegét tartalmazza, kiemelve az eredeti `lmc/src`-ből. Célja,
hogy mind a [`/Windows`](../Windows) (Qt Widgets), mind a jövőbeli
[`/Android`](../Android) (Qt Quick/QML) kliens ugyanazt a kódot használja a
LAN discovery-hez, TCP/UDP üzenetküldéshez, RSA/AES titkosításhoz és a
csevegési előzmények kezeléséhez.

## Jelenlegi állapot

**Fizikailag különálló**, de a build rendszer szintjén **még nem önálló
library** — a `Windows/lmc/src/lmc.pro` jelenleg közvetlenül idefordítja be
ezeket a forrásfájlokat a Windows kliens buildjébe. Az, hogy ez tényleges,
mindkét platform (Widgets + Qt Quick) által linkelhető statikus/shared
library legyen, egy későbbi fázis (a tervben "Fázis 3 — Megosztott Core
kiemelése") munkája — ekkor kell majd megvizsgálni és szükség esetén
eltávolítani a `settings.cpp`/`settings.h` jelenlegi `QApplication`-
függőségét is (alapértelmezett betűtípus/szín lekérdezéséhez használja,
ami Qt Widgets-specifikus, egy QML-alapú Android kliens ezt nem tudja
ugyanígy hívni).

## Ebben a munkamenetben javított hibák

- `netstreamer.cpp` (`MsgStream::readyRead`): helyesen kezeli, ha a TCP
  üzenetkeretezés (4 bájtos hosszfejléc + payload) több hálózati olvasásra
  töredezik szét — korábban ez adatvesztéshez/összeomláshoz vezethetett.
- `crypto.cpp`: az `encrypt()`/`decrypt()` már nem másolja értékben a
  megosztott OpenSSL cipher-kontextust egy `QMap`-ból (ami OpenSSL 1.1+/3.x
  ellen use-after-free/double-free hibát okozott volna gyakorlatilag minden
  titkosított üzenetnél) — most pointert tárol és ad vissza.
- `FileSender`/`FileReceiver`: a belső átviteli puffer most felszabadul a
  destruktorban.

## Szándékosan megtartott korlátozás

A hálózati huzalprotokoll (UDP discovery XML formátuma, TCP üzenetkeretezés,
RSA-2048 OAEP + AES-256-CBC titkosítási séma) **szándékosan változatlan
marad**, hogy a régi, eredeti LAN Messenger kliensekkel a kompatibilitás
megmaradjon — ez a felhasználó explicit kérése volt a tervezés során.
