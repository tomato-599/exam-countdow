# -*- coding: utf-8 -*-
"""高考倒计时悬浮窗 (PyQt6)。

实现按职责拆分在 countdown/ 包内：
  constants  常量与默认配置
  config     配置读写、数据清洗、错误日志
  system     Windows 系统接口（自启动 / 单实例 / 深色模式 / 前台检测 / 图标）
  render     倒计时计算与文字 / 样式渲染
  window     置顶悬浮窗
  dialogs    设置 / JSON 编辑 / 关于窗口
  app        托盘控制器与主循环入口
"""

import sys

from countdown.app import run

if __name__ == "__main__":
    sys.exit(run())
