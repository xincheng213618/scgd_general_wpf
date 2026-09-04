---
knowledge_id: "engine.database-maintenance"
knowledge_type: "topic"
status: "current"
summary: "数据库维护窗口与provider能力：表统计不是删除预览；备份默认关闭，备份和清理不是事务且失败不自动恢复；清理、手动优化和迁移边界彼此独立。"
aliases: ["数据库清理", "数据维护窗口", "清理预览", "刷新统计", "清理前备份", "保留月数", "清空选中表", "清理取消", "索引优化", "结果关联索引", "DatabaseCleanupWindow", "DatabaseCleanupWindowViewModel", "DatabaseCleanupSourceViewModel", "DatabaseCleanupTableInfo", "IDatabaseCleanupSourceProvider", "IDatabaseCleanupSelectionProvider", "IDatabaseCleanupBackupProvider", "IDatabaseCleanupMaintenanceProvider", "IDatabaseCleanupMigrationProvider", "IDatabaseCleanupOptimizationProvider", "OptimizationCommand", "SocketDatabaseCleanupWindowLauncher"]
code_paths: ["Engine/ColorVision.Engine/Mysql/DatabaseCleanupContracts.cs", "Engine/ColorVision.Engine/Mysql/DatabaseCleanupWindow.xaml", "Engine/ColorVision.Engine/Mysql/DatabaseCleanupWindow.xaml.cs", "Engine/ColorVision.Engine/Mysql/DatabaseCleanupWindowViewModel.cs", "Engine/ColorVision.Engine/Mysql/MySqlToolWindow.xaml.cs", "Engine/ColorVision.Engine/Services/DatabaseCleanup/SocketDatabaseCleanupWindowLauncher.cs", "UI/ColorVision.SocketProtocol/ISocketDatabaseCleanupWindowLauncher.cs", "UI/ColorVision.UI/AssemblyHandler.cs", "Projects/ProjectARVRPro/ArvrSqliteCleanupProvider.cs", "Projects/ProjectKB/KbSqliteCleanupProvider.cs"]
test_paths: ["Test/ColorVision.UI.Tests/DatabaseCleanupWindowTests.cs", "Test/ColorVision.UI.Tests/DatabaseCleanupWindowLayoutTests.cs"]
related: ["engine.index", "engine.mysql-maintenance", "ui.sqlite-storage", "ui.database", "ui.discovery", "operations.data"]
---

# 数据库清理窗口、能力接入与完成边界

`DatabaseCleanupWindow` 是多数据源的统计、备份、清理、优化和迁移宿主，实际文件位于 `Engine/ColorVision.Engine/Mysql/`，虽使用 `ColorVision.Database` 命名空间，却属于 Engine 的独立维护链路。宿主只调用 provider，不统一实现业务删除、事务、停写或恢复。

本窗口没有 DatePicker、逐行删除预览或预览批准令牌。它提供表统计、保留月数、可选的选表清空，以及 provider 专有的手动优化和数据迁移。若问题是“预览后只删除这些批次”，应先纠正这个功能前提，再读 [MySQL 结果维护](./mysql-maintenance.md) 或实际 SQLite owner；不能从表格行数推断删除范围。

清理、优化、迁移和备份会写真实数据库或文件。阅读本页不授权执行这些动作；必须先确定目标、业务停写、权限、备份与恢复条件。窗口构造、`OpenWindow` 和 provider 接口没有统一管理员鉴权，具体调用入口必须自行维护授权检查；调用入口可见或方法可调用不代表操作已经获准。

## 数据源发现与窗口范围

无参数 `OpenWindow()` 建立全局窗口，默认 ViewModel 通过 `AssemblyHandler.RefreshAssemblies()` 和 `LoadImplementations<IDatabaseCleanupSourceProvider>()` 实例化 provider，再按 `Order`、`DisplayName` 排序。该刷新会重建程序集列表并清空接口实现类型缓存，但不会替换已经打开的维护窗口持有的 provider 实例；发现规则见[程序集与扩展发现](./../../02-developer-guide/core-concepts/extensibility.md)。这里不扫描磁盘上所有 `.db` 文件，也不保证未加载的项目 provider 可见。

`OpenWindow(owner, source)` 只注入一个 provider，不进行全局发现；`Sources` 对外只读，单源模式要求恰好一项，`SelectedSource` 拒绝集合外对象。Socket 的 Engine launcher 使用此入口；`MySqlToolWindow` 的清理按钮调用无 source 的全局入口。

必须保留真正的 `public static void OpenWindow()` 无参数重载：已发布的 ARVR、KB 等项目插件可能仍引用这个二进制签名。把它替换为带可选参数的方法，只能兼容重新编译的源码，旧 DLL 点击“数据清理”会抛 `MissingMethodException`。无参数重载转入同一全局窗口逻辑，不改变单源范围或清理行为；`DatabaseCleanupWindowTests` 通过精确反射签名与委托绑定检查此兼容入口，不打开真实数据库。

静态窗口表按不区分大小写的 `global` 或 `source:{Id}` 复用窗口，不按数据库连接、账号或文件路径区分。同 ID 再次调用只激活原窗口，不替换 provider，也不重新自动统计。全局和单源窗口可并存；直接构造窗口不走这个复用表。窗口范围是 UI 路由约束，不是数据库权限或进程级互斥机制。

生产入口设置 `refreshOnLoad: true`，第一次 Loaded 调用 `RefreshAllAsync()`，以 `Task.WhenAll` 统计该窗口的**所有** sources；全局模式不是只读取当前选中项。单源只统计注入来源。提供者构造、描述属性和 `LoadTables` 是否会初始化目录/schema/连接需由 owner 核对，不能为回答“有哪些数据源”而直接打开真实维护窗口。

## 接口决定能力，不决定安全保证

| 接口 | 宿主使用方式 | 不可推断 |
| --- | --- | --- |
| `IDatabaseCleanupSourceProvider` | 身份、描述、排序、`LoadTables`、`CleanupHistory(keepMonths)`、`CleanupAll` | 不提供统一预览、删除集合、日期列或事务 |
| `IDatabaseCleanupSelectionProvider` | 显示复选和“清空选中表”，传入表名列表 | 不自动补齐主从依赖或验证 provider 的白名单 |
| `IDatabaseCleanupBackupProvider` | 显示单独备份与“清理前备份”选项 | “完整”内容由实现决定，不包含自动还原承诺 |
| `IDatabaseCleanupMaintenanceProvider` | 将备份和动作委托给 provider 的组合入口 | 同一维护锁不等于同一数据库事务，也不锁住其它进程 |
| `IDatabaseCleanupMigrationProvider` | 显示 provider 的迁移按钮和确认文案 | 直接 API 不因宿主存在就自动备份或获得授权 |
| `IDatabaseCleanupOptimizationProvider` | 显示 provider 的手动优化按钮和确认文案 | 不统一提供 dry-run、自动备份、事务回滚或低负载窗口 |

当前实现入口如下；能否出现在全局窗口仍取决于程序集发现。项目 provider 不能按名字当成共享数据库实现。

| 来源 ID | 实际 owner | 专有能力 |
| --- | --- | --- |
| `mysql-results` | `Mysql/MySqlResultCleanupProvider.cs` | 选表、备份、组合维护与手动结果关联索引优化；结果表范围和 DDL 边界见 [MySQL 契约](./mysql-maintenance.md) |
| `socketmessages-sqlite` | `Mysql/SocketMessagesSqliteCleanupProvider.cs` | 备份、组合维护、旧正文迁移 |
| `flow-diagnostics-sqlite` | `FlowProcessing/Diagnostics/FlowDiagnosticsSqliteCleanupProvider.cs` | 备份、组合维护、诊断消息迁移 |
| `projectarvrpro-sqlite` | `Projects/ProjectARVRPro/ArvrSqliteCleanupProvider.cs` | 项目结果备份、组合维护与迁移 |
| `projectkb-sqlite` | `Projects/ProjectKB/KbSqliteCleanupProvider.cs` | KB 结果备份、组合维护与迁移 |

Socket/Flow 的锁与迁移实现见 [SQLite 正文存储](../ui-components/sqlite-storage.md)。ARVR、KB 的迁移还涉及各自的模型兼容和历史内容重建，必须核对项目实现；这里的能力表不证明它们与通用 gzip 工具具有相同细节。

## 表统计与确认固定了什么

`RefreshAsync` 在后台调用 `LoadTables` 后替换 `Tables`，按表名保留仍存在的选择。不存在的表不能选中，`ExistingRowCount` 和空间只是 provider 返回值的加总，未声明共同时间点或按保留月数筛选。刷新失败不清除之前成功的表快照，退出 busy 后旧快照仍可能让按钮可用。

通常按钮要求 `!IsBusy` 和至少一张存在的表；选表入口还要求 selection 能力及非空选择。这是当前快照的可执行状态，不是“已完成针对本次删除的预览”门禁。直接 provider 调用不依赖这些 UI 条件。

| 确认入口 | 已捕获参数 | 未固定的状态 |
| --- | --- | --- |
| 保留月数清理 | 默认文本为 `3`；解析正整数，在确认前捕获 `keepMonths` | 没有统一最大月数；截止时间由 provider 计算，不保存待删除行集合，也不受当前选表限制 |
| 清空选中表 | 确认前捕获存在且选中的表名数组，随后传给 `CleanupTables` | 未捕获表中记录、连接配置或主从依赖闭包 |
| 清空当前库可清理表 | 弹窗使用当前快照的可用表数量 | 执行只调用 `CleanupAll()`，不传弹窗中的表名/行数；provider 可重新发现当前库 |
| 优化 | provider 的说明，以及“不会删除业务数据、不会自动创建完整备份”的二次提示 | 没有通用 dry-run；不冻结连接、schema、已有索引、数据库负载或临时空间 |
| 迁移 | provider 的说明及强制备份提示 | 没有通用 dry-run、版本批准或恢复协议 |

确认与实际执行之间，provider 若重新读取可变连接配置、系统时间或 schema，宿主不会冻结这些值。XAML 提示“清理主表时需同时选择所有现存关联明细表”，但宿主只捕获选择，没有实现依赖校验；MySQL 当前缺少对应强制门禁的事实见专有契约，不能把提示当成代码保证。

## 备份、执行与失败分层

每个 source 的 `BackupBeforeCleanup` 默认 false，界面“推荐”文字不表示默认勾选或持久策略。普通清理可在没有自动备份的情况下继续；provider 不支持备份时会提示需已有可恢复副本，但宿主不验证副本。单独点击创建备份也不会登记一个后续清理必须匹配的批准记录。

迁移入口不同：缺少 backup 能力直接拒绝；经用户确认后以 `forceBackup: true` 调用执行包装。优化入口则明确关闭可选备份路径：即使当前 source 勾选了“清理前备份”，宿主也不会为 `ExecuteOptimization()` 自动创建备份。优化确认中的“不删除业务数据”只描述该 provider 的动作范围，不代表 DDL 没有持久 schema 变更、可以事务回滚或无需按现场制度留存备份。

备份和动作按以下方式运行：

- 有 maintenance 能力：仅调用 provider 的 `ExecuteCleanupWithBackup(action)`；由它负责备份、锁和动作顺序，宿主不再额外重复备份。
- 只有 backup 能力：先 `CreateBackup()`，正常返回才执行 action；两者之间没有宿主统一维护锁或数据库事务。
- 普通清理未启用备份：直接执行 action；底层若有单独锁或事务仍以实际 provider 为准。

备份阶段失败不会继续调用清理/迁移动作，但不能扩大为“整个入口绝无先前副作用”：初始化、描述读取或 provider 的备份本身可能已经触发动作。组合入口失败时宿主提示如已生成备份则保留，并要求重新确认现状；宿主没有自动恢复或补偿事务。

普通清理和迁移动作成功后另行调用 `LoadTables` 刷新。刷新失败会保留动作成功结果并加警告，不把已经完成的动作回滚；操作失败分支直接报告错误，不自动刷新统计。优化成功后不自动重新统计表，索引大小等显示值仍是旧快照，需由用户显式“刷新统计”。因此成功文案、最新统计、备份可恢复和库健康是不同证据。`DatabaseCleanupExecutionResult` 只是状态文字/摘要，没有统一的提交凭据或跨库成功协议。

## 忙碌、关闭与并发

工作以 `Task.Run` 执行，UI 更新通过 Dispatcher；`IsBusy` 是每个 source ViewModel 的状态。当前来源的操作卡片在忙时禁用，但不是所有窗口和来源共用的锁。全局/单源窗口对同一库可能有不同 ViewModel，运行中切换来源也不会使原操作变成新来源；实际互斥由 provider 自己提供。

窗口没有取消按钮、CancellationToken 或 Closing 阶段的等待/拒绝协议。确认框取消只阻止尚未开始的动作；关闭已运行窗口不取消后台数据库操作、不回滚、不终止外部工具。关闭后窗口注册项被移除，重开可能建立另一套状态，不能用“新窗口不忙”判断旧维护已结束。

`RelayCommand.CanExecute` 和 busy 状态不是权限控制。虽然核心异步包装会再次检查该 ViewModel 的 `IsBusy`，它不能约束公开 provider API 或其它实例；调用方仍须承担授权、目标固定与业务停写责任。

## 验证证据

`DatabaseCleanupWindowTests.cs` 用 fake 覆盖能力开关、默认备份关闭、仅存在表可选、组合维护调用一次，以及 MySQL 白名单/排序/未知明细检测辅助方法；它没有执行真实清理事务或在线索引 DDL。`DatabaseCleanupWindowLayoutTests.cs` 覆盖单源隔离、来源排序/切换、可见性和布局等，使用 fake 或不加载真实库的 provider 构造。

例如布局测试显式刷新选中 source，不等于生产 Loaded 只刷新选中项；应读具体调用而不是仅凭测试名推断。现有测试不证明真实关闭时取消、备份故障恢复、主从完整性、跨窗口互斥或授权门禁。本次仅核对源码与测试内容，没有运行产品或操作用户数据库。
