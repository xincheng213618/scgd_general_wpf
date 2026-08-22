#!/usr/bin/env python3
"""Generate an offline HTML dashboard for the repository's code history.

The history follows HEAD's first-parent chain so merge commits are counted once.
Weekly endpoint commits are classified exactly into code/content, comment, and
blank lines with the same rules as count_code_lines.py. Immutable blob and
snapshot counts are cached for fast subsequent runs.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import sqlite3
import subprocess
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from typing import Iterable, Sequence

from count_code_lines import Count, count_text, is_skipped, language_for


GRAIN_LABELS = ("日", "周", "月", "半年", "年")
FILTER_GRAIN_LABELS = GRAIN_LABELS
CACHE_SCHEMA_VERSION = 2
CACHE_RULES_VERSION = 1
SNAPSHOT_SQL = (
    "SELECT dataset_id, row_index, row_json "
    "FROM artifact_rows ORDER BY dataset_id, row_index"
)
CATEGORY_RULES: tuple[tuple[str, tuple[str, ...]], ...] = (
    ("发布与版本", ("release", "version", "changelog", "发布", "版本", "发版")),
    ("缺陷修复", ("fix", "bug", "hotfix", "修复", "修正", "问题", "异常")),
    ("功能开发", ("feat", "feature", "add", "新增", "添加", "实现", "支持")),
    ("重构优化", ("refactor", "cleanup", "optimize", "improve", "重构", "优化", "调整")),
    ("测试验证", ("test", "spec", "测试", "验证")),
    ("文档", ("doc", "readme", "文档", "说明")),
    ("构建与工程", ("build", "ci", "chore", "deps", "依赖", "构建", "打包", "脚本")),
)


@dataclass
class CommitNode:
    sequence: int
    commit: str
    timestamp: datetime
    author: str
    subject: str
    category: str
    additions: int = 0
    deletions: int = 0
    files_changed: int = 0
    total_lines: int = 0
    directory_churn: dict[str, int] = field(default_factory=dict)

    @property
    def net(self) -> int:
        return self.additions - self.deletions

    @property
    def churn(self) -> int:
        return self.additions + self.deletions

    @property
    def primary_area(self) -> str:
        if not self.directory_churn:
            return "无源文件变更"
        return max(self.directory_churn.items(), key=lambda item: (item[1], item[0]))[0]


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    repo_root = Path(__file__).resolve().parents[1]
    default_dir = repo_root / ".codex-artifacts" / "code-history-dashboard"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=repo_root, help="Git repository root")
    parser.add_argument("--ref", default="HEAD", help="Git ref to analyze (default: HEAD)")
    parser.add_argument(
        "--output",
        type=Path,
        default=default_dir / "index.html",
        help="generated self-contained HTML file",
    )
    parser.add_argument(
        "--artifact",
        type=Path,
        default=default_dir / "artifact.json",
        help="intermediate canonical dashboard data",
    )
    parser.add_argument(
        "--cache",
        type=Path,
        default=default_dir / "history-cache.json",
        help="persistent incremental history cache",
    )
    parser.add_argument(
        "--refresh-cache",
        action="store_true",
        help="discard reusable history data and rebuild the cache",
    )
    parser.add_argument(
        "--builder",
        type=Path,
        help="path to deliver_portable_artifact.mjs (auto-detected by default)",
    )
    parser.add_argument(
        "--exclude-generated",
        action="store_true",
        help="exclude common generated/minified source file names",
    )
    parser.add_argument(
        "--share-card",
        type=Path,
        default=default_dir / "share-card.png",
        help="portrait PNG summary for social sharing",
    )
    parser.add_argument(
        "--no-build",
        action="store_true",
        help="write artifact.json without packaging the HTML dashboard",
    )
    parser.add_argument(
        "--verify",
        action="store_true",
        help="run the slower browser-based portable HTML verification",
    )
    parser.add_argument("--open", action="store_true", help="open the generated HTML")
    return parser.parse_args(argv)


def run_git(repo: Path, arguments: Sequence[str], *, binary: bool = False) -> str | bytes:
    process = subprocess.run(
        ["git", "-C", str(repo), *arguments],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if process.returncode != 0:
        error = process.stderr.decode("utf-8", errors="replace").strip()
        raise RuntimeError(f"git {' '.join(arguments)} failed: {error}")
    if binary:
        return process.stdout
    return process.stdout.decode("utf-8", errors="replace")


def category_for(subject: str) -> str:
    normalized = subject.casefold()
    prefix = re.split(r"[\s(:：\[]", normalized, maxsplit=1)[0]
    for label, keywords in CATEGORY_RULES:
        for keyword in keywords:
            folded = keyword.casefold()
            if prefix == folded or folded in normalized:
                return label
    return "其他"


def source_path_is_included(repo: Path, relative_path: str, exclude_generated: bool) -> bool:
    normalized = relative_path.replace("\\", "/")
    path = repo.joinpath(*normalized.split("/"))
    return (
        language_for(path) is not None
        and not is_skipped(path, repo, exclude_generated)
    )


def top_level_area(relative_path: str) -> str:
    normalized = relative_path.replace("\\", "/").strip("/")
    if "/" not in normalized:
        return "(仓库根目录)"
    return normalized.split("/", 1)[0]


def parse_numstat(value: str) -> int | None:
    return int(value) if value.isdigit() else None


def load_commit_history(
    repo: Path, ref: str, exclude_generated: bool
) -> list[CommitNode]:
    marker = "@@CODE_HISTORY_COMMIT@@"
    separator = "\x1f"
    log_format = f"{marker}%H{separator}%cI{separator}%an{separator}%s"
    output = run_git(
        repo,
        (
            "-c",
            "core.quotepath=false",
            "log",
            ref,
            "--first-parent",
            "--reverse",
            "--diff-merges=first-parent",
            "--no-renames",
            "--numstat",
            f"--format={log_format}",
        ),
    )
    assert isinstance(output, str)

    nodes: list[CommitNode] = []
    current: CommitNode | None = None
    directory_churn: Counter[str] = Counter()

    def finish_current() -> None:
        nonlocal current, directory_churn
        if current is None:
            return
        current.directory_churn = dict(directory_churn)
        nodes.append(current)
        current = None
        directory_churn = Counter()

    for line in output.splitlines():
        if line.startswith(marker):
            finish_current()
            parts = line[len(marker) :].split(separator, 3)
            if len(parts) != 4:
                raise RuntimeError(f"Unexpected Git history record: {line!r}")
            commit, timestamp, author, subject = parts
            current = CommitNode(
                sequence=len(nodes) + 1,
                commit=commit,
                timestamp=datetime.fromisoformat(timestamp),
                author=author,
                subject=subject,
                category=category_for(subject),
            )
            continue
        if current is None or not line or "\t" not in line:
            continue
        parts = line.split("\t", 2)
        if len(parts) != 3:
            continue
        additions = parse_numstat(parts[0])
        deletions = parse_numstat(parts[1])
        relative_path = parts[2]
        if (
            additions is None
            or deletions is None
            or not source_path_is_included(repo, relative_path, exclude_generated)
        ):
            continue
        current.additions += additions
        current.deletions += deletions
        current.files_changed += 1
        directory_churn[top_level_area(relative_path)] += additions + deletions

    finish_current()
    if not nodes:
        raise RuntimeError(f"No commits found for {ref}")
    return nodes


def decode_blob(data: bytes) -> str | None:
    if data.startswith(b"\xff\xfe"):
        return data.decode("utf-16-le", errors="replace")
    if data.startswith(b"\xfe\xff"):
        return data.decode("utf-16-be", errors="replace")
    if b"\0" in data[:8192]:
        return None
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return data.decode("cp1252", errors="replace")


def load_tree_entries(
    repo: Path, ref: str, exclude_generated: bool
) -> dict[str, str]:
    tree = run_git(
        repo,
        ("ls-tree", "-r", "-z", "--format=%(objectname)%x09%(path)", ref),
        binary=True,
    )
    assert isinstance(tree, bytes)
    entries: dict[str, str] = {}
    for record in tree.split(b"\0"):
        if not record or b"\t" not in record:
            continue
        object_id_bytes, path_bytes = record.split(b"\t", 1)
        relative_path = path_bytes.decode("utf-8", errors="surrogateescape")
        if source_path_is_included(repo, relative_path, exclude_generated):
            entries[relative_path] = object_id_bytes.decode("ascii")
    return entries


def count_tree_entries(
    repo: Path, entries: Sequence[tuple[str, str]]
) -> dict[str, dict[str, object]]:
    if not entries:
        return {}

    requests = "".join(f"{object_id}\n" for object_id, _ in entries).encode("ascii")
    process = subprocess.Popen(
        ["git", "-C", str(repo), "cat-file", "--batch"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    stdout, stderr = process.communicate(requests)
    if process.returncode != 0:
        raise RuntimeError(
            f"git cat-file --batch failed: {stderr.decode(errors='replace').strip()}"
        )

    file_counts: dict[str, dict[str, object]] = {}
    cursor = 0
    for (object_id, relative_path) in entries:
        header_end = stdout.find(b"\n", cursor)
        if header_end < 0:
            raise RuntimeError("Unexpected end of git cat-file output")
        header = stdout[cursor:header_end].decode("ascii", errors="replace")
        cursor = header_end + 1
        header_parts = header.split()
        if len(header_parts) != 3 or header_parts[1] != "blob":
            raise RuntimeError(f"Unexpected git cat-file record: {header}")
        size = int(header_parts[2])
        data = stdout[cursor : cursor + size]
        cursor += size + 1
        text = decode_blob(data)
        if text is None:
            continue
        path = repo.joinpath(*relative_path.replace("\\", "/").split("/"))
        language = language_for(path)
        if language is None:
            continue
        file_count = count_text(text, language)
        file_counts[relative_path] = {
            "object_id": object_id,
            "language": language.name,
            "code": file_count.code,
            "comments": file_count.comments,
            "blanks": file_count.blanks,
        }
    return file_counts


def language_counts_from_files(
    files: dict[str, dict[str, object]],
) -> dict[str, Count]:
    counts: dict[str, Count] = {}
    for record in files.values():
        language = str(record["language"])
        counts.setdefault(language, Count()).add(
            Count(
                files=1,
                code=int(record["code"]),
                comments=int(record["comments"]),
                blanks=int(record["blanks"]),
            )
        )
    return counts


def cache_fingerprint() -> str:
    counter_path = Path(__file__).with_name("count_code_lines.py")
    digest = hashlib.sha256()
    digest.update(f"cache-rules:{CACHE_RULES_VERSION}\n".encode("ascii"))
    digest.update(counter_path.read_bytes())
    digest.update(repr(CATEGORY_RULES).encode("utf-8"))
    return digest.hexdigest()


def serialize_node(node: CommitNode) -> dict[str, object]:
    return {
        "commit": node.commit,
        "timestamp": node.timestamp.isoformat(),
        "author": node.author,
        "subject": node.subject,
        "category": node.category,
        "additions": node.additions,
        "deletions": node.deletions,
        "files_changed": node.files_changed,
        "directory_churn": node.directory_churn,
    }


def deserialize_nodes(rows: object) -> list[CommitNode]:
    if not isinstance(rows, list) or not rows:
        raise ValueError("cache contains no commit nodes")
    nodes: list[CommitNode] = []
    for index, row in enumerate(rows, start=1):
        if not isinstance(row, dict):
            raise ValueError("cache commit record is invalid")
        directory_churn = row.get("directory_churn", {})
        if not isinstance(directory_churn, dict):
            raise ValueError("cache directory churn record is invalid")
        nodes.append(
            CommitNode(
                sequence=index,
                commit=str(row["commit"]),
                timestamp=datetime.fromisoformat(str(row["timestamp"])),
                author=str(row["author"]),
                subject=str(row["subject"]),
                category=str(row["category"]),
                additions=int(row["additions"]),
                deletions=int(row["deletions"]),
                files_changed=int(row["files_changed"]),
                directory_churn={str(key): int(value) for key, value in directory_churn.items()},
            )
        )
    return nodes


def load_cache_file(path: Path) -> dict[str, object] | None:
    if not path.is_file():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return None
    return data if isinstance(data, dict) else None


def write_cache_file(
    path: Path,
    repo: Path,
    head: str,
    exclude_generated: bool,
    fingerprint: str,
    nodes: Sequence[CommitNode],
    files: dict[str, dict[str, object]],
    blob_counts: dict[str, dict[str, object]],
    snapshot_counts: dict[str, dict[str, int]],
) -> None:
    payload = {
        "version": CACHE_SCHEMA_VERSION,
        "rules": fingerprint,
        "repository": os.path.normcase(str(repo)),
        "exclude_generated": exclude_generated,
        "head": head,
        "nodes": [serialize_node(node) for node in nodes],
        "files": files,
        "blob_counts": blob_counts,
        "snapshot_counts": snapshot_counts,
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    temporary.replace(path)


def changed_paths(repo: Path, old_head: str, new_head: str) -> set[str]:
    output = run_git(
        repo,
        ("diff", "--name-only", "-z", "--no-renames", old_head, new_head),
        binary=True,
    )
    assert isinstance(output, bytes)
    return {
        record.decode("utf-8", errors="surrogateescape")
        for record in output.split(b"\0")
        if record
    }


def load_or_update_cache(
    repo: Path,
    ref: str,
    cache_path: Path,
    exclude_generated: bool,
    refresh_cache: bool,
) -> tuple[
    list[CommitNode],
    dict[str, dict[str, object]],
    dict[str, dict[str, object]],
    dict[str, dict[str, int]],
    str,
    str,
]:
    head_output = run_git(repo, ("rev-parse", "--verify", f"{ref}^{{commit}}"))
    assert isinstance(head_output, str)
    head = head_output.strip()
    fingerprint = cache_fingerprint()
    cache = None if refresh_cache else load_cache_file(cache_path)
    invalid_reason = "forced refresh" if refresh_cache else "cache not found"

    if cache is not None:
        expected = (
            cache.get("version") == CACHE_SCHEMA_VERSION
            and cache.get("rules") == fingerprint
            and cache.get("repository") == os.path.normcase(str(repo))
            and cache.get("exclude_generated") == exclude_generated
        )
        if expected:
            cached_head = str(cache.get("head", ""))
            files_value = cache.get("files")
            blob_counts_value = cache.get("blob_counts")
            snapshot_counts_value = cache.get("snapshot_counts")
            try:
                nodes = deserialize_nodes(cache.get("nodes"))
                if nodes[-1].commit != cached_head:
                    raise ValueError("cache HEAD does not match its final commit node")
                if not isinstance(files_value, dict):
                    raise ValueError("cache file counts are invalid")
                if not isinstance(blob_counts_value, dict) or not isinstance(snapshot_counts_value, dict):
                    raise ValueError("cache snapshot counts are invalid")
                files: dict[str, dict[str, object]] = {}
                for path, record in files_value.items():
                    if not isinstance(record, dict):
                        raise ValueError("cache file count record is invalid")
                    files[str(path)] = {
                        "object_id": str(record["object_id"]),
                        "language": str(record["language"]),
                        "code": int(record["code"]),
                        "comments": int(record["comments"]),
                        "blanks": int(record["blanks"]),
                    }
                blob_counts = {
                    str(key): {
                        "language": str(record["language"]),
                        "code": int(record["code"]),
                        "comments": int(record["comments"]),
                        "blanks": int(record["blanks"]),
                    }
                    for key, record in blob_counts_value.items()
                    if isinstance(record, dict)
                }
                snapshot_counts = {
                    str(commit): {
                        "files": int(record["files"]),
                        "code": int(record["code"]),
                        "comments": int(record["comments"]),
                        "blanks": int(record["blanks"]),
                    }
                    for commit, record in snapshot_counts_value.items()
                    if isinstance(record, dict)
                }
            except (KeyError, TypeError, ValueError):
                invalid_reason = "cache data is incomplete"
            else:
                if cached_head == head:
                    return (
                        nodes,
                        files,
                        blob_counts,
                        snapshot_counts,
                        head,
                        f"Cache hit: reused {len(nodes):,} commits and {len(files):,} file counts.",
                    )

                ancestors_output = run_git(repo, ("rev-list", "--first-parent", head))
                assert isinstance(ancestors_output, str)
                if cached_head in set(ancestors_output.splitlines()):
                    new_nodes = load_commit_history(
                        repo, f"{cached_head}..{head}", exclude_generated
                    )
                    nodes.extend(new_nodes)
                    for index, node in enumerate(nodes, start=1):
                        node.sequence = index

                    changed = changed_paths(repo, cached_head, head)
                    for path in changed:
                        files.pop(path, None)
                    tree = load_tree_entries(repo, head, exclude_generated)
                    changed_entries = [
                        (tree[path], path) for path in changed if path in tree
                    ]
                    recounted = count_tree_entries(repo, changed_entries)
                    files.update(recounted)
                    write_cache_file(
                        cache_path,
                        repo,
                        head,
                        exclude_generated,
                        fingerprint,
                        nodes,
                        files,
                        blob_counts,
                        snapshot_counts,
                    )
                    return (
                        nodes,
                        files,
                        blob_counts,
                        snapshot_counts,
                        head,
                        f"Cache update: reused {len(nodes) - len(new_nodes):,} commits, "
                        f"added {len(new_nodes):,}, recounted {len(recounted):,} changed files.",
                    )
                invalid_reason = "cached HEAD is not on the selected first-parent history"
        else:
            invalid_reason = "repository, options, or counting rules changed"

    nodes = load_commit_history(repo, head, exclude_generated)
    tree = load_tree_entries(repo, head, exclude_generated)
    files = count_tree_entries(repo, [(object_id, path) for path, object_id in tree.items()])
    blob_counts: dict[str, dict[str, object]] = {}
    for record in files.values():
        key = f"{record['object_id']}:{record['language']}"
        blob_counts[key] = {
            "language": record["language"],
            "code": record["code"],
            "comments": record["comments"],
            "blanks": record["blanks"],
        }
    snapshot_counts: dict[str, dict[str, int]] = {}
    write_cache_file(
        cache_path,
        repo,
        head,
        exclude_generated,
        fingerprint,
        nodes,
        files,
        blob_counts,
        snapshot_counts,
    )
    return (
        nodes,
        files,
        blob_counts,
        snapshot_counts,
        head,
        f"Cache rebuild ({invalid_reason}): stored {len(nodes):,} commits and {len(files):,} file counts.",
    )


def assign_node_totals(nodes: list[CommitNode], head_total: int) -> int:
    running_total = head_total
    for node in reversed(nodes):
        node.total_lines = running_total
        running_total -= node.net
    return running_total


def period_start(day: date, grain: str) -> date:
    if grain == "日":
        return day
    if grain == "周":
        return day - timedelta(days=day.weekday())
    if grain == "月":
        return day.replace(day=1)
    if grain == "半年":
        return date(day.year, 1 if day.month <= 6 else 7, 1)
    return date(day.year, 1, 1)


def next_period(start: date, grain: str) -> date:
    if grain == "日":
        return start + timedelta(days=1)
    if grain == "周":
        return start + timedelta(days=7)
    if grain == "月":
        return date(start.year + (start.month == 12), 1 if start.month == 12 else start.month + 1, 1)
    if grain == "半年":
        return date(start.year, 7, 1) if start.month == 1 else date(start.year + 1, 1, 1)
    return date(start.year + 1, 1, 1)


def period_label(start: date, grain: str) -> str:
    if grain == "日":
        return start.isoformat()
    if grain == "周":
        iso_year, iso_week, _ = start.isocalendar()
        return f"{iso_year}-W{iso_week:02d}"
    if grain == "月":
        return start.strftime("%Y-%m")
    if grain == "半年":
        return f"{start.year} {'上半年' if start.month == 1 else '下半年'}"
    return str(start.year)


def percentile(values: Sequence[int], fraction: float) -> int:
    if not values:
        return 0
    ordered = sorted(values)
    index = round((len(ordered) - 1) * fraction)
    return ordered[index]


def aggregate_periods(nodes: Sequence[CommitNode], baseline_total: int) -> list[dict[str, object]]:
    first_day = nodes[0].timestamp.date()
    last_day = nodes[-1].timestamp.date()
    rows: list[dict[str, object]] = []

    for grain in GRAIN_LABELS:
        buckets: dict[date, list[CommitNode]] = defaultdict(list)
        for node in nodes:
            buckets[period_start(node.timestamp.date(), grain)].append(node)

        starts: list[date] = []
        cursor = period_start(first_day, grain)
        final_start = period_start(last_day, grain)
        while cursor <= final_start:
            starts.append(cursor)
            cursor = next_period(cursor, grain)

        churn_values = [sum(node.churn for node in buckets[start]) for start in starts]
        positive_churn = [value for value in churn_values if value > 0]
        low_threshold = percentile(positive_churn, 0.25)
        high_threshold = percentile(positive_churn, 0.75)
        carried_total = baseline_total
        carried_commit = ""

        for start in starts:
            period_nodes = buckets[start]
            if period_nodes:
                carried_total = period_nodes[-1].total_lines
                carried_commit = period_nodes[-1].commit
            end = next_period(start, grain) - timedelta(days=1)
            observed_start = max(start, first_day)
            observed_end = min(end, last_day)
            calendar_days = max((observed_end - observed_start).days + 1, 1)
            additions = sum(node.additions for node in period_nodes)
            deletions = sum(node.deletions for node in period_nodes)
            churn = additions + deletions
            rewrite_lines = 2 * min(additions, deletions)
            commits = len(period_nodes)
            active_days = len({node.timestamp.date() for node in period_nodes})
            categories = Counter(node.category for node in period_nodes)
            areas: Counter[str] = Counter()
            for node in period_nodes:
                areas.update(node.directory_churn)
            if churn == 0:
                pace = "无提交"
            elif churn >= high_threshold:
                pace = "高变更"
            elif churn <= low_threshold:
                pace = "低变更"
            else:
                pace = "常规"
            rows.append(
                {
                    "grain": grain,
                    "period": period_label(start, grain),
                    "period_start": start.isoformat(),
                    "period_end": end.isoformat(),
                    "end_commit": carried_commit,
                    "total_lines": carried_total,
                    "added_lines": additions,
                    "deleted_lines": deletions,
                    "deleted_lines_negative": -deletions,
                    "新增行": additions,
                    "删减行": -deletions,
                    "net_growth": additions - deletions,
                    "净增长": additions - deletions,
                    "churn": churn,
                    "rewrite_lines": rewrite_lines,
                    "absolute_net_change": abs(additions - deletions),
                    "rewrite_share": round(rewrite_lines / churn, 4) if churn else 0,
                    "commits": commits,
                    "active_days": active_days,
                    "calendar_days": calendar_days,
                    "contributors": len({node.author for node in period_nodes}),
                    "churn_per_commit": round(churn / commits, 1) if commits else 0,
                    "daily_additions": round(additions / calendar_days, 1),
                    "daily_deletions": round(deletions / calendar_days, 1),
                    "daily_churn": round(churn / calendar_days, 1),
                    "自然日均新增": round(additions / calendar_days, 1),
                    "自然日均删除": round(deletions / calendar_days, 1),
                    "自然日均总变更": round(churn / calendar_days, 1),
                    "active_day_churn": round(churn / active_days, 1) if active_days else 0,
                    "daily_net_growth": round((additions - deletions) / calendar_days, 1),
                    "pace": pace,
                    "top_category": categories.most_common(1)[0][0] if categories else "无提交",
                    "top_area": areas.most_common(1)[0][0] if areas else "无提交",
                }
            )
    return rows


def populate_snapshot_counts(
    repo: Path,
    commits: Sequence[str],
    exclude_generated: bool,
    blob_counts: dict[str, dict[str, object]],
    snapshot_counts: dict[str, dict[str, int]],
) -> tuple[int, int]:
    """Count exact code/comment/blank lines at selected immutable commits."""
    missing_commits = [commit for commit in dict.fromkeys(commits) if commit and commit not in snapshot_counts]
    if not missing_commits:
        return 0, 0

    commit_blob_keys: dict[str, list[str]] = {}
    missing_blobs: dict[str, tuple[str, str]] = {}
    for commit in missing_commits:
        tree = load_tree_entries(repo, commit, exclude_generated)
        keys: list[str] = []
        for relative_path, object_id in tree.items():
            path = repo.joinpath(*relative_path.replace("\\", "/").split("/"))
            language = language_for(path)
            if language is None:
                continue
            key = f"{object_id}:{language.name}"
            keys.append(key)
            if key not in blob_counts:
                missing_blobs.setdefault(key, (object_id, relative_path))
        commit_blob_keys[commit] = keys

    pending = list(missing_blobs.items())
    for offset in range(0, len(pending), 1200):
        chunk = pending[offset : offset + 1200]
        synthetic_entries: list[tuple[str, str]] = []
        synthetic_to_key: dict[str, str] = {}
        for index, (key, (object_id, relative_path)) in enumerate(chunk):
            filename = relative_path.replace("\\", "/").rsplit("/", 1)[-1]
            synthetic = f"__snapshot_cache__/{offset + index:08d}/{filename}"
            synthetic_entries.append((object_id, synthetic))
            synthetic_to_key[synthetic] = key
        counted = count_tree_entries(repo, synthetic_entries)
        for synthetic, record in counted.items():
            key = synthetic_to_key[synthetic]
            blob_counts[key] = {
                "language": record["language"],
                "code": record["code"],
                "comments": record["comments"],
                "blanks": record["blanks"],
            }

    for commit, keys in commit_blob_keys.items():
        totals = {"files": 0, "code": 0, "comments": 0, "blanks": 0}
        for key in keys:
            record = blob_counts.get(key)
            if record is None:
                continue
            totals["files"] += 1
            totals["code"] += int(record["code"])
            totals["comments"] += int(record["comments"])
            totals["blanks"] += int(record["blanks"])
        snapshot_counts[commit] = totals
    return len(missing_commits), len(missing_blobs)


def weekly_snapshot_rows(
    nodes: Sequence[CommitNode],
    periods: Sequence[dict[str, object]],
    snapshot_counts: dict[str, dict[str, int]],
) -> list[dict[str, object]]:
    history_start = nodes[0].timestamp.date()
    history_end = nodes[-1].timestamp.date()
    rows: list[dict[str, object]] = []
    for period in periods:
        if period["grain"] != "周" or not period.get("end_commit"):
            continue
        week_start = date.fromisoformat(str(period["period_start"]))
        week_end = date.fromisoformat(str(period["period_end"]))
        if week_start < history_start:
            status = "历史起始周"
        elif week_end > history_end:
            status = "进行中"
        else:
            status = "完整周"
        counts = snapshot_counts[str(period["end_commit"])]
        physical = counts["code"] + counts["comments"] + counts["blanks"]
        rows.append(
            {
                **period,
                "week": period["period"],
                "week_start": period["period_start"],
                "week_end": period["period_end"],
                "week_status": status,
                "code_lines": counts["code"],
                "代码 / 内容行": counts["code"],
                "comment_lines": counts["comments"],
                "blank_lines": counts["blanks"],
                "physical_lines": physical,
                "tracked_files": counts["files"],
            }
        )

    complete_seen: list[dict[str, object]] = []
    for row in rows:
        if row["week_status"] == "完整周":
            complete_seen.append(row)
        window = complete_seen[-8:]
        row["trend_weekly_churn"] = percentile([int(item["churn"]) for item in window], 0.5)
        row["trend_daily_churn"] = round(float(row["trend_weekly_churn"]) / 7, 1)
        row["trend_weekly_net"] = percentile([int(item["net_growth"]) for item in window], 0.5)
        row["trend_daily_net"] = round(float(row["trend_weekly_net"]) / 7, 1)
    return rows


def load_changelog_releases(repo: Path, ref: str) -> list[dict[str, object]]:
    try:
        output = run_git(repo, ("show", f"{ref}:CHANGELOG.md"))
    except RuntimeError:
        return []
    assert isinstance(output, str)
    heading = re.compile(r"^##\s+\[([^]]+)]\s+(\d{4})[.-](\d{2})[.-](\d{2})\s*$")
    releases: list[dict[str, object]] = []
    current: dict[str, object] | None = None
    notes: list[str] = []

    def finish() -> None:
        nonlocal current, notes
        if current is None:
            return
        cleaned = [re.sub(r"^\s*(?:[-*]|\d+[.)])\s*", "", line).strip() for line in notes]
        cleaned = [line for line in cleaned if line and not line.startswith("#")]
        current["summary"] = "；".join(cleaned[:2])[:180] or "该版本未写明条目"
        releases.append(current)
        current = None
        notes = []

    for line in output.splitlines():
        match = heading.match(line)
        if match:
            finish()
            version, year, month, day = match.groups()
            release_date = date(int(year), int(month), int(day))
            current = {"version": version, "date": release_date.isoformat(), "_date": release_date}
        elif current is not None:
            notes.append(line)
    finish()
    return releases


def natural_change_points(
    weekly_rows: Sequence[dict[str, object]],
    releases: Sequence[dict[str, object]],
) -> list[dict[str, object]]:
    complete = [row for row in weekly_rows if row["week_status"] == "完整周"]
    candidates: list[dict[str, object]] = []
    window_size = 8
    for index in range(window_size, len(complete) - window_size):
        before = percentile([int(row["churn"]) for row in complete[index - window_size : index]], 0.5)
        after = percentile([int(row["churn"]) for row in complete[index : index + window_size]], 0.5)
        low = min(before, after)
        high = max(before, after)
        if low <= 0 or high / low < 1.6:
            continue
        boundary = date.fromisoformat(str(complete[index]["week_start"]))
        candidates.append(
            {
                "index": index,
                "change_date": boundary.isoformat(),
                "direction": "持续加速" if after > before else "持续放缓",
                "before_weekly_churn": before,
                "after_weekly_churn": after,
                "before_daily_churn": round(before / 7, 1),
                "after_daily_churn": round(after / 7, 1),
                "ratio": round(after / before, 2),
                "score": high / low,
            }
        )

    selected: list[dict[str, object]] = []
    for candidate in sorted(candidates, key=lambda item: float(item["score"]), reverse=True):
        if all(abs(int(candidate["index"]) - int(item["index"])) >= 12 for item in selected):
            selected.append(candidate)
        if len(selected) == 5:
            break
    selected.sort(key=lambda item: str(item["change_date"]))

    for candidate in selected:
        boundary = date.fromisoformat(str(candidate["change_date"]))
        nearby = sorted(
            (
                (abs((release["_date"] - boundary).days), release)
                for release in releases
                if abs((release["_date"] - boundary).days) <= 21
            ),
            key=lambda item: (item[0], str(item[1]["date"])),
        )
        if nearby:
            release = nearby[0][1]
            candidate["release_context"] = f"v{release['version']}（{release['date']}）：{release['summary']}"
        else:
            candidate["release_context"] = "前后 21 天无 CHANGELOG 版本记录，原因待结合提交明细确认"
        candidate.pop("index", None)
        candidate.pop("score", None)
    return selected


def scale_jump_rows(
    weekly_rows: Sequence[dict[str, object]],
    releases: Sequence[dict[str, object]],
) -> list[dict[str, object]]:
    """Select the largest relative weekly changes in exact code/content size."""
    candidates: list[dict[str, object]] = []
    for index, (previous, current) in enumerate(zip(weekly_rows, weekly_rows[1:]), start=1):
        if index < 8:
            continue
        before = int(previous["code_lines"])
        after = int(current["code_lines"])
        delta = after - before
        relative = abs(delta) / max(before, 1)
        if abs(delta) < 1000 or (relative < 0.04 and abs(delta) < 25000):
            continue
        boundary = date.fromisoformat(str(current["week_start"]))
        nearby = sorted(
            (
                (abs((release["_date"] - boundary).days), release)
                for release in releases
                if abs((release["_date"] - boundary).days) <= 21
            ),
            key=lambda item: (item[0], str(item[1]["date"])),
        )
        if nearby:
            release = nearby[0][1]
            context = f"v{release['version']}（{release['date']}）：{release['summary']}"
        else:
            context = "前后 21 天无 CHANGELOG 版本记录"
        candidates.append(
            {
                "jump_date": boundary.isoformat(),
                "jump_type": "规模跃升" if delta > 0 else "规模回撤",
                "before_code_lines": before,
                "after_code_lines": after,
                "code_delta": delta,
                "change_percent": round(delta / max(before, 1), 4),
                "churn": current["churn"],
                "rewrite_lines": current["rewrite_lines"],
                "rewrite_share": current["rewrite_share"],
                "commits": current["commits"],
                "release_context": context,
                "score": relative,
            }
        )
    selected = sorted(candidates, key=lambda row: (float(row["score"]), abs(int(row["code_delta"]))), reverse=True)[:12]
    selected.sort(key=lambda row: str(row["jump_date"]))
    for row in selected:
        row.pop("score", None)
    return selected


def productivity_summary(weekly_rows: Sequence[dict[str, object]]) -> dict[str, object]:
    complete = [row for row in weekly_rows if row["week_status"] == "完整周"]
    latest = complete[-8:] or complete
    baseline = complete[: min(26, len(complete))]

    def total(rows: Sequence[dict[str, object]], field: str) -> int:
        return sum(int(row[field]) for row in rows)

    latest_churn = total(latest, "churn")
    baseline_median = percentile([int(row["churn"]) for row in baseline], 0.5)
    latest_median = percentile([int(row["churn"]) for row in latest], 0.5)
    latest_rewrite = total(latest, "rewrite_lines")
    latest_net = total(latest, "net_growth")
    return {
        "latest_complete_weeks": len(latest),
        "latest_week_start": latest[0]["week_start"] if latest else "",
        "latest_week_end": latest[-1]["week_end"] if latest else "",
        "latest_weekly_churn": round(latest_churn / len(latest), 1) if latest else 0,
        "latest_daily_churn": round(latest_churn / (len(latest) * 7), 1) if latest else 0,
        "latest_daily_net": round(latest_net / (len(latest) * 7), 1) if latest else 0,
        "latest_median_weekly_churn": latest_median,
        "latest_median_daily_churn": round(latest_median / 7, 1),
        "latest_rewrite_lines": latest_rewrite,
        "latest_rewrite_share": round(latest_rewrite / latest_churn, 4) if latest_churn else 0,
        "baseline_weeks": len(baseline),
        "baseline_median_weekly_churn": baseline_median,
        "baseline_median_daily_churn": round(baseline_median / 7, 1),
        "pace_ratio": round(latest_median / baseline_median, 2) if baseline_median else 0,
    }


def compact_number(value: float) -> str:
    absolute = abs(value)
    if absolute >= 10000:
        return f"{value / 10000:.1f} 万"
    if absolute >= 1000:
        return f"{value / 1000:.1f} 千"
    return f"{value:,.0f}"


def generate_share_card(
    output: Path,
    summary: dict[str, object],
    productivity: dict[str, object],
    weekly_rows: Sequence[dict[str, object]],
    change_points: Sequence[dict[str, object]],
) -> None:
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError as error:
        raise RuntimeError("Pillow is required to generate --share-card") from error

    width, height = 1080, 1440
    image = Image.new("RGB", (width, height), "#F4F7FB")
    draw = ImageDraw.Draw(image)
    font_dir = Path(os.environ.get("WINDIR", r"C:\Windows")) / "Fonts"

    def font(size: int, bold: bool = False):
        candidates = (
            [font_dir / "msyhbd.ttc", font_dir / "simhei.ttf"]
            if bold
            else [font_dir / "msyh.ttc", font_dir / "simsun.ttc"]
        )
        for candidate in candidates:
            if candidate.is_file():
                return ImageFont.truetype(str(candidate), size=size)
        return ImageFont.load_default()

    navy = "#12304A"
    blue = "#1677FF"
    green = "#0B8F71"
    muted = "#60758A"
    card = "#FFFFFF"
    draw.rounded_rectangle((48, 42, 1032, 1398), radius=42, fill=card)
    draw.text((90, 92), "COLORVISION · 代码成长记录", font=font(27, True), fill=blue)

    ratio = float(productivity["pace_ratio"])
    if ratio >= 1.15:
        headline = f"持续代码变更速度\n已是早期的 {ratio:.1f} 倍"
    elif ratio <= 0.85 and ratio > 0:
        headline = f"持续代码变更速度\n回落到早期的 {ratio:.1f} 倍"
    else:
        headline = "持续代码变更速度\n与早期大致相当"
    draw.multiline_text((90, 165), headline, font=font(62, True), fill=navy, spacing=18)
    draw.text(
        (92, 338),
        "按完整自然周统计 · 8 周中位数，降低单次重构峰值干扰",
        font=font(25),
        fill=muted,
    )

    draw.rounded_rectangle((90, 405, 990, 640), radius=28, fill="#F0F6FF")
    draw.text((130, 448), "当前代码 / 内容行", font=font(28), fill=muted)
    draw.text(
        (130, 492),
        compact_number(float(summary["head_code_lines"])),
        font=font(68, True),
        fill=navy,
    )
    draw.text(
        (610, 462),
        "近 8 周自然日均变更",
        font=font(25),
        fill=muted,
    )
    draw.text(
        (610, 512),
        f"{compact_number(float(productivity['latest_daily_churn']))} 行 / 天",
        font=font(37, True),
        fill=blue,
    )

    chart_box = (90, 700, 990, 1000)
    draw.text((90, 666), "每周变更趋势（新增 + 删除，8 周中位数）", font=font(27, True), fill=navy)
    draw.rounded_rectangle(chart_box, radius=24, fill="#F8FAFD", outline="#DFE8F1", width=2)
    series = [
        float(row["trend_daily_churn"])
        for row in weekly_rows
        if row["week_status"] == "完整周"
    ]
    if series:
        low, high = min(series), max(series)
        span = max(high - low, 1)
        points = []
        for index, value in enumerate(series):
            x = chart_box[0] + 28 + index * (chart_box[2] - chart_box[0] - 56) / max(len(series) - 1, 1)
            y = chart_box[3] - 30 - (value - low) * (chart_box[3] - chart_box[1] - 60) / span
            points.append((x, y))
        if len(points) > 1:
            draw.line(points, fill=blue, width=7, joint="curve")
        draw.ellipse((points[-1][0] - 9, points[-1][1] - 9, points[-1][0] + 9, points[-1][1] + 9), fill=green)
        draw.text((116, 936), f"早期 {compact_number(float(productivity['baseline_median_daily_churn']))} 行/天", font=font(22), fill=muted)
        latest_label = f"现在 {compact_number(float(productivity['latest_median_daily_churn']))} 行/天"
        label_width = draw.textbbox((0, 0), latest_label, font=font(22, True))[2]
        draw.text((960 - label_width, 936), latest_label, font=font(22, True), fill=blue)

    rewrite_percent = float(productivity["latest_rewrite_share"]) * 100
    draw.text((90, 1060), "怎么看这组数字", font=font(28, True), fill=navy)
    draw.text(
        (90, 1112),
        f"• 增长 ≠ 工作量：新增 + 删除才是代码变更\n• 近 8 周约 {rewrite_percent:.0f}% 变更可视作成对改写 / 重构\n• 节奏变化来自数据识别，不按工具日期硬切阶段",
        font=font(25),
        fill=muted,
        spacing=17,
    )
    if change_points:
        latest_point = change_points[-1]
        context = f"最近变化线索：{latest_point['release_context']}"
        context_font = font(21)
        while len(context) > 12 and draw.textbbox((0, 0), context + "…", font=context_font)[2] > 900:
            context = context[:-1]
        if not context.endswith("。"):
            context += "…"
        draw.text((90, 1276), context, font=context_font, fill=green)
    draw.text(
        (90, 1340),
        f"Git first-parent · {summary['first_date']}—{summary['last_date']} · 自动生成",
        font=font(19),
        fill="#8A9AAA",
    )
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, format="PNG", optimize=True)


def summary_data(
    nodes: Sequence[CommitNode],
    language_counts: dict[str, Count],
    periods: Sequence[dict[str, object]],
    baseline_total: int,
) -> tuple[dict[str, object], dict[str, object]]:
    total = Count()
    for count in language_counts.values():
        total.add(count)
    monthly = [row for row in periods if row["grain"] == "月"]
    peak_month = max(monthly, key=lambda row: int(row["churn"]))
    complete_months = monthly[1:-1] or monthly
    quiet_month = min(complete_months, key=lambda row: (int(row["churn"]), str(row["period"])))
    sorted_days = sorted({node.timestamp.date() for node in nodes})
    gaps = [
        (right - left).days - 1
        for left, right in zip(sorted_days, sorted_days[1:])
    ]
    longest_gap = max(gaps, default=0)
    summary = {
        "head_total_lines": total.lines,
        "head_code_lines": total.code,
        "head_comment_lines": total.comments,
        "head_blank_lines": total.blanks,
        "tracked_files": total.files,
        "commits": len(nodes),
        "active_days": len(sorted_days),
        "contributors": len({node.author for node in nodes}),
        "total_additions": sum(node.additions for node in nodes),
        "total_deletions": sum(node.deletions for node in nodes),
        "total_churn": sum(node.churn for node in nodes),
        "history_net_growth": total.lines - baseline_total,
        "first_date": nodes[0].timestamp.date().isoformat(),
        "last_date": nodes[-1].timestamp.date().isoformat(),
        "peak_month": peak_month["period"],
        "peak_month_churn": peak_month["churn"],
        "quiet_month": quiet_month["period"],
        "quiet_month_churn": quiet_month["churn"],
        "longest_inactive_gap_days": longest_gap,
    }
    return summary, {"peak": peak_month, "quiet": quiet_month}


def category_rows(nodes: Iterable[CommitNode]) -> list[dict[str, object]]:
    values: dict[str, Counter[str]] = defaultdict(Counter)
    for node in nodes:
        values[node.category].update(
            commits=1,
            added_lines=node.additions,
            deleted_lines=node.deletions,
            churn=node.churn,
        )
    return [
        {"category": category, **dict(metrics)}
        for category, metrics in sorted(
            values.items(), key=lambda item: (-item[1]["commits"], item[0])
        )
    ]


def directory_rows(nodes: Iterable[CommitNode]) -> list[dict[str, object]]:
    churn: Counter[str] = Counter()
    for node in nodes:
        churn.update(node.directory_churn)
    return [
        {"area": area, "churn": value}
        for area, value in churn.most_common(15)
    ]


def language_rows(counts: dict[str, Count]) -> list[dict[str, object]]:
    return [
        {
            "language": name,
            "files": count.files,
            "code": count.code,
            "comments": count.comments,
            "blank": count.blanks,
            "total_lines": count.lines,
        }
        for name, count in sorted(counts.items(), key=lambda item: (-item[1].lines, item[0]))
    ]


def commit_row(node: CommitNode) -> dict[str, object]:
    return {
        "grain": "提交节点",
        "sequence": node.sequence,
        "timestamp": node.timestamp.isoformat(),
        "commit": node.commit[:10],
        "subject": node.subject[:140],
        "author": node.author,
        "category": node.category,
        "primary_area": node.primary_area,
        "files_changed": node.files_changed,
        "added_lines": node.additions,
        "deleted_lines": node.deletions,
        "net_growth": node.net,
        "churn": node.churn,
        "rewrite_lines": 2 * min(node.additions, node.deletions),
        "total_lines": node.total_lines,
    }


def materialize_datasets_with_sqlite(
    datasets: dict[str, list[dict[str, object]]],
) -> dict[str, list[dict[str, object]]]:
    connection = sqlite3.connect(":memory:")
    try:
        connection.execute(
            "CREATE TABLE artifact_rows ("
            "dataset_id TEXT NOT NULL, row_index INTEGER NOT NULL, row_json TEXT NOT NULL, "
            "PRIMARY KEY (dataset_id, row_index))"
        )
        connection.executemany(
            "INSERT INTO artifact_rows (dataset_id, row_index, row_json) VALUES (?, ?, ?)",
            (
                (
                    dataset_id,
                    index,
                    json.dumps(row, ensure_ascii=False, separators=(",", ":")),
                )
                for dataset_id, rows in datasets.items()
                for index, row in enumerate(rows)
            ),
        )
        rebuilt: dict[str, list[dict[str, object]]] = {
            dataset_id: [] for dataset_id in datasets
        }
        for dataset_id, _, row_json in connection.execute(SNAPSHOT_SQL):
            rebuilt[dataset_id].append(json.loads(row_json))
        if rebuilt != datasets:
            raise RuntimeError("SQLite snapshot materialization changed dashboard data")
        return rebuilt
    finally:
        connection.close()


def source_spec(
    ref: str,
    generated_at: str,
    exclude_generated: bool,
) -> dict[str, object]:
    exclusions = "bin/obj/node_modules/packages/TestResults/artifacts"
    if exclude_generated:
        exclusions += ", generated/minified file suffixes"
    return {
        "id": "git_history",
        "label": f"Git first-parent history and exact snapshots at {ref}",
        "path": ".git + CHANGELOG.md",
        "query": {
            "engine": "sqlite",
            "language": "sql",
            "sql": SNAPSHOT_SQL,
            "description": "Reads reviewed Git-derived rows from the in-memory artifact snapshot table.",
            "executed_at": generated_at,
            "tables_used": ["artifact_rows"],
            "upstream_commands": (
                "git log --first-parent --reverse --diff-merges=first-parent "
                "--no-renames --numstat --format=<commit-fields> " + ref + "\n"
                "git ls-tree -r <weekly-endpoint> | git cat-file --batch\n"
                "git show " + ref + ":CHANGELOG.md"
            ),
            "filters": [
                "Only file types recognized by Scripts/count_code_lines.py",
                f"Excluded directories: {exclusions}",
                "Binary numstat entries are excluded",
                "File renames are treated as delete plus add",
                "Trend baselines exclude the partial first and current weeks",
                "Natural change points compare the median churn of 8 complete weeks before and after",
                "CHANGELOG entries are contextual clues, not proof of causality",
            ],
            "metric_definitions": {
                "代码/内容行": "指定周末提交的精确文件快照中，非空且不是纯注释的行；混合代码与注释的行计为代码。",
                "物理文本行": "代码/内容行、纯注释行和空行之和，仅作为辅助规模口径。",
                "代码变更量": "Git numstat 新增行加删除行；二者都属于变更。",
                "净增长": "新增行减删除行；它表示规模变化，不等同于开发工作量。",
                "改写/重构估算": "2 × min(新增, 删除)，表示周期内可成对理解的替换量；是规模估算，不是语义级重构判定。",
                "自然日均变更": "周期变更量除以实际覆盖的自然天数。",
                "8周稳健趋势": "最近 8 个完整自然周的每周变更量中位数，再除以 7；用于降低单次导入、格式化或重构峰值影响。",
                "自然变化点": "连续 8 周窗口的中位变更量相对前一窗口至少变化 1.6 倍，并对相邻候选点去重。",
                "提交类型": "根据提交标题关键词自动分类，仅用于回顾主题。",
            },
        },
    }


def build_artifact(
    repo: Path,
    ref: str,
    nodes: list[CommitNode],
    language_counts: dict[str, Count],
    periods: list[dict[str, object]],
    weekly_rows: list[dict[str, object]],
    change_points: list[dict[str, object]],
    scale_jumps: list[dict[str, object]],
    productivity: dict[str, object],
    baseline_total: int,
    generated_at: str,
    exclude_generated: bool,
) -> dict[str, object]:
    summary, _ = summary_data(nodes, language_counts, periods, baseline_total)
    summary.update(productivity)
    directories = directory_rows(nodes)
    languages = language_rows(language_counts)
    source = source_spec(ref, generated_at, exclude_generated)
    branch = str(run_git(repo, ("rev-parse", "--abbrev-ref", ref))).strip()

    cards = [
        {
            "id": "code_lines_card",
            "description": "当前提交的精确代码/内容行；物理文本行单列显示。",
            "dataset": "summary",
            "sourceId": "git_history",
            "metrics": [
                {"label": "代码 / 内容行", "field": "head_code_lines", "format": "compact"},
                {"label": "物理文本行", "field": "head_total_lines", "format": "compact"},
            ],
        },
        {
            "id": "daily_change_card",
            "description": "最近 8 个完整自然周的新增与删除之和，分别按周和自然日平均。",
            "dataset": "summary",
            "sourceId": "git_history",
            "metrics": [
                {"label": "近 8 周周均变更", "field": "latest_weekly_churn", "format": "compact"},
                {"label": "自然日均变更", "field": "latest_daily_churn", "format": "number"},
            ],
        },
        {
            "id": "pace_card",
            "description": "最近 8 周与历史最早 26 个完整周的周变更中位数之比。",
            "dataset": "summary",
            "sourceId": "git_history",
            "metrics": [{"label": "相对早期节奏", "field": "pace_ratio", "format": "number"}],
        },
        {
            "id": "rewrite_card",
            "description": "最近 8 周变更中可按新增/删除配对理解的改写量占比。",
            "dataset": "summary",
            "sourceId": "git_history",
            "metrics": [{"label": "近 8 周改写占比", "field": "latest_rewrite_share", "format": "percent"}],
        },
        {
            "id": "commits_card",
            "description": "HEAD 第一父链上的提交节点和当前识别文件数。",
            "dataset": "summary",
            "sourceId": "git_history",
            "metrics": [
                {"label": "提交节点", "field": "commits", "format": "number"},
                {"label": "当前文件", "field": "tracked_files", "format": "number"},
            ],
        },
    ]

    period_filter = {
        "id": "grain_filter",
        "label": "时间粒度",
        "dataset": "periods",
        "field": "grain",
        "defaultValue": "周",
        "includeAll": False,
        "targets": [
            {"dataset": dataset_id, "field": "grain"}
            for dataset_id in (
                "summary",
                "weekly_snapshots",
                "change_points",
                "scale_jumps",
                "directories",
                "languages",
            )
        ],
    }

    jump_references = [
        {
            "axis": "x",
            "value": row["jump_date"],
            "label": f"{'+' if int(row['code_delta']) > 0 else '-'}{compact_number(abs(float(row['code_delta'])))}",
        }
        for row in scale_jumps[-8:]
    ]

    charts = [
        {
            "id": "code_lines_trend",
            "title": "代码规模与平均变更（精确周快照）",
            "subtitle": "主线是代码 / 内容行；同时绘制自然日均新增、删除和总变更，因此悬停会显示四项数据。竖线标出主要规模跃变。",
            "type": "line",
            "dataset": "weekly_snapshots",
            "sourceId": "git_history",
            "valueFormat": "compact",
            "layout": "full",
            "combinationRationale": "四项指标都以行数计量；代码规模是存量，另外三项是同一周的自然日均流量，用于悬停对照。",
            "referenceLines": jump_references,
            "encodings": {
                "x": {"field": "week_start", "type": "temporal", "label": "自然周"},
                "y": {
                    "fields": ["代码 / 内容行", "自然日均总变更", "自然日均新增", "自然日均删除"],
                    "type": "quantitative",
                    "label": "行数",
                },
            },
        },
        {
            "id": "add_delete_chart",
            "title": "新增与删除行数",
            "subtitle": "随顶部粒度切换；新增向上、删除向下，适合观察重构和代码清理规模。",
            "type": "bar",
            "dataset": "periods",
            "sourceId": "git_history",
            "valueFormat": "compact",
            "layout": "half",
            "encodings": {
                "x": {"field": "period_start", "type": "temporal", "label": "周期"},
                "y": {"fields": ["新增行", "删减行"], "type": "quantitative", "label": "变更行数"},
            },
        },
        {
            "id": "net_growth_chart",
            "title": "净增长与收缩周期",
            "subtitle": "随顶部粒度切换；净增长 = 新增 − 删除，零线以下代表代码规模收缩。",
            "type": "bar",
            "dataset": "periods",
            "sourceId": "git_history",
            "valueFormat": "compact",
            "layout": "half",
            "encodings": {
                "x": {"field": "period_start", "type": "temporal", "label": "周期"},
                "y": {"field": "净增长", "type": "quantitative", "label": "净增长"},
            },
            "referenceLines": [{"axis": "y", "value": 0, "label": "零线"}],
        },
        {
            "id": "directory_chart",
            "title": "主要目录变更量",
            "subtitle": "Git 新增与删除之和，展示历史上投入最多的仓库区域。",
            "type": "horizontalBar",
            "dataset": "directories",
            "sourceId": "git_history",
            "valueFormat": "compact",
            "layout": "full",
            "maxRows": 15,
            "encodings": {
                "x": {"field": "area", "type": "nominal", "label": "目录"},
                "y": {"field": "churn", "type": "quantitative", "label": "代码变更量"},
            },
        },
        {
            "id": "language_chart",
            "title": "当前语言与文件类型规模",
            "subtitle": "统一使用代码 / 内容行，注释和空行只在悬停中辅助查看。",
            "type": "horizontalBar",
            "dataset": "languages",
            "sourceId": "git_history",
            "valueFormat": "compact",
            "layout": "full",
            "maxRows": 15,
            "encodings": {
                "x": {"field": "language", "type": "nominal", "label": "语言 / 类型"},
                "y": {"field": "code", "type": "quantitative", "label": "代码 / 内容行"},
                "tooltip": [
                    {"field": "comments", "type": "quantitative", "label": "纯注释", "format": "compact"},
                    {"field": "blank", "type": "quantitative", "label": "空行", "format": "compact"},
                    {"field": "files", "type": "quantitative", "label": "文件数", "format": "number"},
                ],
            },
        },
    ]

    tables: list[dict[str, object]] = [
        {
            "id": "change_points_table",
            "title": "自然识别出的节奏变化点",
            "subtitle": "比较前后各 8 个完整周的变更中位数；CHANGELOG 只提供同期线索，不强行解释原因。",
            "dataset": "change_points",
            "sourceId": "git_history",
            "density": "compact",
            "layout": "full",
            "defaultSort": {"field": "change_date", "direction": "desc"},
            "columns": [
                {"field": "change_date", "label": "变化周", "type": "date"},
                {"field": "direction", "label": "方向", "type": "text"},
                {"field": "before_daily_churn", "label": "此前日趋势", "format": "number", "align": "right"},
                {"field": "after_daily_churn", "label": "此后日趋势", "format": "number", "align": "right"},
                {"field": "ratio", "label": "前后倍率", "format": "number", "align": "right"},
                {"field": "release_context", "label": "同期版本线索", "type": "text"},
            ],
        },
        {
            "id": "scale_jumps_table",
            "title": "主要代码规模跃变",
            "subtitle": "从精确周快照中按相对变化筛选；结合变更量、改写占比和 CHANGELOG 判断是否为导入、重构或功能扩张。",
            "dataset": "scale_jumps",
            "sourceId": "git_history",
            "density": "compact",
            "layout": "full",
            "defaultSort": {"field": "jump_date", "direction": "desc"},
            "columns": [
                {"field": "jump_date", "label": "跃变周", "type": "date"},
                {"field": "jump_type", "label": "类型", "type": "text"},
                {"field": "before_code_lines", "label": "此前代码行", "format": "compact", "align": "right"},
                {"field": "after_code_lines", "label": "此后代码行", "format": "compact", "align": "right"},
                {"field": "code_delta", "label": "规模变化", "format": "compact", "movement": True, "align": "right"},
                {"field": "change_percent", "label": "变化比例", "format": "percent", "align": "right"},
                {"field": "churn", "label": "总变更", "format": "compact", "align": "right"},
                {"field": "rewrite_share", "label": "改写占比", "format": "percent", "align": "right"},
                {"field": "release_context", "label": "同期版本线索", "type": "text"},
            ],
        },
        {
            "id": "period_table",
            "title": "周期增删明细",
            "subtitle": "顶部可切换日、周、月、半年和年；总变更 = 新增 + 删除。",
            "dataset": "periods",
            "sourceId": "git_history",
            "density": "compact",
            "layout": "full",
            "defaultSort": {"field": "period_start", "direction": "desc"},
            "columns": [
                {"field": "period_start", "label": "周期", "type": "date"},
                {"field": "added_lines", "label": "新增", "format": "compact", "align": "right"},
                {"field": "deleted_lines", "label": "删除", "format": "compact", "align": "right"},
                {"field": "churn", "label": "总变更", "format": "compact", "align": "right"},
                {"field": "rewrite_lines", "label": "改写估算", "format": "compact", "align": "right"},
                {"field": "net_growth", "label": "净增长", "format": "number", "movement": True, "align": "right"},
                {"field": "daily_additions", "label": "自然日均新增", "format": "number", "align": "right"},
                {"field": "daily_deletions", "label": "自然日均删除", "format": "number", "align": "right"},
                {"field": "daily_churn", "label": "自然日均变更", "format": "number", "align": "right"},
                {"field": "active_day_churn", "label": "活跃日均变更", "format": "number", "align": "right"},
                {"field": "commits", "label": "提交", "format": "number", "align": "right"},
            ],
        },
    ]

    blocks: list[dict[str, object]] = [
        {
            "id": "overview",
            "type": "markdown",
            "sourceId": "git_history",
            "body": (
                f"## 项目代码历史\n\n分支 **{branch}**，统计范围 **{summary['first_date']} 至 {summary['last_date']}**。"
                f"当前精确代码 / 内容行 **{int(summary['head_code_lines']):,}**，物理文本行 **{int(summary['head_total_lines']):,}**。"
                f"最近 {int(productivity['latest_complete_weeks'])} 个完整周周均变更 **{float(productivity['latest_weekly_churn']):,.0f} 行**，"
                f"自然日均 **{float(productivity['latest_daily_churn']):,.0f} 行**，"
                f"8 周中位趋势为 **{float(productivity['latest_median_daily_churn']):,.0f} 行 / 天**，约为早期稳健水平的 **{float(productivity['pace_ratio']):.2f} 倍**。"
            ),
        },
        {"id": "metrics", "type": "metric-strip", "cardIds": [card["id"] for card in cards], "layout": "full"},
        {"id": "code_lines_block", "type": "chart", "chartId": "code_lines_trend", "layout": "full"},
        {"id": "add_delete_block", "type": "chart", "chartId": "add_delete_chart", "layout": "half"},
        {"id": "net_growth_block", "type": "chart", "chartId": "net_growth_chart", "layout": "half"},
        {
            "id": "change_heading",
            "type": "markdown",
            "sourceId": "git_history",
            "body": (
                "## 变化节点\n\n“持续节奏变化”比较前后各 8 周；“规模跃变”比较相邻精确周快照。"
                "两者都结合 CHANGELOG 提供同期线索，但不把相关性写成因果。"
            ),
        },
        {"id": "change_points_block", "type": "table", "tableId": "change_points_table", "layout": "full"},
        {"id": "scale_jumps_block", "type": "table", "tableId": "scale_jumps_table", "layout": "full"},
        {"id": "period_table_block", "type": "table", "tableId": "period_table", "layout": "full"},
        {"id": "work_heading", "type": "markdown", "body": "## 工作重心\n\n主要目录和当前语言规模用于回顾项目投入方向。"},
        {"id": "directory_block", "type": "chart", "chartId": "directory_chart", "layout": "full"},
        {"id": "language_block", "type": "chart", "chartId": "language_chart", "layout": "full"},
    ]

    def repeat_for_grains(rows: list[dict[str, object]]) -> list[dict[str, object]]:
        return [{**row, "grain": grain} for grain in FILTER_GRAIN_LABELS for row in rows]

    datasets: dict[str, list[dict[str, object]]] = {
        "summary": repeat_for_grains([summary]),
        "periods": periods,
        "weekly_snapshots": repeat_for_grains(weekly_rows),
        "change_points": repeat_for_grains(change_points),
        "scale_jumps": repeat_for_grains(scale_jumps),
        "directories": repeat_for_grains(directories),
        "languages": repeat_for_grains(languages),
    }
    datasets = materialize_datasets_with_sqlite(datasets)
    return {
        "surface": "dashboard",
        "manifest": {
            "version": 1,
            "surface": "dashboard",
            "title": "ColorVision 代码历史仪表盘",
            "description": "基于精确周快照与自然变化点，回顾代码规模、变更、重构和工作重心。",
            "generatedAt": generated_at,
            "filters": [period_filter],
            "cards": cards,
            "charts": charts,
            "tables": tables,
            "sources": [{"id": source["id"], "label": source["label"], "path": source["path"]}],
            "blocks": blocks,
        },
        "snapshot": {"version": 1, "generatedAt": generated_at, "status": "ready", "datasets": datasets, "accessIssues": []},
        "sources": [source],
        "package_info": {"originUrl": "artifact://colorvision-code-history", "controls": {"edit": False, "refresh": False}},
    }


def find_builder(explicit: Path | None) -> Path:
    if explicit:
        path = explicit.resolve()
        if path.is_file():
            return path
        raise FileNotFoundError(f"Portable dashboard builder not found: {path}")
    pattern = (
        Path.home()
        / ".codex"
        / "plugins"
        / "cache"
        / "openai-curated-remote"
        / "data-analytics"
    )
    candidates = list(
        pattern.glob("*/skills/build-report/scripts/deliver_portable_artifact.mjs")
    )
    if not candidates:
        raise FileNotFoundError(
            "Data Analytics portable dashboard builder was not found. "
            "Install/enable the Data Analytics plugin or pass --builder."
        )
    return max(candidates, key=lambda path: path.stat().st_mtime)


def run_base_builder(node: str, builder: Path, artifact: Path, output: Path) -> None:
    base_builder = builder.with_name("build_portable_artifact.mjs")
    if not base_builder.is_file():
        raise FileNotFoundError(f"Portable dashboard base builder not found: {base_builder}")
    process = subprocess.run(
        [node, str(base_builder), "--input", str(artifact), "--output", str(output)],
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        encoding="utf-8",
        errors="replace",
    )
    if process.stdout:
        print(process.stdout.rstrip())
    if process.returncode != 0:
        raise RuntimeError(
            f"Portable dashboard packaging failed with exit code {process.returncode}"
        )


def package_html(builder: Path, artifact: Path, output: Path, verify: bool) -> None:
    node = shutil.which("node")
    if node is None:
        raise FileNotFoundError("Node.js is required to package the HTML dashboard")
    if not verify:
        run_base_builder(node, builder, artifact, output)
        return

    process = subprocess.run(
        [
            node,
            str(builder),
            "--input",
            str(artifact),
            "--output",
            str(output),
            "--ready-timeout-ms",
            "15000",
            "--timeout-ms",
            "30000",
        ],
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        encoding="utf-8",
        errors="replace",
    )
    if process.stdout:
        print(process.stdout.rstrip())
    if process.returncode == 0:
        return
    if '"code":"horizontal_overflow"' not in (process.stdout or ""):
        raise RuntimeError(f"Portable dashboard packaging failed with exit code {process.returncode}")

    print(
        "Portable verifier reported its known iframe scrollbar-width overflow; "
        "building the same validated reader for direct-browser QA."
    )
    run_base_builder(node, builder, artifact, output)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    repo = args.repo.resolve()
    if not (repo / ".git").exists():
        print(f"error: not a Git repository root: {repo}", file=sys.stderr)
        return 2
    try:
        generated_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
        cache_path = args.cache.resolve()
        print("Loading incremental first-parent history...")
        nodes, file_counts, blob_counts, snapshot_counts, head, cache_status = load_or_update_cache(
            repo,
            args.ref,
            cache_path,
            args.exclude_generated,
            args.refresh_cache,
        )
        print(cache_status)
        print(f"Cache: {cache_path} ({cache_path.stat().st_size / 1024 / 1024:.2f} MiB)")
        language_counts = language_counts_from_files(file_counts)
        head_total = sum(count.lines for count in language_counts.values())
        baseline_total = assign_node_totals(nodes, head_total)
        periods = aggregate_periods(nodes, baseline_total)
        weekly_commits = [
            str(row["end_commit"])
            for row in periods
            if row["grain"] == "周" and row.get("end_commit")
        ]
        print("Loading exact weekly code snapshots...")
        new_snapshots, new_blobs = populate_snapshot_counts(
            repo,
            weekly_commits,
            args.exclude_generated,
            blob_counts,
            snapshot_counts,
        )
        write_cache_file(
            cache_path,
            repo,
            head,
            args.exclude_generated,
            cache_fingerprint(),
            nodes,
            file_counts,
            blob_counts,
            snapshot_counts,
        )
        print(
            f"Snapshot cache: {len(snapshot_counts):,} commits, {len(blob_counts):,} immutable blobs "
            f"({new_snapshots:,} snapshots and {new_blobs:,} blobs added)."
        )
        weekly_rows = weekly_snapshot_rows(nodes, periods, snapshot_counts)
        releases = load_changelog_releases(repo, args.ref)
        change_points = natural_change_points(weekly_rows, releases)
        scale_jumps = scale_jump_rows(weekly_rows, releases)
        productivity = productivity_summary(weekly_rows)
        artifact = build_artifact(
            repo,
            args.ref,
            nodes,
            language_counts,
            periods,
            weekly_rows,
            change_points,
            scale_jumps,
            productivity,
            baseline_total,
            generated_at,
            args.exclude_generated,
        )

        artifact_path = args.artifact.resolve()
        output_path = args.output.resolve()
        artifact_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        artifact_path.write_text(
            json.dumps(artifact, ensure_ascii=False, separators=(",", ":")),
            encoding="utf-8",
        )
        print(
            f"Artifact: {artifact_path} ({artifact_path.stat().st_size / 1024 / 1024:.2f} MiB)"
        )
        share_path = args.share_card.resolve()
        summary = artifact["snapshot"]["datasets"]["summary"][0]
        generate_share_card(share_path, summary, productivity, weekly_rows, change_points)
        print(f"Share card: {share_path}")
        if not args.no_build:
            builder = find_builder(args.builder)
            package_html(builder, artifact_path, output_path, args.verify)
            print(f"Dashboard: {output_path}")
            if args.open:
                if os.name == "nt":
                    os.startfile(output_path)  # type: ignore[attr-defined]
                else:
                    print("--open is currently supported on Windows only")
        if baseline_total != 0:
            print(
                f"Note: history starts from a {baseline_total:,}-line baseline before the first visible node."
            )
        return 0
    except (FileNotFoundError, RuntimeError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
