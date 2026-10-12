# Interop tesztek az eredeti LAN Messengerrel

Ezek a tesztek a **valódi eredeti klienssel** szemben próbálják ki az új Kotlin és C# implementációt – nem szimulációval, hanem két hálózati
névtérben (`ip netns`) futó példányok között, valódi UDP broadcast/multicast és TCP forgalommal.

```
lab_a 10.9.0.1  ── veth ──  lab_b 10.9.0.2
(eredeti kliens,             (Kotlin CLI vagy C# CLI)
 fej nélkül, Qt 6)
```

## Futtatás

Linuxon, rootként (a névterekhez), a következők kellenek: `qt6-base-dev`, `libssl-dev`, `build-essential`, `iproute2`, `python3`, JDK 17+,
a Kotlin eszközlánc (`AndroidClient/tools/setup-toolchain.sh`) és a .NET 8 SDK.

```bash
sudo ./run-all.sh                 # mindent felépít, felhúzza a labort, lefuttat minden tesztet, lebontja a labort
```

Külön-külön:

```bash
./build.sh                         # eredeti kliens (headless) + Kotlin CLI + C# CLI
sudo ./lab.sh up
sudo CLIENT=kotlin python3 test_legacy.py     # szöveg, nyugta, állapot, adatlap, fájl, avatar, csoportos chat
sudo CLIENT=csharp python3 test_legacy.py
sudo python3 test_kotlin_csharp.py            # az új két implementáció egymással
sudo CLIENT=csharp ROUNDS=20 python3 test_files.py   # fájlátviteli terhelés mindkét irányban, 40 MB-ig
sudo ./lab.sh down
```

## Az „eredeti kliens” futtatója

[`legacy-harness/`](legacy-harness) az eredeti Qt-s `lmcMessaging` osztályt (a `legacy-qt/Core` mappából) csomagolja egy parancssoros programba,
amely stdin-ről parancsot fogad (`msg`, `file`, `accept`, `group`, `status`, `query`, `avatar`) és az eseményeket stdout-ra írja. **Csak tesztcélra van**,
a termékek semmit nem használnak belőle. Ha a `legacy-qt` mappát törlöd, a futtató forrásai a repó korábbi történetéből (a „Kotlin/C# újraírás” előtti állapot)
visszaállíthatók.

## Mit találtak ezek a tesztek (a kód a javított állapotot tükrözi)

* Az `accept` üzenetnek tartalmaznia kell a `filesize`-t, különben az eredeti fogadó sosem fejezi be az átvitelt.
* Az eredeti fogadó legfeljebb 65535 bájtot olvas `readyRead()`-enként, és nem ürít hátralékot → 65535 bájtos küldési darabok + pacing.
* A fogadó oldali szerver-socketen nagy vevőpuffer kell, különben az eredeti *küldő* ritkán megcsonkítja az utolsó darabot.
* Két egyszerre induló kliens versenyhelyzete miatt a visszafejtés a kapcsolat saját kulcsával, majd a felhasználó legutóbbi kulcsával történik.

Részletek: [`../PROTOCOL.md`](../PROTOCOL.md).
