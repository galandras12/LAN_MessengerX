@echo on
REM Build script for the LAN Messenger Windows client.
REM
REM Prerequisites (not included in this repo, see Windows/README.md):
REM   - Qt 6 LTS (e.g. 6.8/6.9) with the desired kit (mingw or msvc) installed
REM   - OpenSSL 3.x development package, with its "include" and "lib"
REM     folders placed at openssl\include and openssl\lib, at the repo root
REM     (a sibling of the Windows and Core folders)
REM
REM Builds, in order: Core (the lmccore static library shared with the
REM planned Android client), lmcapp (single-instance helper library), then
REM the lmc Windows client itself, which links the other two.
REM
REM Usage: build_windows.bat <kit>
REM   kit: mingw64 | msvc2022_64
REM Run from the Windows\ folder.

SET project_dir="%cd%"

goto %1

:mingw64
REM adjust this path to match your local Qt 6 mingw installation
set PATH=C:\Qt\6.8.0\mingw_64\bin;C:\Qt\Tools\mingw1310_64\bin;%PATH%

cd ..\Core
qmake Core.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make

cd ..\Windows\lmcapp\src
qmake lmcapp.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make
if exist ..\lib\liblmcapp2.a move ..\lib\liblmcapp2.a ..\lib\liblmcapp.a

cd ..\..\lmc\src
qmake lmc.pro -spec win32-g++ CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
mingw32-make
goto endmake

:msvc2022_64
REM adjust this path to match your local Qt 6 msvc installation
set PATH=C:\Qt\6.8.0\msvc2022_64\bin;%PATH%
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" x64
@echo on

cd ..\Core
qmake Core.pro CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
nmake

cd ..\Windows\lmcapp\src
qmake lmcapp.pro CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
nmake
if exist ..\lib\lmcapp2.lib move ..\lib\lmcapp2.lib ..\lib\lmcapp.lib

cd ..\..\lmc\src
qmake lmc.pro CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
nmake
goto endmake

:endmake
