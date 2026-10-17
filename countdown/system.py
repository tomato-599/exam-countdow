# -*- coding: utf-8 -*-
"""Windows 系统接口：自启动注册表、单实例互斥体、深色模式、前台窗口检测、托盘图标。"""

from __future__ import annotations

import ctypes
import os
import sys
import traceback
from ctypes import wintypes

from PyQt6.QtCore import Qt
from PyQt6.QtGui import QColor, QFont, QIcon, QPainter, QPixmap

from .config import log_error
from .constants import APP_ID, IS_WINDOWS, PERSONALIZE_KEY, RUN_KEY, SCRIPT_PATH

if IS_WINDOWS:
    _user32 = ctypes.windll.user32
    _kernel32 = ctypes.windll.kernel32
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000


# --------------------------------------------------------------------------- #
# 系统深色模式
# --------------------------------------------------------------------------- #
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


# --------------------------------------------------------------------------- #
# 开机自启动
# --------------------------------------------------------------------------- #
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


# --------------------------------------------------------------------------- #
# 单实例
# --------------------------------------------------------------------------- #
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


# --------------------------------------------------------------------------- #
# 前台窗口检测
# --------------------------------------------------------------------------- #
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
# 图标
# --------------------------------------------------------------------------- #
def make_icon() -> QIcon:
    """用代码画兜底图标（icon.ico 缺失时使用）。"""
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


def _resource_base() -> str:
    """打包后取 PyInstaller 解包目录，源码运行时取项目根目录。"""
    base = getattr(sys, "_MEIPASS", "")
    if base:
        return base
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def app_icon() -> QIcon:
    """加载根目录 icon.ico 作为应用图标；文件缺失时退回代码绘制的备用图标。"""
    path = os.path.join(_resource_base(), "icon.ico")
    if os.path.isfile(path):
        icon = QIcon(path)
        if not icon.isNull():
            return icon
    return make_icon()
