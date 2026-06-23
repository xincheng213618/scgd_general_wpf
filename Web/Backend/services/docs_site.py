"""Helpers for exposing the existing VitePress documentation site."""

from __future__ import annotations

import hashlib
import re
import time
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any


DOCS_BASE_PATH = "/scgd_general_wpf/"
DOCS_REDIRECT_PATH = "/docs"
DOCS_INDEX_CACHE_KEY = "docs_index:v1"
DOCS_INDEX_TTL_SECONDS = 60 * 60 * 24 * 30
DOCS_LOCALES = frozenset({"en", "ja", "ko", "zh-tw"})

DOCS_CATEGORY_LABELS = {
    "00-getting-started": "快速开始",
    "00-projects": "项目资料",
    "01-user-guide": "用户指南",
    "02-developer-guide": "开发指南",
    "03-architecture": "架构说明",
    "04-api-reference": "API 参考",
    "05-resources": "资源索引",
    "codex-skills": "Codex Skills",
}

DOCS_LOCALE_LABELS = {
    "zh-cn": "简体中文",
    "en": "English",
    "ja": "日本語",
    "ko": "한국어",
    "zh-tw": "繁體中文",
}


def repo_root() -> Path:
    return Path(__file__).resolve().parents[3]


def docs_source_dir() -> Path:
    return repo_root() / "docs"


def docs_dist_dir() -> Path:
    return docs_source_dir() / ".vitepress" / "dist"


def _safe_relative_path(doc_path: str | None) -> Path | None:
    clean = (doc_path or "").strip("/")
    if not clean:
        return Path("index.html")

    posix = PurePosixPath(clean)
    if posix.is_absolute() or any(part in ("", ".", "..") for part in posix.parts):
        return None
    return Path(*posix.parts)


def _is_inside(base: Path, candidate: Path) -> bool:
    try:
        candidate.resolve().relative_to(base.resolve())
        return True
    except ValueError:
        return False


def resolve_docs_site_file(doc_path: str | None) -> tuple[Path | None, int]:
    """Resolve VitePress clean URLs to files under docs/.vitepress/dist."""

    dist = docs_dist_dir()
    if not dist.is_dir():
        return None, 503

    rel = _safe_relative_path(doc_path)
    if rel is None:
        return _fallback_404(dist)

    target = dist / rel
    candidates: list[Path] = []

    if target.is_dir():
        candidates.extend([target / "index.html", target / "README.html"])
    candidates.append(target)
    if target.suffix == "":
        candidates.append(target.with_suffix(".html"))

    for candidate in candidates:
        if _is_inside(dist, candidate) and candidate.is_file():
            return candidate, 200

    return _fallback_404(dist)


def _fallback_404(dist: Path) -> tuple[Path | None, int]:
    fallback = dist / "404.html"
    if fallback.is_file():
        return fallback, 404
    return None, 404


def _iso_from_mtime(timestamp: float | None) -> str | None:
    if timestamp is None:
        return None
    return datetime.fromtimestamp(timestamp, tz=timezone.utc).isoformat()


def _latest_mtime(paths: list[Path]) -> float | None:
    latest: float | None = None
    for path in paths:
        try:
            mtime = path.stat().st_mtime
        except OSError:
            continue
        latest = mtime if latest is None else max(latest, mtime)
    return latest


def _source_markdown_files(source: Path) -> list[Path]:
    if not source.is_dir():
        return []
    files: list[Path] = []
    for path in source.rglob("*.md"):
        try:
            relative = path.relative_to(source)
        except ValueError:
            continue
        if ".vitepress" in relative.parts or "node_modules" in relative.parts:
            continue
        files.append(path)
    return files


def docs_index_signature(source: Path | None = None) -> str:
    source = source or docs_source_dir()
    hasher = hashlib.sha256()
    for path in sorted(_source_markdown_files(source), key=lambda item: item.as_posix().lower()):
        try:
            stat = path.stat()
            relative = path.relative_to(source).as_posix()
        except OSError:
            continue
        hasher.update(relative.encode("utf-8"))
        hasher.update(str(stat.st_size).encode("ascii"))
        hasher.update(str(stat.st_mtime_ns).encode("ascii"))
    return hasher.hexdigest()


def _strip_markdown_inline(text: str) -> str:
    value = re.sub(r"`([^`]+)`", r"\1", text)
    value = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", value)
    value = re.sub(r"[*_#]+", "", value)
    return value.strip()


def _read_doc_title(path: Path) -> str:
    try:
        for line in path.read_text(encoding="utf-8-sig").splitlines():
            match = re.match(r"^\s*#\s+(.+?)\s*$", line)
            if match:
                return _strip_markdown_inline(match.group(1))
    except (OSError, UnicodeDecodeError):
        pass
    return path.parent.name if path.stem.lower() == "readme" else path.stem


def _read_doc_excerpt(path: Path, title: str) -> str:
    try:
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except (OSError, UnicodeDecodeError):
        return ""

    pieces: list[str] = []
    in_frontmatter = False
    for index, line in enumerate(lines):
        stripped = line.strip()
        if index == 0 and stripped == "---":
            in_frontmatter = True
            continue
        if in_frontmatter:
            if stripped == "---":
                in_frontmatter = False
            continue
        if not stripped or stripped.startswith("#") or stripped.startswith("|"):
            continue
        text = _strip_markdown_inline(stripped)
        if text and text != title:
            pieces.append(text)
        if len(" ".join(pieces)) >= 140:
            break
    return " ".join(pieces)[:180]


def _doc_url(relative: Path) -> str:
    clean = relative.with_suffix("").as_posix()
    return f"{DOCS_BASE_PATH}{clean}"


def _doc_category(relative: Path) -> tuple[str, str, str]:
    parts = relative.parts
    locale = "zh-cn"
    content_parts = parts
    if parts and parts[0].lower() in DOCS_LOCALES:
        locale = parts[0].lower()
        content_parts = parts[1:]
    category = content_parts[0] if content_parts else "root"
    return locale, category, DOCS_CATEGORY_LABELS.get(category, category)


def _doc_item(source: Path, path: Path) -> dict[str, Any]:
    relative = path.relative_to(source)
    title = _read_doc_title(path)
    locale, category, category_label = _doc_category(relative)
    try:
        stat = path.stat()
        mtime = stat.st_mtime
        size = stat.st_size
    except OSError:
        mtime = 0
        size = 0
    return {
        "title": title,
        "excerpt": _read_doc_excerpt(path, title),
        "path": relative.as_posix(),
        "href": _doc_url(relative),
        "category": category,
        "categoryLabel": category_label,
        "locale": locale,
        "localeLabel": DOCS_LOCALE_LABELS.get(locale, locale),
        "modified": _iso_from_mtime(mtime),
        "modifiedTs": mtime,
        "size": size,
    }


def build_docs_index(source: Path | None = None) -> dict[str, Any]:
    source = source or docs_source_dir()
    items = [_doc_item(source, path) for path in _source_markdown_files(source)]
    items.sort(key=lambda item: (str(item["locale"]), str(item["category"]), str(item["path"]).lower()))

    category_counts: dict[str, int] = {}
    locale_counts: dict[str, int] = {}
    for item in items:
        category_counts[str(item["categoryLabel"])] = category_counts.get(str(item["categoryLabel"]), 0) + 1
        locale_counts[str(item["localeLabel"])] = locale_counts.get(str(item["localeLabel"]), 0) + 1

    recent = sorted(items, key=lambda item: float(item.get("modifiedTs") or 0), reverse=True)[:8]
    return {
        "items": items,
        "recent": recent,
        "summary": {
            "total": len(items),
            "categoryCounts": category_counts,
            "localeCounts": locale_counts,
        },
        "signature": docs_index_signature(source),
        "generatedAt": datetime.now(timezone.utc).isoformat(),
    }


def refresh_docs_index(cache) -> dict[str, Any]:
    from db_cache import now_iso
    from services.artifact_index import _update_index_state

    started = time.monotonic()
    started_at = now_iso()
    _update_index_state(cache, "docs", status="refreshing", started_at=started_at)
    try:
        index = build_docs_index()
        duration_ms = int((time.monotonic() - started) * 1000)
        cache.set_cache_entry(
            DOCS_INDEX_CACHE_KEY,
            index,
            ttl_seconds=DOCS_INDEX_TTL_SECONDS,
            signature=str(index["signature"]),
        )
        _update_index_state(
            cache,
            "docs",
            status="ready",
            signature=str(index["signature"]),
            finished_at=now_iso(),
            item_count=int(index["summary"]["total"]),
            duration_ms=duration_ms,
        )
        return {
            "status": "ok",
            "indexed_count": int(index["summary"]["total"]),
            "duration_ms": duration_ms,
            "signature": str(index["signature"]),
            "summary": index["summary"],
            "errors": [],
        }
    except Exception as exc:
        duration_ms = int((time.monotonic() - started) * 1000)
        _update_index_state(
            cache,
            "docs",
            status="error",
            finished_at=now_iso(),
            duration_ms=duration_ms,
            error=str(exc),
        )
        return {"status": "error", "indexed_count": 0, "duration_ms": duration_ms, "errors": [str(exc)]}


def get_docs_index(cache, *, refresh_if_missing: bool = True) -> dict[str, Any]:
    signature = docs_index_signature()
    cached = cache.get_cache_entry(DOCS_INDEX_CACHE_KEY, signature=signature)
    if cached:
        return dict(cached["value"], cacheUpdatedAt=cached.get("updated_at"), cacheHit=True)
    if refresh_if_missing:
        refresh_result = refresh_docs_index(cache)
        if refresh_result.get("status") == "ok":
            cached = cache.get_cache_entry(DOCS_INDEX_CACHE_KEY, signature=signature)
            if cached:
                return dict(cached["value"], cacheUpdatedAt=cached.get("updated_at"), cacheHit=False)
    return {
        "items": [],
        "recent": [],
        "summary": {"total": 0, "categoryCounts": {}, "localeCounts": {}},
        "signature": signature,
        "generatedAt": None,
        "cacheUpdatedAt": None,
        "cacheHit": False,
    }


def get_docs_index_snapshot(cache, *, refresh_if_missing: bool = False) -> dict[str, Any]:
    cached = cache.get_cache_entry(DOCS_INDEX_CACHE_KEY)
    if cached:
        return dict(cached["value"], cacheUpdatedAt=cached.get("updated_at"), cacheHit=True)
    if refresh_if_missing:
        return get_docs_index(cache, refresh_if_missing=True)
    return {
        "items": [],
        "recent": [],
        "summary": {"total": 0, "categoryCounts": {}, "localeCounts": {}},
        "signature": "",
        "generatedAt": None,
        "cacheUpdatedAt": None,
        "cacheHit": False,
    }


def build_docs_status(cache=None) -> dict[str, object]:
    source = docs_source_dir()
    dist = docs_dist_dir()

    markdown_files = _source_markdown_files(source)
    html_files = list(dist.rglob("*.html")) if dist.is_dir() else []
    manifest = dist / "docs-manifest.json"
    search_index = dist / "docs-search-index.json"
    index_html = dist / "index.html"

    build_files: list[Path] = [index_html, manifest, search_index]
    if not any(path.exists() for path in build_files):
        build_files = html_files
    if cache is not None:
        index = get_docs_index(cache, refresh_if_missing=True)
    else:
        index = build_docs_index(source)
    summary = dict(index.get("summary") or {})
    source_exists = source.is_dir()
    built = index_html.is_file()
    indexed_count = int(summary.get("total") or 0)
    search_ready = search_index.is_file()

    if not source_exists:
        health_status = "error"
        health_message = "未找到 docs 源目录，文档中心无法建立索引。"
        action_hint = "确认仓库根目录下存在 docs 文件夹。"
    elif not built:
        health_status = "warning"
        health_message = "文档源文件存在，但 VitePress 静态站点还没有构建。"
        action_hint = "在仓库根目录执行 npm run docs:build，或使用 Web/Run-Web.ps1 自动检查。"
    elif indexed_count <= 0:
        health_status = "warning"
        health_message = "文档站已经构建，但后台文档索引为空。"
        action_hint = "点击刷新文档索引，或检查 Markdown 文件编码。"
    elif not search_ready:
        health_status = "warning"
        health_message = "文档站可访问，但搜索索引文件未生成。"
        action_hint = "重新执行 npm run docs:build。"
    else:
        health_status = "ok"
        health_message = "文档源文件、静态站点和后台索引都已就绪。"
        action_hint = ""

    return {
        "basePath": DOCS_BASE_PATH,
        "entryUrl": DOCS_BASE_PATH,
        "redirectUrl": DOCS_REDIRECT_PATH,
        "sourcePath": str(source),
        "distPath": str(dist),
        "sourceExists": source_exists,
        "built": built,
        "healthStatus": health_status,
        "healthMessage": health_message,
        "actionHint": action_hint,
        "buildCommand": "npm run docs:build",
        "sourceDocumentCount": len(markdown_files),
        "builtPageCount": len(html_files),
        "lastSourceUpdate": _iso_from_mtime(_latest_mtime(markdown_files)),
        "lastBuildUpdate": _iso_from_mtime(_latest_mtime(build_files)),
        "manifestExists": manifest.is_file(),
        "manifestSizeBytes": manifest.stat().st_size if manifest.is_file() else 0,
        "searchIndexExists": search_index.is_file(),
        "searchIndexSizeBytes": search_index.stat().st_size if search_index.is_file() else 0,
        "indexCached": bool(index.get("cacheHit")),
        "indexedDocumentCount": indexed_count,
        "indexUpdatedAt": index.get("cacheUpdatedAt") or index.get("generatedAt"),
        "categoryCounts": summary.get("categoryCounts") or {},
        "localeCounts": summary.get("localeCounts") or {},
        "recentDocuments": index.get("recent") or [],
    }
