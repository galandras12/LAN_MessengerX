; LAN Messenger X - Inno Setup installation script
; Replaces the original NSIS-based setup.nsi (kept in this folder for
; reference) - see Windows/README.md for why and what changed.
;
; This is a from-scratch rewrite modeled closely on setup.nsi's actual
; behavior (traced section by section, not guessed): same install
; location convention, same Start Menu shortcuts, same Windows Firewall
; exception, same "run the app once with /silent /sync /quit right after
; install" step, and the same optional history/settings cleanup prompt on
; uninstall. It has NOT been run through a real Inno Setup compiler in
; this environment (none is available here) - see Windows/README.md for
; the full list of what is and is not verified.
;
; ProductName/ProductVersion below must be kept in sync by hand with
; IDA_TITLE/IDA_VERSION in Core/src/definitions.h - unlike the .pro files,
; Inno Setup's preprocessor cannot #include a C++ header directly.

#define ProductName "LAN Messenger X"
#define ProductVersion "2.0.10"
#define CompanyName "LAN Messenger X"
#define AppExeName "lmc.exe"
#define ProductUrl "http://lanmessenger.github.io"

[Setup]
; Fixed AppId (a real GUID, generated once for this rewrite) - Inno Setup
; uses this, not the product name, to recognize "is this app already
; installed" across versions/reinstalls. Keep this exact value in every
; future release; do not regenerate it. The doubled leading brace is
; Inno's required escape for a literal "{" here (an unescaped "{...}"
; value is parsed as a {constant} reference, same as {app} below) - this
; is the standard idiom Inno Setup's own script wizard generates.
AppId={{A1D2F6E4-9B3C-4E7A-8F2D-6C7B1E9A4D50}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppPublisher={#CompanyName}
AppPublisherURL={#ProductUrl}
AppSupportURL={#ProductUrl}
AppUpdatesURL={#ProductUrl}
VersionInfoVersion={#ProductVersion}
DefaultDirName={autopf}\{#ProductName}
DefaultGroupName={#ProductName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputBaseFilename=lanmessengerx-{#ProductVersion}-win32-setup
OutputDir=.
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=package\images\setup.ico
LicenseFile=package\eula\license.txt
DisableWelcomePage=no
DisableProgramGroupPage=yes

; The original NSIS script showed a header/banner image throughout the
; wizard (package\images\header-r.bmp, banner.bmp) - Inno's own
; WizardImageFile/WizardSmallImageFile expect different fixed pixel sizes
; than those NSIS MUI2 bitmaps, so they are not reused as-is here; a
; matching pair of Inno-sized images would need to be produced separately
; if the original artwork should carry over. Cosmetic only - see
; Windows/README.md.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Everything windeployqt (run against the Release build of lmc.exe)
; collects into its output folder - the Qt6 DLLs, platform/style/image-
; format plugins, and (if present alongside them) the OpenSSL 3.x
; libcrypto-3-x64.dll/libssl-3-x64.dll the modernized crypto.cpp needs at
; runtime (see Core/README.md's OpenSSL section) - copied wholesale
; rather than named one by one like the old NSIS script's two
; hand-listed OpenSSL 1.0.2 DLLs (libeay32.dll/ssleay32.dll), since a
; fixed DLL list silently goes stale on the next Qt/OpenSSL bump. Point
; SourceDir at that windeployqt output folder when compiling this script,
; e.g.: iscc setup.iss /DSourceDir=..\..\lmc\build-release-deploy
#ifndef SourceDir
  #define SourceDir "..\..\build-release-deploy"
#endif
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "\sounds,\lang"; Flags: ignoreversion recursesubdirs createallsubdirs

; Loose, user-overridable resource folders - deployed next to the exe,
; matching Core/src/stdlocation.h's sysLangDir()/sysThemeDir() (loose,
; on-disk) vs resLangDir()/resThemeDir() (baked into resource.qrc)
; distinction: these are the "install-time defaults", separate from the
; per-user copies StdLocation::userLangDir()/userThemeDir() sync into
; AppData later.
Source: "..\..\lmc\src\resources\sounds\*"; DestDir: "{app}\sounds"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\lmc\src\resources\lang\*"; DestDir: "{app}\lang"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\lmc\src\resources\text\license.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#ProductName}"; Filename: "{app}\{#AppExeName}"; Comment: "Send or receive instant messages."
Name: "{group}\Uninstall {#ProductName}"; Filename: "{uninstallexe}"; Comment: "Uninstall {#ProductName}"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Parity with the old NSIS script's bookkeeping key, in case any external
; deployment tooling reads it - nothing in the app itself does (checked:
; no HKLM\...\LAN Messenger X reads anywhere in Windows/lmc/src).
Root: HKLM; Subkey: "SOFTWARE\{#CompanyName}\{#ProductName}"; ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"
Root: HKLM; Subkey: "SOFTWARE\{#CompanyName}\{#ProductName}"; ValueType: string; ValueName: "Version"; ValueData: "{#ProductVersion}"

[Run]
; Windows Firewall exception - the old NSIS script used the third-party
; nsisFirewall plugin's AddAuthorizedApplication; netsh advfirewall is
; the built-in, plugin-free equivalent and needs both directions since
; AddAuthorizedApplication authorized the app generally, not just one
; direction. Errors are swallowed (runhidden + no "waituntilterminated"
; failure surfaced) rather than failing the whole install if, e.g., the
; Firewall service is disabled - matches the old script's "Pop $0" (read
; but never checked).
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""{#ProductName}"" dir=in action=allow program=""{app}\{#AppExeName}"" enable=yes"; Flags: runhidden; StatusMsg: "Adding Windows Firewall exception..."
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""{#ProductName}"" dir=out action=allow program=""{app}\{#AppExeName}"" enable=yes"; Flags: runhidden

; Matches setup.nsi's post-install "sync startup registry entry from the
; settings file" step exactly (same flags, same intent).
Filename: "{app}\{#AppExeName}"; Parameters: "/silent /sync /quit"; Flags: runhidden waituntilterminated; StatusMsg: "Finishing setup..."

Filename: "{app}\{#AppExeName}"; Description: "Launch {#ProductName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Ask the app to terminate itself cleanly first - matches setup.nsi's
; un.IsAppRunning custom page (skipped here for silent uninstalls the
; same way the original skips its own page callback in that case).
Filename: "{app}\{#AppExeName}"; Parameters: "/silent /term"; Flags: runhidden; RunOnceId: "TerminateApp"

Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""{#ProductName}"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"

; The actual history/settings deletion flags (built in code below, see
; CurUninstallStepChanged) - passed to the app itself, same as
; setup.nsi's "$DeleteHistory $DeleteSettings" ExecWait, so the app's own
; existing cleanup code does the work rather than the installer touching
; those files directly.
Filename: "{app}\{#AppExeName}"; Parameters: "{code:GetUninstallDataFlags} /silent /unsync /quit"; Flags: runhidden; RunOnceId: "CleanupAppData"

[UninstallDelete]
; Matches setup.nsi's forced recursive removal of these specific
; subfolders under AppDataDir.
Type: filesandordirs; Name: "{localappdata}\{#CompanyName}\{#ProductName}\cache"
Type: filesandordirs; Name: "{localappdata}\{#CompanyName}\{#ProductName}\lang"
Type: filesandordirs; Name: "{localappdata}\{#CompanyName}\{#ProductName}\logs"
Type: filesandordirs; Name: "{localappdata}\{#CompanyName}\{#ProductName}\themes"
; Matches setup.nsi's plain (non-recursive) RMDir calls on the containing
; folders - "delete only if now empty", so another app's data under the
; same company folder, or files the user actually wants under Received
; Files, are left alone rather than force-deleted.
Type: dirifempty; Name: "{localappdata}\{#CompanyName}\{#ProductName}"
Type: dirifempty; Name: "{localappdata}\{#CompanyName}"
Type: dirifempty; Name: "{userappdata}\{#CompanyName}"
Type: dirifempty; Name: "{userdocs}\Received Files"

[Code]
var
  UninstallDataFlags: String;

function GetUninstallDataFlags(Param: String): String;
begin
  Result := UninstallDataFlags;
end;

{ Matches setup.nsi's un.OptionsPageShow custom page's intent (offer to
  delete conversation history and/or saved preferences on uninstall,
  default to keeping both) using two plain Yes/No prompts instead of a
  custom checkbox page - simpler and more certain to behave correctly
  than trying to reproduce nsDialogs-style custom controls in Inno's
  Pascal Script, at the cost of two separate prompts instead of one
  combined page. Runs before Inno's own "are you sure?" confirmation and
  the uninstall progress form, and - like the original's "IfSilent"
  branch skipping its own page - is skipped entirely for a silent
  uninstall, which then deletes neither. }
function InitializeUninstall(): Boolean;
begin
  UninstallDataFlags := '';
  if not UninstallSilent then begin
    if MsgBox('Also delete your conversation history and received-file records?',
        mbConfirmation, MB_YESNO) = IDYES then
      UninstallDataFlags := UninstallDataFlags + ' /nohistory /nofilehistory';
    if MsgBox('Also delete your saved preferences?',
        mbConfirmation, MB_YESNO) = IDYES then
      UninstallDataFlags := UninstallDataFlags + ' /noconfig';
  end;
  Result := True;
end;
