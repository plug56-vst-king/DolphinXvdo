; DolphinXvdo Audio Controller - All-in-One Installer Script
; Inno Setup 6 Script
; Self-contained APO with Embedded ReaComp & EQ

#define MyAppName "DolphinXvdo"
#define MyAppVersion "2.3"
#define MyAppPublisher "DolphinXvdo"
#define MyAppExeName "DolphinXVDO.exe"

[Setup]
AppId={{E1A7C520-2F8B-4B39-9591-912A88DF9134}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\DolphinXVDO
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=DolphinXvdo-Setup-v{#MyAppVersion}
SetupIconFile=..\logo\logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "autostart"; Description: "Start DolphinXvdo automatically with Windows (System Tray)"; GroupDescription: "Windows Startup:"; Flags: checkedonce

[Files]
; 1. All DolphinXVDO files (Core app, Node server, Web UI, Engine, Plugins, Config)
Source: "..\DolphinXVDO\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace; Excludes: "*.old,*.bak,*.tmp,*.original,*_old.exe,*_running.exe,DolphinXVDO_*.exe,dolphinxvdo_state.ini,*Merlon Pubg.ini*"
Source: "..\DolphinXVDO\plugins\reacomp-standalone.dll"; DestDir: "{app}\engine\VSTPlugins"; Flags: ignoreversion restartreplace
Source: "..\DolphinXVDO\config\*"; DestDir: "{app}\config"; Flags: ignoreversion recursesubdirs createallsubdirs; Permissions: users-full; Excludes: "*Merlon Pubg.ini*"

[Registry]
; Enable system-wide APO processing in Windows Audio Engine
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Audio"; ValueType: dword; ValueName: "DisableProtectedAudioDG"; ValueData: 1; Flags: uninsdeletevalue

; Point APO Engine to DolphinXVDO directories
Root: HKLM; Subkey: "Software\EqualizerAPO"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}\engine"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\EqualizerAPO"; ValueType: string; ValueName: "ConfigPath"; ValueData: "{app}\config"; Flags: uninsdeletekey

; Autostart with Windows minimized to System Tray
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DolphinXVdo"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\logo.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\logo.ico"; Tasks: desktopicon

[Run]
; Step 1: Register DolphinXVDO APO Engine COM DLL
Filename: "{sys}\regsvr32.exe"; Parameters: "/s ""{app}\engine\EqualizerAPO.dll"""; StatusMsg: "Registering DolphinXVDO Audio Engine..."; Flags: runhidden waituntilterminated

; Step 2: Open Device Selector so the user selects audio devices before using the app
Filename: "{app}\engine\DeviceSelector.exe"; Parameters: "-stylesheet ""{app}\engine\DeviceSelector.qss"""; WorkingDir: "{app}\engine"; StatusMsg: "Opening Audio Device Selector (ตั้งค่าเลือกอุปกรณ์เสียง)..."; Flags: waituntilterminated skipifsilent

; Step 3: Launch DolphinXVDO Controller after devices are configured
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{userappdata}\DolphinXVDO"
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"
Type: files; Name: "{userstartup}\DolphinXvdo.lnk"
Type: files; Name: "{userstartup}\DolphinXVDO.lnk"
Type: files; Name: "{autodesktop}\{#MyAppName}.lnk"

[UninstallRun]
; Step 1: Restore endpoint settings
Filename: "{app}\engine\DeviceSelector.exe"; Parameters: "/u"; Flags: runhidden; RunOnceId: "RestoreEndpoints"

; Step 2: Unregister COM DLL
Filename: "{sys}\regsvr32.exe"; Parameters: "/u /s ""{app}\engine\EqualizerAPO.dll"""; Flags: runhidden; RunOnceId: "UnregisterCOM"

; Step 3: Delete Autostart entries
Filename: "{cmd}"; Parameters: "/c del /f /q ""{userstartup}\{#MyAppName}.lnk"""; Flags: runhidden; RunOnceId: "DeleteStartupLnk"
Filename: "{cmd}"; Parameters: "/c del /f /q ""{userstartup}\DolphinXvdo.lnk"""; Flags: runhidden; RunOnceId: "DeleteStartupLnk2"
Filename: "{cmd}"; Parameters: "/c reg.exe delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""DolphinXVdo"" /f"; Flags: runhidden; RunOnceId: "DeleteRunRegistry"
Filename: "{cmd}"; Parameters: "/c reg.exe delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""DolphinXvdo"" /f"; Flags: runhidden; RunOnceId: "DeleteRunRegistry2"

[Code]
procedure KillProcess(const ExeName: string);
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/f /im ' + ExeName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Automatically stop Windows Audio to unlock DLLs during install
procedure StopAudioService();
var
  ResultCode: Integer;
begin
  Exec('net.exe', 'stop audiosrv /y', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/f /im audiodg.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StartAudioService();
var
  ResultCode: Integer;
begin
  Exec('net.exe', 'start audiosrv', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    KillProcess('DolphinXVDO.exe');
    KillProcess('node.exe');
    StopAudioService();
  end
  else if CurStep = ssPostInstall then
  begin
    StartAudioService();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  AppDataDir: string;
  AppDir: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Step 1: Force terminate running DolphinXVDO & node processes
    KillProcess('DolphinXVDO.exe');
    KillProcess('node.exe');

    // Step 2: Stop Windows Audio Service so audiodg.exe releases EqualizerAPO.dll lock
    StopAudioService();

    // Step 3: Unhook APO from all audio endpoints
    if FileExists(ExpandConstant('{app}\engine\DeviceSelector.exe')) then
    begin
      Exec(ExpandConstant('{app}\engine\DeviceSelector.exe'), '/u', ExpandConstant('{app}\engine'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;

    // Step 4: Unregister COM DLL
    if FileExists(ExpandConstant('{app}\engine\EqualizerAPO.dll')) then
    begin
      Exec('regsvr32.exe', ExpandConstant('/u /s "{app}\engine\EqualizerAPO.dll"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;

    // Step 5: Start Audio Service back up so normal Windows audio works immediately
    StartAudioService();
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    // Step 6: Clean up entire AppData folder (%APPDATA%\DolphinXVDO)
    AppDataDir := ExpandConstant('{userappdata}\DolphinXVDO');
    if DirExists(AppDataDir) then
    begin
      DelTree(AppDataDir, True, True, True);
    end;

    // Step 7: Clean up entire application directory ({app}) including all runtime files
    AppDir := ExpandConstant('{app}');
    if DirExists(AppDir) then
    begin
      DelTree(AppDir, True, True, True);
      if DirExists(AppDir) then
      begin
        Exec('cmd.exe', '/c rmdir /s /q "' + AppDir + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      end;
    end;
  end;
end;
