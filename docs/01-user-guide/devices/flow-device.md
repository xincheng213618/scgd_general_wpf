---
knowledge_id: "operations.flow-device"
knowledge_type: "topic"
status: "current"
summary: "Flow 远端设备包装有工厂但默认类型树过滤；它不执行 FlowEngineLib 本地图，也未提供专用运行/停止和完成回执。"
aliases: ["流程设备", "FlowDevice", "DeviceFlowDevice", "ConfigFlowDevice", "ServiceTypes.Flow", "流程设备为什么不显示", "远端流程服务"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/FlowDevice", "Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Devices/DeviceServiceConfig.cs", "Engine/ColorVision.Engine/Services/Devices/MQTTDeviceService.cs", "Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs", "Engine/ColorVision.Engine/Services/DeviceService.cs", "Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs"]
test_paths: []
related: ["engine.devices", "operations.device-configuration", "engine.mqtt", "flow.session"]
---

# FlowDevice 远端服务包装与本地图边界

`DeviceFlowDevice` 对应 `ServiceTypes.Flow` 的远端设备配置/MQTT 包装。它不是 `FlowEngineLib` 本地节点图运行器，也不是 `FlowJob` 或 `HeadlessFlowJob` 的实现。类中没有加载模板/STN、运行或停止流程、等待整图完成的专用方法；不能把“注册了 Flow 设备工厂”解释成“已具备可执行流程入口”。

## 当前注册和界面可见性

`DeviceServiceFactoryRegistry.RegisterDefaults` 为 `ServiceTypes.Flow = 12` 注册 `ConfigFlowDevice` 和 `DeviceFlowDevice`。与此同时，`ServiceManager.LoadServices` 构建默认 `TypeServices` 时明确过滤 Flow，再由剩余类型构建终端树。正常设备树路径不会展示它自己的 Flow 类型分支，不能指导用户直接在默认列表中新增或选中该类型。

工厂仍可被其它明确入口调用；是否真的实例化、显示或连接，必须追到那个入口。默认过滤不是错误配置的充分证据，不应为排障自动取消过滤或插入数据库资源。通用加载和显示条件见[设备服务链](../../04-api-reference/engine-components/device-service-chain.md)。

本类 `GetDeviceInfo()` 返回空 `UserControl`，没有独立运行控制面板；`DisplayConfig` 属性存在也不代表已注册业务显示页。构造函数创建 `MQTTDeviceService<ConfigFlowDevice>`，通过 `GetMQTTService()` 暴露给服务管理层。

## 配置与命令副作用

`ConfigFlowDevice` 没有增加专用字段，继承通用的名称、代码、发送/订阅主题、服务 token、Id 和 SN。它不包含本地模板键、STN 内容、开始节点或 Debug 执行参数；配置字段存在不证明远端服务已经上线。

本类配置编辑带管理员门禁，使用 `PropertyEditorEditMode.Transactional`；确认后 `Submitted` 调用继承的 `Save()`。**Save 会更新数据库并请求 RC 重启对应设备服务，不能作为只读诊断动作。** 取消只关闭工作副本窗口，不调用这里的 Submitted 保存路径；事务式属性编辑不代表远端设备操作可回滚。

通用导入会读取本地 `.config` JSON、复制配置再调用 Save；导出写本地配置文件，不是流程包导入/导出。通用“重启服务”命令同样走 Save。设备重启需要明确对象和授权，不能为检查“流程设备是否存在”而执行。具体持久化步骤沿[设备配置契约](./configuration.md)，不在本页复制一套保存流程。

`RestartServices` 的 void 入口不等待远端重启完成；RC 未连接或 token 不可用时内部可拒绝请求，调用者不会从返回值获知。数据库已更新或配置窗口已关闭，均不等于远端配置已应用。

## 与本地流程执行分开判断

| 问题 | 应核对的责任边界 |
| --- | --- |
| 远端 Flow 类型设备为何未出现在树中 | 本页的类型过滤、资源和实例创建路径 |
| 节点是否引用某个实际远端服务 | 该节点实现、设备/服务身份、MQTT 请求及返回协议；不能由本类名称推导引用关系 |
| 当前未保存画布、已载入模板或已保存 STN 哪个会被执行 | 本地 `FlowExecutionCoordinator`、工作区和执行入口，见[Flow 执行会话](../workflow/execution.md) |
| 引擎完成是否等于结果落库、导出或后处理完成 | 本地执行会话与最终化契约，不是 DService 的设备状态 |

本类未定义远端运行/停止命令、业务完成事件、超时恢复或幂等重试契约。要接入远端执行，必须先找到实际节点/客户端和远端协议实现；不能直接构造猜测的 MQTT 事件，更不能默认传入 Debug 参数就变成无设备副作用的测试。

## 通信与完成边界

DService 代理 Config 中的主题、代码和 token，接收行为来自通用 `MQTTServiceBase`。本类没有设置专用 `MsgReturnReceived` 处理器，也没有将返回消息映射成本地整图运行结果。

基类收到匹配订阅主题的消息后，按 `MsgID` 更新待处理记录，`Code == 0` 仅令该记录成为 Success；该路径不额外核验返回的 DeviceCode 和 EventName。消息记录、服务连接/状态、本地图引擎完成以及最终后处理完成是不同层次，不能相互替代。通用消息与生命周期边界见[MQTT](../../02-developer-guide/engine-development/mqtt.md)。

构造 DService 会订阅共享接收事件；它自身可通过 `MQTTServiceBase.Dispose()` 解绑并清理请求计时器。但 `DeviceFlowDevice` 没有重写 Dispose，设备基类当前不会代它释放 DService。若某个扩展实际使用该类，必须核对该入口的所有权和释放，不能仅凭 ServiceManager 调用设备 Dispose 就认定没有订阅残留。

## 验证缺口

本页未声明专用自动化测试，`test_paths` 为空。本地 `FlowRuntimeCompletionTests` 测试的是图引擎完成语义，不能作为这个远端设备包装、远端协议或配置重启成功的证据。

只读复核可检查类型过滤、实例入口、配置内容及具体节点的服务引用。真实 RC 重启、网络协议、设备执行、返回关联和资源释放仍须在明确授权的隔离环境验证；本页没有宣称这些已运行通过。
