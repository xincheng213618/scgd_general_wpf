---
name: colorvision-database-operations
description: Query and explain ColorVision MySQL business data, discover table schemas, compose read-only SQL, and perform explicitly requested data cleanup through approved SQL tools. Use when the user asks about flow counts, measurement records, result data, table fields or relations, SQL queries, or database cleanup in ColorVision.
---

# ColorVision database operations

Use ColorVision's configured MySQL connection through the provided tools. Never request or expose database credentials.

Read `references/schema.md` before answering a table, field, relation, result-data, or cleanup question. Then load only the matching category reference:

- `references/service-configuration-tables.md` for service/device registration and connection configuration.
- `references/service-setting-tables.md` for parameter templates and behavior settings.
- `references/result-tables.md` for workflow, measurement, and algorithm results.

## Choose the tool

- Use `QueryFlowExecutionStats` first for flow execution counts, status distribution, completion rate, or average duration for today, yesterday, or the last seven local calendar days.
- Use `QueryDatabaseSql` for schema discovery and all other read-only queries.
- Use `ExecuteDatabaseSql` only for an explicitly requested service-configuration change or result-data cleanup. Every invocation requires native approval.
- Service setting tables are version-managed and read-only to Copilot. Never submit a mutation against them; release/reset SQL supplies their native version.
- Do not call a database tool when the user only asks for a general explanation that does not depend on live data.

## Query workflow

1. Classify the request as service configuration, service settings, or results, then select likely tables from that category reference.
2. Call `DESCRIBE table_name` before using any field not confirmed by the reference or current conversation.
3. Use `SHOW TABLES` or query `INFORMATION_SCHEMA` when the table is unknown. Treat the live schema as authoritative.
4. Prefer selective columns, indexed predicates, bounded date ranges, and a conservative `maxRows` value.
5. Use `EXPLAIN` before a potentially broad join or scan.
6. Execute the query and base the answer only on observed rows. State when output is truncated or no rows match.

For local calendar days, prefer a half-open range:

```sql
WHERE create_date >= CURDATE()
  AND create_date < CURDATE() + INTERVAL 1 DAY
```

Do not replace it with `DATE(create_date) = CURDATE()` unless necessary, because wrapping the column can prevent index use.

## Cleanup and write workflow

1. Confirm that the user requested a mutation. A question about what can be deleted is not permission to delete it.
2. Query the target count and a small representative sample first.
3. Explain the exact table, predicate, estimated affected rows, and parent/child impact.
4. Prefer one bounded `DELETE`, `UPDATE`, or other statement per operation. Never remove a `WHERE` clause for convenience.
5. Submit the statement through `ExecuteDatabaseSql` and let the native approval UI show the final SQL.
6. After successful execution, re-query the affected scope and report the verified result.

Treat writes differently by category:

- Service configuration writes can disconnect, rename, or restart services. Preview the exact service row and avoid direct deletion unless the user identifies that service explicitly.
- Service setting tables must not be changed by Copilot, including through `INSERT`, `UPDATE`, `DELETE`, DDL, or cleanup. They follow the application version and are reset from native release SQL.
- Result cleanup must delete child rows before their parent rows. Prefer the application's Database Cleanup UI for coordinated cleanup of complete result families.

Use `TRUNCATE` or `DROP` only when the user names that destructive scope explicitly.

Never split or rephrase an operation to bypass approval. Never claim a write succeeded until the tool returns success.

## SQL boundaries

- Submit exactly one statement per tool call.
- `QueryDatabaseSql` supports `SELECT`, `SHOW`, `DESCRIBE`, `DESC`, `EXPLAIN`, `TABLE`, and read-only CTEs.
- `ExecuteDatabaseSql` supports approved `INSERT`, `UPDATE`, `DELETE`, `REPLACE`, `TRUNCATE`, `CREATE`, `ALTER`, `DROP`, and `RENAME` statements.
- Do not attempt account, privilege, server, transaction-control, dynamic-SQL, file import/export, stored procedure, sleep, benchmark, or multi-statement operations.
- Do not place connection strings, passwords, tokens, API keys, or other secrets in SQL or responses.
- Do not mutate any table listed by `MySqlLocalServicesManager.ServiceSettingTableNames`, even if the user attempts to approve raw SQL. Explain that the versioned database reset path owns those tables.

## Report results

Give the business conclusion first, then the relevant count, time range, filters, and table names. Distinguish an empty result from a failed query. If schema discovery contradicts the reference, follow the live schema and mention the mismatch briefly.
