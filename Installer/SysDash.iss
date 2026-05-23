[Setup]
AppName=SysDash
AppVersion=1.0.0
DefaultDirName={pf}\SysDash
DefaultGroupName=SysDash
OutputBaseFilename=SysDashSetup
Compression=lzma
SolidCompression=yes

[Files]
; The script lives in Installer\, so reference the publish folder one level up
Source: "..\\publish\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SysDash"; Filename: "{app}\SysDash.exe"
Name: "{commondesktop}\SysDash"; Filename: "{app}\SysDash.exe"

[Run]
Filename: "{app}\SysDash.exe"; Description: "Launch SysDash"; Flags: nowait postinstall skipifsilent