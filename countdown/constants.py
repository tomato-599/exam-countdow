# -*- coding: utf-8 -*-
"""应用级常量：名称、版本、注册表路径、默认配置与字段说明。"""

from __future__ import annotations

import os
import sys
from datetime import date

APP_ID = "GaokaoCountdown"          # 注册表 / 互斥体使用的名字
APP_TITLE = "高考倒计时"
APP_VERSION = "0.5.1"
APP_AUTHOR = "L"
APP_DESC = "置顶悬浮的高考倒计时 · 希沃前台自动隐藏 · 托盘常驻"
RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"
PERSONALIZE_KEY = (
    r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
)
IS_WINDOWS = os.name == "nt"
# 入口固定为仓库根目录的 main.py；不能用 __file__，拆分后本文件不再是入口
SCRIPT_PATH = os.path.abspath(sys.argv[0])


def default_deadline() -> str:
    """默认截止日期：下一个 6 月 7 日 09:00。"""
    today = date.today()
    year = today.year
    if date(year, 6, 7) < today:
        year += 1
    return f"{year}-06-07 09:00:00"


# 前台为这些程序（进程名或窗口标题包含关键词，忽略大小写）时自动隐藏。
# 只列主程序本身：希沃系子进程 / 附属程序（升级器、云同步等）不触发隐藏。
DEFAULT_HIDE_KEYWORDS = ["easinote", "easicamera", "希沃白板", "希沃视频展台"]

DEFAULT_CONFIG = {
    "deadline": default_deadline(),          # 截止日期时间
    "template": "距离{name}还有 {time} 天",     # {time}/{days} = 天数
    "zero_text": "旗开得胜",       # 天数 == 0
    "past_text": "考试已结束",  # 天数 < 0
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
    "school_days": False,                    # 只计上学日（扣除周末和法定节假日）
    # 节假日数据源（GitHub 仓库 tomato-599/holidays_count 的 JSON 文件）
    "holiday_repo": "tomato-599/holidays_count",  # GitHub 仓库（owner/repo）
    "holiday_branch": "main",                # 仓库分支
    "holiday_file": "holidays.json",         # 仓库里的 JSON 文件路径
    "holiday_auto": True,                    # 自动定时从 GitHub 获取更新
    "holiday_cache_hours": 6,                # 自动更新检查间隔（小时）
    # 多倒计时轮播：[{"name": "一模", "deadline": "2027-03-10 09:00:00", "template": ""}]
    "events": [],
    "rotate_seconds": 5,                     # 轮播间隔（秒）
    "event_template": "距离{name}还有 {time} 天",  # 事件没写模板时用它
}

# 每个配置项的中文说明（“关于 / 设置(JSON)”里会一并列出）
CONFIG_FIELDS: dict[str, str] = {
    "deadline": "截止日期时间，ISO 格式 YYYY-MM-DD HH:MM:SS",
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
    "school_days": "为 true 时只计上学日：扣除周末和法定节假日放假天数，调休补班的周末计入（内置数据 + GitHub 节假日文件）",
    "holiday_repo": "节假日数据仓库（GitHub owner/repo），默认 tomato-599/holidays_count",
    "holiday_branch": "节假日仓库分支，默认 main",
    "holiday_file": "仓库里的 JSON 文件路径，默认 holidays.json",
    "holiday_auto": "是否自动定时从 GitHub 获取节假日数据（获取失败不影响使用）",
    "holiday_cache_hours": "自动更新检查间隔（小时），1 ~ 168",
    "events": "多倒计时列表，每项 {\"name\": 名称, \"deadline\": 截止时间, \"template\": 可选单独模板}",
    "rotate_seconds": "多倒计时轮播间隔（秒），2 ~ 600",
    "event_template": "多倒计时条目没写自己模板时使用的文字模板（支持 {name}）",
}
