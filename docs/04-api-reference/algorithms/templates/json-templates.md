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
| 新 schema 找不到 | `Jsons/Schemas/schema-index.json` 是否登记，文件是否发布 |
| 误改 Deprecated | `Deprecated/` 是历史兼容目录，不作为新功能入口 |

## 主链路

| 层 | 当前入口 | 说明 |
| --- | --- | --- |
| 宿主基类 | `ITemplateJson<T>` | 读取 `ModMasterModel.JsonVal`，包装成 `TemplateModel<T>`，处理保存/删除/复制/导入/导出 |
| 参数对象 | `TemplateJsonParam` | 持有 `TemplateJsonModel`、`ResetCommand`、`CheckCommand`、`JsonValueChanged` |
| 编辑控件 | `EditTemplateJson` | AvalonEdit 文本模式、属性模式、注释视图、校验按钮、外部 json.cn 辅助 |
| schema | `Jsons/Schemas/schema-index.json` | 维护各 JSON 模板 schema 索引 |

`JsonValue` 读取时会格式化，写入时只有 JSON 合法才回写 `TemplateJsonModel.JsonVal`。`ResetValue()` 回到字典模板默认 JSON，不是清空文本。

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
