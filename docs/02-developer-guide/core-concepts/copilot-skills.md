---
knowledge_id: "copilot.skills"
knowledge_type: "topic"
status: "current"
summary: "Copilot 技能的项目/用户/内置发现路径、显式调用、同名选择、开关、按需加载、使用统计和 MCP 依赖配置。"
aliases: ["技能", "内置技能", "Skill 什么时候加载", "/skills", "SKILL.md", "SKILL.json", ".agents/skills", "allow_implicit_invocation", "Skill 没有出现", "CopilotAgentSkills", "CopilotAgentSkillCatalog", "CopilotAgentSkillMetadata", "CopilotAgentSkillReference", "CopilotAgentSkillOverrideConfig", "CopilotAgentSkillUsageStore", "Skill MCP 依赖", "colorvision-flow-diagnostics", "colorvision-script-automation"]
code_paths: ["ColorVision/Copilot/Agent/CopilotAgentSkills.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillCatalog.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillCatalogMonitor.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillMetadata.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillSelectionPolicy.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillUsageStore.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillReference.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillCommand.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillDiagnostics.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillMcpDependencyPolicy.cs", "ColorVision/Copilot/Agent/CopilotAgentSkillMcpDependencyInstaller.cs", "ColorVision/Copilot/Config/CopilotAgentSkillOverrideConfig.cs", "ColorVision/Copilot/CopilotChatViewModel.SupportCommands.cs", "ColorVision/Copilot/CopilotChatViewModel.SkillMcpDependencies.cs", "ColorVision/Copilot/CopilotChatViewModel.ConfigPersistence.cs", "ColorVision/Copilot/CopilotSettingsViewModel.Diagnostics.cs", "ColorVision/Copilot/CopilotSettingsWindow.xaml", "ColorVision/Copilot/Skills", "ColorVision/ColorVision.csproj"]
test_paths: ["Test/ColorVision.Copilot.Tests/CopilotAgentSkillCatalogTests.cs", "Test/ColorVision.Copilot.Tests/CopilotCodexSkillInstructionsTests.cs"]
related: ["copilot.lifecycle", "copilot.configuration", "copilot.interactions", "copilot.extensions", "copilot.tool-contracts"]
---

# Copilot 技能：发现、调用与排障

技能以 `SKILL.md` 保存某类任务的工作流程，可附带同目录参考资料。Copilot 先提供相关技能的名称和说明，模型需要时再通过 `load_skill` 读取正文、通过 `read_skill_resource` 读取资料。技能被列出、选中和实际加载是不同状态；加载成功也不代表业务操作执行成功。

ColorVision 使用自己的配置、作用域和工具审批，技能内容不会扩大这些权限。项目指令的发现另见[生命周期与项目指令](./copilot-agent-lifecycle.md)，聊天补全见[本地交互](./copilot-local-interactions.md)。

## 技能放在哪里

有效搜索根按下列顺序加入；目录必须存在且路径不经过符号链接或重解析点。

| 来源 | 发现路径与顺序 |
| --- | --- |
| 项目 | 对每个受信项目，从活动文件所在目录向上到项目根，依次查找 `.agents/skills`；活动文件不在该项目内时只查项目根 |
| 用户 | `%USERPROFILE%\.agents\skills` |
| 内置 | 应用输出目录的 `Copilot\Skills`；源码中对应 `ColorVision/Copilot/Skills`，项目文件将内容复制到构建与发布输出 |

每个搜索根下使用 `<skill-name>/SKILL.md`，也支持一层分组目录 `<group>/<skill-name>/SKILL.md`。不会遍历任意深度，也不默认查找 `.codex/skills` 或整个用户目录。附件、显式文件形成的额外可读搜索根不会自动成为项目技能源。

聊天目录保留同名技能的不同路径。运行时默认按搜索顺序选择同名的第一份；从补全候选中选定具体文件后，结构化 `CopilotAgentSkillReference` 可以指向另一份已发现的同名技能。名称和完整路径都必须匹配，引用不能引入搜索根之外的文件。要切换同名实现，应选择目标路径，不假定禁用一份后一定自动落到另一份。

## 添加与调用

1. 在项目或用户技能目录中创建 `SKILL.md`。`name` 使用 1–64 位小写字母、数字或连字符，`description` 说明适用任务；目录名和名称保持一致便于定位。
2. 正文写清输入、步骤、完成证据和停止条件。较长的领域参考放在该技能的 `references/`，不要把全部资料塞进说明字段。
3. 在聊天中输入 `/skills reload`，核对列表中的名称、来源和具体路径。
4. 使用 `$skill-name` 或 `/skill-name` 加上任务要求；有同名候选时从补全列表选择对应路径。只在普通文字中提到完整名称，不满足运行时的显式调用规则。
5. 根据工具返回的业务证据判断任务结果；使用统计中的“已加载”只证明技能内容被读取。

最小文件示例：

```markdown
---
name: example-diagnostics
description: 检查项目的本地诊断记录并解释失败原因。
---

# 项目诊断

先读取任务指定的记录，区分观察到的现象和推断。
列出支持判断的证据；缺少必要记录时说明缺口。
```

技能脚本的发现与直接执行关闭：`AgentFileSkillsSource` 使用 `scriptRunner: null` 和拒绝脚本的过滤器。读取技能/参考资料由 Framework 的只读规则处理；若任务另需 Shell、文件写入、流程编辑或 SQL，仍使用对应业务工具和原生审批。

## 查看、刷新与开关

| 入口 | 行为 |
| --- | --- |
| `/skills` | 显示当前目录、路径状态、依赖情况及本地使用统计，包含已禁用项 |
| `/skills reload` | 使目录缓存失效并重扫；`refresh`、`重载`、`刷新` 为同义参数 |
| `/skills off N` | 按当前列表编号将具体 `SKILL.md` 路径设为 `Off`；不删除文件 |
| `/skills enable N` | 将该路径设为 `On`，从下一次请求开始生效；不会安装或执行技能 |
| Agent 设置中的 Skill 覆盖 | 编辑名称或具体路径的状态，保存于全局 `CopilotConfig.AgentDefaults.SkillOverrides`，独立于模型 Profile |

列表编号不是永久 ID；目录变化后先重新查看。保存失败与“已保存但界面刷新失败”分别报告，不能把后者理解为配置没有写入。

| 状态 | 选择行为 |
| --- | --- |
| `Auto` | 使用作者策略、依赖条件、任务相关性及连续未加载的历史降级；移除该身份的显式覆盖记录 |
| `On` | 不因历史低使用率降级，但仍受作者策略、依赖、相关性和预算约束 |
| `Name only` | 初始元数据仅保留名称，说明替换为一个不可见字符；正文仍按需读取，也不绕过作者和依赖条件 |
| `Explicit only` | 仅在使用 `$name` 或 `/name` 明确点名时参与选择 |
| `Off` | 即使显式点名也不加入运行时技能集合 |

精确路径覆盖优先于名称覆盖；不同路径可分别设置。`On` 不等于强制调用，也不覆盖 `policy.allow_implicit_invocation: false`。`Name only` 可参与低成本观察，但仍占候选数量与名称预算。

## 作者元数据与 MCP 依赖

`agents/openai.yaml` 可提供 `interface.display_name`、`short_description`、`default_prompt`、`policy.allow_implicit_invocation` 和 `dependencies.tools`。同目录 `SKILL.json` 可作为界面信息与依赖的回退；同时存在时使用 YAML 中非空的界面字段及非空依赖列表。`SKILL.json` 不能代替必需的 `SKILL.md`。当前本地元数据读取器每份文件最多 64 KiB，依赖最多 16 项；它只解析受支持的字段，不是通用 YAML 配置解释器。

例如，仅允许显式调用的作者策略：

```yaml
policy:
  allow_implicit_invocation: false
```

MCP 依赖写在 `dependencies.tools` 中，使用 `type: mcp`、服务标识 `value`、`transport` 和 `url`。当前只支持 `streamable_http`，省略传输也按此类型解释；声明本地 `command` 或其它传输不会启动本地服务进程。

| 依赖状态 | 含义 |
| --- | --- |
| 已配置 | 找到已启用的匹配端点；没有 URL 时按服务名称匹配。这不证明网络可达、认证或工具调用成功 |
| 已配置但禁用 | 保留用户的禁用决定，不自动重新启用 |
| 可配置 | 有有效 URL、受支持传输且无现有匹配项，可在显式调用流程中请求确认 |
| 缺少 URL、配置无效、传输不支持或名称冲突 | 不自动补地址、覆盖配置或执行命令；报告需要处理的原因 |

存在不可用 MCP 依赖的技能不参与隐式匹配；显式点名仍可进入依赖处理。发送前默认启用的依赖处理只解析明确点名的技能：唯一名称可直接解析，同名多份时要求已选择的精确引用，不替用户猜测依赖来源。

确认“是”只向 `ExternalMcpServers` 添加经过校验的配置，默认逐工具审批；它不安装服务端软件、不提供认证，也不代表远端工具获准执行。选择“否”可继续本次任务但不写依赖配置，选择“取消”保留草稿并停止发送；只有不可处理依赖时另询问是否继续。配置写入失败会停止发送，已落盘但运行时刷新失败也停止发送并明确报告。相同依赖/问题在当前会话确认后可避免反复提示。外部 MCP 的连接与调用边界见[扩展能力](./copilot-agent-extensions.md#外部-mcp-工具发现)。

## 目录、上下文预算与使用统计

聊天目录和运行时选择是两层：

- `CopilotAgentSkillCatalog` 最多检查 256 个候选技能文件，按名称排序后展示最多 64 项；本地目录读取器拒绝超过 256 KiB 的 `SKILL.md`，frontmatter 上限 16,384 字符。某项未列出可能是格式、路径或数量限制，不等于磁盘上没有该文件。
- 目录缓存为 5 秒，文件监视器对相关变更去抖后使缓存失效；`/skills reload` 可强制重扫。正在运行的 Agent 保留本轮发现与选择缓存，目录变更从后续请求生效，不承诺热替换正在执行的技能。
- 宿主的 `ConfiguredIncludeSkillInstructions` 默认启用；关闭自动说明时仅显式点名可参与选择。这是宿主选项边界，不表示内置 Copilot 读取外部 `config.toml`，配置来源见[配置主题](./copilot-configuration.md)。
- 运行时最多选择 16 项。名称与说明预算按上下文窗口的 2% × 每 Token 4 字符估算，硬上限 8,000 字符；说明可缩短，超预算候选可省略。预算缩短不改磁盘正文。
- 选择依据是 `name`、`description` 与请求的相关性。显式点名优先；有显式点名时，其它普通候选被省略，`Name only` 仍可参与。显式调用也不绕过 `Off` 或数量/字符预算。

`Copilot/State/skill-usage.json` 位于应用本地数据目录，由 `CopilotAgentSkillUsageStore` 保存。它按技能名称记录选中、实际加载、连续未加载次数与时间；最多 128 个名称、文件上限 1 MiB，不保存用户问题、正文或提示内容。同名技能共享该使用统计，精确路径开关仍独立。

连续至少 20 次被选中却未加载会在 `Auto` 下变为历史 explicit-only。一次实际加载清零连续未加载次数，恢复参与隐式匹配的资格，但不会清除作者或用户的限制。Schema 1 历史数据仅在从未加载时保留可推导的连续未用计数；不能推断的历史从零观察。统计文件读取失败或超限时返回空统计，不应据此断言从未使用过技能。

## 随程序交付的技能

随包文档保留独立运行所需的短流程与参考，完整产品契约集中在对应主题。

| 技能 | 用途与产品说明 |
| --- | --- |
| `colorvision-flow-diagnostics` | 关联流程、设备、超时和结果证据；跨层检查清单随包提供。实时来源见[业务上下文](./copilot-agent-extensions.md) |
| `colorvision-flow-authoring` | 检查图、查节点/属性/端口、预览单项修改、审批与复核；见 [Flow 编辑契约](./copilot-agent-tool-contracts.md#flow-图语义与受保护编辑) |
| `colorvision-database-operations` | 查询 Schema、表分类和受保护写入；见[数据库工具权限与迁移保留](./copilot-agent-session-and-tools.md#数据库工具权限与迁移保留) |
| `colorvision-batch-image-conversion` | [批量图像工具](./image-algorithm-platform-v1.md#copilot-批量图像工具)和 [CVRAW/CVCIE 原生导出](../../04-api-reference/engine-components/cv-image-export.md) |
| `colorvision-script-automation` | 使用工作区补丁和 Shell 创建、执行脚本；见[工作区与内置工具](./copilot-agent-session-and-tools.md)，专有图像继续使用原生解码路径 |

## 技能没有出现或没有运行

按以下顺序检查，避免通过放宽执行权限解决发现问题：

1. `/skills reload` 是否能看到正确来源和路径；核对目录深度、文件大小、frontmatter 和重解析点。
2. 是名称覆盖还是精确路径覆盖，是否为 `Off`，是否选中了同名的另一份文件。
3. 作者是否禁用隐式调用、MCP 依赖是否仅“已配置”但不可用、是否发生历史降级。
4. 使用 `$name` 或 `/name` 再观察；单纯提到名称、候选相关性不足或预算省略都可能导致不加载。
5. 区分目录项、运行时已选择、正文已加载、工具已执行及最终业务成功；分别查看 `/skills`、任务诊断和真实工具结果。

`CopilotAgentSkillCatalogTests` 覆盖项目/用户/内置来源、嵌套路径、同名选择、路径覆盖、元数据回退、监视器与 MCP 依赖计划；`CopilotCodexSkillInstructionsTests` 覆盖兼容选项与技能选择的边界。测试文件存在不表示本轮已运行，也不能替代真实界面或外部 MCP 服务验收。
