from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]

CODE_EXTENSIONS = {
    ".cs",
    ".xaml",
    ".csproj",
    ".props",
    ".targets",
    ".sln",
    ".py",
    ".ps1",
    ".bat",
    ".cmd",
    ".cpp",
    ".c",
    ".h",
    ".hpp",
    ".cc",
    ".hh",
    ".inl",
    ".natvis",
    ".java",
    ".kt",
    ".gradle",
    ".groovy",
    ".json",
    ".jsonc",
    ".yaml",
    ".yml",
    ".toml",
    ".xml",
    ".config",
    ".md",
    ".txt",
    ".sql",
    ".csv",
    ".ts",
    ".tsx",
    ".js",
    ".jsx",
    ".css",
    ".scss",
    ".html",
    ".htm",
    ".vue",
    ".sh",
    ".cmake",
    ".rc",
    ".def",
}

EXECUTABLE_CODE_EXTENSIONS = {
    ".cs",
    ".xaml",
    ".py",
    ".ps1",
    ".bat",
    ".cmd",
    ".cpp",
    ".c",
    ".h",
    ".hpp",
    ".cc",
    ".hh",
    ".inl",
    ".java",
    ".kt",
    ".gradle",
    ".groovy",
    ".sql",
    ".ts",
    ".tsx",
    ".js",
    ".jsx",
    ".css",
    ".scss",
    ".html",
    ".htm",
    ".vue",
    ".sh",
    ".cmake",
    ".rc",
    ".def",
}

SOURCE_EXCLUDED_DIR_NAMES = {
    ".git",
    ".github",
    ".vs",
    ".vscode",
    ".venv",
    "bin",
    "obj",
    "__pycache__",
    "node_modules",
    "packages",
    "x64",
    "Release",
    "Debug",
}

SOURCE_EXCLUDED_TOP_LEVEL_DIRS = {
    "packages",
    "x64",
    "Release",
}

REPO_EXCLUDED_DIR_NAMES = {
    ".git",
    ".vs",
    ".venv",
    "__pycache__",
    "node_modules",
}


@dataclass
class ScopeStats:
    name: str
    file_count: int = 0
    total_lines: int = 0
    non_empty_lines: int = 0
    executable_lines: int = 0
    executable_non_empty_lines: int = 0
    total_bytes: int = 0


def should_skip_dir(path: Path, excluded_dir_names: set[str], excluded_top_level_dirs: set[str]) -> bool:
    if path == REPO_ROOT:
        return False

    if path.name in excluded_dir_names:
        return True

    try:
        relative = path.relative_to(REPO_ROOT)
    except ValueError:
        return False

    first_part = relative.parts[0] if relative.parts else ""
    return first_part in excluded_top_level_dirs


def count_lines(path: Path) -> tuple[int, int]:
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        text = path.read_text(encoding="utf-8-sig")

    lines = text.splitlines()
    non_empty = sum(1 for line in lines if line.strip())
    return len(lines), non_empty


def analyze_scope(
    name: str,
    included_extensions: set[str],
    excluded_dir_names: set[str],
    excluded_top_level_dirs: set[str],
) -> tuple[ScopeStats, Counter[str], Counter[str], Counter[str]]:
    stats = ScopeStats(name=name)
    extension_counter: Counter[str] = Counter()
    directory_total_lines: Counter[str] = Counter()
    directory_executable_lines: Counter[str] = Counter()

    for path in REPO_ROOT.rglob("*"):
        if path.is_dir() and should_skip_dir(path, excluded_dir_names, excluded_top_level_dirs):
            continue
        if not path.is_file():
            continue

        if any(should_skip_dir(parent, excluded_dir_names, excluded_top_level_dirs) for parent in path.parents):
            continue

        suffix = path.suffix.lower()
        if suffix not in included_extensions:
            continue

        try:
            total_lines, non_empty_lines = count_lines(path)
        except (OSError, UnicodeDecodeError):
            continue

        relative = path.relative_to(REPO_ROOT)
        top_level = relative.parts[0] if relative.parts else "."

        stats.file_count += 1
        stats.total_lines += total_lines
        stats.non_empty_lines += non_empty_lines
        stats.total_bytes += path.stat().st_size
        extension_counter[suffix] += total_lines
        directory_total_lines[top_level] += total_lines

        if suffix in EXECUTABLE_CODE_EXTENSIONS:
            stats.executable_lines += total_lines
            stats.executable_non_empty_lines += non_empty_lines
            directory_executable_lines[top_level] += total_lines

    return stats, extension_counter, directory_total_lines, directory_executable_lines


def format_bytes(size: int) -> str:
    units = ["B", "KB", "MB", "GB", "TB"]
    value = float(size)
    for unit in units:
        if value < 1024 or unit == units[-1]:
            return f"{value:.2f} {unit}"
        value /= 1024
    return f"{value:.2f} TB"


def classify_scale(executable_lines: int, project_count: int) -> str:
    if executable_lines >= 300_000 or project_count >= 80:
        return "超大型项目"
    if executable_lines >= 120_000 or project_count >= 35:
        return "大型项目"
    if executable_lines >= 40_000 or project_count >= 15:
        return "中大型项目"
    if executable_lines >= 12_000 or project_count >= 5:
        return "中型项目"
    return "小型项目"


def print_scope_report(
    stats: ScopeStats,
    extension_counter: Counter[str],
    directory_total_lines: Counter[str],
    directory_executable_lines: Counter[str],
    project_count: int,
) -> None:
    print(f"=== {stats.name} ===")
    print(f"文本/代码文件数: {stats.file_count}")
    print(f"总代码行数: {stats.total_lines:,}")
    print(f"非空代码行数: {stats.non_empty_lines:,}")
    print(f"可执行源码行数: {stats.executable_lines:,}")
    print(f"可执行非空源码行数: {stats.executable_non_empty_lines:,}")
    print(f"文本体量: {format_bytes(stats.total_bytes)}")
    print(f"项目文件数(.csproj/.vcxproj/.fsproj/.vbproj): {project_count}")
    print(f"规模判断: {classify_scale(stats.executable_lines, project_count)}")
    print()

    print("按顶层目录统计(总代码行 Top 10):")
    for name, lines in directory_total_lines.most_common(10):
        executable = directory_executable_lines.get(name, 0)
        print(f"  {name:<18} total={lines:>8,}  executable={executable:>8,}")
    print()

    print("按扩展名统计(总代码行 Top 12):")
    for suffix, lines in extension_counter.most_common(12):
        print(f"  {suffix or '<no-ext>':<10} {lines:>8,}")
    print()


def count_project_files() -> int:
    patterns = ["*.csproj", "*.vcxproj", "*.fsproj", "*.vbproj"]
    count = 0
    for pattern in patterns:
        count += sum(1 for _ in REPO_ROOT.rglob(pattern))
    return count


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="统计仓库规模并给出粗略项目体量判断")
    parser.add_argument(
        "--repo-only",
        action="store_true",
        help="只输出仓库视角统计，不输出源码视角统计",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    project_count = count_project_files()

    if not args.repo_only:
        source_stats = analyze_scope(
            name="源码视角(排除 bin/obj/依赖包/发布产物)",
            included_extensions=CODE_EXTENSIONS,
            excluded_dir_names=SOURCE_EXCLUDED_DIR_NAMES,
            excluded_top_level_dirs=SOURCE_EXCLUDED_TOP_LEVEL_DIRS,
        )
        print_scope_report(*source_stats, project_count)

    repo_stats = analyze_scope(
        name="仓库视角(仅排除缓存目录)",
        included_extensions=CODE_EXTENSIONS,
        excluded_dir_names=REPO_EXCLUDED_DIR_NAMES,
        excluded_top_level_dirs=set(),
    )
    print_scope_report(*repo_stats, project_count)


if __name__ == "__main__":
    main()