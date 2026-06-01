from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from opencc import OpenCC

MARKDOWN_LINK_PATTERN = re.compile(r'(!?\[[^\]]*\]\()([^\s)]+)([^)]*\))')
REFERENCE_LINK_PATTERN = re.compile(r'^(\s*\[[^\]]+\]:\s*)(\/\S+)', re.MULTILINE)
HTML_ATTR_PATTERN = re.compile(r'((?:href|src)=["\'])(\/[^"\']+)(["\'])')
FRONTMATTER_LINK_PATTERN = re.compile(r'^(\s*link:\s*)(\/\S+)(\s*)$', re.MULTILINE)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument('--locale', required=True)
    parser.add_argument('--source-locale', default='root')
    parser.add_argument('--config', default='s2twp')
    return parser.parse_args()


def apply_term_overrides(text: str, overrides: list[list[str]]) -> str:
    for source_text, target_text in overrides:
        text = text.replace(source_text, target_text)
    return text


def localize_path(locale_key: str, url: str) -> str:
    if not url.startswith('/'):
        return url

    locale_prefix = f'/{locale_key}'
    if url == locale_prefix or url == f'{locale_prefix}/' or url.startswith(f'{locale_prefix}/'):
        return url

    if url.startswith('/images/') or url.startswith('/favicon'):
        return url

    return f'{locale_prefix}/' if url == '/' else f'{locale_prefix}{url}'


def convert_markdown(content: str, locale_key: str, converter: OpenCC) -> str:
    content = MARKDOWN_LINK_PATTERN.sub(
        lambda match: f"{match.group(1)}{localize_path(locale_key, match.group(2))}{match.group(3)}"
        if match.group(2).startswith('/')
        else match.group(0),
        content,
    )
    content = REFERENCE_LINK_PATTERN.sub(
        lambda match: f"{match.group(1)}{localize_path(locale_key, match.group(2))}",
        content,
    )
    content = HTML_ATTR_PATTERN.sub(
        lambda match: f"{match.group(1)}{localize_path(locale_key, match.group(2))}{match.group(3)}",
        content,
    )
    content = FRONTMATTER_LINK_PATTERN.sub(
        lambda match: f"{match.group(1)}{localize_path(locale_key, match.group(2))}{match.group(3)}",
        content,
    )
    return converter.convert(content)


def convert_navigation_locale(node, source_locale: str, locale_key: str, converter: OpenCC):
    if isinstance(node, list):
        return [convert_navigation_locale(item, source_locale, locale_key, converter) for item in node]
    if isinstance(node, dict):
        converted = {}
        for key, value in node.items():
            if key == 'text' and isinstance(value, dict):
                converted_text = dict(value)
                source_text = value.get(source_locale) or value.get('root') or next(
                    (item for item in value.values() if isinstance(item, str)),
                    '',
                )
                converted_text[locale_key] = converter.convert(source_text)
                converted[key] = converted_text
            else:
                converted[key] = convert_navigation_locale(value, source_locale, locale_key, converter)
        return converted
    return node


def apply_navigation_term_overrides(node, locale_key: str, overrides: list[list[str]]):
    if not overrides:
        return node
    if isinstance(node, list):
        return [apply_navigation_term_overrides(item, locale_key, overrides) for item in node]
    if isinstance(node, dict):
        converted = {}
        for key, value in node.items():
            if key == locale_key and isinstance(value, str):
                converted[key] = apply_term_overrides(value, overrides)
            else:
                converted[key] = apply_navigation_term_overrides(value, locale_key, overrides)
        return converted
    return node


def collect_source_markdown_files(docs_root: Path, source_locale: str, locale_definitions: dict) -> list[tuple[Path, Path]]:
    if source_locale == 'root':
        excluded_entries = {'.vitepress', 'assets', 'public'}
        excluded_entries.update(
            definition.get('pathPrefix')
            for definition in locale_definitions.values()
            if definition.get('pathPrefix')
        )

        source_files: list[tuple[Path, Path]] = []
        for entry in sorted(docs_root.iterdir()):
            if entry.name in excluded_entries:
                continue

            if entry.is_dir():
                for markdown_path in sorted(entry.rglob('*.md')):
                    source_files.append((markdown_path, markdown_path.relative_to(docs_root)))
                continue

            if entry.is_file() and entry.suffix.lower() == '.md':
                source_files.append((entry, entry.relative_to(docs_root)))

        return source_files

    source_root = docs_root / source_locale
    return [
        (markdown_path, markdown_path.relative_to(source_root))
        for markdown_path in sorted(source_root.rglob('*.md'))
    ]


def main() -> None:
    args = parse_args()
    repo_root = Path(__file__).resolve().parents[3]
    docs_root = repo_root / 'docs'
    locale_root = repo_root / 'docs' / args.locale
    navigation_path = repo_root / 'docs' / '.vitepress' / 'i18n' / 'navigation-data.json'
    locale_definition_path = repo_root / 'docs' / '.vitepress' / 'i18n' / 'locale-definitions.json'
    term_override_path = repo_root / 'docs' / '.vitepress' / 'i18n' / 'term-overrides.json'
    converter = OpenCC(args.config)
    locale_definitions = json.loads(locale_definition_path.read_text(encoding='utf-8'))
    term_overrides = json.loads(term_override_path.read_text(encoding='utf-8')).get(args.locale, []) if term_override_path.exists() else []

    changed_markdown_files = 0
    for source_markdown_path, relative_path in collect_source_markdown_files(docs_root, args.source_locale, locale_definitions):
        original = source_markdown_path.read_text(encoding='utf-8')
        converted = apply_term_overrides(convert_markdown(original, args.locale, converter), term_overrides)
        target_markdown_path = locale_root / relative_path
        target_markdown_path.parent.mkdir(parents=True, exist_ok=True)

        current_target = target_markdown_path.read_text(encoding='utf-8') if target_markdown_path.exists() else None
        if converted != current_target:
            target_markdown_path.write_text(converted, encoding='utf-8')
            changed_markdown_files += 1

    navigation_data = json.loads(navigation_path.read_text(encoding='utf-8'))
    converted_navigation = convert_navigation_locale(navigation_data, args.source_locale, args.locale, converter)
    converted_navigation = apply_navigation_term_overrides(converted_navigation, args.locale, term_overrides)
    navigation_changed = converted_navigation != navigation_data
    if navigation_changed:
        navigation_path.write_text(json.dumps(converted_navigation, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    print({
        'locale': args.locale,
        'source_locale': args.source_locale,
        'markdown_files_changed': changed_markdown_files,
        'navigation_updated': navigation_changed,
    })


if __name__ == '__main__':
    main()
