# Validate 判定规则模板

`Validate/` 保留早期合规判定模板的加载和执行兼容。默认合规字典维护窗口已经移除；部分历史项目包仍可能间接读取现有规则数据。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 实际判定模板 | `TemplateComplyParam : ITemplate<ValidateParam>` |
| 规则编辑控件 | `ValidateControl.xaml(.cs)` |
| 菜单入口 | `ExportComply.cs` 中的入口已标记 `Obsolete`，主菜单“模板”不再发现这些类型 |
| 主表/明细表 | `t_scgd_rule_validate_template_master`、`t_scgd_rule_validate_template_detail` |
| 运行时缓存 | `TemplateComplyParam.CIEParams`、`TemplateComplyParam.JNDParams` |

## 两层模型

| 层 | 来源 | 作用 |
| --- | --- | --- |
| 默认合规字典 | `TemplateComplyParam` 按字典代码读取 `SysDictionaryModMasterDao` 和 `SysDictionaryModItemValidateDao` | 仅为历史模板创建和加载提供字段、阈值来源；不再提供通用维护窗口 |
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
| 默认字典维护 | 菜单、模板类和编辑窗口均已移除，现有字典数据由数据库迁移或专用脚本维护 |
| 创建实际模板 | `TemplateComplyParam.Create(...)` 创建主表，按当前 `Code` 复制启用的默认规则明细 |
| 保存 | `TemplateComplyParam.Save()` 保存模板主表和明细规则 |

## 缓存和导入限制

| 项 | 说明 |
| --- | --- |
| `CIEParams` | CIE/常规合规判定模板集合 |
| `JNDParams` | JND 判定模板集合 |
| JND 缓存行为 | 当前构造函数在 `type == 1` 时加入 `JNDParams`，随后也会加入 `CIEParams` |
| 导入限制 | `TemplateComplyParam.Import()` 当前提示“暂不支持模板{Code}的导入” |

现场迁移 Validate 模板时，应通过数据库或脚本处理，并同时迁移其依赖的默认字典数据。

## 依赖关系

| 模块 | 依赖方式 |
| --- | --- |
| JND 类判定 | 判定模板来自 `mod_type = 120` |
| 项目包 | 可能读取 Validate 结果生成最终报表或 OK/NG |

## 排查和维护

| 现象或改动 | 优先检查 |
| --- | --- |
| 旧代码仍引用菜单类型 | `ExportComply` 相关类型仅为兼容保留并标记 `Obsolete`；不要依赖其重新出现在主菜单 |
| 新建模板没有明细 | 默认字典明细是否启用，是否能按 `pid` 查到数据 |
| JND 判定混在 CIE 列表中 | 当前构造函数行为，不要只看 `JNDParams` |
| 新增判定字段 | 同步默认字典、模板明细、项目验收样例和结果说明 |
| 修改 `ValType` 或阈值语义 | 同步算法服务写回的 `ValidateResult` |
| 现场迁移 | 同步 `SysDictionaryMod*` 和 `t_scgd_rule_validate_template_*` |
