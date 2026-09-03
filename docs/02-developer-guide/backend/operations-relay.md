---
knowledge_id: "delivery.backend-operations"
knowledge_type: "topic"
status: "current"
summary: "Backend Operations 的接口、身份与任务回执；区分在线、排队和执行完成，并说明加密快照的下载、消费与过期清理。"
aliases: ["Operations中继", "运维后台", "设备签名中继", "operations_overview", "OperationsDeviceRelayService", "OperationsRelayContext", "OperationsAdminQuery", "SqliteOperationsAdminQuery", "OperationsSupportStore", "SqliteOperationsSupportStore", "OperationsSafeSnapshot", "ops:relay", "ops:operator", "operations:manage", "COLORVISION_OPERATIONS_RELAY_URL", "COLORVISION_OPERATIONS_RELAY_KEY", "signedRelayReady", "awaiting_local_consent", "X-CV-Timestamp", "X-CV-Nonce", "X-CV-Signature", "X-CV-Receipt-Metadata", "host_identity_conflict", "unknown_or_revoked_device", "request_time_out_of_range", "replayed_request", "idempotency_conflict", "window_snapshot_upload_required", "window_snapshot_task_not_uploadable", "window_snapshot_consumed", "window_snapshot_delete_failed", "window_snapshot_expired", "运维窗口快照", "operations_relay_window_snapshots"]
code_paths: ["Web/Backend/routes/operations_relay.py", "Web/Backend/services/operations_device_relay.py", "Web/Backend/routes/admin_api.py", "Web/Backend/ports/operations_admin.py", "Web/Backend/ports/operations_support.py", "Web/Backend/db/repositories/operations_admin.py", "Web/Backend/db/repositories/operations_support.py", "Web/Backend/app_setup.py", "Web/Backend/cli.py", "UI/ColorVision.UI.Desktop/Operations/OperationsRelayClientService.cs", "UI/ColorVision.UI.Desktop/Operations/OperationsRelayProtocol.cs", "UI/ColorVision.UI.Desktop/Operations/OperationsSafeSnapshot.cs", "UI/ColorVision.UI.Desktop/Operations/OperationsDesktopActionService.cs", "UI/ColorVision.UI.Desktop/Operations/OperationsApplicationRestart.cs", "Engine/ColorVision.Engine/Services/Operations/ServiceHostOperationsMqttRestartController.cs", "Engine/ColorVision.Engine/Services/Operations/EngineOperationsMessageChannelHealthProvider.cs", "Engine/ColorVision.Engine/Services/Operations/FlowOperationsRuntimeStatusProvider.cs", "ColorVision/App.xaml.cs"]
test_paths: ["Web/Backend/test_operations_relay.py", "Web/Backend/test_operations_admin.py", "Web/Backend/test_operations_support_store.py", "Test/ColorVision.UI.Tests/OperationsRelayProtocolTests.cs"]
related: ["delivery.backend", "delivery.backend-auth", "delivery.android-operations"]
---

# Backend Operations 中继与只读概览

Backend 保存主机状态、受限任务意图、回执和支持会话记录，不替代桌面执行器。网页“在线”、HTTP `ok`、任务 `queued`、签名有效都不能单独证明电脑动作已经完成。本主题负责 Backend 的凭据、数据与状态边界；手机入口、现场 HTTPS、配对、固定中继和动作前提由 [Android 运维伴侣](./android-operations.md) 维护，不从这里推导任意远程命令能力。

## 三种入口不是同一授权模型

| 入口 | 实际认证与责任 |
| --- | --- |
| `/api/ops/v1/hosts/...`、`tasks`、`receipts`、`support-events` 的 legacy 路由 | `routes/operations_relay.py:_auth` 只接受 `Authorization: Bearer ...`，经 `verify_api_key` 校验。`ops:relay` 用于心跳、轮询、上传回执和支持事件；`ops:operator` 用于查询主机/任务/回执/事件及创建任务。Basic 与网页登录 Session 不能替代此凭据 |
| `/api/ops/v1/device-relay/...` | 路由委托 `OperationsDeviceRelayService`；主机用 Operations 证书签名，手机用已同步的配对设备 P-256 密钥签名，不以市场账号 Session 或 Bearer key 取得设备权限 |
| `GET /api/admin/operations/overview` | 管理 API 要求 `operations:manage`，走共用 `AuthPolicy`，是只读投影，不是任务提交端点。具备该 permission 的普通 Session 也可参与授权，不应将“admin 路径”解释为仅 admin 角色 |

API key 的可申请 scope 与 Session permission 不是同一目录；`admin:*` 的共用 scope 匹配、Basic 和 Session 的优先级见[认证契约](./authentication.md)。不能把 `operations:manage` 直接当作可创建的细粒度 key scope，也不能把 `ops:relay` 当作单台主机身份绑定：legacy 路由按 scope 放行，再使用 URL/请求中的 `hostId`，没有按 key 限定主机的关联检查。

`app_setup.py` 将同一 Backend cache 注入三组 owner：legacy 路由直接存取 Operations 表；签名服务负责签名、设备任务及专用证据；`OperationsAdminQuery` / `SqliteOperationsAdminQuery` 负责 overview；`OperationsSupportStore` / `SqliteOperationsSupportStore` 负责支持会话状态写入。它们不是三个隔离的数据库。尤其 legacy 轮询与回执查询没有 `source_type='device'` 排除条件，不应把新旧协议理解为互不影响的存储隔离边界。

## HTTP 接口速查

以下路径均相对 `/api/ops/v1`，`{hostId}` / `{taskId}` 是路径参数。Backend 的 ID 校验为 1–64 个 ASCII 字母、数字、下划线或连字符。表中“查询”指业务用途；签名查询仍会登记防重放 nonce，窗口快照查询还可能清理过期密文。

| 设备签名路径（全部为 POST） | 调用身份 | 用途与输入边界 |
| --- | --- | --- |
| `/device-relay/hosts/{hostId}/sync` | 主机证书 | 同步主机、签名快照与配对清单；首次绑定主机身份 |
| `/device-relay/hosts/{hostId}/tasks` | 已绑定主机 | 轮询设备提交的任务；正文参与签名，服务按路径中的主机取任务 |
| `/device-relay/tasks` | 配对设备 | 创建有限任务；`hostId`、`capabilityId`、`payload`、`idempotencyKey`、可选 `ttlSeconds`，能力有额外要求 |
| `/device-relay/hosts/{hostId}/tasks/{taskId}/receipts` | 已绑定主机 | 上传状态、evidence 与 `receiptEnvelope` |
| `/device-relay/hosts/{hostId}/snapshot` | 配对设备 | 返回该主机最新同步数据、证书和 `hostEnvelope`；不是触发新的窗口截图 |
| `/device-relay/tasks/{taskId}` | 原任务设备 | 正文提供 `hostId`；返回任务状态、全部已有回执及其可用签名 envelope |
| `/device-relay/hosts/{hostId}/tasks/{taskId}/window-snapshot` | 已绑定主机 | 上传密文二进制及 `X-CV-Receipt-Metadata` |
| `/device-relay/tasks/{taskId}/window-snapshot` | 原任务设备 | 精确正文 `{"hostId":"..."}`；下载已完成任务的密文 |
| `/device-relay/tasks/{taskId}/window-snapshot/consume` | 原任务设备 | 精确正文含 `hostId`、`sealedSha256`；确认消费并删除密文 |

主机快照读取只要求设备在该主机下未撤销且签名有效，不另检查 `ops.status.read`；任务查询和快照下载/消费还限定原 `deviceId`。创建任务才按下文的 capability → scope 表检查权限，不能把创建权限与读取范围混为一谈。

| Legacy 路径与方法 | key scope | 用途与列表边界 |
| --- | --- | --- |
| `POST /hosts/{hostId}/heartbeat` | `ops:relay` | 写主机状态、capabilities 和 snapshot |
| `GET /hosts/{hostId}/tasks` | `ops:relay` | 轮询待投递任务，最多 50 条 |
| `POST /tasks` | `ops:operator` | 创建 legacy 目录允许的任务 |
| `GET /tasks` | `ops:operator` | 可按 `hostId` 过滤，最新 500 条 |
| `POST /hosts/{hostId}/tasks/{taskId}/receipts` | `ops:relay` | 写任务回执 |
| `GET /hosts`、`GET /receipts` | `ops:operator` | 分别返回最新 500 个主机/回执；receipts 可按 `hostId` 过滤 |
| `POST /hosts/{hostId}/support-events` | `ops:relay` | 写支持会话事件 |
| `GET /support-events` | `ops:operator` | 不过滤时最新 500 条；按 `sessionId` 过滤时按时间正序取前 500 条，未另按主机过滤 |

Legacy 列表没有游标分页，返回的 `count` 是本次列表长度。完整审计不能仅凭这些有界列表宣称覆盖所有历史；其字段包含存储的业务内容，隐私范围也不同于下文的管理员 overview。

## 桌面通道选择与 legacy 配置

`OperationsRelayClientService` 构造时，只有以下条件同时成立才选 legacy：`COLORVISION_OPERATIONS_RELAY_URL` 是绝对 HTTPS URL，或 loopback HTTP URL；`COLORVISION_OPERATIONS_RELAY_KEY` 以 `cvmp_` 开头。否则选择固定 `DefaultEndpoint` 的设备签名中继。这个本地前缀判断不是服务端 key 验证；缺少或无效的 legacy 配置也不表示中继被关闭，构造器仍令 `IsConfigured=true`。

需要兼容部署时，在 `Web/Backend/` 下使用现有 CLI 创建独立 relay key：

```powershell
python .\app.py --create-api-key colorvision-relay --scopes ops:relay
```

该命令会初始化 Backend 并写入选定数据库、授予访问能力、向终端输出新 key；只能在已授权的部署配置/数据库上执行，不能作为文档或连通性检查。确认运行环境后再将 URL 与 key 提供给 ColorVision 进程环境，勿写入知识文档或仓库。`--storage` 只改制品目录，不隔离配置或账号数据库，启动前提见 [Backend 入口](./README.md)。

中继由桌面主动连接 Web，不因这条链新增入站端口；这不表示桌面没有另行启用的现场 HTTPS 监听。签名链的固定传输源与加密边界见 [Android 运维伴侣](./android-operations.md#现场通道与固定中继)：签名认证不等于传输加密。

## 签名身份、同步与有限任务

`sync_host` 验证主机自签名 RSA 证书、请求签名及 `snapshotEnvelope`。首次将证书指纹绑定到 `hostId`；已有绑定不接受不同指纹，返回 `host_identity_conflict`。这是协议身份绑定，不是网页管理员审批或外部 CA 对现场电脑的认证。主机同步同时更新快照和配对设备清单：先将该主机已有设备标为撤销，再按本次清单 upsert，因此遗漏设备不会继续保持 active。设备批准/撤销与 scopes 来源于桌面同步，不从市场角色推导。

请求使用 `X-CV-Timestamp`（Unix 秒）、`X-CV-Nonce` 和 Base64 `X-CV-Signature`；设备另带 `X-CV-Device-Id`，主机首次同步带 `X-CV-Host-Certificate`，后续主机请求带与路径相符的 `X-CV-Host-Id`。签名原文为大写 method、path、timestamp、nonce、原始 body 的 SHA-256 小写十六进制值，以换行连接。服务端允许 2 分钟时钟偏差；nonce 为 16–128 个字母/数字/下划线/连字符，按主体保留 5 分钟防重放。新任务还检查未撤销配对记录和 capability 对应 scope，并将原始请求正文/时间/nonce/签名一起保留给桌面。桌面 `OperationsRelayProtocol.TryVerifyDeviceTask` 再次验证本机配对、签名和任务条件。Backend 接受不等于桌面必定接受；撤销传播、过期任务或本机前提不符仍可导致拒绝。

两条创建路线有不同的目录和 payload 规则：

- Legacy `ALLOWED_TASK_CAPABILITIES` 仅为 `ops.diagnostics.request`、`ops.support.message`、`ops.deployment.verify`，不接受 `ops.service.restart`。`support.message` 要求精确的 `sessionId` / `text` 且会话 active；另外两种在此路由只校验对象、大小和顶层 `command` / `executablePath` / `shell` / `script` 禁字段，不能宣称所有额外字段均被拒绝或这些字段会成为可执行命令。
- Signed `ALLOWED_DEVICE_TASK_CAPABILITIES` 按下表限制任务；不是任意路径、窗口、服务或命令选择器。

| Signed capability | 配对设备 scope | payload |
| --- | --- | --- |
| `ops.window.show`、`ops.window.minimize` | `ops.window.control` | 空对象 |
| `ops.messaging.reconnect`、`ops.flow.cancel` | `ops.jobs.create` | 空对象 |
| `ops.service.restart`、`ops.application.restart` | `ops.jobs.create` | 空对象；目标由桌面固定 |
| `ops.diagnostics.request` | `ops.jobs.create` | 仅允许可选 `reason`，长度至多 200 字符 |
| `ops.diagnostics.failures.read` | `ops.diagnostics.read` | 必须显式提供空对象 |
| `ops.window.snapshot.capture` | `ops.jobs.create` | 精确包含 `scheme="p256-hkdf-sha256-aes256gcm-v1"` 与规范 Base64 DER P-256 `recipientPublicKeySpki`，并显式 `ttlSeconds=300` |

Backend 创建 signed 任务要求非空 JSON 对象正文，最多 16,384 字节。为通过桌面复验，应显式提供对象 `payload`、字符串 `idempotencyKey`，`ttlSeconds` 使用整数，且不增加表外顶层字段。Backend 对部分省略值、类型转换或额外顶层字段较宽松，桌面仍可用 `invalid_task_body` / `invalid_task_ttl` 拒绝原始签名正文；Backend 规范化后的 payload 不替换原始签名请求。具体界面操作与本机门禁见 [Android 运维伴侣](./android-operations.md#可见能力不等于操作许可)。

桌面执行目标也固定：窗口动作由 `OperationsDesktopActionService` 作用于 `Application.Current.MainWindow`，消息恢复由 `EngineOperationsMessageChannelHealthProvider` 使用当前MQTT连接/订阅；`FlowOperationsRuntimeStatusProvider` 调用主工作区 `manager.View.StopFlowCommand`，成功可以只是取消请求被接受，不是采集已停止。`ops.service.restart` 由 `ServiceHostOperationsMqttRestartController` 固定重启本机mosquitto；relay在受理前及发送accepted后复查Flow非活动、服务可用/适用且支持维护，仅接受running/stopped/paused状态。设备不能传服务名或维护参数来选择其它目标。

上传的 host capabilities 用于展示；`create_task` 的准入依据是服务端目录与设备 scope，不以该主机最近广告的 capabilities 或“在线”字段代替执行前提。设备读取主机快照也不因 `lastSeenAt` 陈旧自动失败，调用者仍需校验签名内容与新鲜度。

## 排队、重复投递与回执的成功边界

| 返回或状态 | Backend 实际完成的步骤 |
| --- | --- |
| 创建返回 `202` / `queued` | 任务意图已写入 Operations 表，不是桌面受理或执行成功 |
| 轮询后 `delivered` | 服务端已选中任务并更新标记，然后组成响应；响应丢失仍可能留下该状态 |
| `received`、`accepted`、`awaiting_local_consent` 回执 | 回执记录保留原状态，任务行统一归为 `accepted`，不能以任务行分辨“收到”与“等待本机同意” |
| `completed`、`failed`、`rejected` 回执 | 按回执更新任务行。普通 signed 回执另外验证主机签名 envelope 与 host/task/idempotency/status/evidence 对应关系；记录主机声称的结果，不是 Backend 再次执行或独立验证硬件效果 |

普通任务 TTL 默认 900 秒并钳制到 60–3600 秒；signed 窗口快照另要求明确整数 `ttlSeconds=300`。Backend 从任务创建时计算过期；桌面以“签名请求时间 + TTL”与中继 `expiresAt` 的较早值复验，因此 Backend 尚可投递的任务仍可能被桌面以 `expired_task_envelope` 拒绝。轮询每次最多 50 条，仅取未过期的 `queued` / `delivered`，按创建时间排序；signed 主机轮询还要求 `source_type='device'`。`delivered` 会再次返回，不是 exactly-once；收到上述非终态回执转为 `accepted` 后则不再进入这一轮询集合。到期只是投递筛选条件，不自动把普通任务状态改成 `expired`，也不证明已停止桌面作业。

创建任务的去重键是 `(hostId, idempotencyKey)`，不是每个设备各自的命名空间。Legacy 冲突返回已有 taskId，不核对新旧 payload；signed 冲突只有原始 body 文本完全相同才返回已有 taskId，否则 `409 idempotency_conflict`。JSON 空白或字段顺序变化也可能造成冲突。去重不会刷新原任务的签名正文或过期时间；设备间复用同一主机的 key 即使命中已有任务，查询时仍受原设备归属约束。

重试须重新签名并使用新 nonce。普通 signed 请求的 nonce 随所属数据库事务提交，回滚可能使该 nonce 未被记录；窗口快照上传/下载在验签及清理后有提前提交，后续业务失败时 nonce 仍可能已经使用，不能按 HTTP 失败推断可原样重放。普通 signed 回执按同 task/host/status 和完全相同的 envelope body 去重；重新生成 `signedAt` 就是不同记录，legacy 回执没有相同的 envelope 去重。

普通回执写入没有完整的单向状态迁移门禁：非窗口快照任务的新回执仍可覆盖已有终态，因此不能承诺“completed 永不回退”或把 receiptCount 当成功次数。应核对实际回执、来源与动作证据；应用重启的 `accepted` 早于请求关闭，最终回执由桌面持久化工作记录另行上报，不是 accepted 即重启成功。

应用重启成功路径在替换进程启动后，由 `App.xaml.cs` 调用 `OperationsApplicationRestartHandoff.CompletePending` 匹配持久化handoff及其新鲜度、完成工作记录，再由relay发送最终签名回执。失败终态也可能由旧进程上报；不能把“已有最终回执”统一解释成替换进程已经成功启动。

## 失败证据与窗口快照

`ops.diagnostics.failures.read` 的 signed 回执只接受 `completed` 或 `failed`。完成 evidence 必须符合 `failure-evidence-v1` 的精确字段集：固定 `windowDays=7`，可用性/扫描受限标志为布尔值，计数为 0–999 整数，时间带时区，并核对最新时间、计数与 `hasEvidence` 的关系。失败 evidence 精确为 `{"kind":"failure-evidence-error-v1","code":"failure_evidence_unavailable"}`。字段全集由 `FAILURE_EVIDENCE_COMPLETED_KEYS` 与 `_validate_failure_evidence_receipt` 维护；它是主机提交的有界证据摘要，不是 Backend 自行读取电脑事件日志。

### 加密窗口快照的生命周期

此处的窗口快照是独立任务产物，与 `/hosts/{hostId}/snapshot` 返回的状态 JSON 不同。Backend 验证主机签名、metadata、任务归属、长度和密文 SHA-256，不解密图片，也不以摘要匹配证明图片展示了预期现场内容。

| 阶段 | 成功条件与结果 |
| --- | --- |
| 创建 | 精确的方案、公钥与 300 秒 TTL；任务属于提交设备 |
| 上传 | `application/octet-stream`，不接受 `Content-Encoding`，必须提供 Content-Length 且为 61 至 `1536 × 1024 + 60` 字节；`X-CV-Receipt-Metadata` 是规范 Base64 JSON，精确含 status/evidence/receiptEnvelope |
| 提交完成 | 新上传只接受未过期的 `queued` / `delivered`；校验后先写临时文件并 flush/fsync，再替换目标文件、写产物元数据与 completed 回执、更新任务。首次返回 201，完全匹配的重传返回 200/deduplicated |
| 下载 | 原设备未撤销，任务 completed，签名回执、产物记录、文件长度/摘要及有效期一致；返回密文、`Cache-Control: no-store` 与 `X-CV-Sealed-SHA256`，下载不消费 |
| 消费 | 原设备提交同一 `sealedSha256`；删除文件成功后删除产物行并标记任务 consumed。重复有效确认返回 deduplicated；随后下载返回 410 `window_snapshot_consumed` |
| 到期 | evidence 的有效期不超过捕获后 300 秒，也不得晚于任务过期；访问时拒绝过期产物。清理删除密文及产物行，不自动改写任务状态或移除历史回执 |

窗口快照的普通 signed receipts 端点不接受 `completed`、`accepted` 或通用失败 evidence：完成必须走二进制上传；失败精确为 `status="failed"`、`evidence={"kind":"window-snapshot-error-v1","code":"window_snapshot_unavailable"}`。已 completed/consumed 的任务不接受新的失败覆盖。这个限制不能推广到未按 `source_type` 隔离的 legacy 路由。

密文存于 Backend 数据库同级的 `.operations-relay-window-snapshots/{taskId}`；SQLite 保存元数据与回执，不保存密文 BLOB。上传对文件系统和数据库不是同一个事务：失败会尝试删除临时文件和已发布文件；消费删除失败返回 503 `window_snapshot_delete_failed`，不应当作消费成功。数据库后续提交失败也不能自动恢复已删除的文件。

服务构造、签名同步/轮询及相关快照访问会触发清理，没有保证精确到期执行的独立清理定时器。过期文件删除失败可留待以后重试；符合命名规则的孤立产物/临时文件需修改时间超过 10 分钟才清理。同步/轮询的尽力清理不等于“磁盘已清空”，overview 也不能证明产物仍可下载。

## 支持会话事件


支持会话事件由 `record_event` 在 `BEGIN IMMEDIATE` 内检查当前状态并写入；异常回滚该事务。最新状态按最后一条非 `message` 事件决定：先 `session.requested` 才能 `session.active`，active 才能 message；closed/failed 结束会话。已有状态后再次 requested、重复 active、结束后再报 closed/failed 会返回 deduplicated，但重复 message 不自动去重。HTTP `201` 表示事件已记录，`200 deduplicated` 不表示新事件执行；它们不取代桌面本机同意或证明消息已由人阅读。

## 管理员 overview 的范围与隐私投影

`GET /api/admin/operations/overview?hostLimit=100&activityLimit=100` 为 `/admin/operations/hosts` 提供只读数据。两个 limit 默认 100、各限 1–200；非法整数或越界返回 400。`hostLimit` 限制 hosts；`activityLimit` 分别限制 recentTasks、supportSessions、relayDevices，不是所有列表合计上限。

`summary` 来自整表 SQL 计数，不是截断列表长度；但多条 SELECT 没有显式统一读快照事务，不能保证并发写入时摘要与列表同一时刻一致。主要显示值的含义是：

- `online` 由 `lastSeenAt >= now - 90秒` 判断，与主机上报的 `reportedStatus` 分开。桌面每轮完成同步、轮询等处理后再等待 20 秒，网络/任务耗时另计，并非严格每 20 秒心跳；90 秒窗口不是连通性或可控制性保证。
- `signedRelayReady` / `signedRelayHosts` 仅表示身份表存在记录，不重新验签或证明证书仍有效。配对设备 `active` 仅表示 `revoked_at IS NULL`，不等于设备当前在线。
- `pendingTasks` 统计 queued/delivered/accepted，包括仍保留这些状态的过期任务；列表 `expired` 独立计算。failedTasks 包含 failed/rejected，receiptCount 是记录数。支持会话 active 按最新非 message 状态统计，不因心跳过期自动结束。

`_safe_snapshot` 只保留固定字段并做类型/长度归一：application/version/isRunning/uptimeSeconds/capturedAt、process.memoryMb、mainWindow 的存在/状态/可见性，以及 secureOperations 的运行/配对数/relayConfigured/relayRunning。未知键丢弃，缺少或非法值回退；这不是完整 `OperationsRelaySnapshot` 的转发。

Overview 不返回主机证书/指纹、设备公钥、请求签名/nonce/body、任务 payload、回执 evidence、支持消息正文或任意 snapshot 键；返回的是名称/ID、scope、时间、状态和有界统计。这是该响应的字段投影，不是整个 Operations 存储被脱敏、所有字符串均经过敏感信息识别的保证。Legacy operator 列表会返回 snapshot/payload/evidence/事件正文等存储内容；signed 设备接口也需要返回主机证书和 envelope 供客户端验证，不能把 overview 的排除字段推广到这些接口。

该管理 GET 和其只读页面不派发任务；展示一个 capability、signedRelayReady、在线或回执都不授予动作权限。真正创建任务仍要走对应 Bearer/signed 路线，并满足桌面动作的独立门禁。

## 常见结果与排查顺序

先区分失败来自 legacy 路由、Backend 签名验证、桌面复验还是专用产物状态，再核对相应身份、任务与回执；不要通过更换 hostId、清空记录或重复派发动作掩盖问题。

| 错误或现象 | 核对内容 |
| --- | --- |
| `401 api_key_scope_required` | 是否使用 legacy Bearer key，scope 是否适合该路由；网页登录不能替代 |
| `401 unknown_host_identity` / `409 host_identity_conflict` | 主机是否完成同步、当前证书是否与既有 hostId 绑定一致；换证书不会自动替换旧身份 |
| `401 request_time_out_of_range` / `409 replayed_request` | Unix 秒时间与服务器偏差、nonce 格式及是否已使用；重试保留任务幂等键，重新签名，不重放旧请求 |
| `401 unknown_or_revoked_device` / `403 device_scope_required` | 本机配对是否有效且已同步，创建能力要求的 scope 是否存在 |
| `409 idempotency_conflict` | 同主机是否复用了 key，原始 JSON 正文是否改变；不要把冲突当成新的任务已经建立 |
| `queued` / `delivered` 长期无最终结果 | TTL、桌面中继轮询、原始正文复验及本机门禁；accepted 后不再轮询，须查后续回执 |
| `window_snapshot_upload_required` / `invalid_window_snapshot_receipt` | 是否把快照完成当作普通 JSON 回执，或使用了不允许的状态/失败字段 |
| `window_snapshot_task_not_uploadable` / `window_snapshot_conflict` | 当前任务状态，以及重传的签名 metadata、产物记录与文件是否完全匹配 |
| `window_snapshot_expired` / `window_snapshot_consumed` / `window_snapshot_not_found` | 分别核对有效期、消费记录、原设备归属及产物文件；任务 completed 不能替代这些检查 |
| `window_snapshot_delete_failed` | 受限产物目录的文件删除失败；确认失败不会证明产物已删除 |
| overview 在线但不能操作 | 在线仅为 90 秒心跳投影，继续检查签名新鲜度、设备授权、Flow 与维护前提 |

## 源码与验证边界

- `test_operations_relay.py` 覆盖 legacy scope/目录/心跳-任务-回执、支持消息约束，以及 signed 往返、篡改/重放/撤销、受限 payload、幂等冲突、失败证据 schema、窗口快照上传/下载/consume 和清理失败等 Backend 协议分支；测试中的 completed 可由合成主机签名提交，不证明真实电脑执行。
- `test_operations_admin.py` 覆盖无认证拒绝、配置 Basic 成功、合成计数/新旧主机/设备状态、投影排除字段和非法 limit；不覆盖所有 Session/角色组合、并发一致快照或真实网络在线率。共用认证测试见[认证契约](./authentication.md)。
- `test_operations_support_store.py` 覆盖非 message 状态链、并发 requested 去重和未知主机拒写。它不是手机与桌面实际同意交互验收。
- `OperationsRelayProtocolTests.cs` 覆盖桌面签名任务复验、过期/撤销、scope/payload 和部分本机前提；仍不等于 Android—Backend—桌面全链路验收。普通回执终态不可回退、legacy 单主机隔离不属于当前已实现保证。

测试文件定位不表示已经执行；Backend 路由测试顶层导入 `app`，会先发生应用装配，再进入测试自己的临时路径设置，不能当作无副作用读取。文档校验不能代替获授权后的跨进程重试、离线/恢复、真实设备动作及证据生命周期验收。
