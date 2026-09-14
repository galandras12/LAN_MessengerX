# LAN Messenger — Windows kliens

Ez a mappa az eredeti LAN Messenger (QualiaTech, lanmessenger-master) Windows/asztali
kliensének modernizált forrását tartalmazza. Az eredeti, változatlan README és
platform-specifikus build-jegyzetek itt maradtak referenciaként:
[`README-original.md`](README-original.md), [`PLATFORM_SPECIFIC.md`](PLATFORM_SPECIFIC.md).

## Jelenlegi állapot (lásd a teljes tervet: `/root/.claude/plans/reflective-herding-dolphin.md`
a fejlesztői munkamenetben, vagy kérd el a modernizációs terv összefoglalóját)

Ez a munkamenet a modernizációs terv **Fázis 0–1** részét valósította meg:

- ✅ A platformfüggetlen hálózati/protokoll/titkosítási réteg kiemelve a
  [`/Core`](../Core) mappába, ezt linkeli be ez a projekt (`lmc.pro`).
- ✅ Javítva egy konkrét, éles összeomlás-forrás: a TCP üzenetkeretezés
  (`Core/src/netstreamer.cpp`, `MsgStream::readyRead`) nem kezelte helyesen,
  ha a 4 bájtos hosszfejléc vagy az üzenettörzs több hálózati olvasásra
  töredezett szét — ez időszakos, nehezen reprodukálható összeomlásokat
  okozhatott.
- ✅ Javítva egy **use-after-free/double-free hiba** a titkosítási rétegben
  (`Core/src/crypto.cpp`): az `encrypt()`/`decrypt()` minden hívása egy
  OpenSSL kontextus-wrappert **érték szerint** másolt ki egy `QMap`-ból, majd
  a helyi másolat destruktora felszabadította a wrapper által birtokolt
  `EVP_CIPHER_CTX*`-et — miközben a térképben tárolt eredeti még mindig
  ugyanarra a felszabadított memóriára mutatott. Ez OpenSSL 1.1+/3.x ellen
  szinte minden titkosított üzenetváltásnál heap-korrupciót, azaz
  időszakos összeomlást okozott volna. Most a térképek pointert tárolnak,
  másolás nélkül.
- ✅ Javítva egy memóriaszivárgás a fájlátvitelben (`FileSender`/`FileReceiver`
  sosem szabadította fel a belső puffert).
- ✅ A nem használt `sql` Qt modul és a Symbian/Maemo5-specifikus, holt
  `lmcapp.pro` kódágak eltávolítva.
- ⏳ **Még nincs kész**: a tényleges Qt 6 portolás (Qt5/Qt4-es elavult API-k,
  pl. `QRegExp`, `Q_WS_*` makrók cseréje — 11 érintett fájl), az OpenSSL 3.x
  ellen tényleges build tesztelése, a telepítő (NSIS → Inno Setup/MSIX)
  cseréje. Ez a terv Fázis 2-je, külön munkamenetben.

## Build előfeltételek

- Qt 6 LTS (a projekt jelenleg még Qt5-ös API-kat is használ — lásd fent,
  emiatt **Qt 6 alatt egyelőre nem fordul le hiba nélkül**, ez a Fázis 2
  munka tárgya)
- OpenSSL 3.x fejlesztői csomag — az `include` és `lib` mappáit másold ide:
  `Windows/openssl/include`, `Windows/openssl/lib` (a `.pro` fájl ezt várja).
  A Windows-os OpenSSL 3.x disztribúciók általában `libcrypto.lib` néven
  adják az import library-t — ha a tiéd más néven csomagolja, igazítsd a
  `lmc/src/lmc.pro` végén lévő `LIBS +=` sort.
- A `lmcapp` alprojektet (egyedi single-instance könyvtár) előbb kell
  buildelni, lásd `PLATFORM_SPECIFIC.md`.

## Mappa-elrendezés

```
Windows/
├── lmc/src/       - a fő alkalmazás (UI: mainwindow, chatwindow, stb. + lmc.pro)
├── lmcapp/        - egyedi single-instance/singleapplication könyvtár
├── setup/         - telepítő-csomagoló szkriptek (win32/x11/mac)
└── build_windows.bat
```

A hálózati/protokoll/titkosítási/előzmény kód a [`/Core`](../Core) mappában van,
onnan fordítja be közvetlenül a `lmc.pro`.
