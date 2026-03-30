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

_TAG_REASONS = {
    "插件生态": "插件体系与插件市场能力持续增强",
    "架构重构": "这一版包含明显的结构调整或模块重构",
    "更新机制": "更新链路、发布机制或下载流程出现了调整",
    "国际化": "多语言与国际化能力进入新的阶段",
    "通信能力": "设备通信或消息链路被强化",
    "光谱能力": "光谱相关能力在这一阶段被重点推进",
    "流程能力": "Flow / 模板 / 任务流程能力在这一阶段有明显迭代",
    "反馈闭环": "产品开始强化用户反馈与问题闭环能力",
}


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


def base_release_version(version: str) -> str:
    parts = [part for part in str(version).split(".") if part]
    if len(parts) >= 3:
        return ".".join(parts[:3])
    if len(parts) >= 2:
        return ".".join(parts[:2])
    return parts[0] if parts else ""


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


def _milestone_reason(entry: dict[str, Any], *, phase_changed: bool) -> tuple[str, str]:
    if phase_changed:
        return "阶段切换", f"进入 {entry['major_minor']} 阶段，版本演进进入新的主线。"
    tags = list(entry.get("tags", []))
    if "架构重构" in tags:
        return "架构重构", _TAG_REASONS["架构重构"]
    if "插件生态" in tags:
        return "插件生态", _TAG_REASONS["插件生态"]
    if entry.get("change_count", 0) >= 12:
        return "高密度更新", f"这一版累计 {entry['change_count']} 条变更，属于高密度迭代节点。"
    if tags:
        lead = tags[0]
        return lead, _TAG_REASONS.get(lead, f"这一版重点集中在 {lead}。")
    return "常规迭代", "这是一次常规的连续版本推进。"


def enrich_changelog_entries(entries: list[dict[str, Any]]) -> list[dict[str, Any]]:
    chronological = list(reversed(entries))
    previous_phase = ""
    for entry in chronological:
        phase_changed = bool(previous_phase and entry["major_minor"] != previous_phase)
        existing_milestone = bool(entry.get("is_milestone"))
        entry["phase_changed"] = phase_changed
        entry["is_milestone"] = existing_milestone or phase_changed or (
            entry.get("change_count", 0) >= 8 and len(entry.get("tags", [])) >= 2
        )
        label, reason = _milestone_reason(entry, phase_changed=phase_changed)
        entry["annotation_label"] = label
        entry["annotation_reason"] = reason
        previous_phase = entry["major_minor"]
    return entries


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
            "major_nodes": [],
            "story": [],
            "summary": {
                "release_count": 0,
                "avg_gap_days": 0,
                "latest_date": "暂无",
                "earliest_date": "暂无",
                "phase_count": 0,
                "current_phase": "暂无",
                "latest_major_node": None,
                "latest_milestone": None,
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
    major_nodes: list[dict[str, Any]] = []
    for entry in chronological:
        if entry.get("date"):
            monthly_counts[entry["date"].strftime("%Y-%m")] += 1
        phase = str(entry.get("major_minor", ""))
        if phase and phase not in phases:
            phases.append(phase)
            major_nodes.append(
                {
                    "major_minor": phase,
                    "version": entry["version"],
                    "date_display": entry["date_display"],
                    "annotation_label": entry.get("annotation_label", "阶段切换"),
                    "annotation_reason": entry.get("annotation_reason", ""),
                }
            )

    milestones = [
        {
            "version": entry["version"],
            "date_display": entry["date_display"],
            "change_count": entry["change_count"],
            "tags": entry.get("tags", []),
            "highlights": entry.get("highlights", []),
            "major_minor": entry.get("major_minor", ""),
            "annotation_label": entry.get("annotation_label", ""),
            "annotation_reason": entry.get("annotation_reason", ""),
        }
        for entry in sorted(
            milestone_candidates,
            key=lambda item: (item["weight"], item["sort_key"]),
            reverse=True,
        )[:6]
    ]

    latest_major_node = major_nodes[-1] if major_nodes else None
    latest_milestone = milestones[0] if milestones else None
    avg_gap_days = round(sum(gaps) / len(gaps), 1) if gaps else 0
    story = [
        f"从 {chronological[0]['date_display']} 到 {entries[0]['date_display']}，共记录 {len(entries)} 次发布，平均每 {avg_gap_days} 天一次。",
        (
            f"当前迭代主线处于 {entries[0]['major_minor']} 阶段，最近的阶段节点是 {latest_major_node['version']}。"
            if latest_major_node
            else "当前还没有识别到明确的阶段节点。"
        ),
    ]
    if latest_milestone:
        story.append(f"最近的重要节点是 {latest_milestone['version']}：{latest_milestone['annotation_reason']}")

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
        "major_nodes": major_nodes,
        "story": story,
        "summary": {
            "release_count": len(entries),
            "avg_gap_days": avg_gap_days,
            "latest_date": entries[0]["date_display"],
            "earliest_date": chronological[0]["date_display"],
            "phase_count": len(phases),
            "current_phase": entries[0]["major_minor"],
            "latest_major_node": latest_major_node,
            "latest_milestone": latest_milestone,
        },
    }


def analyze_changelog_text(text: str) -> dict[str, Any]:
    entries = enrich_changelog_entries(parse_changelog_entries(text))
    chart = build_iteration_chart(entries)
    return {
        "entries": entries,
        "chart": chart,
        "milestones": chart["milestones"],
        "major_nodes": chart["major_nodes"],
        "story": chart["story"],
        "summary": chart["summary"],
    }


def build_changelog_lookup(entries: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    exact: dict[str, dict[str, Any]] = {}
    branch_latest: dict[str, dict[str, Any]] = {}
    for entry in entries:
        version = str(entry.get("version", ""))
        branch = base_release_version(version)
        if version and version not in exact:
            exact[version] = entry
        if branch and branch not in branch_latest:
            branch_latest[branch] = entry
    return {"exact": exact, "branch": branch_latest}


def resolve_changelog_for_release_group(
    lookup: dict[str, dict[str, Any]],
    *,
    versions: list[str],
    branch: str,
) -> dict[str, Any] | None:
    exact = lookup.get("exact", {})
    for version in versions:
        if version in exact:
            return exact[version]
    branch_map = lookup.get("branch", {})
    return branch_map.get(branch)


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



