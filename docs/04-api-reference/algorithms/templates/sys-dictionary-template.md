# SysDictionary 系统字典模板

本页说明 `Engine/ColorVision.Engine/Templates/SysDictionary/` 的职责。它维护的是算法模板默认字典，核心数据在 `t_scgd_sys_dictionary_mod_master` 和 `t_scgd_sys_dictionary_mod_item`，当前 `TemplateModParam` 只加载 `mod_type = 7` 的算法字典。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 模板类 | `TemplateModParam : ITemplate<DicModParam>` |
| 参数类 | `DicModParam : ParamModBase` |
| 编辑控件 | `EditDictionaryMode.xaml(.cs)` |
| 创建主档窗口 | `CreateDicTemplate.xaml(.cs)` |
| 创建明细窗口 | `CreateDicModeDetail.xaml(.cs)` |
| 菜单入口 | `MenuDefalutDicAlg` |
| 主表 | `t_scgd_sys_dictionary_mod_master` |
| 明细表 | `t_scgd_sys_dictionary_mod_item` |
| 当前范围 | `tenant_id = 0`、`mod_type = 7` |

## 源码入口

| 文件 | 交接用途 |
| --- | --- |
| `DicModParam.cs` | 定义 `TemplateModParam` 的加载、保存、创建和模板数据结构。 |
| `EditDictionaryMode.xaml(.cs)` | 展示字典明细，支持新增、删除、列显示配置和默认值编辑。 |
| `CreateDicTemplate.xaml(.cs)` | 创建字典主档，生成候选 Code/Name。 |
| `CreateDicModeDetail.xaml(.cs)` | 创建字典明细，设置 Symbol、Name、ValueType、DefaultValue。 |
| `MenuDefalutDicAlg.cs` | 在模板算法菜单下打开默认算法字典编辑器。 |
| `SysDictionaryModMasterDao.cs` | 主表模型和 `GetByCode(...)` 查询。 |
| `SysDictionaryModDetaiModel.cs` | 明细表模型、值类型枚举和 DAO。 |

## 数据模型

### 主表

`SysDictionaryModModel` 对应 `t_scgd_sys_dictionary_mod_master`。

| 字段 | 说明 |
| --- | --- |
| `code` | 字典代码，常被模板 `Code`、JSON 模板或创建流程引用。 |
| `name` | 字典显示名称。 |
| `pid` / `p_type` | 父级和类型信息。 |
| `mod_type` | 字典类别；算法默认字典当前是 `7`。 |
| `cfg_json` | 字典级 JSON 配置。 |
| `version` | 版本。 |
| `is_enable` / `is_delete` | 启用和删除状态。 |
| `tenant_id` | 租户。 |

### 明细表

`SysDictionaryModDetaiModel` 对应 `t_scgd_sys_dictionary_mod_item`。

| 字段 | 说明 |
| --- | --- |
| `pid` | 所属字典主档 ID。 |
| `address_code` | 字段地址码；新建明细时默认等于新 ID。 |
| `symbol` | 字段符号，通常对应模板参数名。 |
| `name` | 字段说明。 |
| `default_val` | 默认值。 |
| `val_type` | `Integer`、`Float`、`Bool`、`String`、`Enum`。 |
| `is_enable` / `is_delete` | 启用和删除状态。 |

## 生命周期

1. 菜单 `MenuDefalutDicAlg` 打开 `TemplateEditorWindow(new TemplateModParam())`。
2. `TemplateModParam.Load()` 在 MySQL 已连接时读取 `tenant_id = 0`、`mod_type = 7` 的主档。
3. 每个主档通过 `SysDictionaryModDetailDao.GetAllByPid(model.Id)` 读取明细。
4. `EditDictionaryMode` 展示明细，并允许编辑默认值和启用状态。
5. `CreateDicTemplate` 创建新主档，固定 `ModType = 7`。
6. `CreateDicModeDetail` 创建新明细，默认 `ValueType = String`、`IsEnable = true`。
7. `Save()` 保存每条明细。

当前 `TemplateModParam.Save()` 只保存明细，没有保存主档名称、Code 或其它主表字段。当前删除路径调用的是 `SysResourceModel` 删除；如果现场发现字典主档或明细没有真正删除，优先核对这里的 DAO 和表名是否匹配。

## 与其它模板的关系

| 模块 | 关系 |
| --- | --- |
| 普通强类型模板 | 通过 `TemplateDicId` 读取系统字典明细，生成默认参数项。 |
| JSON 模板 | 多数 JSON 模板主档也是 `mod_type = 7`，但参数内容常放在 `cfg_json`。 |
| Flow 模板 | `TemplateFlow` 会读取系统字典明细来构造节点参数。 |
| Validate | Validate 的默认合规字典使用 `mod_type = 110/111/120`，不要和这里的 `mod_type = 7` 混淆。 |

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 模板字段不显示 | 对应模板的 `TemplateDicId` 是否能在 `t_scgd_sys_dictionary_mod_master` 找到主档。 |
| 新增字段没有进入模板 | 明细 `pid`、`symbol`、`address_code`、`is_enable` 是否正确。 |
| 默认值不生效 | `default_val` 类型是否和 `val_type` 匹配。 |
| 字典菜单没有入口 | `MenuDefalutDicAlg` 是否被菜单系统扫描，权限是否允许。 |
| 删除后仍能看到字段 | 当前删除逻辑是否删到了正确表，缓存是否需要刷新。 |

## 交接清单

- 新增模板目录时，同步确认是否需要新的系统字典主档和明细。
- 修改 `symbol` 时，同时检查模板参数名、导入导出、Flow 包和历史模板。
- 迁移现场数据库时，系统字典主表和明细表必须一起迁移。
- 不要把 `mod_type = 7` 的算法字典和 Validate 的合规判定字典混在同一页配置。
- 如果字典字段影响客户验收，项目文档要写清字段含义和默认值。

## 继续阅读

- [模板管理](./template-management.md)
- [Templates API 参考](./api-reference.md)
- [Validate 判定规则模板](./validate-rules.md)
- [Engine 模板与 Flow 链路](../../engine-components/template-flow-chain.md)
- [当前算法模板覆盖清单](../current-algorithm-template-coverage.md)
