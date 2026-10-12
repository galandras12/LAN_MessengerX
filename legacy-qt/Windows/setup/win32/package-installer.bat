@echo off
rem Builds the Inno Setup installer (setup.iss) - the modern replacement
rem for the old makensis-based setup.bat/setup.nsi pair (still present in
rem this folder for reference, see Windows/README.md).
rem
rem Requires Inno Setup 6 (ISCC.exe) installed, and a Release build of
rem lmc.exe already processed with windeployqt into a single output
rem folder - point SourceDir at that folder below.

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DSourceDir=build-lmc-Desktop_Qt_6-Release\deploy setup.iss

rem setup.iss sets OutputDir=. so ISCC writes the installer directly into
rem this folder, same as the old setup.bat's makensis output.
for %%f in (lanmessengerx-*-setup.exe) do (
    move /Y "%%f" ..\%%~nf.exe
)

exit /b 0
