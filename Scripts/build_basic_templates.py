"""Build the shipped, read-only examples catalog from reviewable JSON (stdlib only)."""
import argparse
import json
import sqlite3
from contextlib import closing
from pathlib import Path

ASSETS = Path(__file__).resolve().parents[1] / "Engine/ColorVision.Engine/Assets/Templates"
COLUMNS = ("id", "template_code", "title", "description", "category", "suggested_name", "module_guide", "device_guide", "purpose")
RUNTIME_PATH = "Assets/Templates/BasicTemplates.db"


def validate_runtime(runtime: Path, installed_files: set[str]) -> None:
    path = runtime / RUNTIME_PATH
    if not path.is_file():
        raise ValueError(f"Missing built-in template catalog: {path}")
    if RUNTIME_PATH.casefold() not in {item.casefold() for item in installed_files}:
        raise ValueError(f"Advanced Installer does not deliver {RUNTIME_PATH} at its runtime path")
    try:
        with closing(sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True)) as db:
            if db.execute("PRAGMA quick_check").fetchone()[0] != "ok":
                raise ValueError("Built-in template catalog integrity check failed")
            if db.execute("PRAGMA user_version").fetchone()[0] != 1:
                raise ValueError("Unsupported built-in template catalog schema")
            if db.execute("SELECT COUNT(*) FROM template_preset").fetchone()[0] == 0:
                raise ValueError("Built-in template catalog is empty")
    except sqlite3.Error as exc:
        raise ValueError(f"Invalid built-in template catalog: {exc}") from exc


def build(source: Path, output: Path) -> None:
    catalog = json.loads(source.read_text(encoding="utf-8"))
    if catalog["schema_version"] != 1:
        raise ValueError("Unsupported catalog schema")
    # Construct completely in memory so an invalid source never replaces the shipped file.
    with closing(sqlite3.connect(":memory:")) as db:
        db.executescript("""
            PRAGMA user_version = 1;
            CREATE TABLE catalog_metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE template_preset (
                id TEXT PRIMARY KEY, template_code TEXT NOT NULL, title TEXT NOT NULL,
                description TEXT NOT NULL, category TEXT NOT NULL, suggested_name TEXT NOT NULL,
                module_guide TEXT NOT NULL, device_guide TEXT NOT NULL, purpose TEXT NOT NULL,
                payload_json TEXT NOT NULL, sort_order INTEGER NOT NULL);
            CREATE INDEX ix_preset_type ON template_preset(template_code, sort_order);
        """)
        db.execute("INSERT INTO catalog_metadata VALUES ('content_version', ?)", (catalog["content_version"],))
        for order, preset in enumerate(catalog["presets"]):
            for column in COLUMNS:
                if not isinstance(preset[column], str) or not preset[column].strip():
                    raise ValueError(f"Invalid {column} in preset {order}")
            if not isinstance(preset["payload"], dict):
                raise ValueError(f"Invalid payload in preset {order}")
            values = tuple(preset[column] for column in COLUMNS)
            db.execute("INSERT INTO template_preset VALUES (?,?,?,?,?,?,?,?,?,?,?)",
                       (*values, json.dumps(preset["payload"], ensure_ascii=False, separators=(",", ":")), order))
        db.commit()
        output.parent.mkdir(parents=True, exist_ok=True)
        # serialize gives stable bytes and avoids SQLite sidecars in the repository.
        output.write_bytes(db.serialize())
    print(f"Built {len(catalog['presets'])} presets: {output} ({output.stat().st_size} bytes)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ASSETS / "BasicTemplates.json")
    parser.add_argument("--output", type=Path, default=ASSETS / "BasicTemplates.db")
    args = parser.parse_args()
    build(args.source, args.output)
