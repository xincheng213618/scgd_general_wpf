---
knowledge_id: "copilot.tool-contracts"
knowledge_type: "topic"
status: "current"
summary: "Copilot 工具结果、事件、审批恢复和 Flow 编辑必须遵守的执行契约。"
aliases: ["工具返回成功是否代表任务完成","审批和恢复怎样绑定调用","临时授权能自动运行Shell吗","自动审查拒绝后如何重试","模板补丁会自动保存吗","CopilotToolExecutionContracts","CopilotToolRegistry","CopilotAgentAccessContext","CopilotAgentTaskEventJournalRegistry","CopilotAutomaticApprovalReviewer","approvals_reviewer","/approve","TemplatePatch","ApplyTemplatePatch","preview_id","current_json","InspectFlowGraph","SearchFlowNodeCatalog","PreviewFlowPatch","ApplyFlowPatch","colorvision-flow-authoring","ValidateFlowGraph","validate_flow_graph","当前流程静态校验","断开连线","disconnect","改线"]
code_paths: ["ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.ApplicationPanels.cs","ColorVision/Copilot/Agent/ICopilotTool.cs","Engine/ColorVision.Engine/FlowProcessing/Integration/FlowCopilotService.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/CameraNodeCreationDefaults.cs","UI/ColorVision.Common/Interfaces/Copilot/CopilotBusinessContextBuilder.cs","ColorVision/Copilot/Agent/Tools/Files/CopilotWorkspacePatchEnvelopeTools.cs","ColorVision/Copilot/Agent/CopilotToolExecution.Support.cs","ColorVision/Copilot/State/CopilotChatState.cs","ColorVision/Copilot/CopilotChatPanel.xaml","ColorVision/Copilot/CopilotChatMessage.WorkspaceDiff.cs","ColorVision/Copilot/Runtime/CopilotTurnEventReducer.cs","ColorVision/Copilot/Runtime/CopilotTurnWorkspaceDiffAccumulator.cs","ColorVision/Copilot/Capabilities/CopilotWorkspaceChangeSetCapability.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.SteeringState.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.Attempts.cs","ColorVision/Copilot/Agent/CopilotToolExecutionContracts.cs","ColorVision/Copilot/Capabilities/CopilotCapabilityResult.cs","ColorVision/Copilot/Agent/CopilotToolExecution.cs","ColorVision/Copilot/Agent/CopilotAgentTaskEventJournal.cs","ColorVision/Copilot/Agent/CopilotAgentTaskEventJournalRegistry.cs","ColorVision/Copilot/Agent/CopilotUserQuestion.cs","ColorVision/Copilot/Agent/CopilotAgentAccessModels.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.ApprovalRouting.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ApprovalExecution.cs","ColorVision/Copilot/Agent/CopilotFrameworkApproval.cs","ColorVision/Copilot/Runtime/CopilotAutomaticApprovalReviewer.cs","ColorVision/Copilot/Runtime/CopilotAutomaticApprovalOverrideStore.cs","ColorVision/Copilot/CopilotChatViewModel.LocalCommandWorkflows.cs","ColorVision/Copilot/CopilotChatViewModel.Permissions.cs","ColorVision/Copilot/Agent/CopilotSharedCapabilityCatalog.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotFlowGraphTools.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowPatch.cs","ColorVision/Copilot/Skills/colorvision-flow-authoring","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.TemplatePatch.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.TemplatePatchSupport.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotApplyTemplatePatchTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.Confirmation.cs","ColorVision/Copilot/Mcp/CopilotMcpToolModels.cs","Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowValidator.cs","UI/ColorVision.Common/Interfaces/Copilot/CopilotFlowValidationSnapshot.cs","ColorVision/Copilot/Capabilities/CopilotFlowGraphValidationService.cs","ColorVision/Copilot/Mcp/CopilotMcpToolEnvironment.FlowGraphValidation.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowGraphValidation.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotValidateFlowGraphTool.cs","ColorVision/Copilot/Capabilities/CopilotFlowGraphInspectionService.cs","ColorVision/Copilot/Capabilities/CopilotFlowGraphInspectionService.Cursor.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowRead.cs","ColorVision/Copilot/Capabilities/CopilotFlowGraphInspectionService.Format.cs","Engine/ST.Library.UI/NodeEditor/STNodeOption.cs","Engine/ST.Library.UI/NodeEditor/STNodeEditor.Edit.cs","ColorVision/Copilot/Capabilities/CopilotFlowParameterRedaction.cs","ColorVision/Copilot/Mcp/CopilotFlowPatchPreviewFormatter.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.ContextFormatting.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.DiagnosticsFormatting.cs","ColorVision/Copilot/Mcp/CopilotMcpAuditLogger.cs","UI/ColorVision.Common/Interfaces/Copilot/CopilotSensitiveTextRedactor.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotListFlowRunsTool.cs","ColorVision/Copilot/Skills/colorvision-flow-diagnostics","ColorVision/Copilot/Capabilities/CopilotApplicationCapabilityInvoker.cs","ColorVision/Copilot/Mcp/CopilotMcpToolResultContract.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.Invocation.cs","ColorVision/Copilot/Agent/CopilotToolResultContract.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectFlowRunAttemptsTool.cs","Engine/ColorVision.Engine/Services/CopilotDeviceServicesReader.cs","ColorVision/Copilot/Capabilities/CopilotDeviceServicesInspectionService.cs","ColorVision/Copilot/Capabilities/CopilotDeviceServicesInspectionService.Cursor.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectDeviceServicesTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolEnvironment.DeviceServices.cs","Engine/ColorVision.Engine/Services/CopilotDeviceContextFactory.cs","Engine/ColorVision.Engine/Services/DeviceService.cs","Engine/ColorVision.Engine/Dao/CopilotLoadedBatchResultsReader.cs","ColorVision/Copilot/Capabilities/CopilotLoadedBatchResultsInspectionService.cs","ColorVision/Copilot/Capabilities/CopilotLoadedBatchResultsInspectionService.Cursor.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectLoadedBatchResultsTool.cs","ColorVision/Copilot/Mcp/CopilotMcpToolEnvironment.LoadedBatchResults.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.LoadedBatchResults.cs","ColorVision/Copilot/Skills/colorvision-database-operations/SKILL.md"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotBatchImageCancellationEvidenceTests.cs","Test/ColorVision.Copilot.Tests/CopilotExecuteMenuApprovalTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowCameraDefaultsApprovalTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowCameraDefaultsMcpTests.cs","Test/ColorVision.Copilot.Tests/CopilotApprovalReviewTests.cs","Test/ColorVision.UI.Tests/FlowExecutionEditingTests.cs","Test/ColorVision.Copilot.Tests/CopilotTemplatePatchReviewTests.cs","Test/ColorVision.Copilot.Tests/CopilotWorkspaceCancellationEvidenceTests.cs","Test/ColorVision.Copilot.Tests/CopilotWorkspaceRecoveryEvidenceTests.cs","Test/ColorVision.Copilot.Tests/CopilotWriteToolAdmissionTests.cs","Test/ColorVision.Copilot.Tests/CopilotToolResultContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotTurnEventProtocolTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentTaskEventJournalIntegrityTests.cs","Test/ColorVision.Copilot.Tests/CopilotCodexApprovalsReviewerTests.cs","Test/ColorVision.Copilot.Tests/CopilotAutomaticApprovalOverrideTests.cs","Test/ColorVision.Copilot.Tests/CopilotSharedCapabilityInputContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotPlanModeTests.cs","Test/ColorVision.Copilot.Tests/CopilotPartialEvidencePresentationTests.cs","Test/ColorVision.UI.Tests/FlowValidatorTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowGraphValidationToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowGraphValidationServiceTests.cs","Test/ColorVision.UI.Tests/CopilotFlowNodePropertyCatalogTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowNodeCatalogMetadataToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowGraphPaginationToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowGraphInspectionServiceTests.cs","Test/ColorVision.UI.Tests/CopilotFlowParameterIdentityTests.cs","Test/ColorVision.UI.Tests/CopilotFlowDisconnectionHostTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowDisconnectionToolTests.cs","Test/ColorVision.UI.Tests/CopilotFlowDisconnectionTests.cs","Test/ColorVision.UI.Tests/STNodeTransactionalDisconnectionTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowPatchParameterPrivacyTests.cs","Test/ColorVision.UI.Tests/CopilotFlowSensitivePropertyTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowParameterRedactionTests.cs","Test/ColorVision.UI.Tests/CopilotFlowPreviewPrivacyTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowPropertyApprovalPrivacyTests.cs","Test/ColorVision.Copilot.Tests/CopilotCredentialNamePolicyTests.cs","Test/ColorVision.UI.Tests/CopilotContextTextPrivacyTests.cs","Test/ColorVision.Copilot.Tests/CopilotSharedTextRedactionTests.cs","Test/ColorVision.Copilot.Tests/CopilotTextArchivePolicyTests.cs","Test/ColorVision.UI.Tests/CopilotFlowLogProjectionPrivacyTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunQueryToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunDiscoveryWorkflowTests.cs","Test/ColorVision.Copilot.Tests/CopilotMcpPartialResultContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotSharedCapabilityCatalogTests.cs","Test/ColorVision.Copilot.Tests/CopilotAlgorithmHelpToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunAttemptsToolTests.cs","Test/ColorVision.UI.Tests/CopilotDeviceServicesSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeviceServicesToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeviceServicesWorkflowTests.cs","Test/ColorVision.UI.Tests/CopilotDeviceHeartbeatSourceTests.cs","Test/ColorVision.UI.Tests/CopilotDeviceHeartbeatContextTests.cs","Test/ColorVision.UI.Tests/CopilotDeviceDiagnosisHeartbeatWorkflowTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultsWorkflowTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultsSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotLoadedBatchResultsServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotLoadedBatchResultsToolTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultReferenceContextTests.cs"]
related: ["copilot.runtime","copilot.execution","copilot.session-tools","copilot.interactions","copilot.extensions","copilot.view-model"]
---

# Copilot 工具契约

本页维护 Agent 与本机 MCP 的工具、审批、恢复证据及 Flow 编辑边界。Schema、限额和具体执行分支以 frontmatter 映射的源码为准；新增工具从 `ICopilotTool`、`CopilotSharedCapabilityCatalog` 与 `CopilotToolExecutor` 定位。

## 工具注册的最小契约

工具必须声明真实使用的输入 Schema、适用上下文和执行方法，只在组合根注册一次。注册表拒绝空名、忽略大小写的重名、非法 Schema 和不安全策略组合。新增能力须明确：

- 访问、风险、审批和幂等性；修改状态声明 `Write`，高风险写入不能声明从不审批。
- 并发资源及超时；只有只读幂等调用可共享读取，写入和未知幂等性均独占。排队不占执行超时，资源身份只持久化不可逆指纹。
- 取消与结构化终态；待审批用 `CopilotToolResult.Approval`，不能靠解析提示文本识别。只有可安全重复的瞬时故障才标记 `Transient`。

网页来源 `WebEvidenceSourceUrls` 随结果冻结：`null` 兼容旧文本识别，空集合明确无来源，空元素非法。来源元数据不证明全文已读，也不授予访问或恢复授权。

## 输入校验与无效调用

Agent 与 MCP 共用 `CopilotToolInputContractValidator` 递归校验，注册阶段拒绝不支持或矛盾的 Schema，调用阶段在审批和工具体之前拒绝非法输入。Schema 是执行约束，不能仅作为发现说明。非法 Agent 调用仍消耗调用预算并形成 `Failed + Validation` 证据；审计只留字段名，不创建审批。模型可以修正参数后重新调用，不能自动重试原非法输入。

`CopilotToolResultContract` 冻结可变集合并校验工具身份、终态及 evidence；伪造或违约内容不能流入 Hook、审计或模型。只读幂等工具违约为不可重试的内部错误；**写入或非幂等工具进入工具体后抛异常、返回空值或违约，必须记为 `OutcomeUnknown`，不能据此断言未写入或自动重试**。执行前已冻结的 `WorkspaceRecheckPaths` 用于核查可能的副作用。工具体尚未进入、合法结构化失败以及有效 MCP JSON-RPC 错误回复仍按明确失败处理，取消和超时保留各自终态。

部分读取成功应保留成功范围和逐文件错误，只有全部失败才把整次读取标为失败。`PartialResultMessage` 是独立的成功覆盖限制，跨共享能力、MCP 适配器、observation、快照与会话保存；业务 JSON 正文不拼接摘要或提示，也不从正文猜测 partial 状态。失败和待审批不能携带该成功提示。旧记录缺少提示时保持空值，空值也不证明任务完整完成。

未知函数由 Framework 按原 Provider CallId 返回 not-found，ColorVision 只观察并记录不可重试的 `Failed + NotFound`，不创建审批或执行工具。它仍消耗预算；服务端已处理或仅供信息的调用不重复计入。

同一运行内，相同工具和精确参数不得无进展重复：普通重试必须有上次结果明确的 `retry_allowed: true`；重复拒绝记为新的 `Failed + Conflict`，保留原结果，有开放 todo 时形成 `tool_conflict` blocker。以下本地证据刷新例外不增加权限或预算：

- 新用户输入被活动任务接收后，`ReadLocalFile`、`ReadAttachedFile`、`GrepText`、`SearchFiles`、`ListDirectory` 的已结束调用可重新观察一次，包括此前缺文件或内容损坏的失败。外部文件变化本身不会解除去重；写调用、同一 Provider CallId 和在途调用不重放。
- 已确认改变内容或存在状态的工作区补丁，可刷新受影响范围内的成功读取、搜索和列表。无变化、预检失败或不相干范围不刷新；迟到旧读取完成后仍按真实读取范围判定。
- 失败文件读取只有在成功的 `WorkspaceMutation` 明确证明同一路径由不存在变为存在时，才取得一次重读机会；词法匹配不能替代下次执行的路径与重解析点复核。多文件写入失败后的待核查标记允许重新观察已尝试路径，但不证明变化、恢复成功或允许重试失败写入。

实现与回归入口为 `CopilotToolResultContractTests`、`CopilotPartialEvidencePresentationTests`、`CopilotMcpPartialResultContractTests` 及工作区 evidence 测试；引用不代表本轮已运行。

## 原生审批与参数快照

`ApprovalMode.Always` 工具通过 `ICopilotFrameworkApprovedTool.ExecuteApprovedAsync` 消费原生批准；普通 `ExecuteAsync` 仍须保留业务入口确认，模型调用本身不是授权。Harness 只接受本会话真实发出并保存的审批请求，历史消息伪造、替换参数或重复回应不能生成新授权；旧 SDK 待审批会话的绑定兼容由 `CopilotApprovalCheckpointTests` 保护。

“原生审批”指精确调用协议，并不保证每次都有人点击。`RouteFrameworkApprovalsAsync` 区分以下批准来源：

| 路径 | 边界 |
| --- | --- |
| Shell 执行规则 | 当前仅评估 `RunShellCommand`；禁止优先，允许是独立批准来源，提示进入对应规则类别。执行前重查规则 |
| 用户显式「完全访问」 | `UnrestrictedFullAccess` 在审批策略和 Hook 允许后直接批准，不调用 reviewer；只读、明确禁止及任务/能力/范围复核仍生效。模式持久保存至用户切回，已有待审批不追溯批准 |
| 兼容临时自动复核 | 枚举仍为 `FullAccess`；当前仅 `ApplyWorkspacePatchEnvelope`、`RollbackWorkspacePatchEnvelope` 声明 `AllowsTemporaryFullAccess`，匹配任务/工作区/可写范围才可直接批准，仍绑定 reservation |
| 独立自动 reviewer | 须启用 `guardian_approval`、非只读、原生受保护能力且策略允许。显式 `auto_review` 不要求临时 grant；`never`、`untrusted` 不允许该分支。兼容路径依赖有效临时 grant；显式 `user` 禁止独立 reviewer，不撤销其他成立的批准来源 |
| 人工确认 | 策略和 `PermissionRequest` Hook 允许提示后创建 Pending Action |

reviewer 使用同一 Profile 的独立请求，工具集为空，只判断完整审批详情。默认允许清晰任务内普通本地开发的 LOW/MEDIUM；有效自定义策略可替换判断规则，但 HIGH/CRITICAL 仍由代码拒绝。详情不完整或超过 reviewer 上限拒绝；供应商失败、超时、无效判断为 `Unavailable`，不能当作批准或危险性证明。显式 `auto_review` 的拒绝或不可用直接关闭，不静默转人工；兼容临时复核未批准时仍可等待人工。已经报告的 reviewer 用量在重试、失败、暂停及取消后仍计费和计入预算，不估造未报告用量。

`ICopilotFrameworkApprovalPresentation.ReviewDetails` 提供不可静默截断的完整目标、命令或差异。超出上限拒绝，结束后清除，不写普通日志/审计；启用自动复核时，符合其更小上限的完整详情会发送给 Profile 的模型供应商。审查编码保留普通文本与不可见/控制字符的区别。人工窗口须显式核对详情，过期、取消或上下文变化使动作失效。

签名、审批、Hook、工具执行和记录必须消费同一个不可变 `CopilotAgentToolInputSnapshot`。批准绑定完整规范化参数哈希及任务/工作区、能力版本、Agent/Provider CallId；只有原请求的 `(ProviderCallId, Signature)` 才能消费一次 reservation。修改原嵌套集合、换 CallId 或截断展示摘要不能转移批准。所有批准来源均不扩大 Schema、工具范围、可写根或任务意图。

## /approve 与自动拒绝后的精确重试

`/approve` 优先列出或打开可人工复核的 Pending Action；`/approve N` 打开窗口，本身不批准执行。没有可审阅 Pending 项时，才列出当前会话/工作区的自动审查拒绝并允许创建一次重试票据。用户拒绝和 reviewer `Unavailable` 不进入该列表。

票据在进程内短期有效，仅绑定 conversation、规范 workspace、工具名和完整参数哈希，不保存可执行参数或旧批准。下一运行可用新 task/call ID，但仍须属于当前任务/工作区，并重新提出完全相同的调用；票据只向 reviewer 证明一次重试获用户授权，reviewer 仍可拒绝，不绕过 Schema、去重、预算或禁止规则。运行中用 steering，空闲时按调度条件发起新轮；调度失败可能只保留票据，反馈不代表已经执行。有效期和消费实现见 `TryAuthorizeOneRetry`；兼容和隔离测试见 `CopilotAutomaticApprovalOverrideTests`。

## 执行作用域和非权威日志

`CopilotExecutionScope` 统一 Agent、MCP、审批与审计的来源、调用身份、会话、任务、运行、工作区和能力版本；具体调用另绑定工具名、Provider CallId 和完整参数。Trace、父运行、工作区快照只能关联，不能继承授权；子 Agent 使用独立 RunId，原始 MCP token 不进入 scope 或审计。

权威执行内仍形成有界审计；次要 log4net 诊断经 `CopilotNonAuthoritativeDiagnosticBoundary` 隔离，普通 sink 异常不能把已完成操作改成失败，致命运行时异常仍上抛。

## 统一工具策略描述符

`CopilotToolCapabilityDescriptor` 是访问、风险、审批、幂等性、并发、超时、审计与 evidence 的唯一策略快照。Framework 包装、执行、重试和展示消费同一 Descriptor；非共享工具可用兼容属性桥接，共享工具直接引用 canonical 对象，不能各层重解释策略。

Framework 保持 provider 提交顺序与 CallId/result 配对，`CopilotToolExecutionGate` 只负责有界共享读取、资源互斥和全局写屏障。外部 MCP 的显式 `read-only` 信任映射为幂等共享读取，默认 `approval` 映射为受保护非幂等独占写入；两者只审计参数名。

执行器还必须重新检查 `IsAllowedForMode`：Plan、Review、Diagnose 和明确只读请求不能靠绕过发现、直接传工具或已有批准进入写工具体；Review 的显式有界工作区验证仍是原有例外。定位 `CopilotWriteToolAdmissionTests`，不要只测工具是否隐藏。

## 能力目录与来源版本

`CopilotCapabilityCatalog` 保存稳定身份、来源、策略及内容指纹，只在能力或来源签名变化时改变 revision。MCP 来源成功发现后原子更新，配置删除则移除，暂时离线保留最后已知元数据；来源身份不泄露端点、token 环境变量名或凭据。插件发布同样经过重复身份和策略校验。

设置诊断及只读资源 `colorvision://copilot/capabilities` 暴露同一元数据快照，不包含调用参数、远端地址或凭据。具体目录结构和限额直接查 `CopilotCapabilityCatalog`。

## Agent 与 MCP 的共享能力定义

`CopilotSharedCapabilityCatalog` 集中声明两侧名称、Schema、策略、说明、调用示例、执行路由与展示元数据。Agent 组合根和 MCP dispatcher 遍历 canonical 集合，各只保留按稳定 ID 分派的 resolver；遗漏 handler、路由或策略漂移在构造阶段失败。不得在 wrapper、dispatcher 或 trace 里再维护第二份能力表。

共享能力显式选择 `ApplicationCapabilityRuntime`、`SurfaceCapabilityAdapter` 或 `WorkspaceAuthorizationAdapter`。应用能力复用进程级 dispatcher，文档/日志保留结果表面投影，文件能力分别使用 Agent 的冻结选择范围和 MCP 的 workspace 授权根。共享定义不共享授权：Agent 仍经过预算、Hook、原生审批和执行器，MCP 仍经过 session identity、两阶段确认和审计。

输入原则上复用同一 Schema，现存表面差异有业务原因：Agent 文件读取可省略路径以读预选文件，MCP 必须给单文件；Agent 菜单只提供审批绑定的执行入口，MCP 另有 `dry_run`。新增差异须进入目录和一致性验证。

MCP annotations 与恢复说明也属于该目录：只读/幂等提示由策略派生，破坏性和开放世界显式声明。只有纯追加写入才可标非破坏；annotation 不替代授权。审批说明区分仅能手动恢复与未知恢复能力，不能承诺不存在的自动回滚。

设备与结果读取的关键来源边界：

- 自动设备上下文按已观测心跳区分 Online/Offline/Unknown；未观测不能因默认 bool 推成离线。`InspectDeviceServices` 只读已加载目录及受支持缓存，不初始化、探测或控制设备。精确对象须用工具返回的 `device_ref` 和 `expected_source_instance_id`，Code/名称/资源 ID 不能代替；成功不证明在线、健康或就绪。
- `InspectLoadedBatchResults` 只读已注册批次详情页的内存主记录。使用有效的 `source_ref`/宿主身份及 `result_ref`，不能用批次号、选中项或固定上下文 source 猜对象。页面释放或集合变化须重新发现；不查询历史数据库、加载图片、改变选择或输出敏感业务值。未知结果码不能补成 PASS/FAIL。

## 能力变化后的安全恢复

checkpoint 保存能力内容指纹，恢复不能只比重启后会重新计数的 revision。原能力缺失/变化、旧 checkpoint 缺少快照时，旧 Harness session 与 todo 不反序列化；有界 user/assistant 对话记忆与当前可见历史去重后交给新 session，按当前能力重规划。新增能力本身不使旧计划失效；记忆不包含 system/tool 消息、参数或授权。

`CopilotAgentEnvironmentContext` 以 host data 交付工作区、平台、Shell、时区、活动文档和有界根目录及只读 Git 身份，不读取环境变量或凭据。checkpoint 只存版本和稳定指纹；工作区、文档、Shell、时区或 Git 状态变化触发安全重规划，单纯日期推进不触发。旧数据缺指纹也重建可执行 session，而不丢失对话语义；路径描述不等于写入授权。

`CopilotToolEvidenceMode` 约束历史证据保存。`CopilotAgentEvidenceArtifacts.Merge` 只接收成功、已完成、只读幂等的记录，按能力/资源指纹有界去重；`NamesOnly` 不保存正文。新 session 可恢复脱敏摘要/摘录，但它们以独立 user-role 数据插入，不升级为 system 指令。`producer_changed`/`producer_unavailable` 仅作历史线索，易变状态必须重新核查，任何历史 evidence 都不构成写操作批准。

## 结构化任务事件 journal

`CopilotAgentTaskEventJournal` 统一记录运行、todo、工具、审批、steering、evidence 和 stop reason；关联只留哈希，摘要脱敏且容量有界，不保存执行参数。旧 checkpoint 缺少 journal、未知 schema 或可选 journal 损坏时丢弃该诊断数据，不能将其直接作为模型上下文。

journal 不代表审批或重放授权。`Finalize` 的特殊恢复只把最后已停止运行的有界结果/审批/blocker 作为不可信 user-role 历史数据，帮助解释已发生的结果，不能据此重放或声称重新核验。

checkpoint、独立 journal 和正式结果的提交权见[检查点与任务事件的所有权](./copilot-view-model-architecture.md#检查点与任务事件的所有权)。诊断 registry 的发布时间不是落盘回执，查询成功也不证明最新状态已保存。外部只读入口见 [Local MCP](./colorvision-mcp.md)。

裁剪尚未配对的 provider 调用、未结清工具或结果未知记录时，`TrimmedSessionResumeRestriction` 必须持久保留保守恢复限制；同一运行的后续快照不能弱化。旧数据缺字段按 `None` 兼容，真正新运行才清除限制。丢失细节不能解释为工具已完成。

结构化提问须先发布含 `UserQuestionRequested` 的 checkpoint，再进入等待；发布失败或宿主先行收尾时关闭未回答问题为 `Cancelled`，已回答内容保留。`CompleteOpenAgentRun` 幂等补齐终态：

| 中断位置 | 恢复含义 |
| --- | --- |
| 已 `ToolStarted` 而无权威终态 | `tool_outcome_unknown`，写入/非幂等操作先核查外部状态，禁止盲目重试 |
| 尚未开始的排队调用 | `tool_not_started`，操作未执行 |
| 等待审批中断 | `approval_interrupted`，操作未执行，下次重新审批 |

普通 Chat 收尾不能在展示层改写 Agent checkpoint。回归入口为 `CopilotAgentTaskEventJournalIntegrityTests`、`CopilotHostedTurnCompletionTests`。

## 结构化恢复协议

恢复由类型化 `CopilotAgentRecoveryRequest` 表达，运行时再次核对 journal 最后 stop reason；无效请求忽略。兼容 session 可继续，Profile/能力漂移则新建 session 重规划。预算、pass、provider 或应用中断可按策略恢复；`AwaitingUser` 等待新决定，`ApprovalDenied` 不提供普通继续。

“重试只读检查”只针对执行器标为 `RetryEligible` 的只读幂等失败，请求只保留工具名与哈希关联键，不保存或重放历史参数。所有恢复仍须核对当前状态、不复用历史批准；普通回答“重新生成”会清除 checkpoint，属于独立路径。

## 暂停、取消与 blocker

只有可序列化 session 完成首次真实持久化后，UI 才能承诺“暂停”；暂停保存可恢复 checkpoint，明确取消丢弃继续状态，外部取消不能伪装成用户暂停。任务 UI、队列及重启语义以[任务与恢复](./copilot-agent-session-and-tools.md#任务-ui、停止原因、运行中-steering-与后续队列)为准。

永久工具失败尚未解决且仍有 todo 时应为 `Blocked`，不能误报 pass 耗尽。后续只读调用以新 CallId、相同工具和精确参数成功，才可消除对应历史 blocker；原审计保留，参数相似、成功后再失败、写操作或未知结果不在此例。等待用户和预算终态的优先级不变。

内置受保护调用不一定产生人工 Pending；外部 MCP 不继承 Agent grant 或重试票据。写入面保持预览/应用分离，避免按参数临时改变审批语义。工作区、取消/超时和补偿失败的文件核查规则统一见[工作区补丁](./copilot-agent-execution.md#工作区补丁的预览、应用与回滚)；未知结果绝不因晚到成功而改写。

`SetTheme` 是独立低风险能力，通用 `ExecuteMenu` 不因预检认为某菜单低风险而直接执行。精确批准后只派发一次菜单请求，不再建第二个 Pending；能力拒绝不自动重复，取消后迟到的预览不创建动作。

## 模板 JSON 预览与应用

`TemplatePatch`/`preview_template_patch` 只计算差异。可应用预览必须从当前活动 JSON 编辑器绑定来源；显式 `current_json` 仅供离线预览，不产生可应用 ID，`template_identifier` 也不是按名称写任意模板的入口。

差异按完整 JSON 值比较，截断/脱敏摘要不能抹平变化。参数候选按真实 CLR 名和展示名同时判敏感；旧来源缺 CLR 名时按展示名兼容。父对象整体替换也不能借删除敏感子字段绕过限制。审批只展示所有变化字段的完整原值/新值，不带未变化正文；超过完整详情上限则拒绝创建动作，应缩小补丁，单字段仍过大则人工编辑。

`ApplyTemplatePatch`/`apply_template_patch` 通过当前表面审批，在建动作前及执行时复核来源、原 JSON、根对象和敏感限制；切换编辑器或内容冲突须重新预览。默认 handler 在 UI 线程更新编辑器文本、绑定参数及属性显示，**不调用 Save/DAO，不代表数据库持久化，也不承诺隔离副本或自动回滚**。保存权和动态来源见[业务上下文](./copilot-agent-extensions.md)。回归入口为 `CopilotTemplatePatchReviewTests`；真实 WPF 生命周期与业务存储仍须另验。

## Flow 图语义与受保护编辑

可见性、写意图与执行准入分工见[工具可见性与任务意图](./copilot-agent-execution.md#工具可见性与任务意图)。历史诊断先用精确标识或 `ListFlowRuns`/`ListFlowIncidents` 发现记录；事故 ID 不是运行 ID，未发现事故不证明成功。`colorvision-flow-diagnostics` 区分当前 UI 与留存证据；当前画布或有界列表不能证明历史配置、完整记录或根因，也不授予修改权限。

`.stn` 是带头和 GZip 的二进制画布，不按文本编辑。`InspectFlowGraph`/`get_flow_graph` 保留 `colorvision.flow-graph.v1`，用稳定节点 instance id、精确类型键、端口、`revision` 和宿主 `sourceInstanceId` 表达图；相同 revision 不证明同一宿主。读取须遵守：

- `nodes`、`edges`、`ports`、`properties` 分别分页，各自续传 opaque cursor；属性须显式 `include_properties=true`。焦点只按 instance id 或业务 NodeId 精确匹配，不猜名称/当前选择；纯空白焦点兼容读取全图。
- 节点页只附带本页内部边。完整拓扑须续完节点，再绑定同一 `revision` 与 `sourceInstanceId` 续完独立边流；端口/参数证据合并也须双绑定，不跨 section 复用游标。
- `has_more` 仅说明当前流分页；概览省略和文本限制看 `coverage` 及逐行标记。参数只覆盖宿主读到的 simple、visible 摘要；未请求属性时计数为 `null`，不是零。读完行流也不能恢复脱敏/截断文本。
- `propertyName` 才是宿主提供的 CLR 名，不能由展示名补造；旧来源可缺该字段，`parameterIndex` 仅为当前摘要序号，不是稳定 ID。摘要变化可使明细游标失效，即使保存状态 revision 未变。
- 游标绑定来源、版本、section、焦点及读取选项；过期重查。输出裁页只推进实际交付行，单行无法容纳时明确失败，不生成空页死循环。revision 不冻结实时 selection/status、设备或外部状态；节点保存异常的 fallback 哈希也不保证全部参数不变。

具体字段、上限与失败分类查 `CopilotFlowGraphInspectionService` 和 `CopilotFlowGraphInspectionServiceTests`，两侧分页适配查 `CopilotFlowGraphPaginationToolTests`。

`ValidateFlowGraph`/`validate_flow_graph` 以读取结果的当前 `expected_revision` 对完整宿主画布执行无弹窗的保存结构校验，不对裁剪片段下结论。来源/版本失效不返回结论；`valid=false` 仍可表示工具成功交付诊断。既有规则检查 Repeat 约束及任一 Start 沿执行端口到 End/显式终止的路径，允许多 Start/End，不因其他未接支路自行加规则。通过不证明设备就绪、业务参数有效、全部分支完整或执行成功。修改后须重读新 revision 再校验，不能复用旧结论背书新图。

`SearchFlowNodeCatalog` 返回当前注册类型及公开属性 Schema，添加节点须使用真实候选和精确 type key，不能猜类型。`declaredEnumNames` 保留原生枚举拼写，flags 不证明任意组合有效；标准布尔候选只适用于原生 bool 和默认 descriptor，nullable/自定义 descriptor 不猜候选。空候选不代表只读，以 `isWritable` 为准；最终仍由宿主 descriptor 转换和 setter 验证，不另造 parser。当前值由显式图读取取得；精确帮助 ID 的静态原理/参数说明见[节点帮助与正式读取契约](../../04-api-reference/extensions/flow-node.md)。

写入仅用 `PreviewFlowPatch`/`ApplyFlowPatch`，每次一种操作：目录 type key 添加节点、稳定实例 ID 与可写 CLR 属性改单值、精确端口连接或断开单条现有边。预览/应用均复核同一宿主、revision、只读及完整执行生命周期；属性先在离图副本中验证，复用原 descriptor 和连接守卫。敏感 CLR 名或展示名任一命中就拒绝，不能以普通名称绕过；新增节点参数、备注和旧/新值输出脱敏，但不改写源配置或指纹。自动上下文、审计与输出归档复用 Common 脱敏规则，不能承诺识别所有未标识 secret。

属性修改、相机继承和断连须在创建审批前完成可取消的正式异步预览；失败/取消的迟到结果不能创建动作或执行应用。完整审批详情展示转换后的属性变化或目标预览，两侧入口一致遵循敏感限制。

断连必须给出非零 Guid D 格式的源/目标实例 ID、`out:N`/`in:N` 四元组及当前 revision，不接受业务别名、缺边静默成功或整端口断开。事务边界只恢复指定边和编辑器历史；同步回调或提交后失败不能留下半条边，但不回滚回调自行改变的其他图对象或外部业务状态，也不重放业务通知。改线是断连、重读 revision、再连接的两次独立预览审批，不保证跨步原子性或自动回滚；中间图静态校验失败时报告诊断，不擅自扩展修图范围。

`add_node` 默认用构造初值；只有目录支持的直接参数相机节点可显式 `inherit_camera_defaults=true`。预览交付本次有效设备/曝光/增益/校正等设置及 `cameraDefaultsFingerprint`，应用原样传回 `camera_defaults_fingerprint`；审批前和执行前复核，源改变须重预览。该指纹只保护实际继承值，不将全局相机设置纳入图 revision，无效来源拒绝。

应用失败仅恢复本次属性、添加节点或新边；成功不保存、不运行流程。删除节点、整端口断连和批量 patch 未开放，不得借 Shell 或直接改 `.stn` 绕过。`colorvision-flow-authoring` 沿“读图/按需校验 → 查目录 → 单项预览 → 审批 → 重读并校验”操作，历史诊断保持独立。

回归从 `CopilotFlowGraphValidationServiceTests`、`CopilotFlowNodePropertyCatalogTests`、`CopilotFlowPatchParameterPrivacyTests`、`CopilotFlowDisconnectionTests` 及 `STNodeTransactionalDisconnectionTests` 定位；源码与替身测试不替代真实设备和完整业务验收。
