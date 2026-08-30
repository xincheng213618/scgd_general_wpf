# FlowEngineLib

ColorVision 的节点图执行内核：加载画布、管理开始节点与服务绑定、分发节点数据及引擎完成事件。可视化画布由 `ST.Library.UI` 提供；模板数据库、编辑工作区、宿主前后处理和客户结果不由本库拥有。

## 使用前提与边界

- 当前目标为 `net10.0-windows;net8.0-windows`，仓库使用 x64；项目引用 `ST.Library.UI`、`ColorVision.UI`，准确框架及依赖以 `FlowEngineLib.csproj` 为准。无界面执行不代表跨平台或无桌面框架依赖。
- 使用真实的画布加载和 `StartNode` / `TryStartNode` / `StopNode` 入口；启动受开始节点、连线及就绪状态约束，不存在通用 `RunFlow`、`PauseFlow` 或 `ResumeFlow` API。
- 本地节点不必要求服务连接；需要外部服务的节点则依赖匹配的服务信息与请求/响应链。加载图或发起请求不证明设备动作已完成，也不授权操作设备。
- `Finished` 是引擎完成事件，不是客户判定、导出或宿主最终化的完成回执。构建本库或生成本地发布输出也不等于完成产品打包、上传或硬件验收。

## 源码知识入口

- [节点图执行内核](../../docs/04-api-reference/engine-components/FlowEngineLib.md)：加载、服务绑定、启动拒绝与完成边界。
- [节点扩展](../../docs/04-api-reference/extensions/flow-node.md)：当前基类和请求/响应扩展点。
- [Flow 架构与责任边界](../../docs/03-architecture/components/engine/flow-engine.md)：编辑器、模板持久化、共享会话和隔离运行的归属。
- [构建与测试前提](../../docs/00-getting-started/prerequisites.md)：Windows、托管与 native 依赖的区别。

这些相对链接用于匹配版本的源码仓库；复制程序集不会同时提供 `docs/`。不要按旧示例补造 API，或把另一版本的说明当作当前程序集的契约。
