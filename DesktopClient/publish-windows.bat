@echo off
rem ---------------------------------------------------------------------------
rem  Builds LanMessengerX.exe (self-contained, single file, nothing else to
rem  install) and a zip of it. Double-click this file; the window stays open.
rem  Needs the .NET 8 SDK:  https://dotnet.microsoft.com/download/dotnet/8.0
rem  If Inno Setup 6 (iscc.exe) is installed, an installer is built as well.
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: the .NET 8 SDK was not found on PATH.
    echo Install it from https://dotnet.microsoft.com/download/dotnet/8.0 and run this again.
    goto :fail
)

echo Publishing win-x64 ...
dotnet publish src\LanMessengerX.App -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=None -p:DebugSymbols=false -o "dist\win-x64"
if errorlevel 1 (
    echo ERROR: dotnet publish failed - see the messages above.
    goto :fail
)

echo Creating dist\LANMessengerX-win-x64.zip ...
powershell -NoProfile -Command "Compress-Archive -Force -Path 'dist\win-x64\LanMessengerX.exe' -DestinationPath 'dist\LANMessengerX-win-x64.zip'"
if errorlevel 1 (
    echo ERROR: could not create the zip file.
    goto :fail
)

set ISCC=
where iscc >nul 2>nul && set ISCC=iscc
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set ISCC="%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set ISCC="%ProgramFiles%\Inno Setup 6\ISCC.exe"
if defined ISCC (
    echo Building the installer with Inno Setup ...
    %ISCC% installer\LANMessengerX.iss
    if errorlevel 1 echo WARNING: the installer could not be built; the exe and zip above are fine.
) else (
    echo Inno Setup 6 not found - skipping the installer ^(optional^).
)

echo.
echo DONE.  dist\win-x64\LanMessengerX.exe   dist\LANMessengerX-win-x64.zip
echo On first start Windows Firewall asks for permission - allow it on private networks.
pause
exit /b 0

:fail
echo.
echo FAILED.
pause
exit /b 1
