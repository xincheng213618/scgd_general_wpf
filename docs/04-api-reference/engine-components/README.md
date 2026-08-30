---
knowledge_id: "engine.index"
knowledge_type: "index"
status: "current"
summary: "按实际代码职责路由 Engine 的设备、消息、模板、Flow、结果与工程依赖；契约和验证由各主题维护。"
aliases: ["Engine代码在哪里","Engine问题该从哪里查","看到Engine类名应该查哪里","修改Engine要先看什么"]
code_paths: ["Engine/ColorVision.Engine/ColorVision.Engine.csproj","Engine/ColorVision.Engine/Services/ServiceManager.cs","Engine/ColorVision.Engine/Templates/TemplateControl.cs","Engine/FlowEngineLib/FlowEngineControl.cs"]
test_paths: []
related: ["engine.host", "engine.devices", "engine.mqtt", "engine.rc-registration", "engine.template-design", "flow.templates", "flow.session", "engine.results"]
---

# Engine 知识入口

`Engine/` 承接设备、模板、Flow、服务消息与结果。按问题直接选择权威主题，再核对其中的源码和验证入口；新增功能和排查现有行为使用同一份契约，不再分别维护开发手册、对象总览和业务矩阵。

## 按问题检索

| 问题 | 主题 | 起点 |
| --- | --- | --- |
| 设备没有出现、服务类型或 MQTT 控制器不匹配 | [设备服务链](./device-service-chain.md) | `DeviceServiceFactoryRegistry`、`ServiceManager` |
| MQTT topic、请求、回复或超时关联不正确 | [MQTT 消息链](../../02-developer-guide/engine-development/mqtt.md) | `MQTTControl`、`MQTTServiceBase`、`MsgRecord` |
| RC 连接测试、令牌、早到服务列表与状态不一致 | [RC 注册与服务快照](./rc-registration.md) | `MqttRCService`、`PendingServiceUpdateBuffer`、`RCServiceConnect` |
| 新增模板、模板未加载或参数保存异常 | [模板核心契约](../../03-architecture/components/templates/design.md)、[编辑与创建宿主](../algorithms/templates/template-management.md) | `TemplateControl`、`ITemplate<T>`；JSON 分支见 [JSON 模板](../algorithms/templates/json-templates.md) |
| Flow 保存、导入或节点关联参数不正确 | [Flow 模板持久化](./template-flow-chain.md) | `TemplateFlow`、`FlowParam`、`FlowPackageHelper` |
| 流程未启动、停止后仍处理结果或结束信号不一致 | [Flow 会话与最终化](../../01-user-guide/workflow/execution.md) | `FlowExecutionSession`、`FlowRunFinalizer`；对象责任见 [Flow 架构](../../03-architecture/components/engine/flow-engine.md) |
| 新节点放哪里、哪些配置用 PropertyGrid | [Flow 节点入口](../flow_nodes_summary.md)、[节点扩展](../extensions/flow-node.md)、[PropertyGrid 契约](../ui-components/property-grid.md) | `STNode`、`FlowNodePropertyEditorAttribute`、`FlowPropertyEditorRegistry` |
| 结果没有 handler、图像缺失、overlay 残留 | [结果交接链](./result-handoff-chain.md) | `ResultHandleRegistry`、`AlgorithmOverlayManager` |
| 客户判定或 CSV/MES/Socket 字段不正确 | [项目知识入口](../projects/README.md)、[Socket 协议](../ui-components/ColorVision.SocketProtocol.md) | `Projects/` 的 `Process/Recipe/Fix` 与具体协议消费方 |
| 数据库清理是否有预览、备份和回滚保证 | [维护窗口](./database-maintenance.md)、[MySQL 结果维护](./mysql-maintenance.md) | `DatabaseCleanupWindow`、`MySqlResultCleanupProvider`；与表浏览器、SQLite 工具分开 |
| SQL恢复失败但数据已变、重置为何没有保留结果 | [MySQL恢复、重置与资源保留](./mysql-recovery.md) | `MySqlDatabaseMaintenanceService`、`RestoreAndRestartAsync`；配置同步和服务重启有独立失败边界 |
| 转换、图像转换、校准参数从哪来 | [转换与校准节点](./flow-conversion-calibration-nodes.md) | `Engine/FlowEngineLib/` |
| FileServer 类型可见性或文件格式读写问题 | [FileServer 包装边界](../../01-user-guide/devices/file-server.md)、[CV 文件读写](./ColorVision.FileIO.md) | `DeviceFileServer` 不等同于本地格式读写库；不要根据包装类推断远程文件操作已实现 |
| 工程依赖、资源缺失或 native 接入 | [Engine 工程契约](./ColorVision.Engine.md)、[OpenCV/native 集成](../../02-developer-guide/engine-development/opencv-integration.md) | `.csproj` 的引用/资源声明、`OpenCVMediaHelper`、native ABI |

## 按模块定位

| 模块 | 源码目录 | 职责 | 文档 |
| --- | --- | --- | --- |
| ColorVision.Engine | `Engine/ColorVision.Engine/` | 设备、模板、流程宿主、MQTT、结果 | [工程与依赖](./ColorVision.Engine.md) |
| FlowEngineLib | `Engine/FlowEngineLib/` | 公共节点、执行控制、Flow 参数 | [FlowEngineLib](./FlowEngineLib.md) |
| cvColorVision | `Engine/cvColorVision/` | native 接口封装 | [cvColorVision](./cvColorVision.md) |
| ColorVision.FileIO | `Engine/ColorVision.FileIO/` | CVRAW/CVCIE 文件读写 | [FileIO](./ColorVision.FileIO.md) |
| ST.Library.UI | `Engine/ST.Library.UI/` | 节点画布和基础控件 | [节点编辑器](./ST.Library.UI.md) |
| ColorVision.ShellExtension | `Engine/ColorVision.ShellExtension/` | Windows Shell 缩略图扩展 | [ShellExtension](./ColorVision.ShellExtension.md) |

## 不要合并的边界

Engine 的历史 `IViewResult`/handler、统一算法的中立 Result/overlay、客户项目的判定与导出是不同契约。前两者的汇合位置见结果链；客户规则留在 `Projects/`，不要因为最终都显示在 ImageEditor 就合并模型或注册机制。

一项改动跨越多个模块时，从具体源码路径查询关联主题，再检查调用者；源码关联只提示复核范围，不是自动证明的调用图。从仓库根目录执行下面的只读命令，将示例路径换成实际改动位置，无需启动产品或构建网页：

```powershell
node docs/.vitepress/scripts/knowledge.mjs impact "Engine/ColorVision.Engine/FlowProcessing/Runtime/FlowExecutionSession.cs"
```

## 待实施设计

本地相机内存预览仍是 `planned`，不表示当前设备已支持该发布器或生命周期：[方案](../../02-developer-guide/engine-development/local-camera-memory-preview.md)、[显示与生命周期](../../02-developer-guide/engine-development/local-camera-memory-preview-runtime.md)、[实施与验证计划](../../02-developer-guide/engine-development/local-camera-memory-preview-validation.md)。

## 验证入口与缺口

索引页只确定实现归属；到目标主题读取测试入口，按[测试范围](../../02-developer-guide/testing.md)选择最小验证。设备动作、数据库写入和外部发布另行授权，编译通过不能代替业务验收。仅维护本文档与索引，不需要启动应用、broker 或设备。
