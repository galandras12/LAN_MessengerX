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
- `history.cpp`/`stdlocation.h`: `QStandardPaths::DataLocation` (Qt6-ban
  ténylegesen megszűnt enumérték, fordítási hiba lett volna) lecserélve
  `QStandardPaths::AppLocalDataLocation`-re — lásd
  [`Android/README.md`](../Android/README.md) "Üzenetelőzmény" szakaszát a
  részletekért (ott találtam meg, közben javítottam mindkét klienshez).

## Elvárt, ártalmatlan fordítási figyelmeztetések (OpenSSL 3.x)

A `crypto.cpp` szándékosan **nincs átírva** a modernebb `EVP_PKEY`-alapú
OpenSSL 3.0 API-ra — az alacsonyszintű `RSA_*` függvénycsalád (`RSA_new`,
`RSA_generate_key`, `RSA_public_encrypt`, `RSA_private_decrypt`,
`PEM_read/write_bio_RSAPublicKey`, `RSA_free` stb.) OpenSSL 3.x fejlécek
alatt **deprecated**-nek van jelölve, de **továbbra is jelen van és
működik** (az OpenSSL visszafelé-kompatibilitási ígérete szerint) — fordítás
közben egy sor `-Wdeprecated-declarations` figyelmeztetés várható
(`'RSA_generate_key' is deprecated` és hasonlók), ez **nem hiba**, a build
ettől még sikeresen lefordul és a program változatlanul működik. Szándékos
döntés nem átírni ezt EVP_PKEY-re ebben a munkamenetben, mert (a) a
tényleges kriptográfiai protokoll/adatformátum nem változna érdemben tőle,
csak az API-hívások alakja, és (b) egy ilyen átírás valós OpenSSL 3.x
build-bel tesztelést igényelne, ami ebben a szandboxban nem elérhető — a
kockázat/haszon arány rossz anélkül. Ha a figyelmeztetések zavaróak,
`-Wno-deprecated-declarations` hozzáadható a `Core.pro`-hoz, de ez csak a
figyelmeztetéseket némítja el, a mögöttes tényt nem változtatja meg.

## Szándékosan megtartott korlátozás

A hálózati huzalprotokoll (UDP discovery XML formátuma, TCP üzenetkeretezés,
RSA + AES-256-CBC titkosítási séma) **szándékosan változatlan marad**, hogy
a régi, eredeti LAN Messenger kliensekkel a kompatibilitás megmaradjon — ez
a felhasználó explicit kérése volt a tervezés során.

⚠️ **Helyesbítés (teljes program audit közben találva)**: a korábbi
munkamenetekben ez a README tévesen "RSA-2048"-at írt. A tényleges,
változatlanul hagyott kulcsméret **`crypto.cpp`-ben `bits = 1024`**, tehát
**RSA-1024** — ez az eredeti LAN Messenger saját, változatlan választása,
nem valami, amit ez a modernizáció bevezetett. Az RSA-1024-et a NIST 2013
óta nem javasolja új célra (a mai gyakorlat legalább RSA-2048-at, inkább
3072/4096-ot vár el) — ez egy valós, ismert kriptográfiai gyengeség az
eredeti protokollban. **Szándékosan nincs megemelve** ebben a munkamenetben
sem, mert a kulcsméret megváltoztatása megtörné a régi kliensekkel való
huzalprotokoll-kompatibilitást (a felhasználó explicit kérése — lásd
fent), ami ennek a modernizációnak a kőbe vésett korlátja. Ha a
kompatibilitás egyszer feladható/verzió-gate-elhető, ez az első dolog,
amit érdemes lenne megoldani.
