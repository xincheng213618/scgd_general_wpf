# Compliance 结果交接

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

### Y 结果

`ComplianceYModel` 适合单值亮度或对比类结果。

| 字段 | 说明 |
| --- | --- |
| `pid` | 主结果 ID。 |
| `name` | 结果项名称。 |
| `data_type` | 数据类型。 |
| `data_value` | 单值结果。 |
| `validate_result` | 判定结果 JSON。 |

### XYZ 结果

`ComplianceXYZModel` 保存色彩/光学相关的多分量结果。

| 字段 | 说明 |
| --- | --- |
| `data_value_x/y/z` | XYZ 分量。 |
| `data_value_u/v` | 色度坐标分量。 |
| `data_value_yyy/xxx/zzz` | 扩展分量字段，按算法服务写回解释。 |
| `data_value_cct` | 色温。 |
| `data_value_wave` | 波长或主波长相关结果。 |
| `validate_result` | 判定结果 JSON。 |

### JND 结果

`ComplianceJNDModel` 保存横向和纵向 JND 判定结果。

| 字段 | 说明 |
| --- | --- |
| `data_val_h` | 横向 JND 值。 |
| `data_val_v` | 纵向 JND 值。 |
| `validate_result` | 判定结果 JSON。 |

## 判定逻辑

三个结果模型的 `Validate` 属性逻辑一致：

1. 如果 `ValidateResult` 为空，`ValidateSingles` 为 `null`，最终返回 `false`。
2. 如果 `ValidateResult` 可以反序列化为 `ObservableCollection<ValidateRuleResult>`，逐条检查。
3. 只有所有规则的 `Result == ValidateRuleResultType.M`，才返回 `true`。
4. 任意一条不是 `M`，最终结果就是 `false`。

这意味着 Compliance 页展示的通过/失败不是重新计算阈值，而是解释算法服务或上游流程已经写回的 `ValidateResult`。

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

## 交接清单

- 说明 Compliance 目录是结果展示和判定解释层，不是规则创建层。
- 新增结果类型时，必须补 handler、DAO、数据表和文档映射。
- 修改 `ValidateResult` JSON 结构时，同步验证 Y、XYZ、JND 三类模型。
- 现场验收时保留主结果、明细表、原图路径、Validate 模板和项目导出文件。
- 若项目使用 JND 结果，需同时阅读 [JND 模板](./jnd-template.md) 和项目页。

## 继续阅读

- [Validate 判定规则模板](./validate-rules.md)
- [BuzProduct 产品业务参数模板](./buz-product-template.md)
- [JND 模板](./jnd-template.md)
- [Engine 结果展示与项目交接链路](../../engine-components/result-handoff-chain.md)
- [当前算法模板覆盖清单](../current-algorithm-template-coverage.md)
