# Service configuration tables

The authoritative core list is `MySqlLocalServicesManager.ServiceConfigurationTableNames`. These rows define service/device identity, hierarchy, or licensing and are preserved by the Service Manager during a database reset.

## `t_scgd_sys_resource`

Principal service/device resource table. `DeviceService<T>` deserializes `txt_value` into the service's typed configuration and writes it back when saving.

| Column | Meaning |
| --- | --- |
| `id` | Service/resource identifier |
| `name`, `code` | Display name and stable service code |
| `type` | Service/resource type |
| `pid` | Parent resource identifier |
| `txt_value` | Serialized service configuration; can contain sensitive connection data |
| `create_date` | Creation time |
| `is_enable`, `is_delete` | Enable and logical-delete state |
| `tenant_id` | Tenant scope |
| `remark` | Description |

Do not select or echo `txt_value` unless the user specifically needs a non-secret property. Prefer identifiers and lifecycle fields.

## `t_scgd_sys_resource_group`

Links resources to groups.

| Column | Meaning |
| --- | --- |
| `id` | Row identifier |
| `resource_id` | Member service/resource identifier |
| `group_id` | Group resource identifier |

Verify both referenced resource rows before changing this table.

## `t_scgd_camera_license`

Associates camera/calibration resources with a license.

| Column | Meaning |
| --- | --- |
| `id` | License row identifier |
| `res_dev_cam_pid` | Camera resource identifier |
| `res_dev_cali_pid` | Calibration resource identifier |
| `lic_type` | License type |
| `value` | Encoded license payload; sensitive |
| `model`, `mac_sn` | Licensed model and device MAC/SN |
| `expired` | Expiration time |
| `customer_name`, `create_date` | Customer and creation time |

Never expose the `value` payload. Use non-sensitive metadata for inventory and expiry queries.

## Query examples

```sql
SELECT id, code, name, type, pid, is_enable, is_delete, tenant_id, create_date
FROM t_scgd_sys_resource
WHERE is_delete = 0
ORDER BY pid, id
LIMIT 200
```

```sql
SELECT id, res_dev_cam_pid, res_dev_cali_pid, lic_type, model, mac_sn, expired, customer_name
FROM t_scgd_camera_license
WHERE expired < CURRENT_TIMESTAMP
ORDER BY expired
LIMIT 200
```

## Write rules

1. Query the exact service/license row first without selecting sensitive payloads.
2. Explain whether the change affects identity, hierarchy, connection behavior, or licensing.
3. Prefer `is_enable` / `is_delete` when application behavior supports them.
4. Do not edit serialized configuration or license payloads with blind string replacement.
5. Re-query safe fields after approval and mention that a service restart may be required.
