---
knowledge_id: "delivery.backend-jobs"
knowledge_type: "topic"
status: "current"
summary: "Backend内置任务的注册、后台轮询、同步手动执行、SQLite单飞和启动恢复；任务返回、历史落盘与业务副作用不是同一成功边界。"
aliases: ["Backend调度", "Backend任务", "后台任务启停", "Scheduled Jobs", "SchedulerThread", "run_job_now", "scheduled_jobs", "job_runs", "SqliteJobRepository", "single-flight", "interrupted", "next_run_at", "job_history_retention", "startup_index_check", "plugin_index_check_interval_seconds"]
code_paths: ["Web/Backend/services/scheduler.py", "Web/Backend/services/artifact_index.py", "Web/Backend/ports/jobs.py", "Web/Backend/db/repositories/jobs.py", "Web/Backend/db/schema_version.py", "Web/Backend/db_cache.py", "Web/Backend/routes/admin_api.py", "Web/Backend/app.py"]
test_paths: ["Web/Backend/test_jobs_repository.py", "Web/Backend/test_schema_version.py", "Web/Backend/test_contracts.py", "Web/Backend/test_admin_data_retention.py"]
related: ["delivery.backend", "delivery.backend-auth", "delivery.backend-retention", "delivery.plugin-catalog", "delivery.backend-observability", "delivery.file-transfer"]
---

# Backend内置任务、执行记录与恢复

`services/scheduler.py` 持有内置任务分派和后台线程，`SqliteJobRepository` 持有定义及执行记录，`routes/admin_api.py` 提供查询与手动控制。本页不是 WPF Quartz 调度器，也不是 Operations Relay远端任务协议；不能把它当成可上传任意函数、脚本或cron表达式的通用任务平台。

启动线程、执行任务及启停定义均可能写数据库；任务还可能删除缓存、审计、快照或到期文件。以下契约不授予运行权限。服务组成、实际数据库路径与 `--storage` 不隔离数据库的前提见[Backend组成](./README.md)。

## 定义注册不等于线程已经启动

`app.py` 主入口处理CLI后调用 `ensure_default_jobs`，再按 `scheduler_enabled` 和debug reloader条件启动 `SchedulerThread`。默认允许调度；debug下仅 `WERKZEUG_RUN_MAIN == "true"` 的子进程启动线程。普通导入/WSGI装配不能据此推断已经走过主入口的定义注册和线程启动。

`ensure_defaults` 为缺失任务插入 `enabled=1`、初始间隔、`next_run_at=当前UTC时间` 和config。已有同ID只刷新name/job_type，不重置enabled、间隔、next_run_at、config或创建时间。异常回滚并打印，不向调用方抛出，因此“主入口继续运行”不证明所有定义已注册。

当前分派按固定 `job_id`，不是按数据库 `job_type` 动态加载处理器；保存的config字段也不是通用执行脚本。

间隔来自 `DEFAULT_JOBS` 的初始值及 `scheduled_jobs.interval_seconds`，当前没有读取旧README示例中的 `plugin_index_check_interval_seconds` 配置键；不能靠往config.json添加这个键来调整周期。下表为新增定义时的默认值，不覆盖已经持久化的间隔：

| job_id | 初始间隔 | 责任及权威边界 |
| --- | --- | --- |
| `plugin_index_check` | 300秒 | 签名/状态检查和全量刷新，详见[插件目录](./plugin-catalog.md) |
| `release_index_check` / `update_index_check` / `tool_index_check` | 各600秒 | 对应制品索引的签名/ready状态检查；相同签名且ready才跳过刷新 |
| `cache_cleanup` | 3600秒 | 删除 `cache_entry.expires_at <= 当前时间` 的记录 |
| `password_recovery_cleanup` | 3600秒 | 在线账号安全历史清理，不是清空在线账号或全部会话；见[账号生命周期](./accounts.md) |
| `transfer_file_cleanup` | 3600秒 | 到期临时Transfer文件/链接清理；期限与身份边界见[Transfer](./file-transfer.md) |
| `access_analytics_retention` | 86400秒 | 在线与快照访问数据保留，见[备份与保留](./backup-retention.md)及[观测数据](./observability.md) |
| `job_history_retention` | 86400秒 | 按开始时间清执行历史，同时保留每任务最新记录和所有running记录 |
| `admin_data_retention` | 86400秒 | 在线/快照审计、快照安全状态和旧快照轮换；见[备份与保留](./backup-retention.md) |
| `database_backup` | 86400秒 | 创建数据库快照并执行隐私清理、检查及轮换；不是仅复制文件 |
| `startup_index_check` | 0 | 线程启动先执行一次；后续tick的条件见下节，不是永久只运行一次 |

`artifact_index.py` 的签名检查不是递归内容校验：release记录存储根文件及符合发行bucket规则的 `History/{major}/{branch}/` 直接子项；update仅记录Update顶层文件，tool仅记录Tool顶层子项。签名依赖这些项的大小/修改时间，不遍历任意深层内容；不能保证子目录内任意变化都会触发周期刷新。

## 后台执行顺序与停止边界

`SchedulerThread.run` 首先把数据库中所有残留 `running` 标为 `interrupted`，然后直接调用 `startup_index_check`，最后进入轮询。这个首次启动检查不读取该定义的enabled，也不因旧success历史而跳过。恢复调用在启动检查的try之外；恢复数据库异常可令线程直接退出。

每轮 `_tick` 先取 `list_enabled()`，再顺序同步执行到期任务；单个任务占用线程时，后面的任务必须等待。轮次结束后分30次各睡1秒，期间检查stop标记；不是精确cron时钟、固定频率并行执行或停机期间逐次补跑。

- `next_run_at` 尚未到则跳过；缺失或解析失败会继续尝试，未附时区的时间按UTC。
- 周期任务完成记录时，以**完成时刻加持久化间隔**计算下次时间，业务success和error都进入这一计算；手动执行同样会推迟下次周期执行。
- tick遇到 `startup_index_check` 时，只要存在任一success历史便跳过，否则仍可再次尝试；这不限制线程启动前面的直接调用。
- `stop()` 只设置事件，不中断正在执行的handler。禁用只更新enabled：已取入本轮列表的任务、正在执行的任务和手动执行不因此取消；没有通过禁用回滚副作用的语义。

这些启动/恢复操作不等于 `/api/ready` 探测；ready也不验证调度线程存活、定义齐全或上次任务成功。

## 手动接口是同步执行，不是接受后排队

| 方法与路径 | 行为 |
| --- | --- |
| `GET /api/admin/jobs` | 按ID排序的定义、latest_run和run_counts；要求 `jobs:read` |
| `GET /api/admin/jobs/<id>/runs` | 执行历史分页；要求 `jobs:read` |
| `POST /api/admin/jobs/<id>/run` | 本次HTTP处理内调用 `run_job_now`；要求 `jobs:write`，不存在为404 |
| `POST .../<id>/enable` / `disable` | 要求 `jobs:write`；提交enabled和updated_at，不存在为404，不修改间隔/next_run_at |

表中省略前缀为 `/api/admin/jobs`。手动run只检查定义存在，不要求enabled，不依赖后台线程正在运行。它没有单独的等待队列、暂停点或取消API；不能把客户端超时/断开当作服务器未执行或已取消。

历史status只接受 `success`、`error`、`running`、`interrupted`，其它值400；limit默认20、范围1–100，offset默认0且非负。按执行ID降序分页，latest_run也是最大ID，不是最后完成时间。run_counts是**当前保留的记录**汇总，清理后会减少，不是从安装起永久累计的成功率。

凭据方式、普通Session permission与API key scope差异、CSRF先行拒绝规则见[HTTP认证](./authentication.md)。`jobs:write` 本身可以触发所有这些内置任务，包括备份和删除型清理；不能用只读历史接口的权限替代它。

## 单飞、失败与历史写入分开判断

`start_run` 先提交 `running` 行，再执行handler。schema v9创建部分唯一索引 `idx_job_runs_single_running ON job_runs(job_id) WHERE status='running'`；同一数据库同一job_id只能有一条running。冲突返回 `status=skipped`、`run_id=null`，HTTP409，不增加一条skipped历史；其它插入失败可直接抛出。

单飞不是所有后台工作的全局锁：不同job_id、绕过调度器直接调用的管理动作仍可并行。启动恢复没有进程owner、租约或心跳检查，直接改所有running；多进程共用数据库时，不能把它宣称成能辨别其它进程仍在执行的分布式调度协调器。schema迁移只修复重复running、保留每job最新一条；它也不代替线程启动恢复。

| 边界 | 可观察结果及限制 |
| --- | --- |
| handler抛异常 | 捕获为 `status=error`、summary/error；HTTP正常返回仍为200，必须读业务status |
| handler正常返回 | 初始status为success；可能只是“无变化”、底层刷新正在进行，或底层吞掉了错误，不是每个制品都已验证 |
| 完成历史提交 | `complete_run` 在一个SQLite事务更新run结果和next_run_at；但捕获异常后只打印，调用者仍可返回原success/error |
| 完成历史失败 | 原running可能留下并继续阻止同ID执行；先核对数据库历史及日志，不把HTTP成功当成历史已落盘 |
| 手动动作审计 | 路由在执行/启停之后尝试写审计；`CacheManager.write_audit` 失败只打印，不能证明审计与业务原子提交 |

例如 `cache_cleanup` 的底层清理失败可打印并返回0，任务仍报告清理0条；`database_backup` 可带轮换警告仍success。插件刷新内部状态及错误另见[插件目录](./plugin-catalog.md)，备份多阶段副作用另见[备份与保留](./backup-retention.md)，不要仅靠job succeeded确认完整业务链。

`interrupted` 表示历史被启动恢复标记，并不证明原handler没有完成部分删除、备份或刷新，也不续跑原进度。恢复后允许新run，需要按相应业务状态判断重试，而不是把旧任务当成从未执行。

## 执行历史保留与验证范围

`job_history_retention` 读取 `job_run_retention_days`（默认30，范围1–3650），用UTC当前时刻减天数作为cutoff。删除条件是 `started_at < cutoff`、非running，且不是该job的最大ID；不是按finished_at，也不会删除每job的最后一条记录。配置更新顺序见[保留策略](./backup-retention.md)。

`test_jobs_repository.py` 有默认元数据更新不重置运行状态、单飞、分页/计数、interrupted恢复、启停、完成结果与next_run_at同事务、保留latest/running及重复手动run用例；`test_schema_version.py` 有重复running迁移与唯一索引；`test_contracts.py` 有jobs读/写权限、分页参数、缺失job、409冲突及启停HTTP用例；每日备份注册/执行关联 `test_admin_data_retention.py`。

本次只核对现有源码与测试内容，未运行产品或测试。真实后台线程计时、停止中的长任务、多进程启动恢复竞争、完成记录提交失败后HTTP结果等没有在本页据此宣称已验收；静态路径映射也不是所有handler成功的证明。
