[Setup]
AppName=SysDash
AppVersion=1.0.0
DefaultDirName={pf64}\SysDash
DefaultGroupName=SysDash
OutputBaseFilename=SysDashSetup
Compression=lzma
SolidCompression=yes

[Files]
Source: "..\\publish\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SysDash"; Filename: "{app}\SysDash.exe"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

; This script expects the CI to replace {#PublishDir} with the actual publish folder path.