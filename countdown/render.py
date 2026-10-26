# -*- coding: utf-8 -*-
"""渲染：剩余天数计算、文字模板展开、HTML / 纯文本生成、卡片样式。"""

from __future__ import annotations

import html
from datetime import date, datetime

from PyQt6.QtGui import QColor
from PyQt6.QtWidgets import QFrame

from .constants import DEFAULT_CONFIG
from .holidays import school_days_between
from .system import is_system_dark


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


def count_days(cfg: dict, raw) -> int:
    """按配置计算剩余天数；开启 school_days 时只计上学日（扣除周末和法定节假日）。"""
    dt = parse_deadline(raw)
    if dt is None:
        return 0
    today = date.today()
    if cfg.get("school_days"):
        if dt.date() < today:
            return (dt.date() - today).days
        return school_days_between(today, dt.date())
    return (dt.date() - today).days


def days_left(cfg: dict) -> int:
    """主倒计时的剩余天数。"""
    return count_days(cfg, cfg.get("deadline"))


def duration_days(cfg: dict, item: dict | None = None) -> int:
    """考试持续天数：条目优先，留空回退到全局设置，最少 1 天。"""
    raw = (item or {}).get("duration_days")
    if raw is None or str(raw).strip() == "":
        raw = cfg.get("duration_days", DEFAULT_CONFIG["duration_days"])
    try:
        return max(1, min(365, int(raw)))
    except Exception:
        return 1


def exam_progress(cfg: dict, item: dict | None = None) -> tuple[int, int] | None:
    """多天考试进行中时返回 (第几天, 共几天)，否则返回 None。"""
    raw = (item or {}).get("deadline") or cfg.get("deadline")
    dt = parse_deadline(raw)
    if dt is None:
        return None
    total = duration_days(cfg, item)
    if total <= 1:
        return None
    offset = (date.today() - dt.date()).days
    if 0 < offset < total:
        return offset + 1, total
    return None


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


def format_ongoing(
    cfg: dict, item: dict | None, day: int, total: int
) -> str:
    """展开多天考试进行中的文字模板。"""
    item = item or {}
    name = str(item.get("name") or "")
    date_text = str(item.get("deadline") or cfg.get("deadline") or "")[:10]
    template = str(cfg.get("ongoing_text") or "").strip()
    if not template:
        template = DEFAULT_CONFIG["ongoing_text"]
    return (
        template.replace("{name}", name)
        .replace("{date}", date_text)
        .replace("{day}", str(day))
        .replace("{total}", str(total))
    )


def build_html(cfg: dict, days: int, item: dict | None = None) -> str:
    text_color, _bg = resolve_colors(cfg)
    urgent_color = cfg.get("urgent_color", "#E53935")
    base_size = int(cfg.get("base_font_size", 20))
    urgent_size = int(cfg.get("urgent_font_size", 34))
    zero_size = int(cfg.get("zero_font_size", 30))

    template, zero_text, past_text, name = resolve_texts(cfg, item)

    # 考试第一天 / 多天考试进行中 / 已结束
    if days <= 0:
        progress = exam_progress(cfg, item)
        if progress:
            text = format_ongoing(cfg, item, *progress)
        else:
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
    if days <= 0:
        progress = exam_progress(cfg, item)
        if progress:
            return format_ongoing(cfg, item, *progress)
        return zero_text if days == 0 else past_text
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
