---
knowledge_id: "algorithms.data-load"
knowledge_type: "topic"
status: "current"
summary: "数据加载与数据加载2的模板选择、参数初值和请求格式；区分要读取的数据来源与本次 Flow 执行设备、流水号及 ZIndex。"
aliases: ["DataLoad是文件导入吗","TemplateDataLoad","AlgDataLoadNode","AlgDataLoadNode2","数据加载2","加载设备Code","加载ZIndex","DataLoadInput"]
code_paths: ["Engine/ColorVision.Engine/Templates/DataLoad/TemplateDataLoad.cs","Engine/ColorVision.Engine/Templates/DataLoad/DataLoadParam.cs","Engine/FlowEngineLib/Node/Algorithm/AlgDataLoadNode.cs","Engine/FlowEngineLib/Node/Algorithm/AlgDataLoadNode2.cs","Engine/FlowEngineLib/Node/Algorithm/DataLoadData.cs","Engine/FlowEngineLib/Node/Algorithm/DataLoadData2.cs","Engine/FlowEngineLib/Node/Algorithm/DataLoadInput.cs","Engine/FlowEngineLib/Node/Algorithm/CVResultType.cs","Engine/FlowEngineLib/Base/CVBaseServerNode.cs","Engine/FlowEngineLib/Base/CVMQTTRequest.cs","Engine/FlowEngineLib/CVTemplateParam.cs","Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs","Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs","Engine/ColorVision.Engine/Templates/ModelBase.cs"]
test_paths: []
related: ["algorithms.index","flow.templates","flow.node-extension","engine.template-design","algorithms.template-management"]
---

# DataLoad 数据加载模板

Flow 的 **数据加载** 和 **数据加载2** 节点将数据来源条件交给算法服务：设备 Code、流水号、结果类型和 ZIndex。前者引用已保存模板，后者直接携带这些字段；节点本身不选择文件或解析文件内容。

## 选择节点并配置

| 节点 | 使用方式 | 适用场景 |
| --- | --- | --- |
| **数据加载**（`AlgDataLoadNode`） | 在节点属性的 **模板** 中选择 DataLoad 模板；旁边的编辑命令打开模板管理窗口 | 复用已保存的数据来源规则 |
| **数据加载2**（`AlgDataLoadNode2`） | 在节点属性中填写 **加载设备Code**、**流水号**、**结果类型**、**加载ZIndex** | 在当前流程直接配置要读取的数据 |

1. 确认算法服务支持 `DataLoad`，并准备可核对的数据来源、流水号、结果类型和索引。
2. 按上表选择节点和配置方式。模板路径需先保存模板参数；编辑器操作见[模板编辑与创建](./template-management.md)。
3. 在受控流程中核对实际请求，再确认下游节点消费的数据确实来自目标记录。请求已构造或模板可选择，都不代表数据已经找到。

两个节点的默认服务与执行设备为 `SVR.Algorithm.Default`、`DEV.Algorithm.Default`，事件码均为 `DataLoad`。这些是请求执行端，和下面的数据来源设备分别配置。

## 模板与显式参数

`TemplateDataLoad : ITemplate<DataLoadParam>` 使用字典 `22`、编码 `DataLoad` 和静态 `Params` 集合；通过普通模板编辑器呈现属性。加载和保存规则见[模板注册、参数与持久化](../../../03-architecture/components/templates/design.md)。

| 含义 | 模板字段及新空对象初值 | 数据加载2字段及新节点初值 |
| --- | --- | --- |
| 数据来源设备 | `DeviceCode = null` | `DataDeviceCode = ""` |
| 要读取的流水号 | `SerialNumber = null` | `SerialNumber = ""` |
| 结果类型 | `CVCommCore.CVResultType.None` | `FlowEngineLib.Node.Algorithm.CVResultType.Algorithm_POI`（枚举值 `0`） |
| 数据定位索引 | `ZIndex = 0` | `DataZIndex = -1` |

这些初值不是推荐配置。已保存模板通过 `ModelBase.GetValue` 读取明细，新建模板预览又使用系统字典默认值，可能与空对象不同。两条路径的结果枚举也属于不同类型，不能只按整数互相替换。

`AlgDataLoadNode2` 的 `_ResultType` 没有显式赋值，因此采用枚举值 `0`，不是 `None`（该枚举的 `None = -1`）。当前字段设置和请求构造不检查来源是否存在，也不解释空流水号或 `ZIndex = -1` 的服务端含义；这些值不会在本节点自动转换成“当前批次”或“最新结果”。

## 请求格式

以下是 `CVMQTTRequest` 的 **`params` 内容**，不是完整 MQTT 消息；设备和模板名称仅作示例。

### 数据加载：引用模板

`getBaseEventData()` 返回 `DataLoadData { TemplateParam = BuildTemp() }`：

```json
{
  "TemplateParam": {
    "ID": -1,
    "Name": "已保存的DataLoad模板"
  }
}
```

`BuildTemp()` 复制基类的模板 ID 和名称，不内联发送模板中的四项来源参数。基类 ID 初值为 `-1`；该节点的 `TempName` 设置器只更新名称，不查询数据库 ID。服务端需要能解析实际收到的模板引用。

### 数据加载2：携带来源条件

`getBaseEventData()` 将节点字段传入 `DataLoadInput`，再包装为 `DataLoadData2`：

```json
{
  "DataInput": {
    "DeviceCode": "source-device",
    "SerialNumber": "batch-001",
    "ResultType": "Camera_Img",
    "ZIndex": 0
  }
}
```

`DataLoadInput` 把结果枚举转成字符串，其余字段原样赋值。`Camera_Img` 是 Flow 枚举中的一个名称；具体服务是否支持该类型、目标记录是否存在，仍需按服务实现核对。

## 数据来源与本次执行信息

外层消息由 `CVBaseServerNode.getActionEvent()` 构造。两层字段各自赋值，不能因同名就当作同一个配置：

| 字段 | 请求外层 | `params.DataInput`（数据加载2） |
| --- | --- | --- |
| `DeviceCode` | 当前节点的 `GetDeviceCode()`，用于执行请求 | 节点的 `DataDeviceCode`，用于定位数据来源 |
| `SerialNumber` | 输入 `CVStartCFC.SerialNumber`，属于本次 Flow 执行 | 节点显式配置的 `SerialNumber`，属于要读取的数据 |
| `ZIndex` | 当前节点的 `base.ZIndex` | 节点显式配置的 `DataZIndex` |

数据加载2的参数构造没有读取 `start.SerialNumber` 来补齐内部流水号，也没有把外层 `ZIndex` 覆盖到 `DataInput.ZIndex`。模板路径则由模板引用提供来源规则；下游如何解释这些规则属于服务端契约。

## 源码入口

| 路径 | 责任 |
| --- | --- |
| `Engine/ColorVision.Engine/Templates/DataLoad/` | 模板注册、字典和参数属性 |
| `Engine/FlowEngineLib/Node/Algorithm/AlgDataLoadNode*.cs` | 两种节点的名称、属性、初值和请求构造 |
| 同目录 `DataLoadData.cs`、`DataLoadData2.cs`、`DataLoadInput.cs`、`CVResultType.cs` | 内层数据结构与 Flow 结果枚举 |
| `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`、`CVMQTTRequest.cs` | 模板引用与请求外层字段 |
| `Engine/FlowEngineLib/PropertyEditor/FlowNodePropertyEditors.cs`、`Engine/ColorVision.Engine/PropertyEditor/FlowNodePropertyEditorRegistration.cs` | `FlowDataLoadTemplateEditor` 代理及模板选择/编辑窗口 |

## 排查与验证

| 现象 | 优先检查 |
| --- | --- |
| 模板列表为空 | `TemplateDataLoad.Params` 是否加载，字典 `22`、编码和模板记录是否存在 |
| 发出请求却找不到数据 | 实际模板引用或 `DataInput` 内容、服务端支持范围与来源记录 |
| 加载到错误批次或设备 | 区分外层执行字段与内层来源字段；核对显式流水号，不假定空值自动继承 |
| 结果类型与预期不同 | 数据加载2的初始结果类型为 `Algorithm_POI`；核对枚举名称及服务端解释 |
| 取错数据层级 | 核对 `DataInput.ZIndex` 或模板 `ZIndex`；不要求它与外层节点 `ZIndex` 相等 |

当前未登记两条 DataLoad 节点路径的专项自动化测试。验证应使用可控来源，分别记录实际请求、服务返回和下游消费结果；本地参数构造不证明服务完成读取。
