# -*- coding: utf-8 -*-
"""远端版本检查：读取节假日仓库根目录的 version.json。

只提示有新版本，不下载、不安装。文件内容支持两种写法：

1. 单行纯文本：``0.7.1``
2. JSON：``{"version": "0.7.1"}``

版本号采用 ``主版本号.次版本号.修订版本号``。
"""

from __future__ import annotations

import json
import urllib.request

from .constants import APP_VERSION, DEFAULT_CONFIG

VERSION_FILE = "version.json"


def parse_version(text: str) -> tuple[int, int, int]:
    """把 'v0.7.1' / '0.7.1' 解析成 (0, 7, 1)；缺位补 0，非数字段按 0 处理。"""
    parts: list[int] = []
    for token in str(text).strip().lstrip("vV").split("."):
        digits = "".join(ch for ch in token if ch.isdigit())
        parts.append(int(digits) if digits else 0)
        if len(parts) == 3:
            break
    while len(parts) < 3:
        parts.append(0)
    return parts[0], parts[1], parts[2]


def is_newer(latest: str, current: str = APP_VERSION) -> bool:
    """远端版本是否比当前版本新（按主.次.修订逐段比较）。"""
    return parse_version(latest) > parse_version(current)


def _extract_version(text: str) -> str:
    """从 version.json 原文里取出版本号；格式非法时抛 ValueError。"""
    raw = text.strip().lstrip("\ufeff")
    if not raw:
        raise ValueError("version.json 为空")
    try:
        payload = json.loads(raw)
    except json.JSONDecodeError:
        payload = None
    if isinstance(payload, dict):
        for key in ("version", "latest", "tag_name", "tag"):
            value = payload.get(key)
            if value:
                return str(value).strip().lstrip("vV")
        raise ValueError("version.json 缺少 version 字段")
    if isinstance(payload, str):
        return payload.strip().lstrip("vV")
    return raw.splitlines()[0].strip().strip(",，\"'").lstrip("vV")


def version_urls(repo: str, branch: str) -> list[str]:
    """与节假日数据相同的镜像顺序（raw 优先，失败换 jsDelivr CDN）。"""
    return [
        f"https://raw.githubusercontent.com/{repo}/{branch}/{VERSION_FILE}",
        f"https://fastly.jsdelivr.net/gh/{repo}@{branch}/{VERSION_FILE}",
        f"https://cdn.jsdelivr.net/gh/{repo}@{branch}/{VERSION_FILE}",
        f"https://testingcf.jsdelivr.net/gh/{repo}@{branch}/{VERSION_FILE}",
        f"https://gcore.jsdelivr.net/gh/{repo}@{branch}/{VERSION_FILE}",
    ]


def fetch_latest_version(cfg: dict) -> str:
    """后台线程调用：返回仓库里的最新版本号；全部镜像失败时抛 RuntimeError。"""
    repo = str(cfg.get("holiday_repo") or DEFAULT_CONFIG["holiday_repo"]).strip().strip("/")
    branch = str(cfg.get("holiday_branch") or DEFAULT_CONFIG["holiday_branch"]).strip()
    last_error = ""
    for url in version_urls(repo, branch):
        try:
            request = urllib.request.Request(
                url, headers={"User-Agent": f"GaokaoCountdown/{APP_VERSION}"}
            )
            with urllib.request.urlopen(request, timeout=10) as response:
                text = response.read().decode("utf-8-sig")
            return _extract_version(text)
        except Exception as exc:
            last_error = f"{type(exc).__name__}: {exc}"
    raise RuntimeError(last_error or "没有可用的下载地址")
