# 高考倒计时 3.2.0（WinUI 3 重构版）

原 PyQt6 版本的完全重构：界面改用 **WinUI 3（Windows App SDK）**，业务逻辑用 C# 重写，独立放在 `winui3/` 目录，不影响根目录的 1.x 版本。

> WinUI 3 不能由纯 C 直接承载：它的官方语言投影是 C#（.NET）和 C++/WinRT。本机没有 MSVC 工具链，因此采用 **WinUI 3 + C#** 这一可编译、可运行的路线；Windows 系统交互（托盘、窗口样式、前台检测）仍通过 Win32 P/Invoke 直接调用。

## 技术栈

| 项目 | 选择 |
| --- | --- |
| UI | WinUI 3 / Windows App SDK 1.8 |
| 语言 | C# / .NET 8 |
| 部署 | 自包含（`WindowsAppSDKSelfContained`），目标机不需要安装 Windows App Runtime |
| 系统交互 | Win32 P/Invoke：托盘图标与菜单、置顶无焦点窗口、圆角区域、前台进程检测、注册表自启动 |
| 配置 | 与 1.x 完全相同的 `config.json` |

## 功能

- 无边框、置顶、不抢焦点的悬浮倒计时卡片，圆角半径可调（0–60 px）+ 半透明背景，文字始终居中
- 支持自定义字体；背景图片可选，图片不透明度、缩放方式（覆盖 / 适应 / 拉伸 / 原始大小）和水平、垂直位置均可调
- 鼠标碰到卡片自动隐藏，移开后恢复；按住左键从卡片外滑入可拖动，松手后记住边距
- 前台是希沃白板 / 希沃视频展台等程序时自动隐藏（关键词可配）
- 多倒计时轮播：每条可单独设置名称、截止时间、持续天数、模板、进度起算日
- 多天考试：开考后显示「考试进行中 · 第 N 天」，结束后显示结束文案
- 只计上学日：扣除周末与法定节假日，调休补班的周末计入
- 节假日数据：内置 2025–2027 表离线兜底 + 从 GitHub 仓库自动 / 手动拉取，带本地缓存
- 进度百分比 `{percent}`：学年 9 月 1 日或自定义起算日；多天考试按已考天数
- 设置密码：PBKDF2-SHA256（120000 次迭代，加盐），与旧版哈希格式互通；保护设置、JSON 编辑、导入配置、开机自启
- 托盘菜单：设置、JSON 编辑、导入 / 导出、重新读取、节假日状态与更新、检查新版本、开机自启、关于、退出
- 应用内更新：从 `version.json` 读取版本号和安装包下载地址，发现新版本后可直接下载并运行安装程序
- 导入 / 导出配置、设置密码、忘记密码的处理方式与 1.x 一致

## 目录结构

```
winui3/
├─ App.xaml / App.cs        应用入口、托盘菜单、全局状态与定时任务
├─ Program.cs               手动 Main（Application.Start）
├─ AppConfig.cs             配置模型、JSON 读写、清洗、密码哈希
├─ Render.cs                剩余天数、进度百分比、模板占位符
├─ HolidayData.cs           内置节假日表、远程 JSON 解析、上学日统计
├─ CountdownWindow.cs       悬浮窗（置顶、悬停隐藏、拖拽、轮播、前台避让）
├─ Native.cs                Win32 互操作、托盘图标与菜单、系统集成
├─ SettingsWindow.cs        六页设置界面（倒计时 / 多倒计时 / 外观 / 行为 / 节假日 / 关于）
├─ Dialogs.cs               密码、JSON 编辑、关于、文件选择、版本检查
├─ Ui.cs                    设置界面通用控件与窗口辅助
├─ Assets/icon.ico          应用图标
└─ tests/LogicTests/        纯逻辑测试（配置、渲染、节假日、密码）
```

## 构建与运行

需要 .NET 8 SDK；可用 dotnet-install 把便携版装到非系统盘（本机在 `D:\Tools\dotnet8\dotnet.exe`），再用其中的 `dotnet.exe` 构建。

```bat
cd winui3
dotnet build -c Release -p:Platform=x64
```

产物：

```
winui3\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\ExamCountdown.exe
```

直接运行 `ExamCountdown.exe` 即可；目录内的 DLL 是自包含的 Windows App SDK 与 .NET 运行时，需整体分发。

> 构建机器没有 .NET SDK 时，可下载 `dotnet-sdk-8.0.x-win-x64.zip` 解压到任意目录，再用其中的 `dotnet.exe` 执行上面的命令。

## 配置兼容性

- 配置目录：`%APPDATA%\GaokaoCountdown\config.json`（与 1.x 相同，首次运行自动生成）
- 节假日缓存：同目录 `holidays_cache.json`（格式相同）
- 运行日志：同目录 `error.log`
- 密码哈希：`pbkdf2_sha256$120000$salt_hex$hash_hex`，旧版生成的哈希可直接验证
- 所有配置字段名、默认值、`{time}` / `{days}` / `{date}` / `{name}` / `{day}` / `{total}` / `{percent}` 语义与 1.x 一致
- 旧版遗留的 `easynote` 拼写与过宽的 `seewo` / `希沃` 关键词会自动修正

## 检查更新

启动 2 秒后检查一次，之后每 24 小时一次；托盘「检查新版本」可手动检查。自动检查只发托盘通知，手动检查会打开更新窗口。

`version.json`（放在节假日仓库根目录，读取地址与节假日数据一致）支持两种格式：

```json
{
  "version": "3.0.3",
  "download_url": "https://github.com/tomato-599/exam-countdow/releases/download/v3.0.3/exam-countdow-Setup-3.0.3.exe"
}
```

也兼容旧的纯文本写法（`3.0.3`），但纯文本没有下载地址，只能打开发布页手动下载。下载完成后会自动运行安装程序，并退出当前程序。

## 与 1.x 的差异

- 设置窗口、密码框、JSON 编辑器、关于页统一为置顶窗口（原版即为置顶，避免被桌面层遮挡）
- 设置界面由 Qt 表单改为 WinUI NavigationView 分页，带实时文字预览
- 托盘菜单、自启动、单实例互斥体名称保持不变
- 版本号从 1.1.0 提升到 3.2.0，配置无需迁移

## 测试

纯逻辑测试不需要 WinUI 运行时：

```bat
cd winui3\tests\LogicTests
dotnet run -c Release
```

可用环境变量 `GAOKAO_CONFIG_DIR` 指定临时配置目录（调试 / 测试用），不影响正式配置。

## 打包提示

自包含输出目录约 400 个文件，可直接用 Inno Setup 等工具整体打包；应用不需要管理员权限，也没有 MSIX 安装流程。

安装包默认装到用户目录（`%LOCALAPPDATA%\Programs\高考倒计时`，约 300 MB）。C 盘空间紧张时，在安装向导的目录页改到 D 盘即可。
