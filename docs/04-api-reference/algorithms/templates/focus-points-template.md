# FocusPoints 关注点模板

`FocusPoints/` 是旧发光区/关注点检测链路里的参数模板目录。它不只是“点位列表”，而是把二值化、滤波、形态学、面积/矩形过滤和 ROI 范围这些前处理参数保存成模板，再交给手动算法页或 Flow 节点使用。

## 先看结论

- 模板类：`TemplateFocusPoints`
- 参数类：`FocusPointsParam`
- 模板字典：`TemplateDicId = 15`
- 模板编码：`Code = "focusPoints"`
- 手动算法入口：`AlgorithmFocusPoints`
- MQTT 事件：`Event_LightArea_GetData`
- Flow 算子：`FocusPoints`
- 模板菜单入口：`ExportFocusPoints`

如果接手“发光区检测结果不对”或“Flow 里 FocusPoints 模板选不到”，先读这页，再对照 [FindLightArea 发光区定位模板](./find-light-area.md)、[POI 模板](./poi-template.md) 和 [Engine 模板与 Flow 链路](../../engine-components/template-flow-chain.md)。

## 源码入口

| 文件 | 作用 |
| --- | --- |
| `Engine/ColorVision.Engine/Templates/FocusPoints/TemplateFocusPoints.cs` | 模板注册、`TemplateDicId`、`Code` 和静态参数集合 |
| `Engine/ColorVision.Engine/Templates/FocusPoints/FocusPointsParam.cs` | 关注点检测参数字段 |
| `Engine/ColorVision.Engine/Templates/FocusPoints/AlgorithmFocusPoints.cs` | 手动算法入口和 MQTT 请求封装 |
| `Engine/ColorVision.Engine/Templates/FocusPoints/DisplayFocusPoints.xaml(.cs)` | 手动执行页，选择模板、图像来源和批次 |
| `Engine/ColorVision.Engine/Templates/FocusPoints/ExportFocusPoints.cs` | 模板菜单项 |
| `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs` | Flow 节点属性面板绑定 `TemplateFocusPoints` |
| `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs` | `AlgorithmType.发光区检测` 映射到 `operatorCode = "FocusPoints"` |
| `Engine/FlowEngineLib/Node/Algorithm/AlgorithmLoopNode.cs` | 循环算法节点里同样映射 `FocusPoints` |

## 参数模型

`FocusPointsParam` 继承 `ParamModBase`，字段来自模板明细表，按 `Category` 分组显示在 PropertyGrid 里。

| 分组 | 字段 | 交接含义 |
| --- | --- | --- |
| `Binarize` | `Binarize`、`BinarizeThresh` | 是否二值化和二值化阈值 |
| `Blur` | `Blur`、`BlurSize` | 是否做均值滤波和滤波尺寸 |
| `Erode` | `Erode`、`ErodeSize` | 是否腐蚀和腐蚀核尺寸 |
| `Dilate` | `Dilate`、`DilateSize` | 是否膨胀和膨胀核尺寸，源码描述文字目前仍写成“腐蚀值” |
| `Param` | `FilterRect`、`Width`、`Height` | 是否启用矩形过滤以及宽高阈值 |
| `FilterArea` | `FilterArea`、`MaxArea`、`MinArea` | 是否按面积过滤以及最大/最小面积 |
| `Roi` | `Roi`、`Left`、`Right`、`Top`、`Bottom` | 是否限制 ROI 以及四边界 |

这里的 `Left/Right/Top/Bottom` 是模板参数，不是结果 overlay 的四点坐标。要看结果点位、ROI 多边形或 POI 复用，请转到 [ROI 原语](../primitives/roi.md) 和 [POI 原语](../primitives/poi.md)。

## 手动执行链路

手动页由 `AlgorithmFocusPoints` 提供，显示名是 `发光区1`，分类是 `数据提取算法`。

执行时 `DisplayFocusPoints` 会：

1. 检查是否选择了 FocusPoints 模板。
2. 从批次、Raw/CIE 服务文件或本地文件中取图像输入。
3. 根据扩展名推断 `FileExtType`：`.cvraw`、`.cvcie`、`.tif`，否则走 `Src`。
4. 从选中的图像服务读取 `DeviceCode` 和 `DeviceType`。
5. 调用 `AlgorithmFocusPoints.SendCommand(...)`。

发送给算法服务的参数包括：

| 参数 | 来源 |
| --- | --- |
| `ImgFileName` | 批次/服务文件/本地文件，若存在历史文件映射会替换为完整路径 |
| `FileType` | 由文件扩展名推断 |
| `DeviceCode` | 当前图像来源服务 |
| `DeviceType` | 当前图像来源服务类型 |
| `TemplateParam` | 当前 `FocusPointsParam` 的 `Id` 和 `Name` |

事件名固定为 `MQTTAlgorithmEventEnum.Event_LightArea_GetData`。如果现场反馈“点位检测没执行”，要同时查模板是否选中、图像文件是否为空、服务端是否支持这个事件名。

## Flow 接入

`FocusPoints` 在 Flow 中至少有两条入口：

| Flow 入口 | 当前行为 |
| --- | --- |
| `AlgorithmNode` | `AlgorithmType.发光区检测` 时设置 `operatorCode = "FocusPoints"`，节点配置器绑定 `TemplateFocusPoints` |
| `AlgorithmLoopNode` | 循环算法属性选择 `发光区检测` 时同样设置 `operatorCode = "FocusPoints"` |
| `AlgorithmFindLightAreaNode` 配置器 | 同一个发光区节点面板里同时提供 JSON AA 找点、ROI、FocusPoints 和保存 POI 模板 |

因此交接时要区分两件事：

- `FocusPoints` 是旧参数模板和算子编码。
- `FindLightArea` 是更完整的发光区定位专题，包含 ROI、结果点和保存 POI 的上下文。

不要只搜 `FocusPoints/` 目录来判断整个发光区能力，Flow 配置器里还会把 `TemplateRoi`、`TemplateAAFindPoints`、`TemplatePoi` 一起暴露给同一个节点。

## 菜单入口

`ExportFocusPoints` 继承 `MenuITemplateAlgorithmBase`：

| 属性 | 值 |
| --- | --- |
| `OwnerGuid` | 来自基类，挂在 `MenuITemplateAlgorithm` 下 |
| `Order` | `2` |
| `Header` | `FocusPoints` |
| `Template` | `new TemplateFocusPoints()` |

这意味着它出现在“模板 -> 算法”这类模板菜单分组下。菜单体系的父子关系见 [模板菜单入口](./template-menu-entries.md)。

## 交接重点

- `TemplateDicId = 15` 和 `Code = "focusPoints"` 是模板装载和导入导出的关键标识。
- 参数字段大多是图像前处理阈值，不要把它们写成项目判定规则。
- 手动执行使用 `Event_LightArea_GetData`，而 Flow 节点使用 `operatorCode = "FocusPoints"`，二者名字不一样。
- `FocusPoints/` 目录当前没有自己的 `ViewHandle*.cs`。结果展示、保存 POI 和 overlay 通常要追发光区、ROI 或 POI 相关链路。
- `DilateSize` 的描述文字目前写成“腐蚀值”，交接时应按字段名理解为膨胀尺寸，修改 UI 文案需要同步模板字典或资源。

## 验收建议

| 场景 | 验收点 |
| --- | --- |
| 模板管理 | 能在模板窗口看到 FocusPoints 模板，字段分组和默认值正确 |
| 手动执行 | 本地图像、Raw/CIE 文件、批次三种来源至少验证一种 |
| Flow 执行 | `AlgorithmNode` 选择 `发光区检测` 后能选择 FocusPoints 模板 |
| 结果追踪 | 结合发光区/POI 结果页确认输出落库、overlay 或保存 POI 的实际链路 |

## 继续阅读

- [FindLightArea 发光区定位模板](./find-light-area.md)
- [POI 模板](./poi-template.md)
- [ROI 原语](../primitives/roi.md)
- [Engine 模板与 Flow 链路](../../engine-components/template-flow-chain.md)
- [当前算法模板覆盖清单](../current-algorithm-template-coverage.md)
