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
REM lmcapp.pro has no win32 DESTDIR (only a unix one - see the .pro), so
REM the build output lands right here in .\src, not in a ..\lib that
REM doesn't even exist. lmc.pro's LMCAPP_PATH lookup (OUT_PWD with "lmc"
REM replaced by "lmcapp") expects it here too - renaming into a ..\lib
REM that was never created just silently no-ops and lmc's link step
REM then fails with "cannot find -llmcapp".
if exist liblmcapp2.a move liblmcapp2.a liblmcapp.a

cd ..\..\lmc\src
REM lmc.pro itself now compiles every .ts to resources\lang\*.qm as a
REM side effect of qmake parsing it (a system() call in the .pro), so
REM there's no separate translation-compiling step needed here anymore.
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
REM see the mingw64 branch above for why this renames in the current
REM directory rather than in a ..\lib that lmcapp.pro never creates on win32
if exist lmcapp2.lib move lmcapp2.lib lmcapp.lib

cd ..\..\lmc\src
REM see the mingw64 branch above - lmc.pro compiles translations itself now
qmake lmc.pro CONFIG+=x86_64 CONFIG-=debug CONFIG+=release
nmake
goto endmake

:endmake
