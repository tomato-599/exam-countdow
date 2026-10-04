# -*- coding: utf-8 -*-
"""生成 app.ico：多尺寸 PNG 压缩的 Windows 图标（红色圆角方块 + “考”）。"""
import os
import struct
import sys

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PyQt6.QtCore import QBuffer, QByteArray, QRectF, Qt
from PyQt6.QtGui import QColor, QFont, QGuiApplication, QImage, QPainter

SIZES = [16, 24, 32, 48, 64, 128, 256]


def render(size: int) -> bytes:
    img = QImage(size, size, QImage.Format.Format_ARGB32)
    img.fill(Qt.GlobalColor.transparent)
    p = QPainter(img)
    p.setRenderHint(QPainter.RenderHint.Antialiasing, True)
    p.setRenderHint(QPainter.RenderHint.TextAntialiasing, True)
    pad = size * 0.03
    p.setPen(Qt.PenStyle.NoPen)
    p.setBrush(QColor("#E53935"))
    p.drawRoundedRect(
        QRectF(pad, pad, size - 2 * pad, size - 2 * pad), size * 0.24, size * 0.24
    )
    p.setPen(QColor("#FFFFFF"))
    font = QFont("Microsoft YaHei")
    font.setPixelSize(max(6, int(size * 0.62)))
    font.setBold(True)
    p.setFont(font)
    p.drawText(img.rect(), Qt.AlignmentFlag.AlignCenter, "考")
    p.end()

    buf = QBuffer()                     # 用自带缓冲区，避免临时对象被回收
    buf.open(QBuffer.OpenModeFlag.WriteOnly)
    img.save(buf, "PNG")
    buf.close()
    return bytes(buf.data())


def main() -> int:
    app = QGuiApplication(sys.argv)   # 必须保持引用，否则字体库会失效
    frames = [(s, render(s)) for s in SIZES]

    header = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    entries, blobs = b"", b""
    for size, data in frames:
        dim = 0 if size >= 256 else size
        entries += struct.pack(
            "<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset
        )
        blobs += data
        offset += len(data)

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "app.ico")
    with open(out, "wb") as fp:
        fp.write(header + entries + blobs)
    print(f"icon written: {out} ({os.path.getsize(out)} bytes, {len(frames)} sizes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
