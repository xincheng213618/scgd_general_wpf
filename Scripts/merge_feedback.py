"""Manually aggregate one machine's feedback snapshots; preview unless --apply is supplied.

Only Python's standard library is required. Original feedback is never written.
Database IDs stay unchanged: conflicting identities abort rather than renumbering
records and breaking result/node/message associations. See the ARVRPro guide.
"""
from __future__ import annotations

import argparse
from contextlib import closing
from datetime import datetime, timezone, timedelta
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import sqlite3
import sys
import uuid
import zipfile


DATABASES = ("ProjectARVRPro.db", "FlowNodeRecords.db", "SocketMessages.db", "MsgRecords.db")
KIND = "ColorVisionFeedbackAggregate"
IDENTITIES = {
    "arvrreuslt": ("sn", "batchid", "testtype", "model", "createtime"),
    "objectivetestresultrecord": ("sn", "createtime"),
    "flownoderecord": ("batch_id", "serial_number", "node_id", "start_time"),
    "flownodemessage": ("batch_id", "serial_number", "node_id", "msg_id", "send_time"),
    "flowrunrecord": ("template_id", "serial_number", "run_key", "started_time_utc"),
    "flowexecutionevent": ("run_record_id", "sequence_no", "event_key", "event_type", "occurred_time_utc"),
    "flownodeattempt": ("run_record_id", "node_id", "invocation_id", "attempt_no", "started_time_utc"),
    "flowincident": ("run_record_id", "incident_key", "kind", "detected_time_utc"),
    "flowtemplatesnapshot": ("template_id", "flow_key", "content_hash"),
    "socketmessage": ("messagetime", "direction", "msgid", "clientendpoint", "eventname"),
    "msgrecord": ("msgid", "sendtime", "sendtopic", "subscribetopic", "createtime"),
}


class MergeError(ValueError):
    pass


def quote(value: str) -> str:
    return '"' + value.replace('"', '""') + '"'


def contained(root: Path, relative: str) -> Path:
    candidate = (root / relative).resolve()
    if not candidate.is_relative_to(root.resolve()) or candidate == root.resolve():
        raise MergeError(f"路径越界：{relative}")
    return candidate


def sha256(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read_json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise MergeError(f"JSON 不是对象：{path}")
    return value


def write_json(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def received_time(metadata: dict, directory: Path) -> str:
    for key in ("serverReceivedAt", "createdAt"):
        if metadata.get(key):
            try:
                value = datetime.fromisoformat(metadata[key].replace("Z", "+00:00"))
                return value.replace(tzinfo=value.tzinfo or timezone.utc).astimezone(timezone.utc).isoformat(timespec="microseconds")
            except (ValueError, TypeError):
                continue
    match = re.match(r"^(\d{8}_\d{6})(_BJT)?_", directory.name)
    if not match:
        raise MergeError(f"反馈缺少接收时间：{directory}")
    zone = timezone(timedelta(hours=8)) if match[2] else timezone.utc
    return datetime.strptime(match[1], "%Y%m%d_%H%M%S").replace(tzinfo=zone).astimezone(timezone.utc).isoformat(timespec="microseconds")


def archive_entries(archive: zipfile.ZipFile) -> dict[str, zipfile.ZipInfo]:
    entries = {}
    for entry in archive.infolist():
        name = entry.filename.replace("\\", "/").rstrip("/")
        parts = PurePosixPath(name).parts
        if not parts or name.startswith("/") or any(p in (".", "..") or ":" in p or p.endswith((".", " ")) for p in parts):
            raise MergeError(f"ZIP 路径不安全：{entry.filename}")
        if any(re.fullmatch(r"(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?", p, re.I) for p in parts):
            raise MergeError(f"ZIP 路径包含保留名称：{entry.filename}")
        if (entry.external_attr >> 16) & 0o170000 == 0o120000:
            raise MergeError(f"ZIP 不支持符号链接：{entry.filename}")
        if not entry.is_dir():
            if name.lower() in entries:
                raise MergeError(f"ZIP 存在重名文件：{entry.filename}")
            entries[name.lower()] = entry
    return entries


def discover(machine_directory: Path, selected: list[str] | None = None) -> list[dict]:
    sources = []
    ids = set()
    for directory in sorted(machine_directory.iterdir()):
        metadata_path = directory / "feedback.json"
        if not directory.is_dir() or directory.name == "_aggregate" or not metadata_path.is_file():
            continue
        metadata = read_json(metadata_path)
        feedback_id = metadata.get("feedbackId")
        if not isinstance(feedback_id, str) or not re.fullmatch(r"[A-Za-z0-9_-]+", feedback_id):
            raise MergeError(f"反馈没有有效的原始 feedbackId：{directory}")
        if selected and feedback_id not in selected:
            continue
        if feedback_id in ids:
            raise MergeError(f"重复 feedbackId：{feedback_id}")
        ids.add(feedback_id)
        archives = list(directory.glob("*.zip"))
        if len(archives) != 1:
            raise MergeError(f"每份反馈需有且只有一个 ZIP：{directory}")
        with zipfile.ZipFile(archives[0]) as archive:
            entries = archive_entries(archive)
            system_infos = [e for e in entries.values() if PurePosixPath(e.filename.replace("\\", "/")).name == "SystemInfo.txt"]
            machine_names = {metadata["machineName"]} if metadata.get("machineName") else set()
            version = None
            for info in system_infos:
                content = archive.read(info).decode("utf-8-sig", errors="replace")
                machine_names.update(re.findall(r"^MachineName:\s*(.+?)\s*$", content, re.M))
                match = re.search(r"^Version:\s*(.+?)\s*$", content, re.M)
                if match:
                    version = match[1]
            if machine_names != {machine_directory.name}:
                raise MergeError(f"机器身份不一致或未知：{feedback_id}，检测到 {sorted(machine_names)}")
            databases = {}
            exports = {}
            for name in DATABASES:
                matches = [e for e in entries.values() if PurePosixPath(e.filename.replace("\\", "/")).name.lower() == name.lower()]
                if len(matches) > 1:
                    raise MergeError(f"反馈有多份 {name}：{feedback_id}")
                if matches:
                    entry = matches[0]
                    databases[name] = entry.filename
                    key = entry.filename.replace("\\", "/").lower()
                    if key + "-wal" in entries or key + "-journal" in entries:
                        raise MergeError(f"反馈数据库不是已完成的导出快照：{feedback_id}/{name}")
                    if key + ".export.json" in entries:
                        status = json.loads(archive.read(entries[key + ".export.json"]))
                        if status.get("Status") != "ok":
                            raise MergeError(f"数据库导出未成功：{feedback_id}/{name}")
                        exports[name] = {k: status.get(k) for k in ("From", "To", "RecentDays", "Tables", "MissingTables")}
        sources.append({"feedbackId": feedback_id, "zipPath": str(archives[0].resolve()),
                        "metadataPath": str(metadata_path.resolve()), "receivedAt": received_time(metadata, directory),
                        "versionAtCollection": version, "databases": databases, "exports": exports})
    if selected and set(selected) - ids:
        raise MergeError(f"找不到 feedbackId：{sorted(set(selected) - ids)}")
    if not sources:
        raise MergeError("没有可用的反馈包。")
    return sorted(sources, key=lambda s: (s["receivedAt"], s["feedbackId"]))


def load_manifest(root: Path, machine: str) -> dict | None:
    path = root / "aggregate.json"
    if not path.exists():
        return None
    manifest = read_json(path)
    if manifest.get("kind") != KIND or manifest.get("formatVersion") != 1 or manifest.get("machine") != machine:
        raise MergeError("聚合清单格式或机器身份不匹配。")
    directory = contained(root, manifest["databaseDirectory"])
    if not directory.is_dir() or not directory.is_relative_to(root.resolve() / "versions"):
        raise MergeError("聚合清单指向的版本不存在或越界。")
    return manifest


def affinity(declared: str) -> str:
    declared = declared.upper()
    if "INT" in declared:
        return "INTEGER"
    if any(word in declared for word in ("CHAR", "CLOB", "TEXT")):
        return "TEXT"
    if "BLOB" in declared or not declared:
        return "BLOB"
    if any(word in declared for word in ("REAL", "FLOA", "DOUB")):
        return "REAL"
    return "NUMERIC"


def columns(db: sqlite3.Connection, table: str, schema: str = "main") -> dict:
    return {row[1].lower(): row for row in db.execute(f"PRAGMA {schema}.table_info({quote(table)})")}


def merge_database(source: Path, destination: Path, feedback: dict) -> dict:
    """Set-based merging keeps large payloads in SQLite, not in Python memory."""
    report = {}
    with closing(sqlite3.connect(destination.resolve().as_uri() + "?mode=rwc", uri=True)) as db:
        db.execute("PRAGMA journal_mode=DELETE")
        db.execute("PRAGMA synchronous=FULL")
        db.execute("ATTACH DATABASE ? AS incoming", (source.as_uri() + "?mode=ro&immutable=1",))
        if db.execute("PRAGMA incoming.quick_check").fetchone()[0] != "ok":
            raise MergeError(f"源数据库校验失败：{source}")
        with db:
            db.execute("CREATE TABLE IF NOT EXISTS _aggregate_row_head(table_name TEXT,row_id INTEGER,feedback_id TEXT,received_at TEXT,PRIMARY KEY(table_name,row_id))")
            db.execute("CREATE TABLE IF NOT EXISTS _aggregate_row_source(table_name TEXT,row_id INTEGER,feedback_id TEXT,PRIMARY KEY(table_name,row_id,feedback_id))")
            tables = [r[0] for r in db.execute("SELECT name FROM incoming.sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]
            for table in tables:
                if table.startswith("_aggregate_"):
                    raise MergeError("输入中包含聚合内部表；请选择原始反馈包。")
                src = columns(db, table, "incoming")
                keys = [key for key, row in src.items() if row[5]]
                if keys != ["id"] or affinity(src["id"][2]) != "INTEGER":
                    raise MergeError(f"暂不支持此表的主键：{table}；未改写编号。")
                dst = columns(db, table)
                if not dst:
                    declarations = [f"{quote(row[1])} {affinity(row[2])}" + (" PRIMARY KEY" if key == "id" else "") for key, row in src.items()]
                    db.execute(f"CREATE TABLE {quote(table)}({','.join(declarations)})")
                else:
                    for key, row in src.items():
                        if key not in dst:
                            db.execute(f"ALTER TABLE {quote(table)} ADD COLUMN {quote(row[1])} {affinity(row[2])}")
                        elif affinity(row[2]) != affinity(dst[key][2]):
                            raise MergeError(f"字段类型变化：{table}.{row[1]}")
                dst = columns(db, table)
                common = [key for key in src if key in dst]
                identities = [key for key in IDENTITIES.get(table.lower(), tuple(common)) if key in src]
                has_time_identity = any("time" in key or key in ("content_hash", "run_key") for key in identities)
                legacy_run = table.lower() == "flowrunrecord" and "completed_time" in src
                if not identities or (table.lower() in IDENTITIES and not has_time_identity and not legacy_run):
                    raise MergeError(f"不足以确认重复记录身份：{table}")
                known_identity = table.lower() in IDENTITIES
                identity_diff = " OR ".join(f"(d.{quote(k)} IS NOT NULL AND s.{quote(k)} IS NOT NULL AND d.{quote(k)} != '' AND s.{quote(k)} != '' AND d.{quote(k)} IS NOT s.{quote(k)})" if known_identity else f"d.{quote(k)} IS NOT s.{quote(k)}" for k in identities)
                anchors = [f"(NULLIF(d.{quote(k)},'') IS NOT NULL AND NULLIF(s.{quote(k)},'') IS NOT NULL)" for k in identities if "time" in k or k in ("content_hash", "run_key")]
                if table.lower() == "flowrunrecord" and "completed_time" in src:
                    # Legacy serials include the precise start timestamp; completion may legitimately change.
                    strong = [f"(NULLIF(d.{quote(k)},'') IS NOT NULL AND NULLIF(s.{quote(k)},'') IS NOT NULL)" for k in ("run_key", "started_time_utc") if k in src]
                    if "serial_number" in src:
                        strong.append("(d.serial_number GLOB '*_[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]T[0-9]*' AND d.serial_number=s.serial_number)")
                    identity_diff += f" OR (NOT ({' OR '.join(strong) or '0'}) AND d.completed_time IS NOT s.completed_time)"
                    anchors.extend(strong)
                    anchors.append("(NULLIF(d.completed_time,'') IS NOT NULL AND NULLIF(s.completed_time,'') IS NOT NULL)")
                if known_identity:
                    identity_diff += f" OR NOT ({' OR '.join(anchors) or '0'})"
                conflict = db.execute(f"SELECT s.id FROM incoming.{quote(table)} s JOIN {quote(table)} d ON d.id=s.id WHERE {identity_diff} LIMIT 1").fetchone()
                if conflict:
                    raise MergeError(f"记录身份冲突：{feedback['feedbackId']}/{destination.name}/{table}/id={conflict[0]}。可能删库重建或来源不同，停止合并；不重新编号。")
                count = db.execute(f"SELECT COUNT(*) FROM incoming.{quote(table)}").fetchone()[0]
                duplicates = db.execute(f"SELECT COUNT(*) FROM incoming.{quote(table)} s JOIN {quote(table)} d ON d.id=s.id").fetchone()[0]
                changed = " OR ".join(f"d.{quote(k)} IS NOT s.{quote(k)}" for k in common if k != "id") or "0"
                table_key = table.lower()
                newer = "(h.received_at < ? OR (h.received_at=? AND h.feedback_id<=?))"
                order = (feedback["receivedAt"], feedback["receivedAt"], feedback["feedbackId"])
                updated = db.execute(f"SELECT COUNT(*) FROM incoming.{quote(table)} s JOIN {quote(table)} d ON d.id=s.id JOIN _aggregate_row_head h ON h.table_name=? AND h.row_id=d.id WHERE {newer} AND ({changed})", (table_key, *order)).fetchone()[0]
                names = ",".join(quote(row[1]) for row in src.values())
                assignments = ",".join(f"{quote(k)}=excluded.{quote(k)}" for k in common if k != "id")
                difference = " OR ".join(f"{quote(table)}.{quote(k)} IS NOT excluded.{quote(k)}" for k in common if k != "id") or "0"
                action = f"DO UPDATE SET {assignments} WHERE ({difference}) AND (SELECT {newer} FROM _aggregate_row_head h WHERE table_name=? AND row_id=excluded.id)" if assignments else "DO NOTHING"
                parameters = (*order, table_key) if assignments else ()
                db.execute(f"INSERT INTO {quote(table)}({names}) SELECT {names} FROM incoming.{quote(table)} WHERE 1 ON CONFLICT(id) {action}", parameters)
                db.execute(f"INSERT INTO _aggregate_row_head SELECT ?,id,?,? FROM incoming.{quote(table)} WHERE 1 ON CONFLICT(table_name,row_id) DO UPDATE SET feedback_id=excluded.feedback_id,received_at=excluded.received_at WHERE received_at<excluded.received_at OR (received_at=excluded.received_at AND feedback_id<=excluded.feedback_id)", (table_key, feedback["feedbackId"], feedback["receivedAt"]))
                db.execute(f"INSERT INTO _aggregate_row_source SELECT ?,id,? FROM incoming.{quote(table)} WHERE 1 ON CONFLICT DO NOTHING", (table_key, feedback["feedbackId"]))
                for index in db.execute(f"PRAGMA incoming.index_list({quote(table)})").fetchall():
                    if index[3] == "pk":
                        continue
                    parts = db.execute(f"PRAGMA incoming.index_xinfo({quote(index[1])})").fetchall()
                    keyed = [part for part in parts if part[5]]
                    if not keyed or any(part[2] is None for part in keyed) or index[4]:
                        continue  # Expression/partial indexes are not executed from source SQL.
                    fields = ",".join(quote(part[2]) + (" DESC" if part[3] else " ASC") for part in keyed)
                    db.execute(f"CREATE {'UNIQUE ' if index[2] else ''}INDEX IF NOT EXISTS {quote(index[1])} ON {quote(table)}({fields})")
                report[table] = {"read": count, "inserted": count - duplicates, "overlap": duplicates, "updated": updated}
    return report


def database_summary(path: Path) -> dict:
    with closing(sqlite3.connect(path.as_uri() + "?mode=ro&immutable=1", uri=True)) as db:
        if db.execute("PRAGMA quick_check").fetchone()[0] != "ok":
            raise MergeError(f"聚合数据库校验失败：{path}")
        tables = [r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '_aggregate_%'")]
        return {table: db.execute(f"SELECT COUNT(*) FROM {quote(table)}").fetchone()[0] for table in tables}


def apply(machine_directory: Path, sources: list[dict]) -> dict:
    root = machine_directory / "_aggregate"
    if not root.resolve().is_relative_to(machine_directory.resolve()):
        raise MergeError("聚合目录指向设备目录之外。")
    root.mkdir(exist_ok=True)
    lock = root / ".merge.lock"
    try:
        stream = lock.open("x", encoding="utf-8")
    except FileExistsError as error:
        raise MergeError(f"已有合并任务，或上次被中断：{lock}；确认无任务运行后手动移除此锁文件。") from error
    try:
        with stream:
            stream.write(f"PID={os.getpid()}\n{datetime.now(timezone.utc).isoformat()}\n")
        previous = load_manifest(root, machine_directory.name)
        imported = {s["feedbackId"]: s for s in previous["sources"]} if previous else {}
        pending = []
        for source in sources:
            source = dict(source)
            source["sha256"] = sha256(Path(source["zipPath"]))
            source["metadataSha256"] = sha256(Path(source["metadataPath"]))
            old = imported.get(source["feedbackId"])
            if old:
                if old["sha256"] != source["sha256"] or old["metadataSha256"] != source["metadataSha256"]:
                    raise MergeError(f"已经导入的原反馈内容变化：{source['feedbackId']}；停止，保留旧聚合。")
            else:
                pending.append(source)
        if not pending:
            print("没有新增反馈，聚合内容未改变。", flush=True)
            return previous
        token = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "_" + uuid.uuid4().hex[:8]
        stage = contained(root, ".pending_" + token)
        database_directory = stage / "Database"
        database_directory.mkdir(parents=True)
        if previous:
            old_directory = contained(root, previous["databaseDirectory"])
            for name in DATABASES:
                if (old_directory / name).exists():
                    shutil.copy2(old_directory / name, database_directory / name)
        for source in pending:
            print(f"导入 {source['feedbackId']} …", flush=True)
            source_directory = contained(root, "Sources/" + source["feedbackId"])
            if source_directory.exists():
                # A failed previous attempt may leave preserved evidence; never silently overwrite it.
                source_directory = contained(root, "Sources/" + source["feedbackId"] + "_" + token)
            source_directory.mkdir(parents=True)
            shutil.copy2(source["metadataPath"], source_directory / "feedback.json")
            source["evidenceDirectory"] = source_directory.relative_to(root).as_posix()
            source["mergeCounts"] = {}
            with zipfile.ZipFile(source["zipPath"]) as archive:
                entries = archive_entries(archive)
                database_members = {member.replace("\\", "/").lower(): name for name, member in source["databases"].items()}
                for key, entry in entries.items():
                    if key in database_members:
                        name = database_members[key]
                        temporary = stage / (name + ".import")
                        with archive.open(entry) as incoming, temporary.open("xb") as target:
                            shutil.copyfileobj(incoming, target)
                        source["mergeCounts"][name] = merge_database(temporary, database_directory / name, source)
                        temporary.unlink()
                    else:
                        destination = contained(source_directory, entry.filename.replace("\\", "/"))
                        destination.parent.mkdir(parents=True, exist_ok=True)
                        with archive.open(entry) as incoming, destination.open("xb") as target:
                            shutil.copyfileobj(incoming, target)
            if sha256(Path(source["zipPath"])) != source["sha256"] or sha256(Path(source["metadataPath"])) != source["metadataSha256"]:
                raise MergeError(f"导入期间原反馈发生变化：{source['feedbackId']}")
            imported[source["feedbackId"]] = source
        summaries = {name: database_summary(database_directory / name) for name in DATABASES if (database_directory / name).exists()}
        manifest = {"kind": KIND, "formatVersion": 1, "machine": machine_directory.name,
                    "updatedAt": datetime.now(timezone.utc).isoformat(), "databaseDirectory": f"versions/{token}/Database",
                    "sources": sorted(imported.values(), key=lambda s: (s["receivedAt"], s["feedbackId"])),
                    "tables": summaries, "policy": "Original IDs; same identity uses latest received snapshot; missing columns stay unavailable; identity conflict aborts."}
        write_json(stage / "generation.json", manifest)
        final = contained(root, "versions/" + token)
        final.parent.mkdir(exist_ok=True)
        stage.rename(final)
        pointer = root / (".aggregate_" + token + ".json")
        write_json(pointer, manifest)
        pointer.replace(root / "aggregate.json")  # Publish one pointer only after every database is validated.
        print(f"完成：{len(imported)} 份反馈。资料文件夹：{root}", flush=True)
        if "ProjectARVRPro.db" in summaries:
            print(f"打开现场数据：{final / 'Database' / 'ProjectARVRPro.db'}", flush=True)
        else:
            print("这些反馈尚无 ProjectARVRPro.db，可查日志/节点，但暂不能打开 ARVR 结果统计。", flush=True)
        return manifest
    finally:
        lock.unlink()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="手动聚合同一机器的现场反馈。默认只预览；--apply 才生成/更新 _aggregate。")
    parser.add_argument("machine_directory", type=Path)
    parser.add_argument("--feedback-id", action="append", help="仅选择指定原始 feedbackId，可重复；默认查看设备下全部反馈")
    parser.add_argument("--apply", action="store_true", help="手动生成或增量更新聚合目录；不修改原反馈")
    args = parser.parse_args(argv)
    try:
        machine_directory = args.machine_directory.resolve(strict=True)
        if not machine_directory.is_dir():
            raise MergeError("请选择一台设备的反馈目录。")
        sources = discover(machine_directory, args.feedback_id)
        manifest = load_manifest(machine_directory / "_aggregate", machine_directory.name)
        imported = {s["feedbackId"] for s in manifest["sources"]} if manifest else set()
        for source in sources:
            exports = [f"{name}: {value['From']} → {value['To']}" for name, value in source["exports"].items() if name == "ProjectARVRPro.db"]
            print(f"{'已导入' if source['feedbackId'] in imported else '待导入'} {source['feedbackId']} · {source['versionAtCollection']} · 数据库 {len(source['databases'])}")
            for line in exports:
                print("  " + line)
        if args.apply:
            apply(machine_directory, sources)
        else:
            print(f"仅预览：{len(sources)} 份反馈。需要合并时手动加 --apply；本次未写入文件。")
        return 0
    except (MergeError, OSError, sqlite3.Error, zipfile.BadZipFile, ValueError, KeyError) as error:
        print(f"合并未完成：{error}。原反馈和已发布聚合保持不变；若有 .pending_* 目录，它只用于排查失败。", file=sys.stderr)
        return 1


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8", errors="backslashreplace")
    raise SystemExit(main())
