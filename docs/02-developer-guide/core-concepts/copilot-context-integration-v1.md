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

## v2 增量：业务上下文入口

在公共契约稳定后，当前版本已经把 Copilot 从“聊天 + 只读 Agent MVP”推进到“业务上下文 Copilot v2”的第一步：

1. 公共请求辅助层

- 在 `UI/ColorVision.Common/Interfaces/Copilot` 下新增 `CopilotPromptRequestHelper`，统一处理请求创建、`CopilotServiceRegistry` 查询、`IsAvailable` 检查、`StartNewConversation`、`SendNow`、`AttachContextSnapshot` 和失败状态消息。
- 解决方案树文件/文件夹入口、模板 JSON 编辑器入口已复用这个 helper，避免各业务模块重复拼 service 调用逻辑。

2. 业务上下文 payload builder

- 新增 `CopilotBusinessContextBuilder`，专门构造 ImageEditor、Flow、Device 三类结构化文本快照。
- builder 位于公共契约层，只输出 `CopilotContextItem`，不依赖 `ColorVision/Copilot` 面板或 ViewModel 实现。
- Device 快照会对 `Token`、`Secret`、`Password`、`SN`、`License` 等敏感字段做脱敏。

3. ImageEditor 入口

- `ZoomEditorToolContextMenu` 增加“问 AI 分析当前图像”。
- 上下文包含图像路径、尺寸、像素格式、通道/深度/DPI、ImageView 元数据、当前选区/ROI、绘图标注/结果摘要。
- 当前版本明确不发送图像像素，只发送结构化摘要。

4. Flow/流程入口

- `FlowEngineManager.ContextMenu` 增加“问 AI 分析当前流程”。
- 上下文包含流程名、模板 ID、运行状态、批次状态/结果/进度、最近运行文本、最近节点、节点输入/输出和带 `STNodeProperty` 的节点参数摘要。
- 入口只读快照，不会调用 `RunFlow`、`StopFlow` 或修改流程。

5. Device/服务入口

- `DeviceService<T>` 的设备菜单增加“问 AI 分析设备状态”。
- 上下文包含服务名/代码/类型、MQTT 心跳状态、最近心跳、Topic、运行属性、配置摘要，以及可读取时的最近服务日志片段。
- 日志不可用或服务目录不可用时只返回降级说明，不阻断 Copilot 请求。

6. 离线测试

- `Test/ColorVision.UI.Tests` 增加纯逻辑测试，覆盖 `CopilotAgentContextBuilder`、本地文件工具在临时目录下的搜索/读取/列目录行为，以及业务上下文 builder 的 ROI、节点参数和敏感字段脱敏。

## v1 为什么先停在这里

v1 刻意不直接进入 Engine、ImageEditor、Flow 或设备服务层，原因是：

- 这些模块一旦直接引用 ColorVision/Copilot，会很快形成反向耦合。
- 当前最缺的不是“更多聊天功能”，而是“可被业务模块稳定调用的上下文接口”。
- 先把公共契约、最小上下文采集和高频入口跑通，后面再接业务对象时成本更低。

## 后续规划

### Phase 1：上下文桥扩展

优先新增这些 Provider：

- 当前窗口/当前页面 Provider
- 当前选中对象 Provider
- 当前图像与 ROI Provider（已先通过 ImageEditor 右键入口落地快照）
- 当前日志与反馈包 Provider
- 当前流程/节点/最近失败 Provider（已先通过 Flow 菜单入口落地快照）

这一阶段的验收标准是：

- 用户从任意关键页面发起 Copilot 时，不需要手动重复说明当前对象。
- Agent 能看到结构化的应用上下文，而不是只有文件路径。

### Phase 2：场景入口

优先从这几类入口接入：

- 图像页右键：分析当前图像、解释当前 ROI（已接入分析当前图像）
- 算法结果页：解释此结果、为什么失败
- Flow：解释当前流程、诊断上次失败（已接入当前流程诊断）
- 设备面板：分析当前设备状态（已接入设备状态诊断）
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
- 已有公共 Copilot 请求辅助层和 ImageEditor、Flow、Device 三类业务上下文 builder。
- 已接入 ImageEditor、Flow、Device 的低风险只读入口，并将新增业务上下文输出统一为英文结构化文本。
- 已接入 active template JSON editor、active flow/selected node、recent flow failure summary、active image/editor metadata（不包含像素）、selected solution file/folder 等上下文来源。
- 已通过 ColorVision MCP 暴露只读上下文资源：live context、workspace、recent logs、current template、current flow、MCP audit log。
- Algorithm 结果页业务对象上下文仍未接入。
- 仍未实现 UI 动作卡片、业务工具和全局设置页嵌入。
- 当前业务入口和 MCP 资源只负责快照、提问、诊断和低风险导航，不负责执行流程、修改配置、删除文件、运行 shell 或控制设备。

## MCP 外部 operator 边界

ColorVision MCP 现在可以让 Codex 作为外部 operator 检查运行中的 WPF 应用，但它的职责边界仍然保守：

- 先调用 `get_server_status` 判断 MCP 是否启用、认证是否通过、当前端点和安全边界。
- 通过 `colorvision://live-context/current`、`colorvision://workspace/current`、`colorvision://logs/recent`、`colorvision://template/current`、`colorvision://flow/current` 读取只读上下文。
- 用 `search_docs` 回答产品行为问题，用 `search_files`、`grep_text`、`read_allowed_file` 回答代码问题。
- 只允许 `open_panel`、`execute_menu`、主题和语言这类低风险 UI 导航或展示操作。
- 不支持设备控制、流程执行、配置修改、文件删除、任意 shell、任意文件读取或图像像素上传。

后续设计与排期建议以本文档为基线推进。