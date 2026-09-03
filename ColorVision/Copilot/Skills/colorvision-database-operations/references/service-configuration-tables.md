# Service configuration tables

The three tables in `MySqlLocalServicesManager.ServiceConfigurationTableNames` hold resources, group membership, and licenses. They are candidates for selective reset preservation described in [Schema and tools](schema.md), not a complete recoverable device backup.

Fields below follow `SysResourceModel`, `SysResourceGoupModel` (source spelling), and `LicenseModel`. Their inherited `id` is mapped as an identity primary key; model annotations and CLR defaults do not prove live DDL or foreign-key enforcement. Confirm the connected schema before using unverified fields or changing relationships.

## `t_scgd_sys_resource`

| Column | Meaning |
| --- | --- |
| `id` | Resource identifier |
| `name`, `code` | Display name and mutable service identity; neither implies database-wide uniqueness |
| `type` | `ServiceTypes` value, not a resource ID |
| `pid` | Nullable parent resource ID |
| `txt_value` | Typed configuration JSON; may contain connection secrets |
| `create_date` | Creation-time field |
| `is_enable`, `is_delete` | Stored enable/delete flags; load paths do not apply them uniformly |
| `tenant_id` | Stored tenant value; not universal tenant isolation |
| `remark` | Description |

`DeviceService<T>` deserializes `txt_value` and then takes `Config.Code` and `Config.Name` from the resource columns. Saving copies the configuration's Code/Name and the complete serialized configuration back to the row. A SQL update to only one representation can leave conflicting values; an existing runtime object can later save its old configuration over the SQL change.

For the normal `ServiceManager.LoadServices` tree:

| Resource path | Filters |
| --- | --- |
| Root terminal | `pid IS NULL`, `tenant_id = 0`, `is_delete = 0`; no enable check |
| Terminal's direct device | Matching parent, enabled, not deleted, tenant 0 |
| Device's child resource | Matching parent, enabled, not deleted; no tenant check |
| Group-linked target | Matching referenced ID; no enable/delete/tenant/parent filter |

These are loading rules, not a recommendation to manipulate flags. An inventory query cannot prove visibility, connection health, or that a deleted/disabled record is excluded from every path. Do not select or echo `txt_value` unless an explicitly needed non-secret property can be isolated safely.

## `t_scgd_sys_resource_group`

| Column | Meaning |
| --- | --- |
| `id` | Link-row identifier |
| `resource_id` | Member resource's `t_scgd_sys_resource.id` |
| `group_id` | Group resource's `t_scgd_sys_resource.id` |

The model's `Group` and `Resourced` properties are ignored by ORM mapping, not additional columns. Verify both endpoints and existing links before a change; the mapping does not establish a SQL foreign key, uniqueness constraint, or automatic cascade cleanup.

## `t_scgd_camera_license`

| Column | Meaning |
| --- | --- |
| `id` | License-row identifier |
| `res_dev_cam_pid`, `res_dev_cali_pid` | Nullable camera/calibration resource IDs, not hardware serial numbers |
| `lic_type` | License-type value; do not invent meanings for numeric values |
| `value` | Encoded license payload; sensitive |
| `model`, `mac_sn` | Model and MAC/SN metadata |
| `expired` | Stored expiry metadata; not the sole source of the UI license state |
| `customer_name`, `create_date` | Customer and creation-time fields |

`PhyCamera.LicenseState` compares the decoded payload's `ColorVisionLicense.ExpiryDateTime` with application-local time, rather than reading the `expired` column. Querying `expired` alone does not validate the license, explain every UI state, or prove hardware authorization. NULL expiry is not evidence of a perpetual license. `LicenseContent` and `ColorVisionLicense` are computed, ORM-ignored properties; never expose the encoded `value` or decoded payload.

## Read-only inventory examples

After confirming the fields, submit each statement separately through `QueryDatabaseSql` with `maxRows: 200` to match its SQL limit. The default tool limit is only 100, and cell/total-output limits still apply.

Resource inventory, not an exact list of enabled or visible services:

```sql
SELECT id, code, name, type, pid, is_enable, is_delete, tenant_id, create_date
FROM t_scgd_sys_resource
WHERE is_delete = 0
ORDER BY pid, id
LIMIT 200
```

Rows whose stored expiry metadata precedes the database session's current time; NULL expiry rows are excluded, and UI license validity is not determined here:

```sql
SELECT id, res_dev_cam_pid, res_dev_cali_pid, lic_type, model, mac_sn, expired
FROM t_scgd_camera_license
WHERE expired < CURRENT_TIMESTAMP
ORDER BY expired, id
LIMIT 200
```

## Configuration changes

1. Establish the exact resource/license rows, relationship endpoints, and affected count using necessary non-secret fields.
2. Explain the requested identity, hierarchy, connection, or license impact before submitting an approved write. Use flags only where the relevant load path supports the intended behavior; avoid blind string replacement of JSON or license payloads.
3. Re-query the changed scope. Direct SQL does not call `DeviceService.Save()`, `RestartRCService()`, or configuration notifications, and does not update already loaded objects. Verify runtime application separately through an authorized application workflow; do not automatically restart services as a query follow-up.
