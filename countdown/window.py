# -*- coding: utf-8 -*-
"""置顶悬浮窗：悬停自动隐藏、前台程序避让、跨天刷新、多倒计时轮播。"""

from __future__ import annotations

from PyQt6.QtCore import QRect, Qt, QTimer
from PyQt6.QtGui import QCursor, QFont
from PyQt6.QtWidgets import QApplication, QFrame, QLabel, QVBoxLayout, QWidget

from .config import config_items, save_config
from .constants import APP_TITLE
from .render import apply_card_style, build_html, count_days
from .system import foreground_process_name, foreground_window_title


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

        # 拖拽（按住左键从窗外滑入窗口后开始）
        self._drag_offset = None
        self._drag_timer = QTimer(self)
        self._drag_timer.setInterval(15)
        self._drag_timer.timeout.connect(self._drag_tick)

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
                build_html(self.cfg, count_days(self.cfg, item.get("deadline")), item)
            )
            hint = self.label.sizeHint()
            widest = max(widest, hint.width())
            tallest = max(tallest, hint.height())
        self.label.setMinimumSize(widest, tallest)

    def refresh_text(self) -> None:
        item = self.current_item()
        days = count_days(self.cfg, item.get("deadline"))
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
        if self._drag_timer.isActive():
            return  # 拖拽中不重新定位
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
        """鼠标挪到悬浮窗上就隐藏，完全不挡操作；按住左键滑入则转为拖拽。"""
        if not self.isVisible():
            super().enterEvent(event)
            return
        if QApplication.mouseButtons() & Qt.MouseButton.LeftButton:
            self._drag_offset = QCursor.pos() - self.frameGeometry().topLeft()
            self._drag_timer.start()
        else:
            self._hover_hidden = True
            self._hover_rect = self.frameGeometry()
            self.hide()
            self._hover_timer.start()
        super().enterEvent(event)

    def _drag_tick(self) -> None:
        if not QApplication.mouseButtons() & Qt.MouseButton.LeftButton:
            self._drag_timer.stop()
            self._persist_position()
            return
        self.move(QCursor.pos() - self._drag_offset)

    def _persist_position(self) -> None:
        """松手后把当前位置换算成边距写回配置，下次启动保持。"""
        frame = self.frameGeometry()
        screen = QApplication.screenAt(frame.center()) or QApplication.primaryScreen()
        if screen is None:
            return
        area = screen.availableGeometry()
        mx = max(0, min(4000, area.right() - frame.right()))
        my = max(0, min(4000, frame.top() - area.top()))
        if mx == self.cfg.get("margin_x") and my == self.cfg.get("margin_y"):
            return
        self.cfg["margin_x"] = mx
        self.cfg["margin_y"] = my
        save_config(self.cfg)

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
