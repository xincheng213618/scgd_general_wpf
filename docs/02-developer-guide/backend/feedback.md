---
knowledge_id: "delivery.backend-feedback"
knowledge_type: "topic"
status: "current"
summary: "反馈提交按服务端账号归属，普通用户只读本人记录，研发只读账号/API key可下载全部诊断附件，管理员独立更新状态；新目录使用北京时间和机器标识。"
aliases: ["反馈收件箱", "我的反馈", "Feedback Inbox", "反馈上传", "feedback:read", "feedback:manage", "feedback_attachment_download", "download_feedback.ps1", "ownerUserId", "machineName", "serverReceivedAt", "feedback.json", ".admin.json"]
code_paths: ["Web/Backend/feedback_service.py", "Web/Backend/services/feedback_admin.py", "Web/Backend/routes/public_api.py", "Web/Backend/routes/admin_api.py", "Web/Backend/services/permission_service.py", "Web/Backend/services/api_key_service.py", "Web/Frontend/src/pages/FeedbackPage.tsx", "Web/Frontend/src/services/admin.ts", "Scripts/download_feedback.ps1", "UI/ColorVision.UI.Desktop/Feedback"]
test_paths: ["Web/Backend/test_feedback_service.py", "Web/Backend/test_feedback_admin.py", "Web/Backend/test_feedback_routes.py", "Web/Backend/test_feedback_download_script.py", "Web/Frontend/tests/feedback.test.ts", "Test/ColorVision.UI.Tests/FeedbackWindowLayoutTests.cs"]
related: ["delivery.backend", "delivery.backend-auth", "delivery.backend-accounts", "delivery.artifact-delivery", "ui.desktop"]
---

# 反馈归属、查询与诊断附件下载

`POST /api/feedback` 把反馈保存到 Backend 制品根的 `Feedback/<feedback_id>/`。反馈正文和附件仍以文件目录为事实源，SQLite 只保存账号、权限、API key 与审计，不替代反馈内容。`feedback_service.py` 负责提交，`feedback_admin.py` 负责目录投影、范围过滤、附件定位和状态 sidecar。

反馈目录在 **Backend 服务器**，不是提交客户端。应从服务启动输出确认当前 storage；不要在客户端安装目录推断服务端收件箱，也不要为升级本功能批量移动或改名旧目录。

## 提交与账号归属

提交使用 multipart form。匿名旧客户端仍可提交；已登录数据库账号提交时，Backend 只使用已验证 Session 中的稳定 `user_id` 写入 `ownerUserId` / `ownerUsername`。客户端表单里的同名字段被忽略，`userName`、`machineName`、`machineInfo`、版本和客户端时间均是诊断信息，不是授权依据。

桌面反馈窗口可选择 Web 账号登录后提交，也可明确选择匿名提交。Web 密码只用于本次 `/api/auth/login`，不写 URL、日志或桌面配置；Session cookie 随本次 `HttpClient` 生命周期结束。ColorVision 本地 RBAC 用户、Windows 用户名、机器名与 Web 账号不是同一身份，不能据此自动认领。

| 字段 | 契约 |
| --- | --- |
| `message`、`userName`、`appVersion`、`machineInfo` | 每项最多 4000 字符 |
| `machineName` | 最多 255 字符；仅用于显示、筛选与安全目录标签 |
| `clientSubmittedAt`、`diagnosticsCollectedAt` | 可选 ISO 8601，必须带时区，服务端归一为 UTC；未知时不从复制时间猜测 |
| 文件 | 全部 multipart 文件字段合计最多 10 个；净化后仍须有有效正文或附件 |
| 请求 | 总请求上限由 `MAX_CONTENT_LENGTH` 控制，当前为 500 MiB |

附件名在净化后拒绝大小写变体的 `feedback.json`、`.admin.json` 及其内部临时命名空间，攻击者不能上传状态或归属 metadata。`feedback.json` 用同目录临时文件、flush/fsync 和 `os.replace` 完成；替换前失败不会产生完整 metadata，但附件与目录不是整体事务，失败目录仍可能作为异常历史记录被管理员看见。

## 目录名与时间口径

新反馈 ID 同时是目录名，格式为：

```text
yyyyMMdd_HHmmss_BJT_<安全机器标识>_<12位唯一后缀>
```

日期时间明确使用北京时间 UTC+08:00，机器标签只保留 ASCII 字母、数字、连字符和下划线并限制长度；唯一后缀避免机器标签清洗或同秒提交碰撞。示例中的时间是 **服务端接收时间**，不是日志采集完成时间。`feedback.json.serverReceivedAt` 保留带偏移的 UTC 事实时间；页面明确按北京时间显示，筛选日期也按北京时间日界线转换为 UTC。

旧目录不改名。旧 metadata 优先使用 `serverReceivedAt`，没有则使用 `createdAt`，两者都没有才回退目录 mtime；机器名优先读 `machineName`，旧记录可从形如 `机器名 / Windows...` 的 `machineInfo` 恢复。恢复出的机器信息只用于展示和筛选，缺失显示“未知机器”。

## 读取范围与权限

登录用户统一使用以下只读接口：

| 方法与路径 | 普通数据库账号 | `developer` / `feedback:read` API key | 管理员 |
| --- | --- | --- | --- |
| `GET /api/feedback` | 仅 `ownerUserId` 等于当前账号 | 全部，包括历史未绑定 | 全部 |
| `GET /api/feedback/<id>` | 仅本人 | 全部 | 全部 |
| `GET /api/feedback/<id>/attachments/<filename>` | 仅本人 | 全部 | 全部 |
| `PUT /api/admin/feedback/<id>/status` | 拒绝 | 默认拒绝 | 需要 `feedback:manage` |

越权详情和附件统一返回 404，不能用 ID 或直链确认他人的记录是否存在。匿名读取返回 401；强制改密 Session 返回 `password_change_required`；撤销、禁用、版本不匹配的数据库 Session 会在请求前清除。历史没有 `ownerUserId` 的记录不会按 `userName`、Windows 用户名或机器名分配给普通用户，只在全局只读/管理范围出现，并标记“历史未绑定”。

`feedback:read` 是独立只读 permission/scope。`developer` 角色初始只有 `admin:access` 和 `feedback:read`；`feedback:manage` 自动包含 read，但普通 `user` 角色会保持 owner-scoped 访问，旧安装曾继承的反馈管理 grant 会被目录初始化移除。`admin:*` 仍满足管理接口。API key 目录只开放 `feedback:read`，无需给开发电脑完整管理员 key。

## 查询、统计与页面

列表支持 `status`、`query`、`machine`、`app_version`、`created_from`、`created_to`、`limit` 和 `offset`。时间边界必须是 ISO 8601；`created_to` 为排他上界。服务先按调用者归属、机器、版本和时间范围裁剪，再计算 summary、搜索、分页，所以普通用户不能从总数、状态统计、附件字节数或搜索结果推断其他账号数据。

`query` 最多 200 字符，匹配反馈 ID、客户端提交者、版本和前 160 字符问题摘要。`status=open` 表示未解决；前端“全部”会省略 status，不发送无效的 `all`。详情附件清单来自目录里的真实直接文件，不信任 metadata 的 `files`，并为每个附件计算 SHA-256。列表只统计大小，不对所有历史大文件逐个哈希。

前台 `/feedback` 对任何已登录账号开放，普通用户显示“我的反馈”；研发只读或管理员显示完整收件箱。页面包含状态、北京时间日期范围、机器、版本和文本筛选，详情展示归属、机器、问题、日志/数据库附件、大小与 SHA-256 摘要。附件使用受控 fetch，401/403/404/传输失败会显示错误，不把失败导航当成下载成功。管理员入口 `/admin/feedback` 复用同一页面，只有 API 返回 `can_manage=true` 时才显示状态动作。

## 开发电脑按编号下载

先由 API Key 管理员创建仅含 `feedback:read` 的 key，并通过安全渠道放入开发电脑进程环境。脚本不接受 URL 中的凭据，也没有仓库明文默认值：

```powershell
$env:COLORVISION_FEEDBACK_API_KEY = '<一次性安全传入的只读 key>'

# 可选查询，只列出候选，不会自动选择“全局最新”
.\Scripts\download_feedback.ps1 `
  -BaseUrl 'https://your-colorvision-host.example' `
  -List -Machine 'ARVR-STATION-07' -Query '黑屏'

# 必须明确给出反馈编号和本地输出根目录
.\Scripts\download_feedback.ps1 `
  -BaseUrl 'https://your-colorvision-host.example' `
  -FeedbackId '20260916_144318_BJT_ARVR-STATION-07_22ca2fafd648' `
  -OutputDirectory 'D:\ColorVisionFeedback'
```

脚本只允许 HTTPS；`-AllowInsecureLocalhost` 仅供本机模拟测试。它先取详情清单，再把每个附件流式写入同目录随机 `.part` 文件，核对字节数和服务端 SHA-256 后以原子 rename 落盘，最后写不可覆盖的 `feedback-manifest.json`。已存在且 hash 一致的附件会复用；内容不同则停止，不覆盖。失败临时文件会删除，未验证文件不会冒充完成。脚本只读反馈附件，不导入或覆盖 ColorVision / ARVRPro 正在运行的数据库。

## 状态 sidecar 与下载完成边界

状态仅允许 `new`、`in_progress`、`resolved`，保存在 `.admin.json`。更新使用临时文件、fsync 和 `os.replace`；它不改 `feedback.json`，也不删除附件。相同状态不重复写。状态、附件与 SQLite 审计不是同一事务。

附件解析拒绝路径穿越、内部文件、符号链接、子目录和解析后越界。HTTP 路由在开始发送前写 `feedback_attachment_download` 审计，并使用 `<feedback_id>__<原附件名>` 作为浏览器下载名；Flask 对该 GET 路由的默认 HEAD 请求也会进入同一处审计，因此审计存在仍不证明客户端完整落盘。开发脚本的完成标准是本地 size 与 SHA-256 均匹配且临时文件已原子落盘。

## 验证范围

`test_feedback_service.py` 覆盖北京时间跨日、同秒唯一 ID、机器名清洗/长度、稳定归属、伪造 owner、内部文件名和 metadata 替换失败。`test_feedback_admin.py` 覆盖历史记录、owner 范围先于统计/搜索/分页、机器/版本/时间筛选、hash、路径穿越和状态原子替换。`test_feedback_routes.py` 使用独立临时配置、账号数据库和 storage，覆盖两个账号隔离、匿名/撤销/强制改密 Session、研发只读、有效/撤销/过期 API key、直链越权与登录提交归属。`test_feedback_download_script.py` 用本机模拟 HTTP 服务验证正常、重复和截断下载；真实反向代理、TLS、超大现场 ZIP 与 ARVRPro 离线读取仍需部署前现场验证。
