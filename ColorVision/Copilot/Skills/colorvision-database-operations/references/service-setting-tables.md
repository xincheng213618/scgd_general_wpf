# Service setting tables

The authoritative core list is `MySqlLocalServicesManager.ServiceSettingTableNames`. These rows contain customer templates and behavior settings selected by services. Database reset/update loads the versioned schema and defaults first, then restores these field rows from the migration backup so customer flows and templates remain available.

Copilot may query these tables but must never mutate them. `ExecuteDatabaseSql` enforces this boundary even if an approval was attempted.

## POI template settings

### `t_scgd_algorithm_poi_template_master`

Fields include `id`, `name`, `type`, `width`, `height`, corner coordinates (`left_top_x` through `left_bottom_y`), `dynamics`, JSON `cfg_json`, `create_date`, `is_enable`, `is_delete`, `remark`, and `tenant_id`.

### `t_scgd_algorithm_poi_template_detail`

Fields include `id`, `pid`, `name`, `pt_type`, `pix_x`, `pix_y`, `pix_width`, `pix_height`, `is_enable`, `is_delete`, and `remark`. Detail `pid` refers to POI master `id`.

## Product/recipe settings

### `t_scgd_buz_product_master`

Fields include `id`, `code`, `name`, `buz_type`, JSON `cfg_json`, `img_file`, `create_date`, `is_enable`, `is_delete`, `tenant_id`, and `remark`.

### `t_scgd_buz_product_detail`

Fields include `id`, `pid`, `code`, `name`, `poi_id`, `order_index`, JSON `cfg_json`, and `val_rule_temp_id`. Detail `pid` normally refers to product master `id`; `poi_id` selects a POI template and `val_rule_temp_id` selects a validation-rule template. Verify each target live before mutation.

## Module parameter settings

### `t_scgd_mod_param_master`

Fields include `id`, `code`, `name`, `mm_id`, `res_pid`, JSON `cfg_json`, `create_date`, `is_enable`, `is_delete`, `remark`, and `tenant_id`. `mm_id` refers to module dictionary metadata and `res_pid` refers to a service resource.

### `t_scgd_mod_param_detail`

Fields include `id`, `cc_pid`, `pid`, `value_a`, `value_b`, `is_enable`, and `is_delete`. Current sensor template code joins detail `pid` to parameter master `id`. Interpret `cc_pid` and value fields through the live module dictionary.

## Query examples

```sql
SELECT id, code, name, mm_id, res_pid, create_date, tenant_id, remark
FROM t_scgd_mod_param_master
WHERE is_enable = 1 AND is_delete = 0
ORDER BY id
LIMIT 200
```

```sql
SELECT master.id, master.name, COUNT(detail.id) AS poi_count
FROM t_scgd_algorithm_poi_template_master AS master
LEFT JOIN t_scgd_algorithm_poi_template_detail AS detail
  ON detail.pid = master.id AND detail.is_delete = 0
WHERE master.is_delete = 0
GROUP BY master.id, master.name
ORDER BY master.id
LIMIT 200
```

## Read-only rules

1. Use these tables only to explain the currently installed version's settings.
2. Do not call `ExecuteDatabaseSql` for `INSERT`, `UPDATE`, `DELETE`, `REPLACE`, DDL, or cleanup involving these tables.
3. Do not preserve them through the Service Manager reset backup; native release SQL is their source of truth.
4. If settings must change, implement the change in the versioned model/SQL migration and release path rather than applying ad-hoc production SQL.
