; WinSpotLight installer (Inno Setup 6)
;
; Version is taken from the APP_VERSION environment variable and falls back
; to 1.0.0 for local builds. In CI it is injected by the release workflow.
#define MyAppVersion GetEnv("APP_VERSION")
#if MyAppVersion == ""
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "WinSpotLight"
#define MyAppExeName "WinSpotLight.exe"

[Setup]
AppId={{8B7A5C3D-2E1F-4A9B-8C6D-1F3E5A7B9C0D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=WinSpotLight
SetupIconFile=icon.ico
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=dist
OutputBaseFilename=WinSpotLight-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent runasoriginaluser
