---
knowledge_id: "ui.sqlite-storage"
knowledge_type: "topic"
status: "current"
summary: "Socket 与 Flow 的 SQLite 正文 gzip 编解码、按ID读写、旧TEXT逐批迁移、WAL备份与VACUUM；通用工具不自动停写/备份/恢复，失败可能已有批次提交。"
aliases: ["SQLite文本压缩", "gzip正文", "明文迁移", "数据库压缩", "数据库文件大小", "消息库维护", "备份恢复", "停止写入", "VACUUM", "WAL", "quick_check", "GzipTextPayloadCodec", "SqliteGzipTextPayloadStore", "SqliteGzipTextMigration", "SqliteFileMaintenance", "SocketMessagePayloadStorage", "SocketMessagesSqliteCleanupProvider", "FlowNodeMessagePayloadStorage", "FlowDiagnosticsSqliteCleanupProvider"]
code_paths: ["UI/ColorVision.Database/GzipTextPayloadCodec.cs", "UI/ColorVision.Database/SqliteGzipTextPayloadStore.cs", "UI/ColorVision.Database/SqliteGzipTextMigration.cs", "UI/ColorVision.Database/SqliteFileMaintenance.cs", "UI/ColorVision.SocketProtocol/SocketMessagePayloadStorage.cs", "Engine/ColorVision.Engine/Mysql/SocketMessagesSqliteCleanupProvider.cs", "Engine/ColorVision.Engine/Mysql/DatabaseCleanupWindowViewModel.cs", "Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeMessagePayloadStorage.cs", "Engine/ColorVision.Engine/FlowProcessing/Diagnostics/LegacyFlowNodeMessagePayloadMigration.cs", "Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowDiagnosticsSqliteCleanupProvider.cs", "Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowDiagnosticsMaintenanceGate.cs", "Engine/ColorVision.Engine/FlowProcessing/Diagnostics/FlowNodeRecordDataBaseHelper.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SqliteGzipTextMigrationTests.cs", "Test/ColorVision.UI.Tests/SocketMessageStorageTests.cs", "Test/ColorVision.UI.Tests/FlowNodeMessageStorageTests.cs", "Test/ColorVision.UI.Tests/DatabaseCleanupWindowTests.cs"]
related: ["ui.database", "ui.database-query", "engine.database-maintenance", "ui.socket-protocol", "operations.data", "flow.session"]
---

# SQLite 正文存储、迁移与文件维护

`UI/ColorVision.Database` 的四个工具负责压缩正文和 SQLite 文件操作，不拥有 Socket 服务或 Flow 执行生命周期。正常读写、旧 TEXT 迁移、完整数据库备份、释放文件空间是不同操作；有 gzip 列不等于旧数据已经迁移，迁移异常也不等于全库未改。

本页解释代码契约，不授权执行迁移、清理、复制用户数据库或启动业务。真实维护须先确认数据所有者、目标文件、停写和连接释放条件、可用空间及恢复方案。数据库位置见[数据所有者](../../01-user-guide/data-management/README.md)，整表删除入口另见[通用查询](./database-query.md)。

## 编码、长度与预览

`GzipTextPayloadCodec.Encode` 使用严格 UTF-8 编码和 GZip，返回压缩字节及原始 UTF-8 字节长度。null 对应两者均 null；空串仍有 gzip 字节且长度为零，不与 null 混同。编码在内存中完成，`Encode` 本身没有 64 MiB 大小门禁。

`Decode` 默认最多解压 `64 * 1024 * 1024` 字节，既检查声明长度也限制实际解压量；负长度、声明超限、长度不符、损坏 gzip 或非法 UTF-8 会报错。压缩字节 null 但长度非 null 也被拒绝。长度为 null 时可解码非 null 的 gzip，但仍受实际字节上限限制。因此“能写入”不保证默认解码一定可读取超大内容。编解码不验证 JSON、业务字段、身份或加密完整性。

`CreatePreview` 默认取最多 256 个 UTF-16 code unit 的前缀，截断时避免留下单独的高代理项，并加省略号。它不是 UTF-8 字节数或完整字素边界，最终字符串可能比参数多一个省略号。Socket owner 传入 96；预览只用于列表，不是完整正文或恢复来源。

## 新列装配与按 ID 读写

`SqliteGzipTextPayloadStore` 接受调用方的 `SqlSugarClient`，不创建或释放业务连接，也不自动建立事务。

| 方法 | 保证与非保证 |
| --- | --- |
| `EnsureSchema` | 对已有表补充缺少的 BLOB、长度 INTEGER 和可选预览 TEXT 列；列名比较不区分大小写。不会创建表、验证已有列类型/约束或把旧明文转换为 gzip |
| `Save` | 要求正 ID，参数化 UPDATE 已存在的一行，写 gzip/长度/可选预览；影响行数非 1 则抛错。不插入、不 upsert、不清旧 TEXT，也没有自有事务来回滚已执行的 UPDATE |
| `Load` | 只取 gzip 和长度并解码；非正 ID、找不到行或存储值为 null 都可返回 null。不会回退到旧明文或用预览补正文 |

表名和列名只允许 ASCII 字母、数字、下划线，并用双引号包围；值通过参数传递。存在的列仍需由 schema owner 保证类型：读取对非 `byte[]` 的值使用 `as byte[]`，不能宣称所有错误列类型都会明确报错。调用方现有事务可以包含 `Save`，但工具不会自动把实体插入、正文更新和其它库写入合成一个事务。

Socket 正常 `Load` 和 Flow 的 `LoadPayloads` 都只读压缩列；实体列表不负责加载完整正文。混有旧 TEXT 的库需要显式迁移，不能承诺正常读取时明文与 gzip 自动混读或双写。业务层按 ID 取全文与发送/显示的责任见 [Socket 消息记录](./ColorVision.SocketProtocol.md)。

## 旧 TEXT 迁移：可重跑，但不是全库原子操作

`SqliteGzipTextMigration.Execute` 要求现有数据库文件和正批大小，默认每批 500。先统计主库/WAL/SHM 大小、做磁盘空间预检并要求 `quick_check` 返回 ok，再按传入 spec 顺序处理各表；没有先验证全部 spec 或开启一个覆盖全库的事务。

每个表先补压缩/长度/可选预览列，再检查旧列是否存在。旧列不存在时报告 `LegacyColumnExists = false`，但补列动作可能已发生。旧列存在时分两类：

1. 旧 TEXT 非 null、gzip 为 null：按正 ID 递增分批读入。内存中编码并解码做逐字一致性比较，然后在当前批次事务内同时写 gzip/长度/预览、将旧 TEXT 置 null。
2. 旧 TEXT 和 gzip 均非 null：必须有长度，解码并与旧 TEXT 完全一致后才在该批事务内清旧值；损坏、缺长度或不一致会报错，不以旧文自动覆盖 gzip。该行旧文保留不代表此前批次从未改动。

更新 WHERE 除 ID 外包含所读原值的匹配条件；影响行数必须为 1。每批独立提交，前面成功的批次、表和 schema 改动不会因后续失败一起回滚。遍历以 `lastId = 0` 开始，要求可读为 Int64 的正 ID；非正 ID 的待迁移记录最终会被残留计数发现并导致失败，不会自动转成新主键。

首次迁移的解码校验发生于内存中的编码结果，不是写库后的逐行读回。迁移仅把旧列值置 null，不删除旧 TEXT 列。重跑会跳过已迁移且无残留的行，仍执行后续空间整理；这是针对该数据布局的重入行为，不是崩溃恢复或任意并发写入的认证。

所有表处理后调用 WAL checkpoint，再执行 VACUUM 和最终 `quick_check`。后段失败时正文可能已经迁移和清空；VACUUM 阶段异常会提示空间释放失败，不能按“迁移失败”直接假定旧字段仍完整。工具本身没有创建备份、暂停业务、取消令牌或自动还原。

## 完整文件备份与空间回收

`SqliteFileMaintenance.CreateVerifiedBackup` 对来源做 `quick_check`，通过 SQLite `BackupDatabase` API 写入带 `.part` 后缀的目标，再检查目标并改名为最终时间戳 `.db`。这不是只复制主库文件；已有测试覆盖 WAL 中已提交记录进入备份。失败路径尝试清理 `.part`，不会恢复来源库。

工具仅检查备份目录名和前缀非空，不提供任意输入路径的包含性校验；调用方必须控制目标。它也不负责停写、自动清理旧备份、跨库/外部结果文件备份或恢复流程。`quick_check` 是数据库结构检查，不验证所有业务记录、图片或协议结果正确，当前 helper 读取的是该 PRAGMA 的首个结果值。

`VacuumAndCheck` 的顺序是统计 DB/WAL/SHM → 空间预检 → `wal_checkpoint(TRUNCATE)` → VACUUM → 再 checkpoint → `quick_check`。空间阈值为统计大小的两倍再加 512 MiB，这是预检而非磁盘空间预留。维护连接禁用 pooling，默认等待/`busy_timeout` 为 30 秒；checkpoint 返回 busy 会抛错，不强制解锁外部进程或关闭全部业务连接。

压缩字节更少、旧 TEXT 置 null 和主文件立即变小不是同一件事；释放已占页依赖 VACUUM 成功，体积变化取决于实际数据。已有连接不等于一定失败，但活跃读写/事务可能阻挡 checkpoint 或 VACUUM。不能把等待超时当作“可以保持业务运行直接整理”的保证。

## Socket / Flow 的维护包装负责什么

[数据库维护窗口](../engine-components/database-maintenance.md)的迁移入口以 `forceBackup: true` 执行，宿主的能力检测、确认、忙碌与失败显示由该主题说明。这里的 Socket/Flow provider 实现组合维护，在同一维护锁内先备份再执行动作；直接调用它们的 `ExecuteMigration()` 或底层 `Execute` 不经过窗口强制备份入口。

| 数据所有者 | 组合维护的实际边界 |
| --- | --- |
| Socket：`SocketMessagesSqliteCleanupProvider` | 在 `SocketMessagePayloadStorage.RunDatabaseMaintenance` 的进程内 lock 中创建备份，再执行迁移/清理；只约束遵守同一锁的路径，不停止 TCP 服务或强制关闭其它连接 |
| Flow：`FlowDiagnosticsSqliteCleanupProvider` | 非重入调用先 `FlushPendingWrites(10s)`，成功后进入 `FlowDiagnosticsMaintenanceGate` 再备份和执行动作；确认文案要求停止流程并关闭分析窗口，但代码不自动执行这些用户动作 |

Flow 的 Flush 可触发首次初始化、schema 装配和写线程启动，因此备份位于 payload 迁移动作之前，不等于整个维护入口在备份前绝无写入。写循环会记录并吞掉动作异常，队列 barrier 完成也不证明此前每条诊断记录都成功保存；随后新入队的动作还须按维护锁排队。

组合动作失败时包装异常保留已经生成的备份路径，**没有自动还原备份**。锁在退出时释放，后续遵循该锁的操作可继续进入；这不是停止/重开服务的协议，更不是库健康或所有后续写入成功的证据。Socket 的迁移后列表刷新使用 `Dispatcher.BeginInvoke`，方法返回也不证明 UI 已显示新数据。

遇到失败应固定备份文件和失败阶段，区分 schema、已提交批次、待迁移/冲突旧值、checkpoint 与空间整理状态；不能直接覆盖源库、删除残留或重新放行业务来“验证恢复”。后续核验、重跑或恢复须在明确授权和停写条件下进行。

## 验证证据与缺口

- `SqliteGzipTextMigrationTests.cs`：编解码/null/空串、损坏与长度限制、补列和按 ID 存储、超过 500 行的跨批迁移、重跑幂等、匹配/不匹配的旧值残留、已提交 WAL 备份、指定测试数据的 VACUUM 回收。
- `SocketMessageStorageTests.cs`、`FlowNodeMessageStorageTests.cs`：隔离临时库的压缩写入、列表不取正文、按 ID 读取及迁移；Flow 另含损坏正文和保护记录清理。
- 组合维护的 fake 测试范围见[维护宿主验证](../engine-components/database-maintenance.md#验证证据)，不作为真实数据库整体原子性的证据。

引用这些测试不表示本次运行。它们不覆盖生产库停写/重开、其它进程占用、磁盘耗尽、进程崩溃、后续批次失败的全库恢复或真实维护窗口的端到端验收；文档与检索构建通过也不能替代这些检查。
