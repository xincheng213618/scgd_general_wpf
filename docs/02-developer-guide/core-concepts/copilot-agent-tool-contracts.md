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

## 工具注册的最小契约

工具实现 `ICopilotTool`，至少提供名称、说明、结构化输入 Schema、请求适用性和执行方法。Framework 只向模型暴露该工具声明的参数，并在执行前拒绝未知参数、缺失必填参数、错误类型和非法行号范围。新增工具时：

1. 实现 `CanHandle`，确保工具只在拥有所需上下文时暴露。
2. 通过 `InputSchema` 声明工具真正使用的参数、类型、说明和必填项。
3. 为修改应用状态的工具声明 `Access = CopilotToolAccess.Write`；只读工具保留默认值。
4. 声明 `RiskLevel`、`ApprovalMode` 和 `Idempotency`。高风险写工具不能使用 `ApprovalMode.Never`；否则统一前置策略直接拒绝执行。
5. 并发契约默认只允许 `ReadOnly + Idempotent` 进入 `SharedRead`；写工具和非幂等/未知只读工具均为 `Exclusive`。工具可通过 `GetConcurrencyKey` 描述资源身份；运行时只持久化其不可逆指纹，不记录原始查询或路径。
6. 按最坏合理耗时设置 `ExecutionTimeout`，默认 30 秒，框架上限 10 分钟。排队时间单独记录，不占用工具执行超时。
7. 在 `ExecuteAsync` 中响应传入的 `CancellationToken`，并返回对模型有用的结构化摘要、内容或错误。工具内部产生的待审批动作必须填充 `CopilotToolResult.Approval`，不能要求运行时解析提示文本。
8. 失败时填写 `FailureKind`。只有可安全重复的瞬时故障才使用 `Transient`；参数错误、权限拒绝、资源不存在、冲突和内部错误不能伪装成可重试故障。
9. 只在组合根注册一次；注册表会拒绝空名称和忽略大小写的重复名称。

网页来源元数据 `WebEvidenceSourceUrls` 随工具结果、Observation 和运行快照冻结；`null` 保留旧文本识别，空集合明确表示无来源，集合中的空元素会被统一结果契约拒绝。它只服务来源补写，不授予访问权限或证明全文读取完成，也不扩展 checkpoint 的恢复格式。

## 输入校验与无效调用

模型提交的工具参数即使未通过 Schema，也属于一次真实的 Agent 工具尝试：运行时会在执行闸门和审批之前拒绝它，消耗一次工具调用预算，并生成 `Failed + Validation` 的 step、ToolResult、任务事件和审计记录。非法参数的审计只保留字段名，不保存字段值；这类失败不可自动重试，但模型可以根据结构化错误改用修正后的参数再次调用。受保护工具同样先校验参数，非法调用不会创建 Pending Action。
Agent `CopilotToolInputSchema.TryBind` 与本机 MCP dispatcher 共用 `CopilotToolInputContractValidator`，递归执行同一组 JSON Schema 关键字，包括封闭对象、必填字段、类型、枚举、字符串/数字/数组范围、正则格式和数组唯一性。Agent registry、MCP dispatcher 与 capability catalog 的所有来源都在注册时验证 Schema；内置、业务模块扩展或外部 MCP 工具只要声明不支持或内部矛盾的约束，就不会进入活动工具面。不能出现 Agent 只校验顶层而 MCP 校验嵌套，或目录已经发布运行时并未执行的约束。工具体返回后，`CopilotToolResultContract` 在统一执行边界核对注册身份、成功/失败/待审批终态、进程 evidence 与非空字段，并复制所有可变集合；违约结果通常归一为不可重试的 `Internal + invalid_tool_output`，已进入写入或非幂等执行的调用按下述未知结果边界处理。原始伪造内容不会进入 Hook、事件、审计或模型结果。该边界不要求每个工具声明 DeepSeek 式输出 Schema，保留 ColorVision 的 operational result、模型可见结果和 Hook 投影。
共享 capability 的 `CopilotCapabilityResult.ToToolResult` 将未分类的失败映射为 `Unspecified`，保留原始原因及已经明确的失败分类，不凭异常文本猜测可重试性。成功结果携带失败字段仍属于违约，不由适配器自动掩盖。批量文件读取只要有有效文件，就在正文中列出逐文件 `[Read Error]` 并保留成功读取范围；只有全部失败才填写终态 `ErrorMessage`。这样部分读取不会被丢弃为 `invalid_tool_output`，失败文件也不会进入成功证据列表。

写入或非幂等工具进入执行后直接抛出普通异常、返回空值或违反结果契约时，没有可信的结构化终态，执行器记录 `Failed + OutcomeUnknown`，禁止自动重试，并保留执行前已冻结的 `WorkspaceRecheckPaths`，供模型重新核查可能已发生的副作用。异常或无效结果不证明写入未发生，也不证明所有候选路径均已改变；取消和超时继续使用各自终态。工具体尚未进入时，元数据读取抛出的普通异常仍按普通失败处理；只读幂等工具的异常和违约结果、工具明确返回的合法结构化失败继续使用原有失败分类。`CopilotToolResultContractTests` 以真实临时文件写入覆盖异常、空值和身份不匹配结果，核验候选路径及模型结果，不保留无效结果中的伪造内容。外部 MCP 适配器把有效 JSON-RPC 错误回复转为明确的结构化失败，避免执行器把服务端拒绝误判为结果未知。

成功调用的覆盖限制通过可选 `PartialResultMessage` 传递到 observation、运行快照和持久化活动记录；失败或待审批结果不能携带非空白提示，字段为 `null` 或与终态矛盾属于 `invalid_tool_output`。它由工具根据分页、扫描上限、未检查路径、读取失败、内容截断或未读附件生成，不从文件正文推断状态；空白提示只表示工具未报告限制，不证明整个任务已经完成。普通指定行范围和任务聚焦窗口本身不算截断。附件读取复用共享结果适配器并保留逐文件 `LocalFileReadScopes`，只有本批次仍有附件未读或实际读取不完整时才显示提示。结果捕获会脱敏提示，活动记录保存时按既有摘要上限截断；旧记录缺省为空，不追溯猜测历史覆盖情况。`CopilotPartialEvidencePresentationTests` 覆盖真实文件搜索／读取、结果快照、附件续读、共享业务能力与会话恢复；模型可见的具体范围、游标与逐文件错误仍由原工具正文提供。

共享 App 能力沿 `CopilotCapabilityResult` → `CopilotMcpToolCallResult` → `CopilotApplicationCapabilityCallResult` → `CopilotToolResult` 保留该独立覆盖提示，再进入既有 observation、模型快照与 Trace。业务 JSON 出口通过共享适配器显式选择 `contentOnly: true`，保留完整原正文，不拼接 `Summary` 或提示，也不根据 JSON 内容、字段名或普通标签猜测 partial 状态。成功提示先脱敏；合法失败继续保留原有失败分类。`CopilotMcpPartialResultContractTests` 与 `CopilotSharedCapabilityCatalogTests` 保护中间结果的提示捕获及终态约束，`CopilotAlgorithmHelpToolTests` 保护共享 JSON 正文与提示分别传递；这些测试引用不代表本轮已运行。

模型请求本轮工具面中不存在的函数时，`FunctionInvokingChatClient` 仍负责生成带原 CallId 的 not-found 结果并交回模型纠错；ColorVision 在其下游 provider 响应处做只读观察，不改写框架消息协议。未知调用消耗工具预算，并按保守的高风险、不可重试、无 evidence 策略记录 `Failed + NotFound` step、任务事件和字段名审计，但绝不会创建审批或进入执行器。观察器按 CallId 去重，并忽略 `InformationalOnly` 或同一 provider 响应中已有匹配 FunctionResult 的服务端已处理调用。
同一运行内模型重复提交完全相同的工具和参数时，普通重试要求上一次结果明确返回 `retry_allowed: true`。已成功、永久失败、正在运行或等待审批的相同调用会被无进展闸门拒绝；拒绝本身作为新的真实工具尝试消耗预算，并使用模型本轮 CallId 生成 `Failed + Conflict` step、ToolResult、任务事件和脱敏审计，但不会覆盖原调用的成功结果或重试状态，也不会再次打开审批。若仍有未完成 todo，最终 blocker 使用稳定代码 `tool_conflict`，停止原因为 `Blocked`，从而把重复调用循环与普通 pass 上限区分开。用户为自动审查拒绝显式创建的一次重试票据见下文；票据本身也不绕过此闸门。

用户在活动任务中提交且被接收的新补充输入，会允许本轮已有工具面中已结束的本地证据调用重新执行一次，也适用于此前因文件缺失或内容损坏而失败的调用。限定工具为 `ReadLocalFile`、`ReadAttachedFile`、`GrepText`、`SearchFiles` 和 `ListDirectory`。该次新观察之后，相同调用继续去重，直到又有新的用户输入；写调用、其他工具的失败调用、尚未结束的调用和相同 Provider CallId 不因此重放，文件范围、审批与全局调用预算也不扩大。外部文件变化本身不自动解除去重，用户可在原任务中补充“文件已补齐或修复，请重新读取”。新观察仍经过原冻结路径范围和工具执行检查；此前被拒绝的路径不会因补充输入获得授权。已成功应用且确认文件内容或存在状态变化的工作区补丁，会单独解除上述五类工具中受影响的成功调用去重，供保存后核验。文件读取按已读取的精确路径匹配，搜索和目录列表按实际查询范围匹配，即使先前没有任何匹配结果也能在新增、修改或删除后刷新；未指定路径时，搜索可覆盖多个根，而目录列表只对应实际列出的目录。该范围只用于本轮证据失效，不增加文件访问授权；无变化的补丁、写入前校验失败以及不相干目录不会解除去重，运行中调用仍不可重复启动。执行器释放读锁到 bridge 登记结果之间也属于未结束窗口：若此时先登记了工作区变更，待旧读取结束后按其真实文件或查询范围决定是否解除去重，避免迟到的旧结果阻止保存后核验；不相干范围和全局调用预算保持原约束。失败的 `ReadLocalFile`／`ReadAttachedFile` 另有确认新建例外：只有成功写入的 `WorkspaceMutation` 明确记录同一文件由不存在变为存在时，才允许重新读取一次。匹配使用工具已报告的尝试路径；本地文件读取在路径解析阶段失败时，则按冻结搜索根与原参数做词法匹配，下一次执行仍完整复核权限和重解析点。预览、已有文件修改、写入失败或仅标记待核查，都不能证明新建成功；这些情况不解除失败读取去重。该例外也覆盖旧失败结果晚于新建结果登记的窗口，仍须等旧调用结束才可重读。多文件补丁开始写入后失败，会对已尝试写入或恢复的精确路径单独标记需重新核查，允许上述本地观察再执行一次；该标记不是文件变化或恢复完成的证据，也不解除失败写调用的去重。

## 原生审批与参数快照

`ApprovalMode.Always` 的工具应实现 `ICopilotFrameworkApprovedTool`，获准后通过 `ExecuteApprovedAsync` 进入工具。Framework 是否包装 `ApprovalRequiredAIFunction` 由 `CopilotCodexApprovalPolicySelection.RequiresNativeApproval` 判断：工具自身的原生审批声明始终生效，冻结的 `untrusted` 策略还会把写能力升级到原生审批。普通 `ExecuteAsync` 必须继续保留直接调用和业务入口所需的确认，不能把“来自模型”本身视为授权。

Harness 的审批绑定保持启用：待审批回应必须匹配 SDK 在该会话中实际发出并保存的请求，调用方在历史消息中拼出的请求／回应不能替代这一记录；回应中的工具或参数被替换时，SDK 仍绑定原请求。已完成调用的历史审批不再次触发执行，ColorVision 的任务作用域、精确参数与执行前复核继续独立生效。`CopilotApprovalCheckpointTests` 使用公开 Harness API 覆盖流式与非流式的保存恢复、参数替换、重复回应和伪造历史，并用真实 1.21 SDK 生成的合成待审批会话检查升级后的绑定兼容性；该夹具只验证 SDK 会话协议，不代表所有历史工程或桌面恢复流程均已验收。

“原生审批”是精确调用协议，不等于每次必须由人点击。`RouteFrameworkApprovalsAsync` 的当前决定路径是：

- `CopilotCodexExecPolicyEvaluator` 当前只评估 `RunShellCommand` 的冻结执行规则。若给出 `Forbidden`，拒绝调用；`Allow` 是单独的批准来源，执行前再次核对；`Prompt` 进入对应规则审批类别，不走临时补丁直接批准分支。
- 用户显式启用「完全访问」时，`UnrestrictedFullAccess` 在审批策略与 `PermissionRequest` Hook 允许后直接批准受保护工具，不调用自动审查模型。执行前复核 grant、当前任务、精确参数和能力版本；只读模式与明确禁止规则仍生效。任务结束只解除任务绑定；访问模式持久保存，直到用户手动切回按需确认，已有待审批动作不会被追溯批准。
- 临时自动复核仍使用兼容枚举 `FullAccess`。`CanAutoApprove` 在此模式下只允许当前 workspace 与可写范围匹配且声明 `AllowsTemporaryFullAccess` 的工具。目前只有 `ApplyWorkspacePatchEnvelope` 和 `RollbackWorkspacePatchEnvelope` 声明此标志；直接批准仍绑定原生 reservation。
- 其余请求先通过审批策略与 `PermissionRequest` Hook，允许提示才创建 Pending Action。`CanAutoReview` 另行决定是否启用独立 reviewer：`guardian_approval` 必须启用、不能是只读模式且必须是原生受保护能力。显式 `approvals_reviewer=auto_review` 不要求临时 grant，但受当前审批类别与策略限制（`never`、`untrusted` 不允许此自动复核）；未显式选择 reviewer 时，兼容路径要求有效的任务/工作区临时 grant，且工具不属于补丁直接批准集合。显式 `user` 禁止独立自动复核，不代表撤销其他已成立的精确批准来源。
- `CopilotAutomaticApprovalReviewer` 用同一 Profile 的独立模型请求读取有界任务、近期 user/assistant 文本及完整审批详情，`Tools` 为空，不执行提议动作。默认策略允许清晰、任务内普通本地开发的 LOW/MEDIUM 判断；有效的 `CodexAutoReviewPolicy` 可以替换默认审查策略，但不替换无工具和结构化输出协议。即使模型输出 APPROVE，HIGH/CRITICAL 仍由代码强制拒绝。缺少完整详情或超出 24,000 字符会拒绝；供应商失败、超时或无效判断是 `Unavailable`，不能当作批准。独立复核已经报告的输入、输出和缓存用量继续计入本轮结果与预算，包括被丢弃的重试及失败、提供商自行取消、任务暂停或取消时已结算的用量；同一响应内的重复表示只记一次，没有已报告用量时不从预算估算制造账单。`CopilotCodexApprovalsReviewerTests` 经正式 Runtime 覆盖拒绝、额度失败、提供商取消后的重试及审批重试中的暂停和取消，核验费用保留、审批关闭、检查点语义及零工具执行。
- 兼容的临时任务复核未批准时，动作仍可等待人工确认；显式 `auto_review` 的动作不出现在人工 Pending 列表，DENY 关闭为 `automatic_review_denied`，Unavailable 关闭为 `automatic_review_unavailable`，不会静默回退人工窗口。Unavailable 只表示没有可用决定，不证明操作本身危险。自动批准落账前还会重查作用域；执行时仍检查精确输入、能力 revision、任务/工作区与动作状态。

这些路径都不扩大工具 Schema、可写根、任务意图或执行安全边界。自动复核是模型判断，不是对任意命令安全性的静态证明；不能因启用临时授权或自动 reviewer 就承诺发布、删除、外部写入等动作可执行。

需要展示具体目标和冲突指纹的工具还可实现 `ICopilotFrameworkApprovalPresentation`。`CopilotToolApprovalPresentation.ReviewDetails` 为本机审批窗口与符合条件的自动复核提供不可静默截断的完整执行详情；它有独立的 128K 上限，不进入 `ConfirmActionPayloadJson`、普通审计或日志，并在动作离开 `Pending` 后清除。启用自动复核时，符合其更小长度上限的完整详情会作为审查证据发给 Profile 对应的模型供应商，不能声称该字段始终只留在本机窗口。Shell 使用该字段展示解析后的 Shell、工作目录、超时、完整命令、字符数与命令 SHA-256；审查编码会将反斜杠加倍，并转义换行、Tab、Unicode `Format`、异常控制符和不可见分隔符，使普通文本与转义后的危险代码点保持可区分，详情区固定为从左到右。人工窗口要求显式确认已核对详情，并在动作过期、取消或任务上下文变化时失效。工作区补丁也能显示完整目标路径以及应用前后的 SHA-256，同时审计仍只记录参数字段名。

所有调用进入 `CopilotToolExecutor` 时都复用 `CopilotAgentToolInputSnapshot` 生成唯一执行输入，签名、审批、Hook、实际工具和 `ToolCall` 记录引用同一份快照；字符串、数字等不可变 JSON 标量保留既有 CLR 类型，可变对象和数组则保存为克隆的 `JsonElement`，顶层用只读字典封装，循环或不可序列化对象直接拒绝。审批动作以该快照生成规范化绑定：固定字段顺序、对象键递归排序、数组保持原顺序，再计算完整 SHA-256。恢复执行前同时复核工具与冻结输入的执行签名、审批动作的参数指纹、Agent call ID、Provider call ID 和当前任务/工作区；只有原请求与恢复调用的 `(ProviderCallId, Signature)` 完全一致时才能消费一次 reservation。这样即使原始嵌套 Dictionary/List 在签名或 Hook 之后被修改、另一个 call ID 重放相同参数，或展示摘要被截断，都不能改变实际执行或转移已有批准。

## /approve 与自动拒绝后的精确重试

`HandlePendingApprovalCommand` 优先处理当前可人工审阅的 Pending Action：无参数且仅一项时直接打开原生复核窗口，多项时列出；`/approve N` 打开指定项窗口，并不是命令本身直接批准。只有没有可审阅 Pending 项时，才改为列出当前 conversation/workspace 的自动审查拒绝；该分支的 `/approve N` 经 `TryAuthorizeOneRetry` 创建一次重试票据。

票据只绑定 conversation、规范 workspace、工具名和完整参数 SHA-256，允许下一运行使用新的 task/call ID，但新 Pending Action 仍须与该次请求的 task/workspace 一致。拒绝记录最多保留 24 小时；票据 30 分钟过期、只能消费一次且只在进程内保存，不包含可执行参数或旧批准。用户拒绝和 reviewer 的 Unavailable 不进入这个拒绝重试列表。

同会话 Agent 正在运行时，宿主发送 steering；空闲且允许调度时提交新的 Agent 轮次，有有效 checkpoint 时携带 `RetryDeniedAction`。其他任务占用、上下文不匹配或调度不允许时可能只保留票据，不能把命令反馈当成已经执行。模型仍须在原任务确有需要时重新提出完全相同的参数；票据不放宽 Schema、无进展闸门、调用预算或工具范围。新调用到达显式 `auto_review` 分支且精确匹配时，reviewer 才收到“一次重试获用户授权”的可信上下文，仍可能再次拒绝；它不是直接放行、相似动作许可或绕过不可覆盖规则的授权。

对应验证入口是 `CopilotCodexApprovalsReviewerTests`（资格、关闭决定与 unavailable 等边界）和 `CopilotAutomaticApprovalOverrideTests`（跨运行精确匹配、单次消费、列表脱敏和排除非自动拒绝）。真实模型判断和各受保护工具仍需端到端验证。

## 执行作用域和非权威日志

Agent、外部 MCP、审批与审计共享不可变的 `CopilotExecutionScope`。授权边界由来源、授权通道、不可逆 session/caller identity、会话、任务、运行、规范工作区和能力目录版本共同确定；具体调用再绑定工具名、Provider call ID 与完整参数签名。Trace、父运行和工作区快照只用于关联与审计，不会被误当作授权继承。子 Agent 只继承父作用域中的安全字段，并创建独立 RunId；原始 MCP session token 不进入该对象或审计。有界内存审计仍在权威执行路径内形成，但向 log4net 写出的次要诊断统一经过 `CopilotNonAuthoritativeDiagnosticBoundary`；普通 sink 异常只计数并被隔离，不能把已经完成的工具或 MCP 调用改写为失败，OOM 等致命运行时异常仍继续上抛。

## 统一工具策略描述符

`CopilotToolCapabilityDescriptor` 是工具策略的标准快照，集中承载访问级别、风险、审批、幂等性、并发、超时、参数审计和 evidence 模式。Harness 提示、Framework 审批包装、执行闸门、重试、trace 与审计都只消费该 Descriptor，避免各层分别解释工具属性。现有非共享工具的独立属性可由 `ICopilotTool.Capability` 默认桥接；与本机 MCP 重叠的 Agent 工具必须直接引用 `CopilotSharedCapabilityCatalog` 中的 Descriptor，兼容属性只能从该 Descriptor 派生。注册表会在工具进入运行时前拒绝非法枚举值及“高风险写入但从不审批”等不安全组合。有效并发与超时也在 Descriptor 中统一收敛：写入或非幂等能力强制独占，超时限制在默认 30 秒、最大 10 分钟之间。

普通工具执行从 `FunctionInvokingChatClient.CurrentContext.CallContent` 读取 provider 原始 CallId；Schema 拒绝、并发只读执行、原生审批、未知函数、trace、审计和任务事件因此使用同一个关联 ID。上下文由框架通过 AsyncLocal 隔离，并发函数不会互相串号；只有脱离 FunctionInvokingChatClient 的直接测试或业务调用才生成本地 CallId。并发调度不再复制第二套 ToolRuntime：框架负责保持 provider 提交顺序与 CallId/result 配对，`CopilotToolExecutionGate` 只负责最多 4 路共享读取、同资源互斥和全局写屏障；契约测试以反序完成的工具锁定提交顺序，防止依赖升级造成漂移。

外部 MCP 工具通过 `CopilotMcpClientCapabilityPolicy` 从本地信任配置生成同一 Descriptor：显式 `read-only` 映射为低风险、幂等共享读取，默认 `approval` 映射为高风险、每次审批、非幂等独占写入。两者均使用 `NamesOnly` 审计模式，只记录参数名而不持久化第三方 Schema 中含义未知的值。

写工具在 `CopilotToolExecutor` 的不可替换内置前置守卫中，重新使用目录的 `IsAllowedForMode` 规则检查本次请求。Plan、Review、Diagnose 和明确只读请求不能仅靠绕过发现目录、直接传入工具或已有批准进入写工具体；Review 的显式有界工作区验证仍是原有例外。Plan／Review 保留各自失败码，其余只读拒绝为 `request_write_access_denied`。`CopilotWriteToolAdmissionTests` 用真实目录与执行器同时验证无审批写工具、已批准受保护写工具的拒绝以及允许写入的对照。

## 能力目录与来源版本

`CopilotCapabilityCatalog` 在 Descriptor 之上提供进程内只读目录。共享目录启动时发布全部内置工具，外部 MCP 成功发现后按来源原子更新；配置中删除的 MCP 来源会移除，暂时离线的来源保留最后已知元数据。每个条目包含稳定 ID、显示名、来源、条目 revision、有效策略、超时和输入 Schema 指纹；整体目录仅在来源或能力签名真正变化时递增 revision。MCP 来源 ID 使用端点与 token 环境变量名称的不可逆短指纹，不暴露 URL、环境变量名称或 token。插件可通过 `PublishSource(Plugin, ...)` 和 `ICopilotCapabilityCatalogIdentity` 发布自己的稳定能力键，同样受 64 个来源上限、重复 ID 检查和 Descriptor 安全校验约束。

## Agent 与 MCP 的共享能力定义

内置 Agent 与本地 MCP 的重叠业务能力由 `CopilotSharedCapabilityCatalog` 声明稳定 capability ID、Agent 工具名、MCP 工具名、两侧说明、两侧输入 Schema、MCP 分类与调用示例、执行路由、Agent trace 展示元数据，以及 Agent 的完整 Descriptor。Agent 与 MCP 的说明允许针对各自表面采用不同措辞，但必须在同一 capability 项中并列声明；具体 Agent 工具只引用目录值，MCP dispatcher 则遍历 canonical `All` 集合自动物化所有共享 descriptor，并仅在一个按稳定 capability ID 分派的 resolver 中绑定具体 handler。新增目录项若没有 handler 会在 dispatcher 构造时失败，不再要求维护第二份共享 descriptor 清单。Descriptor 集中承载访问、风险、审批、幂等性、并发、超时、审计和 evidence 模式；三类 MCP 风险标签从其中的访问/风险/审批组合派生。共享 Agent 工具运行时直接引用目录对象，组合根仍对对象身份、实际说明、实际 Schema、MCP 分类/调用示例、完整 Agent 策略和 MCP 风险标签执行失败即停的漂移检查。同形输入直接复用同一个 `CopilotToolInputSchema`；只有文件读取与菜单执行保留带原因的表面差异：Agent 文件读取可省略路径以批量读取预选文件，外部 MCP 必须给出单一文件；Agent 菜单工具只暴露审批绑定的执行入口，外部 MCP 还提供 `dry_run`。MCP dispatcher 启动时先验证每个 descriptor 的冻结 Schema 是递归封闭且内部一致的对象契约，未实现的关键字会失败而不是静默降级，再建立按工具名索引；统一调用入口随后递归执行索引中同一 Schema，在 handler、审批和应用动作之前拒绝任意层级的未知字段、缺失必填项、错误类型、越界数字/数组和非法枚举，并统一返回 `invalid_arguments + Validation`。Schema 因此同时是启动约束、发现契约和执行契约，而不是仅供客户端参考的说明。Flow、菜单、模板、流程创建、主题和语言等 capability 在目录中显式标为 `ApplicationCapabilityRuntime`，对应 Agent wrapper 与本机 MCP 请求处理器都从 `CopilotApplicationCapabilityInvokerFactory` 取得同一个进程级默认 dispatcher；只有显式注入自定义 environment 的测试或宿主才创建隔离实例。文档与日志读取标为 `SurfaceCapabilityAdapter`：两侧复用同一业务 capability，但保留 Agent 工具结果与 MCP 文本协议的表面投影。四个工作区读取能力标为 `WorkspaceAuthorizationAdapter`，因为 Agent 必须使用本轮预选文件与搜索根，而外部 MCP 必须使用当前 MCP workspace 根；它们仍复用同一搜索、读取与规范结果实现。每个共享能力都必须显式选择这三种执行路由之一，遗漏路由会在目录初始化时失败。注册时若 wrapper 的路由标记与目录不符，或绑定的 runtime 不是组合根实例，都会直接失败。`CopilotApplicationCapabilityInvocation` 统一把成功、失败与等待审批结果投影成 Agent 工具结果；`CopilotAgentTraceEntry` 与 `CopilotAgentTraceGroup` 也直接从目录读取共享工具的运行/完成文案、成功摘要、搜索失败可见性和分组，不再维护另一张按工具名匹配的展示表。新增差异必须进入目录和一致性测试，不能在具体工具、descriptor 或 trace presenter 中另写一份说明、Schema、调用提示、策略、路由、展示分类、结果格式或风险标签。两种表面仍保留各自的可用性和授权管线：Agent 调用继续经过 provider CallId、预算、Hook、原生审批和 `CopilotToolExecutor`，外部 MCP 继续经过 session identity、两阶段确认和 MCP 审计，不能因为共享定义而互相继承授权。

自动设备上下文只按显式心跳观测区分 Online／Offline／Unknown，未观测不因公开 bool 的默认 false 变成离线；时间戳和缓存操作状态不补造心跳观测。该投影与正式设备工具的缓存状态保持独立。

`InspectDeviceServices` 是单个只读的已加载设备目录／精确对象入口，不沿用自动设备上下文的意图猜选。focus 只接受目录中的对象 nonce `device_ref` 与宿主 `expected_source_instance_id`，资源 ID、Code、名称和旧上下文 source 不能替代该绑定。默认来源在排队前冻结已有 manager，并在 UI 采集前后验证同一 owner；不存在的来源不会因查询而初始化。成功仅表示交付了 metadata 与受支持的内置缓存状态，不证明在线、健康、心跳新鲜、就绪或动作成功；插件和不可读状态保持明确未知，不读配置、不主动探测或控制设备。

`InspectLoadedBatchResults` 只查询已打开并注册的批次详情页内存主记录：优先使用自动上下文完整交付的有效 `source_ref`／`expected_source_instance_id` 对，否则先列来源；读行后可用该来源的 `result_ref` 精确聚焦。固定上下文来源仍是 `measurement-results`，不能把它或批次编号、结果编号、当前选择当作对象引用。绑定采集只读已有 registration token，不创建或重新注册页面；历史附件引用可能过期，正式校验失效后重发现。目录和记录分别分页并绑定对应集合，来源释放或对应集合变化须重新发现。成功只表示交付了该页已有 metadata，未知结果码不补成成功或客户 PASS／FAIL。工具不查询历史数据库、创建页面或 wrapper、加载 handler／图片、改变选择，也不输出路径、SN、原始消息、参数、测量值或文件存在判断。

Agent 组合根也遍历 `CopilotSharedCapabilityCatalog.All` 自动物化共享工具，不再手工列出 18 个 wrapper；它与 MCP dispatcher 各只保留一个按稳定 capability ID 分派的 adapter/handler resolver。目录新增能力但任一 resolver 未覆盖时，对应工具面在构造阶段直接失败。一致性测试还要求 Agent 共享子集与 canonical 目录顺序完全一致且只出现一次。

设置诊断显示目录数量与 revision；本机 MCP 将同一快照以 `application/json` 暴露为只读资源 `colorvision://copilot/capabilities`。该资源只包含能力元数据与 Schema 指纹，不包含参数值、远端地址或凭据。

共享 MCP descriptor 的 `readOnlyHint` 和 `idempotentHint` 从同一个 Agent Descriptor 派生；`destructiveHint` 与 `openWorldHint` 则在共享 capability 项中显式声明。只有纯追加写入可以声明非破坏性：流程/模板修改、通用菜单、主题和语言切换均保守标为可能破坏，创建新流程保持追加语义；在线文档搜索是当前唯一开放世界共享能力。非共享 MCP 写入未显式声明时按可能破坏处理，annotation 只提供客户端提示，不替代审批和授权。

共享的审批写入还在 capability 项中声明恢复等级与恢复说明。Agent Framework 在工具自定义或默认审批文案生成后统一叠加该元数据，本机 MCP 的确认上下文也按 MCP 工具名读取同一个值；Flow/模板修改、语言切换和创建流程因此在两条入口都显示“仅可手动恢复”，通用菜单保持明确的未知恢复能力，而不是维护另一张审批 switch 表。

## 能力变化后的安全恢复

Agent Framework checkpoint 保存目录 revision，以及每个能力的稳定 ID、条目 revision 和内容指纹。恢复时不能只比较进程内 revision，因为应用重启后 revision 会重新计数；运行时会逐项比较内容指纹。原能力缺失或指纹变化时，Harness session 与 todo ledger 不会反序列化，但 checkpoint 中独立保存的有界对话记忆会与当前可见历史去重合并后交给新 session，并通过 Harness instructions 强制从当前能力重新规划。该记忆最多 16 条、64K 字符，只允许 user/assistant 消息，保留初始目标与最近问答，不保存 system/tool 消息、参数或授权。新增能力不使旧计划失效；旧格式 checkpoint 缺少能力快照时同样走安全重规划。能力指纹覆盖描述、来源、策略、超时、审计模式、evidence 模式与输入 Schema，但目录和 checkpoint 都不保存调用参数或凭据。

每轮 Agent 还会生成版本化的 `CopilotAgentEnvironmentContext`。Harness 以明确标记为 host data 的 JSON 接收当前工作目录、平台/架构、首选 Shell、本地日期与时区、活动文档、最多 8 个搜索/可写根目录，以及通过只读 `.git/HEAD` 获取的仓库、分支和提交；它不会读取或发送进程环境变量、API key 或其他凭据。路径边界只描述工具可见范围，不代表写入授权。checkpoint 仅保存环境版本和稳定 SHA-256 指纹；工作区、活动文档、Shell、时区或 Git 状态变化会废弃旧 Harness session 并从有界对话记忆重新规划，单纯跨越本地日期不会使 checkpoint 失效。旧 checkpoint 没有环境指纹时同样只重建可执行 session，不丢失对话语义。

`CopilotToolEvidenceMode` 是 Descriptor 的显式证据持久化策略：`None`、`Summary`、`RedactedExcerpt`。只读工具默认 `Summary`，写工具默认 `None`；文档搜索、公开 Web 搜索和页面读取显式选择 `RedactedExcerpt`。`CopilotAgentEvidenceArtifacts.Merge` 仅接收成功、已完成、只读且幂等的 step record，按 capability ID 与哈希 resource key 去重，最多保留 24 条；`NamesOnly` 能力即使声明 excerpt 也只保存摘要。artifact 包含生产能力 ID/指纹、哈希资源键、脱敏摘要/摘录、内容指纹和采集时间，不包含调用参数。

只有 session 反序列化失败、profile 改变或 capability drift 触发新 session 时，恢复层才选择最近 12 条 artifact。可信防注入规则位于 Harness instructions；artifact JSON 使用单独的 user-role data message 插入到当前用户消息之前，避免把历史网页或工具内容提升为 system 指令。每条 artifact 标记 `producer_current`、`producer_changed` 或 `producer_unavailable`；后两者只能作为历史线索，所有易变化状态都必须重新核验，任何历史 evidence 都不能代表写操作审批。
## 结构化任务事件 journal

`CopilotAgentTaskEventJournal` 把原先分散的 todo snapshot、工具生命周期、原生审批、运行中 steering、evidence artifact 和 stop reason 归入同一条版本化序列。每次运行生成独立的 `run:` ID；工具 `CallId`、审批 action ID 和 steering 内容只生成稳定哈希关联键，todo 仅保存数字 ID 与完成统计。摘要复用 MCP 审计脱敏器并限制为 320 字符，journal 最多保留最近 256 条事件。

`CopilotAgentTaskEventJournal.Query` 支持按事件类型、run ID、工具名、subject/related ID 和 `BeforeSequence` 游标查询，单页最多 100 条，结果按新到旧返回。checkpoint 保存完整有界 snapshot，`CopilotAgentRunResult` 返回当前可查询 snapshot；旧 checkpoint、未知 Schema 或损坏的可选 journal 会被丢弃而不会变成模型上下文。journal 默认只作为诊断元数据，不代表任何审批或重放授权；唯一例外是 `Finalize` 恢复会把最后一个已停止 run 中最多 24 条脱敏工具结果、审批结果和 blocker 复制成独立 user-role 数据块，用于解释已经发生的结果。该数据块明确标为不可信历史数据，不能授权、重放或声称重新核验任何操作。

`CopilotAgentTaskEventJournalRegistry` 是当前选中会话 journal 的内存诊断投影。`PublishSelectedTaskEventJournal` 读取 `CurrentAgentTaskEventJournal`，发布有效非空快照；没有可用 journal 时清空 registry。会话切换及请求保存时都会发布，因此 `PublishedAtUtc` 是投影发布时间，不是磁盘保存回执。

checkpoint、独立 journal 与正式运行结果的所有权及提交规则统一见[检查点与任务事件的所有权](./copilot-view-model-architecture.md#检查点与任务事件的所有权)。有界窗口的比较只能验证仍保留的证据，不承诺无限历史的完整性；恢复操作还必须遵守下列提问与工具终态契约。裁剪当前运行尚未配对的 provider 调用、尚未结清的工具执行或结果未知记录时，journal 的 `TrimmedSessionResumeRestriction` 保存保守恢复限制，随独立 journal 和 checkpoint 持久化；丢失细节不能被解释为工具已完成。相同运行的后续快照和加载归一化不能弱化该限制，恢复提示明确说明早期细节已被裁剪并要求先核对外部状态。真正开始新运行后才清除旧运行的裁剪限制；新建 session 前仍先按原 checkpoint 判定是否需要重新规划。旧数据缺少该字段时按 `None` 读取，日志容量和 schema 版本保持兼容。`CopilotAgentTaskEventJournalIntegrityTests` 覆盖容量裁剪、持久化往返、后续调用结清和新运行的边界。

进入结构化提问等待态前，运行时必须先发布包含 `UserQuestionRequested` 的增量 checkpoint；发布失败会立即记录 `UserQuestionResolved(Cancelled)` 并向模型返回拒绝，不留下仅存在于内存的长期等待。若 checkpoint 写入尚未返回而宿主已因取消期限或异常收尾，可见消息中的待回答问题也必须关闭为 `Cancelled`，收起选项并显示取消状态；已经回答的问题及其答案保持不变。这不代表迟到的生产者或外部操作已经结束。`CopilotHostedTurnCompletionTests` 覆盖受控 checkpoint 等待及宿主终态清理。

取消控制、暂停和运行时异常的兜底路径统一调用 conversation 的 `CompleteOpenAgentRun`：它先在最近的 open run 上补齐缺失的控制事件、悬空工具与结构化提问终态以及 `RunStopped`，再原子保留或退休 checkpoint；未得到回答的结构化提问统一落为 `Cancelled`，不会在重启后永久保持 `pending`。已出现 `ToolStarted` 但没有权威终态的调用标记为 `Interrupted + tool_outcome_unknown`，恢复提示明确要求写入或非幂等操作先核对外部状态或询问用户，不能盲目重试；尚未启动的排队调用使用 `tool_not_started`，等待审批被中断则使用 `approval_interrupted`，两者都明确表示受保护操作尚未执行且下次必须重新审批。重复完成回调不会追加第二个终态，普通 Chat 的结束也不会在展示层改写 Agent checkpoint。

外部客户端可通过 Local MCP 的 `colorvision://copilot/task-events` 与 `get_agent_task_events` 查询这个诊断投影；可见会话范围、分页和 unavailable 条件见 [Local MCP](./colorvision-mcp.md)。查询成功不代表最新状态已落盘，也不产生执行授权。
## 结构化恢复协议

`CopilotAgentRecoveryPolicy` 为 `BudgetExhausted`、`TaskPassLimit`、`ProviderFailure` 和应用异常退出形成的 `Interrupted` checkpoint 提供恢复动作；`AwaitingUser` 必须等待新的用户决定，`ApprovalDenied` 也不会显示“继续”按钮。兼容 checkpoint 直接恢复 session；profile 或 capability 发生变化时改为新 session 重新规划。若最近存在执行器明确标记为 `RetryEligible` 的只读幂等失败，UI 显示“重试只读检查”，但恢复请求只保存工具名和哈希 call key，不保存或重放历史参数。

恢复意图以 `CopilotAgentRecoveryRequest` 类型化传入 Harness，而不是只依赖一段用户提示词。运行时会再次核对 checkpoint journal 的最后 stop reason，并记录 `RecoveryRequested` 事件；无效或与 checkpoint 不匹配的恢复元数据会被忽略。恢复指令始终要求重新核对当前状态：写操作不自动重放，历史审批不复用，受保护调用仍需本轮新的精确审批。普通回答的“重新生成”仍是独立路径，会清除 checkpoint，不会伪装成 Agent 恢复。
## 暂停、取消与 blocker

Agent 运行创建可序列化 session 并完成首次持久化后才发布内部 `CheckpointReady` 生命周期事件。此后编辑器主按钮切换为“暂停”：`CopilotAgentRunControl` 先记录 `Pause` 意图，再取消当前 provider/tool 等待；Agent Framework 只捕获这一类显式控制取消，使用不再取消的 finalization token 读取 todo ledger、写入 `PauseRequested` 与 `RunStopped(Paused)`，最后序列化 session。暂停产生的 checkpoint 可走相同恢复协议。到达真实落盘边界之前，主按钮保持普通停止语义，不会虚假承诺已保存。

运行中同时提供独立的显式取消动作。`Cancel` 同样形成可查询事件和 `Cancelled` stop reason，但不会保存本轮新 checkpoint；UI 会清除旧 checkpoint，避免把已明确放弃的任务再次作为可继续状态。外部超时、系统取消和没有类型化控制意图的 `OperationCanceledException` 仍向上传播，不会被伪装成用户暂停。

`CopilotAgentBlockerDetector` 将等待用户决定、审批拒绝，以及执行器明确判定不可重试的永久工具失败归一化为有界 `CopilotAgentBlockerSnapshot`。blocker 只保存类型、稳定 code、脱敏摘要、工具名和哈希 call key，不保存参数；assistant message 持久化最多 8 条，任务卡默认只显示一条紧凑提示。journal 追加 `BlockerDetected` 事件，本机 MCP 可沿用现有事件查询按类型、工具或关联 call key 检索。尚未解决的永久失败且仍有 todo 时 stop reason 为 `Blocked`，不再误报为普通轮次耗尽。

若后续只读调用以新的 CallId、相同工具和精确参数成功完成，任务终态与 blocker 判定共同排除已解决的历史读取失败、超时或拒绝；原始 step 和 journal 审计记录保持不变。匹配使用规范化执行签名，不凭摘要或文件名推断；参数不同、同一 CallId、成功之后的新失败、写操作及结果未知的操作均不能由此消除。该判定不批准或重放调用，等待用户和预算耗尽的优先级保持不变。`CopilotAgentBlockerTests` 覆盖匹配与保留边界；

`CopilotAgentTaskIndex` 从已持久化的会话、最后一条 assistant task ledger、stop reason、blocker 和 checkpoint 派生跨会话任务摘要，不维护第二套任务状态。索引收录仍有未完成 todo 且需要关注的暂停、等待回复、审批拒绝、永久阻塞、预算耗尽、轮次耗尽、Provider 中断和应用中断任务，也收录没有开放 todo 但仍缺最终回答的可恢复任务，并按会话更新时间排序。会话侧栏只显示紧凑标题与状态；可恢复任务可以直接继续，其他任务可以打开原会话补充输入，用户也可以显式放弃并清除 checkpoint。应用重启后会把 journal 中存在 `RunStarted` 但没有对应 `RunStopped` 的运行补记为 `Interrupted`，再从 `chat-state.json` 重建索引，因此不会因为进程退出或 UI 会话切换而丢失任务入口。

`CopilotAgentTaskHost` 是进程级单活动运行宿主，运行通过稳定 run ID 绑定原会话，并集中持有 cancellation token、类型化 run control、checkpoint-ready 边界和 completion task。宿主保留一个活动槽和最多 3 个等待项；从其他会话提交的 Agent 请求按 FIFO 排队，前一项无论成功、失败或取消都会释放槽并提升下一项。整个 Agent 运行仍然串行，因此多任务不会绕过 capability resource、写操作审批或工具执行闸门。暂停只能在 checkpoint 边界后发生，明确取消可以覆盖待处理的暂停；排队任务可在启动前取消，且不会调用模型或工具。订阅者或某个排队任务异常不会阻断后续任务。

Agent 执行期间允许切换或新建会话，事件和最终结果仍写回启动运行的原消息。同一活动会话中的新输入继续作为 Harness steering，不会误建第二个任务；同一排队会话也不会重复入队。提交时冻结 Profile、附件、活动文档和解决方案路径，任务真正启动时再采集最新设备、流程和应用状态。普通 Chat 不进入后台队列，并继续保持会话切换锁定，因为其历史构造依赖当前选中会话。Host 运行对象只存在于当前进程；满足 `ResumeAfterRestart` 条件的显式排队请求通过持久化恢复记录重新准入，不能继续使用旧运行对象或审批。持续目标和内部自动续作另有重启暂停边界，见[后续队列与恢复](./copilot-agent-session-and-tools.md#任务-ui、停止原因、运行中-steering-与后续队列)。Session checkpoint 和任务索引仍负责已开始任务的长期恢复。标题区只显示一条紧凑的运行或排队状态，不展开调度诊断。

可写入工具按以下能力边界使用这条审批协议：

- `CreateFlow`、`ApplyFlowPatch`、`ExecuteMenu` 和 `SetLanguage` 使用 `Always` 原生审批。
- 模板修改拆成低风险只读的 `TemplatePatch` 预览和高风险非幂等的 `ApplyTemplatePatch` 应用，避免一个工具根据参数临时改变审批语义。
- 工作区文本操作拆成只读的 `PreviewWorkspacePatch` / `PreviewCreateWorkspaceFile`，高风险非幂等的 `ApplyWorkspacePatch` / `ApplyCreateWorkspaceFile`，以及统一的 `RollbackWorkspacePatch`。默认可写根只包含当前解决方案目录；用户显式交给 Copilot 的现有文本文件和当前活动文档可作为精确修改授权，但新文件只能位于解决方案可写根。路径穿越、重解析点、非文本扩展、超过 1 MB 的编码内容、多重替换匹配和创建时覆盖现有路径均被拒绝。
- 多文件工作区补丁若在写入途中失败，会在工具错误内容中保留原始失败路径和逐文件补偿结果；回滚失败时同样记录逐文件重新应用结果。`WorkspaceRecheckPaths` 用于本轮重新读取或搜索已尝试路径，并生成任务消息的“文件状态待核查”提示；它不增加访问授权，原始路径集合不直接写入会话历史，也不作为生成差异的依据。提示独立于差异展开区，在没有任何已确认差异时也可见；最多列出 8 个路径，其余保留总数。若本轮先前已有差异，标题改为“先前确认的文件变更”，避免把历史记录当作当前完整状态。提示随 Assistant 消息保存／恢复，旧会话缺少此字段时保持不标记；有界读取或后续单个补丁不能证明整个失败事务已经核验，因此提示作为该轮未完整核验的历史随消息保留。后续已确认补丁可接续被标记路径的当前内容，已有原始基线仍保留；未标记路径的差异连续性检查保持严格。工具仍明确失败；逐文件成功只表示该次恢复动作成功，模型必须重新读取后再描述当前文件。`CopilotWorkspaceRecoveryEvidenceTests` 使用真实只读文件错误覆盖应用／回滚、恢复成功／失败和随后重新读取，补偿失败边界采用受控内部调用；这些验证不等于进程崩溃、取消路径或桌面人工验收。
- 已批准的工作区补丁或回滚跨过取消／超时边界时，执行器继续返回 `OutcomeUnknown`，禁止自动重试写操作；同时把对应变更集的可能受影响路径交给模型和任务消息，产生“文件状态待核查”提示。路径来自冻结参数所指向、且与当前 conversation／workspace 匹配的有效变更集，不代表每个文件已经开始写入、成功或恢复。路径快照在后台、工具体开始前获取；先发布不可变快照再检查取消令牌，确保取消发生于元数据读取期间时，延迟返回的工作线程不能继续进入写入体。原执行锁仍保留到后台工作结束，晚到的结果不会覆盖已经发布的未知终态。未批准或分发检查点失败时不捕获该范围。`CopilotWorkspaceCancellationEvidenceTests` 使用真实补丁应用／回滚与受控结果等待验证写入前后取消、超时、元数据等待及跨会话拒绝；这些用例不等于进程崩溃恢复验收。
- 已批准的 `ConvertBatchImages` 同样在未知终态保留经过授权校验的可能输出目录，复用 `WorkspaceRecheckPaths`，并要求检查路径。显式输出目录覆盖保留结构后的子目录；源旁输出按已验证源文件的父目录或源目录登记，不重新枚举图像。目录范围覆盖竞争重新编号后的输出，不能伪装成完整文件清单或已完成结果；核查时应列目录并读取实际文件。目录内的旧文件读取和子目录观测也会失效，避免把取消前的缓存当作本次写入后的状态。晚到成功仍不覆盖已发布的 `OutcomeUnknown`。
- `SetTheme` 是独立、低风险的明确能力；通用 `ExecuteMenu` 始终受保护，不会因某个菜单项在预检中被判定为低风险而直接执行。原生精确审批通过后只派发一次菜单请求，不再创建第二个 Pending；能力返回拒绝时不自动重复执行。未批准的调用仍走原有预览和待审流程，取消后迟到的预览不能再创建动作。

内置受保护调用经过上述原生精确审批路由，不保证每个调用都创建人工 Pending Action；外部 MCP 继续保留自己的两阶段确认协议，不继承 Agent 的临时 grant 或重试票据。

## 模板 JSON 预览与应用

`TemplatePatch` / `preview_template_patch` 只计算差异，不修改或保存模板。要获得可应用的 `preview_id`，应从活动模板 JSON 编辑器读取当前内容：不传非空 `current_json`，由 `CopilotMcpToolDispatcher` 绑定该编辑器的 `source_id`。显式提供 `current_json` 只供离线预览，不绑定活动编辑器，也不生成可应用预览；`template_identifier` 不是按名称写入任意已保存模板的入口。

模板补丁建议中的节点参数候选同时检查真实 CLR 属性名与展示名，任一名称命中敏感规则就不交付该候选及其值；普通参数仍保留同名展示标签的不同值，旧来源缺少 CLR 名时按展示名兼容处理。候选摘要经过输出脱敏，不改写源节点参数或活动模板 JSON，也不表示该字段已获准修改。

差异按完整 JSON 值比较；摘要中的截断或脱敏不会把实际变化误判为未变化。字段值摘要标明截断和脱敏，前后摘要相同而完整值不同时另行说明；摘要最多展示 80 项变化和 80 项警告，并显示省略数量。原生 Agent 与外部 MCP 的应用审批都通过既有 `ReviewDetails` 展示所有变化字段的名称、原值和新值，使用审查编码保留完整内容，不包含未变化字段或整个模板正文。敏感字段限制也覆盖原值含敏感子字段的父对象整体替换，避免借省略子字段删除敏感值或把旧敏感值送入审批详情；无关且未修改的字段不影响补丁。编码后的详情超过 131,072 字符时拒绝应用且不创建待审批动作，应减少单次变化字段；单个字段仍过大时由用户在模板编辑器中修改。已有自动复核的更小详情上限与动作结束时清除详情的规则继续适用。

`ApplyTemplatePatch` / `apply_template_patch` 必须引用有效预览，并通过所在表面的当前审批链。创建受保护动作前和真正执行时都会复核活动编辑器来源、预览时的原 JSON、补丁 JSON 根对象和敏感字段限制；切换编辑器或内容发生冲突时须重新预览。默认 handler 再通过 `EditTemplateJson.TryApplyCopilotJsonPatchAsync` 定位仍加载的编辑器，在 UI 线程复核当前 JSON 后，只更新 `textEditor.Text`、绑定的 `IEditTemplateJson.JsonValue` 及属性编辑器显示。成功不调用模板 `Save` 或 DAO，不代表数据库已持久化；这是对编辑器及其绑定内存参数的修改，不承诺另有隔离副本或自动回滚。保存仍由模板编辑/持久化流程负责，动态来源生命周期见[业务上下文](./copilot-agent-extensions.md)。

`CopilotSharedCapabilityInputContractTests` 验证预览参数和 `preview_id` 经包装器传给共享能力；`CopilotPlanModeTests` 验证 Plan 模式保留预览而拒绝应用。`CopilotTemplatePatchReviewTests` 覆盖完整值差异、摘要省略、两条审批链的完整详情及其超限拒绝。这些测试不是实际 WPF 编辑器冲突、应用失败、取消或数据库未写入的端到端验证，也不表示本轮已运行测试。

## Flow 图语义与受保护编辑

Flow patch 的可见性由流程相关上下文与当前写入约束决定；动作同义表达或同时要求解释不会隐藏工具。可见性与任务意图、只读模式及原生审批的分工见[工具可见性与任务意图](./copilot-agent-execution.md#工具可见性与任务意图)。

正式历史诊断缺少精确标识时，按问题使用只读 `ListFlowRuns` 或 `ListFlowIncidents` 发现记录；已有用户或正式上下文提供的精确标识时，可直接交给对应证据工具。事故 ID 不能当作运行 ID，没有事故也不表示没有运行或运行成功。内置 `colorvision-flow-diagnostics` 将当前 UI 线索与留存记录分流，按需读取事件、节点尝试、历史、留存模板或差异；选中身份元数据、当前画布和有界列表本身不证明历史执行配置、完整记录或根因，诊断也不授予变更权限。

`.stn` 是带自定义头和 GZip 内容的二进制画布格式，不交给模型按文本读取。`InspectFlowGraph`／MCP `get_flow_graph` 保持 `colorvision.flow-graph.v1` 的原有图字段，返回节点 Guid 对应的稳定 instance id、精确 `module|runtime type` 键、结构化端口、位置、确定性 SHA-256 `revision` 及宿主生成的 `sourceInstanceId`。后者是每个宿主稳定的32位十六进制实例标识，不依赖图 revision，不暴露原始路径；相同 revision 不表示相同宿主。属性值仍仅在显式 `include_properties=true` 时返回并脱敏。

`section` 可选 `nodes`／`edges`／`ports`／`properties`，默认 `nodes`；`max_nodes`、`max_edges` 各为1–200，默认80，明细流另用 `max_items`（1–200，默认80）。`properties` 流必须显式传 `include_properties=true`，不能借选择该 section 隐式读取参数值。可选 `node_id` 长度1–256，纯空白沿用省略焦点、读取全图的兼容语义；其余值不 trim，按节点 instance id 或业务 NodeId 作 OrdinalIgnoreCase 精确匹配，可能命中多个实例，不按名称、前缀或当前选择猜测。可选 `expected_revision` 为64个十六进制字符，`expected_source_instance_id` 为32个十六进制字符。两者分别原样回传已读结果中的 `revision`／`sourceInstanceId`，用于锁定来源和图版本；非空 opaque `cursor` 最多4,096字符，须原样续传。

节点流按 `InstanceId` 的 Ordinal 顺序分页，只附带本页交付节点之间的内部边，附带边另受 `max_edges` 和正文预算限制，省略由 `coverage.edges_truncated` 说明。根对象的 `has_more`／`next_cursor` 只针对当前流；读完所有节点页也不能宣称跨页连线已读全。独立 `edges` 流按源节点／源端口／目标节点／目标端口四元组的 Ordinal 顺序遍历边；指定 `node_id` 时，返回任一端连接所有精确匹配节点实例的边，能读取两端处于不同节点页的连线。

节点概览每个方向最多附带8个端口、最多8条已请求的参数摘要，正文预算不足时可降到0；`inputPortCount`／`outputPortCount`／`propertyCount` 保留范围内总数，`portsTruncated`／`propertiesTruncated` 说明概览省略。未启用属性读取时 `propertyCount` 为 `null`，不能解释为没有参数。需要完整端口行时使用独立 `ports` 流，需要宿主公开的全部参数行时使用 `properties` 流并显式启用属性读取，原样续读各自游标。

端口明细以 `nodeInstanceId`、`direction` 和 `portId` 标识；参数明细交付 `nodeInstanceId`、`parameterIndex`、`propertyName`、展示 `name`、`value` 及 `textTruncated`。参数仅覆盖宿主成功读取的 simple、visible 属性摘要，不是全部 CLR 配置；值同时按真实 CLR 名和展示名判断敏感内容并脱敏。行流结束不恢复被脱敏或超过512字符的值文本；每条参数的 `textTruncated` 和全局 `coverage.text_truncated` 说明文本限制，没有值文本续读。

`parameterIndex` 是当前摘要按 CLR 属性名 Ordinal 排序后的零基序号，旧来源缺少键的条目保留源顺序兼容，不是跨版本稳定 ID；`propertyName` 在宿主提供时保留准确 CLR 名，旧来源可能为空，不能从展示标签推导补键。即使已读到真实属性名，仍须在节点目录确认可写状态和 descriptor，再通过正式预览修改。明细游标还绑定已交付格式的有界端口／参数证据，显示摘要改变时即使保存状态 revision 未变也须重查；它不证明截断之外的值文本已被读取。

需要完整图拓扑时，先续完节点流，再以该结果的 `revision` 和 `sourceInstanceId` 同时传入两个 expected 字段，开始无游标的边流并续到结束；需要合并端口或参数证据时，对独立明细流的第一页同样传回两个标识。独立流首次读取允许省略双绑定，但合并证据应绑定同一来源和版本，不能跨 section 复用游标。每页最多18,000字符的完整 JSON，尾部缩行后只按实际交付行推进；节点尾裁后重算内部边，不跳过尚未交付节点、边或明细行。游标绑定宿主实例、图 revision、section、精确焦点、include-properties 选项、流键及偏移；页数量上限可调整，其余绑定变化为 `Validation`，需重新查询。

原有 `totalNodeCount`／`returnedNodeCount` 保留，另有 `totalEdgeCount`／`returnedEdgeCount`。顶层 `ports`／`properties` 数组交付对应明细流，`totalPortCount`／`totalPropertyCount` 说明完整焦点范围，`returnedPortCount`／`returnedPropertyCount` 仅计明细流本页行数，不计节点概览附带项；未请求属性时总参数数为 `null`。非节点流的节点数组为空。`isTruncated` 包括当前流分页或概览明细省略，`coverage.page_offset` 是当前流偏移；`scope_node_count`／`scope_edge_count`／`scope_port_count`／`scope_property_count` 说明焦点范围，参数计数同样可为 `null`。各类省略分别查看 `edges_truncated`／`ports_truncated`／`properties_truncated`，不能只看 `has_more` 推断概览完整。`text_truncated` 标明文本字段限制，`output_limited` 标明正文预算缩页；极长标签使单节点仍超出预算时，可进一步压缩描述文本并保留完整 instance id。单行最终无法容纳时明确失败且不推进游标，不能以空行和可续页游标形成循环。

每次快照读取在宿主 UI 回调的采集前后核对同一来源并处理取消。首次无当前图为 `NotFound`，已有游标所依赖的来源消失或绑定过期为 `Validation`，超时为 `Transient`，异常或不一致来源为 `Internal`，取消继续传播。跨页不冻结 selection、status 等实时展示；相同 revision 只遵循现有保存状态和结构哈希口径，节点保存异常时已有 fallback 哈希边界保持不变，不能据此保证全部实时或外部状态不变。

`CopilotFlowGraphInspectionServiceTests` 保护四类流的实际交付、明细证据／来源／revision 绑定、输出预算与敏感文本；`CopilotFlowGraphPaginationToolTests` 保护 Agent／MCP 的分页参数和相同 JSON 结果。`CopilotFlowParameterIdentityTests` 保护真实属性名、重复展示名及两种名称的脱敏边界；`FlowExecutionEditingTests` 的真实宿主场景验证来源实例 ID 在同宿主跨 revision 稳定、不同宿主可区分及默认 UI 读取绑定。这些测试不表示已验证历史运行图或现场设备状态。

`ValidateFlowGraph`／MCP `validate_flow_graph` 对当前 Flow 宿主管理器的完整画布执行现有静态校验，不以 `get_flow_graph` 裁剪后的节点片段代替整图。必填 `expected_revision` 是从 `InspectFlowGraph`／`get_flow_graph` 原样取得的64个十六进制字符；宿主 `FlowCopilotService.ValidateCopilotFlowGraph` 只在当前 revision 匹配时调用 `FlowValidator.TryValidate`。UI 调度还复核同一个宿主管理器；来源切换或 revision 过期为 `Validation`，不检查另一版画布。无编辑器为 `NotFound`，超时为 `Transient`，异常或不一致的宿主结果为 `Internal`，均不交付校验结论。正常检查后即使 `valid=false`，工具调用仍成功交付诊断，不能把“工具成功”理解为“图已通过”。

成功正文为最多18,000字符的完整 JSON，`source=current_flow_editor`、`validation_kind=host_save_structure`，包含 `revision`、`valid`、`diagnostic`、可选 `invalid_node_id`、整图 `node_count`／`connection_count` 及 `is_execution_active`／`is_editor_read_only`。`coverage.full_current_graph=true` 说明检查范围；诊断有界脱敏，最多2,048字符，裁剪和脱敏分别由 `diagnostic_truncated`／`diagnostic_redacted` 标记。诊断是数据而非指令，也不承诺列出全部问题。执行活动使用 `View.IsExecutionActive`，包含准备、图执行、停止和后处理，不能仅按 `FlowControl.IsFlowRun` 判断；这些状态不增设结构校验规则，也不授予编辑或运行权限。工具只调用无弹窗的 `TryValidate`，不调用会聚焦节点并显示消息框的 `Validate` 包装器，也不保存、运行或修改画布状态。

规则与宿主保存前校验相同：先校验 Repeat 区域的结构、次数及已有重复参数约束，再检查 Start、End 和执行路径；允许多个 Start／End，任意 Start 沿 `CVStartCFC` 执行端口可达 `CVEndNode` 或显式终止节点即通过。循环控制端口不能替代执行路径，其他未接完的支路不会自行新增为失败。通过仅表示这些静态规则成立，不证明设备就绪、算法参数的业务有效性、所有分支完整、运行成功或历史运行图。

需要理解当前问题或复核编辑结果时，可在编辑前用当前 revision 静态校验；单项 patch 应用后先重新 `InspectFlowGraph`，再以新 revision 校验，比较宿主诊断。静态检查不代替 patch 预览、审批或之后的业务验证，不能用旧 revision 的结论背书新画布。

`FlowValidatorTests` 保护多 Start／End、任一完整路径、执行／循环端口区分、显式终止和 Repeat 校验的既有语义；`CopilotFlowGraphValidationServiceTests` 检查宿主结论与工具成功的区分、来源／revision 失效、诊断边界及异常分类；`CopilotFlowGraphValidationToolTests` 检查 Agent／MCP 同协议、参数、`valid=false` 的成功结果及失败／取消交接。这些边界测试不代表真实设备或完整业务流程验收。

`SearchFlowNodeCatalog` 查询当前编辑器实际注册的节点类型，并返回标题、分类、业务节点类型、默认设备 Code、是否支持继承相机初值的 `canInheritCameraDefaults` 和公开的 `STNodeProperty` Schema。例如“添加相机节点”必须先搜索 `相机` / `camera`，由模型在 `CVCameraNode`、`LVCameraNode`、XR/AOI 等真实候选中选择；候选含义不同则询问用户，不能硬编码或猜测类型名。目录按已加载类型签名缓存，插件节点集合变化时自动重建。

属性 Schema 保留原有 `dataType`、`isWritable` 等信息，并公开声明的 `descriptorType`、`declaredEnumNames`、`isFlagsEnum` 和 `booleanStringCandidates`。枚举名称仅来自属性类型本身 `IsEnum` 时的原生声明，包含别名，保留精确拼写，不使用本地化展示标签；`isFlagsEnum` 仅说明该枚举是否声明 `[Flags]`，不枚举组合或承诺组合均业务有效。仅属性类型恰为 `bool` 且 descriptor 恰为默认 `STNodePropertyDescriptor` 时，才给出字符串候选 `True`／`False`。不解包 `Nullable<T>`，也不为自定义布尔 descriptor 猜测字符串候选；读取这些声明不构造 descriptor 或读取属性值。

这些字段是声明元数据，不是完整允许值域。空候选不表示只读，应单独查看 `isWritable`；真实 CLR 属性名或 `STNodeProperty` 展示名命中敏感规则时，目录标记 `isWritable=false`，预览与应用均拒绝该属性，不能用普通 CLR 名绕过敏感展示名。自定义 descriptor 可能接受额外兼容拼写或不同文本格式，声明的枚举名也不保证绕过自定义转换和 setter 后仍有效。修改时使用精确原生名称或标准布尔候选，未知格式交给正式 `PreviewFlowPatch` 检查，不另造 parser、推断 flags 合法组合或从描述文字猜测数字范围。当前值仍从图快照取得，预览与应用复核相同的可写边界，并沿用宿主的 descriptor 字符串转换与 setter。

`CopilotFlowNodePropertyCatalogTests` 验证真实节点声明到预览的衔接，以及 flags、别名、自定义 descriptor 和 nullable 的元数据边界；`CopilotFlowNodeCatalogMetadataToolTests` 验证 Agent／MCP 返回相同字段并保留原有目录信息。声明投影不额外构造 descriptor 或读取属性值，不代表整个目录不构造节点：标题、设备等信息仍沿用原有节点构造流程。

目录的 `algorithmHelpTopicId` 提供可选的精确帮助引用；非空时用 `ReadAlgorithmHelp`／MCP `read_algorithm_help` 读取随包原理和参数说明，当前参数值仍由 `InspectFlowGraph` 显式查询。正文续读与静态参考边界见[节点帮助与正式读取契约](../../04-api-reference/extensions/flow-node.md)。

写入面收敛为 `PreviewFlowPatch` / `ApplyFlowPatch` 两个工具，而不是为每种动作持续增加工具。每次 patch 只允许一种操作：`add_node` 使用目录返回的精确 type key；`set_property` 使用节点的稳定 ID、目录公开的可写 `propertyName` 和现有 `STNodePropertyDescriptor` 字符串转换，不建立第二套属性转换器，并拒绝密码、token、secret、license 等敏感属性；`connect` 使用图快照中的 `out:N` / `in:N` 端口 ID，并复用 `CanConnect` / `ConnectOption` 的方向、所有者、锁定、单连接、重复边、数据类型和环路校验；`disconnect` 只断开明确指定的一条已存在连线。

Flow 参数与审计的敏感名分类统一复用 Common 的 `IsSensitivePropertyName`，覆盖 `Authorization`、`Bearer`、密码、token、license、中文凭据名和精确 `SN` 等类别；真实 CLR 名与展示名分别判定，名称匹配不受当前 culture 影响。Engine 在共享分类之外继续保留原有更保守的拒绝集合，不放宽既有编辑限制。

自动 Flow／设备业务上下文的 `MaskSensitiveText` 与 MCP 审计的自由文本脱敏共用 Common `CopilotSensitiveTextRedactor.Redact`，处理已支持的 Authorization Header、Bearer 文本、敏感赋值（含内部有分隔符或转义符的引号值）及凭据格式，保留非敏感字段和现有 Header 名／分隔格式。工具输出归档共用 Common 的 `SupportedAssignmentMarkers` 标准标记定义，Header／凭据预处理委托 `RedactArchiveSource`，跨 chunk 的引号状态与资源限制仍由流式归档自身管理。规则作用于 Copilot 交付投影，不回写 UI 原始日志或节点参数；Flow 采集先对完整 UI 运行消息脱敏，再截尾和提取失败行，因此运行消息、失败摘要与失败证据字段保存的是已脱敏日志投影。自由文本规则不等于对任意未标识 secret 的语义识别。

Flow patch 预览 JSON 在序列化输出时保护参数：`add_node` 节点的 `parameters[].value` 与图读取共用真实 CLR 名和展示名的双名称脱敏，节点备注、属性 `oldValue`／`newValue` 等字符串还会去除内联敏感值。输出保留原有结构、普通参数值和 JSON 数值类型，以及相机初值指纹。预览序列化不改写已捕获的节点参数原值或相机有效值；宿主仍以真实配置完成校验、克隆和指纹计算，不能拿已脱敏文本替换源配置或证明敏感值已被读取。

`CopilotFlowPatchParameterPrivacyTests` 保护参数预览、旧值／新值和模板候选的敏感输出边界，以及普通重复标签、相机数值／指纹和节点参数源值；`CopilotFlowSensitivePropertyTests` 保护目录与实际属性守卫一致，并验证属性修改入口在读取旧值、离图克隆或 descriptor 转换前拒绝敏感属性，普通 descriptor 转换继续有效。`CopilotFlowParameterRedactionTests` 覆盖旧 Flow 文本工具的双名称脱敏。这些引用不表示已运行测试或完成现场验收。

断连必须给出图中精确的 `source_node_id`／`target_node_id` 稳定实例 ID（非零 Guid `D` 格式，不接受业务别名）及 `source_port_id=out:N`／`target_port_id=in:N` 四元组，预览和应用均须传回当前64个十六进制字符的 `expected_revision`。沿正式预览与审批执行，审批明确展示将断开的目标。不存在的边明确失败，不能当作已断开的静默成功；也不支持省略目标端点或一次断开整个端口。预览和应用继续复核 revision、编辑器只读和执行生命周期守卫；排队期间已切换宿主的请求也会被拒绝，须重读当前图并重新预览。成功不保存或运行流程。

宿主通过 `PreviewCopilotFlowDisconnection` 只读检查精确边，通过 `DisconnectCopilotFlowNodes` 应用；真正删除边之前，在同步回调后再次核对 revision、生命周期、只读状态及同一对端口所有权。应用复用 `DisConnectOptionTransactional` 的单边编辑边界：成功沿原生 veto／事件路径，启用历史记录时形成单次 undo；回调、历史通知或提交后快照采集抛出异常时，恢复指定边两端的连接结构和编辑器 undo／redo／状态／缓存及 pending 记录，不重载节点。全部状态恢复后，尽力向 undo／redo 集合发出 `Count`、`Item[]` 和 `CollectionChanged(Reset)` 通知；通知期间保留单边编辑作用域，拒绝嵌套历史操作，undo／redo 暂不可用，释放作用域后再刷新命令重查。各类刷新通知异常分别隔离，保留原始失败。不重放节点回调或 `HistoryChanged` 业务通知，也不保证抛错 observer 之后的所有观察者都收到刷新。恢复范围不包括回调自行修改的其他图对象或已经产生的外部业务状态，不能把它解释为完整业务事务。

改线是两次独立操作：先预览并审批 `disconnect`，成功后重新读图取得新 revision，再对 `connect` 单独预览和审批。两步不构成原子批量修改，也不承诺跨步骤自动回滚；静态校验可能在断开后的中间图上返回 `valid=false`，应报告诊断，不能自行增加修图、保存或运行操作。

`CopilotFlowDisconnectionTests` 验证精确边预览、已有守卫、指定边应用及原生 undo／redo；`STNodeTransactionalDisconnectionTests` 保护同步回调、历史通知及提交后异常时的指定边与历史恢复边界；`CopilotFlowDisconnectionHostTests` 验证默认路由、排队切换宿主及回调后守卫复核；`CopilotFlowDisconnectionToolTests` 验证两条审批链的完整目标、冻结参数及失败准入。测试引用不等于真实业务回调或现场流程验收。

`add_node` 默认使用节点构造初值。显式传 `inherit_camera_defaults=true` 时，仅目录标记支持的直接参数相机节点复用普通创建入口的参数解析规则；没有有效相机或源参数无效时拒绝预览。预览节点的 `cameraDefaults` 完整列出实际设备 Code、单路或 R/G/B 曝光、有效增益、平均次数、翻转和校正模板名，`cameraDefaultsFingerprint` 是节点类型与这些有效值的确定性指纹，不含临时节点 Guid。应用必须把该值传为 `camera_defaults_fingerprint`；审批前重新预览和执行前都校验它，来源变化则拒绝添加并要求新预览。指纹只保护本次继承设置，不把全局相机配置纳入图 revision；未使用的相机和未参与有效值的原始参数变化不使旧图失效。这两个相机参数不用于其他操作。

原生审批通过 `ICopilotFrameworkAsyncApprovalPresentation` 在创建 Pending 动作前读取 `set_property`、相机继承或断连的正式预览；这些操作要求异步展示入口。协调器先冻结输入，再等待可取消的展示任务；预览失败或取消后的迟到结果不创建审批动作，也不执行应用，迟到异常被观察。属性修改必须先通过宿主可写与 descriptor 转换检查，敏感展示名即使对应普通 CLR 名也直接拒绝，不把提议值送入审批窗口。成功后，属性审批的编码 `ReviewDetails` 展示脱敏预览中的旧值与转换后新值，遵守既有大小限制和终态清除规则。原生与外部 MCP 的相机继承及断连审批继续展示编码后的完整预览；外部 MCP 的属性修改同样先正式预览成功才进入确认流程。

预览在 UI 线程验证活动流程未运行且 revision 未过期；属性修改在从真实节点持久化数据构造的离图副本上验证，连接及断连预览只检查指定操作的资格。`ApplyFlowPatch` 经原生审批后再次检查相同 revision，再执行单项修改；属性、添加节点或连接失败时恢复属性旧值、移除已加入节点或断开本次新边。成功也不会自动保存或运行流程。revision 基于每个节点真实 `GetSaveData()` 字节哈希和结构化边生成，复杂 `STNodeProperty` 的变化也会失效旧预览；单节点保存异常时退回稳定的基础节点状态哈希，避免上下文采集整体失败。删除节点、整端口断开和批量 patch 尚未开放，不能回退到 Shell 或直接改 `.stn` 绕过边界。对应的 `colorvision-flow-authoring` Skill 使用“检查图并按需静态校验 -> 查目录/属性/端口 -> 预览单项 patch -> 审批 -> 重读 revision 并静态校验”的短流程，诊断仍由独立的 `colorvision-flow-diagnostics` 负责。
