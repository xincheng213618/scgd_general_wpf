# Matching 模板匹配

本页说明 `Engine/ColorVision.Engine/Templates/Matching/` 的模板、手动算法页、Flow 节点和 AOI 结果展示。`Matching` 当前负责模板匹配/定位类能力：前端收集输入图像和模板文件，向算法服务发送 `MatchTemplate`，结果按 AOI 形式回写并绘制四点多边形。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 模板类 | `TemplateMatch : ITemplate<MatchParam>, IITemplateLoad` |
| 参数类 | `MatchParam : ParamModBase` |
| 模板代码 | `MatchTemplate` |
| 字典 ID | `TemplateDicId = 34` |
| 手动算法入口 | `AlgorithmMatching` |
| 显示控件 | `DisplayMatching.xaml(.cs)` |
| 菜单入口 | `ExportMenuItemMatching`，顺序 `50` |
| MQTT 事件 | `MQTTAlgorithmEventEnum.Event_MatchTemplate` |
| Flow 节点 | `AlgorithmTMNode` |
| 结果类型 | `ViewResultAlgType.AOI` |
| 结果表 | `t_scgd_algorithm_result_detail_aoi` |
| 结果 handler | `ViewHandleMatching` |

## 源码入口

| 文件 | 用途 |
| --- | --- |
| `TemplateMatch.cs` | 注册模板名、模板代码和字典 ID。 |
| `MatchParam.cs` | 保存模板匹配参数。 |
| `AlgorithmMatching.cs` | 手动执行入口，组装 MQTT 请求。 |
| `DisplayMatching.xaml(.cs)` | 提供模板选择、模板文件选择、图像来源和执行按钮。 |
| `ViewHandleMatching.cs` | 加载 AOI 结果、绘制四点多边形、生成结果表格列。 |
| `AlgResultAoiDao.cs` | `t_scgd_algorithm_result_detail_aoi` 的模型和 DAO。 |
| `FlowEngineLib/Node/Algorithm/AlgorithmTMNode.cs` | Flow 中的模板匹配节点，`operatorCode = MatchTemplate`。 |

## 参数模型

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `MinReducedArea` | `256` | 取样细致度，描述中标注范围 `64 ~ 2048`。 |
| `ToleranceAngle` | `0` | 误差角度，描述中标注范围 `0-180`。 |
| `Similarity` | `0.7` | 相似度阈值，描述中标注范围 `0-1`。 |
| `MaxOverlapRatio` | `0` | 最大交叠比例，描述中标注范围 `0-0.8`。 |
| `TargetNumber` | `70` | 目标数量。 |

`TemplateFile` 不是 `MatchParam` 字段，而是 `AlgorithmMatching` 和 `AlgorithmTMNode` 运行时参数。维护时要把“参数模板”和“模板文件”分开记录。

## 手动执行链路

菜单打开 `AlgorithmMatching` 后，`DisplayMatching` 绑定 `TemplateMatch.Params`。用户选择参数模板和 `TemplateFile`，图像来源可以是本地文件、服务端 Raw/CIE 文件或批次号。`RunTemplate_Click` 调用 `SendCommand(...)`，请求参数包括 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateFile`、`TemplateParam`，MQTT 事件为 `Event_MatchTemplate`。

当前 XAML 中模板 ComboBox 的 `SelectedIndex` 绑定到 `TemplatePoiSelectedIndex`，而 `SendCommand` 读取的是 `TemplateSelectedIndex`。如果界面选择了模板但发送时仍使用第一条模板，优先检查这处绑定。

## Flow 执行链路

`AlgorithmTMNode` 是 Flow 中的模板匹配节点：

| 属性 | 说明 |
| --- | --- |
| `TempName` | 参数模板名，最终进入 `TemplateParam`。 |
| `TemplateFile` | 匹配用模板文件路径。 |
| `ImgFileName` | 输入图像。 |
| `operatorCode` | 固定为 `MatchTemplate`。 |

节点执行时构造 `TMParam(TemplateFile)`，再通过 `BuildImageParam(...)` 补图像参数。它和手动执行页走同一个服务端事件语义。

## 结果展示

`ViewHandleMatching` 处理 `ViewResultAlgType.AOI`：从 `AlgResultAoiDao.GetAllByPid(result.Id)` 读取明细，打开 `result.FilePath` 原图，取每条 AOI 明细四个角点，通过 `GrahamScan.ComputeConvexHull(...)` 生成凸包，再用蓝色 `DVPolygon` 绘制 overlay，并在表格显示分数、角度、中心点和四个角点坐标。

结果表 `t_scgd_algorithm_result_detail_aoi` 的关键字段包括 `score`、`angle`、`center_x/y`、`left_top_x/y`、`right_top_x/y`、`right_bottom_x/y`、`left_bottom_x/y`。

当前 `Load(...)` 里只有在 `result.ViewResults != null` 时才重新读取 DAO。如果历史结果页没有加载 AOI 明细，优先检查调用链是否已经初始化 `ViewResults`，以及这里的判断是否需要调整。

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 服务没有执行 | `DeviceCode`、`DeviceType`、`Event_MatchTemplate` 和算法服务在线状态。 |
| 模板文件无效 | `TemplateFile` 是否存在，服务端是否能访问该路径。 |
| 参数模板不生效 | ComboBox 绑定、`TemplateSelectedIndex` 和 `TemplateMatch.Params`。 |
| 结果列表为空 | 主结果类型是否为 `AOI`，明细表是否有同 `pid` 数据。 |
| overlay 位置不对 | 四角坐标是否是原图坐标，图像是否被缩放或换源。 |
| 表格列重复 | 当前表头最后两列都写成“左下点x”，应核对是否需要显示 `LeftBottomY`。 |

## 检查清单

- 同时记录参数模板、模板文件、输入图像来源和算法服务设备。
- 修改匹配参数时，同步保存样例图、模板文件和期望 AOI 结果。
- 修改结果字段时，同步更新 DAO、handler 表格列、overlay 和项目导出。
- Flow 与手动页都要验收，因为它们参数来源不同但服务事件相同。
- 如果客户项目依赖 AOI 结果，项目页要说明最终 OK/NG 是否只看 Matching 结果。
