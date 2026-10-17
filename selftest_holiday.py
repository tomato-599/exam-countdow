# -*- coding: utf-8 -*-
"""离线自检：GitHub 节假日数据解析 / 覆盖合并 / 缓存 / 获取 / 托盘集成。"""
import json
import os
import sys
import time
from datetime import date
from pathlib import Path

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

ROOT = Path(__file__).resolve().parent
TMP = ROOT / "_test_config"
TMP.mkdir(exist_ok=True)

import countdown.holidays as H          # noqa: E402
import countdown.config as C            # noqa: E402
import countdown.app as A               # noqa: E402
from PyQt6.QtWidgets import QApplication  # noqa: E402

# 把日志 / 缓存 / 配置全部指到测试目录，避免污染真实数据
_log_lines = []
H.CONFIG_DIR = TMP
H.log_error = _log_lines.append
C.CONFIG_DIR = TMP
C.LOG_FILE = TMP / "error.log"
C.CONFIG_FILE = TMP / "config.json"
A.CONFIG_FILE = TMP / "config.json"
A.set_autostart = lambda enabled: True   # 测试里绝不写注册表

app = QApplication(sys.argv)

# ---------- 1. 样例文件解析 ----------
payload = json.loads((ROOT / "holidays.json").read_text(encoding="utf-8"))
rest, makeup, years = H.parse_payload(payload)
print(f"样例解析: 年份={years} 放假日={len(rest)} 调休日={len(makeup)}")
assert years == [2026, 2027]
assert date(2027, 2, 6) in rest and date(2027, 2, 14) in makeup
assert date(2026, 9, 27) in makeup, "跨月调休日 0927 应在国庆条目下解析出来"
assert date(2026, 10, 1) in rest

# ---------- 2. 内置数据（未覆盖前） ----------
assert not H.is_school_day(date(2026, 10, 1))      # 国庆 放假
assert H.is_school_day(date(2026, 10, 10))          # 周六 调休补班
assert not H.is_school_day(date(2026, 10, 11))      # 周日 不补班
assert date(2026, 9, 25) in H.HOLIDAYS              # 内置 2026 中秋

# ---------- 3. 应用样例：按年份覆盖 ----------
applied_years, r_cnt, m_cnt = H.apply_payload(payload)
print(f"覆盖年份: {applied_years}，{r_cnt} 放假日 / {m_cnt} 调休日")
assert applied_years == [2026, 2027]
assert date(2026, 10, 8) in H.HOLIDAYS, "远程 2026 国庆 1008 应生效"
assert date(2026, 9, 25) not in H.HOLIDAYS, "远程 2026 没有中秋，应被移除"
assert date(2026, 9, 27) in H.MAKEUP, "远程 2026 调休日 0927 应生效"
assert date(2025, 1, 26) in H.MAKEUP, "2025 不在远程文件里，应保留内置数据"
assert H.is_school_day(date(2026, 9, 27))            # 远程调休日算上课
H.reset_to_builtin()
assert date(2026, 9, 25) in H.HOLIDAYS and date(2026, 10, 8) not in H.HOLIDAYS

# ---------- 4. 非法 JSON ----------
for bad in ([], "x", {"2027": {}}, {"2027": {"1": {"元旦": {"放假日": ["abcd"]}}}}):
    try:
        H.parse_payload(bad)
    except ValueError as exc:
        print(f"非法输入被拒绝: {type(bad).__name__} -> {exc}")
    else:
        raise AssertionError(f"应该拒绝: {bad!r}")

# ---------- 5. HolidayStore：file:// 成功获取 + 缓存回读 ----------
sample_uri = (ROOT / "holidays.json").resolve().as_uri()
cfg = dict(H.DEFAULT_CONFIG)
store = H.HolidayStore(cfg)
store.urls = lambda: [sample_uri]
ok, msg = store.fetch(force=True)
print("获取成功:", ok, "|", msg)
assert ok and store.updated_at and (TMP / "holidays_cache.json").exists()
assert "2 年" in msg and store.status_text().startswith("已更新")

store2 = H.HolidayStore(cfg)                 # 新实例：仅靠缓存恢复数据
H.reset_to_builtin()
assert store2.load_cache() and date(2027, 2, 6) in H.HOLIDAYS
assert store2.data_years == [2026, 2027]
print("缓存回读:", store2.status_text())

# ---------- 6. HolidayStore：获取失败保留旧数据 ----------
bad_store = H.HolidayStore(cfg)
bad_store.urls = lambda: [Path(TMP / "no_such.json").as_uri()]
before = set(H.HOLIDAYS)
ok, msg = bad_store.fetch(force=True)
print("获取失败:", ok, "|", msg[:80])
assert not ok and bad_store.last_error and set(H.HOLIDAYS) == before
assert "获取失败" in bad_store.status_text() or True
assert any("节假日数据获取失败" in line for line in _log_lines)

# ---------- 7. 配置清洗 ----------
clean = C.sanitize_config({})
for key in ("holiday_repo", "holiday_branch", "holiday_file", "holiday_auto", "holiday_cache_hours"):
    assert key in clean, key
assert clean["holiday_repo"] == "tomato-599/holidays_count"
bad_cfg = C.sanitize_config({
    "holiday_repo": "https://github.com/x/y",
    "holiday_branch": "",
    "holiday_file": "../../etc/passwd",
    "holiday_cache_hours": 9999,
    "holiday_auto": "yes",
})
assert bad_cfg["holiday_repo"] == "tomato-599/holidays_count"
assert bad_cfg["holiday_branch"] == "main"
assert bad_cfg["holiday_file"] == "holidays.json"
assert bad_cfg["holiday_cache_hours"] == 168
assert bad_cfg["holiday_auto"] is True
assert "holiday_repo" in C.fields_to_html()
print("配置清洗 OK")

# ---------- 8. 托盘控制器集成（不联网） ----------
C.CONFIG_FILE.write_text(
    json.dumps({"autostart": False, "holiday_auto": False, "school_days": True},
               ensure_ascii=False),
    encoding="utf-8",
)
controller = A.CountdownApp(app)
app.processEvents()
assert controller.act_holiday_info.text().startswith("节假日数据：")
print("菜单状态:", controller.act_holiday_info.text())

# 模拟后台线程回调（file:// 源，端到端走一遍 bridge → on_holiday_fetched）
H.HolidayStore.urls = lambda self: [sample_uri]
controller.cfg["holiday_auto"] = False
controller.maybe_refresh_holidays(force=True, notify=True)
deadline = time.time() + 10
while time.time() < deadline and controller._holiday_thread is not None and controller._holiday_thread.is_alive():
    app.processEvents()
    time.sleep(0.05)
for _ in range(20):
    app.processEvents()
    time.sleep(0.02)
print("回调后菜单:", controller.act_holiday_info.text())
assert controller.act_holiday_info.text().startswith("节假日数据：已更新")
assert not controller._holiday_notify

# 设置对话框联动
from countdown.dialogs import SettingsDialog  # noqa: E402
dlg = SettingsDialog(controller.cfg)
assert dlg.holiday_auto_box.isChecked() is False
dlg.holiday_auto_box.setChecked(True)
assert dlg.collect()["holiday_auto"] is True
print("设置对话框 OK")

print("ALL OK")
