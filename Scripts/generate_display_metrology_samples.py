"""Generate deterministic, linear-signal display fixtures with explicit ground truth.

No third-party packages, downloads or private images. PNGs are measurement fixtures,
not photographs of real devices. Existing output files are rejected unless --overwrite.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import zlib


def png(width, height, pixel, color=False):
    def chunk(kind, payload):
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
    rows = bytearray()
    for y in range(height):
        rows.append(0)
        for x in range(width):
            values = pixel(x, y)
            for value in values if color else [values]:
                value = min(1.0, max(0.0, value))
                if color:
                    rows.append(round(value * 255))
                else:
                    rows.extend(struct.pack(">H", round(value * 65535)))
    header = struct.pack(">IIBBBBB", width, height, 8 if color else 16, 2 if color else 0, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"gAMA", struct.pack(">I", 100000)) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")


def dot(x, y, dx=0, dy=0):
    u, v = x % 64 - 31.5 - dx, y % 64 - 31.5 - dy
    return 0.02 + 0.7 * math.exp(-(u*u + v*v) / 32)


def cross(x, y, dx=0, dy=0):
    u, v = x % 64 - 31.5 - dx, y % 64 - 31.5 - dy
    foreground = (abs(u) <= 1.5 and abs(v) <= 18.5) or (abs(v) <= 1.5 and abs(u) <= 18.5)
    return 0.8 if foreground else 0.02


def fixtures():
    files = {}
    files["rgb_registration.png"] = png(192, 192, lambda x, y: [dot(x, y, 2, -1), dot(x, y), dot(x, y, -3, 1)], True)
    files["rgb_cross_9point.png"] = png(192, 192, lambda x, y: [cross(x, y, 2, -1), cross(x, y), cross(x, y, -3, 2)], True)
    files["binocular_left.png"] = png(192, 192, dot)
    files["binocular_right.png"] = png(192, 192, lambda x, y: dot(x, y, 3, -2) * 0.8)
    files["ghost_10_percent.png"] = png(128, 128, lambda x, y: 0.02 + (0.8 if 55 <= x < 65 and 55 <= y < 65 else 0)
        + (0.08 if 95 <= x < 105 and 55 <= y < 65 else 0))

    def defects(x, y):
        value = 0.08 - 0.025 * math.exp(-((x - 175)**2 + (y - 175)**2) / 200)
        if (x, y) == (65, 65): value += 0.2
        if (x, y) == (90, 65): value -= 0.06
        if x == 110 and 80 <= y < 145: value += 0.15
        return value
    files["microdisplay_low_gray.png"] = png(256, 256, defects)
    files["uniform_good.png"] = png(256, 256, lambda x, y: 0.08 + x * 0.0001)

    def edge(x, y):
        sigma = 1 + (x // 96) * 0.5
        distance = (x % 96 - 47.5 - 0.1 * (y % 96 - 47.5)) / math.sqrt(1.01)
        return 0.2 + 0.3 * (1 + math.erf(distance / (math.sqrt(2) * sigma)))
    files["field_sfr.png"] = png(288, 288, edge)
    scan = []
    for i in range(9):
        name = f"eyebox_{i:02d}.png"
        scan.append(name)
        # Reference is center. Four corner positions fail, so only the central cross is accepted.
        gain = 0.25 if i in (0, 2, 6, 8) else 1.0
        files[name] = png(64, 64, lambda x, y, gain=gain: (0.5 + x * 0.001) * gain)
    files["eyebox_scan.json"] = json.dumps({"schemaVersion": 1, "parameters": {"columns": 3, "rows": 3,
        "stepXMillimeters": 1, "stepYMillimeters": 1, "referenceIndex": 4}, "frames": scan}, indent=2).encode()
    truth = {
        "source": "procedural synthetic data; not field images", "signalEncoding": "linear", "decodeExponent": 1,
        "rgb_registration.png": {"grid": [3, 3], "R-G_px": [2, -1], "B-G_px": [-3, 1]},
        "rgb_cross_9point.png": {"grid": [3, 3], "R-G_px": [2, -1], "B-G_px": [-3, 2], "maximumRgbEdgeSeparation_px": 5},
        "binocular_right.png": {"left": "binocular_left.png", "displacement_px": [3, -2], "signalRatio": 0.8},
        "ghost_10_percent.png": {"background": 0.02, "ghostPeakOverPrimary": 0.1, "ghostEnergyOverPrimary": 0.1},
        "microdisplay_low_gray.png": {"brightPoint": [65, 65], "darkPoint": [90, 65], "brightLine": [110, 80, 1, 65], "darkMuraCenter": [175, 175]},
        "field_sfr.png": {"sigmaByColumn": [1, 1.5, 2], "analyticalMtf50_cyclesPerPixel": [math.sqrt(2*math.log(2))/(2*math.pi*s) for s in [1, 1.5, 2]]},
        "eyebox_scan.json": {"acceptedSamples": [1, 3, 4, 5, 7], "fourCornerAcceptedMeshArea_mm2": 0},
        "sha256": {name: hashlib.sha256(data).hexdigest() for name, data in files.items()},
        "references": [
            "https://docs.opencv.org/4.x/d3/dc0/group__imgproc__shape.html",
            "https://www.imatest.com/imaging/validating_slanted_edge/",
            "https://cdrh-rst.fda.gov/head-mounted-display-eye-box-centering-using-transverse-chromatic-aberrations-0",
            "https://gamma-sci.com/products/ned-ar-vr-testing-collections/ned-lmd-e-series/",
        ],
    }
    files["ground_truth.json"] = json.dumps(truth, indent=2).encode()
    files["README.md"] = """# 显示计量合成样本

这些图像由公式生成，包含已知真值，不是现场产品照片。数据为线性信号：输入解码指数用 1。
打开对应 PNG，在图像右键 **算法 → 显示计量** 中运行。参数未列出时使用默认值。

| 图像 | 功能 | 参数或预期 |
| --- | --- | --- |
| rgb_registration.png | RGB 图案套色 | R−G=(2,−1) px，B−G=(−3,1) px |
| rgb_cross_9point.png | 九点十字 RGB 分离 | R−G=(2,−1) px，B−G=(−3,2) px；最大边缘分离 5 px |
| binocular_left.png | 左右眼对准与信号一致性 | 选择 binocular_right.png；位移 (3,−2) px，信号比 0.8 |
| ghost_10_percent.png | 鬼影与杂散光评价 | 背景设为 0.02；主像与鬼影强度比 0.1 |
| microdisplay_low_gray.png | 亮暗点 / 线缺陷 / Mura | 包含亮点、暗点、竖线和暗 Mura |
| uniform_good.png | 亮暗点 / 线缺陷 / Mura | 正常渐变，没有植入缺陷 |
| field_sfr.png | 全视场斜边 SFR | 三列高斯模糊 σ=1、1.5、2 px；MTF50 依次降低 |
| eyebox_04.png | Eyebox 扫描评价 | 导入 eyebox_scan.json；五个采样点满足阈值，四角合格网格面积为 0 |

完整真值、来源链接和文件 SHA-256 见 ground_truth.json。合成验证不等于真实模组验收。
""".encode("utf-8")
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("artifacts/display-metrology/samples"))
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()
    files = fixtures()
    if not args.overwrite:
        existing = [name for name in files if (args.output / name).exists()]
        if existing: parser.error("Output exists; choose another directory or use --overwrite: " + existing[0])
    args.output.mkdir(parents=True, exist_ok=True)
    for name, data in files.items(): (args.output / name).write_bytes(data)
    print(json.dumps({"output": str(args.output.resolve()), "files": len(files), "kind": "synthetic-ground-truth"}))


if __name__ == "__main__": main()
