@echo off
REM Packages an already-built Release lmc.exe into a single, ready-to-run
REM folder at the repo root: Windows-Release\ - so after a rebuild you
REM run ONE command and get a folder you can just double-click lmc.exe
REM in, instead of manually windeployqt-ing and copying OpenSSL DLLs/
REM resources by hand every time (see BUILD.md 1.4, which this
REM automates).
REM
REM What it does, in order:
REM   1. Finds the most recently built lmc.exe anywhere under Windows\lmc
REM      (works whether it landed in src\, src\release\, or a Qt
REM      Creator/Visual Studio shadow-build folder - never assumes one
REM      fixed path, since that varies by build method/Qt version).
REM   2. Wipes and recreates Windows-Release\, copies lmc.exe into it.
REM   3. Runs windeployqt --release against it (pulls in every Qt DLL/
REM      plugin lmc.exe actually needs).
REM   4. Finds and copies the OpenSSL 3.x runtime DLLs
REM      (libcrypto-3-x64.dll/libssl-3-x64.dll) - checks a few known
REM      real locations first (see BUILD.md 1.2 step 4/FAQ.md), then
REM      falls back to the same "dir /s /b" search BUILD.md documents,
REM      across every local fixed drive.
REM   5. Copies the same loose resource folders setup.iss's own [Files]
REM      section deploys at install time (sounds, lang, license.txt) -
REM      so this folder is genuinely standalone-runnable, not just
REM      windeployqt's raw output.
REM
REM Usage: package_release.bat <kit>
REM   kit: mingw64 | msvc2022_64
REM Run from the Windows\ folder, AFTER a Release build (build_windows.bat
REM <kit>, or a Qt Creator/Visual Studio Release build) - this script only
REM gathers/deploys, it does not compile anything itself.

setlocal enabledelayedexpansion

if "%~1"=="" (
    echo Usage: package_release.bat ^<mingw64^|msvc2022_64^>
    exit /b 1
)

set KIT=%~1
set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..
set OUT_DIR=%REPO_ROOT%\Windows-Release

if /i "%KIT%"=="mingw64" (
    REM adjust this path to match your local Qt 6 mingw installation -
    REM same convention/default as build_windows.bat's own mingw64 branch
    set QT_BIN=C:\Qt\6.8.0\mingw_64\bin
) else if /i "%KIT%"=="msvc2022_64" (
    REM adjust this path to match your local Qt 6 msvc installation -
    REM same convention/default as build_windows.bat's own msvc2022_64 branch
    set QT_BIN=C:\Qt\6.8.0\msvc2022_64\bin
) else (
    echo Unknown kit "%KIT%" - expected mingw64 or msvc2022_64.
    exit /b 1
)

if not exist "%QT_BIN%\windeployqt.exe" (
    echo windeployqt.exe not found at "%QT_BIN%".
    echo Edit QT_BIN near the top of this script to match your actual Qt
    echo installation - the same path you already adjusted in
    echo build_windows.bat to build with.
    exit /b 1
)

echo Searching for the most recently built lmc.exe under Windows\lmc ...
set LMC_EXE=
for /f "delims=" %%F in ('dir /b /s /a-d /o-d "%SCRIPT_DIR%lmc\lmc.exe" 2^>nul') do (
    if not defined LMC_EXE set LMC_EXE=%%F
)
if not defined LMC_EXE (
    echo Could not find a built lmc.exe anywhere under Windows\lmc - build
    echo a Release configuration first ^(build_windows.bat %KIT%, or Qt
    echo Creator/Visual Studio^) before running this script.
    exit /b 1
)
echo Found: %LMC_EXE%

if exist "%OUT_DIR%" rmdir /s /q "%OUT_DIR%"
mkdir "%OUT_DIR%"

echo Copying lmc.exe ...
copy /y "%LMC_EXE%" "%OUT_DIR%\" >nul

echo Running windeployqt ...
"%QT_BIN%\windeployqt.exe" --release "%OUT_DIR%\lmc.exe"
if errorlevel 1 (
    echo windeployqt failed - see output above.
    exit /b 1
)

echo Searching for OpenSSL 3.x runtime DLLs ...
set OPENSSL_DLL_DIR=
for %%D in (
    "%OPENSSL_ROOT_DIR%\bin"
    "C:\Program Files\OpenSSL-Win64\bin"
    "C:\Program Files\OpenSSL-Win64"
    "%REPO_ROOT%\openssl\bin"
) do (
    if not defined OPENSSL_DLL_DIR (
        if exist "%%~D\libcrypto-3-x64.dll" set OPENSSL_DLL_DIR=%%~D
    )
)

if not defined OPENSSL_DLL_DIR (
    echo Not found in the usual spots - falling back to a full search of
    echo every local fixed drive ^(same technique as BUILD.md 1.2 step 4
    echo - can take a little while^) ...
    for %%V in (C D E F) do (
        if not defined OPENSSL_DLL_DIR (
            if exist "%%V:\" (
                for /f "delims=" %%P in ('dir /b /s "%%V:\libcrypto-3-x64.dll" 2^>nul') do (
                    if not defined OPENSSL_DLL_DIR set OPENSSL_DLL_DIR=%%~dpP
                )
            )
        )
    )
)

if not defined OPENSSL_DLL_DIR (
    echo WARNING: could not find libcrypto-3-x64.dll anywhere. lmc.exe
    echo will not start without it - copy it ^(and libssl-3-x64.dll, if
    echo your distribution ships it separately^) into "%OUT_DIR%" by
    echo hand, or edit this script's search list to match where you
    echo actually installed OpenSSL - see BUILD.md 1.2 step 4/FAQ.md.
) else (
    echo Found OpenSSL DLLs in: %OPENSSL_DLL_DIR%
    copy /y "%OPENSSL_DLL_DIR%\libcrypto-3-x64.dll" "%OUT_DIR%\" >nul
    if exist "%OPENSSL_DLL_DIR%\libssl-3-x64.dll" (
        copy /y "%OPENSSL_DLL_DIR%\libssl-3-x64.dll" "%OUT_DIR%\" >nul
    )
)

echo Copying loose resources (sounds, lang, license - same as setup.iss) ...
xcopy /y /e /i "%SCRIPT_DIR%lmc\src\resources\sounds" "%OUT_DIR%\sounds\" >nul
xcopy /y /e /i "%SCRIPT_DIR%lmc\src\resources\lang" "%OUT_DIR%\lang\" >nul
copy /y "%SCRIPT_DIR%lmc\src\resources\text\license.txt" "%OUT_DIR%\" >nul

echo.
echo Done. Ready-to-run folder: %OUT_DIR%
echo Just double-click lmc.exe in there to start the app - no separate
echo windeployqt/copy steps needed next time either, just re-run this
echo script after every rebuild.

endlocal
