"""Validation and frozen scoring for shared host benchmark results."""

from __future__ import annotations

import base64
import hashlib
import hmac
import ipaddress
import json
import math
import re
from dataclasses import dataclass
from datetime import datetime, timezone
from statistics import median
from uuid import UUID


WORKLOAD_VERSION = "cv-host-cpu-memory-v1"
SCORING_VERSION = "cv-host-score-v1"
REFERENCE_NAME = "XIN"
SAMPLE_COUNT = 21
PUBLIC_COMPARISON_LIMIT = 12
BENCHMARK_TOKEN = re.compile(r"cvbench_([A-Za-z0-9_-]{43})")
SHARE_MIN_SAMPLE_MS = 0.5
SHARE_MAX_SAMPLE_MS = 120_000
SHARE_MAX_METRIC_SCORE = 2_000
REFERENCE_MEDIANS = {
    "memory-copy": 16.45815,
    "channel-swap": 25.3534,
    "bayer-rg16": 17.43975,
    "pixel-compute": 23.73725,
}
BENCHMARK_TIMESTAMP = re.compile(
    r"(?P<seconds>[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2})"
    r"(?:\.(?P<fraction>[0-9]{1,7}))?(?P<offset>Z|[+-][0-9]{2}:[0-9]{2})"
)


@dataclass(frozen=True)
class BenchmarkSubmission:
    device_id: str
    device_name: str
    run: dict
    content_hash: str
    publish_comparison: bool = False


def parse_benchmark_submission(body: object) -> BenchmarkSubmission:
    if not isinstance(body, dict):
        raise ValueError("A JSON object is required")
    _check_integer(body.get("schemaVersion"), "schemaVersion", 1, 1)
    publish_comparison = body.get("publishComparison", False)
    if type(publish_comparison) is not bool:
        raise ValueError("publishComparison must be a boolean")
    device_id = parse_benchmark_id(body.get("deviceId"), "deviceId")
    device_name = _checked_text(body.get("deviceName"), "deviceName", 80)
    raw_run = body.get("run")
    if not isinstance(raw_run, dict):
        raise ValueError("run must be an object")
    if raw_run.get("workloadVersion") != WORKLOAD_VERSION:
        raise ValueError("Unsupported workloadVersion")

    metrics = _parse_metrics(raw_run.get("metrics"))
    run = {
        "id": parse_benchmark_id(raw_run.get("id"), "run.id"),
        "workloadVersion": WORKLOAD_VERSION,
        "measuredAtUtc": _checked_time(raw_run.get("measuredAtUtc"), "measuredAtUtc"),
        "appStartedAtUtc": _checked_time(raw_run.get("appStartedAtUtc"), "appStartedAtUtc"),
        "approximateSystemStartedAtUtc": _checked_time(raw_run.get("approximateSystemStartedAtUtc"), "approximateSystemStartedAtUtc"),
        "appVersion": _checked_text(raw_run.get("appVersion"), "appVersion", 64, allow_empty=True),
        "runtime": _checked_text(raw_run.get("runtime"), "runtime", 120, allow_empty=True),
        "logicalProcessors": _check_integer(raw_run.get("logicalProcessors"), "logicalProcessors", 1, 2**31 - 1),
        "availableMemoryMiBBefore": _check_integer(raw_run.get("availableMemoryMiBBefore"), "availableMemoryMiBBefore", -1, 2**31 - 1),
        "frameWidth": _check_integer(raw_run.get("frameWidth"), "frameWidth", 9568, 9568),
        "frameHeight": _check_integer(raw_run.get("frameHeight"), "frameHeight", 6380, 6380),
    }
    _add_hardware_metadata(raw_run, run)

    # Only hash the accepted input, with a stable metric order. Server scoring can
    # evolve independently of retries and an older run's saved response.
    hash_run = {
        **run, "metrics": [{"id": item["id"], "samplesMilliseconds": item["samplesMilliseconds"]} for item in metrics],
    }
    identity = {"deviceId": device_id, "deviceName": device_name, "run": hash_run}
    encoded = json.dumps(identity, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")
    run.update(
        scoringVersion=SCORING_VERSION,
        metrics=metrics,
        overallScore=math.exp(sum(math.log(item["score"]) for item in metrics) / len(metrics)),
    )
    return BenchmarkSubmission(device_id, device_name, run, hashlib.sha256(encoded).hexdigest(), publish_comparison)


def benchmark_token_hash(token: str) -> str | None:
    match = BENCHMARK_TOKEN.fullmatch(token)
    if match is None:
        return None
    encoded = match[1].encode("ascii")
    decoded = base64.urlsafe_b64decode(encoded + b"=")
    # Check the final two padding bits so a credential has one canonical form.
    if len(decoded) != 32 or base64.urlsafe_b64encode(decoded).rstrip(b"=") != encoded:
        return None
    return hashlib.sha256(decoded).hexdigest()


def benchmark_source_hash(remote_address: str | None, secret: str | bytes) -> str:
    try:
        address = ipaddress.ip_address(remote_address or "")
        if isinstance(address, ipaddress.IPv6Address):
            address = address.ipv4_mapped or ipaddress.IPv6Network((address, 64), strict=False).network_address
        network = address.packed
    except ValueError:
        network = b"unknown"
    key = secret.encode("utf-8") if isinstance(secret, str) else secret
    return hmac.digest(key, b"benchmark-source" + network, "sha256").hex()


def check_benchmark_plausibility(submission: BenchmarkSubmission) -> None:
    for metric in submission.run["metrics"]:
        if metric["score"] > SHARE_MAX_METRIC_SCORE or any(
            sample < SHARE_MIN_SAMPLE_MS or sample > SHARE_MAX_SAMPLE_MS for sample in metric["samplesMilliseconds"]
        ):
            raise ValueError("Benchmark samples are outside the supported sharing range")


def public_benchmark_profile(run: dict, overall_score: float) -> dict:
    return {
        "label": "共享设备",
        "cpuModel": run.get("cpuModel"),
        "gpuModels": run.get("gpuModels", []),
        "totalMemoryMiB": run.get("totalMemoryMiB"),
        "overallScore": overall_score,
    }


def example_benchmark_profiles() -> list[dict]:
    return [
        {"label": "合成示例 A", "cpuModel": "Intel Core i5-12400", "gpuModels": ["Intel UHD 730"],
         "totalMemoryMiB": 16_384, "overallScore": 75},
        {"label": "合成示例 B", "cpuModel": "AMD Ryzen 7 7700", "gpuModels": ["NVIDIA GeForce RTX 4060"],
         "totalMemoryMiB": 32_768, "overallScore": 105},
        {"label": "合成示例 C", "cpuModel": "Intel Core i9-14900K", "gpuModels": ["NVIDIA GeForce RTX 4080"],
         "totalMemoryMiB": 65_536, "overallScore": 140},
    ]


def parse_benchmark_id(value: object, field: str) -> str:
    if not isinstance(value, str) or len(value) not in (32, 36):
        raise ValueError(f"{field} must be a UUID")
    try:
        identifier = UUID(value)
    except ValueError as exc:
        raise ValueError(f"{field} must be a UUID") from exc
    if identifier.int == 0:
        raise ValueError(f"{field} must be a nonzero UUID")
    return str(identifier)


def _checked_text(value: object, field: str, maximum: int, *, allow_empty: bool = False) -> str:
    if not isinstance(value, str) or len(value) > maximum or (value and not value.isprintable()):
        raise ValueError(f"{field} must be text no longer than {maximum} characters")
    text = value.strip()
    if not text and not allow_empty:
        raise ValueError(f"{field} must not be empty")
    return text


def _check_integer(value: object, field: str, minimum: int, maximum: int) -> int:
    if type(value) is not int or not minimum <= value <= maximum:
        raise ValueError(f"{field} must be an integer between {minimum} and {maximum}")
    return value


def _checked_time(value: object, field: str) -> str:
    match = BENCHMARK_TIMESTAMP.fullmatch(value) if isinstance(value, str) and len(value) <= 40 else None
    if match is None:
        raise ValueError(f"{field} must be an ISO 8601 timestamp with a timezone")
    # DateTimeOffset can emit seven fractional digits. Python 3.10 accepts
    # three or six, and storage uses six-digit UTC timestamps for sorting.
    fraction = (match["fraction"] or "").ljust(6, "0")[:6]
    offset = "+00:00" if match["offset"] == "Z" else match["offset"]
    try:
        parsed = datetime.fromisoformat(f"{match['seconds']}.{fraction}{offset}")
        return parsed.astimezone(timezone.utc).isoformat(timespec="microseconds")
    except (ValueError, OverflowError) as exc:
        raise ValueError(f"{field} must be an ISO 8601 timestamp with a timezone") from exc


def _add_hardware_metadata(raw_run: dict, run: dict) -> None:
    if raw_run.get("cpuModel") is not None:
        run["cpuModel"] = _checked_text(raw_run["cpuModel"], "cpuModel", 160, allow_empty=True)
    if raw_run.get("gpuModels") is not None:
        gpus = raw_run["gpuModels"]
        if not isinstance(gpus, list) or len(gpus) > 16:
            raise ValueError("gpuModels must be an array of up to 16 model names")
        run["gpuModels"] = [_checked_text(gpu, "gpuModels", 160) for gpu in gpus]
    if raw_run.get("totalMemoryMiB") is not None:
        run["totalMemoryMiB"] = _check_integer(raw_run["totalMemoryMiB"], "totalMemoryMiB", 1, 2**31 - 1)


def _parse_metrics(value: object) -> list[dict]:
    if not isinstance(value, list) or len(value) != len(REFERENCE_MEDIANS):
        raise ValueError("metrics must contain the four host benchmark workloads")
    metrics = {}
    for entry in value:
        metric_id = entry.get("id") if isinstance(entry, dict) else None
        if not isinstance(metric_id, str) or metric_id not in REFERENCE_MEDIANS or metric_id in metrics:
            raise ValueError("metrics must contain each known workload exactly once")
        samples = entry.get("samplesMilliseconds")
        if not isinstance(samples, list) or len(samples) != SAMPLE_COUNT:
            raise ValueError(f"samplesMilliseconds must contain {SAMPLE_COUNT} timings per workload")
        samples = [_checked_sample(sample) for sample in samples]
        ordered = sorted(samples)
        midpoint = median(ordered)
        reference = REFERENCE_MEDIANS[metric_id]
        ratio = midpoint / reference
        if ratio <= 0 or not math.isfinite(ratio):
            raise ValueError("samplesMilliseconds produces an invalid time ratio")
        score = 100 / ratio
        if score <= 0 or not math.isfinite(score):
            raise ValueError("samplesMilliseconds produces an invalid score")
        metrics[metric_id] = {
            "id": metric_id,
            "samplesMilliseconds": samples,
            "medianMilliseconds": midpoint,
            "p95Milliseconds": ordered[math.ceil(SAMPLE_COUNT * 0.95) - 1],
            "referenceMilliseconds": reference,
            "score": score,
            "timeRatio": ratio,
        }
    return [metrics[metric_id] for metric_id in REFERENCE_MEDIANS]


def _checked_sample(value: object) -> float:
    if type(value) not in (int, float):
        raise ValueError("samplesMilliseconds must contain positive finite numbers")
    try:
        sample = float(value)
    except OverflowError as exc:
        raise ValueError("samplesMilliseconds must contain positive finite numbers") from exc
    if sample <= 0 or not math.isfinite(sample):
        raise ValueError("samplesMilliseconds must contain positive finite numbers")
    return sample
