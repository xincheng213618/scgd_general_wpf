# Copilot 使用指南

ColorVision Copilot 用来解释当前界面信息、辅助排查流程和设备问题，并在用户明确要求时协助调整模板或工作区文件。它不会自行扩大文件范围、启动流程或控制设备；固定只读机器诊断可以直接执行，受保护操作默认逐次确认，也可以由用户为下一任务或当前任务显式启用“临时自动复核”。输入 `/help` 可以随时查看固定命令目录，输入 `/help <命令>` 可以查询具体用法和 Agent 运行中是否可执行；在 `/help`、`/permissions`、`/usage`、`/diff`、`/goal`、`/mcp`、`/model`、`/reasoning`、`/effort` 或 `/personality` 后输入空格，还会显示可用参数并随输入过滤。候选打开时可用 ↑↓ 环绕选择，Tab 或点击只补全选中项；Enter 会补全并执行终结项，`/goal edit` 这类仍需正文的中间项则只补全并继续等待输入。Agent 运行中，候选只显示当前可立即执行的固定命令以及可作为输入提交的 Skill；正在回答 Agent 问题或查看排队任务时不会弹出命令候选，避免把答案或不可发送的草稿误当成命令。

## 首次配置

1. 打开主界面右侧的 **Copilot** 面板。
2. 进入模型设置，选择服务商并填写 API 地址、模型和 API Key。
3. 使用连接测试确认配置可用。
4. Local MCP 仅供 Codex 等本机客户端读取 ColorVision 上下文；不使用时保持关闭。每个客户端在 `initialize` 后获得独立的进程内会话，同一台机器上的另一个客户端不能复用它创建的审批。需要让 Copilot 使用其他 MCP 服务时，在同一设置页配置 `Copilot MCP clients`。

API Key 和 MCP token 保存在本机加密配置中。发送问题前，应确认当前附加的上下文不包含不希望交给模型的数据。

## 输入区引用与访问模式

在输入框中键入 `@` 可以搜索当前 ColorVision 模板、应用菜单和解决方案文件；也可以点击左下角 `+`，选择 **关联模板、菜单或文件（@）**，在当前光标位置打开同一候选目录。输入 `/mention` 或 `/mention 查询` 也会把命令转换为未闭合的 `@` 查询并打开这份目录；它不会自动选择、附加、读取或发送候选，Agent 运行中需等待当前任务结束后再使用。继续输入会过滤候选，使用 ↑/↓ 选择，Enter 或 Tab 关联，Esc 关闭候选。关联文件会加入本轮真实附件；关联模板或菜单会加入带来源标识的结构化上下文。已保存模板开放 `InspectSavedTemplate`（本机 MCP 名称为 `get_saved_template_context`）只读工具，从已加载集合读取精确 code 和保存名称对应的有界脱敏快照；模板类型引用开放 `InspectTemplateType`（`get_template_type_context`），只返回类型身份、已加载保存名称和参数字段 Schema，不读取参数值。两者都不查询数据库、不修改或保存模板。菜单引用携带稳定菜单 ID（没有 ID 时使用完整路径），后续明确要求执行时 `ExecuteMenu` 使用该 selector，避免按显示标题再次模糊匹配；引用本身不会触发执行，执行仍遵守当前审批策略。打开项目或解决方案后，还可以输入 `/init` 为尚无共享项目指令的根目录生成 `AGENTS.md`：Copilot 会先检查 `AGENTS.override.md`、`AGENTS.md`、`CLAUDE.md` 和 `.claude/CLAUDE.md`，任一项已存在（包括空文件）便只提示现有路径，不覆盖或另建会遮蔽它的文件；检查通过后，Agent 才读取项目结构及相关开发文档，并提出只含一个新增 `AGENTS.md` 的补丁。预览不写文件，应用仍遵守当前原生审批；初始化不会编译、测试或修改其他文件，生成结果应由开发人员审阅并继续精炼。

输入框左下角的盾牌按钮按会话选择 **按需确认** 或 **临时自动复核**。输入 `/permissions` 会打开同一个菜单；`/permissions status` 显示当前文件范围、能力和审批策略；`/permissions ask` 恢复按需确认；`/permissions auto` 启用现有的临时自动复核：

- **按需确认** 是默认状态。受保护操作生成 Action review，用户批准后才继续同一个 Agent Session。
- **临时自动复核** 最长 15 分钟，只绑定下一任务或当前任务及启用时的工作区。已预览的工作区补丁及回滚继续按逐文件路径和 SHA-256 的确定性规则复核；其他受保护调用只有在提供完整原生审批详情时才交给独立、无工具的权限模型，且只有 LOW/MEDIUM 风险可以自动批准。HIGH/CRITICAL、详情缺失或过长、格式错误、超时或模型失败仍逐次确认。切换模式不会自动批准已经在等待的操作。
- 临时授权不写入会话状态。任务结束、取消、失败、超时、工作区变化或应用重启都会恢复“按需确认”；新建会话和 `/fork`、`/branch` 创建的分支也不会继承授权。

## 数学公式

Copilot 回复支持 LaTeX 数学公式。行内公式可使用 `$...$` 或 `\\(...\\)`，独立公式可使用 `$$...$$` 或 `\\[...\\]`；跨行独立公式也会正常排版。代码片段中的美元符号不会被当作公式，无法解析的公式会保留原始文本，避免隐藏模型回复。

## Agent 运行与常用命令

Copilot 由 Agent Framework 组织多轮推理和结构化工具调用；模型按问题选择固定只读诊断、文件或网页调查、数据库和受保护工具，普通概念问答不会被关键词强制执行工具。工具过程默认显示紧凑活动行，展开后再查看参数、资源键、耗时和脱敏诊断。

受保护调用会暂停在原生审查窗口，批准后只执行当前精确调用。停止、连接中断或非完成终态会保留已显示正文、任务账本和可恢复检查点；“重试最终回答”只根据已有上下文和证据补写回答，不重新调用工具或复用审批。

外部 MCP 服务按 `名称 | Streamable HTTP 地址 | token 环境变量 | 默认策略 | 可选工具白名单` 配置，例如 `lab | https://mcp.example.com/mcp | LAB_MCP_TOKEN | approval | get_status=read-only,apply_patch=approval`。远程地址使用 HTTPS（本机 loopback 可用 HTTP），token 只从环境变量读取；默认使用 `approval`，只把明确可信的只读工具标成 `read-only`。

### 常用命令

| 场景 | 入口与行为 |
| --- | --- |
| 任务与队列 | `/tasks`、`/queue` 查看状态；`/plan [任务]`、`/view-plan`、`/goal` 管理任务；`/approve [N]` 只打开原生审查窗口 |
| 运行中输入 | Enter 或 `↳` 转向当前任务，Tab 或 `⇥` 排到下一轮；方形停止按钮立即取消 |
| 会话导航 | `/resume [会话]`、`/rename [名称]`、`/fork 尝试另一套标定方案`（兼容 `/branch`）、`/clear [名称]`（兼容 `/new`）和标题栏“会话树” |
| 回顾与显示 | `/recap`、`/transcript [expand\|collapse]`、`/turn [N]`、`/shortcuts`；`Ctrl+T` 切换任务明细，`Ctrl+E` 打开本机多行编辑器 |
| 查找与回溯 | `/find [文本]` 或 `Ctrl+F` 查当前会话，`/history` 或输入框 `Ctrl+R` 找历史请求，`/rewind [N]` 建会话分支，`Ctrl+S` 暂存或恢复草稿 |
| 上下文与项目 | `/mention [查询]`、`/init`、`/compact [聚焦要求]`、`/context` 和按需加载的 Skills |
| 内容出口 | `/copy [N]` 或 `Ctrl+O` 复制回答；`/export inspection-report.md` 导出可见会话；`/feedback [说明]` 打开既有反馈窗口 |
| 用量与模型 | `/usage [session\|daily\|weekly\|cumulative]`（兼容 `/stats`）；`/model`、`/reasoning high`（兼容 `/effort`）和 `/personality` |
| 本地诊断 | `/doctor`、`/mcp [verbose]`、`/hooks`、`/skills`、`/status`、`/diff`、`/permissions [status\|ask\|auto]` |

### 实现说明

- 会话回顾、计划定位、任务面板和快捷键见 [本地交互与快捷键](../02-developer-guide/core-concepts/copilot-local-interactions.md)。
- 工具选择、网页证据和子 Agent 见 [执行链](../02-developer-guide/core-concepts/copilot-agent-execution.md)，检查点与重试见 [任务、恢复与内置工具](../02-developer-guide/core-concepts/copilot-agent-session-and-tools.md)。
- 上下文、预算和 Skills 见 [生命周期](../02-developer-guide/core-concepts/copilot-agent-lifecycle.md)，外部 MCP 与 Hook 见 [扩展机制](../02-developer-guide/core-concepts/copilot-agent-extensions.md)。
- 受保护修改、任务事件和恢复契约见 [工具契约](../02-developer-guide/core-concepts/copilot-agent-tool-contracts.md)。

## 桌面宠物联动

启用桌面宠物的“关联 Copilot”后，宠物会聚合多个 Copilot 会话的活动，而不是只跟随最后一次收到事件的会话。状态优先级为“需要输入 → 任务受阻 → 已完成待查看 → 运行中”：因此一个会话等待审批时，不会被另一个刚完成或仍在运行的会话遮住；某个排队任务被取消时，也不会把点击目标错误地切换到已取消的会话。宠物徽标在有多项活动时显示数量；单击宠物会打开当前最高优先级的会话，右键菜单中的 **Copilot 活动** 可以选择其他会话。打开“待查看”或“任务受阻”的会话后，该条未读活动会消失并自动展示下一项；暂停或等待审批的会话会保留，直到继续任务或完成审批。待确认卡仍可直接批准、拒绝或打开 Copilot 面板。这一交互采用 [Codex Pets](https://learn.chatgpt.com/docs/pets?surface=app) 的多聊天状态顺序和活动选择原则，但审批与会话数据只来自 ColorVision 自己的 `CopilotAgentTaskHost` 与 `CopilotMcpConfirmationStore`。

## 常用入口

| 场景 | 操作入口 | Copilot 收到的内容 |
| --- | --- | --- |
| 流程失败 | 流程界面右键，选择“问 AI 分析当前流程” | 流程状态、选中节点、节点参数、最近失败证据 |
| 设备异常 | 设备服务右键，选择“问 Copilot” | 在线状态、心跳、服务类型和脱敏后的配置摘要 |
| 图像分析 | 图像编辑器右键，选择“问 AI 分析当前图像” | 图像元数据、ROI 和标注摘要，不包含图像像素 |
| 模板检查 | 模板 JSON 编辑器中的 Copilot 操作 | 当前模板 JSON、校验状态和未保存状态 |
| 日志排查 | Copilot 诊断模式 | 最近日志中的相关行 |

## 流程诊断结果

流程诊断会优先给出：

1. 当前状态和重点节点。
2. 来自快照与日志的证据。
3. 按可能性排序的原因。
4. 从只读检查开始的验证步骤。
5. 有证据支持的模板字段调整候选。
6. 风险和需要人工确认的操作。

如果需要修改模板，安全顺序是“建议 -> `TemplatePatch` 差异预览 -> `ApplyTemplatePatch` 受保护调用 -> 逐次确认 -> 应用”。预览只读；未生成有效预览、未通过当前审批都不会写入模板。

在模板 JSON 编辑器处于活动状态时，也可以直接在 Copilot 中描述参数调整，例如“把 Exposure 调整到 12”。Copilot 只会先生成字段级差异预览；需要继续时，再明确要求应用对应的 `preview_id`。待确认区会说明影响，模板修改始终需要本次逐项批准。修改只进入当前编辑器，仍需由操作者决定是否保存模板。

## 安全边界

- 通用 Shell、PowerShell 和批处理命令始终绑定完整命令、Shell 和工作目录，并形成原生审批详情；按需确认时必须由用户逐次查看和批准，临时自动复核时也只有完整详情和 LOW/MEDIUM 风险可以通过独立权限模型，高风险、详情异常或复核失败仍等待用户。命令运行在独立 Windows Job Object 中，根 Shell 完成、取消或超时时都会收敛其后台子进程。固定只读系统/端口诊断不接受模型命令文本。
- 不自动启动、停止或重跑流程。
- 不直接控制相机、PG、SMU、电机等设备。
- 不删除文件，也不读取工作区允许范围之外的任意路径。
- 密码、token、API Key、许可证和序列号等字段会在业务上下文中脱敏。
- AI 给出的原因属于诊断建议；执行参数调整前仍需工程师确认。

开发人员需要配置本地 MCP 时，参见 [ColorVision 本地 MCP](../02-developer-guide/core-concepts/colorvision-mcp.md)。
