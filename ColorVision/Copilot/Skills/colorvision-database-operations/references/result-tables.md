# Result tables

The authoritative core list is `MySqlResultCleanupProvider.ResultTableNames`. These tables contain generated workflow, device measurement, and algorithm output data. They are excluded from Service Manager reset-preservation exports.

## Workflow execution master

### `t_scgd_measure_batch`

One row represents one workflow execution attempt.

| Column | Meaning |
| --- | --- |
| `id` | Execution identifier and parent key for measurement results |
| `t_id` | Optional task/template identifier |
| `name`, `code` | Execution/workflow name and code |
| `create_date` | Local creation/start timestamp |
| `total_time` | Duration in milliseconds |
| `result` | Optional result message/payload |
| `result_code` | Numeric `FlowStatus` |
| `archived_flag` | Archive state |
| `tenant_id` | Tenant scope |

`result_code`: `0 Ready`, `1 Runing`, `2 Paused`, `3 Failed`, `4 Canceled`, `5 OverTime`, `6 Completed`. `Runing` follows the current enum spelling.

`archived_flag`: `-2 Failed`, `-1 NotArchived`, `0 Pending`, `1 Archived`.

Prefer `QueryFlowExecutionStats` for common period statistics. For a custom period:

```sql
SELECT result_code, COUNT(*) AS execution_count, AVG(total_time) AS avg_duration_ms
FROM t_scgd_measure_batch
WHERE create_date >= CURDATE()
  AND create_date < CURDATE() + INTERVAL 1 DAY
GROUP BY result_code
ORDER BY result_code
```

## Measurement detail tables

These normally reference batch `id` through `batch_id`:

- `t_scgd_measure_result_img`: image paths/data, electrical values, device/result state, and duration.
- `t_scgd_measure_result_smu`: single SMU source and measured voltage/current values.
- `t_scgd_measure_result_smu_scan`: SMU sweep setup plus JSON voltage/current arrays.
- `t_scgd_measure_result_sensor`: generic sensor command and text result.
- `t_scgd_measure_result_spectrometer`: spectrum, chromaticity, EQE, electrical, flux, and efficacy results.
- `t_scgd_measure_result_third_party_algorithm`: third-party algorithm measurement results.

Important spectrometer fields include `data_type` (`0` spectrum, `1` EQE), `v_result`, `i_result`, `a_factor`, `eqe`, `luminous_flux` (lm), `radiant_flux` (W), `luminous_efficacy` (lm/W), `fx`, `fy`, `fu`, `fv`, `fCCT`, `fLd`, `fPur`, `fLp`, `fHW`, `fPL`, `fRi`, `result_code`, `total_time`, and `create_date`.

Always `DESCRIBE` a detail table before using fields not listed here.

## Algorithm results

### `t_scgd_algorithm_result_master`

Fields include `id`, `tid`, `tname`, `img_file`, `img_file_type`, `version`, `nd_port`, `batch_id`, `z_index`, JSON `params`, `device_code`, `smu_data_id`, `result_code`, `result`, `img_result`, `total_time`, and `create_date`.

Current algorithm detail tables use `pid` to reference master `id` when the column exists:

- `t_scgd_algorithm_result_detail_sfr`
- `t_scgd_algorithm_result_detail_poi_mtf`
- `t_scgd_algorithm_result_detail_poi_cie_file`
- `t_scgd_algorithm_result_detail_light_area`
- `t_scgd_algorithm_result_detail_image`
- `t_scgd_algorithm_result_detail_ghost`
- `t_scgd_algorithm_result_detail_fov`
- `t_scgd_algorithm_result_detail_distortion`
- `t_scgd_algorithm_result_detail_compliance_y`
- `t_scgd_algorithm_result_detail_compliance_jnd`
- `t_scgd_algorithm_result_detail_common`
- `t_scgd_algorithm_result_detail_blackmura`
- `t_scgd_algorithm_result_detail_binocular_fusion`
- `t_scgd_algorithm_result_detail_aoi`

Projects/plugins can add result tables. The cleanup provider's list is the authoritative core set; discover live additions before reporting or cleanup.

## Safe cleanup order

The cleanup provider recognizes `create_time`, `create_date`, then `add_time` as candidate timestamps.

1. Algorithm detail rows by old master `pid`.
2. Old `t_scgd_algorithm_result_master` rows.
3. Measurement detail rows by old batch `batch_id`.
4. Old `t_scgd_measure_batch` rows.

Preview:

```sql
SELECT COUNT(*) AS rows_to_delete
FROM t_scgd_measure_result_sensor AS child
JOIN t_scgd_measure_batch AS parent ON parent.id = child.batch_id
WHERE parent.create_date < CURDATE() - INTERVAL 6 MONTH
```

Only after explicit confirmation, submit the matching child deletion through `ExecuteDatabaseSql`, then re-query the count. Repeat per table and never include service configuration or service setting tables in a result cleanup scope. Prefer the application's Database Cleanup UI for cleaning the full family.
