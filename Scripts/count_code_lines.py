#!/usr/bin/env python3
"""Count code, comment, and blank lines in the ColorVision repository.

The script has no third-party dependencies. By default it scans tracked files
plus non-ignored working-tree files, so the result reflects the current checkout
while still respecting .gitignore.
"""

from __future__ import annotations

import argparse
import csv
import json
import subprocess
import sys
from dataclasses import asdict, dataclass
from io import StringIO
from pathlib import Path
from typing import Iterable, Sequence


@dataclass(frozen=True)
class Language:
    name: str
    line_comments: tuple[str, ...] = ()
    block_comments: tuple[tuple[str, str], ...] = ()
    quote_chars: str = "\"'"


@dataclass
class Count:
    files: int = 0
    code: int = 0
    comments: int = 0
    blanks: int = 0

    @property
    def lines(self) -> int:
        return self.code + self.comments + self.blanks

    def add(self, other: "Count") -> None:
        self.files += other.files
        self.code += other.code
        self.comments += other.comments
        self.blanks += other.blanks


C_STYLE = Language("", ("//",), (("/*", "*/"),))
XML_STYLE = Language("", (), (("<!--", "-->"),))

LANGUAGES: dict[str, Language] = {
    ".bat": Language("Batch", ("REM ", "::"), quote_chars='"'),
    ".c": Language("C", C_STYLE.line_comments, C_STYLE.block_comments),
    ".cc": Language("C++", C_STYLE.line_comments, C_STYLE.block_comments),
    ".cmake": Language("CMake", ("#",)),
    ".cpp": Language("C++", C_STYLE.line_comments, C_STYLE.block_comments),
    ".cs": Language("C#", C_STYLE.line_comments, C_STYLE.block_comments),
    ".cshtml": Language(
        "Razor",
        ("//",),
        (("<!--", "-->"), ("@*", "*@"), ("/*", "*/")),
    ),
    ".csproj": Language("MSBuild", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".css": Language("CSS", (), (("/*", "*/"),)),
    ".cxx": Language("C++", C_STYLE.line_comments, C_STYLE.block_comments),
    ".fs": Language("F#", ("//",), (("(*", "*)"),)),
    ".fsproj": Language("MSBuild", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".h": Language("C/C++ Header", C_STYLE.line_comments, C_STYLE.block_comments),
    ".hpp": Language("C/C++ Header", C_STYLE.line_comments, C_STYLE.block_comments),
    ".htm": Language("HTML", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".html": Language("HTML", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".inl": Language("C++", C_STYLE.line_comments, C_STYLE.block_comments),
    ".java": Language("Java", C_STYLE.line_comments, C_STYLE.block_comments),
    ".js": Language("JavaScript", C_STYLE.line_comments, C_STYLE.block_comments, "\"'`"),
    ".json": Language("JSON"),
    ".jsonc": Language("JSON", C_STYLE.line_comments, C_STYLE.block_comments),
    ".less": Language("LESS", C_STYLE.line_comments, C_STYLE.block_comments),
    ".md": Language("Markdown", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".mjs": Language("JavaScript", C_STYLE.line_comments, C_STYLE.block_comments, "\"'`"),
    ".props": Language("MSBuild", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".ps1": Language("PowerShell", ("#",), (("<#", "#>"),), "\"'"),
    ".psd1": Language("PowerShell", ("#",), (("<#", "#>"),), "\"'"),
    ".psm1": Language("PowerShell", ("#",), (("<#", "#>"),), "\"'"),
    ".py": Language("Python", ("#",)),
    ".razor": Language(
        "Razor",
        ("//",),
        (("<!--", "-->"), ("@*", "*@"), ("/*", "*/")),
    ),
    ".resx": Language("XML", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".scss": Language("SCSS", C_STYLE.line_comments, C_STYLE.block_comments),
    ".sh": Language("Shell", ("#",)),
    ".sln": Language("Visual Studio Solution", ("#",)),
    ".sql": Language("SQL", ("--",), (("/*", "*/"),)),
    ".targets": Language("MSBuild", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".toml": Language("TOML", ("#",)),
    ".ts": Language("TypeScript", C_STYLE.line_comments, C_STYLE.block_comments, "\"'`"),
    ".tsx": Language("TypeScript", C_STYLE.line_comments, C_STYLE.block_comments, "\"'`"),
    ".txt": Language("Text"),
    ".vb": Language("Visual Basic", ("'",), quote_chars='"'),
    ".vbproj": Language("MSBuild", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".vue": Language(
        "Vue",
        ("//",),
        (("<!--", "-->"), ("/*", "*/")),
        "\"'`",
    ),
    ".xaml": Language("XAML", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".xml": Language("XML", XML_STYLE.line_comments, XML_STYLE.block_comments),
    ".yml": Language("YAML", ("#",)),
    ".yaml": Language("YAML", ("#",)),
}

SPECIAL_NAMES: dict[str, Language] = {
    "cmakelists.txt": Language("CMake", ("#",)),
    "dockerfile": Language("Dockerfile", ("#",)),
    "makefile": Language("Makefile", ("#",)),
}

SKIP_DIRECTORIES = {
    ".git",
    ".vs",
    "artifacts",
    "bin",
    "node_modules",
    "obj",
    "packages",
    "testresults",
}

GENERATED_SUFFIXES = (
    ".assemblyattributes.cs",
    ".assemblyinfo.cs",
    ".designer.cs",
    ".g.cs",
    ".g.i.cs",
    ".generated.cs",
    ".min.css",
    ".min.js",
)


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    repo_root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "path",
        nargs="?",
        type=Path,
        default=repo_root,
        help=f"directory to scan (default: {repo_root})",
    )
    parser.add_argument(
        "--tracked-only",
        action="store_true",
        help="count only files already tracked by Git",
    )
    parser.add_argument(
        "--exclude-generated",
        action="store_true",
        help="exclude common generated and minified file names",
    )
    parser.add_argument(
        "--format",
        choices=("table", "json", "csv"),
        default="table",
        help="report format (default: table)",
    )
    parser.add_argument(
        "--output",
        type=Path,
        help="write the report to this file instead of stdout",
    )
    return parser.parse_args(argv)


def git_output(arguments: Sequence[str], cwd: Path) -> bytes | None:
    try:
        process = subprocess.run(
            ["git", "-C", str(cwd), *arguments],
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
        )
    except FileNotFoundError:
        return None
    return process.stdout if process.returncode == 0 else None


def git_files(root: Path, tracked_only: bool) -> list[Path] | None:
    top_level_bytes = git_output(("rev-parse", "--show-toplevel"), root)
    if top_level_bytes is None:
        return None

    top_level = Path(top_level_bytes.decode(errors="replace").strip()).resolve()
    try:
        relative_root = root.relative_to(top_level)
    except ValueError:
        return None

    arguments = ["ls-files", "-z", "--cached"]
    if not tracked_only:
        arguments.extend(("--others", "--exclude-standard"))
    arguments.append("--")
    if relative_root.parts:
        arguments.append(relative_root.as_posix())

    file_list = git_output(arguments, top_level)
    if file_list is None:
        return None
    return [
        top_level / item.decode(errors="surrogateescape")
        for item in file_list.split(b"\0")
        if item
    ]


def filesystem_files(root: Path) -> list[Path]:
    return [path for path in root.rglob("*") if path.is_file()]


def is_skipped(path: Path, root: Path, exclude_generated: bool) -> bool:
    try:
        relative = path.relative_to(root)
    except ValueError:
        return True
    if any(part.casefold() in SKIP_DIRECTORIES for part in relative.parts[:-1]):
        return True
    name = path.name.casefold()
    return exclude_generated and name.endswith(GENERATED_SUFFIXES)


def language_for(path: Path) -> Language | None:
    return SPECIAL_NAMES.get(path.name.casefold()) or LANGUAGES.get(path.suffix.casefold())


def decode_source(path: Path) -> str | None:
    try:
        data = path.read_bytes()
    except OSError:
        return None
    if b"\0" in data[:8192] and not data.startswith((b"\xff\xfe", b"\xfe\xff")):
        return None
    if data.startswith(b"\xff\xfe"):
        return data.decode("utf-16-le", errors="replace")
    if data.startswith(b"\xfe\xff"):
        return data.decode("utf-16-be", errors="replace")
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return data.decode("cp1252", errors="replace")


def marker_at(text: str, index: int, markers: Iterable[str]) -> str | None:
    for marker in markers:
        if text.startswith(marker, index):
            return marker
    return None


def block_marker_at(
    text: str, index: int, markers: Iterable[tuple[str, str]]
) -> tuple[str, str] | None:
    for start, end in markers:
        if text.startswith(start, index):
            return start, end
    return None


def count_text(text: str, language: Language) -> Count:
    result = Count(files=1)
    block_end: str | None = None

    for line in text.splitlines():
        if not line.strip() and block_end is None:
            result.blanks += 1
            continue

        has_code = False
        has_comment = False
        quote: str | None = None
        escaped = False
        index = 0

        while index < len(line):
            if block_end is not None:
                has_comment = True
                end_index = line.find(block_end, index)
                if end_index < 0:
                    index = len(line)
                    break
                index = end_index + len(block_end)
                block_end = None
                continue

            character = line[index]
            if quote is not None:
                has_code = True
                if escaped:
                    escaped = False
                elif character == "\\":
                    escaped = True
                elif character == quote:
                    quote = None
                index += 1
                continue

            line_marker = marker_at(line, index, language.line_comments)
            if line_marker is not None:
                has_comment = True
                break

            block_marker = block_marker_at(line, index, language.block_comments)
            if block_marker is not None:
                has_comment = True
                start, block_end = block_marker
                index += len(start)
                continue

            if character in language.quote_chars:
                quote = character
                has_code = True
            elif not character.isspace():
                has_code = True
            index += 1

        if has_code:
            result.code += 1
        elif has_comment:
            result.comments += 1
        else:
            result.blanks += 1

    return result


def collect_counts(
    root: Path,
    tracked_only: bool,
    exclude_generated: bool,
    output_path: Path | None,
) -> tuple[dict[str, Count], int]:
    paths = git_files(root, tracked_only)
    if paths is None:
        paths = filesystem_files(root)

    counts: dict[str, Count] = {}
    skipped_unrecognized = 0
    for path in paths:
        if output_path is not None and path.resolve() == output_path:
            continue
        if is_skipped(path, root, exclude_generated) or not path.is_file():
            continue
        language = language_for(path)
        if language is None:
            skipped_unrecognized += 1
            continue
        text = decode_source(path)
        if text is None:
            skipped_unrecognized += 1
            continue
        file_count = count_text(text, language)
        counts.setdefault(language.name, Count()).add(file_count)
    return counts, skipped_unrecognized


def total_count(counts: Iterable[Count]) -> Count:
    total = Count()
    for count in counts:
        total.add(count)
    return total


def table_report(root: Path, counts: dict[str, Count], skipped: int) -> str:
    rows = sorted(counts.items(), key=lambda item: (-item[1].code, item[0]))
    total = total_count(counts.values())
    language_width = max([len("Language"), len("TOTAL"), *(len(name) for name in counts)])
    columns = (
        ("Files", max(5, len(f"{total.files:,}"))),
        ("Code", max(4, len(f"{total.code:,}"))),
        ("Comments", max(8, len(f"{total.comments:,}"))),
        ("Blank", max(5, len(f"{total.blanks:,}"))),
        ("Total", max(5, len(f"{total.lines:,}"))),
    )

    def row(name: str, count: Count) -> str:
        values = (count.files, count.code, count.comments, count.blanks, count.lines)
        rendered = "  ".join(
            f"{value:>{width},}" for value, (_, width) in zip(values, columns)
        )
        return f"{name:<{language_width}}  {rendered}"

    header = "  ".join(f"{name:>{width}}" for name, width in columns)
    separator = "-" * (language_width + 2 + len(header))
    lines = [
        f"Code line statistics: {root}",
        f"{'Language':<{language_width}}  {header}",
        separator,
        *(row(name, count) for name, count in rows),
        separator,
        row("TOTAL", total),
        "",
        f"Recognized files: {total.files:,}; unrecognized/binary files skipped: {skipped:,}.",
    ]
    return "\n".join(lines)


def json_report(root: Path, counts: dict[str, Count], skipped: int) -> str:
    total = total_count(counts.values())

    def dictionary(count: Count) -> dict[str, int]:
        result = asdict(count)
        result["lines"] = count.lines
        return result

    document = {
        "root": str(root),
        "languages": {
            name: dictionary(count)
            for name, count in sorted(counts.items(), key=lambda item: item[0])
        },
        "total": dictionary(total),
        "skipped_unrecognized_or_binary_files": skipped,
    }
    return json.dumps(document, ensure_ascii=False, indent=2)


def csv_report(counts: dict[str, Count]) -> str:
    output = StringIO(newline="")
    writer = csv.writer(output, lineterminator="\n")
    writer.writerow(("Language", "Files", "Code", "Comments", "Blank", "Total"))
    for name, count in sorted(counts.items(), key=lambda item: (-item[1].code, item[0])):
        writer.writerow((name, count.files, count.code, count.comments, count.blanks, count.lines))
    total = total_count(counts.values())
    writer.writerow(("TOTAL", total.files, total.code, total.comments, total.blanks, total.lines))
    return output.getvalue().rstrip("\n")


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv or sys.argv[1:])
    root = args.path.resolve()
    if not root.is_dir():
        print(f"error: directory does not exist: {root}", file=sys.stderr)
        return 2

    output_path = args.output.resolve() if args.output else None
    counts, skipped = collect_counts(
        root, args.tracked_only, args.exclude_generated, output_path
    )
    if args.format == "json":
        report = json_report(root, counts, skipped)
    elif args.format == "csv":
        report = csv_report(counts)
    else:
        report = table_report(root, counts, skipped)

    if output_path:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(report + "\n", encoding="utf-8")
        print(f"Wrote code line report to {output_path}")
    else:
        print(report)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
