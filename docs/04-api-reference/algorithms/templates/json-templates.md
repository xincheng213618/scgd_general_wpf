# JSON 模板

本页只描述当前仓库里真实可用的 JSON 模板宿主链，不再继续维护“通用算法 DSL 平台 + 跨项目配置框架”式旧稿。

## 先看这个模块现在是什么

按当前源码状态，JSON 模板系统不是一套脱离数据库独立存在的配置平台，而是 `ColorVision.Engine` 模板体系中的一个具体分支。它当前的核心目标是：

- 把 `ModMasterModel.JsonVal` 里的 JSON 内容托管成模板项。
- 通过通用编辑器 `EditTemplateJson` 提供文本编辑和属性编辑两种模式。
- 让具体模板类型以 `ITemplateJson<T>` 的形式复用同一套加载、保存、导入导出逻辑。
- 为像 `PoiAnalysis`、`SFRFindROI` 这类 JSON 驱动模板提供统一宿主。

因此它更像“数据库中的 JSON 模板分支”，而不是一个完全独立的配置子系统。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

如果只是想看“JSON 模板现在怎么存、怎么编、怎么挂进模板窗口”，这些文件已经覆盖主干。

## 当前 JSON 子模板目录

`Jsons/` 目录下不是一种模板，而是一组共享同一 JSON 宿主的具体算法模板。当前源码可分为下面几类：

| 目录 | 模板/字典 | 算法事件 | 结果/菜单 | 交接重点 |
| --- | --- | --- | --- | --- |
| `LedCheck2/` | `TemplateLedCheck2`，`TemplateDicId = 18`，`Code = FindLED` | `Event_OLED_FindDotsArrayMem_GetData` | 无专属 handler | LED 点阵 V2 JSON 模板，schema 为 `FindLED.schema.json`。 |
| `LEDStripDetectionV2/` | `TemplateLEDStripDetectionV2`，`TemplateDicId = 26`，`Code = LEDStripDetection` | `LEDStripDetection`，`Version = 2.0` | `ViewHandleLEDStripDetectionV2`，`MenuLEDStripDetectionV2` | LED 灯条 V2 路径，区别于旧强类型 `LEDStripDetection/`。 |
| `OLEDAOI/` | `TemplateOLEDAOI`，`TemplateDicId = 28`，`Code = OLED.AOI` | `OLEDAOI`，`Version = 2.0` | `ViewHandleOLEDAOI`，`MenuOLEDAOI` | OLED AOI 主模板，下面还有黑屏/四合一/复判子模板。 |
| `BinocularFusion/` | `TemplateBinocularFusion`，`TemplateDicId = 35`，`Code = ARVR.BinocularFusion` | `ARVR.BinocularFusion` | `ViewHandleBinocularFusion` | ARVR 双目融合 JSON 模板。 |
| `SFRFindROI/` | `TemplateSFRFindROI`，`TemplateDicId = 36`，`Code = ARVR.SFR.FindROI` | `ARVR.SFR.FindROI` | `ViewHandleSFRFindROI` | SFR 找 ROI，常与 ARVR/SFR 链路一起排查。 |
| `BlackMura/` | `TemplateBlackMura`，`TemplateDicId = 37`，`Code = BlackMura.Caculate` | `BlackMura.Caculate` | `ViewHandleBlackMura` | BlackMura 计算模板和结果展示。 |
| `Ghost2/` | `TemplateGhostQK`，`TemplateDicId = 38`，`Code = ghost` | `Ghost`，`Version = 2.0` | `ViewHandleGhostQK`，`MenuGhost2` | Ghost V2，handler 依赖结果版本 `2.0`。 |
| `FOV2/` | `TemplateDFOV`，`TemplateDicId = 39`，`Code = FOV` | `FOV`，`Version = 2.0` | `ViewHandleDFOV` | DFOV/FOV V2 JSON 路径。 |
| `Distortion2/` | `TemplateDistortion2`，`TemplateDicId = 40`，`Code = distortion` | `Distortion`，`Version = 2.0` | `ViewHandleDistortion2` | 畸变 V2，handler 依赖结果版本 `2.0`。 |
| `BuildPOIAA/` | `TemplateBuildPOIAA`，`TemplateDicId = 41`，`Code = BuildPOI` | `ARVR.AA.FindPoints`，`Version = 2.0` | 无专属 handler | 根据 AA 找点结果构建 POI 的 JSON 模板。 |
| `AAFindPoints/` | `TemplateAAFindPoints`，`TemplateDicId = 42`，`Code = FindLightArea` | `ARVR.AA.FindPoints`，`Version = 2.0` | `ViewHandleAAFindPoints` | AA 找点/发光区 V2，handler 还会看结果版本。 |
| `PoiAnalysis/` | `TemplatePoiAnalysis`，`TemplateDicId = 44`，`Code = PoiAnalysis` | `PoiAnalysis`，`Version = 1.0` | `ViewHandlePoiAnalysis` | POI 分析 JSON 模板，版本仍是 `1.0`。 |
| `FindCross/` | `TemplateFindCross`，`TemplateDicId = 45`，`Code = FindCross` | `FindCross` | `ViewHandleFindCross` | 十字计算模板，handler 当前检查结果版本 `1.0`。 |
| `MTF2/` | `TemplateMTF2`，`TemplateDicId = 48`，`Code = MTF` | `MTF`，`Version = 2.0` | `ViewHandleMTF2` | MTF V2，区别于 ARVR/MTF 旧模板。 |
| `SFR2/` | `TemplateSFR2`，`TemplateDicId = 49`，`Code = SFR` | `SFR`，`Version = 2.0` | `ViewHandleSFR2` | SFR V2，区别于 ARVR/SFR 旧模板。 |
| `ImageROI/` | `TemplateImageROI`，`TemplateDicId = 52`，`Code = Image.ROI` | `Image.ROI` | 无专属 handler | JSON 图像 ROI，区别于强类型 [ImageCropping 图像裁剪模板](./image-cropping-template.md)。 |
| `KB/` | `TemplateKB`，`TemplateDicId = 150`，`Code = KB` | `KB` | `ViewHandleKB` | KB 项目/算法相关 JSON 模板。 |
| `Deprecated/` | `TemplateCaliAngleShift`、`TemplateCompoundImg` | `CaliAngleShift`、`CompoundImg` | 对应旧 handler | 历史兼容目录，新交接不要优先扩展这里。 |

`Schemas/schema-index.json` 是当前 schema 索引，列出了多数组件 schema 文件，例如 `FindLED.schema.json`、`LEDStripDetection.schema.json`、`OLED.AOI.schema.json`、`ARVR.SFR.FindROI.schema.json`、`SFR.schema.json` 和 `Image.ROI.schema.json`。新增 JSON 模板时，应同步考虑是否需要把 schema 放入对应目录并登记到 schema index。

## V2 与旧强类型模板边界

很多目录名带 `2` 或 `V2`，但真正影响结果 handler 的不是目录名，而是请求参数和结果版本：

| 模板族 | 当前 JSON 路径 | 旧/强类型路径 | 交接边界 |
| --- | --- | --- | --- |
| LED 点/灯条 | `LedCheck2/`、`LEDStripDetectionV2/` | `LedCheck/`、`LEDStripDetection/` | V2 主要走 JSON schema 和 `Version = 2.0`，不要混用旧模板字段。 |
| MTF/SFR/FOV/Ghost/Distortion | `MTF2/`、`SFR2/`、`FOV2/`、`Ghost2/`、`Distortion2/` | `ARVR/MTF`、`ARVR/SFR`、`ARVR/FOV`、`ARVR/Ghost`、旧畸变模板 | handler 通常通过 `result.Version` 区分，排查结果展示时必须看版本。 |
| ROI/裁剪 | `ImageROI/`、`SFRFindROI/` | `ImageCropping/`、`FindLightArea/`、`POI/` | JSON ROI 和强类型裁剪不是同一条链，参数来源和结果表不同。 |
| OLED AOI | `OLEDAOI/` 及其子目录 | 项目包或旧 OLED 节点 | 主模板与黑屏/四合一/复判子模板共享 AOI 领域，但事件名和 schema 不同。 |

交接时如果看到同一个算法名既有旧模板又有 JSON 模板，应按“模板类型 -> MQTT 事件 -> Version -> ViewHandle”四步确认当前走哪条链。

## 当前主链怎么跑

### 宿主基类

`ITemplateJson<T>` 是 JSON 模板分支的通用宿主。它当前负责：

- 用 `TemplateDicId` 从 MySQL 读取 `ModMasterModel`
- 把每条记录包装成 `TemplateModel<T>`
- 提供保存、删除、复制、导入、导出
- 在创建新模板时，从字典模板默认 JSON 生成初始内容

这意味着 JSON 模板虽然长得像纯文本编辑，但当前仍然深度依赖模板字典和数据库记录。

### 参数对象

`TemplateJsonParam` 当前是最基础的 JSON 模板参数对象。它持有：

- `TemplateJsonModel`
- `ResetCommand`
- `CheckCommand`
- `JsonValueChanged` 事件

其中 `JsonValue` 的真实语义是：

- 读取时用 `JsonHelper.BeautifyJson(...)` 格式化
- 写入时只有在 JSON 合法时才回写 `TemplateJsonModel.JsonVal`

`ResetValue()` 则会回到字典模板记录的默认 JSON，而不是简单清空本地文本。

### 编辑器控件

`EditTemplateJson` 是当前真正的编辑入口。它现在同时支持：

- AvalonEdit 文本模式
- `JsonPropertyEditorControl` 属性模式
- 描述注释视图切换
- 校验按钮
- 外部 JSON 网站辅助入口

其中右下角的 `json` 按钮当前实际行为很明确：

- 打开 `https://www.json.cn/`
- 把当前 JSON 复制到剪贴板

这就是当前活动文件里 `Button_Click_1` 的真实作用，不是其它隐藏命令。

### 模式切换与同步

`EditTemplateJson` 当前不是简单文本框包装。它会：

- 用防抖定时器同步文本改动
- 通过 `IEditTemplateJson.JsonValueChanged` 反向刷新界面
- 在文本模式与属性模式之间切换时同步 JSON 内容
- 用 `EditTemplateJsonConfig` 记住宽度和默认编辑模式

因此这里的复杂度主要在“两个编辑面保持同一份 JSON 一致”，而不是算法本身。

## 当前几个最容易写错的点

### 它不是通用文件模板平台

当前 JSON 模板的主存储是 MySQL 的 `ModMasterModel.JsonVal`，不是仓库里一组任意 JSON 文件。继续把它写成“读取磁盘配置目录”会偏离真实实现。

### 不是所有 JSON 模板共享同一个业务 schema

`ITemplateJson<T>` 只提供宿主链；每个具体模板实际需要什么字段，仍由各自的 JSON 约定决定。文档不能再把某一类 JSON 结构写成全系统统一规范。

### 编辑器已经不只是文本编辑器

当前 `EditTemplateJson` 已经支持属性模式和描述模式切换。只描述 AvalonEdit 文本框，会漏掉用户实际看到的一半功能。

### “校验”当前主要是事件触发，不是完整编译器

`CheckCommand` 触发的是 `JsonValueChanged` 事件链，具体怎么响应取决于调用方。不要把它写成独立的 JSON 规则引擎。

### Deprecated 目录不是新功能入口

`Deprecated/` 下仍保留 `CaliAngleShift`、`CompoundImg` 等旧模板和 handler，用于兼容历史数据。新增功能、现场交接和新项目说明不要优先引用这个目录，除非明确在维护旧流程。

## 验收建议

| 场景 | 必验项 |
| --- | --- |
| 编辑 JSON | 文本模式和属性模式互相切换后 JSON 不丢字段 |
| schema 维护 | 新增或修改 schema 后，`Schemas/schema-index.json` 能定位对应文件 |
| V2 算法执行 | MQTT 参数里 `TemplateParam`、`Version`、事件名和服务端预期一致 |
| 结果展示 | `ViewHandle*.cs` 的 `CanHandle1` 或版本判断能匹配实际结果 |
| 导入导出 | JSON 模板导出后重新导入，名称、`Code`、默认值和 JSON 内容正确 |

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
3. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 继续阅读

- [Templates API 参考](./api-reference.md)
- [模板管理](./template-management.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)
