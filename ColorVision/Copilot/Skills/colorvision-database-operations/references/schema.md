# ColorVision database table categories

Use the three categories defined by the application's reset, export, and cleanup code:

| Category | Code source | Purpose | Reference |
| --- | --- | --- | --- |
| Service settings | `MySqlLocalServicesManager.ServiceSettingTableNames` | Parameter templates and behavior selected by a service | `service-setting-tables.md` |
| Service configuration | `MySqlLocalServicesManager.ServiceConfigurationTableNames` | Service/device resources, hierarchy, identity, and licenses | `service-configuration-tables.md` |
| Results | `MySqlResultCleanupProvider.ResultTableNames` | Workflow, device measurement, and algorithm output data | `result-tables.md` |

`MigrationBackupTableNames` contains both service setting and service configuration tables. `WindowsServicePlugin.ServiceManager.MySqlServiceManager` uses the same list as the manual resource backup to preserve field templates and configuration before a database reset and restore them afterward. Result tables are not preserved and a fresh update database starts with empty result data.

The lists in code are authoritative for reset/export/cleanup behavior. Customer projects and plugins can add tables, so inspect the live schema when a requested table is not in these core lists.

## Tool calls

Use `QueryFlowExecutionStats` for common flow statistics:

```json
{"period":"today"}
```

Allowed periods are `today`, `yesterday`, and `last7days`.

Use `QueryDatabaseSql` for discovery and read-only SQL:

```json
{"sql":"DESCRIBE t_scgd_measure_batch","maxRows":100,"timeoutSeconds":15}
```

`sql` is required. `maxRows` accepts 1–500 and defaults to 100. `timeoutSeconds` accepts 1–30 and defaults to 15.

Use `ExecuteDatabaseSql` only for an explicitly requested, previewed service-configuration mutation or result cleanup:

```json
{"sql":"DELETE FROM t_scgd_measure_result_sensor WHERE batch_id = 12345","timeoutSeconds":30}
```

`timeoutSeconds` accepts 1–60 and defaults to 30. Every call opens native approval. The example is illustrative and must not be executed without an explicit request and verified scope.

Mutations referencing a service setting table are rejected before execution, including after approval.

## Discover an unknown table

```sql
SHOW TABLES
```

```sql
DESCRIBE table_name
```

```sql
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_KEY
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'table_name'
ORDER BY ORDINAL_POSITION
```

Classify from the table's role and current code usage, not its prefix alone. Never move a table into a safer category merely to reduce approval friction.
