"""Compare legacy P9 and Grid V2 through the same native DLL.

Run on Windows with numpy and opencv-python. Inputs and outputs are explicit;
the script never writes the source image or accesses devices/databases.
Performance includes native processing, JSON transfer and FreeResult, excluding
file loading. Synthetic correctness is reported separately from real-image drift.
"""

import argparse
import ctypes as ct
import hashlib
import json
import os
from pathlib import Path
import platform
import struct
import subprocess
import sys
import time

import cv2
import numpy as np


class HImage(ct.Structure):
    _pack_ = 8
    _fields_ = [(name, ct.c_int32) for name in ("rows", "cols", "channels", "depth", "stride")] + [("isDispose", ct.c_bool), ("pData", ct.c_void_p)]


class RoiRect(ct.Structure):
    _pack_ = 1
    _fields_ = [(name, ct.c_int32) for name in ("x", "y", "width", "height")]


def read_cvraw(path):
    with Path(path).open("rb") as source:
        def read(fmt):
            return struct.unpack("<" + fmt, source.read(struct.calcsize("<" + fmt)))
        if source.read(5) != b"CVCIE":
            raise ValueError("Expected a ColorVision CVRAW/CVCIE header")
        version, = read("I")
        if version not in (1, 2, 3):
            raise ValueError("Unsupported CV file version")
        filename_length, = read("i")
        if filename_length < 0:
            raise ValueError("Invalid filename length")
        source.read(filename_length)
        if version == 3:
            read("i")
        gain, channels = read("fI")
        read("f" * channels)
        width, height, bits = read("III")
        length, = read("q" if version == 2 else "i")
        if length != width * height * channels * (bits // 8):
            raise ValueError("Unexpected image payload length")
        payload = source.read(length)
        if len(payload) != length:
            raise ValueError("Truncated image")
    dtype = {8: "u1", 16: "<u2", 32: "<f4", 64: "<f8"}[bits]
    shape = (height, width) if channels == 1 else (height, width, channels)
    return np.frombuffer(payload, dtype=dtype).reshape(shape)


class NativeAlgorithms:
    def __init__(self, dll):
        dll = Path(dll).resolve()
        root = Path(__file__).resolve().parents[2]
        self.handles = []
        for directory in (dll.parent, root / "packages/opencv/x64/vc18/bin", root / "DLL/scgd_internal_dll"):
            if directory.is_dir():
                self.handles.append(os.add_dll_directory(str(directory)))
        self.library = ct.CDLL(str(dll))
        self.functions = {}
        for key, name in (("legacy", "M_CalDistortionP9"), ("v2", "M_CalDistortionGridV2")):
            function = getattr(self.library, name)
            function.argtypes = [HImage, RoiRect, ct.c_char_p, ct.POINTER(ct.c_void_p)]
            function.restype = ct.c_int
            self.functions[key] = function
        self.library.FreeResult.argtypes = [ct.c_void_p]
        self.library.FreeResult.restype = ct.c_int
        assert ct.sizeof(HImage) == 32

    def run(self, algorithm, image, rows, cols, bright=True):
        image = np.ascontiguousarray(image)
        channels = 1 if image.ndim == 2 else image.shape[2]
        native = HImage(image.shape[0], image.shape[1], channels, image.dtype.itemsize * 8, image.strides[0], True, image.ctypes.data)
        configuration = dict(expectedRows=rows, expectedCols=cols, brightTarget=bright)
        configuration.update(dict(threshold=-1, minRectSize=40, maxRectSize=400, erodeKernel=3, erodeIterations=0, tvCalcWay=0, sortWithPca=True) if algorithm == "legacy" else dict(maxProcessingSize=1600, minimumContrast=0.02, maximumGridResidualFraction=0.3))
        config = json.dumps(configuration).encode("utf-8")
        pointer = ct.c_void_p()
        started = time.perf_counter_ns()
        cpu_started = time.process_time_ns()
        code = self.functions[algorithm](native, RoiRect(0, 0, image.shape[1], image.shape[0]), config, ct.byref(pointer))
        try:
            raw = ct.string_at(pointer) if pointer.value else b""
        finally:
            if pointer.value:
                self.library.FreeResult(pointer)
        elapsed = (time.perf_counter_ns() - started) / 1e6
        cpu = (time.process_time_ns() - cpu_started) / 1e6
        result = json.loads(raw) if raw else None
        return dict(returnCode=code, elapsedMs=elapsed, processCpuMs=cpu, result=result)


def metrics(points, rows, cols, legacy=False):
    grid = np.asarray(points).reshape(rows, cols, 2)[np.ix_([0, rows // 2, rows - 1], [0, cols // 2, cols - 1])]
    widths = np.linalg.norm(grid[:, 0] - grid[:, 2], axis=1)
    heights = np.linalg.norm(grid[0] - grid[2], axis=1)
    wt, wm, wb = widths
    hl, hc, hr = heights
    gw = float(np.mean(widths)) if legacy else float((wt + wb) / 2)
    gh = float(np.mean(heights)) if legacy else float((hl + hr) / 2)
    def line_distance(p, a, b):
        edge, delta = b - a, p - a
        return float((edge[0] * delta[1] - edge[1] * delta[0]) / np.linalg.norm(edge))
    return dict(horizontalTvPercent=100 * ((wt + wb) / 2 - wm) / wm,
                verticalTvPercent=100 * ((hl + hr) / 2 - hc) / hc,
                topPercent=100 * line_distance(grid[0, 1], grid[0, 0], grid[0, 2]) / gh,
                bottomPercent=-100 * line_distance(grid[2, 1], grid[2, 0], grid[2, 2]) / gh,
                leftPercent=-100 * line_distance(grid[1, 0], grid[0, 0], grid[2, 0]) / gw,
                rightPercent=100 * line_distance(grid[1, 2], grid[0, 2], grid[2, 2]) / gw,
                keystoneHorizontalPercent=100 * ((wt - wb) / gw if legacy else (hl - hr) / gh),
                keystoneVerticalPercent=100 * ((hl - hr) / gh if legacy else (wt - wb) / gw))


def fixture(rows, effect):
    height, width = 1000, 1400
    points = np.array([(x, y) for y in np.linspace(230.2, 770.2, rows) for x in np.linspace(340.3, 1060.3, rows)])
    middle = np.array([700.3, 500.2])
    delta = points - middle
    if effect in ("barrel", "pincushion"):
        radius2 = np.sum((delta / [360, 270]) ** 2, axis=1) / 2
        points = middle + delta * (1 + (-0.12 if effect == "barrel" else 0.12) * radius2[:, None])
    if effect == "keystone-warp":
        points[:, 0] = middle[0] + delta[:, 0] * (1 + 0.18 * delta[:, 1] / 270)
    if effect == "perspective":
        points = middle + delta / (1 + 0.0004 * delta[:, 1, None])
    if effect == "rotate15":
        angle = np.deg2rad(15)
        rotation = np.array([[np.cos(angle), -np.sin(angle)], [np.sin(angle), np.cos(angle)]])
        points = middle + delta @ rotation.T
    image = np.full((height, width), 6.0, np.float32)
    if effect == "gradient":
        image += np.linspace(0, 100, width, dtype=np.float32)[None, :]
    if effect.startswith("leakage-"):
        yy, xx = np.mgrid[:height, :width]
        if effect == "leakage-gradient":
            image += (140.0 * xx / (width - 1)).astype(np.float32)
        elif effect == "leakage-band":
            image += (115.0 * np.exp(-((yy - 500.2) / 65.0) ** 2)).astype(np.float32)
        elif effect == "leakage-halo":
            image += (145.0 * np.exp(-((xx - 700.3) ** 2 + (yy - 500.2) ** 2) / (2 * 180.0 ** 2))).astype(np.float32)
    for index, (x, y) in enumerate(points):
        if effect == "missing-center" and index == len(points) // 2:
            continue
        amplitude = 200.0
        if effect.startswith("leakage-"):
            amplitude = 95.0
        if effect == "weak-center" and index == len(points) // 2:
            amplitude *= 0.18
        if effect == "low-signal":
            amplitude *= 0.15
        left, top = int(x) - 30, int(y) - 30
        yy, xx = np.mgrid[top:top + 61, left:left + 61]
        distance = np.hypot(xx - x, yy - y)
        signal = amplitude * np.clip((24.5 - distance) / 1.5, 0, 1)
        image[top:top + 61, left:left + 61] += signal.astype(np.float32)
    if effect == "reflection":
        cv2.circle(image, (1200, 130), 65, 250, -1)
    if effect == "noise":
        image += np.random.default_rng(71357).normal(0, 5, image.shape).astype(np.float32)
    if effect == "blur":
        image = cv2.GaussianBlur(image, (0, 0), 3)
    image = np.clip(image, 0, 255).astype(np.uint8)
    if effect == "half-size":
        image = cv2.resize(image, None, fx=0.5, fy=0.5, interpolation=cv2.INTER_AREA)
        points = (points + 0.5) * 0.5 - 0.5
    if effect == "dark-target":
        image = 255 - image
    if effect == "gray16":
        image = image.astype(np.uint16) * 128
    if effect == "hdr-reflection":
        image = image.astype(np.uint16)
        image[65:165, 1160:1260] = 65535
    if effect == "float32":
        image = image.astype(np.float32) / 255
    if effect == "blank":
        image[:] = 0
    if effect == "saturated-field":
        image[:] = 255
    return image, points, effect not in ("missing-center", "blank", "saturated-field"), effect != "dark-target"


def evaluate(record, truth, rows, expected_success, algorithm):
    result = record["result"] or {}
    accepted = result.get("success") is True
    evaluation = dict(accepted=accepted, expectedSuccess=expected_success)
    if not expected_success:
        evaluation["outcome"] = "false_accept" if accepted else "correct_reject"
        return evaluation
    if not accepted:
        evaluation["outcome"] = "false_reject"
        return evaluation
    measured = result.get("points", [])
    pairs = {(p.get("row"), p.get("col")): p for p in measured}
    if len(measured) != rows * rows or len(pairs) != rows * rows or any((r, c) not in pairs for r in range(rows) for c in range(rows)):
        evaluation["outcome"] = "invalid_grid"
        return evaluation
    xy = np.array([[pairs[r, c]["x"], pairs[r, c]["y"]] for r in range(rows) for c in range(rows)])
    error = np.linalg.norm(xy - truth, axis=1)
    evaluation.update(pointRmsPx=float(np.sqrt(np.mean(error ** 2))), pointMaxPx=float(error.max()))
    calculated = result.get("metrics") or {}
    expected = metrics(truth, rows, rows, legacy=algorithm == "legacy")
    metric_errors = {key: abs(float(calculated.get(key, float("inf"))) - float(value)) for key, value in expected.items()}
    evaluation["metricMaxErrorPercentagePoints"] = max(metric_errors.values())
    valid_spans = all(float(calculated.get(key, 0)) > 0 for key in ("topWidth", "middleWidth", "bottomWidth", "leftHeight", "centerHeight", "rightHeight"))
    evaluation["outcome"] = "correct" if error.max() <= 1.0 and valid_spans and max(metric_errors.values()) <= 0.1 else "incorrect_accept"
    return evaluation


def peak_working_set():
    class Counters(ct.Structure):
        _fields_ = [("cb", ct.c_ulong), ("PageFaultCount", ct.c_ulong)] + [(name, ct.c_size_t) for name in ("PeakWorkingSetSize", "WorkingSetSize", "QuotaPeakPagedPoolUsage", "QuotaPagedPoolUsage", "QuotaPeakNonPagedPoolUsage", "QuotaNonPagedPoolUsage", "PagefileUsage", "PeakPagefileUsage")]
    counters = Counters()
    counters.cb = ct.sizeof(counters)
    ct.windll.psapi.GetProcessMemoryInfo.argtypes = [ct.c_void_p, ct.c_void_p, ct.c_ulong]
    ct.windll.psapi.GetProcessMemoryInfo(ct.c_void_p(-1), ct.byref(counters), counters.cb)
    return counters.PeakWorkingSetSize


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", required=True, type=Path)
    parser.add_argument("--sample", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--runs", type=int, default=20)
    parser.add_argument("--warmup", type=int, default=3)
    parser.add_argument("--skip-performance", action="store_true")
    parser.add_argument("--worker", choices=("legacy", "v2"))
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    native = NativeAlgorithms(args.dll)
    if args.worker:
        if args.sample is None:
            parser.error("performance worker needs --sample")
        image = read_cvraw(args.sample)
        baseline_peak = peak_working_set()
        for _ in range(args.warmup):
            native.run(args.worker, image, 3, 3)
        records = [native.run(args.worker, image, 3, 3) for _ in range(args.runs)]
        elapsed = [r["elapsedMs"] for r in records]
        summary = dict(algorithm=args.worker, samples=args.runs, warmup=args.warmup,
                       medianMs=float(np.median(elapsed)), p95Ms=float(np.percentile(elapsed, 95)), maxMs=max(elapsed),
                       medianProcessCpuMs=float(np.median([r["processCpuMs"] for r in records])),
                       peakWorkingSetGrowthMiB=(peak_working_set() - baseline_peak) / 1048576,
                       timingBoundary="native ABI call + JSON bytes copy + FreeResult, excludes decoding/JSON parsing",
                       memoryBoundary="process lifetime peak working-set increase from loaded-image baseline through warmup and measured calls; not total algorithm allocations",
                       records=records)
        (args.output / (args.worker + "-performance.json")).write_text(json.dumps(summary, indent=2), encoding="utf-8")
        print(json.dumps({k: v for k, v in summary.items() if k != "records"}))
        return
    environment = dict(python=sys.version, platform=platform.platform(), logicalCpuCount=os.cpu_count(),
                       opencvPython=cv2.__version__, dll=str(args.dll.resolve()), dllSha256=hashlib.sha256(args.dll.read_bytes()).hexdigest(),
                       sample=str(args.sample) if args.sample else None,
                       sampleSha256=hashlib.sha256(args.sample.read_bytes()).hexdigest() if args.sample else None,
                       correctnessGate="Synthetic full grid, max point error <=1 px, valid positive spans and max metric error <=0.1 percentage point; old/new respective formula conventions. Missing/blank must reject.",
                       limitations="One real sample is not a labeled production corpus. Synthetic cases do not estimate field failure probability. Other system workloads and default OpenCV threading may affect wall-clock timing.")
    (args.output / "environment.json").write_text(json.dumps(environment, indent=2), encoding="utf-8")
    correctness = []
    effects = ("clean", "barrel", "pincushion", "perspective", "keystone-warp", "rotate15", "half-size", "weak-center", "low-signal", "gradient", "noise", "blur", "reflection", "gray16", "hdr-reflection", "float32", "dark-target", "leakage-gradient", "leakage-band", "leakage-halo", "missing-center", "blank", "saturated-field")
    for rows in (3, 7):
        for effect in effects:
            image, truth, expected_success, bright = fixture(rows, effect)
            case = dict(name=f"{rows}x{rows}-{effect}", rows=rows, cols=rows, expectedPoints=truth.tolist(), algorithms={})
            for algorithm in ("legacy", "v2"):
                record = native.run(algorithm, image, rows, rows, bright)
                record["evaluation"] = evaluate(record, truth, rows, expected_success, algorithm)
                case["algorithms"][algorithm] = record
            correctness.append(case)
            print(case["name"], {name: item["evaluation"] for name, item in case["algorithms"].items()}, flush=True)
    (args.output / "correctness.json").write_text(json.dumps(correctness, indent=2), encoding="utf-8")
    if args.sample:
        image = read_cvraw(args.sample)
        real = {algorithm: native.run(algorithm, image, 3, 3) for algorithm in ("legacy", "v2")}
        (args.output / "real-sample.json").write_text(json.dumps(real, indent=2), encoding="utf-8")
        if not args.skip_performance:
            for algorithm in ("legacy", "v2"):
                subprocess.run([sys.executable, str(Path(__file__).resolve()), "--dll", str(args.dll.resolve()), "--sample", str(args.sample.resolve()), "--output", str(args.output.resolve()), "--runs", str(args.runs), "--warmup", str(args.warmup), "--worker", algorithm], check=True)
    counts = {algorithm: {} for algorithm in ("legacy", "v2")}
    for case in correctness:
        for algorithm, record in case["algorithms"].items():
            outcome = record["evaluation"]["outcome"]
            counts[algorithm][outcome] = counts[algorithm].get(outcome, 0) + 1
    (args.output / "summary.json").write_text(json.dumps(counts, indent=2), encoding="utf-8")
    print(json.dumps(counts))


if __name__ == "__main__":
    main()
