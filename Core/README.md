# LAN Messenger — megosztott Core

Ez a mappa a LAN Messenger platformfüggetlen hálózati/protokoll/titkosítási/
üzenetelőzmény rétegét tartalmazza, kiemelve az eredeti `lmc/src`-ből. Célja,
hogy mind a [`/Windows`](../Windows) (Qt Widgets), mind a jövőbeli
[`/Android`](../Android) (Qt Quick/QML) kliens ugyanazt a kódot használja a
LAN discovery-hez, TCP/UDP üzenetküldéshez, RSA/AES titkosításhoz és a
csevegési előzmények kezeléséhez.

## Jelenlegi állapot

Önálló, `lmccore` nevű **statikus library** qmake-projekt ([`Core.pro`](Core.pro),
`TEMPLATE = lib`, `CONFIG += staticlib`) — a `Windows/lmc/src/lmc.pro` ezt
linkeli be, nem fordítja be közvetlenül a forrásfájljait.

A `Core.pro` szándékosan **nem kér QtWidgets-et** (`QT -= gui`), így egy
jövőbeli Qt Quick/QML Android kliens Widgets nélkül is linkelheti. Az
egyetlen korábbi Widgets-függés — a `settings.h`-beli `IDS_FONT_VAL`/
`IDS_COLOR_VAL` makrók, amik `QApplication::font()`/`palette()`-et hívtak
alapértékként — `#ifdef QT_WIDGETS_LIB`-fel körbevéve maradt meg: a
Windows kliens build-jében (ahol ez a makró definiálva van, mert
`QT += widgets`) a viselkedés változatlan, egy Widgets nélküli fogyasztó
pedig egyszerűen üres stringet kap alapértékként, és a saját UI-jának
megfelelő alapértéket állíthat be helyette. A `settings.cpp` egyetlen
közvetlen `QApplication`-hívása (`applicationFilePath()`) pedig
`QCoreApplication`-re lett cserélve, mivel ott van ténylegesen definiálva.

Ellenőrizve (grep-el), hogy a `/Core` semmilyen más QtGui/QtWidgets típust
nem használ (`QColor`, `QFont`, `QPixmap`, `QIcon`, `QWidget` stb.) — a
fenti volt az egyetlen ilyen függés.

Amit ez a fázis **nem old meg**: a `Core.pro` build-jét ez a
szandbox-környezet nem tudta ellenőrizni (nincs telepítve Qt), és az
Android célzáshoz (NDK toolchain, a `lmccore` Android ABI-kra fordítása,
az OpenSSL Android-specifikus linkelése) még semmi nem készült — az a
terv Fázis 4-je.

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
