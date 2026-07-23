# Validate 判定规则模板

`Validate/` 有两层规则体系：默认合规字典维护字段和阈值来源，实际判定模板按字典代码生成可选规则。BuzProduct、Compliance 和部分项目包都会间接依赖这套数据。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 默认字典模板 | `TemplateDicComply : ITemplate<DicComplyParam>` |
| 实际判定模板 | `TemplateComplyParam : ITemplate<ValidateParam>` |
| 字典/规则编辑控件 | `DicEditComply.xaml(.cs)`、`ValidateControl.xaml(.cs)` |
| 菜单入口 | `ExportComply.cs`、`ExportDicComply.cs` 中的入口已标记 `Obsolete`，主菜单“模板”不再发现这些类型 |
| 主表/明细表 | `t_scgd_rule_validate_template_master`、`t_scgd_rule_validate_template_detail` |
| 运行时缓存 | `TemplateComplyParam.CIEParams`、`TemplateComplyParam.JNDParams` |

## 两层模型

| 层 | 来源 | 作用 |
| --- | --- | --- |
| 默认合规字典 | `TemplateDicComply` 读取 `SysDictionaryModMasterDao` 和 `SysDictionaryModItemValidateDao` | 维护默认字段代码、阈值和启用状态 |
| 实际判定模板 | `TemplateComplyParam(code, type)` 读取 `t_scgd_rule_validate_template_master/detail` | 从默认规则复制出可编辑实例 |

| 字典 `mod_type` | 当前用途 |
| --- | --- |
| `110` | 点位类 CIE/合规判定菜单 |
| `111` | 点位列表类合规判定菜单 |
| `120` | JND 类合规判定菜单 |

| 表 | 关键字段 |
| --- | --- |
| `t_scgd_rule_validate_template_master` | `dic_pid`、`code`、`name`、`is_enable`、`is_delete`、`tenant_id` |
| `t_scgd_rule_validate_template_detail` | `dic_pid`、`pid`、`code`、`val_max`、`val_min`、`val_equal`、`val_radix`、`val_type` |

## 菜单、创建和保存

| 动作 | 当前逻辑 |
| --- | --- |
| 原动态菜单 | `ExportComply.cs` 及其提供者已弃用，不再根据 `mod_type = 110/111/120` 向主菜单添加入口 |
| 原默认字典菜单 | `ExportDicComply` 已弃用，不再从主菜单打开 `TemplateDicComply` |
| 创建默认字典 | `TemplateDicComply.Create(...)` 创建 `SysDictionaryModModel`，默认 `ModType = 111` |
| 创建实际模板 | `TemplateComplyParam.Create(...)` 创建主表，按当前 `Code` 复制启用的默认规则明细 |
| 保存 | `TemplateDicComply.Save()` 保存默认字典；`TemplateComplyParam.Save()` 保存模板主表和明细规则 |

## 缓存和导入限制

| 项 | 说明 |
| --- | --- |
| `CIEParams` | CIE/常规合规判定模板集合，BuzProduct 下拉框会读取 |
| `JNDParams` | JND 判定模板集合 |
| JND 缓存行为 | 当前构造函数在 `type == 1` 时加入 `JNDParams`，随后也会加入 `CIEParams` |
| 导入限制 | `TemplateComplyParam.Import()` 当前提示“暂不支持模板{Code}的导入” |

现场迁移 Validate 模板时，应通过数据库、脚本或后续新增导入流程处理，并同时迁移默认字典数据。

## 依赖关系

| 模块 | 依赖方式 |
| --- | --- |
| [BuzProduct 产品业务参数模板](./buz-product-template.md) | 明细的 `val_rule_temp_id` 指向 Validate 模板 |
| [Compliance 结果对接](./compliance-results.md) | 读取上游写回的 `ValidateResult`，按 `ValidateRuleResultType.M` 判断通过 |
| [JND 模板](./jnd-template.md) | JND 类判定模板来自 `mod_type = 120` |
| 项目包 | 可能读取 Validate/Compliance 结果生成最终报表或 OK/NG |

## 排查和维护

| 现象或改动 | 优先检查 |
| --- | --- |
| 旧代码仍引用菜单类型 | 菜单类型仅为兼容保留并标记 `Obsolete`；不要依赖其重新出现在主菜单 |
| 新建模板没有明细 | 默认字典明细是否启用，是否能按 `pid` 查到数据 |
| BuzProduct 下拉没有规则 | `TemplateComplyParam.CIEParams` 是否已加载对应 `Code` |
| JND 判定混在 CIE 列表中 | 当前构造函数行为，不要只看 `JNDParams` |
| 新增判定字段 | 同步默认字典、模板明细、项目验收样例和结果说明 |
| 修改 `ValType` 或阈值语义 | 同步算法服务写回的 `ValidateResult` |
| 现场迁移 | 同步 `SysDictionaryMod*` 和 `t_scgd_rule_validate_template_*` |
