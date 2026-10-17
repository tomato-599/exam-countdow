# -*- coding: utf-8 -*-
"""法定节假日 / 调休数据与上学日天数统计。

数据有两个来源：

1. 内置表（本文件）：2025~2027 年安排，离线兜底，随程序版本更新；
2. GitHub 仓库 ``tomato-599/holidays_count`` 的 ``holidays.json``：
   启动时 / 每隔数小时自动获取，成功后写入本地缓存，断网时读缓存。

远程 JSON 格式（详见仓库里的样例 holidays.json）::

    {
      "2027": {                       // 顶层：年份
        "2": {                        // 第二层：月份（仅便于写时分类）
          "春节": {                   // 第三层：节假日名（只便于阅读，不显示）
            "调休": true,             // 是否涉及调休
            "放假日": ["0206", ...],  // 放假日，MMDD
            "调休日": ["0214", ...]   // 调休补班日，MMDD
          }
        }
      }
    }

合并规则：远程文件里出现的年份以远程数据为准（覆盖内置表同年的数据），
其余年份继续用内置表。获取失败时保留缓存 / 内置数据，不影响使用。

上学日 = 周一~周五且不在放假区间内，或调休补班的周末。
"""

from __future__ import annotations

import json
import traceback
import urllib.request
from datetime import date, datetime, timedelta
from pathlib import Path

from .config import CONFIG_DIR, log_error
from .constants import APP_VERSION, DEFAULT_CONFIG

# --------------------------------------------------------------------------- #
# 内置数据（离线兜底）
# --------------------------------------------------------------------------- #
# 每年的放假区间（含首尾两天）：[(开始日期, 结束日期), ...]
HOLIDAY_RANGES: dict[int, list[tuple[str, str]]] = {
    # 国务院办公厅关于2025年部分节假日安排的通知
    2025: [
        ("2025-01-01", "2025-01-01"),  # 元旦
        ("2025-01-28", "2025-02-04"),  # 春节（含除夕）
        ("2025-04-04", "2025-04-06"),  # 清明节
        ("2025-05-01", "2025-05-05"),  # 劳动节
        ("2025-05-31", "2025-06-02"),  # 端午节
        ("2025-10-01", "2025-10-08"),  # 国庆节（含中秋节）
    ],
    # 国务院办公厅关于2026年部分节假日安排的通知
    2026: [
        ("2026-01-01", "2026-01-03"),  # 元旦
        ("2026-02-15", "2026-02-23"),  # 春节
        ("2026-04-04", "2026-04-06"),  # 清明节
        ("2026-05-01", "2026-05-05"),  # 劳动节
        ("2026-06-19", "2026-06-21"),  # 端午节
        ("2026-09-25", "2026-09-27"),  # 中秋节
        ("2026-10-01", "2026-10-07"),  # 国庆节
    ],
    # 2027 年（官方通知发布前为预估，发布后立即更新）
    2027: [
        ("2027-01-01", "2027-01-03"),  # 元旦（预估）
        ("2027-02-05", "2027-02-11"),  # 春节（预估，除夕至初六）
        ("2027-04-03", "2027-04-05"),  # 清明节（预估）
        ("2027-05-01", "2027-05-05"),  # 劳动节（预估）
        ("2027-06-08", "2027-06-10"),  # 端午节（预估）
    ],
}

# 每年的调休补班日（落在周末，但按国务院通知上班 / 上课）
MAKEUP_WORKDAYS: dict[int, list[str]] = {
    2025: ["2025-01-26", "2025-02-08", "2025-04-27", "2025-09-28", "2025-10-11"],
    2026: ["2026-01-04", "2026-02-14", "2026-02-28", "2026-05-09", "2026-09-20", "2026-10-10"],
    # 2027 年（官方通知发布前为预估，发布后立即更新）
    2027: ["2027-02-13", "2027-02-20", "2027-04-25", "2027-05-08"],
}


def _build_holidays() -> set[date]:
    days: set[date] = set()
    for ranges in HOLIDAY_RANGES.values():
        for start_text, end_text in ranges:
            current = date.fromisoformat(start_text)
            end = date.fromisoformat(end_text)
            while current <= end:
                days.add(current)
                current += timedelta(days=1)
    return days


def _build_days(table: dict[int, list[str]]) -> set[date]:
    return {date.fromisoformat(text) for values in table.values() for text in values}


BUILTIN_HOLIDAYS: set[date] = _build_holidays()
BUILTIN_MAKEUP: set[date] = _build_days(MAKEUP_WORKDAYS)

# 活动数据集：启动时读缓存、拉取成功后由 apply_payload 按年份覆盖
HOLIDAYS: set[date] = set(BUILTIN_HOLIDAYS)
MAKEUP: set[date] = set(BUILTIN_MAKEUP)


# --------------------------------------------------------------------------- #
# 远程 JSON 解析
# --------------------------------------------------------------------------- #
# 放假日 / 调休日的可接受字段名（主用中文，英文别名方便手写）
REST_KEYS = ("放假日", "放假", "rest")
MAKEUP_KEYS = ("调休日", "补班日", "调休上班", "makeup")


def _iter_dates(info: dict, keys: tuple[str, ...], year: int):
    """从节假日条目里取出 MMDD 列表，解析成 date（非法项跳过）。"""
    for key in keys:
        raw = info.get(key)
        if isinstance(raw, str):
            raw = [raw]
        if not isinstance(raw, list):
            continue
        for item in raw:
            text = str(item).strip()
            if len(text) != 4 or not text.isdigit():
                continue
            try:
                yield date(year, int(text[:2]), int(text[2:]))
            except ValueError:
                continue


def parse_payload(payload) -> tuple[set[date], set[date], list[int]]:
    """远程 JSON -> (放假日集合, 调休日集合, 年份列表)；格式非法时抛 ValueError。"""
    if not isinstance(payload, dict):
        raise ValueError("顶层必须是 {年份: {月份: {...}}} 形式的对象")
    rest: set[date] = set()
    makeup: set[date] = set()
    years: list[int] = []
    for year_key, months in payload.items():
        text = str(year_key).strip()
        if not (text.isdigit() and len(text) == 4):
            continue  # _说明 之类的辅助键直接忽略
        if not isinstance(months, dict):
            continue
        years.append(int(text))
        for month_key, holidays in months.items():  # noqa: B007 - 月份仅作书写分类
            if not isinstance(holidays, dict):
                continue
            for name, info in holidays.items():  # noqa: B007 - 名称只便于阅读
                if not isinstance(info, dict):
                    continue
                rest.update(_iter_dates(info, REST_KEYS, int(text)))
                makeup.update(_iter_dates(info, MAKEUP_KEYS, int(text)))
    if not rest and not makeup:
        raise ValueError("没有解析到任何有效日期（检查年份键与 MMDD 格式）")
    return rest, makeup, sorted(set(years))


def apply_payload(payload) -> tuple[list[int], int, int]:
    """用远程 JSON 覆盖对应年份的活动数据；返回 (年份, 放假日数, 调休日数)。"""
    global HOLIDAYS, MAKEUP
    rest, makeup, years = parse_payload(payload)
    active_rest = {d for d in HOLIDAYS if d.year not in years} | rest
    active_makeup = {d for d in MAKEUP if d.year not in years} | makeup
    HOLIDAYS = active_rest
    MAKEUP = active_makeup
    return years, len(rest), len(makeup)


def reset_to_builtin() -> None:
    """回退到内置数据（缓存损坏 / 测试用）。"""
    global HOLIDAYS, MAKEUP
    HOLIDAYS = set(BUILTIN_HOLIDAYS)
    MAKEUP = set(BUILTIN_MAKEUP)


# --------------------------------------------------------------------------- #
# 上学日统计
# --------------------------------------------------------------------------- #
def is_school_day(day: date) -> bool:
    """上学日：工作日不在放假区间内，或调休补班的周末。"""
    if day in MAKEUP:
        return True
    return day.weekday() < 5 and day not in HOLIDAYS


def school_days_between(start: date, end: date) -> int:
    """统计 (start, end] 区间内的上学日天数（含 end 当天）。"""
    count = 0
    current = start + timedelta(days=1)
    while current <= end:
        if is_school_day(current):
            count += 1
        current += timedelta(days=1)
    return count


# --------------------------------------------------------------------------- #
# GitHub 数据源
# --------------------------------------------------------------------------- #
USER_AGENT = f"GaokaoCountdown/{APP_VERSION}"


class HolidayStore:
    """GitHub 节假日数据：本地缓存 + 同步获取（在后台线程里调用 fetch）。"""

    def __init__(self, cfg: dict):
        self.cfg = dict(cfg)
        self.updated_at = ""       # 上次成功更新时间（ISO）；空 = 只有内置数据
        self.source = ""           # repo@branch/file
        self.last_error = ""
        self.data_years: list[int] = []

    # ---------------- 地址与缓存 ----------------
    @property
    def cache_file(self) -> Path:
        return CONFIG_DIR / "holidays_cache.json"

    def _source_str(self) -> str:
        repo = str(self.cfg.get("holiday_repo") or DEFAULT_CONFIG["holiday_repo"])
        branch = str(self.cfg.get("holiday_branch") or DEFAULT_CONFIG["holiday_branch"])
        file = str(self.cfg.get("holiday_file") or DEFAULT_CONFIG["holiday_file"])
        return f"{repo}@{branch}/{file}"

    def urls(self) -> list[str]:
        """原始地址 + jsDelivr 各 CDN 端点（raw.githubusercontent / cdn.jsdelivr 在部分地区被重置）。"""
        repo = str(self.cfg.get("holiday_repo") or DEFAULT_CONFIG["holiday_repo"])
        branch = str(self.cfg.get("holiday_branch") or DEFAULT_CONFIG["holiday_branch"])
        file = str(self.cfg.get("holiday_file") or DEFAULT_CONFIG["holiday_file"])
        return [
            f"https://raw.githubusercontent.com/{repo}/{branch}/{file}",
            f"https://fastly.jsdelivr.net/gh/{repo}@{branch}/{file}",
            f"https://cdn.jsdelivr.net/gh/{repo}@{branch}/{file}",
            f"https://testingcf.jsdelivr.net/gh/{repo}@{branch}/{file}",
            f"https://gcore.jsdelivr.net/gh/{repo}@{branch}/{file}",
        ]

    def load_cache(self) -> bool:
        """启动时读本地缓存并应用；没有或损坏则保留内置数据。"""
        try:
            if not self.cache_file.exists():
                return False
            blob = json.loads(self.cache_file.read_text(encoding="utf-8"))
            payload = blob.get("data")
            self.data_years, _, _ = apply_payload(payload)
            self.updated_at = str(blob.get("fetched_at") or "")
            self.source = str(blob.get("source") or "")
            return True
        except Exception:
            log_error("读取节假日缓存失败:\n" + traceback.format_exc())
            return False

    def _save_cache(self, payload) -> None:
        try:
            CONFIG_DIR.mkdir(parents=True, exist_ok=True)
            blob = {
                "fetched_at": self.updated_at,
                "source": self.source,
                "data": payload,
            }
            self.cache_file.write_text(
                json.dumps(blob, ensure_ascii=False, indent=1), encoding="utf-8"
            )
        except Exception:
            log_error("写入节假日缓存失败:\n" + traceback.format_exc())

    # ---------------- 获取 ----------------
    def is_stale(self) -> bool:
        if not self.updated_at:
            return True
        try:
            stamp = datetime.fromisoformat(self.updated_at)
        except Exception:
            return True
        hours = int(self.cfg.get("holiday_cache_hours", 6) or 6)
        return (datetime.now() - stamp).total_seconds() > hours * 3600

    def fetch(self, force: bool = False) -> tuple[bool, str]:
        """从 GitHub 获取节假日 JSON；返回 (是否成功, 提示语)。"""
        if not force and not self.is_stale() and self.updated_at:
            return True, "节假日数据已是最新"
        last_error = ""
        for url in self.urls():
            try:
                request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
                with urllib.request.urlopen(request, timeout=10) as response:
                    raw = response.read()
                payload = json.loads(raw.decode("utf-8-sig"))
                years, rest_count, makeup_count = apply_payload(payload)
            except Exception as exc:  # 网络失败 / 404 / JSON 非法，换镜像地址再试
                last_error = f"{type(exc).__name__}: {exc}"
                continue
            self.updated_at = datetime.now().isoformat(timespec="seconds")
            self.source = self._source_str()
            self.last_error = ""
            self.data_years = years
            self._save_cache(payload)
            return True, (
                f"节假日数据已更新：{len(years)} 年，"
                f"{rest_count} 个放假日、{makeup_count} 个补班日"
            )
        self.last_error = last_error
        log_error(f"节假日数据获取失败（{self._source_str()}）: {last_error}")
        return False, f"节假日数据获取失败：{last_error}"

    # ---------------- 展示（不显示节假日名） ----------------
    def status_text(self) -> str:
        if self.updated_at:
            stamp = self.updated_at.replace("T", " ")[:16]
            return f"已更新 {stamp}（{len(self.data_years)} 年）"
        if self.last_error:
            return "更新失败，使用内置数据"
        return "内置数据"
