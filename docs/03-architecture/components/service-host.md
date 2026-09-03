---
knowledge_id: "platform.service-host"
knowledge_type: "topic"
status: "current"
summary: "ColorVision 服务主机的状态刷新、安装修复、日志诊断、身份票据与就绪条件；自动刷新只更新日志，客户端超时不取消命令，服务停止超过两分钟仍等待排空，服务启动成功日志不证明后台清理和启动完整性检查完成。"
aliases: ["ColorVisionServiceHost", "ColorVision 服务主机", "ColorVision Service Host", "服务主机安装记录", "ServiceHostLogReader", "后台权限代理", "本机特权服务", "服务宿主", "命名管道", "SCM停止预算", "broker ticket", "ServiceHostProtocol", "ColorVisionServiceHostClient", "IColorVisionServiceHostClient", "ServiceHostPipeClient", "ServiceHostCallerIdentity", "ServiceHostBrokerTicketService", "ServiceHostCommandHandler", "ServiceHostPipeServer", "ColorVisionServiceHostService", "ColorVisionServiceHostManager", "ServiceHostStatus", "ServiceHostRuntimeIntegrityChecker", "ServiceHostStartupUpdateChecker", "ServiceHostManagerWindow", "ColorVisionServiceHostWizardStep", "Program.BeginConsoleShutdown"]
code_paths: ["src/ColorVisionServiceHost", "UI/ColorVision.UI/ServiceHost/ServiceHostProtocol.cs", "UI/ColorVision.UI/ServiceHost/IColorVisionServiceHostClient.cs", "ColorVision/ServiceHost"]
test_paths: ["Test/ColorVision.UI.Tests/ServiceHostStatusTests.cs", "Test/ColorVision.UI.Tests/ServiceHostLogReaderTests.cs", "Test/ColorVision.UI.Tests/ServiceHostBrokerTicketTests.cs", "Test/ColorVision.UI.Tests/ServiceHostPipeServerTests.cs", "Test/ColorVision.UI.Tests/ColorVisionServiceHostServiceLifecycleTests.cs", "Test/ColorVision.UI.Tests/ServiceHostStartupStatusTests.cs", "Test/ColorVision.UI.Tests/ServiceHostApplicationUpdateAccessTests.cs"]
related: ["platform.system", "platform.startup-integrity", "delivery.update", "delivery.update-scan-protection", "plugins.windows-service", "engine.shell-extension", "engine.mysql-recovery", "ui.socket-protocol"]
---

# ColorVisionServiceHost：本机权限代理与生命周期

`ColorVisionServiceHost` 是独立的 Windows 后台权限代理，不是 Engine、`WindowsServicePlugin` 插件或 `CVWindowsService` 业务服务ZIP，也不是远程运维HTTP接口。本主题负责桌面管理器、共享管道客户端和服务进程之间的契约；具体业务动作分别看[服务包安装](../../04-api-reference/plugins/standard-plugins/windows-service.md)、[缩略图与关联注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md)、[MySQL恢复](../../04-api-reference/engine-components/mysql-recovery.md)、[主程序更新](../../02-developer-guide/deployment/auto-update.md)和[Socket 程序防火墙放行](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md#查看与设置防火墙)。

源码工程位于 `src/ColorVisionServiceHost/ColorVisionServiceHost.csproj`，目标为 `net10.0-windows` / x64，维护脚本随输出复制到 `Tasks/`。桌面管理与就绪编排在 `ColorVision/ServiceHost/`；共享请求、响应和客户端在 `UI/ColorVision.UI/ServiceHost/`。主题中的源码路径用于本地定位，不能把程序集引用当作运行完成证明。

## 入口与有状态操作

| 入口 | 实际职责与前提 |
| --- | --- |
| `ServiceHostManagerWindow`、`ColorVisionServiceHostManager` | 查看状态，以及明确执行安装/卸载/启停、自更新和维护；安装/启停等管理路径可启动提权PowerShell，不是只读诊断 |
| `ColorVisionServiceHostWizardStep` | 必需的启动向导步骤，以 `IsReadyForPackagedVersion` 判定可继续；配置动作调用 `EnsureReadyAsync`，刷新动作查询状态 |
| `ServiceHostStartupUpdateChecker.CheckAndUpdateAsync` | 名称虽是检查，实际调用 `EnsureReadyAsync`，可能启动、更新或修复服务 |
| `ColorVisionServiceHostClient` | 向本机命名管道发送请求；通用发送方法不自动安装服务或调用 `EnsureReadyAsync` |
| 服务进程普通非交互启动 | 由 `ServiceBase` 进入SCM服务生命周期；默认交互启动只显示帮助，不负责安装/卸载 |
| 服务进程 `--run` / `--send <command>` | 前者启动真实管道与更新扫描保护，后者发送真实请求；都不是无副作用的文档验证命令 |

安装、更新、注册、目录ACL修改、服务/进程控制必须分别确认目标和授权。界面可见、应用内角色、Windows提权、管道ACL与命令票据是不同层，不能互相代替。状态和日志可能含SID、用户名、PID、主机名和路径，不把现场原文默认作为可公开诊断数据。

`--send` 走服务工程自己的 `ServiceHostPipeClient`，不是共享桌面客户端：实际业务请求仅携带命令、不传 `Data`，免票据名单也只有 `ping` / `status` / `issue-broker-ticket`。它会为 `self-update` / `prepare-application-update` / `application-startup-status` 申请票据，被服务端以 `broker_ticket_target_not_allowed` 拒绝；不能当成桌面调用的等价入口。Program传入的3秒只用于连接等待，没有共享客户端的整体等待超时逻辑。

## 检查状态与查看安装记录

默认入口是帮助菜单中的 **ColorVision Service Host**，窗口标题为“ColorVision 服务主机”。按以下顺序排查：

1. 点击“刷新状态”，同时查看“运行状态”“安装完整性”和“服务连接”。运行状态来自SCM；连接正常只表示读到了运行版本，是否就绪还要核对运行路径和文件完整性，条件见后文。
2. 查看“运行日志”“安装记录”和“操作记录”。前两项读取安装目录下 `ColorVisionServiceHost.log` 与 `install.log` 的最近一段，操作记录显示管理页面的调用与响应。默认勾选的“自动刷新”每2秒刷新日志，**不重新查询服务状态**；外部启停或更新服务后仍需点击“刷新状态”。
3. 确认目标后选择恢复动作。“系统维护 → 服务控制”提供“启动服务”和“后台更新服务”等独立操作；概览中的安装、修复或重新安装按钮均调用 `InstallAsync`，不是自动选择启动/自更新的 `EnsureReadyAsync`。安装按钮要求程序包可用且完整、不会降级，并且页面没有正在执行的操作；具体权限和就绪条件见后文。
4. 操作结束后检查新的状态与日志。需要诊断资料时，先刷新再到“支持与诊断”复制摘要；摘要来自最近一次成功查询。“发送反馈”打开反馈窗口并预选存在的运行日志与安装记录，提交前检查资料中的现场信息。

“上次安装未完成”取自当前读到的安装日志末段：后续出现安装完成记录才清除该段中的失败提示。它不等于本次SCM状态，也不证明当前服务仍未运行。界面不展示完整历史；“打开文件”会在资源管理器中定位日志，选中“安装记录”时定位 `install.log`，其他日志页定位运行日志。

## 协议、身份和票据

`ServiceHostProtocol` 与服务端 `ServiceHostContracts` 定义协议版本2、camelCase JSON、UTF-8无BOM编码。服务名和管道名均为 `ColorVisionServiceHost`。共享客户端连接本机 `.`；一个连接只发送一行请求、接收一行响应，随后释放，不是多请求长连接。请求有 `RequestId`、`OperationId`、`Command`、`Data` 和可选 `BrokerTicket`；响应有 `RequestId`、`Success`、`Message`、`Data`。

生产管道ACL给予LocalSystem和Administrators完全控制、Interactive读写权限；能够连接并不表示命令可执行。服务端对所有命令（包括 `ping` / `status`）先从管道获取调用进程PID，再查询该进程token的SID/用户名和实际可执行文件路径，并读取文件SHA256；这些身份字段不是相信请求JSON自报。

`ServiceHostCallerIdentity` 当前要求路径绝对且文件存在：主程序只检查文件名为 `ColorVision.exe`；Host自调用还要求完整路径与当前运行Host一致。这里**没有主程序安装目录白名单或发布者签名验证**。SHA256用于票据绑定，不等于已认证的发布版本。这是当前边界说明，不是安全审计通过或可扩大系统操作授权的结论。

### 命令分类不能按名称猜测

无需broker ticket的六个命令是 `ping`、`status`、`application-startup-status`、`issue-broker-ticket`、`self-update`、`prepare-application-update`，其余命令默认要求票据，连 `com0com-status` / `com0com-list` 也不例外。共享UI客户端与服务端的 `RequiresBrokerTicket` 必须保持一致；独立CLI的现有差异见入口段落。

免票据不等于免身份检查或只读：`application-startup-status` 更新内存启动状态，另要求主程序调用者和已知状态；`prepare-application-update` 使用调用者目录与SID修改目录权限；`self-update` 会进入代理自身更新链。`status` 返回代理进程信息，不检查全部业务服务、数据库或设备健康。

需要票据时，客户端先以目标命令和同一 `OperationId` 请求 `issue-broker-ticket`，收到可用票据后再开第二个连接发送原命令和原 `Data`，两次请求的 `RequestId` 不同。签发只拒绝为上述免票据命令申请票据，不是另一份已支持命令白名单；未知命令也可能先取得票据，最后由分派器拒绝。

### 单次票据的准确含义

`ServiceHostBrokerTicketService` 使用实例内随机密钥HMACSHA256签名，票据期限60秒，绑定目标命令、`OperationId`、调用者SID、PID和可执行文件SHA256。它**不绑定 `Data`、`RequestId` 或原始 `ProcessPath`，也不是用户确认凭据**。密钥和已消费ID只存在内存；实例重建后旧票据不能继续使用。

签名、期限和绑定通过后立即消费TicketId，再执行命令分派；后续参数错误、业务失败或未知命令不会恢复票据。相同票据并发消费只能成功一次，但重新申请票据可以再次执行；`OperationId` 不是持久化幂等账本，不提供业务恰好一次、参数冻结、重试安全或回滚保证。

协议版本错误返回 `unsupported_protocol_version`；身份解析返回false时响应 `untrusted_pipe_client`；票据失败区分 `broker_ticket_required`、`invalid_broker_ticket`、`broker_ticket_scope_mismatch_or_expired`、`broker_ticket_replayed`。命令handler通常将异常转为失败响应，但畸形JSON、身份文件hash读取异常或连接关闭等可能只记日志并结束连接，不能假定每次失败都有结构化错误。

## 客户端超时与服务停止

`ColorVisionServiceHostClient.SendAsync` 把同步pipe发送放入任务，再用延时和调用方取消令牌控制等待。已经进入同步发送后，取消/超时不会向服务端handler传递取消令牌；票据申请与实际命令各自的连接超时也不是业务整体执行期限。当前客户端反序列化响应后没有核对响应 `RequestId` 与请求一致，不能把这个字段当作已完成关联校验的保证。

`ServiceHostPipeServer` 将通过准入门的命令以 `CancellationToken.None` 执行。客户端断开、超时或服务取消pipe I/O，都不等于命令取消；传输层没有自己的业务超时、自动回滚或请求去重。收到超时后应先确认实际完成阶段和目标状态，不能自动重试注册、复制、SQL或服务操作。

停止顺序是关闭命令入口 → 取消pipe I/O → 等待已接纳命令完成 → 等待所有客户端任务结束。检查准入与登记命令在同一锁内，停止后不接纳新命令；已接纳命令尚未结束时，`StopAsync` 不会完成。`RunAsync` 与重复/并发 `StopAsync` 返回同一个完成任务，停止后的同一server实例不重启；完成前不能Dispose。

SCM `OnStop` 的两分钟是正常耗时预算，不是强杀或提前报告Stopped的截止点。超预算只记录并继续等待，通过wait hint报告进度；组件停止失败时仍等待其它组件并尝试释放资源。单个handler异常不自动使整个pipe server故障，不能把一条失败请求等同于服务已停止。

SCM启动会启动更新扫描保护 `ApplicationUpdateScanProtectionService`、启动完整性监视器 `ApplicationStartupIntegrityMonitor` 和pipe；后台启动任务完成不是“Service started”日志的前提。控制台 `--run` 没有启动SCM路径中的完整性监视器，两种启动方式不完全等价。控制台停止入口 `Program.BeginConsoleShutdown` 取消pipe token并请求扫描保护停止；控制台finally另行等待pipe和扫描保护的停止任务，再Dispose，不取消已接纳命令。后台组件分别见[临时扫描排除与清理](../../02-developer-guide/deployment/update-scan-protection.md)和[启动失败上报与缺依赖告警](./startup-integrity.md)，pipe停止完成不等于排除项已全部撤销或主程序健康。

## 包、运行实例与就绪判定

`ServiceHostProtocol.PackageDirectory` 优先向上最多8级寻找 `src/ColorVisionServiceHost/bin`，在首个有候选的祖先下递归选修改时间最新的Host可执行文件；没有源码构建候选才回退主程序目录的 `ServiceHost/`。这不是最高语义版本选择，也不能总是假定正在使用安装包附带版本。安装目标是Windows `CommonApplicationData/ColorVision/ServiceHost`，与源码输出及主程序附带包是不同目录。

`QueryStatusAsync` 组合包/安装文件版本、内容hash、运行时文件检查、SCM状态，以及运行中代理通过pipe报告的版本和路径，不启动或修复服务；读取与RPC仍可能失败和产生日志。包/安装内容hash优先取同名托管DLL，缺失才取EXE，不是整包hash或签名。SCM查询的 `InvalidOperationException` 映射为 `NotInstalled`，其他捕获异常映射 `Unknown`；不要忽略 `RawOutput`，仅凭枚举断言机器上绝无服务。

`ServiceHostRuntimeIntegrityChecker` 以包目录的核心文件、非PDB文件和deps资产建立期望集合，检查包中缺失与安装目录缺失/内容不符，不检查安装目录多出的文件。deps文件无法解析时跳过其资产，不等于清单内容已验证；包目录不可用时 `CanEvaluate=false`，也不等于完整性检查通过。

| 判定 | 当前条件与限制 |
| --- | --- |
| `IsReady` | SCM为Running、pipe返回可解析运行版本、运行路径匹配安装目标，且没有已检测出的运行时不完整；仅SCM Running不够 |
| `IsReadyForPackagedVersion` | `IsReady` 且不需更新、没有已检测出的包缺失；包不可用但已装代理满足条件时仍可为true，不保证包被验证 |
| `NeedsUpdate` | 包存在且版本可解析，并检测到安装版本缺失、包较新、同版本内容hash不同或安装运行时不完整等条件 |
| `CanSelfUpdate` | Running、需更新、运行路径正确、运行版本至少1.4.10.5，且没有已知不完整包；仍只是候选条件，不是自更新已完成 |
| `WouldInstallDowngrade` | 包版本低于已装或运行版本；编排避免用旧包修复覆盖新版本，但已装新版停止时可先尝试启动 |

`EnsureReadyAsync` 先查询；已经就绪直接返回，否则要求可用、版本可解析且完整的包。停止但已装版本合适时先启动；支持自更新时先请求自更新；失败或未就绪才可能回退提权安装/修复，并保留拒绝降级门禁。成功动作后轮询就绪，默认单次就绪等待30秒，不是整个工作流、安装脚本或SCM停止的硬期限。直接 `SelfUpdateAsync` 的成功响应与新进程通过就绪检查不同；此检查属于 `EnsureReadyAsync` 后续阶段。

因此“检查器返回失败”不证明没有启动过、没有改文件或没有继续运行的更新；这里没有整条工作流的事务回滚。单独调用底层安装/更新接口，也不能据编排的门禁推断底层自动具备同样保护。

## 源码核对与验证缺口

- `ServiceHostStatusTests` 测试状态/决策对象、安装脚本文本和临时运行时文件夹；`ServiceHostLogReaderTests` 测试日志末段读取、旧中文编码和安装结果识别。它们不执行真实SCM安装或UAC自更新，也不验证管理窗口的现场操作。
- `ServiceHostBrokerTicketTests` 检查两端命令分类、单次票据重放、命令/PID变化等；没有逐项验证SID/OperationId/hash变化、真实过期等待、签名或生产管道身份准入。
- `ServiceHostPipeServerTests` 使用真实本机命名管道，但大部分用例注入恒成功身份解析和fake handler，绕过生产ACL创建；当前进程token读取测试也不是端到端调用者鉴权。
- `ColorVisionServiceHostServiceLifecycleTests` 使用fake SCM、pipe和扫描保护、手动时钟，验证停止所有权、预算及释放；没有真实服务安装、LocalSystem运行或完整性监视器验收。其中 `SelfUpdateCallerKeepsOwnershipUntilServiceReallyStops` 只模拟 `stopped → copy → restart` 事件与所有权变量，不替换真实文件或执行更新脚本。
- `ServiceHostStartupStatusTests` 手工构造调用上下文；`ServiceHostApplicationUpdateAccessTests` 包含对临时目录实际执行 `icacls` 的用例，不能把全部相关产品测试视为纯只读检查。

文档维护只需核对源码/测试和运行知识、链接、检索校验，不执行这些服务入口或产品测试。真实ACL/调用方准入、提权安装、更新中断与停止drain、业务动作部分失败后的恢复，需另行授权隔离Windows环境；当前知识映射和静态测试证据不能替代该验收。
