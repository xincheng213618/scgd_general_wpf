---
knowledge_id: "algorithms.focus-points"
knowledge_type: "topic"
status: "current"
summary: "说明 FocusPoints 传统模板参数、通用手动宿主与 Flow 发光区检测请求。"
aliases: ["FocusPoints和FindLightArea有什么区别","TemplateFocusPoints","AlgorithmFocusPoints","Event_LightArea_GetData"]
code_paths: ["Engine/ColorVision.Engine/Templates/FocusPoints/TemplateFocusPoints.cs","Engine/ColorVision.Engine/Templates/FocusPoints/FocusPointsParam.cs","Engine/ColorVision.Engine/Templates/FocusPoints/AlgorithmFocusPoints.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs"]
test_paths: []
related: ["algorithms.index","algorithms.find-light-area","algorithms.template-menus"]
---

# FocusPoints 关注点模板

`Engine/ColorVision.Engine/Templates/FocusPoints/` 保存传统发光区/关注点检测参数；手动适配器将参数交给算法服务。它不是全部发光区功能的唯一入口，同时可检索 [FindLightArea](./find-light-area.md)、[ROI](../primitives/roi.md) 和 [POI](./poi-template.md)。

## 契约与位置

| 事项 | 当前实现 |
| --- | --- |
| 模板 / 参数 | `TemplateFocusPoints` / `FocusPointsParam` |
| 字典 / 编码 | `TemplateDicId = 15` / `Code = focusPoints` |
| 手动入口 | `AlgorithmFocusPoints : DisplayAlgorithmBase<SingleTemplateDisplayAlgorithmConfig>`，显示名“发光区1” |
| 通用宿主 | `Services/Devices/Algorithm/DisplayAlgorithmControl.xaml.cs` |
| 手动事件 | `Event_LightArea_GetData` |
| Flow 算子 | `FocusPoints` |

当前没有专用 `DisplayFocusPoints.xaml` 或 `ExportFocusPoints` 菜单类；模板选择与编辑由通用 `DisplayAlgorithmTemplateSelection` 提供，见 [模板入口](./template-menu-entries.md)。

## 参数契约

| 分组 | 字段 | 含义 |
| --- | --- | --- |
| Binarize | `Binarize`、`BinarizeThresh` | 二值化开关和阈值 |
| Blur | `Blur`、`BlurSize` | 均值滤波及尺寸 |
| Erode | `Erode`、`ErodeSize` | 腐蚀及核尺寸 |
| Dilate | `Dilate`、`DilateSize` | 膨胀及核尺寸 |
| Param | `FilterRect`、`Width`、`Height` | 矩形过滤与宽高阈值 |
| FilterArea | `FilterArea`、`MaxArea`、`MinArea` | 面积过滤 |
| Roi | `Roi`、`Left`、`Right`、`Top`、`Bottom` | ROI 限制及四边界 |

`Left/Right/Top/Bottom` 是输入模板参数，不是结果多边形坐标。`DilateSize` 当前描述仍写“腐蚀值”，判断语义要结合字段和服务契约，不要据此改成腐蚀参数。

## 手动与 Flow 路径

手动 `Execute()` 验证 `Config.Template` 与图像输入，`SendCommand` 发出 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam`。当前手动入口设备 code/type 传空字符串，不能沿用旧界面对批次或图像服务选择的承诺。

Flow 的 `AlgorithmNode` / `AlgorithmLoopNode` 将“发光区检测”映射到 `operatorCode = FocusPoints`；普通参数与模板映射优先查 [PropertyGrid 契约](../../ui-components/property-grid.md)，需要多种模板组合的专用面板再查 `FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs`。

`FocusPoints/` 没有自己的 `ViewHandle*.cs`。结果落库、ROI/POI 复用与最终展示需继续沿调用方和 [结果链](../../engine-components/result-handoff-chain.md) 查证，不能虚构独立 handler。

## 验证时需要区分

模板加载依赖字典 15 和 `focusPoints` 编码；手动事件名称与 Flow 算子名称不同。用固定图像验证参数选择、请求和真实下游输出；“模板可选”只证明装载，不证明发光区计算或保存 POI 正确。

## 验证入口与缺口

验证缺口：未登记 FocusPoints 远端请求和结果消费的专门自动化测试；需区分手动事件与 Flow operatorCode，验证实际下游结果而不是假设独立 ViewHandle。
