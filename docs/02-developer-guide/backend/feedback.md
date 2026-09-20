---
knowledge_id: "delivery.backend-feedback"
knowledge_type: "topic"
status: "current"
summary: "反馈提交按服务端账号归属，普通用户只读本人记录，研发只读账号/API key可下载全部诊断附件，管理员独立更新状态；新目录使用北京时间和机器标识。"
aliases: ["反馈收件箱", "我的反馈", "Feedback Inbox", "反馈上传", "feedback:read", "feedback:manage", "feedback_attachment_download", "download_feedback.ps1", "ownerUserId", "machineName", "serverReceivedAt", "feedback.json", ".admin.json"]
code_paths: ["Web/Backend/feedback_service.py", "Web/Backend/services/feedback_admin.py", "Web/Backend/routes/public_api.py", "Web/Backend/routes/admin_api.py", "Web/Backend/services/permission_service.py", "Web/Backend/services/api_key_service.py", "Web/Frontend/src/pages/FeedbackPage.tsx", "Web/Frontend/src/services/admin.ts", "Scripts/download_feedback.ps1", "Scripts/configure_feedback.ps1", "UI/ColorVision.UI.Desktop/Feedback"]
test_paths: ["Web/Backend/test_feedback_service.py", "Web/Backend/test_feedback_admin.py", "Web/Backend/test_feedback_routes.py", "Web/Backend/test_feedback_download_script.py", "Web/Frontend/tests/feedback.test.ts", "Test/ColorVision.UI.Tests/FeedbackWindowLayoutTests.cs"]
related: ["delivery.backend", "delivery.backend-auth", "delivery.backend-accounts", "delivery.artifact-delivery", "ui.desktop"]
---

# 反馈归属、查询与诊断附件下载

`POST /api/feedback` 把反馈保存到 Backend 制品根的 `Feedback/<机器标签>/<反馈目录>/`。反馈正文和附件仍以文件目录为事实源，SQLite 只保存账号、权限、API key 与审计，不替代反馈内容。`feedback_service.py` 负责提交，`feedback_admin.py` 负责目录投影、范围过滤、附件定位和状态 sidecar。

反馈目录在 **Backend 服务器**，不是提交客户端。应从服务启动输出确认当前 storage；不要在客户端安装目录推断服务端收件箱，也不要为升级本功能批量移动或改名旧目录。

## 提交与账号归属

提交使用 multipart form。匿名旧客户端仍可提交；已登录数据库账号提交时，Backend 只使用已验证 Session 中的稳定 `user_id` 写入 `ownerUserId` / `ownerUsername`。客户端表单里的同名字段被忽略，`userName`、`machineName`、`machineInfo`、版本和客户端时间均是诊断信息，不是授权依据。

桌面反馈窗口直接匿名提交，不再弹出 Web 账号密码对话框。Backend 仍兼容其他已登录客户端的 Session 归属，但 ColorVision 本地 RBAC 用户、Windows 用户名和机器名与 Web 账号不是同一身份，不能据此自动认领。

| 字段 | 契约 |
| --- | --- |
| `message`、`userName`、`appVersion`、`machineInfo` | 每项最多 4000 字符 |
| `machineName` | 最多 255 字符；仅用于显示、筛选与安全目录标签 |
| `clientSubmittedAt`、`diagnosticsCollectedAt` | 可选 ISO 8601，必须带时区，服务端归一为 UTC；未知时不从复制时间猜测 |
| 文件 | 全部 multipart 文件字段合计最多 10 个；净化后仍须有有效正文或附件 |
| 请求 | 总请求上限由 `MAX_CONTENT_LENGTH` 控制，当前为 500 MiB |

附件名在净化后拒绝大小写变体的 `feedback.json`、`.admin.json` 及其内部临时命名空间，攻击者不能上传状态或归属 metadata。`feedback.json` 用同目录临时文件、flush/fsync 和 `os.replace` 完成；替换前失败不会产生完整 metadata，但附件与目录不是整体事务，失败目录仍可能作为异常历史记录被管理员看见。

## 目录名与时间口径

反馈编号与存放路径分离。API 使用原始 `feedback.json.feedbackId` 作为稳定编号，不能通过机器名推算身份或拼接磁盘路径。新提交以机器标签为一级目录，以接收时间和唯一后缀为二级目录；新记录的初始编号等于二级目录名：

```text
Feedback/<安全机器标识>/yyyyMMdd_HHmmss_BJT_<12位唯一后缀>/
```

日期时间明确使用北京时间 UTC+08:00，机器标签只保留 ASCII 字母、数字、连字符和下划线并限制长度；唯一后缀避免机器标签清洗或同秒提交碰撞。示例中的时间是 **服务端接收时间**，不是日志采集完成时间。`feedback.json.serverReceivedAt` 保留带偏移的 UTC 事实时间；页面明确按北京时间显示，筛选日期也按北京时间日界线转换为 UTC。

旧平铺目录仍可读取，升级本身不触发迁移。查询与附件定位只扫描旧平铺和“机器／反馈”两层，不递归扫描任意深度，不跟随符号链接或 Windows junction。相同稳定编号对应多个目录时拒绝详情和下载，避免误选另一台机器的附件。接收时间依次使用有效的 `serverReceivedAt`、`createdAt` 和目录名中的时间：含 `_BJT_` 的名称按北京时间解释，早期 `yyyyMMdd_HHmmss_<后缀>` 按 UTC 解释。全部缺失或无效则显示未知并排在最后，绝不使用文件或目录修改时间。比较前统一为 UTC，避免带不同时区的字符串排序出错。机器名优先读 `machineName`，旧记录可从形如 `机器名 / Windows...` 的 `machineInfo` 恢复；旧版客户端的新提交也使用这个机器名生成目录标签。恢复出的机器信息只用于展示和筛选，缺失显示“未知机器”。

仅当用户明确要求整理历史目录时，才将旧目录迁移到上述命名格式，并先保存完整新旧路径对照表、检查目标路径和重名。保留原唯一后缀，不改写附件或原始 `feedback.json`；其中的历史 `feedbackId` 始终是 API 使用的编号，移动目录不改变它。没有有效 metadata 的历史目录放入 `UNKNOWN`，保留原二级目录名作为编号，不猜测机器身份。完成后刷新索引，并核对文件数量、大小和原始 metadata。

提交成功后刷新 `Feedback/index.html`，提供按北京时间倒序排列的机器、提交版本、反馈编号和附件链接，可从共享目录直接打开、用 Ctrl+F 查找。索引更新失败记录警告，但不会把已经保存的反馈报告为上传失败。部署已有目录时可单独调用 `services.feedback_admin.write_feedback_index(storage)` 生成初始索引；它只替换派生索引，不改写原反馈及其 ID。

## 读取范围与权限

登录用户统一使用以下只读接口：

| 方法与路径 | 普通数据库账号 | `developer` / `feedback:read` API key | 管理员 |
| --- | --- | --- | --- |
| `GET /api/feedback` | 仅 `ownerUserId` 等于当前账号 | 全部，包括历史未绑定 | 全部 |
| `GET /api/feedback/<id>` | 仅本人 | 全部 | 全部 |
| `GET /api/feedback/<id>/attachments/<filename>` | 仅本人 | 全部 | 全部 |
| `PUT /api/admin/feedback/<id>/status` | 拒绝 | 默认拒绝 | 需要 `feedback:manage` |
| `PUT /api/admin/feedback/status` | 拒绝 | 默认拒绝 | 批量更新，需要 `feedback:manage` |

越权详情和附件统一返回 404，不能用 ID 或直链确认他人的记录是否存在。匿名读取返回 401；强制改密 Session 返回 `password_change_required`；撤销、禁用、版本不匹配的数据库 Session 会在请求前清除。历史没有 `ownerUserId` 的记录不会按 `userName`、Windows 用户名或机器名分配给普通用户，只在全局只读/管理范围出现，并标记“历史未绑定”。

`feedback:read` 是独立只读 permission/scope。`developer` 角色初始只有 `admin:access` 和 `feedback:read`；`feedback:manage` 自动包含 read，但普通 `user` 角色会保持 owner-scoped 访问，旧安装曾继承的反馈管理 grant 会被目录初始化移除。`admin:*` 仍满足管理接口。API key 目录只开放 `feedback:read`，无需给开发电脑完整管理员 key。

## 查询、统计与页面

列表支持 `status`、`query`、`machine`、`app_version`、`created_from`、`created_to`、`limit` 和 `offset`。时间边界必须是 ISO 8601；`created_to` 为排他上界。服务先按调用者归属、机器、版本和时间范围裁剪，再计算 summary、搜索、分页，所以普通用户不能从总数、状态统计、附件字节数或搜索结果推断其他账号数据。

`query` 最多 200 字符，匹配反馈 ID、客户端提交者、版本和前 160 字符问题摘要。`status=open` 表示未解决；前端“全部”会省略 status，不发送无效的 `all`。详情附件清单来自目录里的真实直接文件，不信任 metadata 的 `files`。默认详情为每个附件计算 SHA-256，供下载脚本校验；网页查看详情显式使用 `include_hashes=false`，仅读取附件名称、大小和修改时间。列表和状态更新也不读取大附件计算 hash。

前台 `/feedback` 对任何已登录账号开放，普通用户显示“我的反馈”；研发只读或管理员显示完整收件箱。服务端为 `/feedback` 注册 SPA 入口，支持直接访问和浏览器刷新；匿名访问页面后进入登录流程，反馈数据仍须经过 API 授权。页面包含状态、北京时间日期范围、机器、版本和文本筛选，详情展示归属、机器、问题、日志/数据库附件及大小。附件使用受控 fetch，401/403/404/传输失败会显示错误，不把失败导航当成下载成功。管理员入口 `/admin/feedback` 复用同一页面，只有 API 返回 `can_manage=true` 时才显示状态动作。

管理员可在列表直接标记已解决或重新打开，也可在详情开始处理、标记已解决和刷新。状态更新只合并当前反馈的状态及更新时间，保留详情权限和附件；迟到响应不能替换另一个反馈或重新打开已关闭的详情。更新后重新请求列表与统计。列表和详情读取超时会显示错误并允许重试，“处理中”计数使用静态时钟图标，不表示网络请求仍在加载。

批量操作支持勾选记录或“一键解决当前筛选”。后一操作按当前机器、版本、日期和文本条件获取未解决记录，确认实际数量后发送固定编号集合；不受当前分页或状态标签限制，确认之后的新提交不会包含在这次更新内。每次最多 500 条，超过时先缩小筛选范围。批量请求正文只接受 `feedback_ids`（不重复的编号数组）和 `status`；服务端逐条返回成功、未变化或失败，并为实际变化写审计。部分失败不会报告全部成功，失败项保留勾选供重试。

## 开发电脑读取反馈

统一入口为 PowerShell 7 的 `Scripts\download_feedback.ps1`。默认 `-Source Auto`：若本机有 `H:\ColorVision\Feedback` 挂载则直接只读定位附件，否则通过已配置的远程 API 下载。显式提供 `-BaseUrl` 或 `-Source Remote` 时走网络；`-Source Local -LocalRoot <路径>` 可指定另一挂载。不要重复下载已有共享附件，也不要修改共享目录里的原反馈文件。

用户明确要求“最新反馈”时使用 `-Latest`，按接收时间选择最新匹配记录，并报告所选机器名、北京时间与编号；不要求用户每次先手工找 ID。尚未写完或无法读取 `feedback.json` 的目录不参与自动选择，远程列表按需继续分页查找。需要指定机器时加 `-Machine`，浏览候选时用 `-List`。`-Latest` 找不到有效接收时间时明确失败，不把未知时间记录当作最新。原始文件时间受复制、同步影响，不参与排序。

```powershell
# 本机优先共享目录；没有挂载时自动从 API 下载到用户缓存目录
pwsh -NoProfile -File .\Scripts\download_feedback.ps1 -Latest
pwsh -NoProfile -File .\Scripts\download_feedback.ps1 -Latest -Machine 'ARVR-STATION-07'
pwsh -NoProfile -File .\Scripts\download_feedback.ps1 -List
```

## 其他电脑按编号下载

先由 API Key 管理员创建仅含 `feedback:read` 的 key。维护电脑不需要管理员账号或交互登录；一台电脑配置一次即可，key 可单独撤销。Windows 初始化脚本通过隐藏输入读取 key，保存到当前用户环境；凭据不进入仓库、命令示例或工具输出。不要把可用 key 随 clone 分发。非 Windows 环境设置同名进程/用户环境变量。

```powershell
# 一次性配置：执行后在隐藏输入提示中粘贴反馈只读 key
pwsh -NoProfile -File .\Scripts\configure_feedback.ps1 `
  -BaseUrl 'http://xc213618.ddns.me:9998' -AllowInsecureHttp

# 远程获取最新反馈；已经完整下载且校验一致的附件直接复用
pwsh -NoProfile -File .\Scripts\download_feedback.ps1 -Latest -Source Remote

# 可选查询，只列出候选
.\Scripts\download_feedback.ps1 `
  -BaseUrl 'https://your-colorvision-host.example' `
  -List -Machine 'ARVR-STATION-07' -Query '黑屏'

# 也可按明确编号下载，并指定输出目录
.\Scripts\download_feedback.ps1 `
  -BaseUrl 'https://your-colorvision-host.example' `
  -FeedbackId '20260916_144318_BJT_ARVR-STATION-07_22ca2fafd648' `
  -OutputDirectory 'D:\ColorVisionFeedback'
```

地址优先使用显式 `-BaseUrl`，其次 `COLORVISION_FEEDBACK_BASE_URL`，最后才是当前服务默认地址。key 使用 `COLORVISION_FEEDBACK_API_KEY`，Windows 上也读取当前用户的已保存环境变量。默认下载缓存为用户 LocalApplicationData 下的 `ColorVision/Feedback/<反馈编号>`。它先取详情清单，再把每个附件流式写入同目录随机 `.part` 文件，核对字节数和服务端 SHA-256 后以原子 rename 落盘，最后写不可覆盖的 `feedback-manifest.json`。已存在且 hash 一致的附件会复用；内容不同则停止，不覆盖。失败临时文件会删除，未验证文件不会冒充完成。脚本只读反馈附件，不导入或覆盖 ColorVision / ARVRPro 正在运行的数据库。

HTTPS 始终优先。现有 HTTP 部署只能在用户明确选择 `-AllowInsecureHttp` 或保存 `COLORVISION_FEEDBACK_ALLOW_HTTP=1` 后使用；`-AllowInsecureLocalhost` 仍仅限模拟测试。HTTP 不加密 key 与诊断附件，使用只读 key 只限制权限，不等于加密。脚本不跟随重定向，也不会绕过 TLS 证书校验。后续配置 HTTPS 时更新服务地址并取消 HTTP 选项。

动态公网 IP 加 DDNS 可以承载 HTTPS，证书绑定域名。但使用公开 CA 自动签发，需要公网 80 的 HTTP-01、公网 443 的 TLS-ALPN-01，或控制 DNS TXT 的 DNS-01 验证。外部只能开放 18080/18443 且没有 DNS 权限时，不能用这些高端口替代标准验证端口；自签名证书也不能让新电脑的浏览器自动信任。当前条件下保留 HTTP，不声称已经完成 HTTPS。

## 状态 sidecar 与下载完成边界

状态仅允许 `new`、`in_progress`、`resolved`，保存在 `.admin.json`。可从待处理直接标记已解决，无须先经过处理中。更新使用临时文件、fsync 和 `os.replace`；它不改 `feedback.json`，也不删除附件。相同状态不重复写。单条更新响应继续提供管理权限字段，但网页只合并状态字段，不把状态响应替换为新的详情。状态、附件与 SQLite 审计不是同一事务；批量也不是跨记录的整体事务，必须核对逐条结果。

管理控制台 `/admin` 按当前反馈读取或管理权限展示未解决、待处理和处理中数量，以及最早未解决反馈的接收时间；点击进入 `/admin/feedback?status=open|new|in_progress` 对应筛选。摘要加载失败显示不可用，不能当作零条待办。客户公共主页不展示这些管理数据。

反馈详情支持 `id` 查询参数，复制链接、直接访问或刷新后可重新打开同一稳定编号；未登录时登录返回地址保留编号和状态筛选。管理入口的详情额外显示内部处理记录，公共 `/feedback` 不加载或展示该记录。`GET /api/admin/feedback/<id>/handling` 要求 `feedback:read` 或 `feedback:manage`，`PUT` 要求 `feedback:manage` 并沿用浏览器 CSRF 检查。原有公共详情、列表和附件接口不交付内部记录。

处理记录 PUT 严格接受 `conclusion`（最多 4000 字符）、`fixed_version`（100 字符）、`verification`（4000 字符）及非负整数 `revision`；三个文本字段允许空值，状态仍可独立更新。操作人从已验证请求身份取得，客户端不能指定。记录保存在 `.admin.json.handling`，包含最新内容、操作人、UTC 修改时间、递增修订号和最近 20 次实际内容修改的快照；界面明确显示此历史上限。相同内容不新增历史或审计，状态切换和批量解决保留处理记录。写入与状态更新共用进程内锁并原子替换 sidecar；修订号不匹配返回 409，表单保留草稿供核对。当前锁不提供多进程并发写入保护。SQLite 审计仅记录反馈编号和修订号，不复制处理正文，也不与 sidecar 构成同一事务。

已有 `.admin.json` 无法解析时，状态更新与处理记录保存均拒绝覆盖；批量操作把该条记为失败，保留管理文件供排查，避免用空状态覆盖已有处理记录。

附件解析拒绝路径穿越、内部文件、符号链接、子目录和解析后越界。HTTP 路由在开始发送前写 `feedback_attachment_download` 审计，并使用 `<feedback_id>__<原附件名>` 作为浏览器下载名；Flask 对该 GET 路由的默认 HEAD 请求也会进入同一处审计，因此审计存在仍不证明客户端完整落盘。开发脚本的完成标准是本地 size 与 SHA-256 均匹配且临时文件已原子落盘。

## 验证范围

`test_feedback_service.py` 覆盖北京时间跨日、同秒唯一 ID、机器名清洗/长度、稳定归属、伪造 owner、内部文件名和 metadata 替换失败。`test_feedback_admin.py` 覆盖历史记录、owner 范围先于统计/搜索/分页、机器/版本/时间筛选、hash、路径穿越和状态原子替换。`test_feedback_routes.py` 使用独立临时配置、账号数据库和 storage，覆盖两个账号隔离、匿名/撤销/强制改密 Session、研发只读、有效/撤销/过期 API key、直链越权与登录提交归属。`test_feedback_download_script.py` 用本机模拟 HTTP 服务验证正常、重复和截断下载；真实反向代理、TLS、超大现场 ZIP 与 ARVRPro 离线读取仍需部署前现场验证。
