; 高考倒计时 —— Inno Setup 6 安装脚本
; 编译： tools\InnoSetup6\ISCC.exe installer.iss

#define AppName "高考倒计时"
#define AppVersion "0.7.1"
; VersionInfoVersion 必须是 X.X.X.X 纯数字、安装包文件名不能带空格，改版本时这两处一起改
#define VersionNum "0.7.1.0"
#define FileTag "0.7.1"
#define AppPublisher "李沐泽"
#define AppExeName "GaokaoCountdown.exe"
#define RunValueName "GaokaoCountdown"

[Setup]
; AppId 固定不变，升级安装时会自动替换旧版本
AppId={{8E6C2B34-5E1A-4C77-9B2D-6B0F1D3A7C51}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#VersionNum}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; 按用户安装，不需要管理员权限
PrivilegesRequired=lowest
OutputDir=dist-installer
OutputBaseFilename={#AppName}-Setup-{#FileTag}
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
AllowNoIcons=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; 安装/升级时若程序正在运行，提示关闭（与应用内的互斥体名字一致）
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

[Files]
Source: "dist\GaokaoCountdown\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "高考倒计时悬浮窗"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "高考倒计时悬浮窗"; Tasks: desktopicon

[Registry]
; 开机自启动（卸载时自动清理）
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "{#RunValueName}"; ValueData: """{app}\{#AppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
; 安装完成后立即启动（无勾选框，直接运行）
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait
