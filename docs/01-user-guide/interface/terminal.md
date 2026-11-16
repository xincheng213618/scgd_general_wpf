---
knowledge_id: "operations.terminal"
knowledge_type: "topic"
status: "current"
summary: "定义内嵌ConPTY会话、编辑器Python运行与外部CMD入口，区分命令提交、脚本结束、shell退出和强制释放。"
aliases: ["终端面板","终端乱码","运行Python","F5","脚本工作目录","在终端中打开","运行脚本","新建 PowerShell","新建 CMD","进程已结束","终端已退出","启动终端失败","TerminalService","TerminalControl","ConPtyTerminal","ConPTY","TerminalScreenBuffer","AvalonEditControll","RunPythonCommand","TrySendCommand","BuildScriptStartupCommand"]
code_paths: ["UI/ColorVision.Solution/Terminal","UI/ColorVision.Solution/Editor/TextEditor.cs","UI/ColorVision.Solution/Editor/AvalonEditor/AvalonEditControll.xaml.cs","UI/ColorVision.Solution/Editor/AvalonEditor/AvalonEditControll.xaml","UI/ColorVision.Solution/Workspace/EditorDocumentService.cs","UI/ColorVision.Solution/Workspace/DockLayoutManager.cs","UI/ColorVision.Solution/Workspace/ViewPanelMenuItems.cs","UI/ColorVision.Solution/Explorer/ScriptFileSupport.cs","UI/ColorVision.Solution/Explorer/SolutionResourceCommands.cs","UI/ColorVision.Solution/TreeViewControl.Command.cs","UI/ColorVision.UI/Environments.cs"]
related: ["ui.solution","ui.documents","operations.index","operations.logs"]
---

# 终端进程、会话与脚本运行

内嵌终端用于执行 PowerShell/CMD 命令和运行磁盘上的脚本。手工命令使用“终端”标签，编辑器或资源树发起的脚本使用“运行”标签，工程生成和项目能力命令使用按需出现的“项目命令”标签。

终端首次可见后在后台启动 shell，PowerShell 还可能执行本机 profile。命令继承宿主环境与权限，能够写文件、联网、启动进程或调用设备。执行前应确认工作目录、解释器及操作范围。

## 打开终端与运行入口

选择“视图 > 终端”显示底部面板。在“终端”标签等待 shell 提示符后输入命令并回车；右键菜单底部显示当前 shell 和会话状态，例如 `PowerShell / Running`。这里的 `Running` 表示 shell 会话处于运行状态，不是某条命令的完成回执。

`TerminalService` 把工作区请求交给 `TerminalControl`，由 `ConPtyTerminal` 启动伪控制台进程。`TerminalPanelProvider` 在底部注册默认隐藏的 `TerminalPanel`。各标签有独立的 ConPTY 会话，并继承宿主环境与权限。

| 入口 | 执行对象与边界 |
| --- | --- |
| 编辑器“运行 Python” / 无修饰键 `F5` | `AvalonEditControll` 检查 Python 文件及保存前提，再调用 `TerminalService.RunScript`，使用“运行”标签 |
| 资源树“运行脚本” | `ScriptFileSupport` 接受存在的 `.bat/.cmd/.ps1/.py/.pyw` 文件，直接按磁盘路径调用 `RunScript`；此入口没有编辑器脏文档保存步骤 |
| 工程生成、项目能力命令 / `TerminalService.SendCommand`、`TrySendCommand`、`TrySendCommandBatch` | 在“项目命令”标签启动运行到退出的会话，运行期间再次提交会被拒绝 |
| 资源树“在终端中打开” | `SolutionResourceShellPolicy.TryOpenTerminal` 用 `Process.Start` 启动外部 `cmd.exe /K cd /d ...`；不经过内嵌会话，也不受其 ConPTY Job 管理 |

工程生成和调试的菜单可见性由 `SolutionFeatureVisibility.ShowBuildAndDebugUI` 控制，当前为关闭。

`RunScript` 会展开面板；若控件尚未物化，只保留最新一个待运行路径。停靠工厂注册复用同一个延迟宿主及其中的标签控件。显式显示会同步物化，自动恢复仍惰性加载。 [停靠注册、布局恢复和重置](../../04-api-reference/ui-components/editor-document-lifecycle.md#停靠注册、布局恢复和重置)

## Shell、工作目录与环境

交互终端默认启动 `powershell.exe -NoLogo -NoExit`。CMD 模式启动 `cmd.exe`，带启动命令时使用 `/K`。右键“新建 PowerShell”或“新建 CMD”会结束当前标签的旧会话、清空输出并启动所选 shell。

- 初次启动或显式新建 shell：优先当前解决方案资源管理器的有效 `DirectoryInfo`，否则使用用户配置文件目录。切换解决方案不会自动给已经运行的 shell 切目录。
- `RunScript`：先检查文件存在，将路径规范化，再以脚本所在目录启动新会话；不使用工程根目录，也不保留上次运行 shell 的临时环境修改。
- 项目命令：沿用“终端”标签的 Shell 方言，在指定目录启动全新进程。未指定目录时使用当前工作区或用户目录。项目命令的 PowerShell 使用 `-NoProfile`，CMD 使用单行 `/C` 命令和带盘符的工作目录。
- `CreateProcessW` 没有传入独立环境块，子进程继承宿主环境；Python、Node、Bash 等解释器依赖相应命令可解析，没有自动选择工程虚拟环境、安装依赖或授予额外权限的契约。

批量项目命令依次在每项指定的工作目录启动对应 Shell，首项非零退出后停止并保留该退出码。前面命令产生的文件或外部副作用会保留。手工终端的当前输入和“运行”标签中的脚本会话独立于项目命令。

## 编辑器保存与 Python/F5

1. 在编辑器打开已有路径的 `.py` 或 `.pyw` 文件。只有这两类文件显示“运行 Python”按钮；普通文本、JSON 等文件没有同一 F5 入口。先确认“运行”标签没有需要保留的进程，新脚本会替换该标签的旧会话。
2. 点击“运行 Python”，或在编辑器按无修饰键的 `F5`。文档有未保存修改时，先写回当前文件；保存失败会提示并停止运行请求。此步骤没有另存为或自动备份，也不会把编辑器缓冲直接送给解释器。
3. 在自动展开的“运行”标签检查输出。脚本以所在目录为工作目录；出现“进程已结束，退出代码”表示本次脚本调用返回，后续 shell 保持打开，详见[脚本启动与完成语义](#脚本启动与完成语义)。

保存与运行门禁位于 `AvalonEditControll.RunCurrentPythonDocument`，`Editor/TextEditor.cs` 负责创建该编辑器。未修改的文档不先保存，实际执行读取磁盘文件；遇到外部修改或删除，应先核对磁盘内容。`.pyw` 同样调用控制台 `python`，没有自动改用 `pythonw`、检查语法或安装依赖的步骤。

关闭编辑器只释放编辑器自身的资源；停止脚本应在对应的“运行”会话操作，按下文[退出、取消与释放](#退出、取消与释放)区分控制字符中断与强制终止。

## 脚本启动与完成语义

`TerminalControl.RunScript` 在文件不存在时只追加“文件不存在”并返回，不停止旧会话。文件存在且经 `TerminalService` 路由的运行请求，才在“运行”控件中调用 `StartShell`：先终止并释放该控件的旧会话，再清空其输出和输入状态。它不会终止另一个手工“终端”控件的进程，但会中断同一运行会话中尚未结束的脚本或后续手工命令；不要把重复运行当作无副作用重试。

`BuildScriptStartupCommand` 统一构造 PowerShell 启动包装。即使“运行”标签选择 CMD，也会通过嵌套 `powershell.exe -NoLogo -NoProfile -EncodedCommand` 执行包装：

- `.py/.pyw` 调用 `python -- $scriptPath`；`.js` 调用 `node -- $scriptPath`；`.sh` 调用 `bash -- $scriptPath`。后两类是底层调用支持，不代表资源树或编辑器已经提供同名运行入口。
- `.ps1` 启动 `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File ...`，显式请求进程级 `Bypass`，不是“先校验策略再决定是否运行”的执行门禁，也不代替任务授权。
- `.bat/.cmd` 通过临时 `COLORVISION_SCRIPT_PATH` 环境变量传给 CMD，等待该批处理进程并恢复原变量；其余扩展名尝试 `& $scriptPath`，没有“任意文件必可执行”的承诺。

路径先以 UTF-16 Base64 编码再解码为参数，批处理另避免路径中的 `%` 等被二次解释；这些措施保护路径传递，不审查脚本内容。`SendCommand`/批次中的命令文本仍直接作为 shell 代码执行，不能接入不可信输入后宣称安全。

包装运行后根据 `$?` / `$LASTEXITCODE`（批处理使用进程退出码）输出“进程已结束，退出代码”。此处表示脚本调用返回，宿主 PowerShell `-NoExit` 或 CMD `/K` 仍保持打开；不表示整棵进程树、脚本启动的独立服务或业务动作都已完成。

`TrySendCommand` 和 `TrySendCommandBatch` 返回 `true` 表示已受理启动。面板不可用、参数不合法或已有项目命令运行时返回 `false`。实际启动和退出结果显示在“项目命令”标签。

## 退出、取消与释放

- `Ctrl+C`：有文本选区时复制，否则发送控制字符 `0x03`；是否中断取决于当前进程。公开 `SendCtrlC` 同样是发送控制字符，不等待取消确认。
- “终止”：`KillShell` 立即断开旧会话，后台通过关闭 Job 终止进程树。
- Shell 自然退出后，后台获取退出码、关闭 Job 并读完伪控制台输出。界面在最后一批输出之后显示“终端已退出”。切换面板或标签会保留这段输出，继续输入或右键新建 Shell 会重新启动会话。
- 隐藏、关闭停靠面板或切换标签：没有绑定为 `TerminalControl.Dispose`，不应据此断言进程停止。`DockLayoutManager` 缓存注册内容并可再次显示，编辑器关闭也不关闭终端。
- 最终 `Dispose` 会解除输出订阅并请求后台清理管道、伪控制台和进程、线程及 Job 句柄。Job 配置 `KILL_ON_JOB_CLOSE`。应用退出时释放已创建的终端控件。

`StartShell` 在后台以挂起状态创建进程，分配到 Job 后才恢复。创建管道、伪控制台、进程或 Job 失败时显示“启动终端失败”并写日志。键盘输入和尺寸变化交给后台处理。

## 输出、输入历史与验证

ConPTY 用 UTF-8 编解码。`TerminalScreenBuffer` 分批处理 VT 控制序列，`TerminalView` 只绘制当前可见行。主屏幕最多保留 `3000` 行回滚历史。窗口变窄时，主屏内容重新折行，较宽的历史行也按当前宽度显示。向上滚动时可以留在当前阅读位置，滚动到底或继续输入时回到实时输出。双击选择一段连续文本，`Shift+单击` 扩展选区，拖动到视口边缘可继续选择历史输出。支持 ANSI 16 色、256 色、真彩色、宽字符、DEC 线框字符和备用屏幕。

方向键、命令历史、补全和行内编辑由当前 Shell 或前台程序处理。支持 Win32 输入模式时，组合键可保留 Shift、Ctrl 和 Alt 修饰状态。PowerShell 的历史保存行为由本机 PSReadLine 配置决定。前台程序启用括号粘贴时，终端按该模式发送剪贴板文本。其他会话中粘贴含换行的文本前会显示预览，确认后才发送。

`Ctrl+Shift+PageUp`、`Ctrl+Shift+PageDown`、`Ctrl+Shift+Home` 和 `Ctrl+Shift+End` 操作界面回滚历史。右键“清屏”清除界面输出和回滚历史，运行时保留光标所在的逻辑行。`Ctrl+L` 交给前台程序处理。

验收使用获授权的隔离目录和合成脚本。核对目标输入法、VT 行为、保存失败、双标签隔离、批次失败和退出后的进程状态，并分别查看输出、磁盘内容和后代进程。 [日志来源与筛选](./log-viewer.md)
