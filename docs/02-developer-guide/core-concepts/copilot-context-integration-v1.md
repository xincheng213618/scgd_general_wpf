# Copilot Context Integration v1

本文档记录 ColorVision Copilot 从“独立聊天面板”演进为“软件上下文服务”的第一版设计边界、已落地项与后续规划。

## 目标

当前 Copilot 已经具备会话、模型配置、附件、工具调用、日志诊断与右侧 Dock 面板，但它对业务对象的理解仍主要停留在文件路径和用户手动挂载的上下文上。

下一阶段的目标不是继续单点增强聊天能力，而是把 Copilot 变成可被各模块调用的公共平台能力：

- 业务模块不直接依赖 ColorVision/Copilot 目录实现。
- Copilot 可以自动携带当前软件场景的结构化上下文。
- Copilot 的入口从状态栏扩展到解决方案树、图像、流程、算法结果和异常等工作流节点。

## 本次已落地的简单版本

这次先完成了低风险、可复用的基础层：

1. 公共契约层

- 在 UI/ColorVision.Common/Interfaces/Copilot 下新增 `ICopilotService`、`ICopilotContextProvider` 以及最小请求/上下文模型。
- `CopilotPanelService` 现在作为 `ICopilotService` 的实现，并通过 `CopilotServiceRegistry` 暴露给其它模块。

2. 最小上下文 Provider

- 新增 `CopilotContextRegistry` 和 `CopilotWorkspaceContextProvider`。
- 当前 Agent 请求会自动挂入“当前工作区”上下文，包括：解决方案根目录、当前活动内容、当前搜索根。
- 这一步不直接理解业务对象，但先把“软件当前工作面”变成了公共可扩展的上下文入口。

3. 解决方案树入口

- `FileNode` 增加“问 AI 解释此文件”和“问 AI 诊断此文件/日志”。
- `FolderNode` 增加“问 AI 总结此文件夹”。
- 这些入口通过公共 `ICopilotService` 触发，而不是直接引用 Copilot 具体实现。

4. 品牌统一

- 状态栏入口名称从 `GitHub Copilot` 调整为 `ColorVision Copilot`。

## 为什么先停在这里

这版刻意不直接进入 Engine、ImageEditor、Flow 或设备服务层，原因是：

- 这些模块一旦直接引用 ColorVision/Copilot，会很快形成反向耦合。
- 当前最缺的不是“更多聊天功能”，而是“可被业务模块稳定调用的上下文接口”。
- 先把公共契约、最小上下文采集和高频入口跑通，后面再接业务对象时成本更低。

## 后续规划

### Phase 1：上下文桥扩展

优先新增这些 Provider：

- 当前窗口/当前页面 Provider
- 当前选中对象 Provider
- 当前图像与 ROI Provider
- 当前日志与反馈包 Provider
- 当前流程/节点/最近失败 Provider

这一阶段的验收标准是：

- 用户从任意关键页面发起 Copilot 时，不需要手动重复说明当前对象。
- Agent 能看到结构化的应用上下文，而不是只有文件路径。

### Phase 2：场景入口

优先从这几类入口接入：

- 图像页右键：分析当前图像、解释当前 ROI
- 算法结果页：解释此结果、为什么失败
- Flow：解释当前流程、诊断上次失败
- 设备面板：分析当前设备状态
- 异常对话框：交给 Copilot 分析

这一阶段要坚持“调用公共接口”，不要让这些模块直接依赖 Copilot 具体窗口或 ViewModel。

### Phase 3：导航型动作

在解释/诊断稳定后，再引入低风险 UI 动作：

- 打开某个面板
- 聚焦某个窗口
- 定位某个配置项
- 打开日志目录

这一阶段只做导航和定位，不做改配置、控设备、跑流程这类高风险动作。

### Phase 4：业务工具

在现有 SearchFiles、GrepText、ReadLocalFile、GetRecentLog 基础上，补充只读业务工具：

- `GetActiveWorkspaceContextTool`
- `GetSelectedImageContextTool`
- `GetAlgorithmResultContextTool`
- `GetFlowRunSummaryTool`
- `GetDeviceStatusTool`
- `GetDiagnosticsPackageSummaryTool`

## 与 Agent/ReAct 路线的关系

Copilot Context Integration v1 解决的是“它是否理解当前软件场景”，而不是“它是否更像一个通用代码代理”。

因此后续路线应分成两条并行线：

- Context Integration：解决软件内上下文与入口。
- Agent/ReAct：解决工具规划、低成本检索、结构化执行与更多诊断工具。

两条路线都重要，但当前优先级应是 Context Integration。

## 当前边界

截至本版本：

- 已有公共 Copilot 契约层。
- 已有最小工作区上下文 provider。
- 已有解决方案树高频入口。
- 仍未接入 ImageEditor、Flow、Algorithm、Device 的业务对象上下文。
- 仍未实现 UI 动作卡片、业务工具和全局设置页嵌入。

后续设计与排期建议以本文档为基线推进。