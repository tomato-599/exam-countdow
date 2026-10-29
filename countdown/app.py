# -*- coding: utf-8 -*-
"""应用控制器：系统托盘菜单、设置联动、主循环入口。"""

from __future__ import annotations

import json
import sys
import threading
import traceback

from PyQt6.QtCore import QObject, QTimer, pyqtSignal
from PyQt6.QtGui import QAction
from PyQt6.QtWidgets import (
    QApplication,
    QDialog,
    QFileDialog,
    QMenu,
    QMessageBox,
    QSystemTrayIcon,
)

from .config import (
    CONFIG_FILE,
    config_items,
    config_to_json,
    install_excepthook,
    load_config,
    log_error,
    sanitize_config,
    save_config,
)
from .constants import APP_TITLE, APP_VERSION
from .dialogs import (
    AboutDialog,
    ConfigEditorDialog,
    SettingsDialog,
    require_settings_password,
)
from .holidays import HolidayStore
from .render import count_days, days_left, plain_text
from .system import (
    app_icon,
    autostart_command,
    launch_command,
    set_autostart,
    single_instance_guard,
)
from .update import fetch_latest_version, is_newer
from .window import CountdownWindow


class _HolidayBridge(QObject):
    """把后台线程的节假日获取结果带回 UI 线程。"""
    finished = pyqtSignal(bool, str)


class _UpdateBridge(QObject):
    """把后台版本检查结果带回 UI 线程。"""
    finished = pyqtSignal(bool, str, str)  # 成功?, 最新版本, 错误信息


class CountdownApp:
    def __init__(self, app: QApplication):
        self.app = app
        # 每次启动都从 config.json 读取设置
        self.cfg = load_config()
        if not CONFIG_FILE.exists():
            save_config(self.cfg)   # 首次运行落盘，方便直接查看 / 编辑 JSON

        # 节假日数据：先读本地缓存（断网也能用），视情况后台从 GitHub 更新
        self.holiday_store = HolidayStore(self.cfg)
        self.holiday_store.load_cache()
        self._holiday_bridge = _HolidayBridge(self.app)
        self._holiday_bridge.finished.connect(self.on_holiday_fetched)
        self._holiday_thread: threading.Thread | None = None
        self._holiday_notify = False

        # 版本检查状态：只提示有新版本，不下载不安装
        self._update_bridge = _UpdateBridge(self.app)
        self._update_bridge.finished.connect(self.on_update_checked)
        self._update_thread: threading.Thread | None = None
        self._update_manual = False
        self._notified_version = ""

        self.window = CountdownWindow(self.cfg)
        self.window.show()

        self.tray = QSystemTrayIcon(app_icon(), app)
        self.tray.setToolTip(plain_text(self.cfg, days_left(self.cfg)))
        self.tray.setContextMenu(self.build_menu())
        self.tray.activated.connect(self.on_tray_activated)
        self.tray.show()

        # 同步开机自启动：缺失、或指向旧路径（例如从 .py 换成了 exe）时重写
        if self.cfg.get("autostart") and autostart_command() != launch_command():
            set_autostart(True)

        self.update_holiday_menu()

        if self.cfg.get("holiday_auto"):
            self.maybe_refresh_holidays()
            self._holiday_timer = QTimer(app)
            self._holiday_timer.setInterval(3_600_000)  # 每小时检查一次是否过期
            self._holiday_timer.timeout.connect(self.maybe_refresh_holidays)
            self._holiday_timer.start()

        # 版本检查：启动 2 秒后查一次，之后每 24 小时一次（后台线程，断网静默）
        self._update_timer = QTimer(app)
        self._update_timer.setInterval(24 * 3_600_000)
        self._update_timer.timeout.connect(self.check_updates)
        self._update_timer.start()
        QTimer.singleShot(2_000, self.check_updates)

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

        act_export = QAction("导出配置…", menu)
        act_export.triggered.connect(self.export_config)

        act_import = QAction("导入配置…", menu)
        act_import.triggered.connect(self.import_config)

        act_reload = QAction("重新读取配置", menu)
        act_reload.triggered.connect(self.reload_config)

        # 节假日数据（GitHub）：第一行只显示状态，第二行手动更新
        self.act_holiday_info = QAction(menu)
        self.act_holiday_info.setEnabled(False)

        act_holiday_update = QAction("立即更新节假日数据", menu)
        act_holiday_update.triggered.connect(self.update_holidays_now)

        act_check_update = QAction("检查新版本", menu)
        act_check_update.triggered.connect(lambda: self.check_updates(manual=True))

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
        menu.addAction(act_export)
        menu.addAction(act_import)
        menu.addAction(act_reload)
        menu.addAction(self.act_holiday_info)
        menu.addAction(act_holiday_update)
        menu.addAction(act_check_update)
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
            plain_text(self.cfg, count_days(self.cfg, item.get("deadline")), item)
            for item in items
        ]
        self.tray.setToolTip(f"{APP_TITLE}：" + " | ".join(parts))
        self.act_toggle.setText(
            "隐藏悬浮窗" if self.window.isVisible() else "显示悬浮窗"
        )
        self.act_next.setEnabled(len(items) > 1)

    # ---------------- 节假日数据（GitHub） ----------------
    def update_holiday_menu(self) -> None:
        self.act_holiday_info.setText(
            f"节假日数据：{self.holiday_store.status_text()}"
        )

    def update_holidays_now(self) -> None:
        """托盘菜单：手动强制更新节假日数据。"""
        self.maybe_refresh_holidays(force=True, notify=True)

    def maybe_refresh_holidays(self, force: bool = False, notify: bool = False) -> None:
        """后台线程获取节假日 JSON；数据过期（或 force）时才发起。"""
        if self._holiday_thread is not None and self._holiday_thread.is_alive():
            return
        if not force and not self.holiday_store.is_stale():
            return
        self._holiday_notify = notify

        def worker() -> None:
            ok, message = self.holiday_store.fetch(force=True)
            self._holiday_bridge.finished.emit(ok, message)

        self._holiday_thread = threading.Thread(target=worker, daemon=True)
        self._holiday_thread.start()

    def on_holiday_fetched(self, ok: bool, message: str) -> None:
        """UI 线程：更新菜单与界面（school_days 计数可能变了）。"""
        self.update_holiday_menu()
        self.refresh_tray_tooltip()
        self.window.refresh_text()
        if self._holiday_notify:
            icon = (
                QSystemTrayIcon.MessageIcon.Information
                if ok
                else QSystemTrayIcon.MessageIcon.Warning
            )
            self.tray.showMessage(APP_TITLE, message, icon, 4000)
            self._holiday_notify = False

    # ---------------- 版本检查 ----------------
    def check_updates(self, manual: bool = False) -> None:
        """后台读取节假日仓库根目录的 version.json；只提示新版本，不自动下载。"""
        if self._update_thread is not None and self._update_thread.is_alive():
            return
        self._update_manual = manual

        def worker() -> None:
            try:
                latest = fetch_latest_version(self.cfg)
            except Exception as exc:
                self._update_bridge.finished.emit(False, "", str(exc))
            else:
                self._update_bridge.finished.emit(True, latest, "")

        self._update_thread = threading.Thread(target=worker, daemon=True)
        self._update_thread.start()

    def on_update_checked(self, ok: bool, latest: str, error: str) -> None:
        manual = self._update_manual
        self._update_manual = False
        if not ok:
            if manual:
                self.tray.showMessage(
                    APP_TITLE,
                    f"检查新版本失败：{error}",
                    QSystemTrayIcon.MessageIcon.Warning,
                    4000,
                )
            return
        if is_newer(latest):
            if manual or latest != self._notified_version:
                self._notified_version = latest
                self.tray.showMessage(
                    APP_TITLE,
                    f"发现新版本 {latest}（当前 {APP_VERSION}），"
                    "请前往 GitHub 仓库下载新版安装包。",
                    QSystemTrayIcon.MessageIcon.Information,
                    6000,
                )
        elif manual:
            self.tray.showMessage(
                APP_TITLE,
                f"当前已是最新版本（{APP_VERSION}）。",
                QSystemTrayIcon.MessageIcon.Information,
                3000,
            )

    def toggle_autostart(self) -> None:
        if not require_settings_password(self.cfg):
            self.act_autostart.setChecked(bool(self.cfg.get("autostart")))
            return
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
        if not require_settings_password(self.cfg):
            return
        dialog = SettingsDialog(self.cfg)
        if dialog.exec() == QDialog.DialogCode.Accepted:
            self.apply_config(dialog.collect())

    def open_config_editor(self) -> None:
        if not require_settings_password(self.cfg):
            return
        dialog = ConfigEditorDialog(self.cfg)
        if dialog.exec() == QDialog.DialogCode.Accepted and dialog.result_cfg:
            self.apply_config(dialog.result_cfg)
            self.tray.showMessage(
                APP_TITLE,
                "设置已保存到 config.json 并立即生效。",
                QSystemTrayIcon.MessageIcon.Information,
                3000,
            )

    # ---------------- 导入 / 导出 ----------------
    def export_config(self) -> None:
        path, _ = QFileDialog.getSaveFileName(
            None, "导出配置", str(CONFIG_FILE), "JSON 文件 (*.json)"
        )
        if not path:
            return
        try:
            export_cfg = dict(self.cfg)
            export_cfg["password_hash"] = ""
            with open(path, "w", encoding="utf-8") as fp:
                fp.write(config_to_json(export_cfg))
        except Exception:
            log_error("导出配置失败:\n" + traceback.format_exc())
            QMessageBox.warning(None, APP_TITLE, "导出配置失败，详情见 error.log。")
            return
        self.tray.showMessage(
            APP_TITLE, f"配置已导出到：\n{path}",
            QSystemTrayIcon.MessageIcon.Information, 4000,
        )

    def import_config(self) -> None:
        if not require_settings_password(self.cfg):
            return
        path, _ = QFileDialog.getOpenFileName(
            None, "导入配置", "", "JSON 文件 (*.json)"
        )
        if not path:
            return
        try:
            with open(path, "r", encoding="utf-8") as fp:
                raw = json.load(fp)
        except Exception:
            log_error("导入配置失败:\n" + traceback.format_exc())
            QMessageBox.warning(None, APP_TITLE, "导入失败：不是合法的 JSON 文件。")
            return
        imported = sanitize_config(raw)
        # 导入文件不带密码哈希时保留本机密码，避免别人分享的配置把本机锁死
        if not imported.get("password_hash"):
            imported["password_hash"] = self.cfg.get("password_hash", "")
        self.apply_config(imported)
        self.tray.showMessage(
            APP_TITLE, "配置已导入并立即生效。",
            QSystemTrayIcon.MessageIcon.Information, 3000,
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
        source_changed = any(
            cfg.get(k) != self.cfg.get(k)
            for k in ("holiday_repo", "holiday_branch", "holiday_file")
        )
        if cfg.get("autostart") != self.cfg.get("autostart"):
            set_autostart(bool(cfg.get("autostart")))
        self.cfg = cfg
        save_config(cfg)
        self.window.apply_config(cfg)
        self.window.set_user_hidden(False)
        self.act_autostart.setChecked(bool(cfg.get("autostart")))
        # 数据源变了就用新地址重新获取一次
        self.holiday_store.cfg = cfg
        self.update_holiday_menu()
        if source_changed and cfg.get("holiday_auto"):
            self.maybe_refresh_holidays(force=True, notify=True)
        self.refresh_tray_tooltip()

    # ---------------- 退出 ----------------
    def quit(self) -> None:
        save_config(self.cfg)
        self.tray.hide()
        self.window.hide()
        self.app.quit()


def run() -> int:
    install_excepthook()

    app = QApplication(sys.argv)
    app.setApplicationName(APP_TITLE)
    app.setWindowIcon(app_icon())
    app.setQuitOnLastWindowClosed(False)

    if single_instance_guard():
        return 0  # 已有实例在运行，静默退出

    if not QSystemTrayIcon.isSystemTrayAvailable():
        QMessageBox.warning(None, APP_TITLE, "当前系统不支持托盘图标。")
        return 1

    controller = CountdownApp(app)
    controller.refresh_tray_tooltip()
    return app.exec()
