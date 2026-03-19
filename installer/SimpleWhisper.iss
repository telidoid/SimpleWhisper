#ifndef Version
  #define Version "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\SimpleWhisper\bin\Release\net10.0\win-x64\publish"
#endif

[Setup]
AppId={{B5E7D3A0-SimpleWhisper-4F2A-9C1E-A8D0F3B6E5C2}
AppName=SimpleWhisper
AppVersion={#Version}
AppPublisher=SimpleWhisper
DefaultDirName={autopf}\SimpleWhisper
DefaultGroupName=SimpleWhisper
OutputBaseFilename=SimpleWhisper-{#Version}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\SimpleWhisper.exe
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SimpleWhisper"; Filename: "{app}\SimpleWhisper.exe"
Name: "{autodesktop}\SimpleWhisper"; Filename: "{app}\SimpleWhisper.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SimpleWhisper.exe"; Description: "Launch SimpleWhisper"; Flags: nowait postinstall skipifsilent
