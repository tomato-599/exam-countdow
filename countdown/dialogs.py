# -*- coding: utf-8 -*-
"""设置窗口、JSON 设置编辑器、关于窗口。"""

from __future__ import annotations

import json
import os
import tempfile
from datetime import date, datetime

from PyQt6.QtCore import QDateTime, QPointF, Qt, pyqtSignal
from PyQt6.QtGui import QColor, QFont, QPainter, QPen, QPixmap, QPolygonF
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
    QMessageBox,
    QPlainTextEdit,
    QPushButton,
    QScrollArea,
    QSlider,
    QSpinBox,
    QTabWidget,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from .config import (
    CONFIG_DIR,
    CONFIG_FILE,
    config_to_json,
    fields_to_html,
    load_config,
    parse_events,
    sanitize_config,
)
from .constants import APP_AUTHOR, APP_DESC, APP_TITLE, APP_VERSION, DEFAULT_CONFIG
from .render import build_html, days_left, resolve_colors
from .system import app_icon, is_system_dark


# --------------------------------------------------------------------------- #
# 设置窗口
# --------------------------------------------------------------------------- #
def fluent_palette() -> dict:
    """仿 Fluent 的调色板：跟随系统浅色 / 深色。"""
    if is_system_dark():
        return {
            "bg": "#202020",
            "panel": "#2B2B2B",
            "text": "#F3F3F3",
            "sub": "#9D9D9D",
            "border": "#3D3D3D",
            "hover": "#383838",
            "pressed": "#2D2D2D",
            "accent": "#60CDFF",
            "accent_hover": "#7BD9FF",
            "accent_text": "#1B1B1B",
            "danger": "#FF99A4",
        }
    return {
        "bg": "#F5F5F5",
        "panel": "#FFFFFF",
        "text": "#1B1B1B",
        "sub": "#6B6B6B",
        "border": "#E0E0E0",
        "hover": "#EFEFEF",
        "pressed": "#E5E5E5",
        "accent": "#0067C0",
        "accent_hover": "#106EBE",
        "accent_text": "#FFFFFF",
        "danger": "#C42B1C",
    }


_ARROW_CACHE: dict[tuple[str, str], str] = {}


def fluent_arrow_png(direction: str, color: str) -> str:
    """用 QPainter 画一个 Fluent 风格小箭头存成 PNG（QSS 的箭头只能用图片）。"""
    key = (direction, color)
    cached = _ARROW_CACHE.get(key)
    if cached and os.path.exists(cached):
        return cached
    out_dir = os.path.join(tempfile.gettempdir(), "gaokao_countdown_ui")
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, f"chevron_{direction}_{color.lstrip('#')}.png")
    size = 16
    pixmap = QPixmap(size, size)
    pixmap.fill(Qt.GlobalColor.transparent)
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing)
    pen = QPen(QColor(color))
    pen.setWidthF(1.8)
    pen.setCapStyle(Qt.PenCapStyle.RoundCap)
    pen.setJoinStyle(Qt.PenJoinStyle.RoundJoin)
    painter.setPen(pen)
    if direction == "down":
        points = (QPointF(4.5, 6.5), QPointF(8.0, 10.0), QPointF(11.5, 6.5))
    else:
        points = (QPointF(4.5, 9.5), QPointF(8.0, 6.0), QPointF(11.5, 9.5))
    painter.drawPolyline(QPolygonF(list(points)))
    painter.end()
    if not pixmap.save(path, "PNG"):
        raise RuntimeError(f"无法写入箭头图片：{path}")
    path = path.replace("\\", "/")  # QSS 的 url() 用正斜杠
    _ARROW_CACHE[key] = path
    return path


def fluent_style(pal: dict) -> str:
    """渲染设置窗口 QSS；箭头图片生成失败时退回到不带箭头的样式。"""
    qss = _FLUENT_QSS.format(**pal)
    try:
        down = fluent_arrow_png("down", pal["sub"])
        up = fluent_arrow_png("up", pal["sub"])
        qss += (
            "QComboBox::down-arrow, QDateTimeEdit::down-arrow {"
            f'image: url("{down}"); width: 16px; height: 16px;'
            "}\n"
            "QSpinBox::up-arrow {"
            f'image: url("{up}"); width: 12px; height: 12px;'
            "}\n"
            "QSpinBox::down-arrow {"
            f'image: url("{down}"); width: 12px; height: 12px;'
            "}\n"
        )
    except Exception:
        pass
    return qss


# 手写仿 Fluent 样式（不依赖第三方库）；{} 为占位符，渲染时填入调色板
_FLUENT_QSS = """
QDialog {{ background: {bg}; }}
QLabel {{ color: {text}; background: transparent; }}

QTabWidget {{ color: {text}; }}
QTabWidget::pane {{ border: none; background: transparent; top: 4px; }}
QTabBar::tab {{
    background: transparent; color: {sub};
    padding: 8px 16px; margin-right: 2px;
    border: none; border-bottom: 2px solid transparent;
}}
QTabBar::tab:hover {{
    color: {text}; background: {hover};
    border-top-left-radius: 6px; border-top-right-radius: 6px;
}}
QTabBar::tab:selected {{
    color: {accent}; border-bottom: 2px solid {accent};
    font-weight: 600;
}}

QFrame#card {{ background: {panel}; border: 1px solid {border}; border-radius: 8px; }}
QLabel#cardTitle {{ color: {sub}; font-weight: 600; }}
QLabel#hint {{ color: {sub}; font-size: 12px; }}

QLineEdit, QPlainTextEdit, QSpinBox, QDateTimeEdit, QComboBox {{
    background: {panel}; color: {text};
    border: 1px solid {border}; border-radius: 6px;
    padding: 4px 8px;
    selection-background-color: {accent}; selection-color: {accent_text};
}}
QSpinBox, QDateTimeEdit, QComboBox {{ padding-right: 26px; }}
QLineEdit:focus, QPlainTextEdit:focus, QSpinBox:focus,
QDateTimeEdit:focus, QComboBox:focus {{
    border: 1px solid {accent};
}}
QLineEdit:disabled, QSpinBox:disabled, QDateTimeEdit:disabled,
QComboBox:disabled {{
    color: {sub}; background: {hover};
}}

/* 下拉按钮（下拉框 / 日期时间）：去掉系统样式，改用自绘箭头图片 */
QComboBox::drop-down, QDateTimeEdit::drop-down {{
    subcontrol-origin: padding; subcontrol-position: center right;
    width: 24px; border: none; background: transparent;
}}

/* 数字微调按钮：右侧上下两块，悬停浅色高亮 */
QSpinBox::up-button {{
    subcontrol-origin: border; subcontrol-position: top right;
    width: 24px; border: none; border-top-right-radius: 6px;
}}
QSpinBox::down-button {{
    subcontrol-origin: border; subcontrol-position: bottom right;
    width: 24px; border: none; border-bottom-right-radius: 6px;
}}
QSpinBox::up-button:hover, QSpinBox::down-button:hover {{ background: {hover}; }}
QSpinBox::up-button:pressed, QSpinBox::down-button:pressed {{ background: {pressed}; }}

/* 下拉弹出列表 */
QComboBox QAbstractItemView {{
    background: {panel}; color: {text};
    border: 1px solid {border}; border-radius: 6px; padding: 4px;
    selection-background-color: {hover}; selection-color: {text};
    outline: none;
}}
QComboBox QAbstractItemView::item {{
    min-height: 26px; padding: 0 8px; border-radius: 4px;
}}

QPushButton {{
    background: {panel}; color: {text};
    border: 1px solid {border}; border-radius: 6px;
    padding: 6px 16px;
}}
QPushButton:hover {{ background: {hover}; }}
QPushButton:pressed {{ background: {pressed}; }}
QPushButton:disabled {{ color: {sub}; }}
QPushButton:default {{
    background: {accent}; color: {accent_text}; border-color: {accent};
}}
QPushButton:default:hover {{
    background: {accent_hover}; border-color: {accent_hover};
}}

QCheckBox {{ color: {text}; spacing: 8px; }}
QCheckBox::indicator {{
    width: 18px; height: 18px;
    border: 1px solid {sub}; border-radius: 4px;
    background: {panel};
}}
QCheckBox::indicator:hover {{ border-color: {accent}; }}
QCheckBox::indicator:checked {{ background: {accent}; border-color: {accent}; }}

QSlider::groove:horizontal {{
    height: 4px; background: {border}; border-radius: 2px;
}}
QSlider::sub-page:horizontal {{ background: {accent}; border-radius: 2px; }}
QSlider::handle:horizontal {{
    width: 16px; height: 16px; margin: -6px 0;
    border-radius: 8px; background: {panel}; border: 1px solid {sub};
}}
QSlider::handle:horizontal:hover {{ border-color: {accent}; }}

/* 动态倒计时列表 */
QScrollArea#eventList {{
    background: transparent;
    border: 1px solid {border}; border-radius: 6px;
}}
QScrollArea#eventList > QWidget > QWidget {{ background: transparent; }}
QPushButton#eventDelete {{
    color: {sub}; background: transparent;
    border: 1px solid transparent; padding: 4px 8px;
}}
QPushButton#eventDelete:hover {{
    color: {danger}; background: {hover}; border-color: {border};
}}
QPushButton#eventDelete:pressed {{ background: {pressed}; }}

QScrollBar:vertical {{
    background: transparent; width: 8px; margin: 2px;
}}
QScrollBar::handle:vertical {{
    background: {border}; border-radius: 4px; min-height: 24px;
}}
QScrollBar::handle:vertical:hover {{ background: {sub}; }}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{ height: 0; }}
QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical {{ background: transparent; }}
"""


def _to_qdatetime(raw) -> QDateTime:
    try:
        dt = datetime.fromisoformat(str(raw))
    except Exception:
        try:
            dt = datetime.combine(date.fromisoformat(str(raw)[:10]), datetime.min.time())
        except Exception:
            dt = datetime.now()
    return QDateTime(dt.year, dt.month, dt.day, dt.hour, dt.minute, dt.second)


class EventRow(QWidget):
    """动态列表中的一行：名称、截止时间、持续天数、可选模板、删除。"""

    changed = pyqtSignal()
    remove_requested = pyqtSignal(object)

    def __init__(self, event: dict | None, parent=None):
        super().__init__(parent)
        event = event or {}
        layout = QHBoxLayout(self)
        layout.setContentsMargins(2, 2, 2, 2)
        layout.setSpacing(6)

        self.name_edit = QLineEdit(self)
        self.name_edit.setPlaceholderText("名称，如 一模")
        self.name_edit.setText(str(event.get("name") or ""))
        self.name_edit.setMinimumWidth(88)
        self.name_edit.setToolTip("倒计时条目名称")

        self.deadline_edit = QDateTimeEdit(self)
        self.deadline_edit.setCalendarPopup(True)
        self.deadline_edit.setDisplayFormat("yyyy-MM-dd HH:mm")
        self.deadline_edit.setDateTime(_to_qdatetime(event.get("deadline")))
        self.deadline_edit.setFixedWidth(190)
        self.deadline_edit.setToolTip("这条倒计时的截止时间（开考时间）")

        self.duration_spin = QSpinBox(self)
        self.duration_spin.setRange(0, 365)
        self.duration_spin.setSpecialValueText("留空=1天")
        self.duration_spin.setSuffix(" 天")
        self.duration_spin.setValue(int(event.get("duration_days") or 1))
        self.duration_spin.setFixedWidth(104)
        self.duration_spin.setToolTip("考试持续天数（含截止当天）；留空按 1 天")

        self.template_edit = QLineEdit(self)
        self.template_edit.setPlaceholderText("可选模板，留空用默认模板")
        self.template_edit.setText(str(event.get("template") or ""))
        self.template_edit.setMinimumWidth(110)
        self.template_edit.setToolTip("支持 {name}、{time}、{date}；留空使用默认模板")

        self.remove_btn = QPushButton("删除", self)
        self.remove_btn.setObjectName("eventDelete")
        self.remove_btn.setFixedWidth(48)
        self.remove_btn.setToolTip("删除这条倒计时")
        self.remove_btn.clicked.connect(lambda: self.remove_requested.emit(self))

        layout.addWidget(self.name_edit, 2)
        layout.addWidget(self.deadline_edit)
        layout.addWidget(self.duration_spin)
        layout.addWidget(self.template_edit, 3)
        layout.addWidget(self.remove_btn)

        self.name_edit.textChanged.connect(lambda _text: self.changed.emit())
        self.template_edit.textChanged.connect(lambda _text: self.changed.emit())
        self.deadline_edit.dateTimeChanged.connect(lambda _dt: self.changed.emit())
        self.duration_spin.valueChanged.connect(lambda _value: self.changed.emit())

    def is_blank(self) -> bool:
        return not self.name_edit.text().strip() and not self.template_edit.text().strip()

    def to_config(self) -> dict | None:
        if self.is_blank():
            return None
        return {
            "name": self.name_edit.text().strip(),
            "deadline": self.deadline_edit.dateTime().toString("yyyy-MM-dd HH:mm:ss"),
            "template": self.template_edit.text().strip(),
            "duration_days": max(1, self.duration_spin.value()),
        }


class EventListWidget(QScrollArea):
    """可增删的倒计时条目列表，顺序即轮播顺序。"""

    changed = pyqtSignal()

    def __init__(self, events=None, parent=None):
        super().__init__(parent)
        self._rows: list[EventRow] = []
        self.setObjectName("eventList")
        self.setWidgetResizable(True)
        self.setFrameShape(QFrame.Shape.NoFrame)
        self.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        self.setVerticalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAsNeeded)

        host = QWidget(self)
        host.setObjectName("eventListHost")
        self._layout = QVBoxLayout(host)
        self._layout.setContentsMargins(6, 6, 6, 6)
        self._layout.setSpacing(6)

        self._empty = QLabel("还没有倒计时条目，点击下方「添加倒计时」新建一条。", host)
        self._empty.setObjectName("hint")
        self._empty.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._layout.addWidget(self._empty)
        self._layout.addStretch(1)
        self.setWidget(host)

        self.set_events(events)

    def set_events(self, events) -> None:
        for row in self._rows:
            row.setParent(None)
            row.deleteLater()
        self._rows.clear()
        for event in parse_events(events):
            self.add_row(event)
        self._sync()

    def add_row(self, event: dict | None = None) -> None:
        row = EventRow(event, self)
        row.changed.connect(self.changed.emit)
        row.remove_requested.connect(self._remove_row)
        self._rows.append(row)
        self._layout.insertWidget(self._layout.count() - 1, row)
        self._sync()

    def _remove_row(self, row: EventRow) -> None:
        if row not in self._rows:
            return
        self._rows.remove(row)
        row.setParent(None)
        row.deleteLater()
        self._sync()

    def to_events(self) -> list[dict]:
        events = []
        for row in self._rows:
            event = row.to_config()
            if event is not None:
                events.append(event)
        return events

    def has_events(self) -> bool:
        return bool(self.to_events())

    def _sync(self) -> None:
        self._empty.setVisible(not self._rows)
        if not self._rows:
            height = 52
        else:
            margins = self._layout.contentsMargins()
            content = sum(row.sizeHint().height() for row in self._rows)
            content += self._layout.spacing() * (len(self._rows) - 1)
            content += margins.top() + margins.bottom()
            height = max(52, min(220, content + 2))
        self.setFixedHeight(height)
        self.changed.emit()


class SettingsDialog(QDialog):
    def __init__(self, cfg: dict, parent=None):
        super().__init__(parent)
        self.cfg = dict(cfg)
        self._pal = fluent_palette()
        self.setWindowTitle(f"{APP_TITLE} - 设置")
        self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self.setMinimumWidth(680)
        self.setStyleSheet(fluent_style(self._pal))

        tabs = QTabWidget(self)

        # ---------------- 倒计时 ----------------
        page, page_layout = self._page()
        card, form = self._card(page, "截止日期")
        self.deadline_edit = QDateTimeEdit(card)
        self.deadline_edit.setCalendarPopup(True)
        self.deadline_edit.setDisplayFormat("yyyy-MM-dd HH:mm")
        self.deadline_edit.setDateTime(_to_qdatetime(cfg.get("deadline")))
        form.addRow("截止日期时间：", self.deadline_edit)

        self.duration_spin = QSpinBox(card)
        self.duration_spin.setRange(0, 365)
        self.duration_spin.setSpecialValueText("留空 = 1 天")
        self.duration_spin.setSuffix(" 天")
        self.duration_spin.setValue(int(cfg.get("duration_days", 1)))
        self.duration_spin.setToolTip("考试持续天数（含截止当天）；留空按 1 天")
        form.addRow("考试持续天数：", self.duration_spin)
        page_layout.addWidget(card)

        card, form = self._card(page, "显示文字")
        self.template_edit = QLineEdit(card)
        self.template_edit.setText(str(cfg.get("template", "")))
        self.template_edit.setPlaceholderText("例如：距离高考还有 {time} 天")
        form.addRow("显示文字：", self.template_edit)

        hint = QLabel(
            "可用占位符：{time} 或 {days} = 剩余天数，{date} = 截止日期，{name} = 事件名称；\n"
            "进行中文字还支持 {day} = 第几天，{total} = 总天数。",
            card,
        )
        hint.setObjectName("hint")
        hint.setWordWrap(True)
        form.addRow("", hint)

        self.zero_edit = QLineEdit(card)
        self.zero_edit.setText(str(cfg.get("zero_text", "")))
        form.addRow("考试第一天显示：", self.zero_edit)

        self.ongoing_edit = QLineEdit(card)
        self.ongoing_edit.setText(str(cfg.get("ongoing_text", "")))
        self.ongoing_edit.setPlaceholderText("考试进行中 · 第{day}天")
        form.addRow("考试进行中显示：", self.ongoing_edit)

        self.past_edit = QLineEdit(card)
        self.past_edit.setText(str(cfg.get("past_text", "")))
        form.addRow("考试结束后显示：", self.past_edit)
        page_layout.addWidget(card)
        page_layout.addStretch(1)
        tabs.addTab(page, "倒计时")

        # ---------------- 多倒计时 ----------------
        page, page_layout = self._page()
        card, form = self._card(page, "轮播")
        self.rotate_spin = QSpinBox(card)
        self.rotate_spin.setRange(2, 600)
        self.rotate_spin.setSuffix(" 秒")
        self.rotate_spin.setValue(int(cfg.get("rotate_seconds", 5)))
        form.addRow("轮播间隔：", self.rotate_spin)

        self.event_template_edit = QLineEdit(card)
        self.event_template_edit.setText(str(cfg.get("event_template", "")))
        self.event_template_edit.setPlaceholderText("距离{name}还有 {time} 天")
        form.addRow("默认模板：", self.event_template_edit)
        page_layout.addWidget(card)

        card, form = self._card(page, "倒计时列表")
        box = card.layout()
        self.event_list = EventListWidget(cfg.get("events"), card)
        box.addWidget(self.event_list)

        add_btn = QPushButton("+ 添加倒计时", card)
        add_btn.clicked.connect(self._add_event)
        box.addWidget(add_btn, 0, Qt.AlignmentFlag.AlignLeft)

        events_hint = QLabel(
            "每行一条，顺序即轮播顺序；名称和模板都为空的行会被忽略。\n"
            "每条可单独设置持续天数（留空 = 1 天，当天结束）；进行中文字在“倒计时”页统一设置。\n"
            "条目模板留空时使用上方「默认模板」；配置列表后，“倒计时”页的截止日期和持续天数不再生效。",
            card,
        )
        events_hint.setObjectName("hint")
        events_hint.setWordWrap(True)
        box.addWidget(events_hint)
        page_layout.addWidget(card)
        page_layout.addStretch(1)
        tabs.addTab(page, "多倒计时")

        self.event_list.changed.connect(self._update_deadline_enabled)

        # ---------------- 外观 ----------------
        page, page_layout = self._page()
        card, form = self._card(page, "字号")
        self.base_size = QSpinBox(card)
        self.base_size.setRange(8, 120)
        self.base_size.setValue(int(cfg.get("base_font_size", 20)))
        form.addRow("常态字号：", self.base_size)

        self.urgent_days = QSpinBox(card)
        self.urgent_days.setRange(1, 365)
        self.urgent_days.setValue(int(cfg.get("urgent_days", 10)))
        form.addRow("紧急天数阈值（小于该天数）：", self.urgent_days)

        self.urgent_size = QSpinBox(card)
        self.urgent_size.setRange(8, 200)
        self.urgent_size.setValue(int(cfg.get("urgent_font_size", 34)))
        form.addRow("紧急时数字字号：", self.urgent_size)

        self.zero_size = QSpinBox(card)
        self.zero_size.setRange(8, 200)
        self.zero_size.setValue(int(cfg.get("zero_font_size", 30)))
        form.addRow("归零/结束时字号：", self.zero_size)
        page_layout.addWidget(card)

        card, form = self._card(page, "颜色")
        self.color_mode = QComboBox(card)
        self.color_mode.addItem("跟随系统（浅色/深色自动切换）", "system")
        self.color_mode.addItem("自定义颜色", "custom")
        index = self.color_mode.findData(cfg.get("color_mode", "system"))
        self.color_mode.setCurrentIndex(max(0, index))
        form.addRow("颜色方案：", self.color_mode)

        self.text_color = str(cfg.get("text_color", "#C62828"))
        self.bg_color = str(cfg.get("bg_color", "#FFFFFF"))
        self.text_color_btn = QPushButton(card)
        self.bg_color_btn = QPushButton(card)
        color_row = QHBoxLayout()
        color_row.addWidget(QLabel("文字", card))
        color_row.addWidget(self.text_color_btn)
        color_row.addWidget(QLabel("背景", card))
        color_row.addWidget(self.bg_color_btn)
        color_widget = QWidget(card)
        color_widget.setLayout(color_row)
        form.addRow("自定义颜色：", color_widget)

        self.urgent_color = str(cfg.get("urgent_color", "#E53935"))
        self.urgent_color_btn = QPushButton(card)
        form.addRow("紧急数字颜色：", self.urgent_color_btn)
        page_layout.addWidget(card)

        card, form = self._card(page, "背景与预览")
        self.opacity = QSlider(Qt.Orientation.Horizontal, card)
        self.opacity.setRange(15, 100)
        self.opacity.setValue(int(float(cfg.get("opacity", 0.92)) * 100))
        form.addRow("背景不透明度：", self.opacity)

        self.preview = QLabel(card)
        self.preview.setTextFormat(Qt.TextFormat.RichText)
        self.preview.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.preview.setMinimumHeight(72)
        form.addRow("预览：", self.preview)
        page_layout.addWidget(card)
        page_layout.addStretch(1)
        tabs.addTab(page, "外观")

        # ---------------- 行为 ----------------
        page, page_layout = self._page()
        card, form = self._card(page, "自动隐藏")
        self.keywords_edit = QLineEdit(card)
        self.keywords_edit.setText(
            ",".join(str(k) for k in cfg.get("hide_keywords", []))
        )
        form.addRow("程序关键词：", self.keywords_edit)

        keyword_hint = QLabel(
            "进程名或窗口标题包含这些词（逗号分隔）时自动隐藏，默认已含希沃白板/希沃视频展台",
            card,
        )
        keyword_hint.setObjectName("hint")
        keyword_hint.setWordWrap(True)
        form.addRow("", keyword_hint)
        page_layout.addWidget(card)

        card, form = self._card(page, "上学日与节假日")
        self.school_days_box = QCheckBox(
            "只计上学日（扣除周末和法定节假日，调休补班的周末算上课）", card
        )
        self.school_days_box.setChecked(bool(cfg.get("school_days", False)))
        form.addRow("", self.school_days_box)

        school_hint = QLabel(
            "按国务院办公厅放假安排扣除周六、周日与放假天数；调休补班的周末仍计入；\n"
            "数据来自内置表 + GitHub 仓库 tomato-599/holidays_count 的节假日 JSON（自动获取、本地缓存）。",
            card,
        )
        school_hint.setObjectName("hint")
        school_hint.setWordWrap(True)
        form.addRow("", school_hint)

        self.holiday_auto_box = QCheckBox("自动从 GitHub 更新节假日 / 调休数据", card)
        self.holiday_auto_box.setChecked(bool(cfg.get("holiday_auto", True)))
        form.addRow("", self.holiday_auto_box)

        holiday_hint = QLabel(
            "启动时与每小时检查更新，获取失败时继续用缓存或内置数据；\n"
            "仓库 / 分支 / 文件名可在 JSON 编辑器里改。",
            card,
        )
        holiday_hint.setObjectName("hint")
        holiday_hint.setWordWrap(True)
        form.addRow("", holiday_hint)
        page_layout.addWidget(card)

        card, form = self._card(page, "启动")
        self.autostart_box = QCheckBox("开机自动启动", card)
        self.autostart_box.setChecked(bool(cfg.get("autostart", True)))
        form.addRow("", self.autostart_box)
        page_layout.addWidget(card)
        page_layout.addStretch(1)
        tabs.addTab(page, "行为")

        # ---------------- 底部按钮 ----------------
        buttons = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Ok
            | QDialogButtonBox.StandardButton.Cancel,
            self,
        )
        buttons.button(QDialogButtonBox.StandardButton.Ok).setText("确定")
        buttons.button(QDialogButtonBox.StandardButton.Cancel).setText("取消")
        reset_btn = QPushButton("恢复默认", self)
        buttons.addButton(reset_btn, QDialogButtonBox.ButtonRole.ResetRole)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        reset_btn.clicked.connect(self.restore_defaults)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(18, 14, 18, 14)
        layout.setSpacing(12)
        layout.addWidget(tabs, 1)
        layout.addWidget(buttons)

        # 联动
        self.deadline_edit.dateTimeChanged.connect(self.update_preview)
        self.duration_spin.valueChanged.connect(self.update_preview)
        self.template_edit.textChanged.connect(self.update_preview)
        self.zero_edit.textChanged.connect(self.update_preview)
        self.ongoing_edit.textChanged.connect(self.update_preview)
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
        self._update_deadline_enabled()
        self.update_preview()

    # ---------------- 辅助 ----------------
    def _page(self) -> tuple[QWidget, QVBoxLayout]:
        """一个标签页：竖向排列多张设置卡片。"""
        page = QWidget(self)
        page.setObjectName("tabPage")
        layout = QVBoxLayout(page)
        layout.setContentsMargins(4, 12, 4, 4)
        layout.setSpacing(10)
        return page, layout

    def _card(self, parent: QWidget, title: str) -> tuple[QFrame, QFormLayout]:
        """仿 Fluent 的设置卡片：标题 + 右对齐标签的表单。"""
        card = QFrame(parent)
        card.setObjectName("card")
        box = QVBoxLayout(card)
        box.setContentsMargins(16, 12, 16, 14)
        box.setSpacing(8)
        head = QLabel(title, card)
        head.setObjectName("cardTitle")
        box.addWidget(head)
        form = QFormLayout()
        form.setLabelAlignment(Qt.AlignmentFlag.AlignRight)
        form.setContentsMargins(0, 0, 0, 0)
        form.setHorizontalSpacing(12)
        form.setVerticalSpacing(8)
        box.addLayout(form)
        return card, form

    def _update_deadline_enabled(self) -> None:
        """配了多倒计时列表时，单个截止日期和持续天数不再起作用。"""
        has_events = self.event_list.has_events()
        self.deadline_edit.setEnabled(not has_events)
        self.duration_spin.setEnabled(not has_events)
        tip = "已配置多倒计时列表，单个倒计时不再生效" if has_events else ""
        self.deadline_edit.setToolTip(tip)
        self.duration_spin.setToolTip(
            tip or "考试持续天数（含截止当天）；留空按 1 天"
        )

    def _add_event(self) -> None:
        deadline = self.deadline_edit.dateTime().toString("yyyy-MM-dd HH:mm:ss")
        self.event_list.add_row(
            {
                "name": "",
                "deadline": deadline,
                "template": "",
                "duration_days": max(1, self.duration_spin.value()),
            }
        )

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
            "border:1px solid #999; border-radius:6px; padding:4px 12px;"
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
        cfg["duration_days"] = max(1, self.duration_spin.value())
        cfg["template"] = self.template_edit.text()
        cfg["zero_text"] = self.zero_edit.text()
        cfg["ongoing_text"] = self.ongoing_edit.text()
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
        cfg["school_days"] = self.school_days_box.isChecked()
        cfg["holiday_auto"] = self.holiday_auto_box.isChecked()
        cfg["events"] = self.event_list.to_events()
        cfg["rotate_seconds"] = self.rotate_spin.value()
        cfg["event_template"] = self.event_template_edit.text() or DEFAULT_CONFIG["event_template"]
        cfg["hide_keywords"] = [
            k.strip() for k in self.keywords_edit.text().split(",") if k.strip()
        ] or list(DEFAULT_CONFIG["hide_keywords"])
        return cfg

    def update_preview(self) -> None:
        cfg = self.collect()
        days = days_left(cfg)
        text_color, bg_color = resolve_colors(cfg)
        self.preview.setStyleSheet(
            f"border:1px dashed {self._pal['border']}; border-radius:10px; padding:10px;"
            f"background-color:{bg_color}; color:{text_color};"
        )
        self.preview.setText(build_html(cfg, days))

    def restore_defaults(self) -> None:
        cfg = dict(DEFAULT_CONFIG)
        self.deadline_edit.setDateTime(_to_qdatetime(cfg["deadline"]))
        self.duration_spin.setValue(cfg["duration_days"])
        self.template_edit.setText(cfg["template"])
        self.zero_edit.setText(cfg["zero_text"])
        self.ongoing_edit.setText(cfg["ongoing_text"])
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
        self.school_days_box.setChecked(cfg["school_days"])
        self.holiday_auto_box.setChecked(cfg["holiday_auto"])
        self.keywords_edit.setText(",".join(cfg["hide_keywords"]))
        self.rotate_spin.setValue(cfg["rotate_seconds"])
        self.event_list.set_events(cfg["events"])
        self.event_template_edit.setText(cfg["event_template"])
        self._sync_color_buttons()
        self.update_preview()


# --------------------------------------------------------------------------- #
# 设置（JSON）
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


# --------------------------------------------------------------------------- #
# 关于
# --------------------------------------------------------------------------- #
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
        icon_label.setPixmap(app_icon().pixmap(64, 64))
        icon_label.setFixedSize(72, 72)

        title = QLabel(f"<b style='font-size:17px;'>{APP_TITLE}</b> {APP_VERSION}", self)
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
