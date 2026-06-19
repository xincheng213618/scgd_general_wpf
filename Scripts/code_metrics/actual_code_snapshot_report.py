#!/usr/bin/env python3
"""Actual LOC snapshot report for scgd_general_wpf.

This script does not count git churn. It samples the repository at historical
commits and counts the actual maintained code lines present at each week/month
end, using the same "exclude docs" maintenance-oriented scope.
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import subprocess
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path


DEFAULT_REPO = Path(r"C:\Users\Xin\Desktop\scgd_general_wpf")
DEFAULT_OUT = Path(r"C:\Users\Xin\Desktop\scgd_general_wpf_code_snapshots")

CODE_EXTS = {
    ".cs",
    ".xaml",
    ".cpp",
    ".c",
    ".h",
    ".hpp",
    ".cc",
    ".cxx",
    ".py",
    ".ts",
    ".tsx",
    ".js",
    ".jsx",
    ".java",
    ".kt",
    ".xml",
    ".sql",
}

EXCLUDED_DIRS = {
    ".git",
    ".vs",
    ".idea",
    ".vscode",
    ".claude",
    ".github",
    "bin",
    "obj",
    "Debug",
    "Release",
    "x64",
    "x86",
    "node_modules",
    "packages",
    "DLL",
    "SDK",
    "docs",
}

GIT_CODE_PATHS = [f":(glob)**/*{ext}" for ext in sorted(CODE_EXTS)]
GIT_EXCLUDE_PATHS = [f":(exclude){name}/**" for name in sorted(EXCLUDED_DIRS) if name not in {".git"}]

PHASES = [
    ("before_2025_09", None, dt.date(2025, 9, 1)),
    ("2025_09_to_2026_03", dt.date(2025, 9, 1), dt.date(2026, 3, 1)),
    ("after_2026_03", dt.date(2026, 3, 1), None),
]


@dataclass
class Snapshot:
    period: str
    period_start: dt.date
    period_end: dt.date
    commit: str
    commit_date: dt.date
    code_files: int
    code_lines: int
    delta_lines: int = 0
    delta_percent: float = 0.0


def run_git(repo: Path, args: list[str], *, text: bool = True, input_data: bytes | None = None) -> str | bytes:
    result = subprocess.run(
        ["git", *args],
        cwd=repo,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=text,
        input=input_data,
    )
    return result.stdout


def parse_date(value: str) -> dt.date:
    return dt.date.fromisoformat(value.strip())


def first_last_dates(repo: Path) -> tuple[dt.date, dt.date]:
    first = run_git(repo, ["log", "--date=short", "--reverse", "--pretty=format:%ad"]).splitlines()[0]
    last = run_git(repo, ["log", "--date=short", "--pretty=format:%ad", "-1"]).strip()
    return parse_date(first), parse_date(last)


def week_start(day: dt.date) -> dt.date:
    return day - dt.timedelta(days=day.weekday())


def month_start(day: dt.date) -> dt.date:
    return dt.date(day.year, day.month, 1)


def next_month(day: dt.date) -> dt.date:
    if day.month == 12:
        return dt.date(day.year + 1, 1, 1)
    return dt.date(day.year, day.month + 1, 1)


def iter_weeks(first: dt.date, last: dt.date) -> list[tuple[dt.date, dt.date, str]]:
    periods: list[tuple[dt.date, dt.date, str]] = []
    start = week_start(first)
    while start <= last:
        end = start + dt.timedelta(days=6)
        periods.append((start, end, start.isoformat()))
        start = end + dt.timedelta(days=1)
    return periods


def iter_months(first: dt.date, last: dt.date) -> list[tuple[dt.date, dt.date, str]]:
    periods: list[tuple[dt.date, dt.date, str]] = []
    start = month_start(first)
    while start <= last:
        end = next_month(start) - dt.timedelta(days=1)
        periods.append((start, end, start.strftime("%Y-%m")))
        start = next_month(start)
    return periods


def is_code_path(path: str) -> bool:
    parts = path.replace("\\", "/").split("/")
    if any(part in EXCLUDED_DIRS for part in parts):
        return False
    if any("_wpftmp" in part for part in parts):
        return False
    return Path(path).suffix.lower() in CODE_EXTS


def latest_commit_before(repo: Path, end: dt.date) -> tuple[str, dt.date] | None:
    commit = run_git(repo, ["rev-list", "-1", f"--before={end.isoformat()} 23:59:59", "HEAD"]).strip()
    if not commit:
        return None
    commit_date = parse_date(run_git(repo, ["show", "-s", "--date=short", "--pretty=format:%ad", commit]).strip())
    return commit, commit_date


def tree_blobs(repo: Path, commit: str) -> list[str]:
    raw = run_git(repo, ["ls-tree", "-r", "-z", commit], text=False)
    blobs: list[str] = []
    for entry in raw.split(b"\x00"):
        if not entry:
            continue
        try:
            meta, path_b = entry.split(b"\t", 1)
            path = path_b.decode("utf-8", errors="replace")
            if not is_code_path(path):
                continue
            parts = meta.decode("ascii", errors="ignore").split()
            if len(parts) >= 3 and parts[1] == "blob":
                blobs.append(parts[2])
        except ValueError:
            continue
    return blobs


def tree_code_file_count(repo: Path, commit: str) -> int:
    raw = run_git(repo, ["ls-tree", "-r", "-z", commit], text=False)
    count = 0
    for entry in raw.split(b"\x00"):
        if not entry:
            continue
        try:
            _meta, path_b = entry.split(b"\t", 1)
            path = path_b.decode("utf-8", errors="replace")
            if is_code_path(path):
                count += 1
        except ValueError:
            continue
    return count


def count_lines(data: bytes) -> int:
    if not data:
        return 0
    return data.count(b"\n") + (0 if data.endswith(b"\n") else 1)


def hydrate_blob_cache(repo: Path, blobs: list[str], cache: dict[str, int]) -> None:
    missing = [blob for blob in sorted(set(blobs)) if blob not in cache]
    if not missing:
        return
    request = ("\n".join(missing) + "\n").encode("ascii")
    result = subprocess.run(
        ["git", "cat-file", "--batch"],
        cwd=repo,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        input=request,
        check=True,
    )
    data = result.stdout
    pos = 0
    for blob in missing:
        newline = data.find(b"\n", pos)
        if newline < 0:
            cache[blob] = 0
            break
        header = data[pos:newline]
        pos = newline + 1
        if not header:
            cache[blob] = 0
            continue
        header_parts = header.split()
        if len(header_parts) < 3 or header_parts[1] != b"blob":
            cache[blob] = 0
            continue
        size = int(header_parts[2])
        content = data[pos : pos + size]
        pos += size + 1  # trailing newline after each object
        cache[blob] = count_lines(content)


def snapshot_loc(repo: Path, commit: str, cache: dict[str, int]) -> tuple[int, int]:
    blobs = tree_blobs(repo, commit)
    hydrate_blob_cache(repo, blobs, cache)
    return len(blobs), sum(cache.get(blob, 0) for blob in blobs)


def diff_net_lines(repo: Path, old_commit: str, new_commit: str) -> int:
    if old_commit == new_commit:
        return 0
    output = run_git(
        repo,
        [
            "diff",
            "--numstat",
            "--no-renames",
            old_commit,
            new_commit,
            "--",
            *GIT_CODE_PATHS,
            *GIT_EXCLUDE_PATHS,
        ],
    )
    net = 0
    for line in output.splitlines():
        parts = line.split("\t")
        if len(parts) < 3 or parts[0] == "-" or parts[1] == "-":
            continue
        path = parts[2]
        if not is_code_path(path):
            continue
        net += int(parts[0]) - int(parts[1])
    return net


def build_snapshots(repo: Path, periods: list[tuple[dt.date, dt.date, str]], cache: dict[str, int]) -> list[Snapshot]:
    snapshots: list[Snapshot] = []
    previous_lines = 0
    for start, end, label in periods:
        latest = latest_commit_before(repo, end)
        if latest is None:
            continue
        commit, commit_date = latest
        files, lines = snapshot_loc(repo, commit, cache)
        delta = lines - previous_lines if snapshots else 0
        pct = (delta / previous_lines * 100.0) if previous_lines else 0.0
        snapshots.append(
            Snapshot(
                period=label,
                period_start=start,
                period_end=end,
                commit=commit[:10],
                commit_date=commit_date,
                code_files=files,
                code_lines=lines,
                delta_lines=delta,
                delta_percent=round(pct, 2),
            )
        )
        previous_lines = lines
    return snapshots


def write_snapshot_csv(path: Path, snapshots: list[Snapshot]) -> None:
    fields = [
        "period",
        "period_start",
        "period_end",
        "commit",
        "commit_date",
        "code_files",
        "code_lines",
        "delta_lines",
        "delta_percent",
    ]
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        for s in snapshots:
            writer.writerow({field: getattr(s, field) for field in fields})


def rows_for_phase(snapshots: list[Snapshot], start: dt.date | None, end: dt.date | None) -> list[Snapshot]:
    return [
        s
        for s in snapshots
        if (start is None or s.period_end >= start)
        and (end is None or s.period_end < end)
    ]


def phase_summary(monthly: list[Snapshot], weekly: list[Snapshot]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for name, start, end in PHASES:
        month_rows = rows_for_phase(monthly, start, end)
        week_rows = rows_for_phase(weekly, start, end) if weekly else []
        if not month_rows:
            continue
        first_index = monthly.index(month_rows[0])
        if first_index > 0:
            start_loc = monthly[first_index - 1].code_lines
        else:
            start_loc = month_rows[0].code_lines
        end_loc = month_rows[-1].code_lines
        growth = end_loc - start_loc
        active_week_deltas = [s.delta_lines for s in week_rows if s.delta_lines != 0]
        active_month_deltas = [s.delta_lines for s in month_rows if s.delta_lines != 0]
        rows.append(
            {
                "phase": name,
                "phase_start": start.isoformat() if start else "",
                "phase_end": end.isoformat() if end else "",
                "months": len(month_rows),
                "weeks": len(week_rows),
                "start_loc": start_loc,
                "end_loc": end_loc,
                "loc_growth": growth,
                "growth_percent": round(growth / start_loc * 100.0, 2) if start_loc else 0.0,
                "avg_monthly_delta": round(sum(active_month_deltas) / len(active_month_deltas), 1) if active_month_deltas else 0,
                "avg_weekly_delta": round(sum(active_week_deltas) / len(active_week_deltas), 1) if active_week_deltas else 0,
            }
        )
    return rows


def write_dict_csv(path: Path, rows: list[dict[str, object]]) -> None:
    if not rows:
        return
    fields = list(rows[0].keys())
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def latest_breakdowns(repo: Path, commit: str, cache: dict[str, int]) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    raw = run_git(repo, ["ls-tree", "-r", "-z", commit], text=False)
    by_top: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    by_ext: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    blobs_by_path: list[tuple[str, str]] = []
    for entry in raw.split(b"\x00"):
        if not entry:
            continue
        try:
            meta, path_b = entry.split(b"\t", 1)
            path = path_b.decode("utf-8", errors="replace")
            if not is_code_path(path):
                continue
            parts = meta.decode("ascii", errors="ignore").split()
            if len(parts) >= 3 and parts[1] == "blob":
                blobs_by_path.append((path, parts[2]))
        except ValueError:
            continue
    hydrate_blob_cache(repo, [blob for _, blob in blobs_by_path], cache)
    for path, blob in blobs_by_path:
        line_count = cache.get(blob, 0)
        top = path.replace("\\", "/").split("/")[0] if "/" in path.replace("\\", "/") else "<root>"
        ext = Path(path).suffix.lower()
        by_top[top][0] += 1
        by_top[top][1] += line_count
        by_ext[ext][0] += 1
        by_ext[ext][1] += line_count
    top_rows = [
        {"top_dir": key, "code_files": value[0], "code_lines": value[1]}
        for key, value in sorted(by_top.items(), key=lambda item: item[1][1], reverse=True)
    ]
    ext_rows = [
        {"extension": key, "code_files": value[0], "code_lines": value[1]}
        for key, value in sorted(by_ext.items(), key=lambda item: item[1][1], reverse=True)
    ]
    return top_rows, ext_rows


def ratio(new: float, old: float) -> str:
    if old == 0:
        return "n/a"
    return f"{new / old:.2f}x"


def markdown_table(headers: list[str], rows: list[list[object]]) -> str:
    lines = [
        "| " + " | ".join(headers) + " |",
        "| " + " | ".join(["---"] * len(headers)) + " |",
    ]
    for row in rows:
        lines.append("| " + " | ".join(str(item) for item in row) + " |")
    return "\n".join(lines)


def write_report(
    path: Path,
    repo: Path,
    first: dt.date,
    last: dt.date,
    weekly: list[Snapshot],
    monthly: list[Snapshot],
    phases: list[dict[str, object]],
) -> None:
    latest = monthly[-1]
    top_months = sorted(monthly[1:], key=lambda s: s.delta_lines, reverse=True)[:12]
    top_weeks = sorted(weekly[1:], key=lambda s: s.delta_lines, reverse=True)[:12] if weekly else []
    phase_map = {row["phase"]: row for row in phases}
    before = phase_map.get("before_2025_09", {})
    mid = phase_map.get("2025_09_to_2026_03", {})
    after = phase_map.get("after_2026_03", {})
    after_vs_before_weekly = ratio(float(after.get("avg_weekly_delta", 0)), float(before.get("avg_weekly_delta", 0)))
    after_vs_mid_weekly = ratio(float(after.get("avg_weekly_delta", 0)), float(mid.get("avg_weekly_delta", 0)))
    after_vs_before_monthly = ratio(float(after.get("avg_monthly_delta", 0)), float(before.get("avg_monthly_delta", 0)))
    after_vs_mid_monthly = ratio(float(after.get("avg_monthly_delta", 0)), float(mid.get("avg_monthly_delta", 0)))

    phase_rows = [
        [
            row["phase"],
            f'{row["start_loc"]:,}',
            f'{row["end_loc"]:,}',
            f'{row["loc_growth"]:,}',
            f'{row["growth_percent"]}%',
            f'{row["avg_monthly_delta"]:,}',
            f'{row["avg_weekly_delta"]:,}',
        ]
        for row in phases
    ]
    month_rows = [
        [s.period, s.commit_date, f"{s.code_lines:,}", f"{s.delta_lines:,}", f"{s.delta_percent}%"]
        for s in top_months
    ]
    week_rows = [
        [s.period_start, s.period_end, s.commit_date, f"{s.code_lines:,}", f"{s.delta_lines:,}", f"{s.delta_percent}%"]
        for s in top_weeks
    ]
    week_section = (
        "## Top Weeks By Actual LOC Growth\n\n"
        + markdown_table(["week start", "week end", "sample commit date", "LOC", "delta", "delta %"], week_rows)
        if weekly
        else "## Weekly Snapshot\n\nWeekly snapshots were skipped in this run. Re-run with `--weekly` if needed."
    )
    content = f"""# scgd_general_wpf Actual Code Snapshot Report

Generated: {dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")}

Repository: `{repo}`

History window: `{first}` to `{last}`

## Counting Rules

- This report counts actual code present at each sampled commit.
- It does **not** count git-added/deleted churn.
- Weekly rows are sampled at week end; monthly rows are sampled at month end.
- Excluded directories: `{", ".join(sorted(EXCLUDED_DIRS))}`.
- Included extensions: `{", ".join(sorted(CODE_EXTS))}`.
- This is the same maintenance-oriented scope as "exclude docs".

## Headline

- Latest month-end LOC: **{latest.code_lines:,}** across **{latest.code_files:,}** code files.
- Latest sampled commit: `{latest.commit}` on `{latest.commit_date}`.
- Avg monthly LOC growth after 2026-03 vs before 2025-09: **{after_vs_before_monthly}**.
- Avg monthly LOC growth after 2026-03 vs 2025-09 to 2026-03: **{after_vs_mid_monthly}**.
- Avg weekly LOC growth ratios are **{after_vs_before_weekly}** / **{after_vs_mid_weekly}** when weekly snapshots are enabled.

## Phase Summary

{markdown_table(["phase", "start LOC", "end LOC", "growth", "growth %", "avg monthly delta", "avg weekly delta"], phase_rows)}

## Top Months By Actual LOC Growth

{markdown_table(["month", "sample commit date", "LOC", "delta", "delta %"], month_rows)}

{week_section}

## Reading Notes

- `delta` means difference between this snapshot and the previous snapshot.
- A negative `delta` means the codebase became smaller at that sample point.
- This is better for "how large the maintained system became over time"; churn is better for "how much editing activity happened".
"""
    path.write_text(content, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=DEFAULT_REPO)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--weekly", action="store_true", help="Also generate weekly snapshots. This is slower on Windows.")
    args = parser.parse_args()

    repo = args.repo
    out = args.out
    out.mkdir(parents=True, exist_ok=True)

    first, last = first_last_dates(repo)
    cache: dict[str, int] = {}
    monthly = build_snapshots(repo, iter_months(first, last), cache)
    weekly = build_snapshots(repo, iter_weeks(first, last), cache) if args.weekly else []
    phases = phase_summary(monthly, weekly)

    write_snapshot_csv(out / "monthly_actual_code_snapshot.csv", monthly)
    if weekly:
        write_snapshot_csv(out / "weekly_actual_code_snapshot.csv", weekly)
    write_dict_csv(out / "phase_actual_code_summary.csv", phases)

    if monthly:
        latest_commit = monthly[-1].commit
        full_commit = run_git(repo, ["rev-parse", latest_commit]).strip()
        top_rows, ext_rows = latest_breakdowns(repo, full_commit, cache)
        write_dict_csv(out / "latest_top_dir_breakdown.csv", top_rows)
        write_dict_csv(out / "latest_extension_breakdown.csv", ext_rows)
        write_report(out / "actual_code_snapshot_report.md", repo, first, last, weekly, monthly, phases)

    print(f"Wrote snapshot reports to: {out}")
    print(f"Monthly snapshots: {len(monthly)}")
    print(f"Weekly snapshots: {len(weekly)}")
    if monthly:
        print(f"Latest LOC: {monthly[-1].code_lines}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
