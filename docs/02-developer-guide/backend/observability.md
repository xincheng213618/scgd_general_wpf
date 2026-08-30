---
knowledge_id: "delivery.backend-observability"
knowledge_type: "topic"
status: "current"
summary: "Backend HTTP访问、SPA体验与性能观测的统计口径、异步丢事件、每日HMAC关联边界、日界线和进程缓存；不等于真实人数或下载完成。"
aliases: ["访问统计", "性能观测", "Access Analytics", "Statistics", "AccessAnalyticsRecorder", "SqliteAccessTrafficQuery", "build_access_event", "build_web_experience_event", "daily_visitor_key", "uniqueVisitorDays", "errorDiagnostics", "reporting_utc_offset_minutes", "access_analytics_enabled", "record_slow_request", "build_performance_summary", "slow_requests", "stats/traffic", "perf/summary", "api/v1/analytics/events"]
code_paths: ["Web/Backend/services/access_analytics.py", "Web/Backend/services/performance_observability.py", "Web/Backend/app_setup.py", "Web/Backend/context.py", "Web/Backend/routes/public_api.py", "Web/Backend/routes/admin_api.py", "Web/Backend/config_loader.py", "Web/Backend/db/schema_version.py", "Web/Backend/db/repositories/jobs.py", "Web/Backend/services/scheduler.py", "Web/Frontend/src/pages/TrafficPage.tsx", "Web/Frontend/src/services/admin.ts"]
test_paths: ["Web/Backend/test_access_analytics.py", "Web/Backend/test_performance_observability.py", "Web/Backend/test_schema_version.py", "Web/Backend/test_contracts.py"]
related: ["delivery.backend", "delivery.artifact-delivery", "delivery.backend-auth"]
---

# 访问统计、浏览器体验与性能观测

本页负责 Backend 观测数据的产生、聚合、读取与局限，不证明用户真实人数、网络传输完成或全站匿名化。统计入口需要的认证方式见[HTTP认证](./authentication.md)，部署和数据库位置见[Backend组成](./README.md)。

## 统计入口与口径

| 入口 | 数据含义 |
| --- | --- |
| `GET /api/admin/stats/overview` | 下载记录总数及当日数、索引计数、版本/缓存信息与当日HTTP访问摘要；不是扫描后核实的全部文件清单 |
| `GET /api/admin/stats/traffic?days=30&limit=10` | HTTP日/路由/客户端聚合、错误诊断、单列的SPA页面与Web Vitals，以及当前进程recorder状态 |
| `GET /api/admin/perf/summary` | 当前进程慢请求缓冲及最近任务运行中的慢/错误样本，不受traffic的days参数限制 |
| `POST /api/v1/analytics/events` | 公开接收浏览器页面或体验事件；202表示接受边界，不能当作持久化回执 |
| `GET /api/stats` | 旧插件下载统计，由 `download_stats.py` 读取，不是HTTP访问或SPA页面数 |

三个管理GET的端点要求均为 `stats:read`。`traffic` 的days默认30、范围1–365；limit默认10、范围1–100，非法值返回400。limit限制top路由、错误路由和top页面条数，不限制每天或客户端分类的行数。页面、HTTP请求和插件完成事件分开统计，不能相加成同一“访问量”。

## HTTP事件在响应钩子产生，不等待文件发送

`app_setup.py::register_slow_request_logging` 在before-request记单调时钟，在after-request读取状态、耗时和已有 `Content-Length`，再构造 `AccessEvent`。耗时截至该响应钩子，不包含后续响应迭代、客户端下载或磁盘写入。体积只是声明的响应体长度：缺失/非法/负长度为0，HEAD、1xx、204、205、304也为0；不会读取或缓冲响应体来补算。有效长度的部分206响应按自身声明长度计入HTTP体积。

HTTP `visits` 是被记录的请求数，HEAD仍可增加请求数，只是体积为0。插件下载数的触发点则是专用路由的完整响应迭代，详见[制品交付](./artifact-delivery.md)；流中途断开可能已经有HTTP访问记录，却没有插件完成记录。

`should_record_access` 按Flask路由模板过滤：跳过OPTIONS、health/ready、旧 `/api/stats`，以及管理统计/性能、analytics、assets/media/brand/favicon等已列前缀。没有匹配规则的请求记作 `__unmatched__`，不是按原始URL逐条储存；不能把这些明确排除项扩大成所有探针、静态文件或未来路由都自动排除。HTTP机器人请求仍以 `bot` 客户端分类参与统计。

`SqliteAccessTrafficQuery` 从SQLite聚合表读取并补齐窗口内缺失日期为0，不会先flush队列。均值来自时长总和/请求数，错误为状态码>=400；新数据分4xx/5xx，旧合并错误保留为 `unclassifiedErrorResponses`。比例的单位为百分比而非0–1。`errorDiagnostics` 是日/模板/方法/状态码聚合，不含异常堆栈；`coverageRate` / `partial` 对比已有错误明细和总数，不能证明没有丢事件。读取也不保证与另一接口处于同一快照。

## SPA页面与Web Vitals是独立上报

`routes/public_api.py::api_web_experience_event` 只在已知请求 `Content-Length > 4096` 时先返回413，再解析JSON；这不是针对未知长度请求独立实施的4KiB流式读取上限。来源检查沿[CSRF规则](./authentication.md#浏览器csrf的准确条件)，该特定端点通过来源检查后免Session token，并不要求登录，也不证明上报内容来自可信真人。

正常入库的请求体只允许以下字段，不接受客户端时间、完整URL、query、referrer、metric ID或DOM目标：

| kind | 精确字段与值 |
| --- | --- |
| `page_view` | `kind`、`route`、`navigationType`；后者只接受hard/spa |
| `web_vital` | `kind`、`route`、`metric`、`value`；metric为LCP/CLS/INP，不区分大小写 |

route最多256字符，拒绝query、fragment、反斜杠和双斜杠；仅支持 `normalize_web_route` 的固定页面和模板，如 `/plugins/:pluginId`、`/browse/*`、`/transfer/share/:token`，未知页面返回400。日和时间取服务端接收时间；识别为bot时在路由校验后直接返回202、`recorded=false`，不入队。

LCP/INP取值0–120000，单位ms；CLS为0–10，单位score。值被舍入后按当前代码阈值分桶：LCP的good上界2500、needs_improvement上界4000；INP为200/500；CLS为0.1/0.25。查询返回样本数、平均、最大值和质量计数/比例，**没有p75或按metric ID去重**。重复合法上报会重复累计，浏览器未发送、被拦截或队列丢失都可能使样本不完整。

HTTP聚合使用 `access_*` 表；SPA页面/路由去重使用 `web_page_daily`、`web_page_visitor_daily`；体验指标使用 `web_vital_daily`。`web.summary` 不混进HTTP `summary.visits`，也不能用API请求数代替页面导航次数。

## 每日HMAC是有限去标识，不是不可关联承诺

HTTP事件以路由模板和desktop/mobile/tablet/bot/other分类替代具体URL与完整User-Agent，事件/聚合表不保存原始地址、query或referrer。`daily_visitor_key` 使用配置secret、报告日和 `remote_addr` 做HMAC，取24个十六进制字符；无地址时不产生visitor key，但请求/页面计数仍可增加。

- HTTP当天 `uniqueVisitors` 按该key去重，不是自然人或设备去重。共享出口可能合并，地址变化可能拆分；客户端分类的unique计数归入该key当日首次出现的类别，不能当成独立设备人数。
- 跨日 `summary.uniqueVisitorDays` 与 `clients[].uniqueVisitorDays` 是每日去重数之和，不是跨日去重人数。SPA的web日总数跨页面去重，top页面按各自路由/日计数，其unique数相加也不等于全站人数。
- 每日key值会变，但HTTP与SPA同日使用相同key；数据库保留其day、首次/末次时间、访问次数，SPA去重表还带路由。持有同一secret和候选地址可重算，不能写成“任何人都无法跨日关联”或“库中只有无关联总数”。Web Vital行本身不带visitor key。

这只是本统计事件边界，不覆盖账号会话、管理审计、Web服务器/代理日志。尤其慢请求与recorder错误见下文，不能套用聚合表不含具体路径的保证。

## 异步队列、202与失败

`AccessAnalyticsRecorder.submit` 将已清洗事件和提交时的数据库路径一起入队；后台daemon线程按路径分组，每组用SQLite事务写入。默认每批最多128条，取到首条后立即收集当前可用条目，不等待凑满；0.5秒是空队列等待间隔，不是成功落库的时限。数据库锁等待发生在worker中，普通生产HTTP钩子不执行这些写事务；`TESTING=true` 则显式同步写，不能用同步测试的即时可见性推断生产时序。

- HTTP after-request忽略submit的false结果；构造/提交异常被捕获，不以统计失败改写原业务响应。入队仍有计算、锁与线程启动开销，不能承诺零延迟。
- SPA POST若submit返回false则返回503；成功入队返回202、`recorded=true`。后台稍后失败仍会丢弃这批事件，不重试、不持久保存队列，也无响应回补。
- `pending` 包含已取出但尚未完成的批次，未必小于队列capacity；`dropped` 是进程内已知丢弃累计值。`lastError` 截断到500字符，下次成功写入会清空；`lastFlushAt` 是最近成功写入时间，不是最后一次入队时间。错误文本不另做路径脱敏。
- `flush()` 只等待pending归零，失败批次同样会减pending，返回true不证明零丢失。退出时 `close()` 默认只等待worker最多2秒，不能保证崩溃、强制退出或超时后队列全部落库；重启也重置这些健康计数。

`access_analytics_enabled=false` 当前只关闭HTTP after-request的聚合分支，**SPA POST没有检查此开关**；慢请求采样也不受它控制。不能把它描述为全站遥测/性能观测总开关。

## 报告日、历史迁移和保留窗口

`reporting_utc_offset_minutes` 默认480（UTC+08:00），接受整数分钟-720至840；这是固定偏移，不是有夏令时规则的地理时区。事件时间仍存UTC，day和每日HMAC按该偏移划分；`downloadsToday` 也用该报告日转换得到的UTC半开区间读取 `download_log`。

启动组成时调用 `configure_access_analytics_calendar` 记录偏移及生效时间。有既有HTTP日聚合时标记 `legacyCalendarDataThroughDay`，不重分配旧日数据。返回的 `calendarBoundaryEffectiveAt`、`legacyCalendarDataThroughDay`、`hasLegacyCalendarData` 是解释历史日界线的线索，不是旧统计已迁成同一时区的证明；当前旧数据标记以 `access_daily` 是否有行决定，不代表逐一核实SPA表历史。仅热改进程配置不会自动重跑启动时的日历迁移。

schema v6移除历史HEAD声明体积，v7给后续请求增加4xx/5xx字段且不猜旧错误类别，v8建立日历metadata表；日历值由应用配置步骤写入。原始请求未保存，不能从这些日聚合精确重建旧时区分布。

访问保留默认90个报告日，清理函数允许1–3650，删除 `day < 今天-(保留天数-1)` 的HTTP/SPA/访客/Vital行。它由计划清理触发，不是读取时即时TTL；超期行也可能在清理执行前存在。计划触发见[后台任务](./jobs.md)，已识别快照的清理和失败结果见[备份与保留](./backup-retention.md)；这些写/删除操作不因本页说明而获得执行授权。

## 性能缓冲和页面上的旧结果

`context.py` 默认慢请求阈值500ms。`record_slow_request` 保存最近100条进程内样本，包含UTC时间、method、path、status和duration；进程重启清空，不是跨worker共享或持久化的全量性能历史。

实际传入的是 `request.path`，**不是Flask路由模板**；缓冲仅截断path到2048字符，控制台慢日志也打印具体路径。路径中可能带文件名、ID或分享token，不能称为已去除所有业务标识；访问聚合的排除规则不用于慢请求分支，统计/探针自身过慢也可进入缓冲。

`build_performance_summary` 返回缓冲最后20条，保留其时间顺序；任务则先由 `SqliteJobRepository.recent_runs(20)` 按ID倒序读取最近20条，再筛duration>=1000ms或status=error。不是从所有历史中寻找20条最慢/失败任务；较旧错误可能不在结果中，低耗时interrupted也不会只因中断而入选。任务行沿用job_runs的summary/error等字段，不是重新脱敏的HTTP聚合。

性能GET没有本模块的TTL结果缓存，每次组合缓冲与任务查询，但不会等待访问队列或生成全局一致快照。`TrafficPage.tsx` 在进入页面、days变更或手动刷新时分别请求traffic和performance，没有定时轮询；失败会继续展示上次成功结果并提示错误。days只过滤traffic，不筛性能样本，两个面板的采样时刻可不同，应看 `generated_at` 与 `process_started_at`。

## 配置与验证范围

| 配置项 | 默认值与当前边界 |
| --- | --- |
| `access_analytics_enabled` | true；只控制HTTP聚合分支 |
| `access_analytics_queue_size` | 4096；构造器至少1，仅限制等待队列 |
| `access_analytics_batch_size` | 128；构造器至少1 |
| `access_analytics_flush_interval_seconds` | 0.5；构造器至少0.05秒，不是持久化SLA |
| `access_analytics_retention_days` | 90；清理函数1–3650 |
| `reporting_utc_offset_minutes` | 480；-720至840整数分钟 |

队列参数在应用组成时读取，修改配置不自动重建recorder；`app_setup.py` 对队列大小/批量/等待间隔使用 `value or default`，数值0会回到默认值，再经构造器下限处理。慢请求阈值和100条容量是当前代码/context值，不应据此发明同名JSON配置开关。

现有 `test_access_analytics.py` 覆盖事件字段清洗、日报日界线、HTTP/SPA分开聚合、未知旧错误、日历标记、队列满、按提交数据库分组、保留窗口和HTTP入口；`test_performance_observability.py` 覆盖缓冲截断及最近任务筛选。`test_schema_version.py` 覆盖v6/v7/v8迁移，`test_contracts.py` 有overview/perf响应字段用例。

本次未运行这些测试。现有HTTP入口用例使用TESTING同步写，不证明生产202后的持久化；未在此核验队列写失败/进程退出的部署行为、无长度4KiB请求、统计关闭时SPA仍入库、多worker合并或代理地址真实性，也不把每日key变化测试当成无法关联的安全证明。排查缺数先看口径、日界线、pending/dropped与页面旧结果，不应只按HTTP200或202断言数据完整。
