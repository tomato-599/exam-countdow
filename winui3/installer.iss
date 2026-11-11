; 高考倒计时 3.2.10（WinUI 3 重构版）—— Inno Setup 6 安装脚本
; 编译： tools\InnoSetup6\ISCC.exe installer.iss

#define AppName "高考倒计时"
#define AppVersion "3.2.10"
; VersionInfoVersion 必须是 X.X.X.X 纯数字，改版本时这几处一起改
#define VersionNum "3.2.10.0"
#define FileTag "3.2.10"
#define AppPublisher "李沐泽"
#define AppExeName "ExamCountdown.exe"
#define RunValueName "GaokaoCountdown"

[Setup]
; 沿用旧版 AppId，升级安装时直接替换 1.x
AppId={{8E6C2B34-5E1A-4C77-9B2D-6B0F1D3A7C51}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#VersionNum}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=dist-installer
OutputBaseFilename={#AppName}-Setup-{#FileTag}
SetupIconFile=Assets\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
AllowNoIcons=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
AppMutex=Global\GaokaoCountdown_Mutex
CloseApplications=no
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "开机自动启动（之后可在软件设置里关闭）"; GroupDescription: "启动选项："; Flags: checkedonce
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; 清理 1.x（PyInstaller）遗留文件
Type: files; Name: "{app}\GaokaoCountdown.exe"
Type: files; Name: "{app}\高考倒计时.exe"
Type: files; Name: "{app}\icon.ico"
Type: filesandordirs; Name: "{app}\_internal"

[Files]
Source: "bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "高考倒计时悬浮窗"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "高考倒计时悬浮窗"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "{#RunValueName}"; ValueData: """{app}\{#AppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait

[Code]
const
  DotnetUrl = 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime';

function HasRuntimeKey(RootKey: Integer): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(RootKey,
      'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
      Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
      if Pos('8.', Names[I]) = 1 then
      begin
        Result := True;
        Exit;
      end;
  end;
end;

function IsDotNet8DesktopInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  // .NET 可能注册在 32 位或 64 位注册表视图，两个都查
  Result := HasRuntimeKey(HKLM32) or HasRuntimeKey(HKLM64);
  if Result then Exit;
  // 兜底：直接看安装目录
  if FindFirst(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'),
      FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and $10) <> 0 then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

procedure KillRunningApps;
var
  ResultCode: Integer;
begin
  // 结束新版 / 旧版倒计时，避免托盘后台进程占用文件导致安装失败
  Exec(ExpandConstant('{cmd}'),
    '/c taskkill /F /T /IM ExamCountdown.exe >nul 2>&1 & ' +
    'taskkill /F /T /IM GaokaoCountdown.exe >nul 2>&1',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(800);
end;

function InitializeSetup: Boolean;
var
  Version: TWindowsVersion;
  Answer: Integer;
  ResultCode: Integer;
begin
  Result := True;
  if not IsWin64 then
  begin
    MsgBox('本程序需要 64 位 Windows 10/11。', mbCriticalError, MB_OK);
    Result := False;
    Exit;
  end;

  GetWindowsVersionEx(Version);
  if (Version.Major < 10) or ((Version.Major = 10) and (Version.Build < 17763)) then
  begin
    MsgBox('本程序需要 Windows 10 1809（build 17763）或更高版本。' + #13#10 +
      '当前系统：' + IntToStr(Version.Major) + '.' + IntToStr(Version.Minor) +
      ' build ' + IntToStr(Version.Build), mbCriticalError, MB_OK);
    Result := False;
    Exit;
  end;

  if not IsDotNet8DesktopInstalled then
  begin
    Answer := MsgBox(
      '未检测到 .NET 8 Desktop Runtime（x64）。' + #13#10#13#10 +
      '请先安装：Windows Desktop Runtime 8.0.x（x64）' + #13#10 +
      '下载地址：' + DotnetUrl + #13#10#13#10 +
      '点击“是”：打开下载页面并退出安装' + #13#10 +
      '点击“否”：仍继续安装（程序可能无法启动）' + #13#10 +
      '点击“取消”：退出安装',
      mbConfirmation, MB_YESNOCANCEL);
    if Answer = IDYES then
    begin
      ShellExec('open', DotnetUrl, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
      Result := False;
    end
    else if Answer = IDCANCEL then
      Result := False;
  end;

  if Result then KillRunningApps;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    KillRunningApps;
end;
