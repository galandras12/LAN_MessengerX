@echo off
REM Packages an already-built Release lmc.exe into a single, ready-to-run
REM folder at the repo root: Windows-Release\ - so after a rebuild you
REM run ONE command (or just double-click this file) and get a folder
REM you can double-click lmc.exe in, instead of manually windeployqt-ing
REM and copying OpenSSL DLLs/resources by hand every time (see BUILD.md
REM 1.4, which this automates).
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
REM Usage: just double-click this file in Explorer, or from a command
REM prompt: package_release.bat [mingw64|msvc2022_64]
REM   The kit argument is OPTIONAL - if omitted (e.g. when double-
REM   clicked, which can't pass one), it auto-picks whichever of the two
REM   QT_BIN paths below actually has windeployqt.exe, trying MinGW
REM   first. Edit MINGW_QT_BIN/MSVC_QT_BIN below to match your actual Qt
REM   installation(s) - same convention as build_windows.bat.
REM Run AFTER a Release build (build_windows.bat <kit>, or a Qt Creator/
REM Visual Studio Release build) - this script only gathers/deploys, it
REM does not compile anything itself. The window stays open and waits
REM for a keypress at the end (success or failure) so double-clicking
REM never just flashes and vanishes.

setlocal enabledelayedexpansion

REM adjust these two to match your local Qt 6 installation(s) - same
REM convention/defaults as build_windows.bat's own mingw64/msvc2022_64
REM branches
set MINGW_QT_BIN=C:\Qt\6.8.0\mingw_64\bin
set MSVC_QT_BIN=C:\Qt\6.8.0\msvc2022_64\bin

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..
set OUT_DIR=%REPO_ROOT%\Windows-Release

if /i "%~1"=="mingw64" (
    set QT_BIN=%MINGW_QT_BIN%
) else if /i "%~1"=="msvc2022_64" (
    set QT_BIN=%MSVC_QT_BIN%
) else if "%~1"=="" (
    if exist "%MINGW_QT_BIN%\windeployqt.exe" (
        set QT_BIN=%MINGW_QT_BIN%
    ) else if exist "%MSVC_QT_BIN%\windeployqt.exe" (
        set QT_BIN=%MSVC_QT_BIN%
    ) else (
        echo Could not find windeployqt.exe in either configured Qt kit:
        echo   %MINGW_QT_BIN%
        echo   %MSVC_QT_BIN%
        echo Edit MINGW_QT_BIN/MSVC_QT_BIN near the top of this script to
        echo match your actual Qt installation.
        goto :fail
    )
) else (
    echo Unknown kit "%~1" - expected mingw64 or msvc2022_64 ^(or no
    echo argument at all, to auto-detect^).
    goto :fail
)

if not exist "%QT_BIN%\windeployqt.exe" (
    echo windeployqt.exe not found at "%QT_BIN%".
    echo Edit MINGW_QT_BIN/MSVC_QT_BIN near the top of this script to
    echo match your actual Qt installation - the same paths you already
    echo adjusted in build_windows.bat to build with.
    goto :fail
)
echo Using Qt kit: %QT_BIN%

echo Searching for the most recently built lmc.exe under Windows\lmc ...
set LMC_EXE=
for /f "delims=" %%F in ('dir /b /s /a-d /o-d "%SCRIPT_DIR%lmc\lmc.exe" 2^>nul') do (
    if not defined LMC_EXE set LMC_EXE=%%F
)
if not defined LMC_EXE (
    echo Could not find a built lmc.exe anywhere under Windows\lmc - build
    echo a Release configuration first ^(build_windows.bat, or Qt
    echo Creator/Visual Studio^) before running this script.
    goto :fail
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
    goto :fail
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
echo.
pause
endlocal
exit /b 0

:fail
echo.
echo FAILED - see the messages above.
echo.
pause
endlocal
exit /b 1
