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
6. A „Look for people again / Refresh” gomb azonnal újra bejelentkezik (announce); a kliensek amúgy is 30 másodpercenként teszik.

## Régi (Qt-s) LAN Messenger kliensekkel működik?
Igen, ez a cél. Az eredeti 1.x/2.x kliensekkel üzenet, csoportos chat, fájl és profilkép is megy mindkét irányban (lásd [`Protocol/interop`](Protocol/interop)).
Amit az új kliensek **nem** támogatnak: mappaküldés (a régi kliens ajánlatát udvariasan elutasítják), az eredeti beépített avatar-galéria (helyette betűs avatar látszik,
amíg valaki saját képet nem állít be), a Public Chat felülete (a protokollt értik, de a felületen nincs).

## Fájlküldés elakad / hibával áll le
Az eredeti kliens fogadó és küldő oldala időzítésre érzékeny (legfeljebb 65535 bájtot olvas `readyRead()`-enként, és nem ürít hátralékot). Az új kliensek ezt kerülik (kis
darabok, lassabb küldés régi partnernek, nagy vevőpuffer), de nagyon terhelt gépen ritkán így is megeshet. Küldd el újra; részletek: [`Protocol/PROTOCOL.md`](Protocol/PROTOCOL.md) 7. fejezet.

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
