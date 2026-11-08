; 高考倒计时 3.2.0（WinUI 3 重构版）—— Inno Setup 6 安装脚本
; 编译： tools\InnoSetup6\ISCC.exe installer.iss

#define AppName "高考倒计时"
#define AppVersion "3.2.0"
; VersionInfoVersion 必须是 X.X.X.X 纯数字，改版本时这几处一起改
#define VersionNum "3.2.0.0"
#define FileTag "3.2.0"
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
AppMutex=Global\GaokaoCountdown_Mutex
CloseApplications=yes
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
