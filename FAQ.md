# Gyakori kérdések

## Nem látom a többieket / a többiek nem látnak engem
1. Ugyanazon a **hálózaton** vagytok? (Ugyanaz a Wi-Fi/LAN, nem vendéghálózat, nem VPN.)
2. Sok routeren a Wi-Fi-n van **„AP-/kliens-elkülönítés”** (client isolation), ami megszakítja a készülékek közti forgalmat. Kapcsold ki, vagy használj kábelt.
3. **Tűzfal**: Windowson első indításkor engedélyezd a hozzáférést *privát* hálózaton. Kézzel: bejövő szabály UDP **és** TCP 50000-es portra. Ha az első kérdést bezártad vagy elutasítottad, Windows tiltó szabályt hagy hátra – ilyenkor a *Beállítások → Network → Allow in Windows Firewall…* gomb (egyszeri rendszergazdai jóváhagyás) törli a tiltást és engedélyezi a programot. A *Beállítások* alján látszik a napló is (`%APPDATA%\LAN Messenger X\lanmessengerx.log`), ebből kiderül, melyik hálózati adaptert használja és kit lát.
   Linuxon `ufw allow 50000` (UDP és TCP).
4. Más portot használ valaki? A kliensek **mind** az 50000-est használják alapból; az eredeti kliens a saját portját használja a többiek felé is, ezért mindenhol azonos kell legyen.
5. Androidon a megosztott multicast/broadcast csomagokat az energiatakarékos Wi-Fi eldobhatja. Az alkalmazás ezért előtérszolgáltatást, Wi-Fi zárat és multicast zárat tart (az értesítési
   sávban „LAN Messenger X” látszik). Ha a gyártód agresszívan öli a háttérfolyamatokat (Xiaomi, Huawei, Samsung…), vedd ki az alkalmazást az akkumulátor-optimalizálásból
   (<https://dontkillmyapp.com>).
6. A „Refresh Contacts List” gomb azonnal újra bejelentkezik (announce). Az **eredeti** kliensek az announce-ot csak induláskor és a „partnerlista frissítése” időközönként
   (alapból 5 percenként) küldik – ezért a később induló kliens régen sokáig nem látta a korábban indultakat. Az új kliensek ezért az alhálózat minden címének 50000-es portját is
   végigpróbálják (a *Hálózat* oldalon kikapcsolható): a partnerek általában 3–8 másodperc alatt megjelennek, akkor is, ha a tűzfal a bejövő forgalmat eldobja.
7. **Fájl érkezik hozzád, de nem jön át?** A fájlt a *küldő* nyit kapcsolatot hozzád, ehhez a tűzfalnak engednie kell a LAN Messenger X-et (lásd 3. pont). Első indításkor a Windows-os
   kliens maga is felajánlja a tűzfalszabályt.

## Régi (Qt-s) LAN Messenger kliensekkel működik?
Igen, ez a cél. Az eredeti 1.x/2.x kliensekkel üzenet, csoportos chat, fájl és profilkép is megy mindkét irányban (lásd [`Protocol/interop`](Protocol/interop)).
Mappaküldés/-fogadás (egymás után elküldött fájlok), nyilvános csevegés, a 20 beépített profilkép és az állapotüzenet is megy. Az eredeti fogadó nem tud **üres fájlt** fogadni, ezért
az üres fájlokat az új kliensek régi partnernek kihagyják a mappából. Az eredeti kliens *Windows-felületének* egyes részletei (csevegő-témák, az automatikus „távol” állapot) nincsenek meg.

## Fájlküldés elakad / hibával áll le
Az eredeti kliens fogadó és küldő oldala időzítésre érzékeny (legfeljebb 65535 bájtot olvas `readyRead()`-enként, és nem ürít hátralékot). Az új kliensek ezt kerülik (kis
darabok, lassabb küldés régi partnernek, nagy vevőpuffer), de nagyon terhelt gépen ritkán így is megeshet. Küldd el újra; részletek: [`Protocol/PROTOCOL.md`](Protocol/PROTOCOL.md) 7. fejezet.

## A profilkép-választó gomb nem csinál semmit
A program három módon próbál fájlt választani: a klasszikus Windows-párbeszédablak, az Avalonia saját ablaka, végül egy a programba épített böngésző, amely mindig megnyílik.
A főablakban a **profilképre kattintva** kinyílik a 20 beépített kép (egy kattintás elég), mellette a „További képek tallózása…”. Hiba esetén a naplóban
(`%APPDATA%\LAN Messenger X\lanmessengerx.log`) látszik, melyik lépés bukott el.

## Hogyan váltok nyelvet?
*Beállítások → Általános → Nyelv*. Az eredeti kliens 19 nyelve közül választhatsz, vagy „System default”. A magyar, német, francia, spanyol, olasz, brazil portugál, orosz és lengyel teljes, a többiben angolul jelenik meg, ami nincs lefordítva
(lásd a [`README.md`](README.md) *Nyelvek* részét). Az Androidon ugyanez a *Beállítások* lapon van.

## Hova kerülnek a fájlok / előzmények / beállítások?
| | Android | Windows | Linux |
|---|---|---|---|
| Kapott fájlok | `Letöltések/LAN Messenger X` | `Letöltések\LAN Messenger X` (állítható) | `~/Downloads/LAN Messenger X` (állítható) |
| Beállítások, előzmények, profilkép | az alkalmazás privát tárhelye | `%APPDATA%\LAN Messenger X` | `~/.config/lanmessengerx` |

## Android: „App not installed” frissítéskor
Más kulccsal aláírt APK-t nem lehet egymásra telepíteni. A tárolt kulcs a repóban van (`AndroidClient/signing`), az azzal épített kiadások frissítik egymást; ha saját kulcsra váltasz, egyszer el kell távolítani a régit.

## Windows SmartScreen figyelmeztet
A kiadás nincs kódaláírva (ehhez kereskedelmi tanúsítvány kell). „További információ → Futtatás mindenképp”.

## A tálca-ikon nem látszik Linuxon
Az Avalonia a StatusNotifier/AppIndicator szabványt használja; GNOME alatt kell hozzá az *AppIndicator* kiterjesztés. Ha nincs tálca, az ablak bezárása kilép (a beállításokban kikapcsolható a „tálcára zárás”).

## Biztonság
A protokoll kompatibilitás miatt az eredeti titkosítási sémát használja (RSA kulcscsere + rögzített IV-vel AES-256-CBC, hitelesítés nélkül), a fájlok pedig titkosítatlanul mennek. Megbízható LAN-ra szánt –
nyilvános Wi-Fin ne használd bizalmas tartalomhoz. A részleteket a [`Protocol/PROTOCOL.md`](Protocol/PROTOCOL.md) 5.3 fejezete tartalmazza.
