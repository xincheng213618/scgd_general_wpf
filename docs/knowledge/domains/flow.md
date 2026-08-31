---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# 流程编排与执行

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

流程编辑、节点运行、参数传递与完成语义。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [Flow 节点检索入口](../../04-api-reference/flow_nodes_summary.md) — `flow.index`
  按节点用途与执行归属定位 FlowEngineLib、Engine 本地节点和属性编辑器。

- [Flow 架构与责任边界](../../03-architecture/components/engine/flow-engine.md) — `flow.architecture`
  区分 Flow 底层画布、节点内核、模板存储、编辑工作区、共享会话与隔离执行的所有权。

- [Flow 运行诊断、中断恢复与 Incident 处置](../../04-api-reference/engine-components/flow-diagnostics.md) — `flow.diagnostics`
  Flow本地诊断SQLite快照、节点尝试与Incident事件列表的读写边界；进程中断恢复只标记失败不续跑，心跳不是判死条件，终态持久化与业务结果分开。

- [Flow 隔离无界面执行](../../04-api-reference/algorithms/templates/flow-engine.md) — `flow.headless`
  隔离STN流程的加载、起始节点就绪、执行超时与诊断收尾；停止请求不证明设备停稳，默认执行不限时，批次与前后处理由调用方负责。

- [Flow 启动、停止与最终化](../../01-user-guide/workflow/execution.md) — `flow.session`
  FlowExecutionSession 的启动前提、停止请求与最终化判据，以及按失败阶段定位证据。

- [Flow 模板、持久化与流程包](../../04-api-reference/engine-components/template-flow-chain.md) — `flow.templates`
  Flow 模板的数据库保存、文档基线、cvflow v3 包兼容，以及版本/搜索侧车的失败边界。

- [Flow 编辑工作区与文档命令](../../01-user-guide/workflow/design.md) — `flow.workspace`
  ViewFlow 与 FlowEditorCanvas 的编辑命令、文档目标、工作区隔离及未保存画布的执行边界。

- [FlowEngineLib 节点扩展](../../04-api-reference/extensions/flow-node.md) — `flow.node-extension`
  说明服务节点基类、请求与响应扩展点、属性编辑和流程完成的边界。

- [Flow 转换与校准节点](../../04-api-reference/engine-components/flow-conversion-calibration-nodes.md) — `flow.conversion-calibration`
  定位 Flow 数据转换、图像转换、单双输入校准及属性选择器。

- [ST.Library.UI](../../04-api-reference/engine-components/ST.Library.UI.md) — `flow.editor`
  说明 ST WPF 节点画布、端口、类型目录及 STN 兼容边界。

- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md) — `flow.runtime`
  说明节点图加载、服务绑定、完成事件和隔离 RuntimeHost 的执行边界。
