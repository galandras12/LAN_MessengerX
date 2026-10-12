# A LAN Messenger protokoll

Ez a dokumentum az **eredeti LAN Messenger** (Qualia Digital Solutions, GPLv3) hálózati protokollját írja le úgy, ahogy a
LAN Messenger X Kotlin ([`AndroidClient/core`](../AndroidClient/core)) és C# ([`DesktopClient/src/LanMessengerX.Core`](../DesktopClient/src/LanMessengerX.Core))
implementációja használja. Nem specifikáció volt előbb: az eredeti Qt-s forráskódból (`legacy-qt/Core/src`) lett visszafejtve, és az
[interop tesztek](interop/README.md) az **eredeti klienssel szemben** igazolják, hogy minden itt leírt dolog tényleg úgy működik.

> A cél a **visszafelé kompatibilitás**: a régi (1.x, 2.x) Windows/Linux/macOS kliensekkel és az új kliensekkel ugyanazon a LAN-on
> lehessen csevegni, fájlt küldeni, csoportos beszélgetést folytatni. Ezért a protokoll szándékosan nem lett lecserélve (pl. TLS-re).

## 1. Áttekintés

| Réteg | Mit csinál | Port / cím |
|---|---|---|
| UDP | felfedezés: *announce* / *depart* broadcast | UDP **50000**, multicast **239.255.100.100** + alhálózati broadcast |
| TCP üzenetfolyam | titkosított XML üzenetek két kliens között | TCP **50000** |
| TCP fájlfolyam | fájl/avatar bájtok, titkosítatlanul | TCP **50000** (ugyanaz a port, a nyitó 4 bájt dönti el) |

Nincs szerver. Minden kliens egyszerre UDP-vevő, TCP-szerver és TCP-kliens. Mindkét port beállítható (alapérték 50000), de **minden
kliensnek ugyanazt kell használnia**: az eredeti kliens a *saját* portját használja a másik fél felé is.

## 2. Felhasználó-azonosító

`userId = <MAC-cím kettőspontok nélkül, 12 hex> + <bejelentkezési név>`, pl. `1A24122F568Aalice`. A többi kliens számára **átlátszatlan
szöveg**, csak egyedinek kell lennie. Korlátok, amiket az új kliensek betartanak:

* a TCP nyitócsomag `"MSG" + userId` legfeljebb 64 bájt, a fájlcsomag `"FILE" + fileId(32) + userId` is 64 bájt – az eredeti egyetlen
  `read(64)`-gyel olvassa, ezért **az új kliensek legfeljebb 28 karakteres azonosítót** adnak (12 hex + legfeljebb 16 karakter név, csak ASCII
  betű/szám/`_`/`-`);
* Android 11+ nem ad MAC-címet → az Android kliens telepítéskor generált, állandó 12 hex számjegyet használ helyette.

## 3. Üzenetboríték (XML)

Minden üzenet (UDP-n és TCP-n is) UTF-8 XML:

```xml
<lmcmessage>
  <head>
    <from>1A24122F568Aalice</from>
    <to>AABBCCDDEE01bob</to>          <!-- broadcastnál (UDP) nincs -->
    <messageid>5</messageid>
    <type>message</type>
  </head>
  <body>
    <message>szia</message>
    ...
  </body>
</lmcmessage>
```

* A keresés a Qt `QDom::elementsByTagName` szabálya: *az első leszármazott az adott nevű elemmel, az elem szövege*. Ismétlődő elemnél az első nyer.
* Küldéskor nincs `<time>` fejléc; a fogadó a saját órájával bélyegzi az üzenetet.
* `messageid`: a küldő folyamatos számlálója (1-től). A fogadó `(feladó, id)` párral szűri a duplikátumokat.
* A gyökérelem neve `lmcmessage`; más gyökerű dokumentumot el kell dobni. **DOCTYPE/entitás-definíciót el kell utasítani** (a hálózat
  megbízhatatlan bemenet) – az új kliensek saját, szigorú értelmezőt (Kotlin) illetve letiltott DTD-t (C#) használnak.
* Az XML-ben tiltott vezérlőkaraktereket küldés előtt el kell távolítani, különben a Qt-s fél az **egész** üzenetet eldobja.
  `\r` küldése `&#13;`-ként történik, hogy ne essen el.

### Üzenettípusok (`<type>`)

`announce`, `depart` (UDP) · `userdata`, `status`, `name`, `note`, `avatar`, `message`, `groupmessage`, `publicmessage`, `broadcast`,
`ping`, `acknowledge`, `query`, `chatstate`, `file`, `folder` (TCP). (Az eredeti további, csak a rétegek közti helyi üzeneteket is
névvel látja el; ezek nem mennek a vezetéken.)

| type | body elemek |
|---|---|
| `userdata` | `userid`, `name`, `address`, `version`, `status`, `note`, `usercaps`, `queryop` (`get`/`result`) |
| `status` | `status` – `chat`, `busy`, `dnd`, `brb`, `away`, `gone` (utóbbi: „megjelenek offline”) |
| `name` / `note` | `name` / `note` |
| `message` | `message`, `font`, `color` (Qt `QFont::toString()` formátum és `#rrggbb`; az új kliensek `Segoe UI,9,-1,5,50,0,0,0,0,0` / `#000000` értéket küldenek, a bejövőt figyelmen kívül hagyják) |
| `acknowledge` | `messageid` (a nyugtázott üzenet azonosítója) |
| `ping` | – (a válasz `acknowledge`) |
| `broadcast` | `broadcast` (szöveg). **TCP-n megy minden címzettnek**, nem UDP-n |
| `publicmessage` | `font`, `color`, `message` – mindenkinek |
| `chatstate` | `chatstate` (`active`/`composing`/`paused`/`inactive`/`gone`), opcionálisan `thread` |
| `query` | kérés: `queryop=get`; válasz: `messageid`, `queryop=result`, `userid`, `name`, `address`, `version`, `status`, `note`, `logon`, `host`, `os`, `firstname`, `lastname`, `about` (üres érték helyett `N/A`) |
| `groupmessage` | `thread`, `groupmsgop` (`request`/`join`/`message`/`leave`), `message`, `font`, `color` |
| `file`, `avatar`, `folder` | lásd 7. |

`usercaps`: bitmaszk – `1` fájl, `2` csoportos üzenet, `4` mappa. Az új kliensek `3`-at hirdetnek.

## 4. UDP felfedezés

Egy datagram = egy teljes XML üzenet (fejléc nélkül), `type` = `announce` vagy `depart`, **nincs `<to>`**. A vevő UDP-socket a `0.0.0.0:50000`
címre köt (`SO_REUSEADDR`, hogy több kliens is megférjen) és belép a `239.255.100.100` multicast csoportba a használt interfészen.

Küldés célpontjai: a multicast csoport, az alhálózat irányított broadcast címe (`cím | ~maszk`), a felhasználó által megadott extra címek.
(Az új kliensek még a `255.255.255.255`-re is küldenek – az Androidon az irányított broadcastot néhány eszköz eldobja. A duplikátum ártalmatlan.)

* **Indulás**: `depart`, majd `announce`. **Kilépés**: `depart`.
* **announce fogadása** ismeretlen feladótól: a fogadó **TCP-kapcsolatot nyit a datagram forráscíméhez** (ez az „initiator” szerep).
* **depart fogadása**: a felhasználó eltávolítása.
* A saját `from` azonosítójú datagramot el kell dobni (visszahallgatás).
* Az új kliensek 30 másodpercenként újra `announce`-olnak, hogy a későn induló vagy hálózatot váltó társak is megtalálják őket.

Az interfész kiválasztása: első olyan interfész, ami *fel van kapcsolva, nem loopback*, van IPv4 címe. (Az új kliensek ezen felül előnyben
részesítik a Wi-Fi/Ethernet nevűeket és a magáncímeket, kerülik a VPN/virtuális adaptereket.) Hálózatváltáskor a szolgáltatások újraindulnak.

## 5. TCP üzenetfolyam

### 5.1 Kapcsolatfelépítés

```
initiator (announce-t kapott)                      acceptor
   ── TCP connect :50000 ───────────────────────▶
   ── "MSG" + <initiator userId>   (nyers, 4+ bájt, keret nélkül) ─▶
   ◀── keret: PUBKEY  + RSA nyilvános kulcs (PEM)
   ── keret: HNDSHK  + RSA-OAEP(kulcs‖IV) ─────────▶
                                                    (acceptor: userdata get) 
   ◀── keret: MESSAG  + AES(userdata, queryop=get)
   ── keret: MESSAG  + AES(userdata, queryop=result) ─▶   → mindkét fél felveszi a másikat
```

Az acceptor a kézfogás végén, amint a `HNDSHK`-t feldolgozta, elküldi a `userdata` (`queryop=get`) üzenetet; az initiator `result`-tal válaszol és
felveszi a partnert; az acceptor a válasz megérkezésekor veszi fel. **Mindkét fél hozzáadja a másikat** – a `get`-et kapó fél a
választ *elküldi*, és csak utána veszi fel a feladót.

### 5.2 Keretezés

Minden kézfogás utáni (és a `PUBKEY`/`HNDSHK`) üzenet egy keret:

```
uint32 big-endian hossz  |  6 bájtos ASCII típus  |  adat
```

típus: `PUBKEY`, `HNDSHK`, `MESSAG` (a `BRDCST` a vezetéken nem fordul elő). A hossz a típus + adat bájtjait számolja. Egy `read()` nem feltétlenül hoz
egész keretet – az új kliensek a hosszfejlécet és a törzset is többszöri olvasásból gyűjtik össze, és az 8 MiB feletti kereteket eldobják.

### 5.3 Titkosítás

1. Az acceptor (a szerver oldal) RSA kulcspárt generál (az eredeti 1024 bitet, az új kliensek 2048 bitet – az OpenSSL mindkettőt elfogadja), és a
   nyilvános kulcsot **PKCS#1 `RSAPublicKey` PEM**-ben küldi (`-----BEGIN RSA PUBLIC KEY-----`, nem az X.509 `PUBLIC KEY`).
2. Az initiator 32 véletlen bájtot húz, és `EVP_BytesToKey(AES-256-CBC, SHA-1, só nélkül, 5 kör)`-rel 32 bájt kulcsot + 16 bájt IV-t származtat.
3. A 48 bájtot (`kulcs‖IV`) **RSA-OAEP (SHA-1, MGF1-SHA-1)** padinggel titkosítja és `HNDSHK`-ban küldi.
4. Innentől minden `MESSAG` adata **AES-256-CBC, PKCS#7**, **rögzített** kulccsal és IV-vel. Mivel az OpenSSL-ben az `EVP_EncryptInit_ex(ctx, NULL…)`
   minden üzenet előtt visszaállítja az IV-t, minden üzenet ugyanazzal az IV-vel titkosított: a dekódolás állapotmentes, üzenetenként független.

Tesztvektorok az OpenSSL parancssorával előállítva: [`testvectors/`](testvectors/README.md).

> Biztonsági megjegyzés: a séma (rögzített IV, hitelesítés nélkül, kulcs-megerősítés nélkül) **nem** állja meg a helyét egy aktív támadóval szemben.
> A kompatibilitás miatt marad így; a LAN-on belüli megbízható környezetre van szabva.

### 5.4 Több kapcsolat ugyanazon két fél között

Ha két kliens egyszerre indul, mindkettő kapcsolatot nyit a másikhoz. Az eredeti kliens felhasználónként *egy* „aktuális” folyamot és
*egy* kulcsot tart (a legutóbbit), ezért versenyhelyzet esetén néha nem tudja visszafejteni a másik üzenetét (a naplóban:
„Message decryption failed”). Az új kliensek: (a) nem nyitnak újabb kapcsolatot, ha már van vagy épp épül; (b) minden üzenetet **először a
saját kapcsolatuk kulcsával, azután a felhasználó legutóbbi kulcsával** próbálnak visszafejteni; (c) a felhasználót csak akkor távolítják el
kapcsolatbontás miatt, ha az *aktuális* folyam zárult le.

## 6. Üzenetkezelés

* **Nyugta**: a `message` és a `ping` fogadója `acknowledge`-et küld. A küldő a függő listán tartja az üzenetet; alapértelmezés szerint 10 mp után
  egyszer újraküldi, utána `message` esetén „nem kézbesítve”, `ping` esetén a partner eltávolítása.
* **Duplikátum-szűrés**: `(feladó, messageid)` – az újraküldött üzenet csak egyszer jelenik meg, de minden példányt nyugtázni kell.
* **Állapotváltozás** (`status`/`name`/`note`): minden ismert partnernek külön TCP üzenetben, azonos `messageid`-vel.
* **Életjel**: az új kliensek 30 mp-enként `ping`-elnek mindenkit; a nem válaszoló partner eltűnik a listáról.
* **Csoportos chat** (`groupmessage`): a szoba azonosítója a `thread` (32 hex).
  * létrehozó: saját szobát nyit, `join`-t küld *minden* ismert felhasználónak, `request`-et a meghívottaknak;
  * `request` fogadója: ha nem ismeri a szobát, létrehozza (és `join`-t küld mindenkinek), majd felveszi a küldőt;
  * `join`: ha ismeri a szobát, felveszi a küldőt; ismeretlen szobánál figyelmen kívül hagyja;
  * `message`: a résztvevőnek külön-külön, `thread` + `message` mezővel;
  * `leave`: minden ismert felhasználónak; a fogadó eltávolítja a küldőt;
  * csak **1.2.10-nél újabb** verziójú partner vehető fel szobába (régebbi nem ismeri a funkciót).
* **Verzió**: az új kliensek `3.0.0`-t hirdetnek, ami minden eredeti küszöbnél (`> 1.2.10`, `> 1.2.30`) újabb.

## 7. Fájlátvitel

Egy fájl három lépés: ajánlat (TCP üzenetfolyamon), elfogadás (ugyanott), majd a bájtok **külön TCP kapcsolaton**, titkosítatlanul.

```
küldő                                           fogadó
 ── file{filetype=normal, fileop=request, fileid, mode=send, filename, filesize} ─▶
 ◀─ file{mode=receive, filetype=normal, fileop=accept, fileid, filename, filesize}      (vagy fileop=decline)
 ── TCP connect → "FILE" + fileId(32 ASCII) + küldő userId (nyers) ─▶
 ◀─ "START"                                  (5 bájt)
 ── a fájl nyers bájtjai, filesize hosszan ─▶
    (a küldő bezárja a kapcsolatot; nincs „complete” üzenet a vezetéken)
```

Fontos részletek, mind az eredeti klienssel való együttműködésből:

* **A `filesize` az accept üzenetben is kötelező.** A küldő (az eredeti kliensben) a méretet *az accept üzenetből* veszi, a fogadó pedig
  pontosan ennyi bájtot vár; `0` méretnél a fogadó sosem fejezi be az átvitelt.
* A `fileid` UUID kötőjelek és kapcsok nélkül, 32 karakter; az `fileop` további értékei: `cancel` (bármelyik fél), `abort` (hiba után).
* Az eredeti a kérésben a `filepath`-ban a küldő *helyi elérési útját* is kiszivárogtatja, az accept pedig a fogadóét. Az új kliensek ezt nem küldik
  (az értéket senki sem használja).
* A fogadó a `FILE` kapcsolaton az azonosítót **legfeljebb 64 bájtból** olvassa; a `userId`-t előtagként érdemes egyeztetni.
* **Az eredeti fogadó törékeny**: minden `readyRead()`-nél *legfeljebb 65535* bájtot olvas, és nem ürít hátralékot. Ha a küldő gyorsabb, a
  hátralék a végén ottmarad, a kapcsolat lezárul, és az átvitel *hibával* ér véget (jellemzően az utolsó darab hiányzik). Ezért:
  * az új kliensek a fájlt **65535 bájtos** darabokban küldik (a 65536 bájt minden darabnál 1 bájt hátralékot hagyna);
  * **3.0.0-nál régebbi** partnernek darabonként ~3 ms szünettel („paced” mód) – ezzel a tesztekben egy átvitel sem hibázott;
  * az új kliensek fogadó oldala a szerver-socketen **1 MiB** vevőpuffert kér, ami a *küldő eredeti kliens* hasonló versenyhelyzetét kerüli el
    (a mérésekben: 12 átvitelből 1 hibás volt, utána 110 átvitelből 0).
* Időtúllépés: a fogadó 30 mp mozdulatlanság után (az eredeti: 20 × 1,5 mp) hibát jelez.
* **Avatar**: ugyanez a folyamat `type=avatar` / `filetype=avatar` jelöléssel, a fogadó **automatikusan elfogadja** (a képet a gyorsítótárba
  menti). Új partner felvételekor és profilkép-váltáskor mindenkinek elküldendő.
* **Mappa** (`folder`): az új kliensek nem fogadják – az ajánlatra `folder{fileop=decline, fileid, folderid}` a válasz, hogy a küldő ne ragadjon be.

## 8. Ismert sajátosságok az eredetiben (amiket az új kliensek kikerülnek)

| Sajátosság | Következmény / megoldás |
|---|---|
| Az `announce` minden fogadásnál új TCP-kapcsolatot nyit, amíg a partner „ismeretlen” | a másik két kapcsolat versenye (5.4) |
| A felhasználó-azonosító a MAC-címből jön | Android 11+: nincs MAC → állandó véletlen azonosító |
| A broadcast üzenet UDP-n *nem* terjed (`processBroadcast` csak `announce`/`depart`-ot kezel) | a `broadcast` típus TCP-n, címzettenként megy |
| `socket->read(64)` az azonosítócsomagra | rövid azonosító (28 karakter) |
| A fogadó 65535 bájtos olvasása | 65535 bájtos küldési darab + pacing |
| Az accept üzenet `filesize`-a nélkül a fogadó nem ér véget | mindig küldeni |

## 9. Interop tesztek

[`interop/`](interop/README.md): az eredeti kliens fej nélküli futtatója + két hálózati névtér (`ip netns`), amelyben valódi UDP broadcast/multicast
és TCP forgalom megy. Lefedett: felfedezés, szöveg (unicode, escape), nyugta, állapot, felhasználói adatlap, fájl mindkét irányban (több száz
ismétlés, 40 MB-ig), avatar, csoportos chat – **Kotlin ↔ eredeti**, **C# ↔ eredeti** és **Kotlin ↔ C#**.
