---
knowledge_id: "operations.terminal"
knowledge_type: "topic"
status: "current"
summary: "定义内嵌ConPTY会话、编辑器Python运行与外部CMD入口，区分命令提交、脚本结束、shell退出和强制释放。"
aliases: ["终端面板","终端乱码","运行Python","F5","脚本工作目录","在终端中打开","运行脚本","TerminalService","TerminalControl","ConPtyTerminal","ConPTY","TerminalScreenBuffer","AvalonEditControll","RunPythonCommand","TrySendCommand","BuildScriptStartupCommand"]
code_paths: ["UI/ColorVision.Solution/Terminal","UI/ColorVision.Solution/Editor/TextEditor.cs","UI/ColorVision.Solution/Editor/AvalonEditor/AvalonEditControll.xaml.cs","UI/ColorVision.Solution/Editor/AvalonEditor/AvalonEditControll.xaml","UI/ColorVision.Solution/Workspace/EditorDocumentService.cs","UI/ColorVision.Solution/Workspace/DockLayoutManager.cs","UI/ColorVision.Solution/Explorer/ScriptFileSupport.cs","UI/ColorVision.Solution/Explorer/SolutionResourceCommands.cs","UI/ColorVision.Solution/TreeViewControl.Command.cs","UI/ColorVision.UI/Environments.cs"]
test_paths: ["Test/ColorVision.UI.Tests/TerminalScreenBufferTests.cs","Test/ColorVision.UI.Tests/AvalonEditorSupportTests.cs","Test/ColorVision.UI.Tests/DockContentRegistrationTests.cs"]
related: ["ui.solution","ui.documents","operations.index","operations.logs"]
---

# 终端进程、会话与脚本运行

`TerminalService` 把工作区请求交给 `TerminalControl`，后者用 `ConPtyTerminal` 启动 Windows 伪控制台进程。它不是任务结果存储、事务执行器或权限沙箱；命令可能改文件、联网、启动其他进程或控制设备，权限来自当前任务。

展开终端并非纯粹查看文本：控件会准备命令历史目录，可见后会启动 shell；PowerShell 启动还可能运行本机 profile。只要求源码问答或诊断时，不应为查阅本页而启动产品、重新执行脚本、结束现有进程或清理数据。

## 入口与会话归属

`TerminalPanelProvider` 在底部以工厂注册默认隐藏的 `TerminalPanel`，首次物化时创建两个独立 `TerminalControl`：“终端”用于手工命令，“运行”用于 `RunScript`。二者不共享 ConPTY 进程，但仍继承同一宿主环境，不构成安全隔离。

| 入口 | 执行对象与边界 |
| --- | --- |
| 编辑器“运行 Python” / 无修饰键 `F5` | `AvalonEditControll` 检查 Python 文件及保存前提，再调用 `TerminalService.RunScript`，使用“运行”标签 |
| 资源树“运行脚本” | `ScriptFileSupport` 接受存在的 `.bat/.cmd/.ps1/.py/.pyw` 文件，直接按磁盘路径调用 `RunScript`；此入口没有编辑器脏文档保存步骤 |
| `TerminalService.SendCommand` / `TrySendCommand` / `TrySendCommandBatch` | 激活“终端”标签，把命令文本发送给交互 shell，不创建独立的每命令任务对象 |
| 资源树“在终端中打开” | `SolutionResourceShellPolicy.TryOpenTerminal` 用 `Process.Start` 启动外部 `cmd.exe /K cd /d ...`；不经过内嵌会话，也不受其 ConPTY Job 管理 |

`RunScript` 会展开面板；若控件尚未物化，只保留最新一个待运行路径，不排队执行全部请求。无 `WorkspaceManager.LayoutManager` 时直接返回，`RunScript` 是 `void`，没有表示脚本成功的返回值。停靠工厂注册复用同一个延迟宿主及其中的终端标签控件，关闭重开或重建布局不把原控件转挂到新的父节点，也不主动重建两个终端实例；显式显示会同步物化，自动恢复仍惰性加载。所有权与失败边界见[停靠注册、布局恢复和重置](../../04-api-reference/ui-components/editor-document-lifecycle.md#停靠注册、布局恢复和重置)，不能据面板重新出现推断旧进程已停止或新进程已启动。

## Shell、工作目录与环境

内嵌终端默认选择 `powershell`，实际启动 `powershell.exe -NoLogo -NoExit`，不是 `pwsh`，也没有默认 `-NoProfile`。CMD 模式启动 `cmd.exe`，带启动命令时使用 `/K`。切换“新建 PowerShell/CMD”是在当前控件内替换会话，不是再新增第三个标签。

- 初次启动或显式新建 shell：优先当前解决方案资源管理器的有效 `DirectoryInfo`，否则使用用户配置文件目录。切换解决方案不会自动给已经运行的 shell 切目录。
- `RunScript`：先检查文件存在，将路径规范化，再以脚本所在目录启动新会话；不使用工程根目录，也不保留上次运行 shell 的临时环境修改。
- `SendCommand(command)`：沿用现有交互 shell 的实际目录；没有可发送的会话时尝试重新启动。带 `workingDirectory` 的重载先发送切目录语句，并非直接修改宿主进程目录。
- `CreateProcessW` 没有传入独立环境块，子进程继承宿主环境；Python、Node、Bash 等解释器依赖相应命令可解析，没有自动选择工程虚拟环境、安装依赖或授予额外权限的契约。

指定目录的 CMD 命令使用 `cd /d ... && command`；PowerShell 单条命令使用 `Set-Location -LiteralPath ...; command`，没有统一的失败短路保护。目录切换失败时不能默认后续命令未执行。批量命令另由 `BuildBatchCommand` 拼接：CMD 以 `&&` 串联；PowerShell 设置 `ErrorActionPreference=Stop` 并在每项后检查 `$?`。批次不是事务，前面产生的文件/外部副作用不会回滚，任意复杂命令内部的失败也不是完整追踪结果。

## 编辑器保存与 Python/F5

`Editor/TextEditor.cs` 只负责打开文件并创建 `AvalonEditControll`；按钮、F5 和保存门禁在 `Editor/AvalonEditor/AvalonEditControll.xaml.cs`。只有 `.py/.pyw` 显示 Python 运行按钮，命令还要求已有文件路径；普通文本、JSON 或其它代码文件没有同一 F5 运行入口。

`RunCurrentPythonDocument` 仅在 `IsDirty` 时先调用 `EditorDocumentService.TrySaveDocument(this)`；保存返回 `false` 或异常被转成保存失败提示时，不发送运行请求。保存通过 `textEditor.Save` 写回当前文件，不是把未保存缓冲直接传给解释器，也没有另存为或自动备份步骤。未修改文档不会先写回磁盘，执行仍读取磁盘文件，因此外部修改、文件删除和编辑器内容可能需要单独核对。

运行并不验证 Python 语法、依赖或解释器版本；`.pyw` 也调用控制台 `python`，不是 `pythonw`。F5 可能写回文件并实际执行程序，不是预览或只读检查。关闭编辑器只释放编辑器本身的订阅/计时器，不等价于终止独立“运行”会话。

## 脚本启动与完成语义

`TerminalControl.RunScript` 在文件不存在时只追加“文件不存在”并返回，不停止旧会话。文件存在且经 `TerminalService` 路由的运行请求，才在“运行”控件中调用 `StartShell`：先终止并释放该控件的旧会话，再清空其输出和输入状态。它不会终止另一个手工“终端”控件的进程，但会中断同一运行会话中尚未结束的脚本或后续手工命令；不要把重复运行当作无副作用重试。

`BuildScriptStartupCommand` 统一构造 PowerShell 启动包装。即使“运行”标签选择 CMD，也会通过嵌套 `powershell.exe -NoLogo -NoProfile -EncodedCommand` 执行包装：

- `.py/.pyw` 调用 `python -- $scriptPath`；`.js` 调用 `node -- $scriptPath`；`.sh` 调用 `bash -- $scriptPath`。后两类是底层调用支持，不代表资源树或编辑器已经提供同名运行入口。
- `.ps1` 启动 `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File ...`，显式请求进程级 `Bypass`，不是“先校验策略再决定是否运行”的执行门禁，也不代替任务授权。
- `.bat/.cmd` 通过临时 `COLORVISION_SCRIPT_PATH` 环境变量传给 CMD，等待该批处理进程并恢复原变量；其余扩展名尝试 `& $scriptPath`，没有“任意文件必可执行”的承诺。

路径先以 UTF-16 Base64 编码再解码为参数，批处理另避免路径中的 `%` 等被二次解释；这些措施保护路径传递，不审查脚本内容。`SendCommand`/批次中的命令文本仍直接作为 shell 代码执行，不能接入不可信输入后宣称安全。

包装运行后根据 `$?` / `$LASTEXITCODE`（批处理使用进程退出码）输出“进程已结束，退出代码”。此处表示脚本调用返回，宿主 PowerShell `-NoExit` 或 CMD `/K` 仍保持打开；不表示整棵进程树、脚本启动的独立服务或业务动作都已完成。

`TrySendCommand` 返回 `false` 的主要路径是没有布局管理器或交互控件；批量空列表也返回 `false`。返回 `true` 只表示已经调用控件发送方法：shell 启动失败、管道写入失败或命令自身失败，仍可能没有成功结果。普通 `SendCommand` 和 `RunScript` 不提供异步完成/取消句柄，调用者不能把方法返回当作执行成功。

## 退出、取消与释放

- `Ctrl+C`：有文本选区时复制，否则发送控制字符 `0x03`；是否中断取决于当前进程。公开 `SendCtrlC` 同样是发送控制字符，不等待取消确认。
- “终止”：`KillShell` 使旧会话编号失效并释放该会话。`ConPtyTerminal.Kill` 请求终止 Job，必要时再终止主进程，最多等待主进程约两秒；这是强制结束，不是业务回滚，也不是所有外部任务停止的证明。
- 底层退出通知：输出读取循环结束后等待主进程最多五秒并查询退出码，再触发 `ProcessExited`；等待结果和退出码查询调用的成败未组成严格完成门禁。控件据此显示“终端已退出”并释放资源，不能把事件名当作每条命令或全部子进程完成的回执。随后激活/输入可重新启动会话。
- 隐藏、关闭停靠面板或切换标签：没有绑定为 `TerminalControl.Dispose`，不应据此断言进程停止。`DockLayoutManager` 缓存注册内容并可再次显示，编辑器关闭也不关闭终端。
- 最终 `Dispose`：先停止会话、解除输出事件，再释放管道、伪控制台和进程/线程/Job 句柄。Job 配置 `KILL_ON_JOB_CLOSE`，应用退出回调释放两控件；读取线程只限时等待，代码未给出可供上层确认全部子进程退出的完成结果。

`StartShell` 先以挂起状态创建进程，分配到 Job 后才恢复；创建管道、伪控制台、进程或 Job 失败会抛异常，由控件尝试释放并显示“启动终端失败”，同时写日志。管道写入异常仅记 debug；界面暂时无输出也可能是等待输入、输出缓冲或失败，不能自动重复提交命令。

## 输出、输入历史与验证

ConPTY 用 UTF-8 编解码，通过带会话编号的输出队列交给 `TerminalScreenBuffer` / `TerminalView`；旧会话回调被过滤，画面可能受 VT 控制序列覆盖、清除或移动。屏幕回滚最多保留 `3000` 行，不是完整日志归档；“清屏”清显示缓冲，不停止进程，也不删除命令历史。`Ctrl+L` 还会向进程发送控制字符。

`CommandHistory` 根据提示行推断上下文，并把手工回车时追踪的输入保存到 `Environments.DirStateTerminal` 下的 `terminal_history*.txt`，每上下文最多 `1000` 条；两个会话不是各自独立的历史文件。自动发送命令和多行粘贴不构成完整可审计记录，也没有敏感命令过滤保证，不应在命令行粘贴凭据。

- `TerminalScreenBufferTests` 覆盖换行、退格、光标移动、清屏、颜色、长行、缩放与快照；不证明所有 VT/IME/交互程序兼容。
- `AvalonEditorSupportTests` 覆盖 Python 扩展识别、路径编码包装和部分保存编码；其中批处理路径测试会真实启动 PowerShell，ConPTY Job 测试会真实启动 CMD 并调用 `Kill`，不属于纯解析测试。Job 测试断言看到就绪输出，没有断言所有后代进程均退出。
- `DockContentRegistrationTests` 检查延迟物化、关闭/隐藏重开及内存布局替换时的宿主和内容复用；使用合成内容，不启动 ConPTY，也不证明隐藏、关闭、退出时的进程生命周期。

当前未声明 F5 保存失败后不运行、双标签并发隔离、待运行请求覆盖、批次失败短路、取消确认与应用退出后全部进程消失的端到端覆盖。文档核验只读源码；真实验证需获准的合成脚本与隔离目录，并分别检查磁盘内容、输出和进程状态，不运行产品脚本或设备命令代替测试。通用输出定位见[日志来源与筛选](./log-viewer.md)。
