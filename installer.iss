; HA DeskLink Inno Setup Installer
; Version is injected by CI via sed — see .github/workflows/build.yml
#define AppName "HA DeskLink"
#define AppExe "HA_DeskLink.exe"
#define AppVersion "0.0.0"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Fabian Kirchweger
AppPublisherURL=https://github.com/TechFlipsi/ha-desklink-windows
AppSupportURL=https://github.com/TechFlipsi/ha-desklink-windows/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=output
OutputBaseFilename=HA_DeskLink_Setup_{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=force
RestartApplications=no
SetupIconFile=src\HaDeskLink\Assets\icon.ico

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{#AppName} starten"; Flags: nowait postinstall runasoriginaluser

[UninstallDelete]
Type: filesandordirs; Name: "{app}"