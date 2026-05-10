# 算法系统概览

本页只描述当前仓库里实际在跑的模板与算法接入链，不再继续维护“算法分类百科 + 示例代码 + GPU 能力总论”式旧稿。

## 先看这套系统真正落在哪

当前和“算法”最直接相关的代码，并不只在一个目录里：

- `Engine/ColorVision.Engine/Templates/`：模板定义、模板管理、模板编辑和大部分业务算法 UI 接入点。
- `Engine/FlowEngineLib/`：流程节点、开始/结束链和执行控制。
- `Engine/ColorVision.Engine/Services/Devices/Algorithm/`：算法设备服务接入面。
- `Engine/cvColorVision/` 与更底层原生库：承接部分真正的底层计算与互操作。

因此如果只把这章理解成“托管算法函数目录”，会直接偏离当前实现。

## 当前主链是怎么串起来的

从现状看，算法/模板最常见的运行链大致是：

1. `TemplateContorl` 扫描已加载程序集中的 `IITemplateLoad` 实现，并把模板注册进系统。
2. `TemplateManagerWindow` 和 `TemplateEditorWindow` 负责让用户浏览、创建、编辑模板。
3. 具体业务算法的 UI 类通常继承 `DisplayAlgorithmBase`，并暴露 `OpenTemplateCommand` 一类入口。
4. 这些算法 UI 在 `SendCommand(...)` 中组装 `CVTemplateParam`、文件路径、设备信息等参数。
5. 参数再通过 `MQTTAlgorithm` 或相邻服务链发给真正执行端。
6. 如果是流程模板，则会进入 `TemplateFlow` + `FlowEngineToolWindow` + `FlowEngineLib` 这一条执行链。

这意味着：很多你在 `Templates/*/Algorithm*.cs` 里看到的类，当前职责更接近“算法前端适配器”，而不是最终算子本身。

## 当前模板系统里最重要的几块

### 模板注册与管理

这部分核心关注点在：

- `ITemplate.cs`
- `TemplateContorl.cs`
- `TemplateManagerWindow.xaml(.cs)`
- `TemplateEditorWindow.xaml(.cs)`

它们决定模板怎么出现、怎么打开、怎么进入编辑流程。

### Flow 模板

`Templates/Flow/` 不是普通参数模板的简单分支，而是把流程图、流程编辑窗口、导入导出和批次执行接到一起的特殊模板族。

当前关键入口包括：

- `TemplateFlow.cs`
- `FlowEngineToolWindow.xaml(.cs)`
- `DisplayFlow.xaml(.cs)`

### JSON 模板

`Templates/Jsons/` 当前承接了一批以 JSON 配置为核心的模板实现。它的共同链路主要是：

- `ITemplateJson<T>`：装载、保存、导入导出公共逻辑。
- `TemplateJsonParam`：JSON 模板参数基础类型。
- `EditTemplateJson.xaml(.cs)`：双模式编辑控件，支持文本编辑和属性编辑切换。

这也是为什么你会在模板系统里同时看到传统参数对象和 JSON 文本编辑器两种形态。

### 业务模板族

当前仍然能直接看出的主要模板族包括：

- `POI/`
- `ARVR/`
- `JND/`
- `LedCheck/`
- `Compliance/`
- `Jsons/` 下的多个业务模板实现

这些目录并不是同一时期按同一规则设计出来的，阅读时不要预设它们一定拥有完全一致的抽象层级。

## 当前几个最容易误读的点

### 误区 1：把 `Algorithm*.cs` 当成最终算法实现

很多这类类当前主要做的是：

- 打开模板编辑窗口
- 维护 UI 侧选择状态
- 组装消息参数
- 调用 `PublishAsyncClient(...)`

真正的底层处理经常在设备服务端、MQTT 对端、原生库或其他链路上完成。

### 误区 2：认为 `POI` 只是一个独立小专题

从当前代码看，POI 仍然是多个 ARVR/定位/分析类算法共享的上游模板依赖。它的模板与点位数据会被多个算法 UI 重复引用。

### 误区 3：把 Flow 模板排除在模板系统之外

Flow 模板只是表现形式更复杂，但它仍然通过 Templates 系统进入主程序，并由相邻窗口和流程库接管后续运行。

### 误区 4：以为 JSON 模板只是“临时兼容层”

当前 `Jsons/` 目录和 `ITemplateJson<T>` 仍然是实际在用的主路径之一，不应被写成已经被强类型模板完全取代。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
6. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
7. 具体业务算法目录，如 `POI/`、`ARVR/`、`Jsons/` 下各 `Algorithm*.cs`

## 继续阅读

- [算法与模板概览](./README.md)
- [Templates 模块分析](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib 架构](../../03-architecture/components/engine/flow-engine.md)
