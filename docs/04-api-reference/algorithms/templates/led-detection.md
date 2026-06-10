# LED 检测模板

本页说明当前仓库中 LED 检测相关模板的交接边界，重点覆盖 `LEDStripDetection/` 和 `LedCheck/` 两个强类型模板，并说明它们和 `Jsons/LEDStripDetectionV2/`、`Jsons/LedCheck2/` 的关系。

## 先分清四个入口

| 入口 | 类型 | 代码/事件 | 适用场景 |
| --- | --- | --- | --- |
| `LEDStripDetection/` | 强类型模板 | `Code = LEDStripDetection`，`Event_LED_StripDetection` | 旧灯条定位，参数由 `LEDStripDetectionParam` 管理。 |
| `LedCheck/` | 强类型模板 | `Code = FindLED`，`Event_LED_Check_GetData` | 灯珠检测，依赖可选/必选 POI 参数，结果绘制圆点。 |
| `Jsons/LEDStripDetectionV2/` | JSON 模板 | `Code = LEDStripDetection`，事件名字符串 `LEDStripDetection`，`Version = 2.0` | 新灯条/POI 中心计算，参数结构更复杂，适合继续扩展。 |
| `Jsons/LedCheck2/` | JSON 模板 | `Code = FindLED`，`Event_OLED_FindDotsArrayMem_GetData` | 亚像素级灯珠检测，带颜色、FDA 类型和固定 LED 点位。 |

交接时不要只凭 `Code` 判断唯一实现：`LEDStripDetection` 和 `FindLED` 都存在强类型旧实现和 JSON 新实现。

## LEDStripDetection 强类型链路

| 文件 | 交接用途 |
| --- | --- |
| `TemplateLEDStripDetection.cs` | 注册强类型灯条模板，`TemplateDicId = 21`，`IsUserControl = true`。 |
| `LEDStripDetectionParam.cs` | 保存点数、点距、起始位置、二值化比例、调试和保存路径。 |
| `EditLEDStripDetection.xaml(.cs)` | 自定义参数编辑控件。 |
| `AlgorithmLEDStripDetection.cs` | 组装 `Event_LED_StripDetection` 请求。 |
| `DisplayLEDStripDetection.xaml(.cs)` | 选择模板、图像来源、批次号/Raw/本地图像，并触发执行。 |

关键参数：

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `Method` | `1` | 算法方法选择，具体含义由算法服务解释。 |
| `PointNumber` | `160` | 灯条点数。 |
| `PointDistance` | `50` | 点间距。 |
| `StartPosition` | `100` | 起始位置。 |
| `BinaryPercentage` | `10` | 二值化比例。 |
| `IsDebug` | `false` | 是否开启调试输出。 |
| `SaveName` | `binim.tif` | 调试/保存图路径。 |

请求参数包括 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam` 和 `IsInversion`。

## LedCheck 强类型链路

| 文件 | 交接用途 |
| --- | --- |
| `TemplateLedCheck.cs` | 注册灯珠检测模板，`Code = FindLED`。 |
| `LedCheckParam.cs` | 保存灯珠通道、固定半径、轮廓面积、二值化补正、灯珠网格数量等参数。 |
| `AlgorithmLedCheck.cs` | 同时收集灯珠模板和 POI 模板，并发布 `Event_LED_Check_GetData`。 |
| `DisplayLedCheck.xaml(.cs)` | 选择灯珠模板、POI 模板和图像来源。 |
| `ViewHandleMTF.cs` | 从 POI 结果表恢复点位，以半径绘制灯珠结果。 |
| `ViewResultLedCheck.cs` | 保存点位和半径。 |

`LedCheck` 的请求比灯条定位多一个 `POITemplateParam`。当前 UI 使用 `TemplatePoi.Params.CreateEmpty()`，因此交接时要确认现场是允许空 POI，还是必须选择具体 POI 模板。

`ViewHandleLedCheck.CanHandle` 当前为空列表。如果现场出现“算法执行成功但结果页不接管展示”，要先检查结果类型是否注册到这个 handler，而不是只排查绘图代码。

## JSON V2 入口

`Jsons/LEDStripDetectionV2/` 和 `Jsons/LedCheck2/` 使用 `ITemplateJson`，参数由 JSON 文本和 `EditTemplateJson` 承载。它们更适合复杂参数和后续扩展：

- `TemplateLEDStripDetectionV2`：`TemplateDicId = 26`，`Name = LedStripDetectionV2`，默认 JSON 包含 `debugCfg`、`mathMaskRect`、`nV1`、`threshold`、`dRatio`、`pattern`、`CalcMethod`。
- `AlgorithmLEDStripDetectionV2`：事件名为 `LEDStripDetection`，会传 `Version = 2.0`，可附带 `POITemplateParam`。
- `TemplateLedCheck2`：`TemplateDicId = 18`，`Code = FindLED`。
- `AlgorithmLedCheck2`：事件为 `Event_OLED_FindDotsArrayMem_GetData`，会传 `Color`、`FDAType` 和四个 `FixedLEDPoint`。

## 选择哪个入口

| 需求 | 建议入口 |
| --- | --- |
| 维护旧灯条定位参数 | `LEDStripDetection/` |
| 新增复杂灯条参数或需要 JSON 版本治理 | `Jsons/LEDStripDetectionV2/` |
| 维护传统灯珠检测和 POI 半径展示 | `LedCheck/` |
| 亚像素级 OLED 点阵检测 | `Jsons/LedCheck2/` |
| 排查结果展示 | 先看结果类型是否被 handler 接管，再看绘图和导出。 |

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 灯条模板下拉为空 | `TemplateLEDStripDetection.Params` 是否加载，`TemplateDicId = 21` 是否存在。 |
| V2 模板下拉为空 | `TemplateLEDStripDetectionV2` 是否加载，`TemplateDicId = 26` 的 JSON 字典是否恢复。 |
| 灯珠检测执行失败 | `TemplateParam`、`POITemplateParam`、输入图像类型和设备 `Code/Type`。 |
| JSON 参数改了不生效 | 确认修改的是 V2 JSON 模板，而不是旧强类型模板。 |
| 结果不显示 | `ViewResultAlgType` 是否能匹配对应 handler，`ViewHandleLedCheck.CanHandle` 是否需要补注册。 |
| 导出 CSV 不对 | `ViewHandleLedCheck.SideSave(...)` 当前写行时误用了表头集合，导出能力需要单独验收。 |

## 交接清单

- 涉及 `Code = LEDStripDetection` 的变更必须说明是旧强类型还是 JSON V2。
- 涉及 `Code = FindLED` 的变更必须说明是 `LedCheck` 还是 `LedCheck2`。
- 修改强类型参数时，同步更新参数类、默认值、编辑控件和现场样例。
- 修改 JSON 参数时，同步更新 schema/说明 JSON、`Mysql*` 恢复命令和版本策略。
- 修改结果展示时，同步更新 handler 的 `CanHandle`、导出、项目验收页和截图样例。

## 继续阅读

- [JSON 模板](./json-templates.md)
- [POI 模板](./poi-template.md)
- [结果交接链路](../../engine-components/result-handoff-chain.md)
- [当前算法模板覆盖清单](../current-algorithm-template-coverage.md)
