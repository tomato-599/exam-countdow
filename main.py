# -*- coding: utf-8 -*-
"""
高考倒计时悬浮窗 (PyQt6)

功能:
  * 桌面右上角置顶悬浮窗，显示 "距离高考还有 X 天"
  * 鼠标移到悬浮窗上自动隐藏（鼠标离开后自动恢复），不挡点击
  * 检测到希沃白板 / 希沃视频展台等程序在前台时自动隐藏
  * 常驻 Windows 系统托盘，右键图标可进入设置 / 退出
  * 设置：截止日期时间、显示文字模板、颜色（跟随系统或自定义）、字号
  * 默认开机自启动（可在设置里关闭）
  * 剩余天数 < 10 天：数字字号变大且变红；== 0 天：显示 "旗开得胜，未来可期！"
"""

from __future__ import annotations

import ctypes
import html
import json
import os
import sys
import traceback
from ctypes import wintypes
from datetime import date, datetime
from pathlib import Path

from PyQt6.QtCore import Qt, QDateTime, QTimer, QRect
from PyQt6.QtGui import (
    QAction,
    QColor,
    QCursor,
    QFont,
    QIcon,
    QPainter,
    QPixmap,
)
from PyQt6.QtWidgets import (
    QApplication,
    QCheckBox,
    QColorDialog,
    QComboBox,
    QDateTimeEdit,
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QFrame,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMenu,
    QMessageBox,
    QPushButton,
    QPlainTextEdit,
    QSlider,
    QSpinBox,
    QSystemTrayIcon,
    QTabWidget,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

# --------------------------------------------------------------------------- #
# 常量
# --------------------------------------------------------------------------- #
APP_ID = "GaokaoCountdown"          # 注册表 / 互斥体使用的名字
APP_TITLE = "高考倒计时"
APP_VERSION = "1.1.0"
APP_AUTHOR = "李沐泽"
APP_DESC = "置顶悬浮的高考倒计时 · 希沃前台自动隐藏 · 托盘常驻"
RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"
PERSONALIZE_KEY = (
    r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
)
IS_WINDOWS = os.name == "nt"
SCRIPT_PATH = os.path.abspath(__file__)

CONFIG_DIR = Path(os.environ.get("APPDATA") or Path.home()) / APP_ID  # 占位，下面会重新解析


def _resolve_config_dir() -> Path:
    """优先使用 %APPDATA%\\GaokaoCountdown；不可写时退回程序目录。"""
    primary = Path(os.environ.get("APPDATA") or Path.home()) / APP_ID
    try:
        primary.mkdir(parents=True, exist_ok=True)
        probe = primary / ".write_test"
        probe.write_text("ok", encoding="utf-8")
        probe.unlink()
        return primary
    except Exception:
        fallback = Path(SCRIPT_PATH).parent / "config"
        try:
            fallback.mkdir(parents=True, exist_ok=True)
        except Exception:
            pass
        return fallback


CONFIG_DIR = _resolve_config_dir()
CONFIG_FILE = CONFIG_DIR / "config.json"
LOG_FILE = CONFIG_DIR / "error.log"

# 前台为这些程序（进程名或窗口标题包含关键词，忽略大小写）时自动隐藏
DEFAULT_HIDE_KEYWORDS = ["easynote", "seewo", "希沃"]


def default_deadline() -> str:
    """默认截止日期：下一个 6 月 7 日 09:00。"""
    today = date.today()
    year = today.year
    if date(year, 6, 7) < today:
        year += 1
    return f"{year}-06-07 09:00:00"


DEFAULT_CONFIG = {
    "deadline": default_deadline(),          # 截止日期时间
    "template": "距离高考还有 {time} 天",     # {time}/{days} = 天数
    "zero_text": "旗开得胜，未来可期！",       # 天数 == 0
    "past_text": "高考已结束，愿你前程似锦！",  # 天数 < 0
    "zero_font_size": 30,                    # 归零/结束时字号
    "base_font_size": 20,                    # 常态文字字号
    "urgent_days": 10,                       # 小于该天数进入“紧急”样式
    "urgent_font_size": 34,                  # 紧急时的数字字号
    "urgent_color": "#E53935",               # 紧急时的数字颜色（红）
    "color_mode": "system",                  # system = 跟随系统；custom = 自定义
    "text_color": "#C62828",                 # 自定义文字颜色
    "bg_color": "#FFFFFF",                   # 自定义背景颜色
    "opacity": 0.92,                         # 背景不透明度 0~1
    "autostart": True,                       # 开机自启动
    "margin_x": 28,                          # 距离屏幕右边缘
    "margin_y": 24,                          # 距离屏幕上边缘
    "hide_keywords": DEFAULT_HIDE_KEYWORDS,  # 自动隐藏的关键词
    # 多倒计时轮播：[{"name": "一模", "deadline": "2027-03-10 09:00:00", "template": ""}]
    "events": [],
    "rotate_seconds": 5,                     # 轮播间隔（秒）
    "event_template": "距离{name}还有 {time} 天",  # 事件没写模板时用它
}

# 每个配置项的中文说明（“关于 / 设置(JSON)”里会一并列出）
CONFIG_FIELDS: dict[str, str] = {
    "deadline": "截止日期时间，ISO 格式 2027-06-07 09:00:00",
    "template": "显示文字模板，{time} 或 {days} = 剩余天数，{date} = 截止日期",
    "zero_text": "剩余天数为 0（高考当天）时显示的文字",
    "past_text": "截止日期已过时显示的文字",
    "base_font_size": "常态文字字号（px）",
    "urgent_font_size": "紧急状态下数字字号（px）",
    "zero_font_size": "归零 / 结束时文字字号（px）",
    "urgent_days": "紧急阈值：剩余天数小于该值时数字放大变红",
    "urgent_color": "紧急状态数字颜色（十六进制）",
    "color_mode": "颜色方案：system = 跟随系统，custom = 自定义",
    "text_color": "自定义模式下的文字颜色",
    "bg_color": "自定义模式下的背景颜色",
    "opacity": "背景不透明度，0.15 ~ 1.0",
    "autostart": "是否开机自动启动（true / false）",
    "margin_x": "悬浮窗距离屏幕右边距（px）",
    "margin_y": "悬浮窗距离屏幕上边距（px）",
    "hide_keywords": "前台程序命中这些关键词（进程名或窗口标题）时自动隐藏",
    "events": "多倒计时列表，每项 {\"name\": 名称, \"deadline\": 截止时间, \"template\": 可选单独模板}",
    "rotate_seconds": "多倒计时轮播间隔（秒），2 ~ 600",
    "event_template": "多倒计时条目没写自己模板时使用的文字模板（支持 {name}）",
}


# --------------------------------------------------------------------------- #
# 小工具
# --------------------------------------------------------------------------- #
def _clamp_int(value, low: int, high: int, default: int) -> int:
    try:
        return max(low, min(high, int(value)))
    except Exception:
        return default


def _clamp_float(value, low: float, high: float, default: float) -> float:
    try:
        return max(low, min(high, float(value)))
    except Exception:
        return default


def _color_or(value, default: str) -> str:
    text = str(value or "").strip()
    return text if QColor(text).isValid() else default


def sanitize_config(raw) -> dict:
    """把 JSON 文件 / 手写内容收敛成合法配置，非法值回退到默认值。"""
    data = dict(DEFAULT_CONFIG)
    if isinstance(raw, dict):
        data.update(
            {k: v for k, v in raw.items() if k in DEFAULT_CONFIG and v is not None}
        )
    data["deadline"] = str(data.get("deadline") or DEFAULT_CONFIG["deadline"])
    data["template"] = str(data.get("template") or "{time}")
    data["zero_text"] = str(data.get("zero_text") or "")
    data["past_text"] = str(data.get("past_text") or "")
    data["base_font_size"] = _clamp_int(data.get("base_font_size"), 8, 120, 20)
    data["urgent_font_size"] = _clamp_int(data.get("urgent_font_size"), 8, 200, 34)
    data["zero_font_size"] = _clamp_int(data.get("zero_font_size"), 8, 200, 30)
    data["urgent_days"] = _clamp_int(data.get("urgent_days"), 1, 365, 10)
    data["opacity"] = _clamp_float(data.get("opacity"), 0.15, 1.0, 0.92)
    data["margin_x"] = _clamp_int(data.get("margin_x"), 0, 4000, 28)
    data["margin_y"] = _clamp_int(data.get("margin_y"), 0, 4000, 24)
    data["color_mode"] = "custom" if data.get("color_mode") == "custom" else "system"
    data["text_color"] = _color_or(data.get("text_color"), "#C62828")
    data["bg_color"] = _color_or(data.get("bg_color"), "#FFFFFF")
    data["urgent_color"] = _color_or(data.get("urgent_color"), "#E53935")
    data["autostart"] = bool(data.get("autostart"))
    keywords = data.get("hide_keywords")
    if isinstance(keywords, str):
        keywords = [k.strip() for k in keywords.split(",") if k.strip()]
    if not isinstance(keywords, list):
        keywords = []
    keywords = [str(k).strip() for k in keywords if str(k).strip()]
    data["hide_keywords"] = keywords or list(DEFAULT_HIDE_KEYWORDS)
    data["events"] = parse_events(data.get("events"))
    data["rotate_seconds"] = _clamp_int(data.get("rotate_seconds"), 2, 600, 5)
    data["event_template"] = str(
        data.get("event_template") or DEFAULT_CONFIG["event_template"]
    )
    return data


def config_to_json(cfg: dict) -> str:
    """配置的 JSON 文本（未转义中文、缩进 2）。"""
    return json.dumps(cfg, ensure_ascii=False, indent=2)


def fields_to_html() -> str:
    """字段说明的 HTML 列表。"""
    rows = "".join(
        f"<tr><td style='padding:2px 10px 2px 0;color:#c62828;white-space:nowrap;'>"
        f"<code>{html.escape(key)}</code></td>"
        f"<td style='padding:2px 0;'>{html.escape(text)}</td></tr>"
        for key, text in CONFIG_FIELDS.items()
    )
    return f"<table style='font-size:12px;'>{rows}</table>"


def log_error(text: str) -> None:
    try:
        CONFIG_DIR.mkdir(parents=True, exist_ok=True)
        with open(LOG_FILE, "a", encoding="utf-8") as fp:
            fp.write(f"[{datetime.now():%Y-%m-%d %H:%M:%S}] {text}\n")
    except Exception:
        pass


def install_excepthook() -> None:
    def hook(exc_type, exc_value, exc_tb):
        text = "".join(traceback.format_exception(exc_type, exc_value, exc_tb))
        try:
            sys.stderr.write(text)
        except Exception:
            pass
        log_error(text)

    sys.excepthook = hook


def load_config() -> dict:
    """每次启动都从 config.json 读取；文件不存在或出错则用默认值。"""
    raw: dict = {}
    try:
        if CONFIG_FILE.exists():
            with open(CONFIG_FILE, "r", encoding="utf-8") as fp:
                raw = json.load(fp)
    except Exception:
        log_error("读取配置失败:\n" + traceback.format_exc())
        raw = {}
    return sanitize_config(raw)


def save_config(cfg: dict) -> None:
    try:
        CONFIG_DIR.mkdir(parents=True, exist_ok=True)
        with open(CONFIG_FILE, "w", encoding="utf-8") as fp:
            json.dump(cfg, fp, ensure_ascii=False, indent=2)
    except Exception:
        log_error("保存配置失败:\n" + traceback.format_exc())


def is_system_dark() -> bool:
    """读取注册表判断系统是否处于深色模式。"""
    if not IS_WINDOWS:
        return False
    try:
        import winreg

        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, PERSONALIZE_KEY) as key:
            value, _ = winreg.QueryValueEx(key, "AppsUseLightTheme")
            return int(value) == 0
    except Exception:
        return False


def launch_command() -> str:
    """开机自启动使用的命令行。"""
    if getattr(sys, "frozen", False):           # 打包成 exe 后
        return f'"{sys.executable}"'
    exe = sys.executable
    pythonw = os.path.join(os.path.dirname(exe), "pythonw.exe")
    if os.path.exists(pythonw):
        exe = pythonw
    return f'"{exe}" "{SCRIPT_PATH}"'


def set_autostart(enabled: bool) -> bool:
    """写入 / 删除开机自启动注册表项，返回是否成功。"""
    if not IS_WINDOWS:
        return False
    try:
        import winreg

        with winreg.CreateKeyEx(
            winreg.HKEY_CURRENT_USER, RUN_KEY, 0, winreg.KEY_SET_VALUE
        ) as key:
            if enabled:
                winreg.SetValueEx(
                    key, APP_ID, 0, winreg.REG_SZ, launch_command()
                )
            else:
                try:
                    winreg.DeleteValue(key, APP_ID)
                except FileNotFoundError:
                    pass
        return True
    except Exception:
        log_error("设置开机自启动失败:\n" + traceback.format_exc())
        return False


def autostart_command() -> str | None:
    """读取当前写入的自启动命令；没有则返回 None。"""
    if not IS_WINDOWS:
        return None
    try:
        import winreg

        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY) as key:
            value, _ = winreg.QueryValueEx(key, APP_ID)
        return str(value)
    except Exception:
        return None


def single_instance_guard() -> bool:
    """True = 已经有实例在运行。"""
    if not IS_WINDOWS:
        return False
    try:
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.CreateMutexW.restype = wintypes.HANDLE
        kernel32.CreateMutexW.argtypes = [
            wintypes.LPVOID,
            wintypes.BOOL,
            wintypes.LPCWSTR,
        ]
        handle = kernel32.CreateMutexW(None, False, f"Global\\{APP_ID}_Mutex")
        already = ctypes.get_last_error() == 183  # ERROR_ALREADY_EXISTS
        if not handle:
            return False
        globals()["_MUTEX_HANDLE"] = handle  # 保持引用，避免句柄被回收
        return already
    except Exception:
        log_error("单实例检测失败:\n" + traceback.format_exc())
        return False


def make_icon() -> QIcon:
    """用代码画一个托盘图标，省去外部资源文件。"""
    pm = QPixmap(64, 64)
    pm.fill(Qt.GlobalColor.transparent)
    painter = QPainter(pm)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing, True)
    painter.setPen(Qt.PenStyle.NoPen)
    painter.setBrush(QColor("#E53935"))
    painter.drawRoundedRect(2, 2, 60, 60, 16, 16)
    painter.setPen(QColor("#FFFFFF"))
    font = QFont("Microsoft YaHei", 26)
    font.setBold(True)
    painter.setFont(font)
    painter.drawText(pm.rect(), Qt.AlignmentFlag.AlignCenter, "考")
    painter.end()
    return QIcon(pm)


# --------------------------------------------------------------------------- #
# Windows 前台窗口检测
# --------------------------------------------------------------------------- #
if IS_WINDOWS:
    _user32 = ctypes.windll.user32
    _kernel32 = ctypes.windll.kernel32
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000


def foreground_process_name() -> str:
    """返回前台窗口所属进程的可执行文件名（小写）。"""
    if not IS_WINDOWS:
        return ""
    try:
        hwnd = _user32.GetForegroundWindow()
        if not hwnd:
            return ""
        pid = wintypes.DWORD()
        _user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
        if not pid.value:
            return ""
        handle = _kernel32.OpenProcess(
            PROCESS_QUERY_LIMITED_INFORMATION, False, pid.value
        )
        if not handle:
            return ""
        try:
            buf = ctypes.create_unicode_buffer(1024)
            size = wintypes.DWORD(len(buf))
            if _kernel32.QueryFullProcessImageNameW(
                handle, 0, buf, ctypes.byref(size)
            ):
                return os.path.basename(buf.value).lower()
        finally:
            _kernel32.CloseHandle(handle)
    except Exception:
        log_error("前台进程检测失败:\n" + traceback.format_exc())
    return ""


def foreground_window_title() -> str:
    if not IS_WINDOWS:
        return ""
    try:
        hwnd = _user32.GetForegroundWindow()
        if not hwnd:
            return ""
        length = _user32.GetWindowTextLengthW(hwnd)
        if length <= 0:
            return ""
        buf = ctypes.create_unicode_buffer(length + 1)
        _user32.GetWindowTextW(hwnd, buf, length + 1)
        return buf.value
    except Exception:
        return ""


# --------------------------------------------------------------------------- #
# 渲染
# --------------------------------------------------------------------------- #
def resolve_colors(cfg: dict) -> tuple[str, str]:
    """返回 (文字颜色, 背景颜色)。"""
    if cfg.get("color_mode") == "custom":
        return cfg.get("text_color", "#C62828"), cfg.get("bg_color", "#FFFFFF")
    if is_system_dark():
        return "#F2F2F2", "#1E1E1E"
    return "#202124", "#FFFFFF"


def parse_deadline(raw) -> datetime | None:
    """把 '2027-06-07 09:00:00' / '2027-06-07' 之类的字符串解析成 datetime。"""
    text = str(raw or "").strip()
    if not text:
        return None
    try:
        return datetime.fromisoformat(text)
    except Exception:
        pass
    try:
        return datetime.combine(date.fromisoformat(text[:10]), datetime.min.time())
    except Exception:
        return None


def days_until(raw) -> int:
    dt = parse_deadline(raw)
    if dt is None:
        return 0
    return (dt.date() - date.today()).days


def days_left(cfg: dict) -> int:
    """主倒计时的剩余天数。"""
    return days_until(cfg.get("deadline"))


def parse_events(raw) -> list[dict]:
    """规范化多倒计时列表：每项 {name, deadline, template}。"""
    events: list[dict] = []
    if not isinstance(raw, list):
        return events
    for entry in raw:
        if not isinstance(entry, dict):
            continue
        deadline = str(entry.get("deadline") or "").strip()
        if not deadline:
            continue
        events.append(
            {
                "name": str(entry.get("name") or "").strip(),
                "deadline": deadline,
                "template": str(entry.get("template") or "").strip(),
            }
        )
    return events


def events_to_text(events) -> str:
    """多倒计时 -> 设置界面的多行文本（名称 | 截止时间 | 可选模板）。"""
    lines = []
    for event in parse_events(events):
        line = f"{event['name']} | {event['deadline']}"
        if event["template"]:
            line += f" | {event['template']}"
        lines.append(line)
    return "\n".join(lines)


def parse_events_text(text: str) -> list[dict]:
    """设置界面的多行文本 -> 多倒计时列表。"""
    events: list[dict] = []
    for line in str(text or "").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = [p.strip() for p in line.split("|")]
        if len(parts) < 2 or not parts[1]:
            continue
        events.append(
            {
                "name": parts[0],
                "deadline": parts[1],
                "template": parts[2] if len(parts) > 2 else "",
            }
        )
    return events


def config_items(cfg: dict) -> list[dict]:
    """所有倒计时条目；没有配置多倒计时时就是默认的那一条。"""
    events = parse_events(cfg.get("events"))
    if events:
        return events
    return [
        {"name": "", "deadline": str(cfg.get("deadline") or ""), "template": ""}
    ]


def resolve_texts(cfg: dict, item: dict | None) -> tuple[str, str, str, str]:
    """返回 (文字模板, 归零文字, 已过文字, 名称)。"""
    item = item or {}
    name = str(item.get("name") or "")
    override = str(item.get("template") or "")
    if override:
        template = override
    elif name:  # 多倒计时条目没写模板时用统一的事件模板
        template = str(
            cfg.get("event_template") or DEFAULT_CONFIG["event_template"]
        )
    else:
        template = str(cfg.get("template") or "")
    zero = str(cfg.get("zero_text") or "").replace("{name}", name)
    past = str(cfg.get("past_text") or "").replace("{name}", name)
    return template, zero, past, name


def _esc(text: str) -> str:
    """转义并按 Qt 富文本规则保留空格。"""
    return html.escape(text).replace(" ", "&nbsp;")


def build_html(cfg: dict, days: int, item: dict | None = None) -> str:
    text_color, _bg = resolve_colors(cfg)
    urgent_color = cfg.get("urgent_color", "#E53935")
    base_size = int(cfg.get("base_font_size", 20))
    urgent_size = int(cfg.get("urgent_font_size", 34))
    zero_size = int(cfg.get("zero_font_size", 30))

    template, zero_text, past_text, name = resolve_texts(cfg, item)

    # 当天 / 已过
    if days <= 0:
        text = (zero_text if days == 0 else past_text) or zero_text or ""
        return (
            f'<span style="font-size:{zero_size}px;font-weight:700;'
            f'color:{urgent_color};">{_esc(text)}</span>'
        )

    date_text = str((item or {}).get("deadline") or cfg.get("deadline") or "")[:10]

    placeholder = "{time}" if "{time}" in template else (
        "{days}" if "{days}" in template else None
    )
    if placeholder:
        prefix, suffix = template.split(placeholder, 1)
    else:  # 模板里没写占位符，就把天数补在后面
        prefix, suffix = template + " ", ""

    prefix = prefix.replace("{date}", date_text).replace("{name}", name)
    suffix = suffix.replace("{date}", date_text).replace("{name}", name)

    urgent = days < int(cfg.get("urgent_days", 10))
    if urgent:
        number_style = (
            f"font-size:{urgent_size}px;font-weight:700;color:{urgent_color};"
        )
    else:
        number_style = f"font-size:{base_size}px;font-weight:700;color:{text_color};"

    normal_style = f"font-size:{base_size}px;color:{text_color};"
    return (
        f'<span style="{normal_style}">{_esc(prefix)}</span>'
        f'<span style="{number_style}">{days}</span>'
        f'<span style="{normal_style}">{_esc(suffix)}</span>'
    )


def plain_text(cfg: dict, days: int, item: dict | None = None) -> str:
    """给托盘提示用的纯文本。"""
    template, zero_text, past_text, name = resolve_texts(cfg, item)
    if days == 0:
        return zero_text
    if days < 0:
        return past_text
    date_text = str((item or {}).get("deadline") or cfg.get("deadline") or "")[:10]
    text = template or "{time}"
    text = text.replace("{time}", str(days)).replace("{days}", str(days))
    text = text.replace("{date}", date_text).replace("{name}", name)
    return text


def apply_card_style(frame: QFrame, cfg: dict) -> str:
    """应用圆角半透明卡片样式，返回当前文字颜色。"""
    text_color, bg_color = resolve_colors(cfg)
    color = QColor(bg_color)
    alpha = int(max(0.15, min(1.0, float(cfg.get("opacity", 0.92)))) * 255)
    # 深色背景用浅色描边，浅色背景用深色描边
    border = (
        "rgba(255,255,255,0.16)"
        if color.lightness() < 128
        else "rgba(0,0,0,0.12)"
    )
    frame.setStyleSheet(
        "QFrame#card {"
        f"background-color: rgba({color.red()},{color.green()},{color.blue()},{alpha});"
        "border-radius: 14px;"
        f"border: 1px solid {border};"
        "}"
    )
    return text_color


# --------------------------------------------------------------------------- #
# 悬浮窗
# --------------------------------------------------------------------------- #
class CountdownWindow(QWidget):
    def __init__(self, cfg: dict):
        super().__init__(None)
        self.cfg = cfg
        self._hover_hidden = False
        self._hover_rect = QRect()
        self._foreground_blocked = False
        self._user_hidden = False
        self._items: list[dict] = []
        self._index = 0

        self.setWindowFlags(
            Qt.WindowType.FramelessWindowHint
            | Qt.WindowType.WindowStaysOnTopHint
            | Qt.WindowType.Tool
            | Qt.WindowType.WindowDoesNotAcceptFocus
        )
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground, True)
        self.setAttribute(Qt.WidgetAttribute.WA_ShowWithoutActivating, True)
        self.setWindowTitle(APP_TITLE)

        self.card = QFrame(self)
        self.card.setObjectName("card")
        self.label = QLabel(self.card)
        self.label.setTextFormat(Qt.TextFormat.RichText)
        self.label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.label.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground, True)

        card_layout = QVBoxLayout(self.card)
        card_layout.setContentsMargins(18, 9, 18, 9)
        card_layout.addWidget(self.label)

        root = QVBoxLayout(self)
        root.setContentsMargins(0, 0, 0, 0)
        root.addWidget(self.card)

        # 鼠标移上去 -> 隐藏；光标离开原区域后再显示
        self._hover_timer = QTimer(self)
        self._hover_timer.setInterval(120)
        self._hover_timer.timeout.connect(self._check_hover_release)

        # 前台程序检测
        self._fg_timer = QTimer(self)
        self._fg_timer.setInterval(500)
        self._fg_timer.timeout.connect(self.check_foreground)
        self._fg_timer.start()

        # 跨天刷新
        self._refresh_timer = QTimer(self)
        self._refresh_timer.setInterval(30_000)
        self._refresh_timer.timeout.connect(self.refresh_text)
        self._refresh_timer.start()

        # 多倒计时轮播
        self._rotate_timer = QTimer(self)
        self._rotate_timer.timeout.connect(self.next_item)

        self.apply_config(cfg)

    # ---------------- 配置 ----------------
    def apply_config(self, cfg: dict) -> None:
        self.cfg = cfg
        self._items = config_items(cfg)
        if self._index >= len(self._items):
            self._index = 0
        self._sync_rotate_timer()
        self._fit_to_widest()
        self.refresh_text()
        self.apply_theme()
        self.place_top_right()

    def _sync_rotate_timer(self) -> None:
        """只有配了多个倒计时才轮播。"""
        if len(self._items) > 1:
            interval = int(self.cfg.get("rotate_seconds", 5)) * 1000
            self._rotate_timer.start(max(2000, interval))
        else:
            self._rotate_timer.stop()

    def current_item(self) -> dict:
        if not self._items:
            self._items = config_items(self.cfg)
        if self._index >= len(self._items):
            self._index = 0
        return self._items[self._index]

    def next_item(self) -> None:
        """切换到下一条倒计时（多倒计时轮播 / 托盘“下一个倒计时”）。"""
        if len(self._items) > 1:
            self._index = (self._index + 1) % len(self._items)
        self.refresh_text()

    def _fit_to_widest(self) -> None:
        """按最长的一条预留尺寸，轮播时窗口不会来回跳动。"""
        if not self._items:
            return
        widest = tallest = 0
        for item in self._items:
            self.label.setText(
                build_html(self.cfg, days_until(item.get("deadline")), item)
            )
            hint = self.label.sizeHint()
            widest = max(widest, hint.width())
            tallest = max(tallest, hint.height())
        self.label.setMinimumSize(widest, tallest)

    def refresh_text(self) -> None:
        item = self.current_item()
        days = days_until(item.get("deadline"))
        self.label.setText(build_html(self.cfg, days, item))
        text_color = apply_card_style(self.card, self.cfg)
        self.label.setStyleSheet(
            f"color: {text_color}; background: transparent;"
        )
        font = QFont("Microsoft YaHei UI")
        if not font.exactMatch():
            font = QFont("Microsoft YaHei")
        self.label.setFont(font)
        self.adjustSize()
        self.place_top_right()

    def apply_theme(self) -> None:
        apply_card_style(self.card, self.cfg)

    def place_top_right(self) -> None:
        screen = QApplication.primaryScreen()
        if screen is None:
            return
        area = screen.availableGeometry()
        mx = int(self.cfg.get("margin_x", 28))
        my = int(self.cfg.get("margin_y", 24))
        self.adjustSize()
        x = area.right() - self.width() - mx
        y = area.top() + my
        self.move(max(area.left(), x), max(area.top(), y))

    # ---------------- 显示 / 隐藏 ----------------
    def set_user_hidden(self, hidden: bool) -> None:
        self._user_hidden = hidden
        self.update_visibility()

    def update_visibility(self) -> None:
        if self._user_hidden or self._hover_hidden or self._foreground_blocked:
            self.hide()
        else:
            self.show()
            self.raise_()

    def enterEvent(self, event) -> None:  # noqa: N802
        """鼠标挪到悬浮窗上就隐藏，完全不挡操作。"""
        if self.isVisible():
            self._hover_hidden = True
            self._hover_rect = self.frameGeometry()
            self.hide()
            self._hover_timer.start()
        super().enterEvent(event)

    def _check_hover_release(self) -> None:
        if not self._hover_hidden:
            self._hover_timer.stop()
            return
        if not self._hover_rect.contains(QCursor.pos()):
            self._hover_hidden = False
            self._hover_timer.stop()
            self.update_visibility()

    def check_foreground(self) -> None:
        """前台是希沃白板 / 希沃视频展台等程序时自动隐藏。"""
        keywords = [str(k).lower() for k in self.cfg.get("hide_keywords", []) if str(k).strip()]
        blocked = False
        if keywords:
            name = foreground_process_name()
            title = foreground_window_title().lower()
            haystack = f"{name} {title}"
            blocked = any(k in haystack for k in keywords)

        if blocked != self._foreground_blocked:
            self._foreground_blocked = blocked
            self.update_visibility()
        elif blocked:
            self.hide()


# --------------------------------------------------------------------------- #
# 设置窗口
# --------------------------------------------------------------------------- #
class SettingsDialog(QDialog):
    def __init__(self, cfg: dict, parent=None):
        super().__init__(parent)
        self.cfg = dict(cfg)
        self.setWindowTitle(f"{APP_TITLE} - 设置")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self.setMinimumWidth(480)

        form = QFormLayout()
        form.setLabelAlignment(Qt.AlignmentFlag.AlignRight)

        # 截止日期时间
        self.deadline_edit = QDateTimeEdit(self)
        self.deadline_edit.setCalendarPopup(True)
        self.deadline_edit.setDisplayFormat("yyyy-MM-dd HH:mm")
        self.deadline_edit.setDateTime(self._to_qdatetime(cfg.get("deadline")))
        form.addRow("截止日期时间：", self.deadline_edit)

        # 文字模板
        self.template_edit = QLineEdit(self)
        self.template_edit.setText(str(cfg.get("template", "")))
        self.template_edit.setPlaceholderText("例如：距离高考还有 {time} 天")
        form.addRow("显示文字：", self.template_edit)

        hint = QLabel(
            "可用占位符：{time} 或 {days} = 剩余天数，{date} = 截止日期，{name} = 事件名称", self
        )
        hint.setStyleSheet("color: #888; font-size: 12px;")
        form.addRow("", hint)

        # 多倒计时轮播
        self.rotate_spin = QSpinBox(self)
        self.rotate_spin.setRange(2, 600)
        self.rotate_spin.setSuffix(" 秒")
        self.rotate_spin.setValue(int(cfg.get("rotate_seconds", 5)))
        form.addRow("多倒计时轮播间隔：", self.rotate_spin)

        self.events_edit = QPlainTextEdit(self)
        self.events_edit.setPlaceholderText(
            "一模 | 2027-03-10 09:00\n二模 | 2027-05-05 09:00 | 距离{name}还有 {time} 天"
        )
        self.events_edit.setPlainText(events_to_text(cfg.get("events")))
        self.events_edit.setFixedHeight(88)
        self.events_edit.setFont(QFont("Consolas", 9))
        form.addRow("多倒计时列表：", self.events_edit)

        events_hint = QLabel(
            "每行一个：名称 | 截止时间 | 可选模板（第三段可省略）。\n"
            "留空则只显示上面的单个倒计时；填了就按列表轮播显示。",
            self,
        )
        events_hint.setStyleSheet("color: #888; font-size: 12px;")
        form.addRow("", events_hint)

        self.event_template_edit = QLineEdit(self)
        self.event_template_edit.setText(str(cfg.get("event_template", "")))
        form.addRow("多倒计时默认模板：", self.event_template_edit)
        self.event_template_edit.setPlaceholderText("距离{name}还有 {time} 天")

        # 特殊文字
        self.zero_edit = QLineEdit(self)
        self.zero_edit.setText(str(cfg.get("zero_text", "")))
        form.addRow("天数归零时显示：", self.zero_edit)

        self.past_edit = QLineEdit(self)
        self.past_edit.setText(str(cfg.get("past_text", "")))
        form.addRow("截止日期已过显示：", self.past_edit)

        # 字号
        self.base_size = QSpinBox(self)
        self.base_size.setRange(8, 120)
        self.base_size.setValue(int(cfg.get("base_font_size", 20)))
        form.addRow("常态字号：", self.base_size)

        self.urgent_days = QSpinBox(self)
        self.urgent_days.setRange(1, 365)
        self.urgent_days.setValue(int(cfg.get("urgent_days", 10)))
        form.addRow("紧急天数阈值（小于该天数）：", self.urgent_days)

        self.urgent_size = QSpinBox(self)
        self.urgent_size.setRange(8, 200)
        self.urgent_size.setValue(int(cfg.get("urgent_font_size", 34)))
        form.addRow("紧急时数字字号：", self.urgent_size)

        self.zero_size = QSpinBox(self)
        self.zero_size.setRange(8, 200)
        self.zero_size.setValue(int(cfg.get("zero_font_size", 30)))
        form.addRow("归零/结束时字号：", self.zero_size)

        # 颜色
        self.color_mode = QComboBox(self)
        self.color_mode.addItem("跟随系统（浅色/深色自动切换）", "system")
        self.color_mode.addItem("自定义颜色", "custom")
        index = self.color_mode.findData(cfg.get("color_mode", "system"))
        self.color_mode.setCurrentIndex(max(0, index))
        form.addRow("颜色方案：", self.color_mode)

        self.text_color = str(cfg.get("text_color", "#C62828"))
        self.bg_color = str(cfg.get("bg_color", "#FFFFFF"))
        self.text_color_btn = QPushButton(self)
        self.bg_color_btn = QPushButton(self)
        color_row = QHBoxLayout()
        color_row.addWidget(QLabel("文字", self))
        color_row.addWidget(self.text_color_btn)
        color_row.addWidget(QLabel("背景", self))
        color_row.addWidget(self.bg_color_btn)
        color_widget = QWidget(self)
        color_widget.setLayout(color_row)
        form.addRow("自定义颜色：", color_widget)

        self.urgent_color = str(cfg.get("urgent_color", "#E53935"))
        self.urgent_color_btn = QPushButton(self)
        form.addRow("紧急数字颜色：", self.urgent_color_btn)

        # 不透明度
        self.opacity = QSlider(Qt.Orientation.Horizontal, self)
        self.opacity.setRange(15, 100)
        self.opacity.setValue(int(float(cfg.get("opacity", 0.92)) * 100))
        form.addRow("背景不透明度：", self.opacity)

        # 自动隐藏关键词
        self.keywords_edit = QLineEdit(self)
        self.keywords_edit.setText(
            ",".join(str(k) for k in cfg.get("hide_keywords", []))
        )
        form.addRow("前台自动隐藏的程序关键词：", self.keywords_edit)

        keyword_hint = QLabel(
            "进程名或窗口标题包含这些词（逗号分隔）时自动隐藏，默认已含希沃白板/希沃视频展台", self
        )
        keyword_hint.setStyleSheet("color: #888; font-size: 12px;")
        form.addRow("", keyword_hint)

        # 开机自启
        self.autostart_box = QCheckBox("开机自动启动", self)
        self.autostart_box.setChecked(bool(cfg.get("autostart", True)))
        form.addRow("", self.autostart_box)

        # 预览
        self.preview = QLabel(self)
        self.preview.setTextFormat(Qt.TextFormat.RichText)
        self.preview.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.preview.setMinimumHeight(72)
        self.preview.setStyleSheet(
            "border:1px dashed #bbb; border-radius:10px; padding:10px;"
        )
        form.addRow("预览：", self.preview)

        buttons = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Ok
            | QDialogButtonBox.StandardButton.Cancel,
            self,
        )
        reset_btn = QPushButton("恢复默认", self)
        buttons.addButton(reset_btn, QDialogButtonBox.ButtonRole.ResetRole)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        reset_btn.clicked.connect(self.restore_defaults)

        layout = QVBoxLayout(self)
        layout.addLayout(form)
        layout.addWidget(buttons)

        # 联动
        self.deadline_edit.dateTimeChanged.connect(self.update_preview)
        self.template_edit.textChanged.connect(self.update_preview)
        self.zero_edit.textChanged.connect(self.update_preview)
        self.past_edit.textChanged.connect(self.update_preview)
        self.base_size.valueChanged.connect(self.update_preview)
        self.urgent_days.valueChanged.connect(self.update_preview)
        self.urgent_size.valueChanged.connect(self.update_preview)
        self.zero_size.valueChanged.connect(self.update_preview)
        self.color_mode.currentIndexChanged.connect(self._on_color_mode)
        self.text_color_btn.clicked.connect(lambda: self._pick_color("text"))
        self.bg_color_btn.clicked.connect(lambda: self._pick_color("bg"))
        self.urgent_color_btn.clicked.connect(lambda: self._pick_color("urgent"))

        self._sync_color_buttons()
        self.update_preview()

    # ---------------- 辅助 ----------------
    @staticmethod
    def _to_qdatetime(raw) -> QDateTime:
        try:
            dt = datetime.fromisoformat(str(raw))
        except Exception:
            try:
                dt = datetime.combine(date.fromisoformat(str(raw)[:10]), datetime.min.time())
            except Exception:
                dt = datetime.now()
        return QDateTime(dt.year, dt.month, dt.day, dt.hour, dt.minute, dt.second)

    def _on_color_mode(self, _index: int) -> None:
        self._sync_color_buttons()
        self.update_preview()

    def _sync_color_buttons(self) -> None:
        """自定义模式下文字/背景色可选；紧急红色始终可调。"""
        custom = self.color_mode.currentData() == "custom"
        self.text_color_btn.setEnabled(custom)
        self.bg_color_btn.setEnabled(custom)
        self.urgent_color_btn.setEnabled(True)
        self._paint_button(self.text_color_btn, self.text_color)
        self._paint_button(self.bg_color_btn, self.bg_color)
        self._paint_button(self.urgent_color_btn, self.urgent_color)

    @staticmethod
    def _paint_button(button: QPushButton, color: str) -> None:
        qcolor = QColor(color)
        text_color = "#000000" if qcolor.lightness() > 140 else "#FFFFFF"
        button.setText(color.upper())
        button.setStyleSheet(
            f"background-color:{color}; color:{text_color};"
            "border:1px solid #999; border-radius:4px; padding:4px 10px;"
        )

    def _pick_color(self, which: str) -> None:
        current = {
            "text": self.text_color,
            "bg": self.bg_color,
            "urgent": self.urgent_color,
        }[which]
        chosen = QColorDialog.getColor(QColor(current), self, "选择颜色")
        if not chosen.isValid():
            return
        value = chosen.name()
        if which == "text":
            self.text_color = value
        elif which == "bg":
            self.bg_color = value
        else:
            self.urgent_color = value
        self._sync_color_buttons()
        self.update_preview()

    def collect(self) -> dict:
        cfg = dict(self.cfg)
        cfg["deadline"] = self.deadline_edit.dateTime().toString("yyyy-MM-dd HH:mm:ss")
        cfg["template"] = self.template_edit.text()
        cfg["zero_text"] = self.zero_edit.text()
        cfg["past_text"] = self.past_edit.text()
        cfg["base_font_size"] = self.base_size.value()
        cfg["urgent_days"] = self.urgent_days.value()
        cfg["urgent_font_size"] = self.urgent_size.value()
        cfg["zero_font_size"] = self.zero_size.value()
        cfg["color_mode"] = self.color_mode.currentData()
        cfg["text_color"] = self.text_color
        cfg["bg_color"] = self.bg_color
        cfg["urgent_color"] = self.urgent_color
        cfg["opacity"] = self.opacity.value() / 100.0
        cfg["autostart"] = self.autostart_box.isChecked()
        cfg["events"] = parse_events_text(self.events_edit.toPlainText())
        cfg["rotate_seconds"] = self.rotate_spin.value()
        cfg["event_template"] = self.event_template_edit.text() or DEFAULT_CONFIG["event_template"]
        cfg["hide_keywords"] = [
            k.strip() for k in self.keywords_edit.text().split(",") if k.strip()
        ] or list(DEFAULT_HIDE_KEYWORDS)
        return cfg

    def update_preview(self) -> None:
        cfg = self.collect()
        days = days_left(cfg)
        text_color, bg_color = resolve_colors(cfg)
        self.preview.setStyleSheet(
            "border:1px dashed #bbb; border-radius:10px; padding:10px;"
            f"background-color:{bg_color}; color:{text_color};"
        )
        self.preview.setText(build_html(cfg, days))

    def restore_defaults(self) -> None:
        cfg = dict(DEFAULT_CONFIG)
        self.deadline_edit.setDateTime(self._to_qdatetime(cfg["deadline"]))
        self.template_edit.setText(cfg["template"])
        self.zero_edit.setText(cfg["zero_text"])
        self.past_edit.setText(cfg["past_text"])
        self.base_size.setValue(cfg["base_font_size"])
        self.urgent_days.setValue(cfg["urgent_days"])
        self.urgent_size.setValue(cfg["urgent_font_size"])
        self.zero_size.setValue(cfg["zero_font_size"])
        self.color_mode.setCurrentIndex(self.color_mode.findData(cfg["color_mode"]))
        self.text_color = cfg["text_color"]
        self.bg_color = cfg["bg_color"]
        self.urgent_color = cfg["urgent_color"]
        self.opacity.setValue(int(cfg["opacity"] * 100))
        self.autostart_box.setChecked(cfg["autostart"])
        self.keywords_edit.setText(",".join(cfg["hide_keywords"]))
        self.rotate_spin.setValue(cfg["rotate_seconds"])
        self.events_edit.setPlainText(events_to_text(cfg["events"]))
        self.event_template_edit.setText(cfg["event_template"])
        self._sync_color_buttons()
        self.update_preview()


# --------------------------------------------------------------------------- #
# 关于 / 设置(JSON)
# --------------------------------------------------------------------------- #
class ConfigEditorDialog(QDialog):
    """直接编辑 config.json 内容，保存后立即生效。"""

    def __init__(self, cfg: dict, parent=None):
        super().__init__(parent)
        self.result_cfg: dict | None = None
        self.setWindowTitle(f"{APP_TITLE} - 编辑设置（JSON）")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self.setMinimumSize(620, 520)

        hint = QLabel(
            "直接编辑下面这段 JSON，保存后立即生效；每次启动也会重新读取它。\n"
            "未知字段会被忽略，越界数值会自动修正，颜色必须写成 #RRGGBB。",
            self,
        )
        hint.setStyleSheet("color:#888;")

        self.editor = QPlainTextEdit(self)
        self.editor.setPlainText(config_to_json(cfg))
        self.editor.setFont(QFont("Consolas", 10))
        self.editor.setLineWrapMode(QPlainTextEdit.LineWrapMode.NoWrap)
        self.editor.setTabChangesFocus(False)

        buttons = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Save
            | QDialogButtonBox.StandardButton.Cancel,
            self,
        )
        buttons.button(QDialogButtonBox.StandardButton.Save).setText("保存并应用")
        buttons.button(QDialogButtonBox.StandardButton.Cancel).setText("取消")
        buttons.accepted.connect(self._save)
        buttons.rejected.connect(self.reject)

        fmt_btn = QPushButton("格式化", self)
        fmt_btn.clicked.connect(self._format)
        default_btn = QPushButton("填入默认值", self)
        default_btn.clicked.connect(
            lambda: self.editor.setPlainText(config_to_json(sanitize_config({})))
        )
        buttons.addButton(fmt_btn, QDialogButtonBox.ButtonRole.ResetRole)
        buttons.addButton(default_btn, QDialogButtonBox.ButtonRole.ResetRole)

        layout = QVBoxLayout(self)
        layout.addWidget(hint)
        layout.addWidget(self.editor)
        layout.addWidget(buttons)

    def _format(self) -> None:
        try:
            data = json.loads(self.editor.toPlainText())
        except json.JSONDecodeError as exc:
            QMessageBox.warning(self, APP_TITLE, f"JSON 格式有误：\n{exc}")
            return
        self.editor.setPlainText(config_to_json(data))

    def _save(self) -> None:
        try:
            data = json.loads(self.editor.toPlainText())
        except json.JSONDecodeError as exc:
            QMessageBox.warning(
                self, APP_TITLE, f"JSON 格式有误，无法保存：\n\n{exc}"
            )
            return
        if not isinstance(data, dict):
            QMessageBox.warning(self, APP_TITLE, "JSON 顶层必须是一个对象 { }。")
            return
        self.result_cfg = sanitize_config(data)
        self.accept()


class AboutDialog(QDialog):
    """关于：版本信息 + 当前设置（JSON 列出）+ 字段说明。"""

    def __init__(self, cfg: dict, on_apply=None, parent=None):
        super().__init__(parent)
        self.cfg = cfg
        self.on_apply = on_apply
        self.setWindowTitle(f"关于 {APP_TITLE}")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self.setMinimumSize(640, 520)

        icon_label = QLabel(self)
        icon_label.setPixmap(make_icon().pixmap(64, 64))
        icon_label.setFixedSize(72, 72)

        title = QLabel(f"<b style='font-size:17px;'>{APP_TITLE}</b> v{APP_VERSION}", self)
        desc = QLabel(APP_DESC, self)
        desc.setStyleSheet("color:#888;")
        author = QLabel(f"作者：{APP_AUTHOR}　·　Python 3 + PyQt6", self)
        path = QLabel(f"配置文件：{CONFIG_FILE}", self)
        path.setWordWrap(True)
        path.setTextInteractionFlags(Qt.TextInteractionFlag.TextSelectableByMouse)
        path.setStyleSheet("color:#888; font-size:12px;")

        head = QHBoxLayout()
        head.addWidget(icon_label)
        info = QVBoxLayout()
        info.addWidget(title)
        info.addWidget(desc)
        info.addWidget(author)
        info.addWidget(path)
        info.addStretch(1)
        head.addLayout(info, 1)

        self.json_view = QPlainTextEdit(self)
        self.json_view.setReadOnly(True)
        self.json_view.setFont(QFont("Consolas", 10))
        self.json_view.setLineWrapMode(QPlainTextEdit.LineWrapMode.NoWrap)
        self.json_view.setPlainText(config_to_json(self.cfg))

        field_view = QTextEdit(self)
        field_view.setReadOnly(True)
        field_view.setFont(QFont("Microsoft YaHei", 9))
        field_view.setHtml(fields_to_html())

        tabs = QTabWidget(self)
        tabs.addTab(self.json_view, "当前设置（config.json）")
        tabs.addTab(field_view, "字段说明")

        copy_btn = QPushButton("复制 JSON", self)
        copy_btn.clicked.connect(self._copy)
        edit_btn = QPushButton("编辑设置（JSON）…", self)
        edit_btn.clicked.connect(self._edit)
        reload_btn = QPushButton("重新读取", self)
        reload_btn.clicked.connect(self._reload)
        folder_btn = QPushButton("打开配置文件夹", self)
        folder_btn.clicked.connect(self._open_folder)
        close_btn = QPushButton("关闭", self)
        close_btn.clicked.connect(self.accept)

        row = QHBoxLayout()
        row.addWidget(copy_btn)
        row.addWidget(edit_btn)
        row.addWidget(reload_btn)
        row.addWidget(folder_btn)
        row.addStretch(1)
        row.addWidget(close_btn)

        layout = QVBoxLayout(self)
        layout.addLayout(head)
        layout.addWidget(tabs, 1)
        layout.addLayout(row)

    # ---------------- 操作 ----------------
    def _copy(self) -> None:
        QApplication.clipboard().setText(config_to_json(self.cfg))
        QMessageBox.information(self, APP_TITLE, "设置 JSON 已复制到剪贴板。")

    def _refresh(self, cfg: dict) -> None:
        self.cfg = cfg
        self.json_view.setPlainText(config_to_json(cfg))

    def _edit(self) -> None:
        editor = ConfigEditorDialog(self.cfg, self)
        if editor.exec() == QDialog.DialogCode.Accepted and editor.result_cfg:
            if self.on_apply:
                self.on_apply(editor.result_cfg)
            self._refresh(editor.result_cfg)

    def _reload(self) -> None:
        cfg = load_config()
        if self.on_apply:
            self.on_apply(cfg)
        self._refresh(cfg)

    def _open_folder(self) -> None:
        try:
            CONFIG_DIR.mkdir(parents=True, exist_ok=True)
            os.startfile(CONFIG_DIR)  # noqa: S606 - Windows 专用
        except Exception as exc:
            QMessageBox.warning(self, APP_TITLE, f"打开配置文件夹失败：\n{exc}")


# --------------------------------------------------------------------------- #
# 应用
# --------------------------------------------------------------------------- #
class CountdownApp:
    def __init__(self, app: QApplication):
        self.app = app
        # 每次启动都从 config.json 读取设置
        self.cfg = load_config()
        if not CONFIG_FILE.exists():
            save_config(self.cfg)   # 首次运行落盘，方便直接查看 / 编辑 JSON

        self.window = CountdownWindow(self.cfg)
        self.window.show()

        self.tray = QSystemTrayIcon(make_icon(), app)
        self.tray.setToolTip(plain_text(self.cfg, days_left(self.cfg)))
        self.tray.setContextMenu(self.build_menu())
        self.tray.activated.connect(self.on_tray_activated)
        self.tray.show()

        # 同步开机自启动：缺失、或指向旧路径（例如从 .py 换成了 exe）时重写
        if self.cfg.get("autostart") and autostart_command() != launch_command():
            set_autostart(True)

        self.tooltip_timer = QTimer(app)
        self.tooltip_timer.setInterval(30_000)
        self.tooltip_timer.timeout.connect(self.refresh_tray_tooltip)
        self.tooltip_timer.start()

    # ---------------- 托盘 ----------------
    def build_menu(self) -> QMenu:
        menu = QMenu()
        self.act_toggle = QAction("显示 / 隐藏悬浮窗", menu)
        self.act_toggle.triggered.connect(self.toggle_window)

        act_settings = QAction("设置…", menu)
        act_settings.triggered.connect(self.open_settings)

        act_json = QAction("编辑设置（JSON）…", menu)
        act_json.triggered.connect(self.open_config_editor)

        act_reload = QAction("重新读取配置", menu)
        act_reload.triggered.connect(self.reload_config)

        act_about = QAction("关于", menu)
        act_about.triggered.connect(self.open_about)

        self.act_autostart = QAction("开机自动启动", menu)
        self.act_autostart.setCheckable(True)
        self.act_autostart.setChecked(bool(self.cfg.get("autostart")))
        self.act_autostart.triggered.connect(self.toggle_autostart)

        act_refresh = QAction("立即刷新", menu)
        act_refresh.triggered.connect(self.refresh_now)

        self.act_next = QAction("下一个倒计时", menu)
        self.act_next.triggered.connect(self.show_next_item)

        act_quit = QAction("退出", menu)
        act_quit.triggered.connect(self.quit)

        menu.addAction(self.act_toggle)
        menu.addAction(act_refresh)
        menu.addAction(self.act_next)
        menu.addSeparator()
        menu.addAction(act_settings)
        menu.addAction(act_json)
        menu.addAction(act_reload)
        menu.addAction(self.act_autostart)
        menu.addSeparator()
        menu.addAction(act_about)
        menu.addSeparator()
        menu.addAction(act_quit)
        return menu

    def on_tray_activated(self, reason) -> None:
        if reason == QSystemTrayIcon.ActivationReason.Trigger:
            self.toggle_window()
        elif reason == QSystemTrayIcon.ActivationReason.DoubleClick:
            self.open_settings()

    def toggle_window(self) -> None:
        self.window.set_user_hidden(self.window.isVisible())

    def refresh_now(self) -> None:
        self.window.refresh_text()
        self.refresh_tray_tooltip()

    def show_next_item(self) -> None:
        self.window.next_item()
        self.refresh_tray_tooltip()

    def refresh_tray_tooltip(self) -> None:
        items = config_items(self.cfg)
        parts = [
            plain_text(self.cfg, days_until(item.get("deadline")), item)
            for item in items
        ]
        self.tray.setToolTip(f"{APP_TITLE}：" + " | ".join(parts))
        self.act_toggle.setText(
            "隐藏悬浮窗" if self.window.isVisible() else "显示悬浮窗"
        )
        self.act_next.setEnabled(len(items) > 1)

    def toggle_autostart(self) -> None:
        enabled = self.act_autostart.isChecked()
        ok = set_autostart(enabled)
        if not ok:
            self.act_autostart.setChecked(not enabled)
            self.tray.showMessage(
                APP_TITLE, "设置开机自启动失败，请查看 error.log", QSystemTrayIcon.MessageIcon.Warning, 4000
            )
            return
        self.cfg["autostart"] = enabled
        save_config(self.cfg)

    # ---------------- 设置 / 关于 ----------------
    def open_settings(self) -> None:
        dialog = SettingsDialog(self.cfg)
        if dialog.exec() == QDialog.DialogCode.Accepted:
            self.apply_config(dialog.collect())

    def open_config_editor(self) -> None:
        dialog = ConfigEditorDialog(self.cfg)
        if dialog.exec() == QDialog.DialogCode.Accepted and dialog.result_cfg:
            self.apply_config(dialog.result_cfg)
            self.tray.showMessage(
                APP_TITLE,
                "设置已保存到 config.json 并立即生效。",
                QSystemTrayIcon.MessageIcon.Information,
                3000,
            )

    def open_about(self) -> None:
        AboutDialog(self.cfg, on_apply=self.apply_config).exec()

    def reload_config(self) -> None:
        """放弃内存中的设置，重新从 config.json 读取。"""
        self.apply_config(load_config())
        self.tray.showMessage(
            APP_TITLE,
            "已重新读取 config.json。",
            QSystemTrayIcon.MessageIcon.Information,
            3000,
        )

    def apply_config(self, cfg: dict) -> None:
        cfg = sanitize_config(cfg)
        if cfg.get("autostart") != self.cfg.get("autostart"):
            set_autostart(bool(cfg.get("autostart")))
        self.cfg = cfg
        save_config(cfg)
        self.window.apply_config(cfg)
        self.window.set_user_hidden(False)
        self.act_autostart.setChecked(bool(cfg.get("autostart")))
        self.refresh_tray_tooltip()

    # ---------------- 退出 ----------------
    def quit(self) -> None:
        save_config(self.cfg)
        self.tray.hide()
        self.window.hide()
        self.app.quit()


def main() -> int:
    install_excepthook()

    app = QApplication(sys.argv)
    app.setApplicationName(APP_TITLE)
    app.setQuitOnLastWindowClosed(False)

    if single_instance_guard():
        return 0  # 已有实例在运行，静默退出

    if not QSystemTrayIcon.isSystemTrayAvailable():
        QMessageBox.warning(None, APP_TITLE, "当前系统不支持托盘图标。")
        return 1

    controller = CountdownApp(app)
    controller.refresh_tray_tooltip()
    return app.exec()


if __name__ == "__main__":
    sys.exit(main())
