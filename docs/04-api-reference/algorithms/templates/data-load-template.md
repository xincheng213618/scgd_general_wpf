# DataLoad 数据加载模板

本页说明 `Engine/ColorVision.Engine/Templates/DataLoad/` 与 Flow 数据加载节点的关系。`DataLoad` 不是图像算法，也没有独立结果 handler；它负责把“从哪个设备、哪个批次、哪类结果、哪个 ZIndex 取数据”这些信息整理成模板或 Flow 请求参数。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 模板类 | `TemplateDataLoad : ITemplate<DataLoadParam>, IITemplateLoad` |
| 参数类 | `DataLoadParam : ParamModBase` |
| 模板代码 | `DataLoad` |
| 字典 ID | `TemplateDicId = 22` |
| Flow 节点 | `AlgDataLoadNode`、`AlgDataLoadNode2` |
| Flow 事件码 | `operatorCode = "DataLoad"` |
| 配置器 | `AlgDataLoadNodeConfigurator` |
| 结果 handler | 当前没有单独的 `ViewHandleDataLoad` |

## 源码入口

| 文件 | 用途 |
| --- | --- |
| `TemplateDataLoad.cs` | 注册 DataLoad 模板、模板代码和 `TemplateDicId`。 |
| `DataLoadParam.cs` | 定义设备、结果类型、流水号和 ZIndex 参数。 |
| `FlowEngineLib/Node/Algorithm/AlgDataLoadNode.cs` | 通过模板名构造 `DataLoadData { TemplateParam = BuildTemp() }`。 |
| `FlowEngineLib/Node/Algorithm/AlgDataLoadNode2.cs` | 直接构造 `DataLoadData2(new DataLoadInput(...))`。 |
| `FlowEngineLib/Node/Algorithm/DataLoadData*.cs` | 定义发给服务端的数据结构。 |
| `Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs` | 在流程编辑器里给 `AlgDataLoadNode` 绑定设备和 DataLoad 模板选择面板。 |

## 参数模型

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `DeviceCode` | `string?` | 数据来源设备 Code。 |
| `ResultType` | `CVCommCore.CVResultType` | 要加载的结果类型。 |
| `SerialNumber` | `string?` | 批次/流水号。 |
| `ZIndex` | `int` | Flow 或服务侧用于定位数据层级的索引。 |

这些字段在 `DataLoadParam` 里以 PropertyGrid 属性呈现；在 `AlgDataLoadNode2` 里则以节点属性 `DataDeviceCode`、`SerialNumber`、`ResultType`、`DataZIndex` 直接出现。

## 两条 Flow 路径

### `AlgDataLoadNode`

`AlgDataLoadNode` 更偏模板驱动：

1. 节点配置器显示设备选择和 DataLoad 模板选择。
2. 用户选择 `TempName`。
3. 节点执行时调用 `BuildTemp()`。
4. 请求体是 `DataLoadData`，只包含 `TemplateParam`。

这条路径适合把加载规则沉淀成模板，现场只选择模板名。

### `AlgDataLoadNode2`

`AlgDataLoadNode2` 更偏显式参数驱动：

1. 节点上直接维护设备 Code、流水号、结果类型和 ZIndex。
2. 执行时构造 `DataLoadInput`。
3. `ResultType` 会被转成字符串。
4. 请求体是 `DataLoadData2 { DataInput = ... }`。

这条路径适合 Flow 中临时读取指定批次或指定结果。

## 和模板系统的关系

`TemplateDataLoad` 继承普通 `ITemplate<T>`，因此模板明细仍然来自系统字典和模板表。它没有自定义编辑控件，主要依赖通用模板编辑器和 PropertyGrid 展示 `DataLoadParam`。

维护时不要把 DataLoad 写成“文件导入器”。当前代码没有直接选择文件或解析文件格式，它只是把数据定位参数交给算法服务或 Flow 链路，由下游服务按设备、流水号、结果类型和 ZIndex 读取数据。

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| Flow 节点找不到模板 | `TemplateDataLoad.Params` 是否加载，`TemplateDicId = 22` 对应字典是否存在。 |
| 加载到错误批次 | `SerialNumber` 是来自模板、节点显式属性，还是上游 Flow 传递。 |
| 加载到错误设备 | `DeviceCode/DataDeviceCode` 是否指向预期算法服务设备。 |
| 加载结果类型不对 | `CVResultType` 转字符串后是否是服务端期望值。 |
| 多层结果取错 | `ZIndex/DataZIndex` 是否和 Flow 节点层级一致。 |

## 检查清单

- 写清使用的是 `AlgDataLoadNode` 模板路径，还是 `AlgDataLoadNode2` 显式参数路径。
- 记录设备 Code、结果类型、流水号来源和 ZIndex 语义。
- 修改服务端 DataLoad 协议时，同步检查 `DataLoadData`、`DataLoadData2` 和 Flow 节点文档。
- 如果要加文件导入能力，应新增明确的文件参数和失败提示，不要混在当前 DataLoad 模板里。
- 验收时用真实批次确认加载结果和下游节点消费的是同一条数据。
