---
knowledge_id: "copilot.tool-contracts"
knowledge_type: "topic"
status: "current"
summary: "Copilot 工具结果、事件、审批恢复和 Flow 编辑必须遵守的执行契约。"
aliases: ["工具返回成功是否代表任务完成","审批和恢复怎样绑定调用","临时授权能自动运行Shell吗","自动审查拒绝后如何重试","模板补丁会自动保存吗","CopilotToolExecutionContracts","CopilotToolRegistry","CopilotAgentAccessContext","CopilotAutomaticApprovalReviewer","approvals_reviewer","/approve","TemplatePatch","ApplyTemplatePatch","preview_id","current_json","InspectFlowGraph","SearchFlowNodeCatalog","PreviewFlowPatch","ApplyFlowPatch","colorvision-flow-authoring"]
code_paths: ["ColorVision/Copilot/Agent/CopilotToolExecutionContracts.cs","ColorVision/Copilot/Agent/CopilotToolExecution.cs","ColorVision/Copilot/Agent/CopilotAgentTaskEventJournal.cs","ColorVision/Copilot/Agent/CopilotAgentAccessModels.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.ApprovalRouting.cs","ColorVision/Copilot/Agent/CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ApprovalExecution.cs","ColorVision/Copilot/Agent/CopilotFrameworkApproval.cs","ColorVision/Copilot/Runtime/CopilotAutomaticApprovalReviewer.cs","ColorVision/Copilot/Runtime/CopilotAutomaticApprovalOverrideStore.cs","ColorVision/Copilot/CopilotChatViewModel.LocalCommandWorkflows.cs","ColorVision/Copilot/CopilotChatViewModel.Permissions.cs","ColorVision/Copilot/Agent/CopilotSharedCapabilityCatalog.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotFlowGraphTools.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.FlowPatch.cs","ColorVision/Copilot/Skills/colorvision-flow-authoring","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.TemplatePatch.cs","ColorVision/Copilot/Mcp/CopilotMcpToolDispatcher.TemplatePatchSupport.cs","ColorVision/Copilot/Mcp/CopilotMcpToolModels.cs","Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotToolResultContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotTurnEventProtocolTests.cs","Test/ColorVision.Copilot.Tests/CopilotAgentTaskEventJournalIntegrityTests.cs","Test/ColorVision.Copilot.Tests/CopilotCodexApprovalsReviewerTests.cs","Test/ColorVision.Copilot.Tests/CopilotAutomaticApprovalOverrideTests.cs","Test/ColorVision.Copilot.Tests/CopilotSharedCapabilityInputContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotPlanModeTests.cs"]
related: ["copilot.runtime","copilot.execution","copilot.session-tools","copilot.interactions","copilot.extensions"]
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

## 输入校验与无效调用

模型提交的工具参数即使未通过 Schema，也属于一次真实的 Agent 工具尝试：运行时会在执行闸门和审批之前拒绝它，消耗一次工具调用预算，并生成 `Failed + Validation` 的 step、ToolResult、任务事件和审计记录。非法参数的审计只保留字段名，不保存字段值；这类失败不可自动重试，但模型可以根据结构化错误改用修正后的参数再次调用。受保护工具同样先校验参数，非法调用不会创建 Pending Action。
Agent `CopilotToolInputSchema.TryBind` 与本机 MCP dispatcher 共用 `CopilotToolInputContractValidator`，递归执行同一组 JSON Schema 关键字，包括封闭对象、必填字段、类型、枚举、字符串/数字/数组范围、正则格式和数组唯一性。Agent registry、MCP dispatcher 与 capability catalog 的所有来源都在注册时验证 Schema；内置、业务模块扩展或外部 MCP 工具只要声明不支持或内部矛盾的约束，就不会进入活动工具面。不能出现 Agent 只校验顶层而 MCP 校验嵌套，或目录已经发布运行时并未执行的约束。工具体返回后，`CopilotToolResultContract` 在统一执行边界核对注册身份、成功/失败/待审批终态、进程 evidence 与非空字段，并复制所有可变集合；违约结果归一为不可重试的 `Internal + invalid_tool_output`，原始伪造内容不会进入 Hook、事件、审计或模型结果。该边界不要求每个工具声明 DeepSeek 式输出 Schema，保留 ColorVision 的 operational result、模型可见结果和 Hook 投影。
模型请求本轮工具面中不存在的函数时，`FunctionInvokingChatClient` 仍负责生成带原 CallId 的 not-found 结果并交回模型纠错；ColorVision 在其下游 provider 响应处做只读观察，不改写框架消息协议。未知调用消耗工具预算，并按保守的高风险、不可重试、无 evidence 策略记录 `Failed + NotFound` step、任务事件和字段名审计，但绝不会创建审批或进入执行器。观察器按 CallId 去重，并忽略 `InformationalOnly` 或同一 provider 响应中已有匹配 FunctionResult 的服务端已处理调用。
同一运行内模型重复提交完全相同的工具和参数时，普通重试要求上一次结果明确返回 `retry_allowed: true`。已成功、永久失败、正在运行或等待审批的相同调用会被无进展闸门拒绝；拒绝本身作为新的真实工具尝试消耗预算，并使用模型本轮 CallId 生成 `Failed + Conflict` step、ToolResult、任务事件和脱敏审计，但不会覆盖原调用的成功结果或重试状态，也不会再次打开审批。若仍有未完成 todo，最终 blocker 使用稳定代码 `tool_conflict`，停止原因为 `Blocked`，从而把重复调用循环与普通 pass 上限区分开。用户为自动审查拒绝显式创建的一次重试票据见下文；票据本身也不绕过此闸门。

## 原生审批与参数快照

`ApprovalMode.Always` 的工具应实现 `ICopilotFrameworkApprovedTool`，获准后通过 `ExecuteApprovedAsync` 进入工具。Framework 是否包装 `ApprovalRequiredAIFunction` 由 `CopilotCodexApprovalPolicySelection.RequiresNativeApproval` 判断：工具自身的原生审批声明始终生效，冻结的 `untrusted` 策略还会把写能力升级到原生审批。普通 `ExecuteAsync` 必须继续保留直接调用和业务入口所需的确认，不能把“来自模型”本身视为授权。

“原生审批”是精确调用协议，不等于每次必须由人点击。`RouteFrameworkApprovalsAsync` 的当前决定路径是：

- `CopilotCodexExecPolicyEvaluator` 当前只评估 `RunShellCommand` 的冻结执行规则。若给出 `Forbidden`，拒绝调用；`Allow` 是单独的批准来源，执行前再次核对；`Prompt` 进入对应规则审批类别，不走临时补丁直接批准分支。
- `CanAutoApprove` 只对有效 `FullAccess` grant 下、非只读模式、当前 workspace 与可写范围匹配且声明 `AllowsTemporaryFullAccess` 的工具直接批准。目前只有 `ApplyWorkspacePatchEnvelope` 和 `RollbackWorkspacePatchEnvelope` 声明此标志；直接批准仍绑定原生 reservation，不是任意工具免审。
- 其余请求先通过审批策略与 `PermissionRequest` Hook，允许提示才创建 Pending Action。`CanAutoReview` 另行决定是否启用独立 reviewer：`guardian_approval` 必须启用、不能是只读模式且必须是原生受保护能力。显式 `approvals_reviewer=auto_review` 不要求临时 grant，但受当前审批类别与策略限制（`never`、`untrusted` 不允许此自动复核）；未显式选择 reviewer 时，兼容路径要求有效的任务/工作区临时 grant，且工具不属于补丁直接批准集合。显式 `user` 禁止独立自动复核，不代表撤销其他已成立的精确批准来源。
- `CopilotAutomaticApprovalReviewer` 用同一 Profile 的独立模型请求读取有界任务、近期 user/assistant 文本及完整审批详情，`Tools` 为空，不执行提议动作。默认策略允许清晰、任务内普通本地开发的 LOW/MEDIUM 判断；有效的 `CodexAutoReviewPolicy` 可以替换默认审查策略，但不替换无工具和结构化输出协议。即使模型输出 APPROVE，HIGH/CRITICAL 仍由代码强制拒绝。缺少完整详情或超出 24,000 字符会拒绝；供应商失败、超时或无效判断是 `Unavailable`，不能当作批准。
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

## 能力目录与来源版本

`CopilotCapabilityCatalog` 在 Descriptor 之上提供进程内只读目录。共享目录启动时发布全部内置工具，外部 MCP 成功发现后按来源原子更新；配置中删除的 MCP 来源会移除，暂时离线的来源保留最后已知元数据。每个条目包含稳定 ID、显示名、来源、条目 revision、有效策略、超时和输入 Schema 指纹；整体目录仅在来源或能力签名真正变化时递增 revision。MCP 来源 ID 使用端点与 token 环境变量名称的不可逆短指纹，不暴露 URL、环境变量名称或 token。插件可通过 `PublishSource(Plugin, ...)` 和 `ICopilotCapabilityCatalogIdentity` 发布自己的稳定能力键，同样受 64 个来源上限、重复 ID 检查和 Descriptor 安全校验约束。

## Agent 与 MCP 的共享能力定义

内置 Agent 与本地 MCP 的重叠业务能力由 `CopilotSharedCapabilityCatalog` 声明稳定 capability ID、Agent 工具名、MCP 工具名、两侧说明、两侧输入 Schema、MCP 分类与调用示例、执行路由、Agent trace 展示元数据，以及 Agent 的完整 Descriptor。Agent 与 MCP 的说明允许针对各自表面采用不同措辞，但必须在同一 capability 项中并列声明；具体 Agent 工具只引用目录值，MCP dispatcher 则遍历 canonical `All` 集合自动物化所有共享 descriptor，并仅在一个按稳定 capability ID 分派的 resolver 中绑定具体 handler。新增目录项若没有 handler 会在 dispatcher 构造时失败，不再要求维护第二份共享 descriptor 清单。Descriptor 集中承载访问、风险、审批、幂等性、并发、超时、审计和 evidence 模式；三类 MCP 风险标签从其中的访问/风险/审批组合派生。共享 Agent 工具运行时直接引用目录对象，组合根仍对对象身份、实际说明、实际 Schema、MCP 分类/调用示例、完整 Agent 策略和 MCP 风险标签执行失败即停的漂移检查。16 项同形输入直接复用同一个 `CopilotToolInputSchema`；只有文件读取与菜单执行保留带原因的表面差异：Agent 文件读取可省略路径以批量读取预选文件，外部 MCP 必须给出单一文件；Agent 菜单工具只暴露审批绑定的执行入口，外部 MCP 还提供 `dry_run`。MCP dispatcher 启动时先验证每个 descriptor 的冻结 Schema 是递归封闭且内部一致的对象契约，未实现的关键字会失败而不是静默降级，再建立按工具名索引；统一调用入口随后递归执行索引中同一 Schema，在 handler、审批和应用动作之前拒绝任意层级的未知字段、缺失必填项、错误类型、越界数字/数组和非法枚举，并统一返回 `invalid_arguments + Validation`。Schema 因此同时是启动约束、发现契约和执行契约，而不是仅供客户端参考的说明。Flow、菜单、模板、流程创建、主题和语言等 12 个 capability 在目录中显式标为 `ApplicationCapabilityRuntime`，对应 Agent wrapper 与本机 MCP 请求处理器都从 `CopilotApplicationCapabilityInvokerFactory` 取得同一个进程级默认 dispatcher；只有显式注入自定义 environment 的测试或宿主才创建隔离实例。文档与日志读取标为 `SurfaceCapabilityAdapter`：两侧复用同一业务 capability，但保留 Agent 工具结果与 MCP 文本协议的表面投影。四个工作区读取能力标为 `WorkspaceAuthorizationAdapter`，因为 Agent 必须使用本轮预选文件与搜索根，而外部 MCP 必须使用当前 MCP workspace 根；它们仍复用同一搜索、读取与规范结果实现。每个共享能力都必须显式选择这三种执行路由之一，遗漏路由会在目录初始化时失败。注册时若 wrapper 的路由标记与目录不符，或绑定的 runtime 不是组合根实例，都会直接失败。`CopilotApplicationCapabilityInvocation` 统一把成功、失败与等待审批结果投影成 Agent 工具结果；`CopilotAgentTraceEntry` 与 `CopilotAgentTraceGroup` 也直接从目录读取共享工具的运行/完成文案、成功摘要、搜索失败可见性和分组，不再维护另一张按工具名匹配的展示表。新增差异必须进入目录和一致性测试，不能在具体工具、descriptor 或 trace presenter 中另写一份说明、Schema、调用提示、策略、路由、展示分类、结果格式或风险标签。两种表面仍保留各自的可用性和授权管线：Agent 调用继续经过 provider CallId、预算、Hook、原生审批和 `CopilotToolExecutor`，外部 MCP 继续经过 session identity、两阶段确认和 MCP 审计，不能因为共享定义而互相继承授权。

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

`CopilotAgentTaskEventJournalRegistry` 只发布当前选中会话最近一次已保存的 snapshot；新一轮保留上一安全点，直到新的增量 checkpoint 原子替换它，运行完成后再发布带最终 stop reason 的版本。conversation 通过 `CurrentAgentTaskEventJournal` 派生当前值：checkpoint 存在时必须持有当前权威 journal，并且是内存与持久化的单一 owner；只有没有可恢复 checkpoint 时，`LatestAgentTaskEventJournal` 才独立持有证据。终态由 `CommitAgentRunState` 以 run-result journal 为权威一次提交；checkpoint 落后或属于另一 run 时，先创建重基到该 journal 的 checkpoint 副本，而不是持久化两个非等价 owner。旧快照加载时也会选择较新 evidence 并迁入 checkpoint。进入结构化提问等待态前，运行时必须先发布包含 `UserQuestionRequested` 的增量 checkpoint；发布失败会立即记录 `UserQuestionResolved(Cancelled)` 并向模型返回拒绝，不留下仅存在于内存的长期等待。取消控制、暂停和运行时异常的兜底路径统一调用 conversation 的 `CompleteOpenAgentRun`：它先在最近的 open run 上补齐缺失的控制事件、悬空工具与结构化提问终态以及 `RunStopped`，再原子保留或退休 checkpoint；未得到回答的结构化提问统一落为 `Cancelled`，不会在重启后永久保持 `pending`。已出现 `ToolStarted` 但没有权威终态的调用标记为 `Interrupted + tool_outcome_unknown`，恢复提示明确要求写入或非幂等操作先核对外部状态或询问用户，不能盲目重试；尚未启动的排队调用使用 `tool_not_started`，等待审批被中断则使用 `approval_interrupted`，两者都明确表示受保护操作尚未执行且下次必须重新审批。重复完成回调不会追加第二个终态，普通 Chat 的结束也不会在展示层改写 Agent checkpoint。本机 MCP 通过 `colorvision://copilot/task-events` 暴露最近 100 条，通过只读 `get_agent_task_events` 支持类型、run、工具、关联 ID、`before_sequence` 和 `max_events` 过滤。两者均为显式诊断入口，不加入默认 diagnostic bundle，也不产生聊天活动行；没有已保存 journal 时直接返回 unavailable，不回退到日志搜索或其他工具。
## 结构化恢复协议

`CopilotAgentRecoveryPolicy` 为 `BudgetExhausted`、`TaskPassLimit`、`ProviderFailure` 和应用异常退出形成的 `Interrupted` checkpoint 提供恢复动作；`AwaitingUser` 必须等待新的用户决定，`ApprovalDenied` 也不会显示“继续”按钮。兼容 checkpoint 直接恢复 session；profile 或 capability 发生变化时改为新 session 重新规划。若最近存在执行器明确标记为 `RetryEligible` 的只读幂等失败，UI 显示“重试只读检查”，但恢复请求只保存工具名和哈希 call key，不保存或重放历史参数。

恢复意图以 `CopilotAgentRecoveryRequest` 类型化传入 Harness，而不是只依赖一段用户提示词。运行时会再次核对 checkpoint journal 的最后 stop reason，并记录 `RecoveryRequested` 事件；无效或与 checkpoint 不匹配的恢复元数据会被忽略。恢复指令始终要求重新核对当前状态：写操作不自动重放，历史审批不复用，受保护调用仍需本轮新的精确审批。普通回答的“重新生成”仍是独立路径，会清除 checkpoint，不会伪装成 Agent 恢复。
## 暂停、取消与 blocker

Agent 运行创建可序列化 session 并完成首次持久化后才发布内部 `CheckpointReady` 生命周期事件。此后编辑器主按钮切换为“暂停”：`CopilotAgentRunControl` 先记录 `Pause` 意图，再取消当前 provider/tool 等待；Agent Framework 只捕获这一类显式控制取消，使用不再取消的 finalization token 读取 todo ledger、写入 `PauseRequested` 与 `RunStopped(Paused)`，最后序列化 session。暂停产生的 checkpoint 可走相同恢复协议。到达真实落盘边界之前，主按钮保持普通停止语义，不会虚假承诺已保存。

运行中同时提供独立的显式取消动作。`Cancel` 同样形成可查询事件和 `Cancelled` stop reason，但不会保存本轮新 checkpoint；UI 会清除旧 checkpoint，避免把已明确放弃的任务再次作为可继续状态。外部超时、系统取消和没有类型化控制意图的 `OperationCanceledException` 仍向上传播，不会被伪装成用户暂停。

`CopilotAgentBlockerDetector` 将等待用户决定、审批拒绝，以及执行器明确判定不可重试的永久工具失败归一化为有界 `CopilotAgentBlockerSnapshot`。blocker 只保存类型、稳定 code、脱敏摘要、工具名和哈希 call key，不保存参数；assistant message 持久化最多 8 条，任务卡默认只显示一条紧凑提示。journal 追加 `BlockerDetected` 事件，本机 MCP 可沿用现有事件查询按类型、工具或关联 call key 检索。永久失败且仍有 todo 时 stop reason 为 `Blocked`，不再误报为普通轮次耗尽。

`CopilotAgentTaskIndex` 从已持久化的会话、最后一条 assistant task ledger、stop reason、blocker 和 checkpoint 派生跨会话任务摘要，不维护第二套任务状态。索引收录仍有未完成 todo 且需要关注的暂停、等待回复、审批拒绝、永久阻塞、预算耗尽、轮次耗尽、Provider 中断和应用中断任务，也收录没有开放 todo 但仍缺最终回答的可恢复任务，并按会话更新时间排序。会话侧栏只显示紧凑标题与状态；可恢复任务可以直接继续，其他任务可以打开原会话补充输入，用户也可以显式放弃并清除 checkpoint。应用重启后会把 journal 中存在 `RunStarted` 但没有对应 `RunStopped` 的运行补记为 `Interrupted`，再从 `chat-state.json` 重建索引，因此不会因为进程退出或 UI 会话切换而丢失任务入口。

`CopilotAgentTaskHost` 是进程级单活动运行宿主，运行通过稳定 run ID 绑定原会话，并集中持有 cancellation token、类型化 run control、checkpoint-ready 边界和 completion task。宿主保留一个活动槽和最多 3 个等待项；从其他会话提交的 Agent 请求按 FIFO 排队，前一项无论成功、失败或取消都会释放槽并提升下一项。整个 Agent 运行仍然串行，因此多任务不会绕过 capability resource、写操作审批或工具执行闸门。暂停只能在 checkpoint 边界后发生，明确取消可以覆盖待处理的暂停；排队任务可在启动前取消，且不会调用模型或工具。订阅者或某个排队任务异常不会阻断后续任务。

Agent 执行期间允许切换或新建会话，事件和最终结果仍写回启动运行的原消息。同一活动会话中的新输入继续作为 Harness steering，不会误建第二个任务；同一排队会话也不会重复入队。提交时冻结 Profile、附件、活动文档和解决方案路径，任务真正启动时再采集最新设备、流程和应用状态。普通 Chat 不进入后台队列，并继续保持会话切换锁定，因为其历史构造依赖当前选中会话。Host 运行对象只存在于当前进程；满足 `ResumeAfterRestart` 条件的显式排队请求通过持久化恢复记录重新准入，不能继续使用旧运行对象或审批。持续目标和内部自动续作另有重启暂停边界，见[后续队列与恢复](./copilot-agent-session-and-tools.md#任务-ui、停止原因、运行中-steering-与后续队列)。Session checkpoint 和任务索引仍负责已开始任务的长期恢复。标题区只显示一条紧凑的运行或排队状态，不展开调度诊断。

目前所有可写入的审批工具均已收敛到这条协议，注册表中不再保留 `Conditional` 工具：

- `CreateFlow`、`ApplyFlowPatch`、`ExecuteMenu` 和 `SetLanguage` 使用 `Always` 原生审批。
- 模板修改拆成低风险只读的 `TemplatePatch` 预览和高风险非幂等的 `ApplyTemplatePatch` 应用，避免一个工具根据参数临时改变审批语义。
- 工作区文本操作拆成只读的 `PreviewWorkspacePatch` / `PreviewCreateWorkspaceFile`，高风险非幂等的 `ApplyWorkspacePatch` / `ApplyCreateWorkspaceFile`，以及统一的 `RollbackWorkspacePatch`。默认可写根只包含当前解决方案目录；用户显式交给 Copilot 的现有文本文件和当前活动文档可作为精确修改授权，但新文件只能位于解决方案可写根。路径穿越、重解析点、非文本扩展、超过 1 MB 的编码内容、多重替换匹配和创建时覆盖现有路径均被拒绝。
- `SetTheme` 是独立、低风险的明确能力；通用 `ExecuteMenu` 始终受保护，不会因某个菜单项在预检中被判定为低风险而直接执行。

Agent Framework 是唯一运行路径，不再是 Profile 可选项。内置受保护调用经过上述原生精确审批路由，不保证每个调用都创建人工 Pending Action；外部 MCP 继续保留自己的两阶段确认协议，不继承 Agent 的临时 grant 或重试票据。

## 模板 JSON 预览与应用

`TemplatePatch` / `preview_template_patch` 只计算差异，不修改或保存模板。要获得可应用的 `preview_id`，应从活动模板 JSON 编辑器读取当前内容：不传非空 `current_json`，由 `CopilotMcpToolDispatcher` 绑定该编辑器的 `source_id`。显式提供 `current_json` 只供离线预览，不绑定活动编辑器，也不生成可应用预览；`template_identifier` 不是按名称写入任意已保存模板的入口。

`ApplyTemplatePatch` / `apply_template_patch` 必须引用有效预览，并通过所在表面的当前审批链。创建受保护动作前和真正执行时都会复核活动编辑器来源、预览时的原 JSON、补丁 JSON 根对象和敏感字段限制；切换编辑器或内容发生冲突时须重新预览。默认 handler 再通过 `EditTemplateJson.TryApplyCopilotJsonPatchAsync` 定位仍加载的编辑器，在 UI 线程复核当前 JSON 后，只更新 `textEditor.Text`、绑定的 `IEditTemplateJson.JsonValue` 及属性编辑器显示。成功不调用模板 `Save` 或 DAO，不代表数据库已持久化；这是对编辑器及其绑定内存参数的修改，不承诺另有隔离副本或自动回滚。保存仍由模板编辑/持久化流程负责，动态来源生命周期见[业务上下文](./copilot-agent-extensions.md)。

`CopilotSharedCapabilityInputContractTests` 验证预览参数和 `preview_id` 经包装器传给共享能力；`CopilotPlanModeTests` 验证 Plan 模式保留预览而拒绝应用。这些测试不是实际 WPF 编辑器冲突、应用失败、取消或数据库未写入的端到端验证，也不表示本轮已运行测试。

## Flow 图语义与受保护编辑

`.stn` 是带自定义头和 GZip 内容的二进制画布格式，不交给模型按文本读取。`InspectFlowGraph` 从活动编辑器生成 `colorvision.flow-graph.v1`：包含基于节点 Guid 的稳定 instance id、保存时使用的精确 `module|runtime type` 键、结构化输入/输出端口、边、位置和确定性 SHA-256 revision；属性值只有显式请求时才返回并经过脱敏。输出限制最多 200 个节点，避免大型流程无界占用上下文。

`SearchFlowNodeCatalog` 查询当前编辑器实际注册的节点类型，并返回标题、分类、业务节点类型、默认设备 Code 和可写 `STNodeProperty` Schema。例如“添加相机节点”必须先搜索 `相机` / `camera`，由模型在 `CVCameraNode`、`LVCameraNode`、XR/AOI 等真实候选中选择；候选含义不同则询问用户，不能硬编码或猜测类型名。目录按已加载类型签名缓存，插件节点集合变化时自动重建。

写入面收敛为 `PreviewFlowPatch` / `ApplyFlowPatch` 两个工具，而不是为每种动作持续增加工具。每次 patch 只允许一种操作：`add_node` 使用目录返回的精确 type key；`set_property` 使用节点的稳定 ID、目录公开的可写 `propertyName` 和现有 `STNodePropertyDescriptor` 字符串转换，不建立第二套属性转换器，并拒绝密码、token、secret、license 等敏感属性；`connect` 只接受图快照中的 `out:N` / `in:N` 端口 ID，并复用 `CanConnect` / `ConnectOption` 的方向、所有者、锁定、单连接、重复边、数据类型和环路校验。

预览在 UI 线程验证活动流程未运行且 revision 未过期；属性修改在从真实节点持久化数据构造的离图副本上验证，连线只运行无副作用的连接资格检查。`ApplyFlowPatch` 经原生审批后再次检查相同 revision，再执行单项修改；失败时恢复属性旧值、移除已加入节点或断开本次新边。成功也不会自动保存或运行流程。revision 基于每个节点真实 `GetSaveData()` 字节哈希和结构化边生成，复杂 `STNodeProperty` 的变化也会失效旧预览；单节点保存异常时退回稳定的基础节点状态哈希，避免上下文采集整体失败。删除、断连和批量 patch 尚未开放，不能回退到 Shell 或直接改 `.stn` 绕过边界。对应的 `colorvision-flow-authoring` Skill 保持“检查图 -> 查目录/属性/端口 -> 预览单项 patch -> 审批 -> 复核 revision”的短流程，诊断仍由独立的 `colorvision-flow-diagnostics` 负责。
