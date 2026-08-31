---
knowledge_id: "delivery.backend-operations"
knowledge_type: "topic"
status: "current"
summary: "Backend Operations 的 Bearer 与设备签名中继、任务回执和管理员只读投影；在线、排队、验签与真实动作完成各有边界。"
aliases: ["Operations中继", "运维后台", "设备签名中继", "operations_overview", "OperationsDeviceRelayService", "OperationsRelayContext", "OperationsAdminQuery", "SqliteOperationsAdminQuery", "OperationsSupportStore", "SqliteOperationsSupportStore", "OperationsSafeSnapshot", "ops:relay", "ops:operator", "operations:manage", "COLORVISION_OPERATIONS_RELAY_URL", "COLORVISION_OPERATIONS_RELAY_KEY", "signedRelayReady", "awaiting_local_consent"]
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

请求签名覆盖 method、path、timestamp、nonce 和原始 body 的 SHA-256；服务端允许 2 分钟时钟偏差，nonce 按主体存储 5 分钟防重放。新任务还检查未撤销配对记录和 capability 对应 scope，并将原始请求正文/时间/nonce/签名一起保留给桌面。桌面 `OperationsRelayProtocol.TryVerifyDeviceTask` 再次验证本机配对、签名和任务条件。Backend 接受不等于桌面必定接受；撤销传播、过期任务或本机前提不符仍可导致拒绝。

两条创建路线有不同的目录和 payload 规则：

- Legacy `ALLOWED_TASK_CAPABILITIES` 仅为 `ops.diagnostics.request`、`ops.support.message`、`ops.deployment.verify`，不接受 `ops.service.restart`。`support.message` 要求精确的 `sessionId` / `text` 且会话 active；另外两种在此路由只校验对象、大小和顶层 `command` / `executablePath` / `shell` / `script` 禁字段，不能宣称所有额外字段均被拒绝或这些字段会成为可执行命令。
- Signed `ALLOWED_DEVICE_TASK_CAPABILITIES` 是当前窗口、消息恢复、Flow 取消、固定服务/应用重启和受限诊断/快照的有限目录。普通窗口/恢复/取消/重启必须空 payload；诊断请求只允许 `reason`；失败证据读取必须显式空 payload；加密窗口快照需要精确 `scheme` / `recipientPublicKeySpki`，不是任意路径、窗口或命令选择器。具体动作、桌面检查和手机可见入口继续查 [Android 运维伴侣](./android-operations.md#可见能力不等于操作许可)，不能从 capability 名称绕过本机门禁。

桌面执行目标也固定：窗口动作由 `OperationsDesktopActionService` 作用于 `Application.Current.MainWindow`，消息恢复由 `EngineOperationsMessageChannelHealthProvider` 使用当前MQTT连接/订阅；`FlowOperationsRuntimeStatusProvider` 调用主工作区 `manager.View.StopFlowCommand`，成功可以只是取消请求被接受，不是采集已停止。`ops.service.restart` 由 `ServiceHostOperationsMqttRestartController` 固定重启本机mosquitto；relay在受理前及发送accepted后复查Flow非活动、服务可用/适用且支持维护，仅接受running/stopped/paused状态。设备不能传服务名或维护参数来选择其它目标。

上传的 host capabilities 用于展示；`create_task` 的准入依据是服务端目录与设备 scope，不以该主机最近广告的 capabilities 或“在线”字段代替执行前提。设备读取主机快照也不因 `lastSeenAt` 陈旧自动失败，调用者仍需校验签名内容与新鲜度。

## 排队、重复投递与回执的成功边界

| 返回或状态 | Backend 实际完成的步骤 |
| --- | --- |
| 创建返回 `202` / `queued` | 任务意图已写入 Operations 表，不是桌面受理或执行成功 |
| 轮询后 `delivered` | 服务端已选中任务并更新标记，然后组成响应；响应丢失仍可能留下该状态 |
| `received`、`accepted`、`awaiting_local_consent` 回执 | 回执记录保留原状态，任务行统一归为 `accepted`，不能以任务行分辨“收到”与“等待本机同意” |
| `completed`、`failed`、`rejected` 回执 | 按回执更新任务行。普通 signed 回执另外验证主机签名 envelope 与 host/task/idempotency/status/evidence 对应关系；记录主机声称的结果，不是 Backend 再次执行或独立验证硬件效果 |

普通任务 TTL 默认 900 秒并钳制到 60–3600 秒；signed 窗口快照另要求明确整数 `ttlSeconds=300`。轮询每次最多 50 条，仅取未过期的 `queued` / `delivered`，按创建时间排序；signed 主机轮询还要求 `source_type='device'`。`delivered` 会再次返回，不是 exactly-once；收到上述非终态回执转为 `accepted` 后则不再进入这一轮询集合。到期只是投递筛选条件，不自动把普通任务状态改成 `expired`，也不证明已停止桌面作业。

创建任务的去重键是 `(hostId, idempotencyKey)`。Legacy 冲突返回已有 taskId，不核对新旧 payload；signed 冲突只有原始 body 文本相同才返回已有 taskId，否则 `409 idempotency_conflict`。已提交nonce的签名请求重试需新nonce，原签名重放被拒绝；nonce与所属数据库事务一起提交，事务失败回滚不等于已记录使用。普通 signed 回执以同 task/host/status 和完全相同的 envelope body 去重；重新生成 `signedAt` 就是不同记录，legacy 回执没有相同的 envelope 去重。

普通回执写入没有完整的单向状态迁移门禁：非窗口快照任务的新回执仍可覆盖已有终态，因此不能承诺“completed 永不回退”或把 receiptCount 当成功次数。应核对实际回执、来源与动作证据；应用重启的 `accepted` 早于请求关闭，最终回执由桌面持久化工作记录另行上报，不是 accepted 即重启成功。

应用重启成功路径在替换进程启动后，由 `App.xaml.cs` 调用 `OperationsApplicationRestartHandoff.CompletePending` 匹配持久化handoff及其新鲜度、完成工作记录，再由relay发送最终签名回执。失败终态也可能由旧进程上报；不能把“已有最终回执”统一解释成替换进程已经成功启动。

窗口快照在签名协议中是专用例外：该协议的普通 receipts 路由不接受快照任务的 `completed`，必须通过受限二进制上传，核对签名 metadata、长度、摘要、TTL 和任务归属后存储密文与完成回执；后续下载/consume 有自己的设备归属和状态规则。这个限制不能套用到未按source_type隔离的legacy路由。也不能将普通 JSON 回执规则推广成快照已上传、可下载或已消费的保证，或由 overview 推导证据下载权限。

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

## 源码与验证边界

- `test_operations_relay.py` 覆盖 legacy scope/目录/心跳-任务-回执、支持消息约束，以及 signed 往返、篡改/重放/撤销、受限 payload、幂等冲突、失败证据 schema、窗口快照上传/下载/consume 和清理失败等 Backend 协议分支；测试中的 completed 可由合成主机签名提交，不证明真实电脑执行。
- `test_operations_admin.py` 覆盖无认证拒绝、配置 Basic 成功、合成计数/新旧主机/设备状态、投影排除字段和非法 limit；不覆盖所有 Session/角色组合、并发一致快照或真实网络在线率。共用认证测试见[认证契约](./authentication.md)。
- `test_operations_support_store.py` 覆盖非 message 状态链、并发 requested 去重和未知主机拒写。它不是手机与桌面实际同意交互验收。
- `OperationsRelayProtocolTests.cs` 覆盖桌面签名任务复验、过期/撤销、scope/payload 和部分本机前提；仍不等于 Android—Backend—桌面全链路验收。普通回执终态不可回退、legacy 单主机隔离不属于当前已实现保证。

测试文件定位不表示已经执行；Backend 路由测试顶层导入 `app`，会先发生应用装配，再进入测试自己的临时路径设置，不能当作无副作用读取。文档校验不能代替获授权后的跨进程重试、离线/恢复、真实设备动作及证据生命周期验收。
