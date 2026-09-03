---
knowledge_id: "algorithms.focus-points"
knowledge_type: "topic"
status: "current"
summary: "发光区1（FocusPoints）的模板选择、参数初值和图像输入；区分手动 MQTT 模板引用、Flow 算子与计算结果。"
aliases: ["FocusPoints和FindLightArea有什么区别","TemplateFocusPoints","AlgorithmFocusPoints","Event_LightArea_GetData","发光区1","FocusPoints模板","请先选择FocusPoints模板"]
code_paths: ["Engine/ColorVision.Engine/Templates/FocusPoints/TemplateFocusPoints.cs","Engine/ColorVision.Engine/Templates/FocusPoints/FocusPointsParam.cs","Engine/ColorVision.Engine/Templates/FocusPoints/AlgorithmFocusPoints.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs","Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs","Engine/FlowEngineLib/Node/Algorithm/AlgorithmLoopNode.cs","Engine/ColorVision.Engine/Abstractions/IDisplayAlgorithm.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmControl.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Algorithm/DisplayAlgorithmConfiguration.cs","Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs","Engine/ColorVision.Engine/Services/ServicesHelper.cs","Engine/ColorVision.Engine/Templates/ModelBase.cs"]
test_paths: []
related: ["algorithms.index","algorithms.find-light-area","algorithms.template-menus"]
---

# FocusPoints 关注点模板

`Engine/ColorVision.Engine/Templates/FocusPoints/` 保存传统发光区/关注点检测参数；手动适配器将参数交给算法服务。它不是全部发光区功能的唯一入口，同时可检索 [FindLightArea](./find-light-area.md)、[ROI](../primitives/roi.md) 和 [POI](./poi-template.md)。

## 手动使用

1. 在可用算法设备的通用手动面板中选择“数据提取算法”分组的 **发光区1**。
2. 在 **FocusPoints模板** 中选择已有模板；需要编辑时使用模板旁的编辑命令并保存。模板加载依赖字典 `15` 和编码 `focusPoints`，模板选择与持久化见[模板入口](./template-menu-entries.md)。
3. 设置图像文件路径，确认算法服务能读取该文件。当前输入检查只拒绝空路径，再按扩展名确定 `FileType`，不会检查文件存在性或远端访问权限。
4. 点击 **计算**，核对请求记录、服务返回及本次实际结果。返回 `MsgRecord` 表示已建立请求记录并发起发送，不能据此判断网络发送或计算完成。

## 契约与位置

| 事项 | 当前实现 |
| --- | --- |
| 模板 / 参数 | `TemplateFocusPoints` / `FocusPointsParam` |
| 字典 / 编码 | `TemplateDicId = 15` / `Code = focusPoints` |
| 手动入口 | `AlgorithmFocusPoints : DisplayAlgorithmBase<SingleTemplateDisplayAlgorithmConfig>`，显示名“发光区1” |
| 通用宿主 | `Services/Devices/Algorithm/DisplayAlgorithmControl.xaml.cs` |
| 手动事件 | `Event_LightArea_GetData` |
| Flow 算子 | `FocusPoints` |

模板选择与编辑由通用 `DisplayAlgorithmTemplateSelection` 提供，执行界面由 `DisplayAlgorithmControl` 根据配置生成。

## 参数契约

| 分组 | 字段 | 新空对象初值 | 含义 |
| --- | --- | --- | --- |
| Binarize | `Binarize`、`BinarizeThresh` | `false`、`0` | 二值化开关和阈值 |
| Blur | `Blur`、`BlurSize` | `false`、`0` | 均值滤波及尺寸 |
| Erode | `Erode`、`ErodeSize` | `false`、`0` | 腐蚀及核尺寸 |
| Dilate | `Dilate`、`DilateSize` | `false`、`0` | 膨胀及核尺寸 |
| Param | `FilterRect`、`Width`、`Height` | `false`、`100`、`100` | 矩形过滤与宽高阈值 |
| FilterArea | `FilterArea`、`MaxArea`、`MinArea` | `false`、`100`、`100` | 面积过滤 |
| Roi | `Roi`、`Left`、`Right`、`Top`、`Bottom` | `false`，四个边值均为 `100` | ROI 开关与四边参数 |

上表是无明细的新 `FocusPointsParam` 对象初值，不是已保存模板的默认配置，也不是推荐测量参数。加载后 `ModelBase.GetValue` 优先读取模板明细的 `ValueA`；明细集合非空却缺少某个字段时返回该类型的默认值。该参数类没有数值范围或边界关系校验，算法服务接受的范围需按对应实现确认。

`Left/Right/Top/Bottom` 是输入模板参数，不是结果多边形坐标。`DilateSize` 当前描述仍写“腐蚀值”，判断语义要结合字段和服务契约，不要据此改成腐蚀参数。

## 手动与 Flow 路径

手动 `Execute()` 要求有效模板选择及非空图像路径。`SendCommand` 的 `Params` 包含 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam`：

- `TemplateParam` 只传所选参数对象的 `ID` 和 `Name`，不内联发送二值化、滤波等数值；服务端需要能解析这个模板引用。
- 手动入口的 `Params.DeviceCode` / `Params.DeviceType` 为空字符串，`SerialNumber` 也为空。消息外层的 `DeviceCode`、`Token`、`ServiceName` 为 `null` 时，`MQTTServiceBase` 会补入对应服务值；两层字段含义不同。
- `FileType` 按扩展名映射：`.cvraw → Raw`、`.cvcie → CIE`、`.tif/.tiff → Tif`，其它扩展名为 `Src`；这只是请求分类，不保证服务支持该文件内容。

Flow 的 `AlgorithmNode` / `AlgorithmLoopNode` 将“发光区检测”映射到 `operatorCode = FocusPoints`；普通参数与模板映射优先查 [PropertyGrid 契约](../../ui-components/property-grid.md)，需要多种模板组合的专用面板再查 `FlowProcessing/Editor/NodeConfiguration/AlgorithmNodeConfigurators.cs`。

结果落库、ROI/POI 复用与最终展示由调用方及通用[结果链](../../engine-components/result-handoff-chain.md)衔接；本目录提供参数与请求适配器。

## 排查与验证

| 现象 | 先确认 |
| --- | --- |
| 模板列表为空或提示“请先选择FocusPoints模板” | 字典、编码、模板是否加载，以及选择项是否有 `FocusPointsParam` 值 |
| 发出请求但没有结果 | 算法设备与服务状态、请求事件、模板 ID/名称，以及服务可读的图像路径 |
| 手动成功、Flow 失败 | 两条路径使用的模板、图像及算子配置；手动事件名与 Flow `operatorCode` 不同 |
| 参数或轮廓不符合预期 | 实际保存的模板明细、图像条件和服务返回，不能只看新对象初值或编辑器显示 |

当前未登记 FocusPoints 远端请求和结果消费的专项自动化测试。验证时使用获准的固定图像，把参数选择、实际请求、服务结果和最终展示关联起来；模板可选不证明计算或保存 POI 成功。
