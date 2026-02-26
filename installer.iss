; Final Optimized Inno Setup Script for Pisonet Lockscreen App
#define MyAppName "Pisonet Lockscreen App"
#define MyAppVersion "1.3"
#define MyAppPublisher "Pisonet Dev"
#define MyAppExeName "PisonetLockscreenApp.exe"
#define MyWatchdogName "PisonetWatchdog.exe"
#define SourcePath "bin\Release\net9.0-windows\win-x64"

[Setup]
AppId={{A1B2C3D4-E5F6-4G7H-8I9J-K0L1M2N3O4P5}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=admin
OutputDir=dist\installer
OutputBaseFilename=PisonetLockscreenSetup
SetupIconFile=ayie.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
AppMutex=PisonetLockscreenMutex,PisonetWatchdogMutex

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main Application Files
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml,client_socket_log.txt,error_log.txt"
Source: "ayie.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Create Scheduled Task for Main App (Highest Privileges, starts at logon)
; Using single quotes for the path inside /tr to handle spaces correctly in schtasks
Filename: "schtasks"; Parameters: "/create /tn ""PisonetLockscreen"" /tr ""'{app}\{#MyAppExeName}'"" /sc onlogon /rl highest /f"; Flags: runhidden
; Create Scheduled Task for Watchdog (Highest Privileges, starts at logon)
Filename: "schtasks"; Parameters: "/create /tn ""PisonetWatchdog"" /tr ""'{app}\{#MyWatchdogName}'"" /sc onlogon /rl highest /f"; Flags: runhidden
; Run the app after installation.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Force kill processes before uninstallation
Filename: "taskkill"; Parameters: "/F /IM {#MyWatchdogName}"; Flags: runhidden; RunOnceId: "KillWatchdog"
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
function IsAppRunning(const FileName: string): Boolean;
var
  FSWbemLocator: Variant;
  FWMIService: Variant;
  FWbemObjectSet: Variant;
begin
  Result := False;
  try
    FSWbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    FWMIService := FSWbemLocator.ConnectServer('', 'root\CIMV2');
    FWbemObjectSet := FWMIService.ExecQuery(Format('SELECT Name FROM Win32_Process WHERE Name="%s"', [FileName]));
    Result := (FWbemObjectSet.Count > 0);
  except
    Result := False;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if IsAppRunning('{#MyAppExeName}') or IsAppRunning('{#MyWatchdogName}') then
  begin
    if MsgBox('Pisonet Lockscreen or Watchdog is currently running. Do you want the installer to try and close them automatically?', mbConfirmation, MB_YESNO) = idYes then
    begin
      ShellExec('open', 'taskkill.exe', '/F /IM {#MyWatchdogName}', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
      ShellExec('open', 'taskkill.exe', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
      Result := True;
    end
    else
    begin
      MsgBox('Please close the application before running the installer.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if IsAppRunning('{#MyAppExeName}') or IsAppRunning('{#MyWatchdogName}') then
  begin
    MsgBox('Please close the Pisonet Lockscreen and Watchdog before uninstalling.', mbError, MB_OK);
    Result := False;
  end;
  
  // Clean up scheduled tasks and registry run keys on uninstall
  if Result then
  begin
    Exec('schtasks', '/delete /tn "PisonetLockscreen" /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('schtasks', '/delete /tn "PisonetWatchdog" /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Clean up Registry Run keys
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PisonetLockscreen');
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PisonetWatchdog');
  end;
end;
