# LAN Messenger X – asztali kliens (C# / Avalonia)

Egyetlen kódbázis **Windowsra és Linuxra** (macOS-en is lefordul, kipróbálva nem). .NET 8, [Avalonia 11](https://avaloniaui.net), Qt nélkül.

```
src/LanMessengerX.Core/     protokoll-motor – nincs UI-függése (XML boríték, kripto, UDP/TCP, felhasználók, csoportok, fájlok, mappák)
src/LanMessengerX.App/      Avalonia alkalmazás (nézetek, nézetmodellek, beállítások, előzmények, értesítések, tálca)
tests/LanMessengerX.Tests/  xUnit: XML, kripto (OpenSSL-vektorok), protokoll-segédek, felfedezés
tools/i18n/                 a nyelvi fájlok előállítása (az eredeti .ts fordításokból + saját fordításokból)
tools/LanMessengerX.Cli/    parancssoros kliens az interop teszteknek (../Protocol/interop)
tools/LanMessengerX.UiShots/ valódi ablakok fejetlen renderelése PNG-be (felülvizsgálathoz)
packaging/, installer/      .deb/.desktop és Inno Setup
publish.sh, publish-windows.bat
```

A `Core` ugyanazt a protokollt valósítja meg, mint az Android kliens Kotlin magja (a kettő kódja szándékosan tükrözi egymást), és ugyanazokkal a tesztekkel
igazolt az eredeti klienssel szemben. Leírás: [`../Protocol/PROTOCOL.md`](../Protocol/PROTOCOL.md).

## Felület

Kétpaneles, az eredeti 1.x kliens funkcióival: fent menüsor (*Messenger / Tools / Help*), balra profil (kép – kattintásra a 20 beépített kép és a saját képek tallózása –, állapot-menü, **állapotüzenet**),
**People** lista (partnercsoportokkal, jobb gombos menüvel: beszélgetés, broadcast, fájl, mappa, adatok, áthelyezés csoportba) és **Chat rooms** (Public chat + csoportos csevegőszobák), alul
*Broadcast / Chat room / Transfers*; jobbra a beszélgetés (buborékok, hangulatjelek, kattintható hivatkozások, kézbesítési pipa, „ír…”, fájl- és mappakártyák elfogadással és folyamatsávval,
broadcast-kártya, napi elválasztók). Külön ablakok: Beállítások (9 oldal), Fájlátvitelek, Előzmények, Névjegy, Súgó. A téma követi a rendszert (állítható). Bezáráskor/kicsinyítéskor a tálcára
mehet (állítható); az egy példányos futást zárolófájl biztosítja.

**Nyelv:** *Beállítások → Általános → Nyelv* (19 nyelv, magyarral). A szövegek angol kulccsal a `Assets/lang/*.json` fájlokban vannak; előállításuk: `tools/i18n/build_lang.py`.

**Fájlválasztás:** először a klasszikus Windows-párbeszédablak, ha az nem jön létre, az Avalonia ablaka, végül a programba épített böngésző – a gomb sosem „csinál semmit”.

**Felfedezés:** UDP broadcast/multicast **és** az alhálózat átvizsgálása (tartalékként, ha nem jön bejövő kapcsolat: a bejövő forgalmat tiltó tűzfal mögött is megtalálja a partnereket). Az első indításkor Windowson felajánlja a tűzfalszabályt.

## Adatok

Beállítások, előzmények (soronként egy JSON), profilkép és a kapott avatarok: Windowson `%APPDATA%\LAN Messenger X`, Linuxon `~/.config/lanmessengerx`.

## Futtatás / csomagolás

Lásd [`../BUILD.md`](../BUILD.md). Fejlesztéshez: `dotnet run --project src/LanMessengerX.App`.

## Ellenőrzöttség

* ✔ Linuxon a publikált, önálló bináris virtuális kijelzőn (Xvfb) indítva, egérrel és billentyűzettel vezérelve az eredeti klienssel szemben: felfedezés, üzenet, állapot, fájl elfogadása – és a fogadott fájl bájtra azonos.
* ✔ Headless-renderelt képernyőképek világos/sötét témában; Windows-ablakok is ezzel a motorral renderelődnek.
* ✘ Windowson nem futott: a Windows-specifikus részek (tűzfalszabály a telepítőben, PowerShell-es toast, tálca-ikon) kipróbálatlanok.
