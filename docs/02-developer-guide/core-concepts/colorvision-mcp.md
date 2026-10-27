---
knowledge_id: "copilot.mcp-server"
knowledge_type: "reference"
status: "current"
summary: "ColorVision 入站本地 MCP 的连接与会话、工具和资源、工作区读取、两阶段确认及菜单写入边界。"
aliases: ["ColorVision 本地 MCP","Local MCP","Codex 如何连接 ColorVision","MCP 工具能否执行流程","CopilotMcpServer","CopilotMcpRequestHandler","CopilotMcpClientSessionStore","CopilotMcpToolDispatcher","Mcp-Session-Id","MCP-Protocol-Version","MCP SSE","MCP 会话容量","-32013","Safety boundary","confirm_action","arguments_digest","get_enabled_tools","get_agent_task_events","agent_task_events_scope_required","workspace_scope_changed","no_allowed_roots","execute_menu","create_flow","Copy Codex Config","Copy Token Command","ListFlowIncidents","list_flow_incidents","inspect_flow_incident","InspectRecordedFlowTemplate","inspect_recorded_flow_template","InspectFlowRunHistory","inspect_flow_run_history","CompareRecordedFlowTemplates","compare_recorded_flow_templates","留存流程配置差异","ReadAlgorithmHelp","read_algorithm_help","InspectFlowRunEvents","inspect_flow_run_events","ValidateFlowGraph","validate_flow_graph","ListFlowRuns","list_flow_runs","InspectFlowRunAttempts","inspect_flow_run_attempts","InspectDeviceServices","inspect_device_services","InspectLoadedBatchResults","inspect_loaded_batch_results"]
code_paths: ["ColorVision/Copilot/Mcp/","ColorVision/Copilot/Capabilities/","ColorVision/Copilot/CopilotExecutionScope.cs","ColorVision/Copilot/Config/CopilotConfig.cs","ColorVision/Copilot/CopilotMcpSettingsControl.xaml","ColorVision/Copilot/CopilotSettingsViewModel.cs","ColorVision/Copilot/CopilotSettingsViewModel.McpProperties.cs","ColorVision/Copilot/CopilotSettingsViewModel.McpOperations.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotListFlowIncidentsTool.cs","Engine/ColorVision.Engine/Services/FlowDiagnostics/FlowIncidentInspectionReader.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectRecordedFlowTemplateTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectFlowRunHistoryTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotCompareRecordedFlowTemplatesTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotReadAlgorithmHelpTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectFlowRunEventsTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotValidateFlowGraphTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotListFlowRunsTool.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectFlowRunAttemptsTool.cs","Engine/ColorVision.Engine/Services/CopilotDeviceServicesReader.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectDeviceServicesTool.cs","Engine/ColorVision.Engine/Dao/CopilotLoadedBatchResultsReader.cs","ColorVision/Copilot/Agent/Tools/Application/CopilotInspectLoadedBatchResultsTool.cs","UI/ColorVision.Common/Interfaces/Copilot/CopilotBusinessContextBuilder.cs"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotMcpServerLifecycleTests.cs","Test/ColorVision.Copilot.Tests/CopilotMcpRequestIdentityTests.cs","Test/ColorVision.Copilot.Tests/CopilotMcpPathSecurityTests.cs","Test/ColorVision.Copilot.Tests/CopilotMcpConfirmationDecisionTests.cs","Test/ColorVision.Copilot.Tests/CopilotMcpConnectionDiagnosticTests.cs","Test/ColorVision.Copilot.Tests/CopilotSharedCapabilityInputContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowIncidentToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowIncidentQueryServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowIncidentQueryToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowIncidentReadSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecordedFlowTemplateSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecordedFlowTemplateServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecordedFlowTemplateToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecordedFlowTemplateGraphTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunHistorySourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunHistoryServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunHistoryToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecordedFlowTemplateComparisonSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecordedFlowTemplateComparisonGraphTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecordedFlowTemplateComparisonToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotRecordedFlowTemplateComparisonServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotAlgorithmHelpReadingServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotAlgorithmHelpToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunEventsToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunEventSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunEventServiceTests.cs","Test/ColorVision.UI.Tests/FlowValidatorTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowGraphValidationToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowGraphValidationServiceTests.cs","Test/ColorVision.UI.Tests/CopilotFlowNodePropertyCatalogTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowNodeCatalogMetadataToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowGraphPaginationToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowGraphInspectionServiceTests.cs","Test/ColorVision.UI.Tests/CopilotFlowParameterIdentityTests.cs","Test/ColorVision.UI.Tests/CopilotFlowDisconnectionHostTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowDisconnectionToolTests.cs","Test/ColorVision.UI.Tests/CopilotFlowDisconnectionTests.cs","Test/ColorVision.UI.Tests/STNodeTransactionalDisconnectionTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunQueryToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunQueryServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunDiscoveryWorkflowTests.cs","Test/ColorVision.Copilot.Tests/CopilotMcpPartialResultContractTests.cs","Test/ColorVision.Copilot.Tests/CopilotSharedCapabilityCatalogTests.cs","Test/ColorVision.Copilot.Tests/CopilotPartialEvidencePresentationTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunAttemptsToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotFlowRunAttemptServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeviceServicesServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeviceServicesToolTests.cs","Test/ColorVision.Copilot.Tests/CopilotDeviceServicesWorkflowTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultsWorkflowTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultsSourceTests.cs","Test/ColorVision.Copilot.Tests/CopilotLoadedBatchResultsServiceTests.cs","Test/ColorVision.Copilot.Tests/CopilotLoadedBatchResultsToolTests.cs","Test/ColorVision.UI.Tests/CopilotLoadedBatchResultReferenceContextTests.cs"]
related: ["copilot.runtime", "copilot.extensions", "copilot.configuration", "copilot.tool-contracts","flow.diagnostics"]
---

# ColorVision 本地 MCP

ColorVision 本地 MCP 让同一台计算机上的 MCP 客户端读取应用上下文、日志、文档和允许范围内的文件，也提供面板导航、主题切换以及需确认的模板、流程和菜单操作。服务默认关闭，启用后仅监听 IPv4 loopback，并要求 Bearer token。连接授权覆盖该服务暴露的工具，并不等于只有读取权限。

本页描述 ColorVision **作为 MCP server 的入站接口**。Copilot 连接其他服务的出站配置见[外部 MCP 工具发现](./copilot-agent-extensions.md#外部-mcp-工具发现)；两者独立启用，不能互相继承审批或会话。

## 启用与连接

前提是 ColorVision 正在运行，客户端支持下述 JSON HTTP 请求和会话握手。启用服务会开始本机监听；复制配置不代表已经连接。

1. 在 Copilot 设置的本地 MCP 区域勾选 **Enable local MCP server**。确认 `Port`、`Endpoint` 和 `Bearer token`，保存设置后应用到监听器。默认端口为 `38473`，有效范围为 `1–65535`；`McpEnabled`、`McpPort`、`McpBearerToken` 保存在 `CopilotConfig`。
2. 使用 **Copy Codex Config** 复制当前草稿端点的配置片段，按客户端的配置入口保存。设置页生成的默认片段如下：

   ```toml
   [mcp_servers.colorvision]
   url = "http://127.0.0.1:38473/mcp"
   bearer_token_env_var = "COLORVISION_MCP_TOKEN"
   ```

3. 让客户端进程取得与已保存设置一致的 token。**Copy Token Command** 复制的是写入 Windows 用户环境变量的命令，不会替你执行。下面仅为占位示例，实际 token 不应写入仓库、问题报告或普通日志：

   ```powershell
   [Environment]::SetEnvironmentVariable('COLORVISION_MCP_TOKEN', '<ColorVision 设置中的 token>', 'User')
   ```

   用户环境变量的修改不会更新已运行进程的环境；重新启动客户端，并确认启动它的进程取得了新值。服务本身不读取 `COLORVISION_MCP_TOKEN`，它核对的是请求头 `Authorization: Bearer <token>` 与 `CopilotConfig` 中的值。
4. 使用 **Test Connection** 检查握手和只读 `get_server_status`，再由外部客户端建立自己的会话。该按钮使用当前草稿端口和 token，不自动保存或启用服务；具体诊断预算与保存边界见[配置与连接诊断](./copilot-configuration.md#诊断与发现的副作用)。

更换端口会重启监听器；同端口更换 token 会清除现有会话。关闭服务、重启应用或会话过期后，客户端需要重新初始化。`Regenerate` 先修改设置草稿，保存后才影响正在运行的服务。

本地 MCP 的启动与 Chat 面板是否显示无关。服务关闭或缺少 token 时不创建请求处理器及其工具元数据；首次启用时先准备处理器，再开始监听。关闭或重启监听器会清理已创建处理器的会话，后续启用复用处理器，不依靠打开 Chat 才能接受外部请求。

## HTTP 协议与会话

端点是 `http://127.0.0.1:<port>/mcp`。当前实现返回 JSON，使用 MCP 会话头；它不是完整的 Streamable HTTP 功能实现，不能据此假定支持 SSE、订阅或流恢复。

| 请求 | 当前行为 |
| --- | --- |
| `POST initialize` | 单独的 JSON-RPC 2.0 请求；不得附带已有 `Mcp-Session-Id`。`params` 需含非空 `protocolVersion`、对象 `capabilities` 以及非空 `clientInfo.name` / `version`；成功从响应头取得随机会话 ID |
| 后续请求 | 携带相同 Bearer token 和 `Mcp-Session-Id`，先发送 `notifications/initialized`。服务支持的协议版本是 `2025-03-26`；后续 `MCP-Protocol-Version` 头可省略，提供时必须与此值一致 |
| JSON-RPC 方法 | `ping`、`tools/list`、`tools/call`、`resources/list`、`resources/read`。初始化后的非空批量请求顺序处理；普通 `notifications/initialized` 返回 HTTP `202` 空正文 |
| `GET /mcp` | 也需要认证、有效会话和匹配的可选版本头，只返回 JSON 状态，不建立 SSE 流 |
| 其他 HTTP 方法 | 有效会话下返回 `405`；没有 `DELETE` 主动关闭会话接口 |
| 响应判定 | HTTP `200` 不等于业务成功。工具结果在 `content[].text` 中，`isError=true` 表示失败或等待确认；资源读取失败返回 JSON-RPC error |

`initialize` 返回固定 `protocolVersion=2025-03-26`，并不要求客户端传入的版本与它相同；客户端应检查协商结果。`serverInfo.version=1.0.0` 是接口声明值，不是 ColorVision 安装版本。服务接收 initialized 通知，但没有将其作为工具调用的独立状态门禁。

会话保存在进程内，最多 `256` 个，空闲 `30` 分钟失效，按请求解析时清理过期项。会话绑定创建它的网络来源；两个同为 `127.0.0.1` 的客户端仍取得不同随机 ID 和派生身份。更换会话不能继续使用旧会话的审批动作，token 相同也不能转移批准。

监听器最多同时处理 `16` 个 TCP 客户端，超额连接直接关闭。请求头上限 `64 KiB`，请求体上限 `1 MiB`，支持 Content-Length 或 chunked 请求体；同一请求同时声明两者会被拒绝。每个连接有 `30` 秒的读取、分派与写回复总预算，响应使用 `Connection: close`。这些连接限制与工具自身超时共同生效；超时、取消或断线不证明已开始的操作没有产生影响。

## 工具与资源

先通过 `tools/list` 获取当前名称与 Schema，或调用 `get_enabled_tools` 查看分类、风险标签及示例。列表表示工具已注册；活动编辑器、工作区、数据库和审批条件仍可能使单次调用不可用。`tools/call.params.arguments` 应为 JSON 对象，字段按所列 Schema 提供。

### 工具用途与效果

| 用途 | 工具 | 结果与边界 |
| --- | --- | --- |
| 服务和运行环境 | `get_server_status`、`get_enabled_tools`、`get_runtime_environment_summary` | 监听器、活动/排队运行数量、运行环境等快照，不输出 token |
| 调用诊断 | `get_audit_log`、`get_audit_summary`、`get_last_tool_error`、`get_diagnostic_bundle` | 当前 MCP 会话的调用审计；诊断包另含应用日志、运行环境、实时上下文和流程摘要，默认最多 `12000` 字符，可设 `1000–60000`，不含 Agent journal |
| 应用与模板上下文 | `get_live_context`、`inspect_device_services`、`inspect_loaded_batch_results`、`get_workspace_context`、`get_active_template_context`、`get_saved_template_context`、`get_template_type_context` | 读取已发布或已加载的内存状态。批次结果仅限已打开并注册的详情页；已保存模板用精确 `template_code` / `template_name` 定位；类型查询只需 code，返回字段结构和已加载名称，不返回参数值，不主动查询数据库 |
| 日志与产品文档 | `get_recent_log`、`search_docs`、`read_algorithm_help` | 应用日志、已发布在线文档索引或随包离线算法原文；在线索引可能联网，不等于当前源码或安装版本 |
| 工作区文件 | `search_files`、`grep_text`、`read_allowed_file`、`list_allowed_directory` | 受允许根约束的搜索、文本读取和目录分页，见下文 |
| 流程诊断 | `get_flow_summary`、`diagnose_flow_failure`、`list_flow_runs`、`list_flow_incidents`、`inspect_flow_incident`、`inspect_flow_run_events`、`inspect_flow_run_attempts`、`inspect_recorded_flow_template`、`inspect_flow_run_history`、`compare_recorded_flow_templates`、`get_flow_graph`、`get_flow_node_catalog`、`preview_flow_action` | 发现运行或事故并读取精确关联的证据、运行事件、留存模板、运行历史或保存差异，或读取当前流程；预览动作只返回说明，即使名称是 `select_node` / `open_node_property` 也不实际选择或打开界面 |
| 当前图静态校验 | `validate_flow_graph` | 以 `get_flow_graph` 的精确 revision 校验完整当前画布；正常返回 `valid=false` 仍是成功交付诊断，不代表可运行、设备就绪或历史运行图有效 |
| 模板建议和预览 | `suggest_template_patch`、`preview_template_patch` | 建议字段或计算差异，不修改编辑器；满足活动编辑器条件的预览会在内存保存 `preview_id` |
| 流程预览和修改 | `preview_flow_patch`、`apply_flow_patch` | 单项 `add_node`、`set_property`、`connect` 或指定已存在边的 `disconnect`；应用需确认且复核 `expected_revision`，成功只修改当前编辑器，不自动保存或运行 |
| 模板应用 | `apply_template_patch` | 需要有效 `preview_id` 和确认；修改活动编辑器及绑定内存参数，不自动保存到数据库 |
| 创建流程 | `create_flow` | 确认后走 `TemplateFlow.Create` 创建空流程，**会写入数据库**并刷新流程列表；不是临时编辑器预览，不运行该流程。未指定名称时由应用生成名称，重名返回 `flow_name_exists` |
| 面板与主题 | `open_panel`、`set_theme` | 直接执行，不创建二次确认。主题切换会更新主题配置并调用配置保存 |
| 菜单与语言 | `execute_menu`、`set_language` | 菜单默认 dry-run，实际执行按匹配菜单的风险分类处理；语言修改需确认，可能触发现有重启提示，见下文 |
| 执行批准 | `confirm_action` | 只消费相同会话、工作区和参数绑定的已批准动作 |
| Agent 事件 | `get_agent_task_events` | 标准 HTTP 外部会话没有 Copilot conversation 绑定，返回 `agent_task_events_scope_required`；工具出现在列表中不代表可以读取进程级任务历史 |

`diagnose_flow_failure` 分开呈现当前流程状态、Flow 宿主筛选出的失败相关摘录、节点／模板配置和最近应用日志，不通过关键词生成故障原因。`FailureEvidence` / `RecentFailureSummary` 也是宿主提供的文本线索，不等于结构化异常或已确认根因；`Running` 状态、`Timeout` 等配置字段及日志搜索词本身不构成故障证据。各来源独立采集，最近日志尚未按运行批次或节点关联，必须先对齐原始记录中的流程／批次、节点与时间，再提出参数调整。输出中的日志行数是请求上限；来源缺失或日志读取失败会明确报告，不能解释为流程成功。`CopilotFlowDiagnosisTests` 覆盖正常配置、真实错误摘录、旧摘要兼容和日志读取失败的区分。

`inspect_device_services` 是一个已加载设备目录／精确对象只读入口。省略 `device_ref` 读取目录，`max_devices` 默认40、范围1–100，原样续传不超过4,096字符的 `cursor`；focus 须使用返回的32hex对象引用和宿主标识（作为 `expected_source_instance_id`），不能与 cursor 并用。游标绑定宿主和成员对象集合，按实际交付末行续读；名称、资源编号或当前选择不作回退。输出最多18,000字符完整 JSON，只有资源 metadata 与可信内置缓存状态，未知插件为 metadata-only unsupported。缓存状态不是在线／健康、心跳或动作完成保证；源在排队前绑定且 UI 采集前后复核，不初始化管理器、读取配置、创建界面或探测／控制设备。

`inspect_loaded_batch_results` 优先使用上下文已交付的完整有效32hex `source_ref`／`expected_source_instance_id` 对，否则先分页列出已打开并注册的批次详情页来源，再按精确绑定读取该页内存主记录；可加同源 `result_ref` 精确聚焦，不能与 cursor 并用。历史附件引用仍需正式核验，失效后重发现，不按批次 ID 回退。`max_items` 默认40、范围1–100，cursor 最多4,096字符并原样续传；目录和记录分别绑定来源及对象集合，最多18,000字符完整 JSON 按实际交付末行推进。它不按当前选择猜批次，不查询未打开的历史结果，不加载明细或图片；结果码、耗时和时间仅为已有 metadata，未知码不代表成功或客户 PASS／FAIL，不交付路径、SN、原始消息、参数、测量值或文件存在判断。

理解算法原理或参数含义时，先从 `get_flow_node_catalog` 的 `algorithmHelpTopicId` 取得精确 `topic_id`，再调用 `read_algorithm_help`。工具只读随包同源 Markdown，不联网或执行图示；`max_characters` 默认6,000、范围256–12,000，正文最多18,000字符完整 JSON，按 `has_more`／`next_cursor` 原样续读。文本改变须重读，静态帮助不代表当前参数值、测量或验收结果；当前值仍从 `get_flow_graph` 显式查询。来源、完整性与分页规则见[节点帮助与正式读取契约](../../04-api-reference/extensions/flow-node.md)。

节点参数取值先看 `get_flow_node_catalog` 返回的 `descriptorType`、精确 `declaredEnumNames`、`isFlagsEnum` 和 `booleanStringCandidates`，同时保留 `dataType`／`isWritable` 的区分。声明名称与候选不等于完整业务合法域，空候选不表示只读；自定义 descriptor 的未知格式仍通过 `preview_flow_patch` 核验，不从描述推导数字约束。直接 enum、标准 bool、自定义 descriptor 和 nullable 的边界见 [Flow 图参数契约](./copilot-agent-tool-contracts.md#flow-图语义与受保护编辑)。

`get_flow_graph` 的 `section=nodes`（默认）、`edges`、`ports`、`properties` 是独立分页流。节点概览的内部边、端口和参数附带明细均有界并说明省略，不能据节点流结束宣称其余流完整。拓扑读取先续完节点，再用返回的 `revision`／`sourceInstanceId` 分别作为 `expected_revision`／`expected_source_instance_id` 开始并续完边流；合并端口或参数明细也应在首个独立请求传回双绑定。节点／边上限各80、明细 `max_items` 默认80，均为1–200；`properties` 流须显式 `include_properties=true`。原样续传各流 `cursor`，正文最多18,000字符，只按实际交付行推进；参数值仍脱敏及最多512字符，展示标签不能代替目录的可写 `propertyName`。完整焦点、来源／版本绑定及实时状态边界见 [Flow 图读取契约](./copilot-agent-tool-contracts.md#flow-图语义与受保护编辑)。

当前整图静态检查使用 `validate_flow_graph`，必填 `expected_revision` 须原样传回 `get_flow_graph` 给出的64个十六进制字符。只在 revision 匹配时调用宿主既有校验，过期、无编辑器或异常明确失败；检查完成但 `valid=false` 是正常诊断结果。工具不弹窗、聚焦、保存或运行流程；规则、执行活动状态及编辑前后检查建议见 [Flow 图语义与受保护编辑](./copilot-agent-tool-contracts.md#flow-图语义与受保护编辑)。

断连通过现有 patch 工具的 `operation=disconnect` 指定已存在边的 source／target Guid `D` 实例 ID 和 `out:N`／`in:N` 四元组，预览及审批明确展示目标；缺边失败，不支持整个端口或批量断开。改线须先审批断开，重新读图后再独立预览并审批连接，两步没有原子批量承诺；断开后的静态 `valid=false` 不授权自动修图、保存或运行。完整守卫与工作流见 [Flow 图编辑契约](./copilot-agent-tool-contracts.md#flow-图语义与受保护编辑)。

没有精确运行编号时，先用 `list_flow_runs` 发现留存运行，普通或慢运行不需要存在事故。省略 `flow_key` 时包含全部留存及 legacy／未知身份记录；可选1–256字符的非空白完整键按 Unicode Trim 后大小写敏感精确匹配，不按名称或前缀猜测。`max_results` 默认20、范围1–100；原样续传不超过4,096字符的 opaque `cursor`，规范筛选不变时可调整页大小。ID倒序不是执行时间顺序，分页是实时只读快照而非完整历史；18,000字符预算只按实际交付行推进。返回的精确 `run_record_id` 可直接交给历史、事件或留存模板工具，hash 格式不表示模板字节已验证；缺库／schema 不是无运行。

不知道事故编号时，先调用 `list_flow_incidents`：`state` 默认 `active`，可选 `active/open/acknowledged/resolved/all`；`max_results` 默认20，范围1–100，续页原样回传不超过4,096字符的 opaque `cursor`。再用列表中的完整 `incident_id` 调用 `inspect_flow_incident`，读取关联运行、节点尝试与按序事件。两者仅只读发现与取证，不执行控制、处置或写库；缺库或缺 schema 不能解释为无事故。

事故列表每页有独立只读快照，跨页是实时 keyset 查询，不是冻结的完整历史。游标绑定数据源路径哈希、状态、锚点及日期原始排序值的指纹；换来源／状态、锚点删除、时间或其原始存储表示改变后需重新查询，即使解析后的 ticks 相同。正文保持有界完整 JSON；为输出预算缩页时，以实际交付的末条记录生成下一游标。状态的精确语义、来源和覆盖限制、当前事故窗口入口及详情输入／输出上限统一见 [Flow 运行诊断](../../04-api-reference/engine-components/flow-diagnostics.md)。当前快照和未关联日志不能替代正式证据。

详情事件被截断或需要更早记录时，使用运行列表、事故详情、已读历史或用户提供的精确 `run_record_id` 调用 `inspect_flow_run_events`，不从当前画布猜 ID。`max_events` 默认40、范围1–100；从最新窗口向更早事件原样续传不超过4,096字符的 `cursor`，各页内按序号／ID升序交付。18,000字符预算缩页后以实际交付的最早事件续读；游标绑定来源、运行及锚点，失效需重查。空事件页与缺库／schema／运行失败明确区分，不读 SN、操作员或 `data_json`，不执行或写库；完整排序、实时分页及证据边界见 [Flow 运行诊断](../../04-api-reference/engine-components/flow-diagnostics.md)。

需要事故／历史摘要之外的节点尝试详情时，按需用 `inspect_flow_run_attempts`：必填精确正 Int32 `run_record_id`，可选1–128字符的非空白精确 `node_id` 不 trim、不限 GUID，`max_attempts` 默认40、范围1–100。最新 attempt-ID 窗口按 ID升序交付，原样续传不超过4,096字符的 `cursor` 读取更早记录；18,000字符预算只按实际交付的最早行推进，不去重重复尝试编号。输出保留完整 Int64 attempt ID、记录结果、有效时间差及有界脱敏错误；截断 Outcome 不补为已知成功，失败 attempt 不证明运行停止或 ContinueOnFail。每页为实时留存窗口，缺库／schema／运行与合法空页明确区分。

历史模板取证使用 `inspect_recorded_flow_template`：将用户、运行列表或事故详情给出的精确 `run_record_id`（必填正 Int32）传入，`max_nodes` 默认80、范围1–200；可选 `node_id` 必须是长度36的非零 GUID `D` 格式。工具只读取该运行精确关联且通过长度／hash 校验的留存模板，交付有界完整 JSON 的节点身份、属性名与可验证连线，不返回属性值、不实例化节点或执行流程。缺引用、缺表、损坏或不支持格式会明确失败；缺端口布局或被裁剪时说明覆盖限制，不猜测连接。`get_flow_graph` 仍读取当前画布，留存模板也不保证包含未保存修改；完整关联、格式和预算规则见 [Flow 运行诊断](../../04-api-reference/engine-components/flow-diagnostics.md)。

历史状态／耗时对照使用 `inspect_flow_run_history`：必填精确正 Int32 `run_record_id`，`max_runs` 默认10、范围1–50；可选 `node_id` 为1–128字符的非空白精确标识，不限 GUID、不 trim 或模糊匹配。工具仅交付有界先前记录及可用的节点尝试，不读 SN、操作员或 payload。窗口按更小记录 ID 取样，不承诺实际执行先后；缺身份、缺尝试 schema 或裁剪会明确说明。确认成功参照与差值仅基于符合条件的实际交付记录，不能解释为全库成功率或客户 PASS/FAIL；完整身份、统计与覆盖规则见 [Flow 运行诊断](../../04-api-reference/engine-components/flow-diagnostics.md)。

留存配置差异使用 `compare_recorded_flow_templates`：从运行列表、已读历史或用户输入给出精确正 Int32 `baseline_run_record_id` 与 `current_run_record_id`，`max_changes` 默认50、范围1–200，是每页节点及增删边明细的合计上限，续页可调整。可选 `cursor` 为不超过4,096字符的非空 opaque 游标；当 `has_more=true` 时按需原样传回 `next_cursor` 并保持两个 ID 及顺序，首段不能当作全部。工具每次在同一读取快照中验证两侧精确引用，只比较相同完整 FlowKey 或明确标记的 legacy 弱身份，不使用当前画布回退；游标绑定来源路径哈希、运行／快照引用、校验 hash、身份依据类型及偏移，绑定改变或合法游标依赖的运行／快照被删除时返回 `Validation` 并须从首段重查；首次查询缺失仍为 `NotFound`，不承诺跨页冻结源或全部展示 metadata。正文保留完整差异计数和全局 flags，明细受行数及18,000字符预算限制且只按实际交付行续读；不交付字段值、字段 hash 或原始 STN，缺布局的连接不猜测。差异仅针对留存数据，不能直接证明执行画布、根因或执行分支；完整预算、失败分类及解释边界见 [Flow 运行诊断](../../04-api-reference/engine-components/flow-diagnostics.md)。

### 只读资源

每个 URI 都使用完整的 `colorvision://` 前缀。资源返回请求时的快照，不订阅后续变化；读取本身也会形成调用审计。

| URI | 内容 |
| --- | --- |
| `colorvision://live-context/current` | 当前已发布业务上下文 |
| `colorvision://workspace/current` | 解决方案目录、活动文档和允许搜索根 |
| `colorvision://logs/recent` | 应用近期日志 |
| `colorvision://template/current` | 活动模板 JSON 编辑器上下文 |
| `colorvision://flow/current` | 活动流程与节点摘要 |
| `colorvision://mcp/audit-summary` | 当前会话审计、失败及待批准计数 |
| `colorvision://mcp/audit-log` | 当前会话近期调用记录 |
| `colorvision://copilot/capabilities` | JSON 能力目录，包含内置及已发现的 Copilot 能力元数据；不是本入站服务 `tools/list` 的同义列表，也不会自动把所有能力变成可调用 MCP 工具 |
| `colorvision://copilot/task-events` | JSON Agent journal 入口；与同名用途工具一样，标准外部会话因未绑定 conversation 而拒绝读取 |

业务上下文是已发布的结构化文本，不包含图像像素，也不代表每次读取都会重新采集设备。发布和刷新机制见[业务上下文](./copilot-agent-extensions.md#业务模块-agent-扩展)。Agent journal 的保存、过滤和有界查询见[结构化任务事件](./copilot-agent-tool-contracts.md#结构化任务事件-journal)。

## 工作区读取范围

`get_workspace_context` 显示的是 **ColorVision 当前解决方案**与活动文档，不是外部客户端自己的工作目录。默认允许根来自现有解决方案目录，以及活动文档所指目录或文件的父目录；活动文档可能使解决方案外的目录也成为允许根。没有有效根时返回 `no_allowed_roots`，不会回退到任意磁盘位置。

- 相对路径以第一个允许根解析，不会逐个根尝试；需要访问其他允许根时传其完整路径。路径规范化后必须仍位于允许范围，重解析点路径会被拒绝。
- `search_files` 查字面文件名/路径片段；`grep_text` 查不区分大小写的单行字面文本，不执行正则或 Shell。分页 `cursor` 是不透明续页值，保持原查询和范围后原样传回。
- `read_allowed_file` 必须明确给出单一 `path`，只读取支持的文本扩展名，可按 Schema 给出行列范围；不能用它读 `.stn`、图片或任意二进制。`list_allowed_directory` 不传 path 时列允许根，续页时需要明确目录。
- 文件调用会重查当前工作区。一次调用内解决方案发生切换时返回 `workspace_scope_changed`，需在新上下文重新查询。UI 线程繁忙导致默认 `2` 秒内未取得工作区快照时，可能返回空范围，而不是证明磁盘上没有文件。

这些根限制针对文件工具。应用日志、已加载模板和实时上下文各有自己的来源，不能把“受限文件读取”理解为所有返回内容都来自解决方案目录。

## 应用操作与两阶段确认

### 面板、主题和菜单

`open_panel` 只接受别名：`copilot` 打开 Copilot，`log` 打开日志，`solution` / `config` / `template` 都显示 `ProjectPanel`，`device` 显示 `AcquirePanel`。它们不是打开任意窗口或设备指令；面板未注册时返回 `panel_not_registered`。`set_theme` 直接修改并保存主题配置，不能把整个 MCP 服务描述为“不修改配置”。

`execute_menu` 的 `dry_run` 默认为 `true`，返回匹配菜单、候选、可执行状态和风险，不执行命令。`dry_run=false` 时，低风险菜单可直接调度；需要确认的菜单创建待批准动作，批准后通过 `confirm_action` 调度。无法唯一匹配、匹配不够明确或 `CanExecute` 为 false 时不会调度。

菜单来自当前可见的主窗口/全局菜单，风险按标题、路径、GUID 和类型名称中的词语分类，未知类别默认需确认。这是通用菜单调用，**没有逐项禁止设备控制、流程运行、文件写入或删除的白名单**。确认绑定的是查询参数，执行时还会重新解析当前菜单；返回 `scheduled` 仅表示安排了 UI 命令，不能当作菜单业务已成功完成。需要评估具体命令效果及当前界面结果，不能由风险标签推断绝无副作用。

专用流程诊断和 `preview_flow_action` 拒绝 start/stop/run，图补丁也不运行流程；这些限制不能推广为通用菜单绝不会触发流程。接口没有接受任意 Shell/PowerShell/Python 文本的专用命令工具。

### 从预览到执行

先读状态与工作区，再取得相关上下文和预览。模板与 Flow 使用不同的预览凭据：

| 操作 | 调用顺序与条件 |
| --- | --- |
| 模板 JSON | 可先用 `suggest_template_patch`，然后 `preview_template_patch` → `apply_template_patch { preview_id }` → ColorVision 批准 → `confirm_action`。预览默认有效 `10` 分钟；只有绑定活动编辑器的预览能应用，显式传非空 `current_json` 仅供离线预览 |
| Flow 图 | `get_flow_graph` 取得 revision，可先 `validate_flow_graph { expected_revision }`；按需要查询 `get_flow_node_catalog`；`preview_flow_patch` → `apply_flow_patch` → 批准 → `confirm_action`。应用后重新读图，再以新 revision 静态校验。apply 使用操作参数和 `expected_revision`，会自行重新预览，不接收模板的 `preview_id`；显式继承相机设置时还必须传回预览的 `camera_defaults_fingerprint` |
| 语言、创建流程、需确认菜单 | 调用目标工具取得待批准动作 → 在 ColorVision 核对效果并批准 → `confirm_action` |

模板冲突校验、内存修改与保存边界见[模板 JSON 预览与应用](./copilot-agent-tool-contracts.md#模板-json-预览与应用)；Flow 节点类型、属性、端口和 revision 契约见[Flow 图语义与受保护编辑](./copilot-agent-tool-contracts.md#flow-图语义与受保护编辑)。预览是审查数据，不是授权，也不能跨表面复用批准。

待确认响应的错误码为 `confirmation_required`，风险值为 `confirmation-required`，并含 `action_id`、`tool_name`、`arguments_summary`、`arguments_digest` 与 `expires_at`。它表示等待用户，不是操作已失败执行或已完成。

1. 保留原始响应，在 ColorVision 的 Pending Actions 区域审阅来源、工作区、影响和参数后批准。外部客户端的自然语言“已同意”或本地客户端自己的确认不代替 ColorVision 批准。
2. 在原会话中调用 `confirm_action`，原样提交 `action_id`、`tool_name`、`arguments_digest`。摘要只用于展示，不能替代完整规范化参数的 SHA-256 绑定。
3. 核对工具结果与实际应用状态。动作默认从创建起有效 `5` 分钟，批准不延长有效期；执行前重查工具名、参数指纹、调用方、工作区、作用域和状态。执行中的或已执行动作不能重放，即使执行结果失败也不能重新消费同一动作。

会话过期、工作区切换、动作过期、模板内容变化或 Flow revision 变化后，需要重新读取、预览并取得新批准。传输中断或超时时先检查应用状态，不盲目重试写入。内置 Agent 使用自己的原生审批恢复链，详见[工具审批契约](./copilot-agent-tool-contracts.md#原生审批与参数快照)。

## 审计、输入与错误处理

外部会话的审计、审计摘要、最近工具失败和待审批计数按派生 session identity 隔离；通用审计不返回可复用 action ID 或参数指纹。应用日志和业务上下文仍是应用级数据，不因审计隔离就变成客户端专属数据。

原始 session ID 不写入普通审计；token、密码、Authorization 等采用字段和文本规则脱敏，检测到敏感确认参数时会拒绝创建动作。此类规则不能保证识别任意形式的秘密，不应主动将凭据放进查询、模板值或诊断文本。

Dispatcher 在处理器和审批创建前按 Schema 校验对象参数；非法嵌套字段、类型、必填项、范围和枚举返回 `invalid_arguments`。HTTP 包装层存在另一个边界：缺少或非对象 `arguments` 会先转为空对象，大小写冲突的重复键可能使连接因解析异常关闭。因此客户端应发送无重复字段的对象，不能依赖所有畸形 HTTP 输入都得到同一种 Schema 错误。

HTTP 工具回复仍只投影 `content[].text` 与 `isError`，不单独序列化内部 `ErrorCode`、`FailureKind` 或 `PartialResultMessage`。覆盖提示仅沿原生共享 App 调用链传递到既有 observation、快照与活动记录；业务 JSON 正文不因提示而拼接额外摘要，外部客户端仍按各工具原正文中的覆盖范围及游标解释结果，不能假定新增了结构化 partial 字段。资源失败统一使用 JSON-RPC `-32002` 并附说明。下表的工具错误码用于对应源码和审计诊断，不能假定回复中必有 `errorCode` 字段。需要进一步定位时，可在同一会话读取 `get_last_tool_error` 或 `get_audit_log`。

| 现象或错误 | 检查与处理 |
| --- | --- |
| 连接拒绝、MCP disabled、端口不可用 | 确认应用仍在运行、设置已保存、端口未占用；监听启动失败时读设置页状态。关闭监听时可能直接连接失败，不一定有 HTTP 错误体 |
| `401` | 核对请求 Bearer token 与已保存 token；检查客户端是否仍持有旧环境值 |
| `400 / -32010`、`404 / -32011` | 前者缺少会话头；后者会话未知、过期或来源不符。重新单独 initialize，不要在新握手上附旧会话头 |
| `400 / -32014` | 后续版本头不受支持；检查 initialize 返回的 `2025-03-26` |
| `503 / -32013` | 会话容量已满；复用已有会话，或等待过期。频繁 Test Connection 也会创建新会话 |
| `tool_not_found`、`invalid_arguments` | 重新读取 `tools/list`，使用正式工具名、对象参数与当前 Schema |
| `no_allowed_roots`、`workspace_scope_changed`、`path_not_allowed` | 查 `get_workspace_context`，确认活动解决方案、根目录、路径和 UI 响应；不要扩大到任意路径绕过限制 |
| `agent_task_events_scope_required` | 普通外部会话未绑定 Copilot conversation；不能通过传一个 conversation 参数取得权限，应在应用内查看该任务诊断 |
| `confirmation_required`、`action_pending` | 到 ColorVision 审阅并批准，再从原会话 confirm；反复提交确认不会代替用户批准 |
| `action_source_mismatch`、`action_workspace_mismatch`、`action_scope_mismatch`、`action_arguments_mismatch` | 旧动作与当前调用不匹配，不能替换 digest 或迁移批准；在正确上下文重新提出动作 |
| `action_expired`、`action_already_executed` | 过期需重新申请；已执行先核实实际结果，避免重复副作用 |
| `template_patch_preview_required`、`template_patch_conflict`、`template_context_mismatch` | 打开目标 JSON 编辑器重新预览；离线 JSON 预览不授予编辑器写入权限 |
| `flow_execution_not_supported`、`flow_patch_failed` | 前者属于专用诊断预览的运行限制；后者按返回信息核对活动编辑器、运行状态、revision、类型/属性/端口 |
| `tool_execution_timeout`、连接在响应前关闭 | 调用超时或取消可能留下已完成、仍在进行或未知结果；检查实际状态后再决定是否重试 |

## 实现与验证入口

| 责任 | 入口 |
| --- | --- |
| 监听、HTTP、握手及会话 | `CopilotMcpServer`、`CopilotMcpRequestHandler`、`CopilotMcpClientSessionStore` |
| 工具与资源列表、Schema、调用和审计 | `CopilotMcpToolDispatcher` 的 `Descriptors`、`Resources`、`Invocation`、`Diagnostics` 分部 |
| 工作区捕获与路径授权 | `CopilotMcpToolEnvironment`、`CopilotMcpWorkspaceSnapshotCapture`、dispatcher `Search` 分部 |
| 确认和执行绑定 | `CopilotExecutionScope`、`CopilotMcpConfirmationStore`、dispatcher `Confirmation` 分部 |
| 菜单解析与真实副作用 | `CopilotMenuToolSupport`、`CopilotApplicationCapability.ExecuteMenuAsync` 与具体菜单命令 |
| 正式事故发现与详情 | `CopilotFlowIncidentQueryService`、`CopilotFlowIncidentInspectionService` 与只读 `FlowIncidentInspectionReader` |

共享工具的定义、策略、路由、Schema 一致性和 Agent trace 由[共享能力契约](./copilot-agent-tool-contracts.md#agent-与-mcp-的共享能力定义)统一维护；新增本地工具仍需在 `CopilotMcpToolDefinition` 集合绑定 descriptor 与 handler。新增业务快照按[业务上下文扩展](./copilot-agent-extensions.md#业务模块-agent-扩展)接入，不另建 MCP 专属事实副本。

相关测试包含：`CopilotMcpRequestIdentityTests` 的认证、会话隔离和事件拒绝；`CopilotMcpConfirmationDecisionTests` 的作用域、完整参数绑定和审计载荷；`CopilotMcpServerLifecycleTests` 的退出取消；`CopilotMcpConnectionDiagnosticTests` 的握手及状态响应校验。`CopilotFlowIncidentQueryToolTests`、`CopilotFlowIncidentToolTests` 检查事故发现／详情的 Agent 与 MCP 同协议，分页和只读源覆盖见 [Flow 运行诊断](../../04-api-reference/engine-components/flow-diagnostics.md)。共享输入测试见 `CopilotSharedCapabilityInputContractTests`。

下面是实现变更后的可选验证命令，会构建测试依赖，部分用例会启动临时 loopback 监听器、WPF Dispatcher 并创建临时文件；文档修改不要求为验证文字而启动服务：

```powershell
dotnet test .\Test\ColorVision.Copilot.Tests\ColorVision.Copilot.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~CopilotMcp|FullyQualifiedName~CopilotApprovalReview" -v minimal
```

这些测试不等于真实客户端互通、所有菜单风险分类、数据库写入或设备行为的端到端验收。网站与检索通过也只验证文档结构和可发现性。
