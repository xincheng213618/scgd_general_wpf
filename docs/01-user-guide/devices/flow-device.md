---
knowledge_id: "operations.flow-device"
knowledge_type: "topic"
status: "current"
summary: "旧 Flow 与第三方算法资源的协议值、装配过滤和流程节点读取兼容；资源记录与远端服务状态分别核对。"
aliases: ["流程设备", "ServiceTypes.Flow", "流程设备为什么不显示", "远端流程服务", "ThirdPartyAlgorithms", "第三方算法设备", "旧流程第三方算法", "TPAlgorithmNode", "TPAlgorithm2Node"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Type/TypeService.cs", "Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs", "Engine/ColorVision.Engine/PropertyEditor/DeviceNameEditor.cs", "Engine/FlowEngineLib/Node/Algorithm/TPAlgorithmNode.cs", "Engine/FlowEngineLib/Node/Algorithm/TPAlgorithm2Node.cs", "Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowControl.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LegacyThirdPartyAlgorithmNodeTests.cs"]
related: ["engine.devices", "operations.device-configuration", "engine.mqtt", "flow.session", "flow.runtime"]
---

# 旧 Flow 与第三方算法资源兼容

历史数据库资源和流程仍可能使用 Flow 或第三方算法协议。本页维护这些数据在当前客户端中的装配与读取边界，供排查资源记录存在但设备列表没有显示、或旧流程节点仍可读取的情况。

## 协议与资源装配

- `ServiceTypes.Flow = 12`、`ThirdPartyAlgorithms = 13`、`ThirdPartyAlgorithms32 = 14`，RC 服务枚举及结果类型 `ThirdPartyAlgorithms_File = 40` / `ThirdPartyAlgorithms_RealParam = 41` 用于解释已有协议与数据；不得重排或复用这些值。
- `ServiceManager` 过滤这些默认类型分支。`DeviceServiceFactoryRegistry` 没有对应的内置工厂，遗留资源即使位于其它可见终端下，也会因工厂返回 null 而跳过创建；资源记录仍留在数据库。扩展可通过公开工厂接口显式注册自己的实现。
- RC 的 `ServiceTokens` 独立收集远端服务信息，不依赖客户端设备实例。客户端未显示资源不能证明远端服务不存在或已经停止；服务端状态需另行核对。

## 旧流程读取

`TPAlgorithmNode` 和 `TPAlgorithm2Node` 保留类型身份，用于读取旧流程；`Obsolete` 使它们不出现在新建节点目录。`DeviceNameEditor` 对 `TPALGORITHMS` 提供空候选列表，允许手工编辑设备编码，不回退列出其它设备类型。保留协议和节点不等于确认某条现场旧流程能够成功运行，仍需核对远端服务、设备编码、算子及参数模板。

本地执行与最终化边界见 [Flow 执行会话](../workflow/execution.md)，设备装配规则见[设备服务链](../../04-api-reference/engine-components/device-service-chain.md)。

## 验证范围

`LegacyThirdPartyAlgorithmNodeTests` 使用内存画布验证两个旧节点的类型、设备编码、算子和模板名称在保存后仍可读取，不连接设备。自动化验证不替代现场历史资源、旧流程和外部插件清点；真实流程执行或服务重启仍须在明确授权的隔离环境验证。
