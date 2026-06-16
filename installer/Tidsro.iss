; Tidsro installer — per-user, no admin. Wraps the self-contained single-file build.
; The version is passed in by publish.ps1 via /DAppVersion=<x.y.z>.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "Tidsro"
#define AppPublisher "Malin Fossum"
#define AppExeName "Tidsro.exe"
#define AppUrl "https://github.com/malinfossum/tidsro"

[Setup]
AppId={{AD23F2BA-DA9E-4A84-A72A-B5D18F51AE18}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\dist
OutputBaseFilename=Tidsro-Setup
SetupIconFile=..\src\Tidsro\Assets\icons\tidsro.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the launch-at-startup entry the app may have written, so an uninstall leaves nothing behind.
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v Tidsro /f"; Flags: runhidden; RunOnceId: "DelTidsroAutostart"
