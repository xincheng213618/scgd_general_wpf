---
knowledge_id: "delivery.update-scan-protection"
knowledge_type: "topic"
status: "current"
summary: "ServiceHost提供的主程序增量更新临时Defender排除项、目录准入和清理所有权；启用失败不阻断更新，服务停止或保护超时不保证排除项立即恢复。"
aliases: ["更新扫描保护", "临时Defender排除项", "排除项恢复", "扫描保护超时清理", "ApplicationUpdateScanProtection", "ApplicationUpdateScanProtectionService", "PowerShellDefenderExclusionManager", "IDefenderExclusionManager", "ApplicationUpdateScanProtectionPaths", "ApplicationUpdateScanProtectionState", "COLORVISION_UPDATE_SCAN_PROTECTION_ID"]
code_paths: ["UI/ColorVision.UI/Update/ApplicationUpdateScanProtection.cs", "ColorVision/Update/AutoUpdater.cs", "ColorVision/MainWindow.xaml.cs", "UI/ColorVision.UI/ServiceHost/IColorVisionServiceHostClient.cs", "src/ColorVisionServiceHost/ApplicationUpdateScanProtectionService.cs", "src/ColorVisionServiceHost/ServiceHostCommandHandler.cs", "src/ColorVisionServiceHost/ColorVisionServiceHostService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ApplicationUpdateScanProtectionTests.cs", "Test/ColorVision.UI.Tests/UpdaterBatchExecutionTests.cs", "Test/ColorVision.UI.Tests/ColorVisionServiceHostServiceLifecycleTests.cs"]
related: ["delivery.update", "platform.service-host"]
---

# 更新扫描保护：临时排除项与清理所有权

`ApplicationUpdateScanProtection` 是主程序增量/组合更新链调用的可选辅助机制：经本机ServiceHost临时修改Microsoft Defender的路径排除项，减少更新文件阶段的扫描干扰。它不是扫描文件、验证安装包可信、目录可写检查或启动完整性认证。启用失败时记录日志并返回空ID，更新链可以继续；不能把“更新成功”理解为扫描保护曾启用或已恢复。

这会改变系统安全设置，必须在明确授权的目标和隔离验证环境中执行；文档维护不调用Begin/Complete、不启动更新或Defender命令，也不建议为排障添加永久排除项。普通管道身份与broker ticket边界见[本机权限代理](../../03-architecture/components/service-host.md)，更新包、文件替换与恢复由[自动更新](./auto-update.md)负责。

## 客户端从哪里开始、何时交接

当前调用位于 `AutoUpdater.TryStartIncrementalApplicationUpdate`：创建唯一 `ColorVisionUpdate-*` 临时根及更新接管状态后，异步请求扫描保护，与下载器停止/其它实例关闭等准备交错执行；随后等待保护请求返回，再解包和准备替换。这里不是对每一种插件独立更新、快照恢复或完整安装器都统一包裹的全局机制。

`TryBegin` 先以1秒等待ping；成功才调用 `BeginApplicationUpdateScanProtectionAsync`，默认请求等待30秒、保护寿命180秒。失败响应、异常或成功但空 `protectionId` 都返回null，但语义不同：空ID也可能表示所需精确路径已在排除列表。null不阻断更新，且不证明系统没有排除项。退出更新的3秒权限准备限制属于另一个接口，不是包含本阶段在内的整个退出准备期限。

| 更新阶段 | 保护ID与清理责任 |
| --- | --- |
| 外部更新进程尚未接管，准备失败、退出权限不足或启动更新进程失败 | 原进程尝试 `TryComplete(scanProtectionId)`；没有拿到ID时不发送完成请求 |
| 外部进程已经接管 | 原进程保留接管状态，不在通常成功返回时立即清除保护；更新批处理把ID放入 `COLORVISION_UPDATE_SCAN_PROTECTION_ID` |
| 批处理重新打开主程序 | 子进程继承ID；主窗口首次 `ContentRendered` 调用 `CompleteAfterUpdateRestart`，清除自身环境变量并后台尝试完成 |
| 退出后不重启、启动未到主窗口、响应丢失或完成请求失败 | 依赖ServiceHost的状态文件与后台过期清理；不是批处理保证清理成功 |

`CompleteAfterUpdateRestart` 用进程内一次性标志防重复请求，失败不会重置标志或恢复环境变量。`TryComplete` 捕获失败并记“延后清理”日志，没有独立客户端重试。批处理的成功和失败路径均可能按重启/重开选择启动主程序；环境变量传递本身不证明新进程到了主窗口、完成请求被接纳或Defender已恢复。

## 服务端目录与会话准入

`begin-application-update-scan-protection` 与 `complete-application-update-scan-protection` 都要求生产管道身份和命令票据；`protectionId` 是持久会话标识，不是替代broker ticket的授权凭据。

`ResolveApplicationDirectory` 从调用者实际可执行文件路径取主程序目录，要求文件存在且名为 `ColorVision.exe`，并拒绝应用位于卷根。Begin的 `updateRoot` 必须已存在、末级目录名以 `ColorVisionUpdate-` 开头，且经完整路径规范化后位于允许临时根之下：Host进程的Temp，或按调用者SID在ProfileList映射出的用户 `AppData/Local/Temp`。寿命默认180秒，输入夹在30–300秒；没有心跳续租接口。

这些是词法路径和文件存在性检查，不解析目录junction最终目标，也不证明目录所有权、文件内容或签名可信。主程序文件名准入的限制仍由代理主题说明，不能从“目录已通过ResolvePaths”推出安装来源已认证。

Complete要求N格式GUID，存在状态时只比较调用者主程序目录与状态中的 `ApplicationDirectory`。虽然状态记录 `CallerSid`，完成时不比较它，也不绑定最初PID或OperationId；同目录另一个通过准入的主程序可结束会话。状态文件不存在时返回“already cleared”成功，没有额外核对Defender当前列表或同ID的恢复journal。

## 新增、复用与并发边界

Begin在进程内操作锁下先清理同应用目录的旧JSON会话，然后才申请新排除项；清理旧状态失败会阻止本次Begin。它不是多进程引用计数，第二次更新可能结束第一次更新的保护。检查旧JSON时遇到无法解析的记录也会使该轮旧状态清理失败，不能把错误只理解为同一应用仍在更新。

生产 `PowerShellDefenderExclusionManager` 读取排除列表，按精确路径条目决定新增，新增后再次读取确认。它不展开父目录/通配模式的有效覆盖；没有新增条目时返回成功、空ID和已有路径，不建立常规JSON会话，响应里的到期时间也不会令这些原有排除项到期。所有权只记录本次确认新增的 `ChangedPaths`，不是把所有请求路径都占为己有。

有新增时保存 `ProtectionId`、应用/临时目录、`AddedPaths`、创建/过期时间和CallerSid。Complete、到期清理仅尝试删除 `AddedPaths`，保留Begin时已存在的精确条目；但与外部管理员/策略并发更改同路径没有隔离，不能保证永不删除后来由别人重新添加的相同条目。

Begin期间旧会话清理、新增排除、写状态和响应发送不是统一事务。客户端超时不取消已接纳命令，可能已改变排除项却未获得ID；不能立即重复Begin并假定前一次未发生。

## 状态文件、过期与停止

状态位于 `CommonApplicationData/ColorVision/ServiceHost/UpdateScanProtection`，JSON通过临时文件写入后移动保存；新增前另写 `.pending` 恢复journal，记录准备新增路径，确认JSON保存后尝试删除journal。写JSON失败时尝试撤销已新增项，只有撤销成功才尝试删除journal；任何“尝试”都不等于无副作用回滚完成。

`Start` 立即调度一轮清理，并设置15秒周期；清理先处理 `.pending`，不等待其过期，再处理已到 `ExpiresAtUtc` 的JSON。单轮串行且周期回调不叠加清理任务；过期时间只是清理资格，不是Defender自动撤销时间。进程未运行、命令慢或清理失败都可延后恢复。JSON保存后的journal删除是尽力而为，后续pending清理不核对同ID活跃JSON；因此拿到ID也不保证排除项持续保留到到期。

`StopAsync` 关闭清理调度、取消后续扫描并等待正在执行的清理任务；它**不遍历清除所有尚未过期会话**。取消发生在记录间检查，已在运行的外部命令仍需结束；停止期间可以留下待恢复journal。服务进程重启后会再次扫描持久状态，但仍按上述pending/过期条件，不保证全部立刻恢复。

同一实例重复 `Start` 复用首次清理任务，不能借此恢复已停止的调度；首次 Start 之前就已停止会拒绝启动。重复 `StopAsync` 返回同一任务，`Dispose` 要求该任务已进入终态，包含失败终态。这里管理的是后台清理生命周期，`Begin` / `Complete` 自身不检查调度停止标记；生产命令的接纳和排空另由 ServiceHost pipe 控制，不能单独用此 StopAsync 推断不再有排除项变更。

| 失败位置 | 可确认的结果 |
| --- | --- |
| 新增外部命令失败 | 尝试journal恢复并返回失败；部分排除项可能已改变，残留journal供后续清理 |
| JSON写入失败 | 尝试撤销新增项再抛错；不是全链原子回滚 |
| Complete删除排除失败 | 返回失败并保留JSON，后台之后可重试 |
| 后台读取损坏状态、删除失败 | 记录错误，不能据服务还在运行推断清理成功；通常保留待处理状态 |
| 外部PowerShell超过20秒 | 尝试终止子进程树并返回失败，不能证明系统设置未改或回滚完成 |

外部适配器检查进程退出码及自定义成功标记，并在移除后重读列表；这不是Windows安全策略全状态验收。手工删除状态文件会丢失恢复依据，不应作为一般排障建议；现场核查状态或日志须限定范围并脱敏，不能公开用户路径/SID。

## 验证证据边界

`ApplicationUpdateScanProtectionTests` 使用临时目录、fake Defender适配器和可控时间核对新增/完成、过期与恢复journal、路径拒绝和停止所有权；另有启动PowerShell仅解析脚本文本的用例，不执行真实Defender变更。fake添加器将所有请求路径视为新增，没有直接覆盖预先已有项保留、零新增空ID、Defender回滚失败、JSON持久化失败或SID/PID变化。停止测试还明确允许剩余pending，不能将通过解释为全部排除项已撤销。`ColorVisionServiceHostServiceLifecycleTests` 证明的是fake组件停止的编排，不证明生产策略恢复。

`UpdaterBatchExecutionTests.ExitUpdateBatchPropagatesScanProtectionIdToRestartedApplication` 只断言生成批处理包含ID环境变量，不启动真实主程序验证ContentRendered或清理；同测试文件其它用例会执行临时批处理，不能把整个文件当成无副作用文本检查。

当前没有据此声明真实Defender策略、junction准入、外部策略并发、跨进程会话重叠、响应丢失后恢复或重启后主窗口清理的端到端验收。维护本知识只核对源码/测试及文档验证，运行上述系统操作另需授权。
