#define MyAppName "Ammar DM (32-bit)"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Ammar Al-Sumaidaie"
#define MyAppExeName "AmmarDM.exe"
#define MyAppSource "D:\AmmarDM\Publish\AmmarDM_x86"

[Setup]
AppId={{E5268B92-7A9B-4B6A-9142-CE6E18C7A311}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={pf}\AmmarDM
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=D:\AmmarDM\InstallerOutput
OutputBaseFilename=AmmarDM-v1.0.0-Setup-x86
SetupIconFile=D:\AmmarDM\Resources\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "تشغيل Ammar DM تلقائياً عند بدء تشغيل ويندوز"; GroupDescription: "إعدادات إضافية:"

[Files]
Source: "{#MyAppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "D:\AmmarDM\Resources\app_icon.ico"; DestDir: "{app}\Resources"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\app_icon.ico"; WorkingDir: "{app}"
Name: "{group}\إلغاء تثبيت {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\app_icon.ico"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AmmarDM"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart
Root: HKLM; Subkey: "Software\Google\Chrome\NativeMessagingHosts\com.ammardm.downloader"; ValueType: string; ValueName: ""; ValueData: "{app}\com.ammardm.downloader.json"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Microsoft\Edge\NativeMessagingHosts\com.ammardm.downloader"; ValueType: string; ValueName: ""; ValueData: "{app}\com.ammardm.downloader.json"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\com.ammardm.downloader"; ValueType: string; ValueName: ""; ValueData: "{app}\com.ammardm.downloader.json"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Edge\NativeMessagingHosts\com.ammardm.downloader"; ValueType: string; ValueName: ""; ValueData: "{app}\com.ammardm.downloader.json"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ManifestJson: string;
  ManifestPath: string;
  ExeEscaped: string;
begin
  if CurStep = ssPostInstall then
  begin
    ExeEscaped := ExpandConstant('{app}\{#MyAppExeName}');
    StringChangeEx(ExeEscaped, '\', '\\', True);
    
    ManifestJson := 
      '{'#13#10 +
      '  "name": "com.ammardm.downloader",'#13#10 +
      '  "description": "Ammar DM Native Messaging Host",'#13#10 +
      '  "path": "' + ExeEscaped + '",'#13#10 +
      '  "type": "stdio",'#13#10 +
      '  "allowed_origins": ['#13#10 +
      '    "chrome-extension://*",'#13#10 +
      '    "edge-extension://*"'#13#10 +
      '  ]'#13#10 +
      '}';
      
    ManifestPath := ExpandConstant('{app}\com.ammardm.downloader.json');
    SaveStringToFile(ManifestPath, ManifestJson, False);
  end;
end;
