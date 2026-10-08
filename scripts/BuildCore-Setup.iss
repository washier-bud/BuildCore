#define MyAppName "BuildCore"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "BuildCore"
#define MyAppExeName "BuildCore.exe"

[Setup]
AppId={{A2B9E6A2-8F3A-4E75-9B4A-1F7B0C2D9E61}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\BuildCore
DefaultGroupName={#MyAppName}
OutputDir=..\BuildCore\bin\releases
OutputBaseFilename=BuildCore-{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\BuildCore\Assets\BuildCore.ico
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=BuildCore PC performance optimization and system tuning.
VersionInfoProductName={#MyAppName}
OutputManifestFile=BuildCore-Setup-Manifest.txt

[Files]
Source: "..\BuildCore\bin\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: shellexec nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"