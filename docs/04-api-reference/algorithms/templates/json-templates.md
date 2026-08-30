---
knowledge_id: "algorithms.json-templates"
knowledge_type: "reference"
status: "current"
summary: "JSON模板数据库存储、编辑器与结果版本匹配；Schema优先读取程序集嵌入资源，再回退磁盘索引，不要求输出目录有散文件。"
aliases: ["JSON模板保存和V2结果如何对应","ITemplateJson","TemplateJsonParam","EditTemplateJson","CanHandle1","SchemaIndexResourceName","TryLoadTemplateSchema"]
code_paths: ["Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs","Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs","Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs","Engine/ColorVision.Engine/Templates/Jsons/Schemas/schema-index.json"]
test_paths: ["Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs"]
related: ["algorithms.index","engine.host","engine.template-design","engine.results"]
---

# JSON 模板

JSON 模板是 `ColorVision.Engine` 模板体系中的一个分支，核心是把 `ModMasterModel.JsonVal` 托管成模板项，并通过 `ITemplateJson<T>` 和 `EditTemplateJson` 复用加载、编辑、保存、导入导出链路。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 模板列表为空 | `TemplateDicId`、MySQL 模板主表、`ITemplateJson<T>.Load()` |
| 默认 JSON 不对 | `SysDictionaryModModel.JsonVal`、schema/default 文件 |
| 文本/属性模式切换丢字段 | `EditTemplateJson` 同步逻辑、`JsonValueChanged`、防抖更新 |
| 校验按钮没效果 | `CheckCommand` 只是触发事件链，具体响应看调用方 |
| V2 结果展示不上 | MQTT 事件名、`Version`、`ViewHandle*.CanHandle1` |
| 新 schema 找不到 | 按模板 `Code` 核对索引登记、程序集资源名与磁盘回退，不只检查输出目录是否有文件 |
| 误改 Deprecated | `Deprecated/` 是历史兼容目录，不作为新功能入口 |

## 主链路

| 层 | 当前入口 | 说明 |
| --- | --- | --- |
| 宿主基类 | `ITemplateJson<T>` | 读取 `ModMasterModel.JsonVal`，包装成 `TemplateModel<T>`，处理保存/删除/复制/导入/导出 |
| 参数对象 | `TemplateJsonParam` | 持有 `TemplateJsonModel`、`ResetCommand`、`CheckCommand`、`JsonValueChanged` |
| 编辑控件 | `EditTemplateJson` | AvalonEdit 文本模式、属性模式、注释视图、校验按钮、外部 json.cn 辅助 |
| schema | `Jsons/Schemas/schema-index.json` | 维护各 JSON 模板 schema 索引 |

`JsonValue` 读取时会格式化，写入时只有 JSON 合法才回写 `TemplateJsonModel.JsonVal`。`ResetValue()` 回到字典模板默认 JSON，不是清空文本。

## Schema 查找与发布边界

`EditTemplateJson.TryLoadTemplateSchema` 从 `TemplateJsonParam.TemplateJsonModel.Code` 定位 Schema；Code 为空时不加载。当前查找顺序是：

1. 先读 `typeof(EditTemplateJson).Assembly` 中的索引资源 `Templates/Jsons/Schemas/schema-index.json`，在 `schemas` 数组中按忽略大小写的 `code` 匹配条目，再按 `file` 读取 Schema 嵌入资源。
2. 嵌入资源未命中才查磁盘：分别从 `AppContext.BaseDirectory` 与 `Environment.CurrentDirectory` 向上最多八层，在每层尝试 `Templates/Jsons/Schemas/schema-index.json` 和 `Engine/ColorVision.Engine/Templates/Jsons/Schemas/schema-index.json`；路径去重后逐个读取。
3. 磁盘条目同样按 Code 匹配，Schema 路径相对索引所属的 `Jsons` 目录解析。候选不存在、无法匹配、索引解析或文件读取失败时继续尝试后续候选；全部未命中返回 `null`，属性编辑器收到空 Schema，而不是自动下载或补建文件。

查找阶段只解析索引，不验证 Schema 文本内容。非空但内容无效的嵌入 Schema 也会优先返回；后续编辑器解析失败不会因此触发磁盘回退。不能把“找到了资源”和“Schema 能被消费”当作同一状态。

工程的嵌入资源和 `LogicalName` 规则见 [Engine 资源打包](../../engine-components/ColorVision.Engine.md#资源不是同一种输出文件)。输出目录没有 Schema 散文件不等于漏包；反过来，开发机能通过磁盘回退找到源码文件，也不能证明交付程序集资源正确。检查新增 Schema 时要分别核对 Code、索引条目、资源名和实际产物，不能只确认仓库里存在 JSON 文件。

## 当前模板族

| 目录 | TemplateDicId / Code | 维护重点 |
| --- | --- | --- |
| `LedCheck2/` | `18` / `FindLED` | LED 点阵 V2，schema 为 `FindLED.schema.json` |
| `LEDStripDetectionV2/` | `26` / `LEDStripDetection` | LED 灯条 V2，通常 `Version = 2.0`，有结果 handler 和菜单 |
| `OLEDAOI/` | `28` / `OLED.AOI` | OLED AOI 主模板，含黑屏/四合一/复判子模板 |
| `BinocularFusion/` | `35` / `ARVR.BinocularFusion` | ARVR 双目融合 |
| `SFRFindROI/` | `36` / `ARVR.SFR.FindROI` | SFR 找 ROI，常和 ARVR/SFR 链路一起排查 |
| `BlackMura/` | `37` / `BlackMura.Caculate` | BlackMura 计算和结果展示 |
| `Ghost2/` | `38` / `ghost` | Ghost V2，handler 依赖结果版本 |
| `FOV2/` | `39` / `FOV` | DFOV/FOV V2 |
| `Distortion2/` | `40` / `distortion` | 畸变 V2，handler 依赖结果版本 |
| `BuildPOIAA/` | `41` / `BuildPOI` | 根据 AA 找点结果构建 POI |
| `AAFindPoints/` | `42` / `FindLightArea` | AA 找点/发光区 V2 |
| `PoiAnalysis/` | `44` / `PoiAnalysis` | POI 分析 JSON 模板，版本仍可为 `1.0` |
| `FindCross/` | `45` / `FindCross` | 十字计算，handler 检查结果版本 |
| `MTF2/` | `48` / `MTF` | MTF V2，区别于 ARVR/MTF 旧模板 |
| `SFR2/` | `49` / `SFR` | SFR V2，区别于 ARVR/SFR 旧模板 |
| `ImageROI/` | `52` / `Image.ROI` | JSON 图像 ROI，不等同强类型裁剪模板 |
| `KB/` | `150` / `KB` | KB 项目/算法相关 JSON 模板 |
| `Deprecated/` | 历史模板 | 仅维护兼容旧数据 |

## V2 与旧模板边界

| 模板族 | JSON 路径 | 旧/强类型路径 | 排查顺序 |
| --- | --- | --- | --- |
| LED 点/灯条 | `LedCheck2/`、`LEDStripDetectionV2/` | `LedCheck/`、`LEDStripDetection/` | 模板类型 -> 事件名 -> schema -> handler |
| MTF/SFR/FOV/Ghost/Distortion | `MTF2/`、`SFR2/`、`FOV2/`、`Ghost2/`、`Distortion2/` | `ARVR/*` 或旧模板 | 模板类型 -> MQTT 事件 -> `Version` -> `ViewHandle` |
| ROI/裁剪 | `ImageROI/`、`SFRFindROI/` | `ImageCropping/`、`FindLightArea/`、`POI/` | 参数来源和结果表不同 |
| OLED AOI | `OLEDAOI/` 及子目录 | 项目包或旧 OLED 节点 | 主模板、黑屏、四合一、复判事件和 schema 不同 |

同名算法同时存在旧模板和 JSON 模板时，不要只看目录名；以实际 MQTT 事件、`Version` 和结果 handler 为准。

## 验收

| 场景 | 必验项 |
| --- | --- |
| 编辑 JSON | 文本模式和属性模式互切后 JSON 不丢字段 |
| schema 维护 | 新增/修改 schema 后 `schema-index.json` 能定位文件 |
| V2 执行 | MQTT 参数里的模板名、事件名、`Version` 和服务端预期一致 |
| 结果展示 | `ViewHandle*.cs` 的版本判断能命中实际结果 |
| 导入导出 | 导出后重新导入，名称、`Code`、默认值和 JSON 内容正确 |

## 边界

- 主存储是数据库 `ModMasterModel.JsonVal`，不是磁盘 JSON 配置目录。
- `ITemplateJson<T>` 只提供宿主链，每个模板字段仍由各自 JSON 约定决定。
- `EditTemplateJson` 已有文本/属性/注释三类视图，不只是 AvalonEdit 文本框。
- `CheckCommand` 不是完整 JSON 规则引擎。
- `Deprecated/` 不作为新功能、现场说明或新项目说明的优先入口。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 宿主基类 | `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs` |
| 参数对象 | `TemplateJsonParam.cs` |
| 编辑器 | `EditTemplateJson.xaml.cs` |
| 典型模板 | `PoiAnalysis/TemplatePoiAnalysis.cs`、`SFRFindROI/TemplateSFRFindROI.cs` |
| schema 索引 | `Schemas/schema-index.json` |

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/FlowPackageCompatibilityTests.cs`。

流程包兼容测试覆盖 JSON 模板载荷的部分导入边界，不覆盖编辑器所有视图互切、各 schema 或远端 V2 结果；这些需专门样例。
