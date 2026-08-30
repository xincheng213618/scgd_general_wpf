---
knowledge_id: "algorithms.template-dictionary"
knowledge_type: "reference"
status: "current"
summary: "说明保留的系统字典 DAO 与模板默认值、传感器和旧流程兼容依赖。"
aliases: ["系统字典窗口没了还能删除表吗","TemplateDicId","SysDictionaryModModel","SensorTemplateDictionaryService"]
code_paths: ["Engine/ColorVision.Engine/Dao/SysDictionaryModMasterDao.cs","Engine/ColorVision.Engine/Dao/SysDictionaryModDetaiModel.cs","Engine/ColorVision.Engine/Templates/ITemplate.cs","Engine/ColorVision.Engine/Services/Devices/Sensor/Templates/SensorTemplateDictionaryService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SensorTemplateMigrationTests.cs","Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs"]
related: ["algorithms.index","engine.template-design","algorithms.json-templates"]
---

# SysDictionary 系统字典兼容层

系统字典不再提供算法、传感器或合规默认字典的通用维护窗口。相关数据库模型和读取链路继续保留，用于 JSON 模板默认参数、旧强类型模板、历史流程包和传感器运行时兼容。

## 当前范围

| 事项 | 当前实现 |
| --- | --- |
| 通用编辑窗口 | 已移除 |
| 算法字典菜单 | 已移除 |
| 传感器字典菜单 | 已移除 |
| 合规默认字典菜单 | 已移除 |
| 字典主表 | `t_scgd_sys_dictionary_mod_master` |
| 普通字典明细表 | `t_scgd_sys_dictionary_mod_item` |
| 合规字典明细表 | `t_scgd_sys_dictionary_mod_item_validate` |

## 仍在使用的源码入口

| 文件 | 用途 |
| --- | --- |
| `Dao/SysDictionaryModMasterDao.cs` | 字典主档模型；JSON 模板也通过主档的 `cfg_json` 保存默认参数 |
| `Dao/SysDictionaryModDetaiModel.cs` | 旧强类型模板和传感器命令定义使用的明细模型 |
| `Dao/SysDictionaryModItemValidateDao.cs` | 历史合规判定模板使用的默认规则明细 |
| `Templates/ITemplate.cs` | 创建旧强类型模板时按 `TemplateDicId` 复制默认明细 |
| `Templates/Jsons/ITemplateJson.cs` | 创建或重置 JSON 模板时读取字典主档的 `cfg_json` |
| `Templates/ModelBase.cs` | 通过 `SysPid` 和字典 `symbol` 恢复旧模板属性映射 |
| `Templates/Flow/FlowPackageHelper.cs` | 导入导出历史流程包时处理字典依赖 |
| `Services/Devices/Sensor/Templates/SensorTemplateDictionaryService.cs` | 按需创建和读取传感器命令定义 |

## 数据模型

| 表 | 关键字段 | 说明 |
| --- | --- | --- |
| `t_scgd_sys_dictionary_mod_master` | `id`、`code`、`mod_type`、`cfg_json`、`version`、`is_enable`、`is_delete` | JSON 模板使用 `cfg_json`；旧模板使用主档 ID 关联明细 |
| `t_scgd_sys_dictionary_mod_item` | `pid`、`address_code`、`symbol`、`default_val`、`val_type` | 旧强类型模板依赖 `symbol` 完成属性映射 |
| `t_scgd_sys_dictionary_mod_item_validate` | `pid`、规则代码、阈值和启用状态 | 早期合规模板的默认规则来源 |

## 运行时关系

| 模块 | 关系 |
| --- | --- |
| 普通强类型模板 | 通过 `TemplateDicId` 读取字典明细，创建默认参数并恢复属性映射 |
| JSON 模板 | 读取字典主档的 `cfg_json`；默认 JSON 通过模板编辑器的“设为默认参数”更新 |
| Flow 模板 | 创建流程参数和导入流程包时读取字典明细 |
| Sensor | 由专用服务维护运行所需的命令定义，不再暴露通用字典编辑窗口 |
| Validate | 仅保留历史规则加载和执行兼容，不再暴露默认字典编辑窗口 |

## 维护约束

- 不要因为维护窗口已经移除就删除字典表或 DAO；旧模板仍依赖字典 ID 和 `symbol`。
- 新算法参数继续使用 JSON 模板，不再新增通用字典明细编辑入口。
- 修改旧模板字典数据应通过版本化数据库迁移或专用脚本完成，并验证历史模板和流程包。
- 迁移现场数据库时，字典主档、明细和引用它们的模板记录必须保持 ID 一致。

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/SensorTemplateMigrationTests.cs`、`Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs`。

测试覆盖传感器迁移与流程包兼容的一部分；现场数据库迁移必须保留主从 ID 和旧模板引用，不把移除窗口理解为删除数据授权。
