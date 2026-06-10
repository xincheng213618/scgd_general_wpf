# Validate 判定规则模板

本页说明 `Engine/ColorVision.Engine/Templates/Validate/` 的两层规则体系。Validate 不是单一模板：第一层维护“默认合规字典”，第二层按字典代码创建实际可选的判定规则模板。BuzProduct、Compliance 和部分项目包都会间接依赖这套数据。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 默认字典模板 | `TemplateDicComply : ITemplate<DicComplyParam>` |
| 实际判定模板 | `TemplateComplyParam : ITemplate<ValidateParam>` |
| 字典编辑控件 | `DicEditComply.xaml(.cs)` |
| 规则编辑控件 | `ValidateControl.xaml(.cs)` |
| 菜单入口 | `ExportComply.cs`、`ExportDicComply.cs` |
| 判定模板主表 | `t_scgd_rule_validate_template_master` |
| 判定模板明细表 | `t_scgd_rule_validate_template_detail` |
| 运行时缓存 | `TemplateComplyParam.CIEParams`、`TemplateComplyParam.JNDParams` |

## 两层模型

### 默认合规字典

`TemplateDicComply` 维护默认字段和阈值来源。它读取 `SysDictionaryModMasterDao` 中的字典主档，再读取 `SysDictionaryModItemValidateDao` 中的默认规则明细。

| 字典 `mod_type` | 当前用途 |
| --- | --- |
| `110` | 点位类 CIE/合规判定菜单。 |
| `111` | 点位列表类合规判定菜单。 |
| `120` | JND 类合规判定菜单。 |

新增默认规则时，先在默认字典里维护字段代码、阈值和启用状态，再让实际判定模板从这些默认项复制出可编辑实例。

### 实际判定模板

`TemplateComplyParam(code, type)` 按字典 `Code` 加载实际模板。它读取 `t_scgd_rule_validate_template_master`，再通过主表 `id` 读取 `t_scgd_rule_validate_template_detail`。

| 表 | 关键字段 | 说明 |
| --- | --- | --- |
| `t_scgd_rule_validate_template_master` | `dic_pid`、`code`、`name`、`is_enable`、`is_delete`、`tenant_id` | 某个字典代码下的一套判定模板。 |
| `t_scgd_rule_validate_template_detail` | `dic_pid`、`pid`、`code`、`val_max`、`val_min`、`val_equal`、`val_radix`、`val_type` | 具体判定项和阈值。 |

## 菜单生成

`ExportComply.cs` 会根据系统字典动态生成菜单：

| 来源 | 菜单行为 |
| --- | --- |
| `mod_type = 110` | 创建 `TemplateComplyParam(item.Code)`，作为点位类判定模板入口。 |
| `mod_type = 111` | 创建 `TemplateComplyParam(item.Code)`，作为点位列表类判定模板入口。 |
| `mod_type = 120` | 创建 `TemplateComplyParam(item.Code, 1)`，作为 JND 类判定模板入口。 |
| `ExportDicComply` | 打开 `TemplateDicComply`，维护默认合规字典。 |

因此，用户在菜单里看到的“判定模板入口”并不是固定写死的页面，而是由系统字典数据驱动生成。

## 创建与保存

### 创建默认字典

`TemplateDicComply.Create(templateCode, templateName)` 会创建一个 `SysDictionaryModModel`，当前默认 `ModType = 111`，再把它包装成 `DicComplyParam` 加入模板列表。

### 创建实际判定模板

`TemplateComplyParam.Create(templateName)` 的关键过程是：

1. 创建 `ValidateTemplateMasterModel { Code = Code, Name = templateName }`。
2. 根据当前 `Code` 找到对应 `SysDictionaryModModel`。
3. 把主表 `DId` 设置为字典 `id`。
4. 读取该字典下启用的 `SysDictionaryModItemValidateModel`。
5. 复制 `ValMax`、`ValMin`、`ValEqual`、`ValRadix`、`ValType` 到 `ValidateTemplateDetailModel`。
6. 保存明细，并重新加载成 `ValidateParam`。

### 保存

`TemplateComplyParam.Save()` 会保存模板主表名称和每条明细规则。`TemplateDicComply.Save()` 会保存默认字典主档和默认规则明细。

## 运行时缓存

`TemplateComplyParam` 维护两个静态字典：

| 缓存 | 说明 |
| --- | --- |
| `CIEParams` | CIE/常规合规判定模板集合，BuzProduct 下拉框会读取它。 |
| `JNDParams` | JND 判定模板集合。 |

当前构造函数在 `type == 1` 时会把集合加入 `JNDParams`，随后也会加入 `CIEParams`。交接时要按现有行为说明，不要假设 JND 模板只存在于 `JNDParams`。

## 导入限制

`TemplateComplyParam.Import()` 当前显示“暂不支持模板{Code}的导入”。如果现场需要迁移 Validate 模板，应通过数据库、脚本或后续新增导入流程处理，并同时迁移默认字典数据。

## 与其他模块的关系

| 模块 | 依赖方式 |
| --- | --- |
| [BuzProduct 产品业务参数模板](./buz-product-template.md) | 明细的 `val_rule_temp_id` 指向 Validate 模板。 |
| [Compliance 结果交接](./compliance-results.md) | 读取上游写回的 `ValidateResult`，按 `ValidateRuleResultType.M` 判断通过。 |
| [JND 模板](./jnd-template.md) | JND 类判定模板来自 `mod_type = 120`。 |
| 项目包 | 可能读取 Validate/Compliance 结果生成最终报表或 OK/NG。 |

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 菜单没有某类判定模板 | `SysDictionaryModMaster` 是否存在对应 `mod_type`，且 `is_delete = false`。 |
| 新建模板没有明细 | 默认字典明细是否启用，`SysDictionaryModItemValidateDao` 是否能按 `pid` 查到数据。 |
| BuzProduct 下拉没有规则 | `TemplateComplyParam.CIEParams` 是否已加载对应 `Code`。 |
| JND 判定混在 CIE 列表中 | 这是当前构造函数行为，排查时不要只看 `JNDParams`。 |
| 导入按钮不可用 | 当前实际判定模板不支持导入，应另行迁移数据。 |

## 交接清单

- 先讲清“默认字典”和“实际判定模板”两层，不要把它们混成一张表。
- 新增判定字段时，同步更新默认字典、模板明细、项目验收样例和结果说明。
- 修改 `ValType` 或阈值语义时，同步检查算法服务写回的 `ValidateResult`。
- 现场迁移时，同时迁移 `SysDictionaryMod*` 和 `t_scgd_rule_validate_template_*`。
- 修改菜单生成逻辑时，要验证 `mod_type = 110/111/120` 三条路径。

## 继续阅读

- [BuzProduct 产品业务参数模板](./buz-product-template.md)
- [Compliance 结果交接](./compliance-results.md)
- [模板管理](./template-management.md)
- [Engine 结果展示与项目交接链路](../../engine-components/result-handoff-chain.md)
- [当前算法模板覆盖清单](../current-algorithm-template-coverage.md)
