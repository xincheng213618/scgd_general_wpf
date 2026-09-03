# ColorVision database schema and tools

Use this reference to classify tables, choose a configured MySQL tool, and interpret its bounded output. A general explanation needs no live query; statements about the connected database require observed results.

## Table categories and reset scope

| Category | Code source | Purpose | Reference |
| --- | --- | --- | --- |
| Service settings | `MySqlLocalServicesManager.ServiceSettingTableNames` | Parameter templates and service behavior | [Service settings](service-setting-tables.md) |
| Service configuration | `MySqlLocalServicesManager.ServiceConfigurationTableNames` | Resources, hierarchy, identity, and licenses | [Service configuration](service-configuration-tables.md) |
| Results | `MySqlResultCleanupProvider.ResultTableNames` | Workflow, measurement, and algorithm outputs | [Results](result-tables.md) |

These lists define core preservation/cleanup scope, not every table in an installation. Plugins and customer projects can add tables. Classify by role and current code usage, not by prefix or desired approval level.

`MigrationBackupTableNames` combines the six service-setting and three service-configuration tables. The Service Manager reset path backs up data from listed base tables that actually exist, executes the version SQL, then restores preserved data. Manual resource backup uses the same core list but is a separate operation. This is selective preservation, not a complete backup or an atomic rollback. Results are outside the preservation list; their state after reset depends on the executed version SQL, not a universal empty-database guarantee.

## Confirm the live schema

The category references describe model mappings. They do not prove the connected database's columns, indexes, defaults, foreign keys, or data. For an unknown table use `SHOW TABLES`; use `DESCRIBE table_name` before relying on an unconfirmed field. Replace the placeholder with the verified name:

```sql
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_KEY
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'table_name'
ORDER BY ORDINAL_POSITION
```

If live discovery differs from a reference, report the mismatch. Do not reinterpret the write-protection category to make a rejected operation executable.

## Tool inputs

All tools use ColorVision's configured MySQL connection; they do not accept credentials or a separate connection string.

| Tool | Inputs and defaults | Use |
| --- | --- | --- |
| `QueryFlowExecutionStats` | `period`: `today`, `yesterday`, or `last7days` | Flow execution aggregates; application-local calendar days, with `last7days` including today and the preceding six days |
| `QueryDatabaseSql` | Required `sql`; `maxRows` 1–500, default 100; `timeoutSeconds` 1–30, default 15 | One read-only statement, including schema discovery |
| `ExecuteDatabaseSql` | Required `sql`; `timeoutSeconds` 1–60, default 30 | One explicitly requested service-configuration mutation or result cleanup through the native approval path |

Both SQL inputs are limited to 20,000 characters. The SQL policy in `SKILL.md` applies even to approved calls. SQL `CURDATE()` uses the database session date, which can differ from the application-local statistics range.

Read-only examples:

```json
{"period":"today"}
```

```json
{"sql":"DESCRIBE t_scgd_measure_batch","maxRows":100,"timeoutSeconds":15}
```

## Interpret query output

`QueryDatabaseSql` returns `rows_returned`, `truncated`, and bounded `data_tsv`, not a lossless export:

- At most `maxRows` rows and 100 columns are read into the result. Row or column clipping sets `truncated: true`.
- Cell text is flattened and clipped after 512 characters with an ellipsis. SQL NULL becomes `NULL`; binary values become a length marker.
- The row-output budget is 32,000 characters. The literal `<output truncated at 32000 characters>` marks omitted output rows; `rows_returned` still counts buffered rows, and the previously written `truncated` flag is not updated for this clipping.
- Therefore `truncated: false` does not prove complete text or complete displayed rows. Use narrower columns/predicates and explicit counts or aggregates for totals; increasing `maxRows` alone does not remove the other limits.
- Sensitive column-name markers and text redaction hide recognized secrets; SQL containing a recognized sensitive identifier can redact all cells. Arbitrary JSON in `txt_value` and encoded license `value` are not guaranteed safe by their column names. Select only necessary non-secret fields, never rename or transform secrets to evade redaction.

An empty successful result is different from a failed or unavailable query. Do not infer absent data from an error, redacted cell, or clipped output.

## Approved writes and verification

The following synthetic cleanup example is only a parameter shape. It requires an explicit request, confirmed schema, and a count/sample establishing this exact scope before submission:

```json
{"sql":"DELETE FROM t_scgd_measure_result_sensor WHERE batch_id = 12345","timeoutSeconds":30}
```

A write must pass native approval and SQL validation; a call does not guarantee a popup or execution. The approval presentation contains a redacted SQL preview clipped to 1,000 characters, not necessarily the complete statement. Present the exact non-secret statement and intended scope for review; do not assume a clipped preview establishes consent. Mutations referencing a service-setting table remain rejected after approval.

Ordinary DML uses a transaction; DDL may implicitly commit and cannot be rolled back by this tool. A successful `affected_rows` result reports the database operation, not service restart or runtime configuration refresh. Re-query the approved scope and distinguish database persistence from device state. After an error or interruption, establish current state before considering a retry.
