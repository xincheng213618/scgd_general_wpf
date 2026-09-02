---
knowledge_id: "delivery.backend-feedback"
knowledge_type: "topic"
status: "current"
summary: "Backend公开反馈提交、文件目录收件箱、状态sidecar和受控附件响应；上传与管理校验不同，201、resolved及下载审计各有完成边界。"
aliases: ["反馈收件箱", "Feedback Inbox", "反馈上传", "save_feedback", "FeedbackSaveResult", "FeedbackValidationError", "feedback_admin", "query_feedback", "resolve_feedback_attachment", "update_feedback_status", "feedback_attachment_download", "feedback.json", ".admin.json", "feedback:manage", "FeedbackPage"]
code_paths: ["Web/Backend/feedback_service.py", "Web/Backend/services/feedback_admin.py", "Web/Backend/routes/public_api.py", "Web/Backend/routes/admin_api.py", "Web/Backend/storage_paths.py", "Web/Backend/config_loader.py", "Web/Backend/app_setup.py", "Web/Backend/app.py", "Web/Backend/context.py", "Web/Backend/download_stats.py", "Web/Backend/db_cache.py", "Web/Backend/services/http_method_safety.py", "Web/Frontend/src/pages/FeedbackPage.tsx", "Web/Frontend/src/services/admin.ts", "Web/Frontend/src/utils/feedback.ts"]
test_paths: ["Web/Backend/test_app.py", "Web/Backend/test_feedback_admin.py", "Web/Backend/test_contracts.py", "Web/Frontend/tests/feedback.test.ts"]
related: ["delivery.backend", "delivery.backend-auth", "delivery.artifact-delivery", "ui.desktop"]
---

# Backend反馈提交、处理状态与附件访问

公开 `POST /api/feedback` 把反馈保存到制品根的 `Feedback/<feedback_id>/`；管理API实时读取这些目录，不把SQLite当反馈正文事实源。`feedback_service.py` 负责接收与落文件，`services/feedback_admin.py` 负责收件箱、状态和附件定位，`routes/admin_api.py` 负责授权后的HTTP响应及审计。

桌面端如何选择日志、机器信息或Dump并提交，统一见[桌面辅助壳层](../../04-api-reference/ui-components/ColorVision.UI.Desktop.md)；本页不复制采集器或桌面权限流程。提交、改状态、下载附件都会写文件或可能写审计，不是文档验证授权；不能为核验本页读取真实反馈、启动服务或发上传请求。实际storage/数据库路径见[Backend组成](./README.md)。

## 公开提交接受什么

`routes/public_api.py::api_feedback` 不套上传或管理认证装饰器；它读 `request.form` 和 `request.files`，不是JSON正文API。浏览器来源和Session写请求仍先受共用[CSRF门禁](./authentication.md)约束，公开入口不等于跨源任意写入。

| 输入 | 当前限制 |
| --- | --- |
| `message`、`userName`、`appVersion`、`machineInfo` | 缺省为空；每字段最多4000个Python字符串字符，先检查长度再strip，不因两端空白被移除而放宽长度 |
| 文件字段 | 遍历所有字段名及其getlist，收集有非空filename的文件；不是只接受名为attachments的字段 |
| 文件数量 | 全部字段合计最多10个，在净化名称/实际保存前计数 |
| 最低内容 | strip后的message非空，或至少有一个被收集的文件；仅userName、版本或机器信息不够 |
| 请求大小 | app_setup显式设置总请求 `MAX_CONTENT_LENGTH=500*1024*1024` 字节；不是每附件各500MiB的保证，框架解析与部署层还可先行拒绝 |

违反反馈字段、数量或最低内容规则返回400 `error`；正常返回201，仅含 `feedbackId` 和 `message="Feedback received"`。路由只专门捕获 `FeedbackValidationError`，文件保存/JSON写入等异常不变成201，也没有失败后统一撤销已写文件的步骤。

`userName`、appVersion、machineInfo均是调用方自报，不是已验证账号或客户端身份。metadata中的 `clientIp` 来自 `hash_ip(remote_addr)`：无地址为空，有地址则为无盐SHA-256的前16个十六进制字符；它不是原始IP，也不是每日轮换标识或强匿名化证明。

## 落盘顺序与上传端的真实边界

`save_feedback` 用UTC时间和message/userName/完整时间组成的hash前12位生成反馈ID，目录形如 `Feedback/YYYYMMDD_HHMMSS_<suffix>/`。ID不是请求幂等键、附件内容hash或访问凭证；相同内容再次提交通常产生另一份目录。

保存顺序是：建反馈目录 → 逐个保存附件 → 最后直接写 `feedback.json`。metadata含feedbackId、四个表单字段、clientIp、createdAt和实际记录的files名称。附件同名时 `unique_output_path` 依次使用stem-1、stem-2等名称，不覆盖同次提交里已保存的普通同名文件；不是跨请求加锁的唯一性事务。

文件名由 `storage_paths.sanitize_filename` 取当前平台 `Path.name`，再把斜杠和若干非法字符替换为下划线。净化后空名会跳过，且不重新校验“至少一个附件”；因此201不保证上传者提供的每个文件都进入最终清单。此服务没有附件扩展名/MIME/ZIP内容检查，也不解压诊断包来验证其内容。

特别不能把后面的管理附件防护反推到公开提交：上传服务没有调用 `_safe_feedback_directory` / `resolve_feedback_attachment`，没有相同的符号链接/真实父目录检查，也没有拒绝内部名称 `feedback.json`、`.admin.json`。名为feedback.json的上传文件可能被随后写入的metadata覆盖；名为.admin.json且内容符合管理读取格式的上传文件可能成为其状态来源。这里只描述当前实现限制，不声称该上传链已通过完整安全审计。

`feedback.json` 不是经临时文件原子替换、附件与metadata也没有整体事务或完成marker。中途失败可能留部分附件、空/损坏metadata或目录；收件箱仍可能枚举它们。公开201只表示本次保存调用正常返回，不证明持久介质断电安全、后台已处理、附件可安全打开或接收者已下载。

## 管理收件箱的查询与投影

以下四类接口均要求 `feedback:manage`；获此permission的普通用户Session也可参与，不应按页面“仅管理员”文案断言只允许role=admin。凭据优先级和API key可申请scope的区别见[HTTP认证](./authentication.md)。

| 方法与路径 | 行为 |
| --- | --- |
| `GET /api/admin/feedback` | 按status/query过滤后分页，附全收件箱summary |
| `GET /api/admin/feedback/<id>` | 一条反馈的有界文字字段、状态与真实附件清单 |
| `GET /api/admin/feedback/<id>/attachments/<path:filename>` | 解析受限直接文件后调用send_file；会先尝试写审计 |
| `PUT /api/admin/feedback/<id>/status` | 更新独立.admin.json状态，不改反馈正文 |

查询status可省略，或为 `open`、`new`、`in_progress`、`resolved`；open等于非resolved。query先strip，最多200字符；limit默认20、范围1–100，offset默认0且非负。非法参数400，详情/附件目标无效或不存在404。

每次查询先枚举整个Feedback下的直接目录、读取metadata/state并盘点附件，再过滤和切页；limit不限制扫描目录数，也不是SQL分页或全文索引。根目录缺失/非目录/符号链接时返回空集合，不能只凭total=0证明从未收到过反馈。

文本搜索仅匹配feedback_id、user_name、app_version和**前160字符message_preview**，不搜完整message、machine_info或附件名。records按created_at文本与feedback_id降序排列；summary在过滤前计算，含各状态数、附件总数/字节数、无效metadata/state数及可解析时间中的oldest_open_at。因此summary不是当前搜索或当前页的汇总。

`feedback.json` / `.admin.json` 仅接受不超过1MiB的非符号链接JSON对象。缺失/损坏metadata不隐藏目录，message等字段可为空，created_at缺失时用目录mtime；旧目录后续文件修改可能改变这一回退时间。字段有界读取：普通文字最多4000字符，时间最多100，client_ip最多200；详情不是不受限的原始JSON下载。

`metadata_valid` / `state_valid` 主要表示文件是否可读为受限JSON对象，不是完整schema或业务内容校验。缺少state文件视为可接受的默认状态；缺失/不认识的status回退new，即使该JSON对象仍被标为state_valid。不要把界面的“正常”标签解释成附件内容安全或所有必需字段齐全。

附件清单来自目录中的真实直接文件，而非信任metadata.files；跳过两个内部文件名、符号链接、子目录和解析后不在该目录的文件，按名称casefold排序。目录名不符合详情ID规则的历史目录仍可能出现在总览，但详情/下载会404；“列表枚举到”不等于每个旧目录都能通过管理路由打开。

## 状态sidecar与前端工作流

PUT正文必须且只能含 `status`，值为new/in_progress/resolved。服务器只校验目标值，不限制必须依次推进；resolved不是删除、修复验证、通知提交者或保证已看过附件。

`update_feedback_status` 先读旧状态；相同则返回详情，不写文件也不写状态变更审计，亦不顺便修复损坏/缺失的sidecar。不同则将status和UTC updatedAt写入同目录唯一临时文件，flush/fsync后在进程内锁中 `os.replace` 到 `.admin.json`，然后重新读取详情。它不改提交的feedback.json。

原子边界是sidecar替换，不是sidecar、反馈附件和SQLite审计的统一事务；旧状态在锁外读取，接口没有revision/If-Match，多客户端更新可覆盖旧读结果。replace前失败保留旧state，路由对OSError返回500；替换后读取或返回失败不证明更新未落盘。只有changed才尝试 `feedback_status_update` 审计，写审计失败只打印，不回滚sidecar。

`FeedbackPage.tsx` 默认筛open；按钮引导new→in_progress→resolved，已解决项可重新打开到in_progress，这只是UI路线。它用独立详情请求展示附件，状态成功后替换详情并请求刷新列表；详情关闭/切换会abort详情读取，不是取消此前已发出的状态写请求。

当前有一处明确的前后端不匹配：页面“全部”项使用 `status="all"`，`services/admin.ts::getFeedbackInbox` 原样传入查询，而Backend不接受all，因此该选择按当前代码返回400。API全量查询应省略status；不能把前端选项存在当作该分支已可用。

## 附件定位、响应与审计不是完成下载

管理附件先验证feedback_id：非空、最长128、仅ASCII字母/数字/下划线/点/连字符，且不能是单独的点或双点；Feedback根和目标目录必须是真实目录、非符号链接，目标resolve后的父目录必须恰为根。

filename须等于当前平台 `Path(filename).name`，不能是内部名称；目标还必须是非符号链接的直接文件，resolve后的父目录必须恰为当前反馈目录。以上条件不满足统一404。内部名称排除是源码字符串精确比较，路径解释仍受宿主文件系统影响；这些检查不是路径检查后文件永不变化的锁定句柄，也不是上传端已经使用同样防护的证据。

`feedback_attachment` 成功定位后先尝试写 `feedback_attachment_download`，再直接 `send_file(as_attachment=True, download_name=target.name)`；它没有经过 `ArtifactDeliveryService` 的完成迭代回调。当前自动HEAD禁用表只覆盖Operations两个端点，未排除此附件GET，所以HEAD也可能走到审计，而不发送响应体。

因此即使审计detail写着“downloaded”，它也只是已到达发送前的位置：后续条件响应、发送失败、中断或客户端下载未落盘都不能据此排除。反过来 `CacheManager.write_audit` 捕获数据库异常，只打印而继续发送，真实文件响应也不保证必有审计。列表/详情读没有对应的附件下载审计；响应头/缓存基线见[HTTP制品与响应策略](./artifact-delivery.md)，不要把该主题的插件完成计数套到这里。

## 验证范围与缺口

`test_app.py` 的公开反馈用例覆盖超过10个文件返回400、表单metadata持久化、同名附件追加编号；不等于保留名、符号链接、净化后空名或中途保存失败已验证。

`test_feedback_admin.py` 覆盖全局summary与过滤、缺metadata旧目录、open筛选、详情、路径穿越和内部文件拒绝、精确状态payload、sidecar持久化及replace失败保留旧状态。`test_contracts.py` 有管理权限、列表/详情/普通附件GET、状态更新和审计动作存在的HTTP用例；这不是HEAD或客户端下载完成证明。

`Web/Frontend/tests/feedback.test.ts` 只验证状态引导、文案和等待时间辅助函数，不覆盖页面网络请求、all过滤、真实下载或并发状态更新。真实网络、目录并发变化、跨平台路径大小写及故障后的部分落盘仍是验证缺口。
