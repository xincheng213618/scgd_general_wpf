---
knowledge_id: "engine.cvcie-results"
knowledge_type: "topic"
status: "current"
summary: "CVCIE POI 结果的 XYZ 非正值替换开关、可配置最小值，以及色坐标、色温和主波长重算。"
aliases: ["CVCIE计算负值", "CVCIEShowConfig", "WindowCVCIE", "启用非正值替换", "ClampNonPositiveValues", "MinimumValue", "0.0001"]
code_paths: ["Engine/ColorVision.Engine/Media/WindowCVCIE.xaml.cs", "Engine/ColorVision.Engine/Media/CVRawOpen.cs", "Engine/ColorVision.Engine/Services/POI/PoiMeasurementService.cs", "Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/PoiResultCIExyuvData.cs", "Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/PoiResultData.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CvcieResultValueTests.cs", "Test/ColorVision.UI.Tests/PoiMeasurementServiceTests.cs"]
related: ["engine.file-io", "algorithms.poi-routes", "engine.opencv-helper-api", "ui.property-grid"]
---

# CVCIE POI 结果数值

在 CVCIE 的 POI 结果窗口点击齿轮，进入 `CVCIEShowConfig` 的“计算结果”分类，配置非正值处理。该配置随应用配置保存，旧配置缺少新字段时使用默认值。

| 配置 | 默认值 | 行为 |
| --- | --- | --- |
| 启用非正值替换 `ClampNonPositiveValues` | 开启 | 将 X、Y、Z 中小于或等于 0 的值替换为最小值；单通道结果处理 Y |
| 最小值 `MinimumValue` | `0.0001` | 非正值的替换值，接受大于或等于 0 的有限数值；输入负数、NaN 或无穷大时保留先前设置 |

正常正值保持实际值，即使它小于配置的最小值。XYZ 发生替换时，根据替换后的 XYZ 重算 x、y、u′、v′、CCT 和 Wave，不单独替换色坐标。重算复用 POI 原生算法的色温公式与主波长表；XYZ 全零或含无穷值时，衍生值为 NaN。开启时延续旧 XYZ 结果转换对 NaN 的替换规则；关闭或 XYZ 未变化时保留原始计算结果。

## 生效时机与输出

`CVRawOpen` 的 POI 命令在开始计算时取得配置快照，图上标注、结果列表、统计与 CSV 使用相同数值。配置从下一次计算开始持续生效，已有结果和标注不刷新。

流程或历史结果通过 `PoiResultCIExyuvData(PoiPointResultModel)` / `PoiResultCIEYData(PoiPointResultModel)` 创建显示数据时也使用该配置；已经缓存的结果不回写。数据库中的原始 `Value` 和 CVCIE 文件保持原样。

本地 POI 使用 `PoiMeasurementService.CalculateRaw` 和 V2 的 `PreserveNonPositiveValues` 标志跳过原生固定下限，再统一应用配置；关闭开关时能拿到实际负值。常规 `Calculate`、鼠标探针和真彩图像显示不受此配置控制。原生标志与兼容边界见 [POI Batch API](./opencv-helper-api.md#poi-batch-api)。

## 验证边界

`CvcieResultValueTests` 覆盖配置兼容、持续生效、XYZ 替换与衍生参数重算；`PoiMeasurementServiceTests` 覆盖原生负值保留和旧调用行为。真实图像与窗口交互需结合本地样本检查。
