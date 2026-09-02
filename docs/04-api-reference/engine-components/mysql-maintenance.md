---
knowledge_id: "engine.mysql-maintenance"
knowledge_type: "topic"
status: "current"
summary: "MySQL 批次与结果表的历史删除、整表截断和SQL备份；统计不是清理预览，无全程事务或自动恢复，主从选择和管理员权限不能只依赖界面提示。"
aliases: ["MySQL结果清理", "删除批次", "历史结果清理", "结果表备份", "完整SQL备份", "清理事务回滚", "主从表清理", "清理管理员权限", "MySqlResultCleanupProvider", "CleanupHistory", "CleanupTables", "FindUnknownDetailTables", "ValidateCleanupTableNames", "MySqlLocalServicesManager", "BackupAllMysql", "mysqldump", "FOREIGN_KEY_CHECKS", "t_scgd_algorithm_result_master", "t_scgd_measure_batch"]
code_paths: ["Engine/ColorVision.Engine/Mysql/MySqlResultCleanupProvider.cs", "Engine/ColorVision.Engine/Mysql/MySqlLocalServicesManager.cs", "Engine/ColorVision.Engine/Mysql/MySqlToolWindow.xaml", "Engine/ColorVision.Engine/Mysql/MySqlToolWindow.xaml.cs", "Engine/ColorVision.Engine/Mysql/DatabaseCleanupWindowViewModel.cs", "UI/ColorVision.Database/MySqlControl.cs", "UI/ColorVision.Database/MySqlSetting.cs", "UI/ColorVision.Database/MySqlProtocolDefaults.cs"]
test_paths: ["Test/ColorVision.UI.Tests/DatabaseCleanupWindowTests.cs", "Test/ColorVision.UI.Tests/MySqlBackupRestoreSafetyTests.cs"]
related: ["engine.database-maintenance", "engine.mysql-recovery", "engine.results", "ui.database", "ui.sqlite-storage", "operations.data"]
---

# MySQL 结果清理、备份与失败边界

当前 MySQL 工具窗口的“数据清理”按钮打开[统一维护窗口](./database-maintenance.md)，结果表由 `MySqlResultCleanupProvider`（ID `mysql-results`）处理。这个链路只有表统计与保留月数，没有逐批次/逐行删除预览或 DatePicker，也不接受一组经预览批准的记录 ID。

清理会删除真实结果数据；备份会运行外部 `mysqldump` 并写 SQL 文件。本页不是运行授权或清理脚本。必须确认目标库、停写、关联表、备份和恢复条件后再执行；不要为了验证文档调用 provider、命令或恢复接口。该 provider 不内置管理员/RBAC检查和确认弹窗，不能把方法可调用、界面可点或持有数据库凭据当成获准操作。

## 责任与目标表范围

provider 的 `CleanupTableDefinitions` 是结果清理白名单，核心分为四组：

| 表组 | 当前用途与关联 |
| --- | --- |
| `t_scgd_algorithm_result_master` | 算法结果主表 |
| 已登记 `t_scgd_algorithm_result_detail_*` | 算法明细，历史清理优先按 `pid → 主表.id` |
| `t_scgd_measure_batch` | 测量批次主表 |
| 已登记 `t_scgd_measure_result_*` | 测量明细，历史清理优先按 `batch_id → 批次.id` |

具体登记表以该数组为准，不因为新表具有相同前缀就自动允许清空。结果清理不是数据库重置；资源、模板和配置类表不在这组清理白名单中。表内图片路径也不代表其外部文件会随清理一起删除或随备份进入 SQL。

`MySqlDatabaseMaintenanceService` 的 [SQL恢复、重置与资源保留](./mysql-recovery.md)是另一条契约，不能因名称含 Maintenance 就把它当成本页清理实现。`MySqlLocalServicesManager` 还留有旧 `CleanupHistoryCommand` / `CleanupAllResultTablesCommand` 实现；当前窗口按钮不绑定这些旧清理命令，不能将旧链的确认、表发现或失败行为套到新 provider。

## 统计不是清理预览，连接也未冻结

`LoadTables` 从 `INFORMATION_SCHEMA.TABLES` 取白名单表的 data/index 大小，再逐表 Count。返回的是整表统计，不按将要使用的保留月数过滤；多个查询没有共同快照，统计行数不是将要删除的行数。清理执行会重新读取表/列信息，并非照着之前的统计逐行删除。

client 和 schema 查询都从当前 `MySqlSetting.Instance.MySqlConfig` 取配置；组合维护门不会冻结配置对象。不能只凭弹窗中曾显示的数据库名保证备份、删除和后续统计始终使用同一配置，应在维护期间禁止其它路径改变目标并核对实际连接。

## 按月历史删除

`CleanupHistory(keepMonths)` 进入维护门后，以执行时本地 `DateTime.Now.AddMonths(-keepMonths)` 为截止点。窗口先校验正整数，provider 公开方法没有同样校验；时间并非确认弹窗时冻结，也不是按日历整天或服务器时区自动对齐。比较为 `< @cutoffDate`，相等记录不在该条件中。

删除前先发现当前库已登记表及同前缀明细表。若有未登记的 `t_scgd_algorithm_result_detail_*` 或 `t_scgd_measure_result_*`，`FindUnknownDetailTables` 会拒绝本次历史清理；这个门禁不覆盖后述选表/全部截断路径。

时间列按 `create_time`、`create_date`、`add_time` 优先匹配。历史清理顺序是：

1. 已登记算法明细：主表存在、有可识别时间列且明细有 `pid` 时，INNER JOIN 主表，按主表日期删明细；否则尝试明细自己的时间列。
2. 算法主表：有可识别时间列才按日期删除。
3. 已登记测量明细：优先 INNER JOIN 批次表并按批次日期删除，否则尝试自己的时间列。
4. 测量批次表：有可识别时间列才按日期删除。

缺少关联条件与可识别时间列时，该项可跳过或报告 0 行，并不阻止后续继续处理主表；走 INNER JOIN 时未关联到父行的孤儿记录不因此被清理。成功文字不证明所有过期或孤儿数据都已消失。时间值参数化，表/列标识符使用反引号转义，但并不据此构成完整 schema/业务关联验证。

此路径没有包围全部删除的 `BeginTran` / `CommitTran` / `RollbackTran`。按顺序先删子表只降低部分关联风险，不提供整体回滚；中途异常之前的删除可能已经生效。`SummaryLines` 只在正常返回时交给宿主，异常后不能用未显示汇总来判断零变更。

## 选表与全部截断

`CleanupTables` 的校验只做：非空输入、仅允许登记表名、忽略大小写去重、按依赖类别排序。`CleanupAll` 则传入全部登记表名。执行时重新查当前库，跳过不存在的表；“全部”是登记的结果表，不是所有数据库表。

实际顺序与按月清理不同：算法明细 → 测量明细 → 算法主表 → 测量批次；同组再按表名。代码先发 `SET FOREIGN_KEY_CHECKS = 0`，逐表 `TRUNCATE TABLE`，finally 尝试设置回 1。没有整体事务、自动备份或自动恢复，也不保存并恢复进入时的检查开关值；异常可能发生在已有表清空之后，finally 失败也不是补偿成功。

**当前实现缺口：** 选中主表时并不强制同时选择所有关联明细；选表和全部截断也不调用未知明细检测。XAML 的“需同时选择所有现存关联明细表”只是提示，白名单和排序不足以保证选择完整，恢复外键检查也不是已清空数据的恢复。不能在 AI 操作计划中把这个前提省略，或把上述实现描述为已经安全处理任意扩展表。本轮只记录源码事实，未修复产品逻辑。

## 维护门、完整备份与恢复的区别

`MySqlLocalServicesManager.RunDatabaseMaintenance` 使用进程内 `SemaphoreSlim`，以 `AsyncLocal<int>` 允许同一维护调用链嵌套。它串行化使用该入口的维护动作，不停止采集/流程，不阻止其它 DAO、浏览器、直接 SQL 或外部进程写入，也不是数据库事务。等待入口没有用户取消协议。

`CreateBackup()` 调用 `BackupAllMysql()`；组合 `ExecuteCleanupWithBackup` 在同一维护门内先备份，再执行传入动作。普通清理是否自动备份由[维护窗口策略](./database-maintenance.md#备份、执行与失败分层)决定。provider 自身的 `CleanupHistory` / `CleanupTables` / `CleanupAll` 不先备份。

完整 SQL 备份的实际过程是：

- 从当前库枚举 `BASE TABLE`，将库名和表名交给配置的 `mysqldump.exe`；不是仅备份清理白名单，也不是 MySQL 服务器上所有库或整个项目工作区。
- 写入默认“我的文档/ColorVision/Backup”下的唯一 `.sql.part`，正常完成并确认非空后改名为最终 `.sql`，再加入管理器备份列表。路径以实际 `BackupPath` 为准；失败尝试删除临时文件。
- 不试恢复、不逐表比对导出内容或验证业务数据一致性；“退出码为零 + 非空文件”不是恢复演练通过。

本入口不传 `dataOnly: true`；只有另一种 data-only 模式才显式添加 `--single-transaction`、`--quick` 等参数。因此不能给完整备份承诺代码显式建立了跨表一致快照；实际还受工具版本、默认选项、表引擎和并发写入影响。代码也没有显式启用存储过程/事件的备份选项。外部图片、项目 SQLite、其它库和服务配置不由这份 SQL 一起保护。

`BackupMysqlResource` 是另一种资源迁移备份，表清单和 `--replace`/依赖补充不同，不应拿它代替清理前完整结果备份。组合清理失败不自动调用恢复；MySQL provider 也没有像 Socket/Flow 包装那样把已生成备份路径追加进异常。文件可能已经生成，但宿主没有收到正常组合结果时，不保证失败弹窗列出它，需单独核对备份目录/列表。

手动SQL恢复不是撤销按钮；底层导入与桌面“导入→配置同步→注册中心重启”的完成条件不同，详见 [MySQL恢复契约](./mysql-recovery.md)。恢复须独立授权，不能在清理异常后自行触发。

## 外部工具与错误边界

MySQL 进程使用 `ProcessStartInfo.ArgumentList`，不通过 shell 拼接重定向，配置 UTF-8 MB4，密码放子进程 `MYSQL_PWD` 环境变量而不是命令行。环境变量并非加密秘密存储；工具错误文本会进入异常，没有统一字段脱敏，分享输出前仍须检查。

导出/导入按流处理标准输入输出，等待进程和流任务；总执行预算为 2 小时。超时或流失败会尝试终止整个子进程树，并最多等待 15 秒观察收尾；终止本身可能失败并被记录。这个内部超时不等于维护窗口提供取消，更不保证终止客户端就回滚此前已执行 SQL。备份最终文件与业务恢复完成仍需分开核验。

## 验证范围

`DatabaseCleanupWindowTests.cs` 检查白名单拒绝、去重和顺序、未知明细检测辅助方法，以及 fake 组合维护分派；没有证明真实主从选择闭包或数据库事务。`MySqlBackupRestoreSafetyTests.cs` 检查进程参数、字符集、密码不在命令行、方法返回路径的类型、进程内维护门串行/嵌套，以及源码中入口/防护代码的存在。

这些测试不是实际 `mysqldump/mysql`、生产表引擎、并发写入、删除后恢复、截断中途异常或断电恢复演练。
