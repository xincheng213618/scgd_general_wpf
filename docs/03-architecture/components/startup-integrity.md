---
knowledge_id: "platform.startup-integrity"
knowledge_type: "topic"
status: "current"
summary: "主程序启动失败识别、状态上报和后台缺依赖告警；十秒观察不强杀进程，已处理终态抑制重复弹窗，无告警不证明安装完整。"
aliases: ["启动缺文件告警", "无法启动", "启动失败弹窗", "启动依赖缺失", "十秒启动观察", "启动告警抑制", "StartupFailureGuard", "StartupFailurePresentation", "StartupRegistryChecker", "ApplicationStartupIntegrityMonitor", "ApplicationStartupStatusHub", "ApplicationStartupStatusReport", "ApplicationRuntimeDependencyInspector", "InstalledColorVisionLocator", "WmiColorVisionProcessStartSource", "application-startup-status", "failed-handled"]
code_paths: ["ColorVision/StartupFailureGuard.cs", "ColorVision/ProgramTimer.cs", "ColorVision/App.xaml.cs", "ColorVision/MainWindow.xaml.cs", "src/ColorVisionServiceHost/ApplicationStartupIntegrityMonitor.cs", "src/ColorVisionServiceHost/ServiceHostCommandHandler.cs", "src/ColorVisionServiceHost/ColorVisionServiceHostService.cs", "src/ColorVisionServiceHost/Program.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ApplicationStartupIntegrityMonitorTests.cs", "Test/ColorVision.UI.Tests/ServiceHostStartupStatusTests.cs"]
related: ["platform.runtime", "platform.service-host", "delivery.update"]
---

# 启动失败上报与缺依赖告警

ColorVision有两条协作但不同的路径：主程序 `StartupFailureGuard` 识别早期依赖异常、提示后自行退出；ServiceHost的 `ApplicationStartupIntegrityMonitor` 观察注册安装的主程序启动，只在特定提前退出条件下补充缺文件告警。**后台监测器不强制结束主程序，不自动修复、重装或回滚。**没有告警也不等于全部依赖完整、应用健康或监测已成功启用。

本主题负责失败识别、状态协作与提示门禁；启动记录/恢复窗口归[运行时与恢复](../overview/runtime.md)，本机pipe身份与整体服务停止归[ServiceHost](./service-host.md)，更新文件替换归[自动更新](../../02-developer-guide/deployment/auto-update.md)。不要把本主题的文件存在性检查与[更新扫描保护](../../02-developer-guide/deployment/update-scan-protection.md)的Defender排除项变更混为一谈。

## 主程序自身的早期异常路径

`App` 构造器仅在非DEBUG分支接入Dispatcher异常等处理并调用 `StartupFailureGuard.Begin`；普通源码Debug启动不能直接套用这条完整上报/失败处理链。guard尽量只依赖BCL和原生MessageBox，避免日志或UI依赖本身损坏时连提示也无法构造，但这不是任意进程级崩溃都能捕获的保证。

guard只在Begin之后、`MarkReady`之前处理识别出的依赖异常。它遍历InnerException、Aggregate和Reflection加载异常，区分程序集文件缺失/加载失败、格式/类型问题以及native DLL或入口点缺失；`FileNotFoundException` 还需文件名符合程序集身份或 `.dll` / `.exe`，不是所有业务数据文件缺失都会触发“无法启动”。

`TryHandleStartupFailure` 每进程最多进入一次失败提示，顺序是：

1. 尝试写入 `StartupRegistryChecker.MarkDependencyFailure`，失败不阻止原生提示。
2. 发送 `failed-handled` 状态，携带组件、异常类型、截断详情及 `promptShown=true`。
3. 调用本机原生MessageBox提示。
4. `Environment.Exit(-1)` 退出当前应用。

因此“后台不杀进程”不等于主程序guard不会自行退出。`promptShown=true` 在弹框之前发送，不是用户已经看到或确认提示的回执；提示中的重装/配置保留文字也不证明安装器已验证该承诺。未识别的异常仍归相应应用异常处理链，不由这里自动修复。

## 上报与健康标记的边界

`StartupRegistryChecker` 实际定义在 `ColorVision/ProgramTimer.cs`，不是单独同名文件。其阶段更新发progress，Clear发ready；主窗口首次渲染、初始化器路径等可以调用Clear，所以ready是启动流程标记，不是设备、数据库、插件业务能力逐项通过的健康认证。

| 状态 | 主程序发送与服务端消费 |
| --- | --- |
| `begin` / `progress` | guard排入线程池发送；Hub记录但不结束PID观察 |
| `ready` | guard记录启动已完成后异步发送；终态，结束该PID观察 |
| `failed-handled` | 失败提示路径同步尝试发送；终态，表示应用接管了失败提示，不是已完成修复 |

guard独立使用BCL命名管道和JSON，不依赖共享UI客户端。后台发送的连接等待250毫秒，失败提示发送750毫秒；这不是完整RPC的硬期限，读写超时只在pipe支持时设置。发送异常返回false，调用方不以成功响应为继续条件；线程池排队也不提供所有进度按顺序到达、必达或持久化保证。

`application-startup-status` 免broker ticket但仍经过生产pipe身份检查，另要求调用者文件名为 `ColorVision.exe` 和已知state。handler使用实际调用上下文的PID，并截断stage/component/detail等字段，不相信请求自报的进程身份；没有成功上报时不能假定后台知道主程序已处理异常。

## 哪些进程会被后台观察

SCM服务启动会启动监测器，控制台 `--run` 不启动它。监测器Start从HKLM的32/64位卸载注册表寻找 `DisplayName=ColorVision`、有效安装目录及其 `ColorVision.exe`，保存本次启动的路径集合；不是监听仓库里或任意便携目录的所有同名程序，运行中也不自动刷新安装目录集合。

监测器订阅WMI `Win32_ProcessStartTrace`，并枚举已存在的同名进程。没有注册安装或WMI订阅失败时可以只记日志并返回已完成的启动任务；“Service started”或Start任务完成不是观察源已可用的证明。

可读取进程实际路径时必须命中注册安装路径；无法读取路径、只有一个注册安装且session有效时，会回退该唯一路径。因此不能声称每次告警都已严格验证实际执行路径。进程在取得句柄等步骤前已消失也可能不形成完整观察，无告警不能据此排除早期退出。

## 十秒窗口与重复告警抑制

活动观察按PID去重，竞速等待进程退出任务、10秒延时和 `ApplicationStartupStatusHub` 的终态任务。10秒是观察窗口，不是启动超时强杀、弹窗倒计时或持续健康检查周期。

| 观察结果 | 后续行为 |
| --- | --- |
| 已收到 `ready` / `failed-handled` | 优先结束观察，不补缺文件告警；不要求 `PromptShown=true` |
| 退出任务在窗口内先完成且没有终态 | 检查依赖缺项；确有缺项才尝试向该session告警 |
| 十秒结束时仍未取得退出/终态结果 | 可能检查并记录缺项，但抑制提示；不会强制终止进程，也不继续观察它后来何时退出 |
| 依赖缺项为空 | 不提示，不能推出完整安装或其它启动失败原因不存在 |

实现将退出任务赢得竞速当作提前退出，没有另外await并验证该任务成功状态；诊断时不能把告警升级为故障因果已经全面确认。应用终态与退出竞速仍是异步路径，上报失败或时序变化可能影响是否补充提示。

观察finally会从Hub移除PID状态；Hub没有持久记录或TTL，活动PID去重和终态抑制不是固定时间全局节流，也不是永久失败黑名单。不能通过一个进程没弹第二次推断整个安装在某个冷却期内都不会再提示。

## “缺依赖”到底检查什么

`ApplicationRuntimeDependencyInspector` 以安装目录为根进行有限文件存在性检查：

- 要求 `ColorVision.deps.json` 与 `ColorVision.runtimeconfig.json` 存在，不解析runtimeconfig内容。
- deps使用 `targets` 中首个JObject目标，不按 `runtimeTarget.name` 选择。
- `runtime` 资产只取文件名，在应用根目录检查。
- `runtimeTargets` 仅检查native且RID为 `win` 或Host当前架构对应 `win-*` 的项目；越界/不合适的相对路径跳过。
- deps缺失、不可读、非法JSON或没有可用target时报告该控制文件，不推导无法知道的其它依赖。

它不验证hash、版本、签名、可加载性、未声明DLL、全部插件或系统运行时；某DLL存在但已损坏/版本不符时可能不在缺项列表。检查器也不确认文件为何缺失，不能只凭提示认定被Defender隔离或更新包损坏。

## 告警、手工入口与停止

后台告警使用 `WTSSendMessage` 向对应session发送，展示前6项及剩余数量，`wait=false`；返回成功不表示用户看见或确认。失败记Win32错误，没有自动重试或文件修复。不要把告警窗口或日志原文默认公开，其中可能包含用户路径和异常细节。

服务程序的 `--show-startup-integrity-warning` 是另一条有界面副作用的入口：直接检查指定目录并向当前session尝试提示，显式目录不要求注册安装，也不经过10秒、进程退出或终态门禁。它不启动持续监测，不是只读文档验证命令；文档维护不得为验证文字而运行它、制造依赖缺失或结束用户进程。

监测器Stop关闭接纳、退订/停止WMI源、取消并等待已登记观察任务，Dispose释放事件源；SCM整体停止仍受ServiceHost核心生命周期约束。停止完成只说明观察资源已结束，不是清除应用故障或执行恢复。

## 验证证据与缺口

`ApplicationStartupIntegrityMonitorTests` 使用临时文件检查依赖列表、直接测试Hub和布尔告警条件、构造异常验证guard分类；WMI用例只构造/Dispose事件源，没有Start订阅。它不执行真实监测器启动、注册安装发现、进程退出竞速、WTS提示或guard的MessageBox/Exit。`ServiceHostStartupStatusTests` 手工构造上下文验证handler接受null details，不证明真实pipe握手、身份或早期上报成功。

当前没有据这些用例声明真实SCM/WMI启动、多个安装/路径回退、窗口与进程退出竞速、后台停止排空或安装器修复验收。文档校验只证明知识路径、链接和检索可用；系统集成验证需另行授权隔离Windows环境。
