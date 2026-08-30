---
knowledge_id: "delivery.backend-records"
knowledge_type: "topic"
status: "current"
summary: "Backend审计与NAS部署历史的来源、筛选total与summary统计、展示脱敏和保留边界；空审计可能是查询失败，历史成功不证明当前服务或完整恢复。"
aliases: ["管理记录", "审计日志", "部署历史", "部署历史筛选", "AuditPage", "DeploymentHistoryPage", "get_audit_log_page", "query_deployment_history", "Write-WebDeploymentHistory", "api/admin/audit-log", "api/admin/deployments", "web-deploy-history.jsonl", "malformed_records", "history_retention", "summary.records", "summary.statuses", "summary.sources"]
code_paths: ["Web/Backend/routes/admin_api.py", "Web/Backend/db_cache.py", "Web/Backend/services/deployment_history.py", "Web/DeploymentHistory.psm1", "Web/Deploy-Nas.ps1", "Web/Frontend/src/services/admin.ts", "Web/Frontend/src/pages/AuditPage.tsx", "Web/Frontend/src/pages/DeploymentHistoryPage.tsx", "Web/Frontend/src/utils/auditLog.ts", "Web/Frontend/src/utils/deploymentHistory.ts"]
test_paths: ["Web/Backend/test_db_cache.py", "Web/Backend/test_contracts.py", "Web/Backend/test_deployment_history.py", "Web/Test-DeploymentHistory.ps1", "Web/Frontend/tests/auditLog.test.ts", "Web/Frontend/tests/deploymentHistory.test.ts"]
related: ["delivery.backend", "delivery.backend-auth", "delivery.backend-retention", "delivery.backend-jobs", "delivery.backend-observability"]
---

# 审计与部署记录：来源、查询与证据边界

`routes/admin_api.py` 提供两个只读查询入口，但它们不是同一个日志库：审计读取Backend数据库 `audit_log`，部署历史读取制品存储根的 `web-deploy-history.jsonl`。React页面只展示服务端返回的记录；没有记录、记录成功、当前服务正常是三个不同判断。

| 查询 | 权限与分页 | 当前数据来源 |
| --- | --- | --- |
| `GET /api/admin/audit-log` | `audit:read`；limit默认100、1–500，offset默认0且非负 | `CacheManager.get_audit_log_page`，SQLite按id倒序 |
| `GET /api/admin/deployments` | `deployments:read`；limit默认20、1–100，offset默认0且非负 | `query_deployment_history`，按文件有效记录的出现顺序倒序 |

非法整数或越界分页返回400，不是夹取到边界。凭据优先级、普通Session权限、API key可申请scope与CSRF见[HTTP认证](./authentication.md)。不要为了查询文档而读取实际事件、部署记录或凭据；下面描述的是源码契约。

## 审计：同次查询一致，不保证事件完整

`get_audit_log_page` 在同一个连接显式BEGIN后，使用相同WHERE条件取得COUNT和当前页；`total` 是本次筛选匹配总量，不是本页长度。另一页请求不共享这个事务，期间新增/保留清理仍可改变页边界。offset超过末尾返回空entries但保留total。

- action是精确匹配。actor在actor_id或actor_type中LIKE包含匹配，target同样匹配target_id或target_type；`%`、`_` 没有转义成字面字符，不能当成严格子串查找。
- since/until分别生成 `created_at >= ?` / `<= ?`，两端包含。路由只去空白，没有解析、归一化时区或校验先后关系；数据库比较保存的文本，不是自动转换任意日期格式的时间查询。
- 排序按id，不按created_at。字段由各调用方传入，事件时间采用 `now_iso()`；不能把id顺序解释成所有外部动作完成的精确时间顺序。
- 查询异常被捕获为 `entries=[], total=0`，路由仍可HTTP200。先排除数据库查询故障、过滤条件和保留清理，再把空结果解释为没有匹配事件。

`write_audit` 单独INSERT并commit，异常只打印，不向业务调用方抛出。各路由通常在业务步骤之后尝试写审计，不能宣称业务与审计全链原子、所有失败均有记录、或审计存在就代表客户端已收到响应。具体动作的先后顺序仍查对应主题；[任务执行记录](./jobs.md)也不是audit_log的同义表。

## 审计展示不是新的隐私授权边界

API用 `SELECT *` 返回事件字段，包含ip、user_agent和detail。`AuditPage` 先取得整页数据，点击“查看”只是选择该行打开Drawer，没有第二个详情请求或额外授权。IP/客户端信息在抽屉中呈现，不表示打开抽屉前它们尚未传到浏览器。

`auditLog.ts` 把已知action/actor/target映射为中文标签，同时保留原代码和未知值。detail优先解析JSON对象，其次按 `key=value` 提取；摘要最多取前三个解析字段，旧句子走有限翻译规则，否则显示原文。颜色、中文“已下载”等文案来自事件类型/记录文本，不是页面重新确认业务完成，也不是自动脱敏所有detail的处理器。

访问统计的每日HMAC不能推广到审计：审计可保存原来源地址及调用方写入的其它细节。[审计保留与快照清理](./backup-retention.md)说明cutoff、部分失败和识别范围；审计GET不会顺便执行到期删除。

## 部署历史：全文读取后分页，summary不随筛选收缩

`query_deployment_history` 逐行读取完整文件，再筛选和分页，不是只读limit行或读取数据库索引。首行接受UTF-8 BOM；空行、无法解码/解析的行、JSON非对象各计入malformed_records并跳过。合法JSON对象即可进入记录集，没有要求每个发布字段都存在。路径缺失或 `is_file()` 为false时返回空记录；reader未实施writer的拒绝reparse point检查，一般打开/读取OSError也没有统一转换为空结果。

status/source按去空白、小写后的精确值过滤；缺status按unknown，缺source按legacy。commit按 `deployed_commit → commit → target_commit` 的首个非空值做不区分大小写包含匹配。未知status/source不是400，只可能无匹配。倒序依据文件行序，不重新按timestamp排序。

- entries仅当前页；total是筛选后全部有效对象数。
- summary.records、statuses、sources来自筛选前全部有效对象，malformed_records来自整文件，因此不等于筛选结果或当前页统计。
- summary的status/source分组保留记录文本大小写，过滤却小写化；未知和旧数据不应强行归入success/failed。
- sequence是当前文件中的物理行号。历史裁剪/改写后可重新编号，不是跨部署永久不变的事件ID。
- summary.retention_limit默认500；最新有效对象的history_retention.keep_records经 `_integer` 转换后为正整数时采用它，也可能来自数字字符串或可截断的浮点数。它是记录中的历史配置提示，不是查询时读取了当前部署器配置或执行了清理。

## 部署投影的敏感字段限制

正常记录投影不返回server、runtime_log_path、原始error等字段；backup_path仅取最后文件名。status恰为failed才把error映射为粗略failure_reason，按source_control、frontend_build、tests、service_health、backup、deployment顺序进行关键词归类，不是结构化异常原因分析。recovery只保留每项第一个冒号前的文本，保留摘要只挑约定键。

这些是字段投影规则，不是任意历史内容的通用隐私净化：timestamp/source/commit等通过 `_text` 转成字符串，retention摘要的允许字段值直接保留，recovery前缀也非固定枚举校验。损坏或非标准记录若把敏感内容放进允许字段，不能沿用“绝不返回任何路径/秘密”的保证。历史文件只能由受信流程维护，不能把这个API当成任意JSON的安全发布器。

`DeploymentHistoryPage` 的详情也直接使用已取得的行，不重新探测服务器。`deploymentNotice` 对failed展示失败；其它状态只要记录里ready=true且health=ok就显示通过提示，否则提示证据不完整。构建/测试各自显示success/passed/skipped/未记录等状态；绿色提示不证明所有阶段均已跑过，更不证明当前运行版本、当前健康或完整业务恢复。

## 写入与500条保留不是读取端的职责

`Web/DeploymentHistory.psm1` 的 `Write-WebDeploymentHistory` 默认KeepRecords=500，范围20–100000；读取既有全部行，追加新记录前裁掉最早行，将本次history_retention摘要写入新记录。按追加顺序保留所有状态的JSON对象，不是只保留500条成功部署。

既有文件中出现空行、畸形JSON或非对象时，writer拒绝改写，不会像reader那样跳过坏行后继续裁剪。这使诊断信息保留，但后续部署历史可能停止更新。临时文件写完后使用Replace或Move发布；拒绝目标文件本身为reparse point，没有跨进程写锁或所有上层目录都可信的验证协议，不能宣称多部署器并发不会丢记录。

`Deploy-Nas.ps1` 的包装 `Write-DeploymentHistory` 捕获写入失败，往当前结果对象填history_retention.status=error并发出warning；不会因此回滚已完成的部署步骤，也不保证这一失败对象又写入了历史文件。因此命令成功、终端结果存在和历史页已更新是独立证据。

这里的部署历史及部署目录/bundle保留不是 [Backend数据库快照](./backup-retention.md) 的marketplace_backup策略。实际部署会联网、构建、改文件并重启服务；不要运行Deploy-Nas或历史writer来验证本文。恢复信息表示记录了动作，不证明恢复全过程成功。

## 对照测试与验证缺口

`test_db_cache.py` 覆盖审计共同筛选、精确total和越界末页；`test_contracts.py` 覆盖审计分页400及部署入口认证/分页/常规敏感字段投影。`test_deployment_history.py` 覆盖倒序、过滤、坏行计数、缺文件与常规路径移除；`Web/Test-DeploymentHistory.ps1` 使用临时目录验证追加裁剪、坏记录拒绝和失败dry-run不写历史。前端auditLog/deploymentHistory测试覆盖标签、未知值和证据不足文案。

本主题依据源码和既有测试内容整理；未运行部署、数据库或产品测试。并发历史写入、非标准记录任意字段净化、数据库异常后的空200及真实服务恢复仍需分别验证，不能由检索命中或文档构建通过替代。
