<h1 align="center">考试倒计时</h1>

<p align="center">
  <img src="icon.ico" alt="考试倒计时图标">
</p>

<p align="center">
  <img alt="Python 3.13.14" src="https://img.shields.io/badge/Python-3.13.14-3776AB?style=flat-square&logo=python&logoColor=white">
  <img alt="PyQt6" src="https://img.shields.io/badge/PyQt6-6.11-41CD52?style=flat-square&logo=qt&logoColor=white">
  <img alt="Windows 10 | 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square">
  <a href="https://github.com/deepseek-ai/deepseek-harness"><img alt="Powered by DSH" src="https://img.shields.io/badge/Powered_by-DSH-4D6BFE?style=flat-square&logo=deepseek&logoColor=white"></a>
  <a href="https://github.com/deepseek-ai/deepseek-harness"><img alt="DeepSeek V4 Flash" src="https://img.shields.io/badge/DeepSeek-V4_Flash-4D6BFE?style=flat-square&logo=deepseek&logoColor=white"></a>
</p>

**考试倒计时**（exam-countdow）是一款面向高考、中考、考研等考试场景的 Windows 桌面倒计时悬浮窗。置顶显示、无边框圆角、半透明，常驻系统托盘；支持多倒计时轮播、多天考试进行中状态、只计上学日、希沃白板前台自动隐藏、联网更新节假日数据与新版本提示。

## 功能特性

- **置顶悬浮窗**：无边框、圆角、半透明，默认位于屏幕右上角，不占任务栏、不进 Alt+Tab；
- **悬停隐藏**：鼠标移到窗口上自动隐藏，移开后恢复，不挡桌面点击；
- **拖拽记忆**：按住鼠标左键从窗外滑到窗口上即可拖动，松手后自动记住位置；
- **多倒计时轮播**：一模 / 二模 / 高考 / 寒假等多条倒计时隔几秒自动切换，也可手动切换；
- **多天考试**：每条倒计时可单独设置考试持续天数；开考当天显示归零文案，之后显示「考试进行中 · 第 N 天」，全部结束后才显示已结束，留空按 1 天处理；
- **只计上学日**：扣除周末、法定节假日，调休补班的周末计入，按真实上课天数倒计时；
- **场景自动隐藏**：希沃白板 / 视频展台在前台时自动隐藏，切回桌面自动出现；
- **联网更新**：节假日 / 调休数据从 GitHub 拉取，多镜像自动回退，本地缓存断网可用；
- **新版本提示**：启动后与每 24 小时检查一次版本，发现新版本只在托盘提示，不自动更新；
- **外观与设置**：仿 Fluent 标签页设置界面，深浅色跟随系统，可自定义字号、颜色、透明度；
- **托盘常驻**：导入 / 导出配置、编辑 JSON、开机自启、立即刷新、关于等一应俱全。

## 安装说明

### 方式一：安装包（推荐）

从 [Releases](https://github.com/tomato-599/exam-countdow/releases) 下载最新的 Setup 安装包，双击按向导安装：

- 按用户安装到 `%LOCALAPPDATA%\Programs\`，**不需要管理员权限**，不弹 UAC；
- 自动创建开始菜单快捷方式，桌面快捷方式可选；
- 安装完成后自动启动，并默认开启开机自启（可随时关闭）；
- 带完整卸载程序，卸载时清理快捷方式与自启项，个人配置保留。

系统要求：**Windows 10 / 11（x64）**。

### 方式二：源码运行

需要 Python 3.13.x：

```bat
git clone https://github.com/tomato-599/exam-countdow.git
cd exam-countdow
python -m pip install -r requirements.txt
pythonw main.py
```

也可以直接双击仓库里的 `安装依赖.bat` 安装依赖、`启动倒计时.bat` 启动程序。

> 如果启动时报 `ImportError: DLL load failed while importing QtCore`，说明 PyQt6 绑定与 Qt 运行库版本不匹配，执行 `python -m pip install -U "PyQt6>=6.10"`。

## 使用方法

1. 启动后悬浮窗出现在屏幕右上角，系统托盘出现图标。
2. 鼠标移到悬浮窗上它会隐藏（避免挡住桌面图标），光标移开后自动恢复；按住鼠标左键从窗外滑到窗口上即可拖动，松手后位置自动保存。
3. 托盘操作：**单击**显示 / 隐藏悬浮窗，**双击**打开设置，**右键**打开完整菜单。
4. 托盘菜单包含：设置…、编辑设置（JSON）…、导出 / 导入配置…、重新读取配置、节假日数据状态与立即更新、检查新版本、开机自动启动、关于、退出。
5. 设置界面分为 **倒计时 / 多倒计时 / 外观 / 行为** 四个标签页，改动带实时预览，点「确定」立即生效。
6. 「显示文字」模板支持占位符：`{time}` 或 `{days}` = 剩余天数，`{date}` = 截止日期，`{name}` = 事件名称。例如 `距离{name}还有 {time} 天`；「考试进行中」文字还支持 `{day}` = 第几天、`{total}` = 总天数。
7. 「多倒计时」标签页里的「倒计时列表」是可视化表单：点「+ 添加倒计时」新建一行，分别填写名称、截止时间、持续天数和可选模板，行尾「删除」移除。持续天数留空按 1 天（开考当天结束）；列表顺序即轮播顺序；条目内的模板留空时使用「默认模板」，名称和模板都为空的行会被忽略。
8. 配置文件位于 `%APPDATA%\GaokaoCountdown\config.json`，首次运行自动生成；直接编辑该文件或在托盘「编辑设置（JSON）…」中修改均可，删除后恢复默认。运行日志在同目录的 `error.log`。
9. 需要更多程序触发「前台自动隐藏」时，在设置里把进程名或窗口标题关键词加入「自动隐藏的关键词」（英文逗号分隔）。

## 技术栈 / 库

| 类别 | 说明 |
| --- | --- |
| 语言 | Python 3.13.14 |
| GUI | PyQt6 6.11（Qt 6 Widgets，手写 QSS 实现仿 Fluent 界面） |
| 系统集成 | ctypes 调用 Win32 API（注册表开机自启、命名互斥体单实例、前台窗口检测）；QSystemTrayIcon 系统托盘 |
| 网络 / 数据 | 标准库 urllib.request + json：GitHub raw 与 jsDelivr 多镜像、失败自动回退、本地缓存 |
| 打包 | PyInstaller 6.14 + Inno Setup 6.7.3（免安装版位于 `tools\InnoSetup6\`） |
| 图标 | `icon.ico` / `icon.png`（运行时从 `sys._MEIPASS` 加载） |

## 数据与联网

- **节假日数据**：默认从 `tomato-599/holidays_count` 仓库的 `holidays.json` 获取，raw 失败自动切换 jsDelivr 镜像；启动时检查、每 6 小时缓存过期策略，断网时使用本地缓存与内置数据；
- **版本检查**：同一仓库根目录的 `version.json`（一行版本号，如 `0.7.1`），启动 2 秒后与每 24 小时检查，只在托盘提示新版本，**不自动下载、不自动安装**；
- 数据源可在 `config.json` 的 `holiday_repo` / `holiday_branch` / `holiday_file` 中修改。

## 从仓库克隆并打包

```bat
git clone https://github.com/tomato-599/exam-countdow.git
cd exam-countdow
python -m pip install -r requirements.txt
```

确认源码可正常运行后，双击 `打包安装包.bat` 一键打包，它依次执行：

```bat
python -m PyInstaller --noconfirm --clean --windowed --icon icon.ico --add-data "icon.ico;." --name GaokaoCountdown main.py
tools\InnoSetup6\ISCC.exe installer.iss
```

- 产物：`dist\GaokaoCountdown\`（PyInstaller 目录版）与 `dist-installer\` 下的 Setup 安装包；
- 打包依赖：PyInstaller（`python -m pip install pyinstaller`）与 Inno Setup 6；`tools\` 为第三方工具目录，默认不入库，克隆后请把 Inno Setup 6 解压到 `tools\InnoSetup6\`；
- 改版本号需同步修改 `countdown/constants.py` 的 `APP_VERSION` 和 `installer.iss` 顶部的 `AppVersion` / `VersionNum` / `FileTag`；
- `打包安装包.bat` 为 GBK + CRLF 编码，编辑时请保持原编码，否则 cmd 会解析报错。

## 项目结构

```text
countdown/          # 应用模块：悬浮窗、托盘、设置界面、节假日数据、版本检查
main.py             # 程序入口
icon.ico / icon.png # 应用图标
installer.iss       # Inno Setup 安装脚本
打包安装包.bat       # 一键打包：PyInstaller + Inno Setup
tools/InnoSetup6/   # 免安装版 Inno Setup
```

## 说明

- 天数按**自然日**计算；开启「只计上学日」后，改为统计 `(今天, 截止日期]` 区间内的上课天数（扣除周末与法定节假日，调休补班的周末计入）；
- 截止日期即**开考时间**；持续天数包含开考当天，留空或填 1 表示当天结束。持续天数大于 1 时，开考当天仍显示归零文案，之后到全部结束前显示「考试进行中」，超过持续天数才显示结束文案；
- 默认截止时间为下一个 6 月 7 日 09:00，首次运行请在设置里改成自己的日期；
- 未来年份的放假安排为预估数据，官方通知发布后随节假日在线数据更新；
- 程序为**单实例**，重复启动会静默退出；
- 配置中的非法值会自动兜底，手改 `config.json` 不会导致启动失败；
- 本项目大部分使用Vibe Coding，若有问题，欢迎提交 Issue（改正代码绝对100%真人含量）。

## 特别感谢

本项目在开发、构建与运行过程中，使用或接入了下列平台与工具（排名不分先后）。谨向以下平台及开源社区致以由衷的感谢：

- **[DeepSeek 开放平台](https://platform.deepseek.com/)** — DeepSeek模型 API
- **[OpenRouter](https://openrouter.ai/)** — 多模型统一接入
- **[DeepSeek Harness](https://github.com/deepseek-ai/deepseek-harness)** — 开发工具链
- **[Qoder](https://qoder.com/)** — 开发工具链
- **[Qwen](https://qwen.ai/)** — 通义千问模型
- **[Canva](https://www.canva.cn/)** — 图标绘制

感谢各平台提供的模型能力、接入服务与工具链支持。
