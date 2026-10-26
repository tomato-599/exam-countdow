# -*- coding: utf-8 -*-
"""配置层：配置目录解析、config.json 读写、数据清洗、多倒计时条目、错误日志。"""

from __future__ import annotations

import html
import json
import os
import sys
import traceback
from datetime import datetime
from pathlib import Path

from PyQt6.QtGui import QColor

from .constants import (
    APP_ID,
    CONFIG_FIELDS,
    DEFAULT_CONFIG,
    DEFAULT_HIDE_KEYWORDS,
    SCRIPT_PATH,
)


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


# --------------------------------------------------------------------------- #
# 错误日志
# --------------------------------------------------------------------------- #
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


# --------------------------------------------------------------------------- #
# 多倒计时条目
# --------------------------------------------------------------------------- #
def _duration_days(value) -> int:
    """考试持续天数：留空、非法或小于 1 都按 1 天处理。"""
    try:
        return max(1, min(365, int(value)))
    except Exception:
        return 1


def parse_events(raw) -> list[dict]:
    """规范化多倒计时列表：每项 {name, deadline, template, duration_days}。"""
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
                "duration_days": _duration_days(entry.get("duration_days")),
            }
        )
    return events


def config_items(cfg: dict) -> list[dict]:
    """所有倒计时条目；没有配置多倒计时时就是默认的那一条。"""
    events = parse_events(cfg.get("events"))
    if events:
        return events
    return [
        {
            "name": "",
            "deadline": str(cfg.get("deadline") or ""),
            "template": "",
            "duration_days": _duration_days(cfg.get("duration_days")),
        }
    ]


# --------------------------------------------------------------------------- #
# 数据清洗
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
    data["duration_days"] = _duration_days(data.get("duration_days"))
    data["template"] = str(data.get("template") or "{time}")
    data["zero_text"] = str(data.get("zero_text") or "")
    data["ongoing_text"] = str(data.get("ongoing_text") or "")
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
    data["school_days"] = bool(data.get("school_days"))
    keywords = data.get("hide_keywords")
    if isinstance(keywords, str):
        keywords = [k.strip() for k in keywords.split(",") if k.strip()]
    if not isinstance(keywords, list):
        keywords = []
    keywords = [str(k).strip() for k in keywords if str(k).strip()]
    # 修正旧版遗留：easynote 是主程序名 easinote 的误拼；seewo / 希沃 是宽泛词，会命中希沃系所有子进程
    keywords = list(dict.fromkeys(
        "easinote" if k.lower() == "easynote" else k for k in keywords
    ))
    broad = {"seewo", "希沃"}
    if any(k.lower() in broad for k in keywords):
        keywords = [k for k in keywords if k.lower() not in broad]
        for k in DEFAULT_HIDE_KEYWORDS:
            if k not in keywords:
                keywords.append(k)
    data["hide_keywords"] = keywords or list(DEFAULT_HIDE_KEYWORDS)
    data["events"] = parse_events(data.get("events"))
    data["rotate_seconds"] = _clamp_int(data.get("rotate_seconds"), 2, 600, 5)
    data["event_template"] = str(
        data.get("event_template") or DEFAULT_CONFIG["event_template"]
    )
    # 节假日数据源
    repo = str(data.get("holiday_repo") or "").strip().strip("/")
    if repo.count("/") != 1 or any(c.isspace() for c in repo) or repo.startswith("http"):
        repo = DEFAULT_CONFIG["holiday_repo"]
    data["holiday_repo"] = repo
    branch = str(data.get("holiday_branch") or "").strip()
    if not branch or any(c in branch for c in " \t\r\n~^:?#[\\"):
        branch = DEFAULT_CONFIG["holiday_branch"]
    data["holiday_branch"] = branch
    hfile = str(data.get("holiday_file") or "").strip().strip("/")
    if not hfile or ".." in hfile or any(c in hfile for c in " \t\r\n?#[\\"):
        hfile = DEFAULT_CONFIG["holiday_file"]
    data["holiday_file"] = hfile
    data["holiday_auto"] = bool(data.get("holiday_auto"))
    data["holiday_cache_hours"] = _clamp_int(
        data.get("holiday_cache_hours"), 1, 168, 6
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


# --------------------------------------------------------------------------- #
# 读写
# --------------------------------------------------------------------------- #
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
