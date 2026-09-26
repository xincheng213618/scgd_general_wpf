---
knowledge_id: "operations.flow-device"
knowledge_type: "topic"
status: "current"
summary: "FlowDevice 与 ThirdPartyAlgorithms 的 WPF 设备包装已移除；保留协议值、旧流程节点和服务端能力，记录用途及按需重建入口。"
aliases: ["流程设备", "FlowDevice", "DeviceFlowDevice", "ConfigFlowDevice", "ServiceTypes.Flow", "流程设备为什么不显示", "远端流程服务", "ThirdPartyAlgorithms", "DeviceThirdPartyAlgorithms", "第三方算法设备", "已移除设备模块", "重建设备模块"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Type/TypeService.cs", "Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs", "Engine/ColorVision.Engine/PropertyEditor/DeviceNameEditor.cs", "Engine/FlowEngineLib/Node/Algorithm/TPAlgorithmNode.cs", "Engine/FlowEngineLib/Node/Algorithm/TPAlgorithm2Node.cs", "Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowControl.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LegacyThirdPartyAlgorithmNodeTests.cs"]
related: ["engine.devices", "operations.device-configuration", "engine.mqtt", "flow.session", "flow.runtime"]
---

# 已移除的 Flow 与第三方算法设备包装

WPF 客户端已移除 `Services/Devices/FlowDevice/` 和 `Services/Devices/ThirdPartyAlgorithms/`，不再提供对应的内置设备工厂、配置面板和第三方算法模板管理界面。本页保留旧名称的检索入口，说明移除边界，并供以后出现明确需求时重建使用。

## 原用途与移除原因

| 旧模块 | 原用途与限制 |
| --- | --- |
| `FlowDevice` | `ServiceTypes.Flow` 的配置和通用 MQTT 包装；没有专用的流程加载、运行、停止或完成处理，设备信息页为空。它不承担本地 Flow 图执行。 |
| `ThirdPartyAlgorithms` | 第三方算法设备配置、DLL 绑定、算子参数模板和手动调用界面；旧上传方法只更新窗口状态，没有真正传输文件，专用消息回调没有业务结果处理。 |

两类设备此前已被默认类型树过滤。移除包装和工厂可减少未使用默认入口的维护负担；不能据此推断现场服务端、历史流程或外部插件没有使用相应协议。

## 保留的兼容边界

- `ServiceTypes.Flow = 12`、`ThirdPartyAlgorithms = 13`、`ThirdPartyAlgorithms32 = 14`，RC 服务枚举及结果类型 `ThirdPartyAlgorithms_File = 40` / `ThirdPartyAlgorithms_RealParam = 41` 继续保留。不得重排协议枚举或复用这些值。
- `ServiceManager` 继续过滤这些默认类型分支，内置工厂也不再为它们创建对象。遗留资源即使位于其它可见终端下，也会因没有内置工厂而跳过；本次移除不删除或迁移数据库记录。扩展仍可通过公开工厂接口显式注册自己的实现。
- `TPAlgorithmNode` 和 `TPAlgorithm2Node` 保留原类型身份，用于读取旧流程；`Obsolete` 仍使它们不出现在新建节点目录。旧节点的设备编码可手工编辑，但设备选择器不再提供已移除包装的候选，也不回退列出其它设备类型。
- 本地流程执行继续使用 `FlowProcessing/Runtime` 和 `FlowEngineLib`；RC 的 `ServiceTokens` 独立收集远端服务信息，不依赖这两个 WPF 设备包装。保留协议和节点不等于确认某条现场旧流程能够成功运行。
- `cvwindowsservice` 是独立服务端仓库。其 `FlowPlugin`、`ThirdPartyAlgorithmsPlugin`、TPA 64/32 位服务、结果查询 API 及数据表不属于本次移除范围。

本地执行与最终化边界见 [Flow 执行会话](../workflow/execution.md)，设备装配规则见[设备服务链](../../04-api-reference/engine-components/device-service-chain.md)。

## 按需重建入口

优先按新需求确定是否确实需要独立设备面板，再重建设备包装。原实现可通过 Git 查找；在仓库根目录执行以下只读命令，定位删除提交并查看其父提交中的文件：

```powershell
git log --all -- Engine/ColorVision.Engine/Services/Devices/FlowDevice Engine/ColorVision.Engine/Services/Devices/ThirdPartyAlgorithms
```

| 需求 | 应参考的入口 |
| --- | --- |
| 恢复客户端设备配置与面板 | 当前 `DeviceServiceFactoryRegistry`、`DeviceService<TConfig>`、元数据属性编辑器及管理员权限约定；历史目录仅供参考 |
| 远端运行、停止、组合流程及完成回执 | `cvwindowsservice` 的 `FlowPlugin/FlowDeviceMQTTAdapter.cs` 与 `FlowPlugin/FlowDeviceProxy.cs` |
| 第三方 DLL 绑定、算子发现和参数调用 | `cvwindowsservice` 的 `ThirdPartyAlgorithmsPlugin/TPAlgorithmDevice.cs`、`TPAlgorithmDeviceMQTTAdapter.cs` 与 `Features/FeatureTPAlgorithmFactory.cs` |
| 第三方算法结果查询及文件下载 | `cvwindowsservice` 的 `RegWindowsService/REST/API/IFlow.cs`、`NodeAPIService.cs` 与 `FlowResultService.cs` |

重建时应针对当前服务端协议实现真正的上传、响应关联、错误处理和订阅释放，避免原样恢复旧空实现。若恢复类型树或菜单入口，应同时定义支持范围、配置持久化与 RC 重启副作用，以及结果由哪个处理链负责。

## 验证范围

`LegacyThirdPartyAlgorithmNodeTests` 使用内存画布验证两个旧节点的类型、设备编码、算子和模板名称在保存后仍可读取，不连接设备。删除后的基本验证还包括主应用及相关客户项目编译、设备命令与菜单发现测试。自动化验证不替代现场历史资源、旧流程和外部插件清点；真实上传、流程执行或服务重启仍须在明确授权的隔离环境验证。
