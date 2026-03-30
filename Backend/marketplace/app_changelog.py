from __future__ import annotations

import re
from collections import defaultdict
from datetime import date, datetime
from pathlib import Path
from typing import Any, Callable


GetCacheEntry = Callable[..., dict[str, Any] | None]
SetCacheEntry = Callable[..., None]

_HEADER_RE = re.compile(r"^##\s*\[(?P<version>[^]]+)\]\s+(?P<date>\d{4}[.-]\d{2}[.-]\d{2})\s*$")
_BULLET_RE = re.compile(r"^(?:[-*+]\s+|\d+[.)、．]\s*|\d+\.)")

_KEYWORD_TAGS = [
    ("插件", "插件生态"),
    ("Plugin", "插件生态"),
    ("更新", "更新机制"),
    ("重构", "架构重构"),
    ("多语言", "国际化"),
    ("MQTT", "通信能力"),
    ("光谱", "光谱能力"),
    ("Flow", "流程能力"),
    ("反馈", "反馈闭环"),
]


def changelog_signature(changelog_path: Path) -> str:
    try:
        stat = changelog_path.stat()
    except OSError:
        return "missing"
    return f"{stat.st_mtime_ns}:{stat.st_size}"


def _parse_date(raw: str) -> date | None:
    value = raw.strip().replace(".", "-")
    try:
        return datetime.strptime(value, "%Y-%m-%d").date()
    except ValueError:
        return None


def _major_minor(version: str) -> str:
    parts = [part for part in str(version).split(".") if part]
    if len(parts) >= 2:
        return ".".join(parts[:2])
    return parts[0] if parts else "未分组"


def _item_weight(text: str) -> int:
    lowered = text.lower()
    score = 1
    for keyword in ("重构", "新增", "增加", "优化", "支持", "修复", "更新", "插件", "feedback", "plugin"):
        if keyword in lowered or keyword in text:
            score += 1
    return score


def _summarize_tags(items: list[str]) -> list[str]:
    tags: list[str] = []
    joined = "\n".join(items)
    for keyword, label in _KEYWORD_TAGS:
        if keyword in joined and label not in tags:
            tags.append(label)
    return tags[:4]


def parse_changelog_entries(text: str) -> list[dict[str, Any]]:
    entries: list[dict[str, Any]] = []
    current: dict[str, Any] | None = None

    for raw_line in text.splitlines():
        line = raw_line.strip()
        header_match = _HEADER_RE.match(line)
        if header_match:
            if current:
                entries.append(current)
            version = header_match.group("version").strip()
            parsed_date = _parse_date(header_match.group("date"))
            current = {
                "version": version,
                "date": parsed_date,
                "date_display": parsed_date.strftime("%Y-%m-%d") if parsed_date else header_match.group("date"),
                "items": [],
            }
            continue

        if current is None or not line:
            continue

        if _BULLET_RE.match(line):
            cleaned = _BULLET_RE.sub("", line).strip()
            if cleaned and current is not None:
                current["items"].append(cleaned)

    if current:
        entries.append(current)

    for entry in entries:
        items = list(entry["items"])
        entry["change_count"] = len(items)
        entry["major_minor"] = _major_minor(str(entry["version"]))
        entry["weight"] = sum(_item_weight(item) for item in items)
        entry["tags"] = _summarize_tags(items)
        entry["highlights"] = items[:3]
        entry["is_milestone"] = entry["change_count"] >= 12 or any("重构" in item or "新增" in item for item in items)
        entry["sort_key"] = entry["date"].isoformat() if entry["date"] else ""
    entries.sort(key=lambda item: item["sort_key"], reverse=True)
    return entries


def build_iteration_chart(entries: list[dict[str, Any]]) -> dict[str, Any]:
    chronological = list(reversed(entries))
    if not chronological:
        return {
            "svg": {
                "width": 760,
                "height": 260,
                "max_changes": 0,
                "points": [],
                "path": "",
                "bars": [],
                "baseline_y": 178,
            },
            "monthly_counts": [],
            "milestones": [],
            "summary": {
                "release_count": 0,
                "avg_gap_days": 0,
                "latest_date": "暂无",
                "earliest_date": "暂无",
                "phase_count": 0,
            },
        }

    max_changes = max(max(int(entry.get("change_count", 0)), 1) for entry in chronological)
    release_count = len(chronological)
    width = 760
    height = 260
    padding_x = 42
    padding_top = 28
    usable_width = max(width - padding_x * 2, 1)
    usable_height = 150

    points: list[dict[str, Any]] = []
    path_commands: list[str] = []
    bars: list[dict[str, Any]] = []
    milestone_candidates: list[dict[str, Any]] = []
    gaps: list[int] = []

    last_date: date | None = None
    for index, entry in enumerate(chronological):
        x = padding_x + (usable_width * index / max(release_count - 1, 1))
        change_count = int(entry.get("change_count", 0))
        y = padding_top + usable_height - ((usable_height * max(change_count, 1)) / max_changes)
        point = {
            "version": entry["version"],
            "date_display": entry["date_display"],
            "change_count": change_count,
            "x": round(x, 2),
            "y": round(y, 2),
            "tags": entry.get("tags", []),
            "is_milestone": bool(entry.get("is_milestone")),
            "major_minor": entry.get("major_minor", ""),
        }
        points.append(point)
        path_commands.append(("M" if index == 0 else "L") + f" {point['x']} {point['y']}")
        bars.append(
            {
                "x": round(x - 8, 2),
                "y": round(y, 2),
                "width": 16,
                "height": round(padding_top + usable_height - y, 2),
                "version": entry["version"],
                "date_display": entry["date_display"],
                "change_count": change_count,
                "is_milestone": bool(entry.get("is_milestone")),
            }
        )

        if entry.get("is_milestone"):
            milestone_candidates.append(entry)

        if last_date and entry.get("date"):
            gaps.append((entry["date"] - last_date).days)
        if entry.get("date"):
            last_date = entry["date"]

    monthly_counts: dict[str, int] = defaultdict(int)
    phases: list[str] = []
    for entry in chronological:
        if entry.get("date"):
            monthly_counts[entry["date"].strftime("%Y-%m")] += 1
        phase = str(entry.get("major_minor", ""))
        if phase and phase not in phases:
            phases.append(phase)

    milestones = [
        {
            "version": entry["version"],
            "date_display": entry["date_display"],
            "change_count": entry["change_count"],
            "tags": entry.get("tags", []),
            "highlights": entry.get("highlights", []),
            "major_minor": entry.get("major_minor", ""),
        }
        for entry in sorted(
            milestone_candidates,
            key=lambda item: (item["weight"], item["sort_key"]),
            reverse=True,
        )[:6]
    ]

    return {
        "svg": {
            "width": width,
            "height": height,
            "max_changes": max_changes,
            "points": points,
            "path": " ".join(path_commands),
            "bars": bars,
            "baseline_y": padding_top + usable_height,
        },
        "monthly_counts": [
            {"month": month, "count": count}
            for month, count in sorted(monthly_counts.items())[-12:]
        ],
        "milestones": milestones,
        "summary": {
            "release_count": len(entries),
            "avg_gap_days": round(sum(gaps) / len(gaps), 1) if gaps else 0,
            "latest_date": entries[0]["date_display"],
            "earliest_date": chronological[0]["date_display"],
            "phase_count": len(phases),
        },
    }


def analyze_changelog_text(text: str) -> dict[str, Any]:
    entries = parse_changelog_entries(text)
    chart = build_iteration_chart(entries)
    return {
        "entries": entries,
        "chart": chart,
        "milestones": chart["milestones"],
        "summary": chart["summary"],
    }


def get_cached_changelog_analysis(
    changelog_path: Path,
    *,
    get_cache_entry: GetCacheEntry,
    set_cache_entry: SetCacheEntry,
    cache_key: str,
    ttl_seconds: int,
) -> dict[str, Any]:
    signature = changelog_signature(changelog_path)
    cached = get_cache_entry(cache_key, signature=signature)
    if cached:
        return cached["value"]

    try:
        text = changelog_path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        analysis = analyze_changelog_text("")
    else:
        analysis = analyze_changelog_text(text)

    set_cache_entry(cache_key, analysis, ttl_seconds=ttl_seconds, signature=signature)
    return analysis



