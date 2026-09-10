#!/usr/bin/env python3
"""Archive exact release notes and keep the public CHANGELOG intentionally short."""

from __future__ import annotations

import argparse
import re
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path


HEADER_RE = re.compile(
    r"^##\s*\[(?P<version>\d+(?:\.\d+){3})\]\s+"
    r"(?P<date>\d{4}[.-]\d{2}[.-]\d{2})\s*$",
    re.MULTILINE,
)
ITEM_RE = re.compile(r"^\d+[.)、．]\s*\S", re.MULTILINE)
HISTORY_PREAMBLE = "#   CHANGELOG\n\n"


class ChangelogFormatError(ValueError):
    """Raised when changelog input would make the archive ambiguous or lossy."""


@dataclass(frozen=True)
class ReleaseSection:
    version: str
    date: str
    markdown: str


@dataclass(frozen=True)
class ArchiveSummary:
    release_count: int
    added_count: int
    preserved_count: int
    latest_version: str
    latest_item_count: int


def version_key(version: str) -> tuple[int, ...]:
    parts = tuple(int(part) for part in version.split("."))
    if len(parts) != 4:
        raise ChangelogFormatError(f"Expected a four-part version, got {version!r}.")
    return parts


def _normalize_section(markdown: str) -> str:
    return markdown.replace("\r\n", "\n").replace("\r", "\n").strip()


def parse_release_sections(text: str) -> list[ReleaseSection]:
    matches = list(HEADER_RE.finditer(text))
    if not matches:
        raise ChangelogFormatError("No dated four-part release headings were found.")

    sections: list[ReleaseSection] = []
    seen_versions: set[str] = set()
    for index, match in enumerate(matches):
        version = match.group("version")
        if version in seen_versions:
            raise ChangelogFormatError(f"Duplicate release heading: {version}.")
        seen_versions.add(version)
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        markdown = _normalize_section(text[match.start() : end]) + "\n"
        sections.append(
            ReleaseSection(
                version=version,
                date=match.group("date"),
                markdown=markdown,
            )
        )
    return sections


def _merge_sections(
    source_sections: list[ReleaseSection],
    history_sections: list[ReleaseSection],
) -> tuple[list[ReleaseSection], int, int]:
    archived = {section.version: section for section in history_sections}
    added_count = 0
    preserved_count = 0
    for section in source_sections:
        existing = archived.get(section.version)
        if existing is None:
            archived[section.version] = section
            added_count += 1
            continue
        if _normalize_section(existing.markdown) != _normalize_section(section.markdown):
            preserved_count += 1
    return (
        sorted(archived.values(), key=lambda item: version_key(item.version), reverse=True),
        added_count,
        preserved_count,
    )


def build_changelog_files(
    source_text: str,
    history_text: str | None = None,
) -> tuple[str, str, ArchiveSummary]:
    source_sections = parse_release_sections(source_text)
    history_sections = parse_release_sections(history_text) if history_text else []
    archived_sections, added_count, preserved_count = _merge_sections(source_sections, history_sections)

    latest = max(source_sections, key=lambda item: version_key(item.version))
    latest_item_count = len(ITEM_RE.findall(latest.markdown))
    if not 1 <= latest_item_count <= 3:
        raise ChangelogFormatError(
            f"Public release {latest.version} must contain one to three top-level numbered items; "
            f"found {latest_item_count}."
        )

    public_text = f"# CHANGELOG\n\n{latest.markdown.rstrip()}\n"
    history_body = "\n\n".join(section.markdown.rstrip() for section in archived_sections)
    rendered_history = HISTORY_PREAMBLE + history_body + "\n"
    summary = ArchiveSummary(
        release_count=len(archived_sections),
        added_count=added_count,
        preserved_count=preserved_count,
        latest_version=latest.version,
        latest_item_count=latest_item_count,
    )
    return public_text, rendered_history, summary


def _atomic_write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(
        mode="w",
        encoding="utf-8",
        newline="\n",
        dir=path.parent,
        prefix=f".{path.name}.",
        suffix=".tmp",
        delete=False,
    ) as stream:
        stream.write(text)
        temporary_path = Path(stream.name)
    temporary_path.replace(path)


def _print_summary(summary: ArchiveSummary) -> None:
    print(
        f"History contains {summary.release_count} exact release(s); "
        f"added {summary.added_count}; preserved {summary.preserved_count} archived version(s); "
        f"public CHANGELOG keeps "
        f"{summary.latest_version} with {summary.latest_item_count} item(s)."
    )


def main(argv: list[str] | None = None) -> int:
    repo_root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "path",
        nargs="?",
        type=Path,
        default=repo_root / "CHANGELOG.md",
        help="Public CHANGELOG path (defaults to the repository root CHANGELOG.md).",
    )
    parser.add_argument(
        "--history-path",
        type=Path,
        default=repo_root / "docs" / "_history" / "CHANGELOG.md",
        help="Private release archive path (defaults to docs/_history/CHANGELOG.md).",
    )
    parser.add_argument(
        "--source-path",
        type=Path,
        help="Optional migration source; defaults to the public CHANGELOG path.",
    )
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true", help="Archive releases, then compact the public file.")
    mode.add_argument("--check", action="store_true", help="Check both public and historical files.")
    args = parser.parse_args(argv)

    source_path = args.source_path or args.path
    try:
        source_text = source_path.read_text(encoding="utf-8")
        history_text = args.history_path.read_text(encoding="utf-8") if args.history_path.is_file() else None
        public_text, rendered_history, summary = build_changelog_files(source_text, history_text)
    except (OSError, UnicodeError, ChangelogFormatError) as exc:
        print(f"CHANGELOG archival failed: {exc}", file=sys.stderr)
        return 2

    _print_summary(summary)
    if args.check:
        public_current = args.path.is_file() and args.path.read_text(encoding="utf-8") == public_text
        history_current = history_text == rendered_history
        if public_current and history_current:
            print("Public CHANGELOG and History are current.")
            return 0
        stale = []
        if not public_current:
            stale.append(str(args.path))
        if not history_current:
            stale.append(str(args.history_path))
        print("CHANGELOG archival is not current: " + ", ".join(stale), file=sys.stderr)
        return 1

    if history_text != rendered_history:
        _atomic_write(args.history_path, rendered_history)
        print(f"Updated History first: {args.history_path}")
    else:
        print(f"History is already current: {args.history_path}")

    public_current = args.path.is_file() and args.path.read_text(encoding="utf-8") == public_text
    if not public_current:
        _atomic_write(args.path, public_text)
        print(f"Updated public CHANGELOG: {args.path}")
    else:
        print(f"Public CHANGELOG is already current: {args.path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
