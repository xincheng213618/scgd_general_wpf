"""Compare full-image 7x7 detection on the 14 OpenCV circles regression images.

Requires numpy and opencv-python. Supply an existing dataset containing
circles1.png ... circles14.png and circles_corners1.dat ... circles_corners14.dat.
No downloads, reference-based cropping, image-specific tuning, or source writes.
Use a new/empty output directory for each run to preserve earlier evidence.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import platform
import sys
import time

import cv2
import numpy as np

from benchmark_grid_distortion import NativeAlgorithms, metrics


GRID_SIZE = 7
CASE_COUNT = 14


def sha256(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2, allow_nan=False), encoding="utf-8")


def load_case(dataset, index):
    image_path = dataset / f"circles{index}.png"
    reference_path = dataset / f"circles_corners{index}.dat"
    if not image_path.is_file() or not reference_path.is_file():
        raise ValueError(f"Missing image or reference for circles{index} in {dataset}")
    # This fixed decode matches the original public-sample comparison; no resizing.
    image = cv2.imread(str(image_path), cv2.IMREAD_GRAYSCALE)
    if image is None or image.ndim != 2 or image.dtype != np.uint8 or min(image.shape) < GRID_SIZE:
        raise ValueError(f"Invalid grayscale-decodable image: {image_path}")
    storage = cv2.FileStorage(str(reference_path), cv2.FILE_STORAGE_READ)
    try:
        if not storage.isOpened():
            raise ValueError(f"Cannot open reference: {reference_path}")
        found = storage.getNode("isFound")
        reference = storage.getNode("corners").mat()
        if found.empty() or found.real() != 1 or reference is None or reference.shape != (GRID_SIZE, GRID_SIZE, 2):
            raise ValueError(f"Expected isFound=1 and 7x7 two-channel corners: {reference_path}")
        reference = np.asarray(reference, dtype=np.float64)
        if not np.isfinite(reference).all():
            raise ValueError(f"Non-finite reference coordinates: {reference_path}")
        if ((reference < 0).any() or (reference[:, :, 0] >= image.shape[1]).any()
                or (reference[:, :, 1] >= image.shape[0]).any()
                or len(np.unique(reference.reshape(-1, 2), axis=0)) != GRID_SIZE ** 2):
            raise ValueError(f"Out-of-image or duplicate reference coordinates: {reference_path}")
    finally:
        storage.release()
    manifest = dict(name=f"circles{index}", width=image.shape[1], height=image.shape[0],
                    imageFile=image_path.name, imageSha256=sha256(image_path),
                    referenceFile=reference_path.name, referenceSha256=sha256(reference_path))
    return image, reference, manifest


def align_square_grid(points, reference):
    """Only eight whole-grid symmetries; no fitting or arbitrary point matching."""
    measured = np.asarray(points, dtype=np.float64).reshape(GRID_SIZE, GRID_SIZE, 2)
    choices = []
    for transpose in (False, True):
        initial = reference.transpose(1, 0, 2) if transpose else reference
        for flip_rows in (False, True):
            for flip_columns in (False, True):
                oriented = initial[::-1] if flip_rows else initial
                oriented = oriented[:, ::-1] if flip_columns else oriented
                errors = np.linalg.norm(measured - oriented, axis=2)
                choices.append((float(np.sqrt(np.mean(errors ** 2))), float(errors.max()), oriented.copy(),
                                dict(transpose=transpose, flipRows=flip_rows, flipColumns=flip_columns)))
    return min(choices, key=lambda choice: choice[0])


def metric_difference(actual, expected):
    if not isinstance(actual, dict):
        return dict(available=False, reason="missing_metrics")
    differences = {}
    for key, expected_value in expected.items():
        value = actual.get(key)
        if isinstance(value, bool) or not isinstance(value, (float, int)) or not np.isfinite(value):
            return dict(available=False, reason=f"missing_or_non_finite_metric:{key}")
        differences[key] = float(value - expected_value)
    return dict(available=True, signedPercentagePoints=differences,
                maxAbsolutePercentagePoints=max(abs(value) for value in differences.values()))


def compare_points(points, reference, legacy=False, reported_metrics=None):
    rmse, maximum, oriented, orientation = align_square_grid(points, reference)
    expected = metrics(oriented, GRID_SIZE, GRID_SIZE, legacy=legacy)
    measured = metrics(points, GRID_SIZE, GRID_SIZE, legacy=legacy)
    comparison = dict(detected=True, status="ok", referenceRmsePx=rmse, referenceMaxPx=maximum,
                      withinReferenceRmse2Px=rmse <= 2.0, withinReferenceMax1Px=maximum <= 1.0,
                      orientation=orientation,
                      formula="legacy_three_span_swapped_keystone" if legacy else "opposite_edge_mean",
                      referenceMetrics=expected, metricsFromDetectedPoints=measured,
                      geometryVsReference=metric_difference(measured, expected))
    if reported_metrics is not None:
        comparison["reportedVsReference"] = metric_difference(reported_metrics, expected)
        comparison["reportedVsGeometry"] = metric_difference(reported_metrics, measured)
    return comparison


def compare_native(record, reference, image_shape, legacy=False):
    result = record.get("result") or {}
    if result.get("success") is not True:
        return dict(detected=False, status=result.get("statusCode", "no_result"))
    points = result.get("points")
    if not isinstance(points, list) or len(points) != GRID_SIZE ** 2:
        return dict(detected=False, status="invalid_point_count", nativeClaimedSuccess=True)
    ordered = {}
    for point in points:
        if not isinstance(point, dict):
            return dict(detected=False, status="invalid_point", nativeClaimedSuccess=True)
        row, col = point.get("row"), point.get("col")
        if (type(row) is not int or type(col) is not int or not 0 <= row < GRID_SIZE
                or not 0 <= col < GRID_SIZE or (row, col) in ordered):
            return dict(detected=False, status="invalid_grid_indices", nativeClaimedSuccess=True)
        x, y = point.get("x"), point.get("y")
        if (isinstance(x, bool) or isinstance(y, bool) or not isinstance(x, (float, int))
                or not isinstance(y, (float, int)) or not np.isfinite([x, y]).all()
                or not 0 <= x < image_shape[1] or not 0 <= y < image_shape[0]):
            return dict(detected=False, status="invalid_coordinate", nativeClaimedSuccess=True)
        ordered[row, col] = (x, y)
    xy = np.asarray([ordered[r, c] for r in range(GRID_SIZE) for c in range(GRID_SIZE)], dtype=np.float64)
    if len(np.unique(xy, axis=0)) != GRID_SIZE ** 2:
        return dict(detected=False, status="duplicate_coordinate", nativeClaimedSuccess=True)
    return compare_points(xy, reference, legacy, result.get("metrics", {}))


def run_opencv(image, reference):
    started = time.perf_counter_ns()
    found, centers = cv2.findCirclesGrid(image, (GRID_SIZE, GRID_SIZE), flags=cv2.CALIB_CB_SYMMETRIC_GRID)
    elapsed = (time.perf_counter_ns() - started) / 1e6
    record = dict(elapsedMs=elapsed, found=bool(found))
    if not found:
        record["comparison"] = dict(detected=False, status="not_found")
        return record
    if centers is None or centers.size != GRID_SIZE ** 2 * 2 or not np.isfinite(centers).all():
        record["comparison"] = dict(detected=False, status="invalid_centers")
        return record
    points = centers.reshape(-1, 2).astype(np.float64)
    record["points"] = points.tolist()
    record["comparison"] = compare_points(points, reference)
    return record


def summarize(cases, algorithms):
    summary = {}
    for algorithm in algorithms:
        records = [case["algorithms"][algorithm]["comparison"] for case in cases]
        detected = [record for record in records if record["detected"]]
        summary[algorithm] = dict(
            detected=len(detected),
            referenceRmseWithin2Px=sum(record.get("withinReferenceRmse2Px", False) for record in records),
            referenceMaxWithin1Px=sum(record.get("withinReferenceMax1Px", False) for record in records),
            maxReferenceRmsePx=max((record["referenceRmsePx"] for record in detected), default=None),
            maxReferencePointDeviationPx=max((record["referenceMaxPx"] for record in detected), default=None),
            rejected=[dict(name=case["name"], status=record["status"])
                      for case, record in zip(cases, records) if not record["detected"]])
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", required=True, type=Path, help="Native opencv_helper.dll to evaluate")
    parser.add_argument("--dataset", required=True, type=Path, help="Directory containing all 14 image/reference pairs")
    parser.add_argument("--output", required=True, type=Path, help="New or empty directory; earlier runs are never overwritten")
    parser.add_argument("--skip-opencv", action="store_true", help="Skip the Python OpenCV baseline, still run both native algorithms")
    args = parser.parse_args()
    args.dll, args.dataset, args.output = args.dll.resolve(), args.dataset.resolve(), args.output.resolve()
    if not args.dll.is_file():
        parser.error(f"DLL does not exist: {args.dll}")
    if not args.dataset.is_dir():
        parser.error(f"Dataset directory does not exist: {args.dataset}")
    if args.output.exists() and (not args.output.is_dir() or any(args.output.iterdir())):
        parser.error("Output must be a new or empty directory; choose a new path to preserve prior evidence")
    manifest = []
    try:
        for index in range(1, CASE_COUNT + 1):
            _, _, entry = load_case(args.dataset, index)
            manifest.append(entry)
    except (ValueError, cv2.error) as error:
        parser.error(str(error))
    native = NativeAlgorithms(args.dll)
    args.output.mkdir(parents=True, exist_ok=True)
    environment = dict(
        startedUtc=datetime.now(timezone.utc).isoformat(), python=sys.version, platform=platform.platform(),
        openCvPython=cv2.__version__, numpy=np.__version__, opencvPythonThreads=cv2.getNumThreads(),
        dllPath=str(args.dll), dllSha256=sha256(args.dll), datasetPath=str(args.dataset),
        runnerSha256=sha256(__file__), nativeWrapperSha256=sha256(Path(__file__).with_name("benchmark_grid_distortion.py")),
        caseCount=CASE_COUNT, dataset=manifest, skippedOpenCv=args.skip_opencv,
        preprocessing="Original resolution, IMREAD_GRAYSCALE, full image, expectedRows=7, expectedCols=7, brightTarget=false; no reference-based ROI or per-image tuning.",
        nativeConfigurations=dict(
            legacy=dict(expectedRows=7, expectedCols=7, brightTarget=False, threshold=-1, minRectSize=40,
                        maxRectSize=400, erodeKernel=3, erodeIterations=0, tvCalcWay=0, sortWithPca=True),
            v2=dict(expectedRows=7, expectedCols=7, brightTarget=False, maxProcessingSize=1600,
                    minimumContrast=0.02, maximumGridResidualFraction=0.3)),
        opencvConfiguration="findCirclesGrid(image, (7, 7), flags=CALIB_CB_SYMMETRIC_GRID); default blob detector",
        referencePolicy="Official OpenCV .dat regression coordinates, not calibration/metrology ground truth. Compare using the best of eight whole-square-grid transpose/flip symmetries only; no arbitrary point permutation or coordinate fit. RMSE<=2px and maximum<=1px are separate descriptive bins, not an absolute accuracy certification.",
        formulaPolicy="Native legacy metrics use the legacy three-span/swapped-keystone convention; V2 and OpenCV use opposite-edge means. Geometry versus reference, reported versus reference, and reported versus geometry are separate signed percentage-point differences.",
        timingPolicy="One un-warmed call per algorithm per image in one process, varying dimensions. Native time includes ABI processing + JSON byte copy + FreeResult, excludes parsing and image I/O. OpenCV time covers findCirclesGrid only. These are observations, not a controlled speed comparison or multiplier.")
    write_json(args.output / "environment.json", environment)
    cases = []
    algorithms = ("legacy", "v2") if args.skip_opencv else ("legacy", "v2", "opencvDefault")
    for index in range(1, CASE_COUNT + 1):
        image, reference, case = load_case(args.dataset, index)
        if case != manifest[index - 1]:
            raise RuntimeError(f"Dataset changed during the run: {case['name']}")
        case["algorithms"] = {}
        for algorithm in ("legacy", "v2"):
            record = native.run(algorithm, image, GRID_SIZE, GRID_SIZE, bright=False)
            record["comparison"] = compare_native(record, reference, image.shape, legacy=algorithm == "legacy")
            case["algorithms"][algorithm] = record
        if not args.skip_opencv:
            case["algorithms"]["opencvDefault"] = run_opencv(image, reference)
        cases.append(case)
        write_json(args.output / f"{case['name']}.json", case)
        print(case["name"], " ".join(f"{name}={case['algorithms'][name]['comparison']['status']}" for name in algorithms), flush=True)
    if sha256(args.dll) != environment["dllSha256"]:
        raise RuntimeError("DLL changed during the run; results cannot be attributed to one binary")
    summary = dict(caseCount=len(cases), dllSha256=environment["dllSha256"],
                   completedUtc=datetime.now(timezone.utc).isoformat(), algorithms=summarize(cases, algorithms),
                   referencePolicy=environment["referencePolicy"], timingPolicy=environment["timingPolicy"])
    write_json(args.output / "summary.json", summary)
    print(json.dumps({name: {key: value for key, value in result.items() if key != "rejected"}
                      for name, result in summary["algorithms"].items()}))


if __name__ == "__main__":
    main()
