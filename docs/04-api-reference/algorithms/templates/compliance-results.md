# Compliance 结果对接

本页说明 `Engine/ColorVision.Engine/Templates/Compliance/` 的结果模型和展示链路。这个目录当前不是模板编辑器，也不负责创建判定规则；它负责把算法服务或项目流程写回的合规结果读出来、展示出来，并根据 `ValidateResult` 判断是否通过。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| Y 结果 | `ComplianceYModel`、`ComplianceYDao`、`ViewHandleComplianceY` |
| XYZ 结果 | `ComplianceXYZModel`、`ComplianceXYZDao`、`ViewHandleComplianceXYZ` |
| JND 结果 | `ComplianceJNDModel`、`ComplianceJNDDao`、`ViewHandleComplianceJND` |
| 判定来源 | `ValidateResult` JSON |
| 判定对象 | `ObservableCollection<ValidateRuleResult>` |
| 通过条件 | 每条 `ValidateRuleResult.Result` 都等于 `ValidateRuleResultType.M` |
| 结果入口 | `IResultHandleBase` handler |

## 结果类型映射

| Handler | 可处理结果类型 | 数据表 |
| --- | --- | --- |
| `ViewHandleComplianceY` | `Compliance_Contrast`、`Compliance_Math`、`Compliance_Contrast_CIE_Y`、`Compliance_Math_CIE_Y` | `t_scgd_algorithm_result_detail_compliance_y` |
| `ViewHandleComplianceXYZ` | `Compliance_Contrast_CIE_XYZ`、`Compliance_Math_CIE_XYZ` | `t_scgd_algorithm_result_detail_compliance_xyz` |
| `ViewHandleComplianceJND` | `Compliance_Math_JND` | `t_scgd_algorithm_result_detail_compliance_jnd` |

## 数据模型

| 模型 | 关键字段 | 说明 |
| --- | --- | --- |
| `ComplianceYModel` | `pid`、`name`、`data_type`、`data_value`、`validate_result` | 单值亮度或对比类结果 |
| `ComplianceXYZModel` | `data_value_x/y/z`、`data_value_u/v`、`data_value_yyy/xxx/zzz`、`data_value_cct`、`data_value_wave`、`validate_result` | 色彩和光学多分量结果 |
| `ComplianceJNDModel` | `data_val_h`、`data_val_v`、`validate_result` | 横向和纵向 JND 判定结果 |

## 判定逻辑

三个结果模型的 `Validate` 属性逻辑一致：`ValidateResult` 为空时失败；能反序列化为 `ObservableCollection<ValidateRuleResult>` 时逐条检查；只有所有规则的 `Result == ValidateRuleResultType.M` 才通过。Compliance 页展示的是上游写回的判定解释，不重新计算阈值。

## 展示链路

1. 结果页按 `ViewResultAlgType` 找到对应 `ViewHandleCompliance*`。
2. Handler 如果发现 `ResultImagFile` 存在，会先打开图像。
3. Handler 按主结果 `id` 查询对应明细表。
4. 查询结果转换为 `IViewResult` 集合，绑定到 ListView。
5. ListView 根据 handler 设置的列显示名称、数值和判定 JSON。

当前 `ViewHandleComplianceXYZ` 的表格绑定列包含 `DataValue`，但模型里主要暴露的是 `DataValuex/y/z/u/v/...` 分量字段。若 XYZ 页面出现空值，优先检查绑定列和模型属性是否需要对齐。

## 与 Validate 和 BuzProduct 的关系

| 模块 | 在判定链里的角色 |
| --- | --- |
| [Validate 判定规则模板](./validate-rules.md) | 定义哪些字段、阈值和比较方式构成一套规则。 |
| [BuzProduct 产品业务参数模板](./buz-product-template.md) | 在产品明细中通过 `val_rule_temp_id` 指定某个点位使用哪套规则。 |
| Compliance 结果 | 读取上游写回的 `ValidateResult`，展示每个结果项是否通过。 |
| 项目包 | 可能继续读取 Compliance/JND/POI 结果，生成报表、CSV 或最终 OK/NG。 |

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 页面没有明细 | 结果类型是否命中对应 handler，明细表是否有同 `pid` 数据。 |
| 图像没打开 | `ResultImagFile` 路径是否存在，归档或迁移后路径是否失效。 |
| `Validate` 显示失败 | `validate_result` 是否为空，或其中是否存在非 `M` 的规则结果。 |
| XYZ 值为空 | 检查 ListView 绑定列和 `ComplianceXYZModel` 的字段名是否一致。 |
| 项目报表和结果页不一致 | 项目包是否对 Compliance 结果又做了二次筛选、排序或聚合。 |

## 检查清单

| 改动 | 同步检查 |
| --- | --- |
| 新增结果类型 | handler、DAO、数据表和文档映射 |
| 修改 `ValidateResult` JSON | Y、XYZ、JND 三类模型 |
| 现场验收 | 主结果、明细表、原图路径、Validate 模板和项目导出文件 |
| 项目使用 JND | 同步看 [JND 模板](./jnd-template.md) 和项目页 |
