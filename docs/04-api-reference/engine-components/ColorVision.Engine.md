# ColorVision.Engine

`Engine/ColorVision.Engine/` 是主程序的引擎宿主层，连接模板、设备服务、MQTT、Flow、结果展示和部分编辑 UI。它不是纯算法库，也不是所有算法都在本地执行的单体模块。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 模板列表为空 | MySQL 连接、`TemplateInitializer`、`TemplateControl.GetInstance()`、程序集是否加载 |
| 新模板类看不到 | 是否实现 `IITemplateLoad`，是否调用 `TemplateControl.AddITemplateInstance` |
| JSON 模板保存失败 | `ITemplateJson<T>` 数据库读写、模板名重复、schema 是否发布 |
| 设备资源没有变成服务对象 | `SysResourceModel.Type`、`ServiceTypes`、`DeviceServiceFactoryRegistry` |
| 设备命令一直 Timeout | MQTT 主题、service token、`MqttRCService` 状态、`MsgRecord` |
| Flow 节点没有服务 | `FlowEngineManager`、RC 服务列表、`DisplayFlow` 刷新逻辑 |
| Flow 跑完项目拿不到结果 | `FlowCompleted`、`ViewResultAlg`、项目包 `Process/` 映射 |
| overlay 不对 | `IResultHandleBase`、结果类型、ImageEditor 图元和坐标转换 |
| 打包后图标/schema/工具缺失 | `ColorVision.Engine.csproj` 的 `Resource` / `None Update` |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 模板注册 | `TemplateControl` | MySQL 可用后扫描 `IITemplateLoad` 并注册到 `ITemplateNames` |
| JSON 模板 | `ITemplateJson<T>`、`EditTemplateJson` | 数据库读写、参数包装、文本/属性/注释视图编辑 |
| Flow 桥接 | `FlowEngineManager`、`DisplayFlow` | 将 Flow 模板、服务 token、节点刷新和 UI 操作接到 `FlowEngineLib` |
| 设备服务 | `DeviceService`、`DeviceServiceFactoryRegistry` | 树节点、菜单、配置导入导出、服务对象工厂 |
| MQTT 运行时 | `MQTTServiceBase`、`MqttRCService` | 发布/订阅、消息记录、心跳、RC 注册和 token 缓存 |
| 算法适配 | `AlgorithmPOI`、`AlgorithmMTF` 等 | 打开模板编辑器、组装 MQTT 参数、调用设备服务 |
| 结果展示 | `IViewResult`、`IResultHandleBase` | 连接历史结果、表格明细和 ImageEditor overlay |

## 运行链路

1. `TemplateInitializer` 等初始化器等待数据库可用。
2. `TemplateControl` 扫描程序集中的模板加载器并注册模板实例。
3. 设备资源按 `ServiceTypes` 进入 `DeviceServiceFactoryRegistry` 创建服务对象。
4. 设备/算法服务通过 `MQTTServiceBase` 发送命令，并用 `MsgRecord` 追踪状态。
5. `MqttRCService` 维护注册中心连接和 service token。
6. Flow 模板通过 `FlowEngineManager` / `DisplayFlow` 接入 `FlowEngineLib` 和服务节点。
7. 结果由 handler 命中类型后交给 ImageEditor 或表格展示。

## 检查

| 验收项 | 要查什么 |
| --- | --- |
| 构建目标 | 主宿主 `net10.0-windows`、x64，Engine 依赖可解析 |
| 模板初始化 | MySQL 连接后能扫描 `IITemplateLoad`，`ITemplateNames` 有真实模板项 |
| JSON 模板 | 能读取、编辑、保存、删除，schema 文件随输出发布 |
| 设备工厂 | Camera、PG、Spectrum、SMU、Sensor、Algorithm、Calibration 等类型能实例化 |
| MQTT 命令链 | 发命令后 `MsgRecord` 有 Success/Fail/Timeout 状态 |
| RC 服务链 | 注册中心连接后能刷新可用服务和 token |
| Flow 桥接 | 流程模板能加载、编辑、保存、运行，并刷新服务节点 |
| 结果展示 | 历史结果能打开，overlay、表格和结果类型匹配 |
| 交付资源 | 图标、schema、工具 exe 等发布输出完整 |

## 边界

- 很多算法类只是模板/参数/MQTT 的适配器，不是本地算法内核。
- 模板系统依赖数据库和程序集扫描，不是完全本地静态模板集。
- Flow 执行内核在 `FlowEngineLib`，主程序编辑、选择和运行还需要 `Templates/Flow/` 桥接层。
- 设备实例化当前以 `DeviceServiceFactoryRegistry` 为中心，不建议继续写分散构造说明。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 模板注册 | `Templates/TemplateContorl.cs`、`Templates/Jsons/ITemplateJson.cs` |
| JSON 编辑 | `Templates/Jsons/EditTemplateJson.xaml.cs` |
| 设备服务 | `Services/DeviceService.cs`、`Services/Devices/DeviceServiceFactory.cs` |
| MQTT/RC | `Services/Core/MQTTServiceBase.cs`、`Services/RC/MQTTRCService.cs` |
| Flow 桥接 | `Templates/Flow/FlowEngineManager.cs`、`Templates/Flow/DisplayFlow.xaml.cs` |
| 算法适配 | `Templates/POI/AlgorithmImp/AlgorithmPOI.cs`、`Templates/ARVR/MTF/AlgorithmMTF.cs` |
