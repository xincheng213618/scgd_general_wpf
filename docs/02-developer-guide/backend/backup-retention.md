---
knowledge_id: "delivery.backend-retention"
knowledge_type: "topic"
status: "current"
summary: "Backend六项在线保留配置、SQLite快照创建、隐私清理和轮换；原子配置替换不等于全链事务，备份存在也不证明所有清理或恢复安全。"
aliases: ["运维保留策略", "Operational Retention Settings", "数据库备份", "create_database_backup", "database_backup", "run_admin_data_retention", "admin_data_retention", "marketplace_backup", "backup_security_scrub", "admin_db_backup_keep_count", "audit_log_retention_days", "OPERATIONAL_RETENTION_SETTINGS", "persist_config_values"]
code_paths: ["Web/Backend/services/database_backup.py", "Web/Backend/services/admin_data_retention.py", "Web/Backend/services/account_security_cleanup.py", "Web/Backend/services/operational_settings.py", "Web/Backend/services/config_persistence.py", "Web/Backend/services/access_analytics.py", "Web/Backend/services/scheduler.py", "Web/Backend/db_cache.py", "Web/Backend/routes/admin_api.py", "Web/Backend/marketplace_services.py", "Web/Frontend/src/pages/SettingsPage.tsx"]
test_paths: ["Web/Backend/test_operational_settings.py", "Web/Backend/test_admin_data_retention.py", "Web/Backend/test_db_cache.py", "Web/Backend/test_account_security_cleanup.py", "Web/Backend/test_access_analytics.py", "Web/Backend/test_contracts.py"]
related: ["delivery.backend", "delivery.backend-jobs", "delivery.backend-accounts", "delivery.backend-auth", "delivery.plugin-catalog", "delivery.backend-observability"]
---

# Backend保留配置、数据库快照与隐私清理

`operational_settings.py` 定义允许在线修改的六项保留配置；`database_backup.py` 组合SQLite备份与清理，`admin_data_retention.py` 负责审计和快照轮换，`account_security_cleanup.py` 区分在线安全历史清理与快照安全状态移除。这里不是制品目录备份、NAS部署回滚，也不是WPF业务数据库维护。

创建快照、保存策略和执行清理均为写操作；缩短保留期可能删除文件和数据库记录，不提供回收站或统一回滚。服务实际数据库及配置路径见[Backend组成](./README.md)，不能把 `--storage` 或临时制品目录当成数据库隔离。调度/手动执行边界见[内置任务](./jobs.md)。

## 六项配置及生效时机

`GET /api/admin/settings/retention` 返回 `values`、各项minimum/maximum和 `restart_required=false`；GET与PUT均要求 `settings:manage`。这不是允许改凭证、secret、storage、监听、scheduler或Copilot配置的通用接口。

| 配置键 | 默认 / 范围 | 后续消费者 |
| --- | --- | --- |
| `app_release_keep_count` | 5 / 1–100 | 主程序历史协调使用的保留数量 |
| `plugin_package_keep_count` | 3 / 1–100 | 每插件包历史协调使用的保留数量，详见[插件目录](./plugin-catalog.md) |
| `access_analytics_retention_days` | 90 / 1–3650 | 访问数据清理及新快照访问数据清理 |
| `job_run_retention_days` | 30 / 1–3650 | job_history_retention；保留latest/running的例外见[执行历史](./jobs.md) |
| `audit_log_retention_days` | 365 / 1–3650 | 在线及识别到的快照审计清理 |
| `admin_db_backup_keep_count` | 10 / 2–1000 | 识别到的数据库快照数量轮换，保护项可能使实际数量超过此值 |

GET尝试将现有值转int，缺失、bool、不能转换或越界时显示默认值；这是该接口的有效值计算，不是修复磁盘原配置，也不证明所有其它消费者都以同样方式容错。

`PUT /api/admin/settings/retention` 必须只有一个 `values` 对象，而且必须完整给出六个键；缺项、多项、bool、字符串数字、非int或越界均400。不是PATCH局部修改，也没有revision/If-Match并发前置条件。

保存顺序是：校验全部值 → 进程内锁下读取已有JSON并保留未公开键 → 写同目录唯一临时文件、flush/fsync → `os.replace` 替换配置 → 更新当前进程live mapping → 按changed尝试写审计 → 返回updated/unchanged。临时文件写入或replace失败不先更新live配置；读取/持久化的OSError或ValueError在路由返回500。

原子边界是一次配置文件替换，不是多进程锁、所有业务数据事务或其它进程配置同步。并发客户端的全量六项提交仍可能覆盖彼此的旧读值；审计写入失败只打印，不撤销配置。`restart_required=false` 表示这六项供当前进程后续读取，不等于PUT立即运行所有清理、撤销在途操作或保证定时任务已经启动。

`SettingsPage.tsx` 保存前显示本地检测到的逐项旧值→新值，并对缩短保留范围提示下一次发布/清理可能删除数据；确认框不是实时枚举待删文件，也不提供后端并发版本保护。`marketplace_services.py` 的主程序/插件历史协调在调用时读取相应保留值；详细文件选择与发布行为不由本页复制定义。

## 备份接口与路径范围

| 方法与路径 | 契约 |
| --- | --- |
| `GET /api/admin/backup/db` | 要求 `backups:manage`；返回backups、count、keep_count，常规条目只有name、UTC created_at和size_bytes |
| `POST /api/admin/backup/db` | 同权限，同步执行 `create_database_backup`；正常200及status=ok，创建/清理异常500 |

这两个接口不提供快照下载或恢复动作。正常创建响应主动移除内部 `backup_path` 字段；错误消息和 `backup_retention.errors` 仍来自异常文本，不能扩大成任何失败响应都已脱敏所有本地路径的保证。认证与CSRF见[HTTP认证](./authentication.md)。

快照位于 `cache.db_path.parent`，不是任意传入的制品目录。名称为 `marketplace_backup_YYYYMMDD_HHMMSS.db`，按UTC生成；同秒碰撞依次尝试未来0–59秒，均被占用则失败。`created_at` 来自文件名解析，不是文件系统创建时间，也不是某个真实数据库事务的精确时间戳。

`_BACKUP_LOCK` 在本进程串行化管理接口与每日database_backup的创建流程。它不是跨进程文件锁，也不覆盖其它独立保留job；不要把命名探测加 `os.replace` 当成多进程不会覆盖同名目标的保证。

## 一次创建包含哪些步骤

1. `CacheManager.backup_db` 使用SQLite连接的 `backup()` 把在线库复制到同目录临时库，commit并做 `PRAGMA quick_check`，通过后用 `os.replace` 放到最终快照名。它能包含已提交WAL内容，拒绝源/目标同一路径；不是直接只复制主 `.db` 文件。
2. 对**本次新快照**调用 `prune_access_analytics_database`，按当前访问保留策略删除过期数据并做quick_check。
3. 调用 `run_admin_data_retention`：先清在线审计，再逐份清快照审计、安全状态，最后轮换旧快照；当前新快照加入轮换保护集。
4. 检查本次新快照是否出现在审计/安全清理失败路径中；若出现，尝试删除该新文件并抛错。其它清理异常同样进入删除新快照的失败分支。
5. 返回名称、大小、访问/安全清理计数及 `backup_retention`；管理路由再尝试写db_backup审计。定时任务使用同一个创建服务。

最终名字在隐私清理前已经可见，所以目录中出现文件不等于整个创建完成。若删除新快照本身失败，也不能声称失败后文件必不存在。清理之前或期间已提交的在线/旧快照修改不会随本次失败回滚；底层SQLite一致性和quick_check不等于制品、配置、数据库及运行中业务的统一恢复点。

新快照访问数据清理与安全清理计数不是同一范围：`access_analytics_deleted` 针对新快照，`security_rows_deleted` / `security_accounts_invalidated` 来自本次扫描的全部成功安全清理快照汇总。不要将它们解释为仅本次文件的独立计数。

## 快照安全清理不是所有凭据吊销

`scrub_account_security_database` 在单个SQLite事务内删除快照中的以下临时表内容（表缺失则记0）：`user_sessions`、`login_attempts`、`registration_rate_limits`、`password_recovery_rate_limits`、`password_recovery_requests`。它不对在线库执行这份“全部清空”操作。

快照用户密码hash、资料、角色和权限仍保留；若users有 `auth_version` 且尚未到 `BACKUP_SECURITY_SCRUB_VERSION=1`，则全部账号版本加1，并在有schema_version表时保存 `backup_security_scrub` 标记。重复处理已有标记的快照不再反复加版本；旧结构缺少对应列/标记表的行为须按该分支核对。quick_check在此事务commit前完成，失败回滚本次安全清理。

这些措施阻止把被复制的数据库会话记录当作可恢复登录状态，但**不等于所有认证材料均已轮换**：配置管理员cookie不走数据库auth_version；API key记录不在上述移除表中；secret_key与upload_auth也不属于这个数据库清理动作。恢复后的身份边界仍需分别核对[账号生命周期](./accounts.md)与[API key认证](./authentication.md)，不能沿用“任何旧浏览器cookie或key都绝不复活”的笼统承诺。SQL删除与quick_check也不是磁盘安全擦除或完整隐私认证。

在线 `password_recovery_cleanup` 则先推进找回申请过期，再事务清理闲置/已撤销会话、过期限流来源和已结案找回历史；不是删除全部在线会话。30天闲置和安全历史规则由[账号主题](./accounts.md)维护，它们不属于这六个可在线修改的保留字段。

## 审计、访问数据与文件轮换是独立阶段

`run_admin_data_retention` 的顺序及故障边界：

- 在线audit按UTC当前时刻减天数，删除 `created_at < cutoff`，等于cutoff保留；在线阶段异常直接中止后续阶段。
- 快照audit逐份提交删除并检查quick_check；一份失败记录errors/errorPaths，继续其它文件。删除提交后才检查的阶段，检查失败不代表此前删除被回滚。
- 快照安全清理跳过audit失败文件，各文件独立事务；失败继续收集。
- 最后轮换快照，将audit失败、安全清理失败及显式protected_paths一起保护。总体status=error时仍可能已经清了在线数据、改了其它快照并删掉一批旧文件。

`access_analytics_retention` 是另一个job：先清在线访问数据，再逐份清快照访问数据；快照出错汇总后让job报error，不回滚已完成部分。`admin_data_retention` 本身不负责访问数据删除；新备份创建也只先清新快照的访问数据，并不借此清所有旧快照访问表。报表日、指标表与隐私聚合含义统一见[观测数据](./observability.md)。

审计/安全/轮换只识别数据库目录内、名称严格匹配上述模式的普通文件，排除符号链接和解析到目录外的路径；清单另外要求名称可解析为真实日期。轮换按名称降序取最新keep_count，再合并保护集，而非按mtime或文件内容健康度排序。非匹配文件不会因数量超限被删除，保护文件和删除错误都可能使afterCount高于keepCount。

这是 `admin_data_retention.py` 的文件边界，不能推广给所有清理器：访问数据快照扫描仅检查名称模式和resolve后的父目录，没有相同的 `is_file` / `is_symlink` 排除条件。也不能把未命名为规范快照的任意副本当作自动受到全部隐私保留处理。

## 结果与失败应该怎样读取

| 返回/状态 | 正确解释 |
| --- | --- |
| 新快照安全/审计清理失败 | 创建报错并尝试删除新文件；在线审计和旧文件可能已改变 |
| 旧快照坏库、审计/安全清理失败 | 记录并保护该文件；其它阶段可继续，保留数量可能超过上限 |
| 旧文件轮换删除失败 | `backup_retention.errors` 和status指出问题，不恢复已删除文件 |
| 创建HTTP status=ok | 本次新快照通过所执行步骤；仍须查看 `backup_retention.status/errors`，不能推断全部旧快照处理成功 |
| database_backup job success | 可带retention warning；不同于admin_data_retention job遇汇总errors后报error |

`backup_retention` 保留keepCount、beforeCount、afterCount、removedCount、removedBytes、preservedUnclassified及错误，并附在线/快照审计、安全清理汇总。文件大小大于0、ready成功、job success和历史中出现备份名，都不能替代新快照检查、旧快照错误处理及实际恢复演练。

## 验证范围与缺口

`test_operational_settings.py` 覆盖精确allowlist、范围、未公开配置保留及replace失败不更新live；`test_contracts.py` 有设置GET/PUT、权限、审计、备份创建/清单不暴露正常path字段。`test_db_cache.py` 覆盖已提交WAL内容和拒绝源库覆盖。

`test_admin_data_retention.py` 覆盖UTC严格cutoff、快照完整性、安全状态清理且不改在线库、重复scrub、保护当前/异常/非规范文件、同秒名称避让、每日备份注册/执行及新快照安全清理失败后删除。在线安全保留有 `test_account_security_cleanup.py`；访问快照保留有 `test_access_analytics.py`。这些测试的存在不代表本次已经运行。

本次未启动服务、读写真实数据库或配置，也未运行测试。跨进程同时备份、创建期间其它清理job、磁盘故障的多阶段部分成功、清理后实际恢复及各种旧凭据组合，不能由现有静态核对或正常路径用例宣布全量通过；尤其不能把quick_check等同于业务恢复验收。
