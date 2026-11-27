---
knowledge_id: "engine.database-maintenance"
knowledge_type: "topic"
status: "current"
summary: "数据库维护窗口与provider能力：表统计不是删除预览；备份默认关闭，备份和清理不是事务且失败不自动恢复；清理、手动优化和迁移边界彼此独立。"
aliases: ["数据库清理", "数据维护窗口", "清理预览", "刷新统计", "清理前备份", "保留月数", "清空选中表", "清理取消", "索引优化", "结果关联索引", "DatabaseCleanupWindow", "DatabaseCleanupWindowViewModel", "DatabaseCleanupSourceViewModel", "DatabaseCleanupTableInfo", "IDatabaseCleanupSourceProvider", "IDatabaseCleanupSelectionProvider", "IDatabaseCleanupBackupProvider", "IDatabaseCleanupMaintenanceProvider", "IDatabaseCleanupMigrationProvider", "IDatabaseCleanupOptimizationProvider", "OptimizationCommand", "SocketDatabaseCleanupWindowLauncher"]
code_paths: ["Engine/ColorVision.Engine/Mysql/DatabaseCleanupContracts.cs", "Engine/ColorVision.Engine/Mysql/DatabaseCleanupWindow.xaml", "Engine/ColorVision.Engine/Mysql/DatabaseCleanupWindow.xaml.cs", "Engine/ColorVision.Engine/Mysql/DatabaseCleanupWindowViewModel.cs", "Engine/ColorVision.Engine/Mysql/MySqlToolWindow.xaml.cs", "Engine/ColorVision.Engine/Services/DatabaseCleanup/SocketDatabaseCleanupWindowLauncher.cs", "UI/ColorVision.SocketProtocol/ISocketDatabaseCleanupWindowLauncher.cs", "UI/ColorVision.UI/AssemblyHandler.cs", "Projects/ProjectARVRPro/ArvrSqliteCleanupProvider.cs", "Projects/ProjectKB/KbSqliteCleanupProvider.cs"]
related: ["engine.index", "engine.mysql-maintenance", "ui.sqlite-storage", "ui.database", "ui.discovery", "operations.data"]
---

# 数据库清理窗口、能力接入与完成边界

`DatabaseCleanupWindow` 是多数据源的统计、备份、清理、优化和迁移宿主，实际文件位于 `Engine/ColorVision.Engine/Mysql/`，虽使用 `ColorVision.Database` 命名空间，却属于 Engine 的独立维护链路。宿主只调用 provider，不统一实现业务删除、事务、停写或恢复。

本窗口没有 DatePicker、逐行删除预览或预览批准令牌。它提供表统计、保留月数、可选的选表清空，以及 provider 专有的手动优化和数据迁移。若问题是“预览后只删除这些批次”，应先纠正这个功能前提，再读 [MySQL 结果维护](./mysql-maintenance.md) 或实际 SQLite owner；不能从表格行数推断删除范围。

清理、优化、迁移和备份会写真实数据库或文件。阅读本页不授权执行这些动作；必须先确定目标、业务停写、权限、备份与恢复条件。窗口构造、`OpenWindow` 和 provider 接口没有统一管理员鉴权，具体调用入口必须自行维护授权检查；调用入口可见或方法可调用不代表操作已经获准。

## 数据源发现与窗口范围

无参数 `OpenWindow()` 建立全局窗口，默认 ViewModel 通过 `AssemblyHandler.RefreshAssemblies()` 和 `LoadImplementations<IDatabaseCleanupSourceProvider>()` 实例化 provider，再按 `Order`、`DisplayName` 排序。该刷新会重建程序集列表并清空接口实现类型缓存，但不会替换已经打开的维护窗口持有的 provider 实例；发现规则见[程序集与扩展发现](./../../02-developer-guide/core-concepts/extensibility.md)。这里不扫描磁盘上所有 `.db` 文件，也不保证未加载的项目 provider 可见。

`OpenWindow(owner, source)` 只注入一个 provider，Socket 使用此入口。MySQL 工具窗口的“数据清理”和本机“流程分析”中的“数据库清理”均打开全局窗口。清理目标由维护窗口内选择的数据源和确认操作决定。离线只读的流程分析隐藏清理入口。

必须保留真正的 `public static void OpenWindow()` 无参数重载：已发布的 ARVR、KB 等项目插件可能仍引用这个二进制签名。把它替换为带可选参数的方法，只能兼容重新编译的源码，旧 DLL 点击“数据清理”会抛 `MissingMethodException`。无参数重载转入同一全局窗口逻辑，不改变单源范围或清理行为；

静态窗口表按不区分大小写的 `global` 或 `source:{Id}` 复用窗口，不按数据库连接、账号或文件路径区分。同 ID 再次调用只激活原窗口，不替换 provider，也不重新自动统计。全局和单源窗口可并存；直接构造窗口不走这个复用表。窗口范围是 UI 路由约束，不是数据库权限或进程级互斥机制。

生产入口设置 `refreshOnLoad: true`，第一次 Loaded 调用 `RefreshAllAsync()`，以 `Task.WhenAll` 统计该窗口的**所有** sources；全局模式不是只读取当前选中项。单源只统计注入来源。提供者构造、描述属性和 `LoadTables` 是否会初始化目录/schema/连接需由 owner 核对，不能为回答“有哪些数据源”而直接打开真实维护窗口。

MySQL 在统计前取得已验证的连接。统计结果与清理或优化确认显示该连接的库名和主机端口，备份、执行及操作后的统计沿用同一目标。更换连接会清除旧统计与勾选项；若连接在确认期间更换，确认失效，用户需要刷新统计后重新选择。该绑定只确定目标库和当前连接，表统计仍是整表汇总。

## 接口决定能力，不决定安全保证

| 接口 | 宿主使用方式 | 不可推断 |
| --- | --- | --- |
| `IDatabaseCleanupSourceProvider` | 身份、描述、排序、`LoadTables`、`CleanupHistory(keepMonths)`、`CleanupAll` | 不提供统一预览、删除集合、日期列或事务 |
| `IDatabaseCleanupSelectionProvider` | 显示复选和“清空勾选表”，传入表名列表 | 不自动补齐主从依赖或验证 provider 的白名单 |
| `IDatabaseCleanupBackupProvider` | 显示单独“创建完整备份”按钮，内部保留清理前备份能力 | “完整”内容由实现决定，不包含自动还原承诺 |
| `IDatabaseCleanupMaintenanceProvider` | 将备份和动作委托给 provider 的组合入口 | 同一维护锁不等于同一数据库事务，也不锁住其它进程 |
| `IDatabaseCleanupMigrationProvider` | 显示 provider 的迁移按钮和确认文案，并通过 `HasPendingMigration()` 判断当前库是否仍需迁移 | 直接 API 不因宿主存在就自动备份或获得授权；旧 provider 未实现检查时默认保留可执行状态 |
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

ARVRPro 和 KB 在 SQLite 事务中提交同一次清理的表删除。KB 全部清理仍保留当天未结束的会话。全部清理保留结果表的自增序列，新测量会使用更大的结果 ID。历史窗口和导出按原 ID 读取明细。清理后迟到的保存找不到原行时跳过回写，旧结果也不会重新进入列表。事务提交后才尝试回收空间并刷新项目窗口。

## 表统计与确认固定了什么

`RefreshAsync` 在后台调用 `LoadTables` 后替换 `Tables`，并通过 `HasPendingMigration()` 刷新迁移状态。相同来源按表名保留仍有效的选择。`ExistingRowCount` 和空间是 provider 返回值的加总，没有共同的快照时间点。MySQL 刷新失败时清除旧统计，SQLite 刷新失败时保留上次成功结果。迁移按钮在完成首次状态检查前保持禁用。

通常按钮要求 `!IsBusy` 和至少一张存在的表；选表入口还要求 selection 能力及非空勾选。保留月数输入实时校验，必须是大于 0 的整数，无效时提示错误并禁用历史清理按钮。这是当前快照的可执行状态，不是“已完成针对本次删除的预览”门禁。直接 provider 调用不依赖这些 UI 条件。

历史清理按保留月数处理当前数据库，不受表格勾选影响；“清空勾选表”删除所勾选表内的全部记录，不受保留月数限制。表统计和勾选行数都不是历史清理的待删除数量，窗口不显示截止日期预览。

“全部数据清理”区域默认展开，仅处理当前数据库，执行前仍二次确认该数据源的全部可清理表范围及不可撤销提示。

| 确认入口 | 已捕获参数 | 未固定的状态 |
| --- | --- | --- |
| 保留月数清理 | 默认文本为 `3`；实时校验正整数，在确认前捕获 `keepMonths` | 没有统一最大月数；截止时间由 provider 计算，不保存待删除行集合，也不受当前勾选表限制 |
| 清空勾选表 | 确认前捕获存在且勾选的表名，交给 `CleanupTables` | 清空所选表的全部记录，主从依赖仍需现场核对 |
| 清空当前库可清理表 | 弹窗显示快照中的可用表数量 | `CleanupAll()` 执行时重新发现目标库内的可清理表 |
| 优化 | provider 的说明与二次确认 | 执行时读取 schema 和已有索引，数据库负载及临时空间会变化 |
| 迁移 | provider 的说明及强制备份提示 | 没有通用 dry-run、版本批准或恢复协议 |

确认固定了 MySQL 的连接目标，表结构、数据和执行时间仍会变化。“清空勾选表”的主从关联提示由操作员核对，MySQL 的表关系边界见[清理契约](./mysql-maintenance.md#选表与全部截断)。

## 备份、执行与失败分层

窗口不显示备份策略卡片或清理前备份复选框；支持备份的 source 仍提供独立“创建完整备份”按钮。每个 source 的 `BackupBeforeCleanup` 默认 false，普通清理默认不自动备份；内部仍保留该属性及清理前备份执行能力，不作为界面的持久策略。所有清理仍需二次确认，确认框说明本次是否自动创建备份；provider 不支持备份时会提示需已有可恢复副本，但宿主不验证副本。单独点击创建备份也不会登记一个后续清理必须匹配的批准记录。

迁移入口不同：缺少 backup 能力直接拒绝；经用户确认后会再次调用 `HasPendingMigration()`，没有待迁移内容时直接禁用按钮并结束，不创建重复备份；仍需迁移时才以 `forceBackup: true` 调用执行包装。优化入口则明确关闭可选备份路径：即使内部将当前 source 的 `BackupBeforeCleanup` 设置为 true，宿主也不会为 `ExecuteOptimization()` 自动创建备份。优化确认中的“不删除业务数据”只描述该 provider 的动作范围，不代表 DDL 没有持久 schema 变更、可以事务回滚或无需按现场制度留存备份。

备份和动作按以下方式运行：

- 有 maintenance 能力：仅调用 provider 的 `ExecuteCleanupWithBackup(action)`；由它负责备份、锁和动作顺序，宿主不再额外重复备份。
- 只有 backup 能力：先 `CreateBackup()`，正常返回才执行 action；两者之间没有宿主统一维护锁或数据库事务。
- 普通清理未启用备份：直接执行 action；底层若有单独锁或事务仍以实际 provider 为准。

备份阶段失败不会继续调用清理/迁移动作，但不能扩大为“整个入口绝无先前副作用”：初始化、描述读取或 provider 的备份本身可能已经触发动作。组合入口失败时宿主提示如已生成备份则保留，并要求重新确认现状；宿主没有自动恢复或补偿事务。迁移成功后，迁移前完整备份也会继续保留用于人工恢复，不自动删除；空间不足时应在确认新版本读取正常后，由用户按现场留存制度清理备份目录。

普通清理和迁移动作成功后另行调用 `LoadTables` 与 `HasPendingMigration()` 刷新；迁移已完成时按钮立即禁用。刷新失败会保留动作成功结果并加警告，不把已经完成的动作回滚；再次点击迁移时仍会先复核状态，从而避免为了“无需迁移”再复制一份完整数据库。操作失败分支直接报告错误，不自动刷新统计。优化成功后不自动重新统计表，索引大小等显示值仍是旧快照，需由用户显式“刷新统计”。因此成功文案、最新统计、备份可恢复和库健康是不同证据。`DatabaseCleanupExecutionResult` 只是状态文字/摘要，没有统一的提交凭据或跨库成功协议。

## 忙碌、关闭与并发

工作以 `Task.Run` 执行，UI 更新通过 Dispatcher；`IsBusy` 是每个 source ViewModel 的状态。当前来源的操作卡片在忙时禁用，但不是所有窗口和来源共用的锁。全局/单源窗口对同一库可能有不同 ViewModel，运行中切换来源也不会使原操作变成新来源；实际互斥由 provider 自己提供。

窗口没有取消按钮、CancellationToken 或 Closing 阶段的等待/拒绝协议。确认框取消只阻止尚未开始的动作；关闭已运行窗口不取消后台数据库操作、不回滚、不终止外部工具。关闭后窗口注册项被移除，重开可能建立另一套状态，不能用“新窗口不忙”判断旧维护已结束。

`IsBusy` 门禁只约束当前 ViewModel。直接调用 provider 或使用其它窗口实例时，调用方仍负责授权、固定目标和协调停写。
