# LAN Messenger X – asztali kliens (C# / Avalonia)

Egyetlen kódbázis **Windowsra és Linuxra** (macOS-en is lefordul, kipróbálva nem). .NET 8, [Avalonia 11](https://avaloniaui.net), Qt nélkül.

```
src/LanMessengerX.Core/     protokoll-motor – nincs UI-függése (XML boríték, kripto, UDP/TCP, felhasználók, csoportok, fájlok)
src/LanMessengerX.App/      Avalonia alkalmazás (nézetek, nézetmodellek, beállítások, előzmények, értesítések, tálca)
tests/LanMessengerX.Tests/  xUnit: XML, kripto (OpenSSL-vektorok), protokoll-segédek
tools/LanMessengerX.Cli/    parancssoros kliens az interop teszteknek (../Protocol/interop)
tools/LanMessengerX.UiShots/ valódi ablakok fejetlen renderelése PNG-be (felülvizsgálathoz)
packaging/, installer/      .deb/.desktop és Inno Setup
publish.sh, publish-windows.bat
```

A `Core` ugyanazt a protokollt valósítja meg, mint az Android kliens Kotlin magja (a kettő kódja szándékosan tükrözi egymást), és ugyanazokkal a tesztekkel
igazolt az eredeti klienssel szemben. Leírás: [`../Protocol/PROTOCOL.md`](../Protocol/PROTOCOL.md).

## Felület

Kétpaneles: balra profil + állapot, **People/Groups** lista, alul *Broadcast / Group / History*; jobbra a beszélgetés (buborékok, kézbesítési pipa, „ír…”, fájlkártyák
elfogadással és folyamatsávval, broadcast-kártya, napi elválasztók). A téma követi a rendszert (állítható). Bezáráskor a tálcára megy (állítható); az egy példányos futást
zárolófájl biztosítja.

## Adatok

Beállítások, előzmények (soronként egy JSON), profilkép és a kapott avatarok: Windowson `%APPDATA%\LAN Messenger X`, Linuxon `~/.config/lanmessengerx`.

## Futtatás / csomagolás

Lásd [`../BUILD.md`](../BUILD.md). Fejlesztéshez: `dotnet run --project src/LanMessengerX.App`.

## Ellenőrzöttség

* ✔ Linuxon a publikált, önálló bináris virtuális kijelzőn (Xvfb) indítva, egérrel és billentyűzettel vezérelve az eredeti klienssel szemben: felfedezés, üzenet, állapot, fájl elfogadása – és a fogadott fájl bájtra azonos.
* ✔ Headless-renderelt képernyőképek világos/sötét témában; Windows-ablakok is ezzel a motorral renderelődnek.
* ✘ Windowson nem futott: a Windows-specifikus részek (tűzfalszabály a telepítőben, PowerShell-es toast, tálca-ikon) kipróbálatlanok.
