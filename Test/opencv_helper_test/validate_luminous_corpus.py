"""Replay a local luminous-area corpus against an explicit, immutable reference.

Derived from the reviewed Find corpus validation script. References describe
reviewed behavior, not pixel-level ground truth. This tool never promotes a run.
"""

from __future__ import annotations

import argparse
from contextlib import ExitStack
import ctypes as ct
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import struct
import sys
import time

import cv2
import numpy as np


EXTENSIONS = {".cvraw", ".tiff", ".tif", ".png", ".bmp", ".jpg", ".jpeg"}
DTYPES = {np.dtype("uint8"): 8, np.dtype("uint16"): 16,
          np.dtype("float32"): 32, np.dtype("float64"): 64}


class HImage(ct.Structure):
    _pack_ = 8
    _fields_ = [(name, ct.c_int32) for name in ("rows", "cols", "channels", "depth", "stride")] + [
        ("isDispose", ct.c_bool), ("pData", ct.c_void_p)]


class RoiRect(ct.Structure):
    _pack_ = 1
    _fields_ = [(name, ct.c_int32) for name in ("x", "y", "width", "height")]


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def sha256(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def reject_constant(value):
    raise ValueError(f"Non-finite JSON number: {value}")


def write_json(path, value):
    # Exclusive creation prevents accidentally replacing a reference or old evidence.
    with Path(path).open("x", encoding="utf-8") as target:
        json.dump(value, target, ensure_ascii=False, indent=2, allow_nan=False)
        target.write("\n")


def discover(samples):
    samples = Path(samples).resolve(strict=True)
    if not samples.is_dir():
        raise ValueError("Samples must be a directory")
    paths = sorted((p for p in samples.iterdir() if p.is_file() and p.suffix.lower() in EXTENSIONS),
                   key=lambda p: p.name.casefold())
    if not paths:
        raise ValueError("No original images found in the sample directory")
    return paths


def read_cvraw(data):
    """Read the interleaved camera payload (CVCIE versions 1/2/3)."""
    offset = 5
    if data[:5] != b"CVCIE":
        raise ValueError("Invalid CVRAW magic")

    def read(fmt):
        nonlocal offset
        size = struct.calcsize("<" + fmt)
        if offset + size > len(data):
            raise ValueError("Truncated CVRAW header")
        values = struct.unpack_from("<" + fmt, data, offset)
        offset += size
        return values

    version, = read("I")
    if version not in (1, 2, 3):
        raise ValueError("Unsupported CVRAW version")
    name_length, = read("i")
    if name_length < 0 or name_length > len(data) - offset:
        raise ValueError("Invalid CVRAW name length")
    offset += name_length
    if version == 3:
        read("i")  # NDPort
    _, channels = read("fI")
    if channels not in (1, 3, 4):
        raise ValueError("Unsupported CVRAW channel count")
    read("f" * channels)
    width, height, bits = read("III")
    if not (0 < width <= 2147483647 and 0 < height <= 2147483647):
        raise ValueError("Invalid CVRAW dimensions")
    if bits not in (8, 16, 32, 64):
        raise ValueError("Unsupported CVRAW depth")
    length, = read("q" if version == 2 else "i")
    expected = width * height * channels * (bits // 8)
    if length != expected or length > len(data) - offset:
        raise ValueError("CVRAW payload length mismatch")
    dtype = {8: "u1", 16: "<u2", 32: "<f4", 64: "<f8"}[bits]
    shape = (height, width) if channels == 1 else (height, width, channels)
    # FileIO permits optional acquisition/calibration metadata after the pixels.
    # Hash the complete original file, but pass only its declared pixels to native.
    return np.frombuffer(data, dtype=dtype, offset=offset, count=width * height * channels).reshape(shape)


def load_image(data, suffix):
    image = read_cvraw(data) if suffix.lower() == ".cvraw" else cv2.imdecode(
        np.frombuffer(data, dtype=np.uint8), cv2.IMREAD_UNCHANGED)
    if image is None:
        raise ValueError("Unable to decode image")
    if image.dtype not in DTYPES:
        raise ValueError(f"Unsupported image dtype: {image.dtype}")
    if image.ndim not in (2, 3) or (image.ndim == 3 and image.shape[2] not in (1, 3, 4)):
        raise ValueError(f"Unsupported image shape: {image.shape}")
    return np.ascontiguousarray(image)


def validate_result(result):
    if not isinstance(result, dict) or type(result.get("Success")) is not bool:
        raise ValueError("Native result must contain a boolean Success")

    def finite_numbers(value):
        if isinstance(value, float) and not math.isfinite(value):
            raise ValueError("Native result contains a non-finite number")
        if isinstance(value, dict):
            for item in value.values():
                finite_numbers(item)
        elif isinstance(value, list):
            for item in value:
                finite_numbers(item)

    finite_numbers(result)
    corners = result.get("Corners")
    if not isinstance(corners, list) or len(corners) not in ((4,) if result["Success"] else (0, 4)):
        raise ValueError("Native result violates the success/corner contract")
    for point in corners:
        if not isinstance(point, dict) or any(
                type(point.get(axis)) not in (int, float) for axis in ("X", "Y")):
            raise ValueError("Native corner must contain finite numeric X/Y")
    if not result["Success"] and (not isinstance(result.get("FailureReason"), str) or not result["FailureReason"]):
        raise ValueError("Native rejection must contain a failure reason")


def index_rows(rows):
    indexed = {}
    if not isinstance(rows, list):
        raise ValueError("Report rows must be an array")
    for row in rows:
        name = row.get("file") if isinstance(row, dict) else None
        if not isinstance(name, str) or not name or Path(name).name != name:
            raise ValueError("Each row must identify one sample filename")
        if name.casefold() in indexed:
            raise ValueError(f"Duplicate sample row: {name}")
        indexed[name.casefold()] = row
    return indexed


def reference_rows(reference):
    if not isinstance(reference, dict) or not isinstance(reference.get("config", {}), dict):
        raise ValueError("Reference must be an object with an algorithm configuration object")
    if reference.get("schemaVersion", 1) != 1:
        raise ValueError("Unsupported reference schema version")
    rows = index_rows(reference.get("rows"))
    if reference.get("technicalErrors", 0) or reference.get("status") == "technical-error":
        raise ValueError("A technically failed run cannot be used as a reference")
    for row in rows.values():
        if row.get("error"):
            raise ValueError(f"A failed observation cannot be used as a reference: {row['file']}")
        validate_result(row.get("result"))
    return rows


def changed_fields(expected, actual, prefix):
    if type(expected) is not type(actual):
        return [prefix]
    if isinstance(expected, dict):
        changes = []
        for key in sorted(set(expected) | set(actual)):
            field = f"{prefix}.{key}"
            changes.extend([field] if key not in expected or key not in actual
                           else changed_fields(expected[key], actual[key], field))
        return changes
    if isinstance(expected, list):
        if len(expected) != len(actual):
            return [prefix]
        return [field for i, (old, new) in enumerate(zip(expected, actual))
                for field in changed_fields(old, new, f"{prefix}[{i}]")]
    return [] if expected == actual else [prefix]


def compare_rows(expected, actual):
    previous, current = index_rows(expected), index_rows(actual)
    differences = []
    for key in sorted(set(previous) | set(current)):
        old, new = previous.get(key), current.get(key)
        name = (new if new is not None else old)["file"]
        if old is None or new is None:
            changes = ["added" if old is None else "missing"]
        else:
            changes = []
            for field in ("sha256", "sizeBytes", "shape", "dtype", "returnCode", "result"):
                changes.extend([field + ":unbound"] if field not in old or field not in new
                               else changed_fields(old[field], new[field], field))
            if new.get("error"):
                changes.append("error")
        if changes:
            differences.append({"file": name, "changes": changes})
    return differences


def bind_reference(samples, reference, output):
    reference_bytes = Path(reference).read_bytes()
    original = json.loads(reference_bytes, parse_constant=reject_constant)
    rows = reference_rows(original)
    paths = discover(samples)
    if set(rows) != {path.name.casefold() for path in paths}:
        raise ValueError("Reference and sample filenames must match completely before binding")
    bound = []
    for path in paths:
        row = dict(rows[path.name.casefold()])
        if row.get("error") or type(row.get("returnCode")) is not int or row["returnCode"] <= 0:
            raise ValueError(f"Cannot bind a failed observation: {path.name}")
        validate_result(row.get("result"))
        if not isinstance(row.get("shape"), list) or row.get("dtype") not in ("uint8", "uint16", "float32", "float64"):
            raise ValueError(f"Reference lacks image metadata: {path.name}")
        data = path.read_bytes()
        identity = {"sha256": hashlib.sha256(data).hexdigest(), "sizeBytes": len(data)}
        del data
        if any(field in row and row[field] != value for field, value in identity.items()):
            raise ValueError(f"Cannot rebind changed input identity: {path.name}")
        row.update(identity)
        bound.append(row)
    result = {"schemaVersion": 1, "referenceKind": "reviewed-behavior", "config": original.get("config", {}),
              "provenance": {"sourceReport": str(Path(reference).resolve()),
                             "sourceReportSha256": hashlib.sha256(reference_bytes).hexdigest(), "boundAtUtc": utc_now(),
                             "inputIdentity": "bound-at-import",
                             "limitation": "Current input hashes do not prove historical input identity or pixel-level accuracy."},
              "rows": bound}
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    write_json(output, result)
    return result


class NativeDetector:
    def __init__(self, path):
        if sys.platform != "win32" or ct.sizeof(ct.c_void_p) != 8:
            raise ValueError("Native corpus validation requires Windows x64 Python")
        if ct.sizeof(HImage) != 32 or HImage.isDispose.offset != 20 or HImage.pData.offset != 24 or ct.sizeof(RoiRect) != 16:
            raise ValueError("ctypes structures do not match the native x64 ABI")
        self.library = ct.CDLL(str(path))
        self.call = self.library.M_FindLuminousAreaV2
        self.call.argtypes = [HImage, RoiRect, ct.c_char_p, ct.POINTER(ct.c_void_p)]
        self.call.restype = ct.c_int
        self.library.FreeResult.argtypes = [ct.c_void_p]
        self.library.FreeResult.restype = ct.c_int
        get_module_path = ct.WinDLL("kernel32", use_last_error=True).GetModuleFileNameW
        get_module_path.argtypes = [ct.c_void_p, ct.c_wchar_p, ct.c_uint32]
        get_module_path.restype = ct.c_uint32
        buffer = ct.create_unicode_buffer(32768)
        if not get_module_path(self.library._handle, buffer, len(buffer)):
            raise OSError("Unable to identify the loaded native DLL")
        self.loaded_path = str(Path(buffer.value).resolve(strict=True))
        if Path(self.loaded_path) != Path(path):
            raise ValueError(f"Loaded native DLL differs from the requested DLL: {self.loaded_path}")

    def detect(self, image):
        if image.strides[0] > 2147483647 or any(size > 2147483647 for size in image.shape):
            raise ValueError("Image layout exceeds native int32 limits")
        source = HImage(image.shape[0], image.shape[1], image.shape[2] if image.ndim == 3 else 1,
                        DTYPES[image.dtype], image.strides[0], False, image.ctypes.data)
        pointer = ct.c_void_p()
        started = time.perf_counter()
        code = self.call(source, RoiRect(0, 0, 0, 0), b"{}", ct.byref(pointer))
        elapsed = (time.perf_counter() - started) * 1000
        try:
            if code <= 0 or not pointer:
                raise ValueError(f"Native call failed: returnCode={code}, hasResult={bool(pointer)}")
            result = json.loads(ct.string_at(pointer), parse_constant=reject_constant)
            validate_result(result)
            return code, elapsed, result
        finally:
            if pointer:
                self.library.FreeResult(pointer)


def save_preview(path, image, result, expected=None):
    scale = min(1.0, 960 / image.shape[1])
    small = cv2.resize(image, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
    if small.ndim == 3 and small.shape[2] != 1:
        # OpenCV color conversion does not accept float64; conversion is preview-only.
        source = small.astype(np.float32) if small.dtype == np.float64 else small
        gray = cv2.cvtColor(source, cv2.COLOR_BGRA2GRAY if small.shape[2] == 4 else cv2.COLOR_BGR2GRAY)
    else:
        gray = small.squeeze()
    finite = gray[np.isfinite(gray)]
    low, high = np.percentile(finite, (0.1, 99)) if finite.size else (0, 1)
    normalized = np.nan_to_num((gray.astype(float) - low) / max(high - low, 1), nan=0, posinf=1, neginf=0)
    preview = cv2.cvtColor(np.uint8(np.clip(normalized, 0, 1) * 255), cv2.COLOR_GRAY2BGR)
    for observation, color in ((expected, (0, 255, 0)), (result, (0, 0, 255))):
        if observation and observation.get("Corners"):
            points = np.int32([[p["X"] * scale, p["Y"] * scale] for p in observation["Corners"]])
            cv2.polylines(preview, [points], True, color, 1 if observation is expected else 2)
    success, encoded = cv2.imencode(".png", preview)
    if not success:
        raise ValueError("Unable to encode preview")
    encoded.tofile(path)


def run(args):
    paths = discover(args.samples)
    dll = args.dll.resolve(strict=True)
    reference_bytes = args.reference.read_bytes() if args.reference else None
    reference = json.loads(reference_bytes, parse_constant=reject_constant) if reference_bytes is not None else None
    expected = reference_rows(reference) if reference is not None else {}
    for directory in args.dependency_dir:
        if not directory.is_dir():
            raise ValueError(f"Explicit dependency directory does not exist: {directory}")
    output = args.output.resolve()
    if output == args.samples.resolve() or args.samples.resolve() in output.parents:
        raise ValueError("Output must be outside the original sample directory")
    output.mkdir(parents=True, exist_ok=False)
    previews = output / "previews"
    previews.mkdir()
    report = {"schemaVersion": 1, "startedAtUtc": utc_now(), "samples": str(args.samples.resolve()),
              "dll": str(dll), "dllSha256": sha256(dll), "scriptSha256": sha256(__file__), "config": {},
              "reference": str(args.reference.resolve()) if args.reference else None,
              "referenceSha256": hashlib.sha256(reference_bytes).hexdigest() if reference_bytes is not None else None,
              "environment": {"python": platform.python_version(), "numpy": np.__version__,
                              "opencv": cv2.__version__, "platform": platform.platform()}, "rows": []}
    dependency_dirs = [dll.parent, Path(__file__).resolve().parents[2] / "packages/opencv/x64/vc18/bin"]
    dependency_dirs.extend(args.dependency_dir)
    report["dependencyDirectories"] = [str(path.resolve()) for path in dependency_dirs if path.is_dir()]
    report["dependencyIdentityScope"] = "Search directories only; these do not identify every loaded dependency."
    with ExitStack() as stack:
        checkpoint = stack.enter_context((output / "observations.jsonl").open("x", encoding="utf-8"))
        detector, load_error = None, None
        try:
            for directory in report["dependencyDirectories"]:
                stack.enter_context(os.add_dll_directory(directory))
            detector = NativeDetector(dll)
            report["loadedNativeLibrary"] = getattr(detector, "loaded_path", str(dll))
        except (OSError, ValueError, AttributeError) as error:
            load_error = str(error)
        for path in paths:
            row = {"file": path.name}
            try:
                data = path.read_bytes()
                row.update(sha256=hashlib.sha256(data).hexdigest(), sizeBytes=len(data))
                image = load_image(data, path.suffix)
                del data
                row.update(shape=list(image.shape), dtype=str(image.dtype))
                if load_error:
                    raise ValueError(load_error)
                code, elapsed, result = detector.detect(image)
                row.update(returnCode=code, elapsedMs=elapsed, result=result)
                save_preview(previews / (path.name + ".png"), image, result,
                             expected.get(path.name.casefold(), {}).get("result"))
            except Exception as error:
                row["error"] = f"{type(error).__name__}: {error}"
            finally:
                image = None
            report["rows"].append(row)
            # Checkpoint completed samples, so later technical failures retain prior evidence.
            checkpoint.write(json.dumps(row, ensure_ascii=False, allow_nan=False) + "\n")
            checkpoint.flush()
            print(json.dumps({"file": path.name, "success": row.get("result", {}).get("Success"),
                              "error": row.get("error")}, ensure_ascii=True), flush=True)
    report["differences"] = compare_rows(reference["rows"], report["rows"]) if reference is not None else []
    reasons = []
    if reference is None:
        reasons.append("No reference supplied; observations do not establish correctness")
    else:
        if reference.get("config", {}) != report["config"]:
            reasons.append("Algorithm configuration differs from the reference")
        if reference.get("referenceKind") != "reviewed-behavior":
            reasons.append("Reference lacks an explicit reviewed-behavior declaration; compare for review only")
    errors = sum(bool(row.get("error")) for row in report["rows"])
    if sha256(dll) != report["dllSha256"]:
        reasons.append("Native DLL changed during the run")
        errors += 1
    exit_code = 1 if errors else 2 if reasons or report["differences"] else 0
    report.update(completedAtUtc=utc_now(), technicalErrors=errors, reviewReasons=reasons,
                  exitCode=exit_code, status={0: "unchanged", 1: "technical-error", 2: "needs-review"}[exit_code])
    write_json(output / "report.json", report)
    differences = {item["file"]: item["changes"] for item in report["differences"]}
    lines = ["# 发光区样本回归", "", f"状态：{report['status']}；退出码：{exit_code}。", "",
             f"文件数：{len(paths)}；技术错误：{errors}；需复核文件：{len(differences)}。", "",
             "参考只表示已复核的历史行为，不是逐像素真值。单次调用耗时仅供观察。",
             "预览绿色为参考边界，红色为本次边界；重合时红色覆盖绿色。", ""]
    lines.extend(f"- {reason}" for reason in reasons)
    for row in report["rows"]:
        changes = differences.get(row["file"], [])
        conclusion = ", ".join(changes) if changes else ("公开结果与输入身份一致。" if reference is not None else "仅记录本次观测，尚无参考可比较。")
        lines.extend([f"## {row['file']}", "", row.get("error") or conclusion, ""])
        preview = previews / (row["file"] + ".png")
        if preview.exists():
            lines.extend([f"![叠图](previews/{preview.name})", ""])
    (output / "summary.md").write_text("\n".join(lines), encoding="utf-8")
    print(f"{report['status']}: {output / 'report.json'}", flush=True)
    return exit_code


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    bind = commands.add_parser("bind-reference", help="Bind current file hashes to existing reviewed outputs; never rerun or replace them")
    replay = commands.add_parser("run", help="Replay all original files without modifying samples or references")
    for command in (bind, replay):
        command.add_argument("--samples", type=Path, required=True)
        command.add_argument("--reference", type=Path, required=command is bind)
        command.add_argument("--output", type=Path, required=True)
    replay.add_argument("--dll", type=Path, required=True)
    replay.add_argument("--dependency-dir", type=Path, action="append", default=[])
    args = parser.parse_args(argv)
    try:
        if args.command == "bind-reference":
            result = bind_reference(args.samples, args.reference, args.output)
            print(f"Bound {len(result['rows'])} reviewed outputs; expected results were preserved: {args.output}")
            return 0
        return run(args)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"Validation setup failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
