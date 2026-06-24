# FocusPoints 关注点模板

`FocusPoints/` 是旧发光区/关注点检测链路里的参数模板目录。它保存二值化、滤波、形态学、面积/矩形过滤和 ROI 范围参数，再交给手动算法页或 Flow 节点使用。

## 速查

| 项 | 值 |
| --- | --- |
| 模板类 / 参数类 | `TemplateFocusPoints` / `FocusPointsParam` |
| 模板字典 / 编码 | `TemplateDicId = 15` / `Code = "focusPoints"` |
| 手动算法入口 | `AlgorithmFocusPoints` |
| MQTT 事件 | `Event_LightArea_GetData` |
| Flow 算子 | `FocusPoints` |
| 模板菜单入口 | `ExportFocusPoints` |

如果排查“发光区检测结果不对”或“Flow 里 FocusPoints 模板选不到”，同时对照 [FindLightArea 发光区定位模板](./find-light-area.md)、[POI 模板](./poi-template.md) 和 [Engine 模板与 Flow 链路](../../engine-components/template-flow-chain.md)。

## 源码入口

| 文件 | 作用 |
| --- | --- |
| `TemplateFocusPoints.cs` | 模板注册、`TemplateDicId`、`Code` 和静态参数集合 |
| `FocusPointsParam.cs` | 关注点检测参数字段 |
| `AlgorithmFocusPoints.cs`、`DisplayFocusPoints.xaml(.cs)` | 手动算法入口、图像来源和 MQTT 请求 |
| `ExportFocusPoints.cs` | 模板菜单项 |
| `AlgorithmNodeConfigurators.cs` | Flow 节点属性面板绑定 `TemplateFocusPoints` |
| `AlgorithmNode.cs`、`AlgorithmLoopNode.cs` | `发光区检测` 映射到 `operatorCode = "FocusPoints"` |

## 参数模型

| 分组 | 字段 | 含义 |
| --- | --- | --- |
| `Binarize` | `Binarize`、`BinarizeThresh` | 是否二值化和阈值 |
| `Blur` | `Blur`、`BlurSize` | 是否均值滤波和滤波尺寸 |
| `Erode` | `Erode`、`ErodeSize` | 是否腐蚀和腐蚀核尺寸 |
| `Dilate` | `Dilate`、`DilateSize` | 是否膨胀和膨胀核尺寸，源码描述文字目前仍写成“腐蚀值” |
| `Param` | `FilterRect`、`Width`、`Height` | 是否启用矩形过滤以及宽高阈值 |
| `FilterArea` | `FilterArea`、`MaxArea`、`MinArea` | 是否按面积过滤以及最大/最小面积 |
| `Roi` | `Roi`、`Left`、`Right`、`Top`、`Bottom` | 是否限制 ROI 以及四边界 |

这里的 `Left/Right/Top/Bottom` 是模板参数，不是结果 overlay 的四点坐标。结果点位、ROI 多边形或 POI 复用看 [ROI 原语](../primitives/roi.md) 和 [POI 原语](../primitives/poi.md)。

## 执行链路

| 路径 | 当前行为 |
| --- | --- |
| 手动执行 | `AlgorithmFocusPoints` 显示名为 `发光区1`；`DisplayFocusPoints` 检查模板、图像输入、`FileExtType`、`DeviceCode/DeviceType` 后发送命令 |
| 请求参数 | `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam` |
| MQTT 事件 | `MQTTAlgorithmEventEnum.Event_LightArea_GetData` |
| Flow `AlgorithmNode` | `AlgorithmType.发光区检测` 设置 `operatorCode = "FocusPoints"`，配置器绑定模板 |
| Flow `AlgorithmLoopNode` | 循环算法属性选择 `发光区检测` 时同样设置 `operatorCode = "FocusPoints"` |
| 发光区节点面板 | 同时提供 JSON AA 找点、ROI、FocusPoints 和保存 POI 模板 |

`FocusPoints` 是旧参数模板和算子编码，`FindLightArea` 是更完整的发光区定位专题。不要只搜 `FocusPoints/` 目录来判断整个发光区能力。

## 菜单和验收

`ExportFocusPoints` 继承 `MenuITemplateAlgorithmBase`，挂在 `MenuITemplateAlgorithm` 下，`Order = 2`，`Header = FocusPoints`，`Template = new TemplateFocusPoints()`。菜单体系见 [模板菜单入口](./template-menu-entries.md)。

| 场景 | 验收点 |
| --- | --- |
| 模板管理 | 能看到 FocusPoints 模板，字段分组和默认值正确 |
| 手动执行 | 本地图像、Raw/CIE 文件、批次三种来源至少验证一种 |
| Flow 执行 | `AlgorithmNode` 选择 `发光区检测` 后能选择 FocusPoints 模板 |
| 结果追踪 | 结合发光区/POI 结果页确认输出落库、overlay 或保存 POI 的实际链路 |

## 维护重点

- `TemplateDicId = 15` 和 `Code = "focusPoints"` 是模板装载和导入导出的关键标识。
- 参数字段大多是图像前处理阈值，不要写成项目判定规则。
- 手动执行使用 `Event_LightArea_GetData`，Flow 节点使用 `operatorCode = "FocusPoints"`。
- `FocusPoints/` 目录当前没有自己的 `ViewHandle*.cs`，结果展示要追发光区、ROI 或 POI 链路。
- `DilateSize` 的描述文字目前写成“腐蚀值”，维护时按字段名理解为膨胀尺寸。
