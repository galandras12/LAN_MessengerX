; Inno Setup 6 script. Build with:  iscc installer\LANMessengerX.iss   (publish-windows.bat does it when Inno Setup is installed)
; Expects dist\win-x64\LanMessengerX.exe from publish-windows.bat.
#define AppName "LAN Messenger X"
#define AppVersion "3.1.0"
#define AppExe "LanMessengerX.exe"

[Setup]
AppId={{6B0E7F3E-3E5B-4C3B-9F63-1A2D6E5C0B1D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=galandras12
AppPublisherURL=https://github.com/galandras12/LAN_MessengerX
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=LANMessengerX-{#AppVersion}-setup
SetupIconFile=..\src\LanMessengerX.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "autostart"; Description: "Start LAN Messenger X when I sign in"; Flags: unchecked

[Files]
Source: "..\dist\win-x64\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: autostart

[Run]
; Let the messenger through the firewall on private networks (discovery uses UDP 50000, chat/files TCP 50000).
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""LAN Messenger X (TCP)"" dir=in action=allow program=""{app}\{#AppExe}"" protocol=TCP localport=50000 profile=private"; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""LAN Messenger X (UDP)"" dir=in action=allow program=""{app}\{#AppExe}"" protocol=UDP localport=50000 profile=private"; Flags: runhidden
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""LAN Messenger X (TCP)"""; Flags: runhidden; RunOnceId: "FwTcp"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""LAN Messenger X (UDP)"""; Flags: runhidden; RunOnceId: "FwUdp"
