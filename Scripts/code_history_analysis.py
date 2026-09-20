"""Worktree inventory and offline presentation for the code history dashboard.

Project ownership is a physical-directory partition, not evaluated MSBuild items.
Each recognized file belongs to exactly one nearest project directory, or one
unowned top-level directory. This makes drill-down totals additive.
"""
from __future__ import annotations

import json
from collections import defaultdict
from datetime import datetime, timedelta, timezone
from pathlib import Path

from count_code_lines import count_text, decode_source, git_files, is_skipped, language_for

PROJECT_SUFFIXES = {".csproj", ".vcxproj", ".fsproj", ".vbproj", ".shproj"}
METRICS = ("files", "code", "comments", "blank", "lines")


def totals(rows):
    return {key: sum(row[key] for row in rows) for key in METRICS}


def collect_worktree(repo: Path, exclude_generated: bool, excluded_paths=()) -> dict:
    """Use the same Git selection and line classifier as count_code_lines.py."""
    candidates = git_files(repo, tracked_only=False)
    if candidates is None:
        raise RuntimeError("Cannot enumerate Git worktree files; refusing an unfiltered filesystem scan")
    excluded = {path.resolve() for path in excluded_paths}
    paths = sorted(set(path for path in candidates if not is_skipped(path, repo, exclude_generated)))
    project_dirs = defaultdict(list)
    for path in paths:
        if path.suffix.lower() in PROJECT_SUFFIXES and path.is_file():
            project_dirs[path.parent.relative_to(repo).as_posix()].append(path.name)
    files = []
    skipped = 0
    for path in paths:
        if path.resolve() in excluded:
            continue
        # Do not read source linked outside the selected repository.
        if not path.resolve().is_relative_to(repo) or not path.is_file():
            skipped += 1
            continue
        language = language_for(path)
        content = decode_source(path) if language else None
        if content is None:
            skipped += 1
            continue
        relative = path.relative_to(repo)
        owner = next((parent.as_posix() for parent in relative.parents
                      if parent.as_posix() in project_dirs), None)
        area = relative.parts[0] if len(relative.parts) > 1 else "根目录"
        project = "project:" + owner if owner is not None else "folder:" + area
        count = count_text(content, language)
        files.append({"path": relative.as_posix(), "area": area, "project": project,
                      "language": language.name, "files": 1, "code": count.code,
                      "comments": count.comments, "blank": count.blanks, "lines": count.lines})
    groups = defaultdict(list)
    for row in files:
        groups[row["project"]].append(row)
    projects = []
    for key, rows in groups.items():
        is_project = key.startswith("project:")
        directory = key.split(":", 1)[1]
        definitions = project_dirs[directory] if is_project else []
        name = " / ".join(Path(item).stem for item in definitions) if definitions else directory
        projects.append({"id": key, "name": name, "directory": directory,
                         "kind": "工程目录" if is_project else "独立目录",
                         "definitions": definitions, "area": rows[0]["area"], **totals(rows)})
    projects.sort(key=lambda row: (-row["code"], row["id"]))
    language_groups = defaultdict(list)
    for row in files:
        language_groups[row["language"]].append(row)
    languages = [{"language": language, **totals(rows)} for language, rows in language_groups.items()]
    return {"root": str(repo), "scannedAt": datetime.now(timezone.utc).isoformat(),
            "files": files, "projects": projects,
            "languages": sorted(languages, key=lambda row: -row["code"]),
            "total": totals(files), "project_count": len(project_dirs),
            "project_definition_count": sum(map(len, project_dirs.values())),
            "skipped": skipped, "exclude_generated": exclude_generated}


def history_analysis(nodes, weekly_rows, ref, generated_at, branch):
    # Original offset and date are retained: this matches existing history buckets.
    commits = [{"commit": node.commit, "date": node.timestamp.date().isoformat(),
                "timestamp": node.timestamp.isoformat(), "hour": node.timestamp.hour,
                "weekday": node.timestamp.weekday(), "author": node.author,
                "subject": node.subject, "category": node.category,
                "area": node.primary_area, "areas": node.directory_churn,
                "files": node.files_changed, "added": node.additions,
                "deleted": node.deletions, "net": node.net, "churn": node.churn}
               for node in nodes]
    # Anchor to the selected history endpoint; older refs must not be compared to today.
    last_day = max(node.timestamp.date() for node in nodes)
    monday = last_day - timedelta(days=last_day.weekday())
    end = monday - timedelta(days=1)
    start = end - timedelta(days=6)
    first_day = min(node.timestamp.date() for node in nodes)
    def window(left, right):
        rows = [row for row in commits if left.isoformat() <= row["date"] <= right.isoformat()]
        return {"start": left.isoformat(), "end": right.isoformat(),
                "available": left >= first_day, "commits": len(rows),
                "active_days": len({row["date"] for row in rows}),
                **{key: sum(row[key] for row in rows) for key in ("added", "deleted", "net", "churn")}}
    return {"ref": ref, "branch": branch, "head": nodes[-1].commit,
            "generatedAt": generated_at, "first": first_day.isoformat(), "last": last_day.isoformat(),
            "commits": commits, "weeks": weekly_rows,
            "recent_week": window(start, end),
            "previous_week": window(start - timedelta(days=7), end - timedelta(days=7))}


def write_dashboard(artifact: Path, output: Path) -> None:
    document = json.loads(artifact.read_text(encoding="utf-8"))
    data = document["analysis"]
    template_dir = Path(__file__).with_name("code_history_dashboard")
    template = (template_dir / "index.html").read_text(encoding="utf-8")
    # Escaping '<' prevents file names / commit messages from terminating script tags.
    payload = json.dumps(data, ensure_ascii=True, separators=(",", ":")).replace("<", "\\u003c")
    page = template.replace("/* DASHBOARD_STYLE */", (template_dir / "dashboard.css").read_text(encoding="utf-8"))
    page = page.replace("/* DASHBOARD_SCRIPT */", (template_dir / "dashboard.js").read_text(encoding="utf-8"))
    page = page.replace("DASHBOARD_DATA", payload)
    temporary = output.with_suffix(output.suffix + ".new")
    temporary.write_text(page, encoding="utf-8")
    temporary.replace(output)
