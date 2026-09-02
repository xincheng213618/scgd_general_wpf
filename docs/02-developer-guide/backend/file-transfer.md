---
knowledge_id: "delivery.file-transfer"
knowledge_type: "topic"
status: "current"
summary: "Web文件中转的上传、取消与续传、完成判定、权限、覆盖和公开分享；队列不持久化，分享绑定当前文件名。"
aliases: ["文件中转", "Transfer", "file:transfer", "Upload-Offset", "X-Transfer-Client", "stream_transfer_upload", "create_or_resume_transfer_upload", "get_or_create_transfer_share", "断点续传", "匿名上传", "文件分享过期", "TransferPanel", "uploadTransferFile", "取消上传", "重试失败项", "服务器确认中", "colorvision.transfer.client-id"]
code_paths: ["Web/Backend/routes/transfer.py", "Web/Backend/transfer_files.py", "Web/Backend/app_setup.py", "Web/Backend/config_loader.py", "Web/Backend/services/auth_policy.py", "Web/Backend/services/permission_service.py", "Web/Backend/services/scheduler.py", "Web/Backend/services/artifact_delivery.py", "Web/Backend/routes/artifact_delivery.py", "Web/Frontend/src/pages/TransferPage.tsx", "Web/Frontend/src/pages/TransferSharePage.tsx", "Web/Frontend/src/components/TransferPanel.tsx", "Web/Frontend/src/services/transferUpload.ts", "Web/Frontend/src/services/transferShares.ts", "Web/Frontend/src/utils/transferAccess.ts"]
test_paths: ["Web/Backend/test_transfer_files.py", "Web/Backend/test_auth_policy.py", "Web/Frontend/tests/transferAccess.test.ts"]
related: ["delivery.backend", "operations.file-server", "operations.exports"]
---

# 文件中转、覆盖与公开分享

Web 的“文件中转”（`/transfer`）用于多文件上传、断点续传和链接分享。上传由 `TransferPanel` / `transferUpload.ts` 调用 Backend 中转 API，`transfer_files.py` 管理文件、会话和分享记录。它不解析或发布插件包，也不刷新插件市场索引。文件列表和直接下载需要授权；分享链接则允许持有者公开访问。

配置与数据库、启动副作用见[Backend 组成](./README.md)。本页的上传、覆盖、删除和配置说明不是执行这些动作的授权。

## 使用网页中转文件

1. 打开 `/transfer`。已登录账号需要 `file:transfer` 权限；访客入口取决于下文的匿名上传开关。页面先确认登录状态；需改密码时进入账号页，已登录但缺权限时也进入账号页，未开放访客上传时跳转登录并保留原目标地址。
2. 点击“选择文件”或拖放文件，将文件加入队列，然后点击“开始上传”。选择文件不会自动上传。队列按去除首尾空白、忽略大小写后的文件名去重，同名项会被忽略；上传过程中不能继续添加文件。
3. 查看每一项的状态。文件逐个上传，普通失败后会继续后面的文件；认证失败或用户取消会停止后续上传。进度到 100% 时可能仍显示“服务器确认中”，只有该项变成“上传完成”才表示收到完成结果。汇总中的“完成 N”仅计算成功项，不保证整批全部成功。
4. 完成后点击“复制链接”。已登录用户也可在下方“文件”列表分享、下载或删除；访客没有这个列表，应在关闭页面前保存分享链接。分享页提供文件名、大小、更新时间、到期时间（如有）和“下载文件”。复制失败时可从分享页地址栏复制。

已登录用户开始上传前，会按已加载的文件列表提示“将覆盖”并要求“覆盖并上传”。这是客户端预检，不会锁住文件名，也不能保证期间没有其他上传。访客没有文件列表可供预检，同名冲突由服务端拒绝。覆盖会影响已发出的分享链接，具体见下文。

### 取消、重试与刷新

| 操作或现象 | 处理方式与结果 |
| --- | --- |
| 取消正在上传的队列 | “取消上传”中止当前请求并把待传项标为“已取消”；已完成项保留。它不调用服务端删除，也不保证尚未开始的项已有断点 |
| 重试失败或取消项 | “重试失败项”只重试失败项；取消项需点击该行“重新上传”。“开始上传”只处理“等待上传”项。仍有效且匹配的未完成会话可从确认 offset 继续 |
| 从队列移除、清除已完成 | 只移除当前页面的条目，不删除服务端文件、会话或分享。删除已完成文件需使用下方文件列表的删除动作并确认 |
| 刷新页面、关闭后重开 | 队列和本地 `File` 对象不持久化；需重新选择原文件并开始上传，由服务端查找匹配会话。重新打开页面不会自动继续读取本地文件 |
| 访客换浏览器或清理站点数据 | `X-Transfer-Client` 来自同源 localStorage 的 `colorvision.transfer.client-id`；改变这个标识不能接管原匿名会话。localStorage 不可用时只保留内存备用标识，刷新后不能依赖它恢复 |

恢复要求上传身份、文件名、大小和指纹相符，且未完成会话仍在保留期内。已登录账号与访客身份之间切换不能接续同一会话。浏览器指纹还包含文件修改时间，重命名或修改文件也可能重新创建会话。

如果上次其实已完成，只是页面丢失结果，重新添加文件不会复用已完成回执：授权用户可能再次覆盖，访客可能遇到同名冲突。先用已保存的分享链接或授权文件列表确认。单次上传过程中的断线恢复会查询原会话，规则见“断点与重试”。

分享页的“无需登录”提示不能代替站点配置；其“我也要传文件”仍进入 `/transfer` 并重新检查访问条件。页面将请求失败统一显示为“分享链接已失效”，排障还需看具体错误：网络失败并不证明文件已过期或被删除。

## 路径和访问控制

`transfer_upload_dir` 默认是 `Transfer`，相对路径拼在 `storage_path` 下，也允许显式绝对路径；因此 `--storage` 不一定移动绝对配置的中转目录。相对配置包含 `..` 时拒绝。文件名入口只接受目录直属文件：拒绝斜杠、反斜杠、冒号、控制字符和 `.uploading` 后缀，并检查解析后的目标仍位于中转根内。

`.transfer_uploads/` 保存会话 JSON 与未完成数据，`.transfer_shares/` 保存分享 JSON。它们不是普通列表可见的业务子目录；列表跳过目录、点开头文件和未完成临时文件。

| 访问方式 | 实际权限边界 |
| --- | --- |
| 普通文件列表、直接下载、整文件上传、删除 | 共用策略检查 `file:transfer`；可接受配置 Basic、获准的 Session 或 Bearer，`admin:*` 可满足该共用 scope 检查 |
| 普通用户 Session | 每次按角色权限判定，不是登录就永久全权；这些文件接口操作共享目录，不按上传者过滤文件列表 |
| 断点会话 | 在文件权限基础上额外匹配 `owner_type`/`owner_id`；其他身份查询同一会话返回 `404`，持有文件权限不等于接管所有会话 |
| 匿名断点上传 | 默认关闭；启用后仅允许带合法 `X-Transfer-Client` UUID 的创建、读取和追加本人会话；该 UUID 用于匿名客户端连续性，不是账号认证 |
| 分享元数据、分享下载 | 持 token 即可访问，不要求原上传者 Session 或 `file:transfer`，也不要求此时仍启用匿名上传 |

匿名开关只在值为 JSON `true` 时启用；默认大小上限为 `2147483648` 字节，非法或非正上限回退到该默认值。配置变更应遵循 Backend 的配置加载边界，例如修改配置文件并重新启动是有状态操作：

```json
{
  "anonymous_transfer_upload_enabled": true,
  "anonymous_transfer_max_bytes": 2147483648
}
```

匿名请求不能使用普通列表、直接下载、整文件上传或删除接口，也不能覆盖同名目标；它仍能通过公开分享链接下载。已登录却缺权限的 Session、要求改密码的 Session，以及携带无效显式 Authorization 的请求不会降级成匿名上传。浏览器写请求还受共用 CSRF/同源边界约束；不能把有 scope 等同于绕过浏览器保护。

## API 与完成信号

| 方法与路径 | 契约 |
| --- | --- |
| `GET /api/transfer/files` | 列出直属文件及直接下载、分享链接；不是递归浏览 |
| `PUT/POST /api/transfer/files/<filename>` | 流式整文件上传，成功替换已有目标时返回 `200`，新文件返回 `201`，响应带 `replaced` |
| `GET /api/transfer/files/<filename>` | 受保护的文件下载，经共用 artifact delivery 返回内容 |
| `DELETE /api/transfer/files/<filename>` | 删除目标文件和按该文件名匹配的分享元数据，不移入回收站 |
| `POST /api/transfer/uploads` | 以 `filename`、`total_size`、`fingerprint` 创建或恢复断点会话；响应包含 `upload_id`、确认 `offset`、`complete`、建议 `chunk_size` |
| `GET /api/transfer/uploads/<upload_id>` | 查询所属会话的服务端确认进度，可能协调磁盘与会话元数据 |
| `PATCH /api/transfer/uploads/<upload_id>` | 按 `Upload-Offset` 顺序追加数据，返回新的确认进度和完成状态 |
| `GET /api/transfer/shares/<token>` / `.../<token>/download` | 公开读取分享元数据或下载其当前文件；分享页地址为 `/transfer/share/<token>` |

协议建议块大小为 8 MiB，服务端单块最大为 16 MiB。整文件与 PATCH 通过输入流读取，绕过普通包上传的 500 MiB 全局限制；这不取消匿名总大小限制、单块限制、磁盘容量或反向代理的请求体/超时约束。

### 整文件覆盖

`stream_transfer_upload` 将请求写入同目录下唯一的 `.uploading` 临时文件，读完后 `os.replace` 到最终文件名。同名文件默认被替换，没有“不存在才创建”的请求前置条件。失败时清理该临时文件，但替换成功后还会继续创建/复用分享、写审计；这些后续步骤失败不会自动恢复旧文件，因此不能把错误响应一概当作“磁盘完全没变”。

这条流式路径本身不校验包 manifest、文件签名或完整内容哈希，也不保存旧版本。需要保留历史或避免影响已发出的链接，应在上传前明确目标文件名和覆盖授权，而非依赖接口自动备份。

### 断点与重试

新建/恢复会话按上传者、精确文件名、声明大小和由 64 个十六进制字符组成的 `fingerprint` 匹配未完成会话。网页用文件名、大小、`lastModified` 和首尾各最多 64 KiB 的样本计算 SHA-256 指纹；这不是整个文件的内容哈希。服务端只校验指纹格式并用它找会话，不重算上传内容的哈希，因此 fingerprint 不是服务端完整性验收。零字节文件可在创建会话时直接完成。

追加只接受与服务端确认值相等的 offset；错误 offset 返回 `409`，缺少/无法解析的请求头为 `400`，过大块为 `413`，超过声明总大小为 `409`。同一进程用会话锁串行追加；不要把它推断成多个独立服务进程共享目录时的分布式锁。

每次从已确认 offset 截断并继续写 `.part`，写完块后才持久化确认进度。断线或响应丢失时，应重新查询确认 offset，而不是直接重发下一块；半块数据可能已落在临时文件中但尚未被确认。服务端协调逻辑可以根据已有部分文件与元数据恢复，也可能在最终文件大小符合声明时修复完成记录，仍不是内容哈希验证。

网页对分块网络错误、`409` 和 `5xx` 先查询原会话；已完成则返回结果，offset 有变化则从新确认位置继续。查询未能取得进展时，同一分块最多尝试 3 次，中间等待 500 ms、1000 ms，再提示手动重试。其它 `4xx` 不走这条自动重试；创建会话请求也没有此分块重试循环。

到达总大小后，授权用户的断点上传也会替换同名目标；匿名会话在创建及最终提交处拒绝已存在的同名文件。成功响应的 `complete=true` 表示这条会话完成，不表示文件以后不可覆盖或永久存在。当前协议没有取消并删除会话的 DELETE 入口；停止客户端上传不是立即清理服务端临时文件。

## 分享绑定文件名，不绑定不可变内容

上传完成会创建或复用随机 token 的分享。`get_or_create_transfer_share` 按文件名匹配已有记录并复用 token；下载时再按记录中的文件名解析当前文件，而不是校验某次上传的 hash 或版本。

因此，授权用户覆盖同名文件后，先前收到分享链接的人仍可通过旧 token 读取新内容。分享链接是访问凭据，不要把它当作只有上传者能访问的普通站内路径，也不要把“精确文件”解释为不可变上传快照。删除文件时会移除匹配分享；在同名文件仍存在期间，普通重传不是撤销分享。

`GET /api/transfer/files` 不只是读目录：它先清理过期临时文件，还会为缺少分享的文件建立分享元数据。查询断点状态可能改写协调后的会话 JSON；读取过期分享也会触发清理。不能把所有 GET/HEAD 当成零磁盘副作用。

## 到期与整文件重传的例外

| 对象或路径 | 当前保留语义 |
| --- | --- |
| 未完成会话及部分数据 | 根据 `updated_at` 超过 7 天后可被会话清理移除；创建会话时触发该清理，不是精确到秒的删除定时器 |
| 已完成会话回执 | 根据 `updated_at` 超过 1 天后可清理；回执过期不等同于授权用户的最终文件过期 |
| 正常完成的匿名文件/分享 | 分享记录到期时间通常为完成后 24 小时；到期访问可返回 `410` 并触发删除文件及分享记录 |
| 正常完成的授权断点上传 | 显式传 `expires_at=0`，新分享无到期时间，并可清除已复用分享的旧临时到期时间 |
| 授权整文件 PUT/POST | 新分享默认无到期时间；但调用分享函数时没有显式传 `expires_at=0`，复用已有临时分享时不会清除旧到期时间 |

最后一项是当前需要单独注意的例外：用整文件接口覆盖已有匿名临时文件后，上传响应虽然固定返回 `expires_at=null`、`temporary=false`，原分享元数据仍可能带到期时间；后续清理会按文件名删除当前文件，可能删除刚覆盖的新内容。不能无条件宣称“登录上传永不过期”，也不能只看这次整文件响应判断保留期限；应核对实际分享元数据。本主题记录的是源码边界，不表示已在生产上复现或授权修产品。

`transfer_file_cleanup` 是小时级后台任务，是否运行仍取决于 Backend 调度器和 job 状态。列表、创建会话及过期分享/会话访问也可能触发文件清理；调度器关闭不等于临时文件永不清理。删除失败或服务没有运行时，不保证到达 24 小时就已从磁盘移除。

## 实现与验证范围

`TransferPage.tsx` / `transferAccess.ts` 决定网页访问，`TransferPanel.tsx` 管理内存队列、覆盖确认和操作反馈，`transferUpload.ts` 负责指纹、客户端标识、分块和重试；`TransferSharePage.tsx` / `transferShares.ts` 读取公开分享。`transferAccess.test.ts` 覆盖登录跳转、访客开关、权限移除和强制改密分流，不覆盖完整上传队列。

`test_transfer_files.py` 有直属路径限制、所属会话、错误 offset、断线重试、匿名覆盖冲突、大小限制、过期删除、常规 Basic/Bearer/Session 与浏览器 CSRF、整文件流式上传和 HEAD 不走普通删除分支等用例。`test_auth_policy.py` 验证普通角色参与授权、scope、配置 Basic 及改密限制。

这些测试不构成真实反向代理、大文件容量、浏览器刷新/服务重启端到端或多进程并发验收；本页未声称已运行测试。尤其“整文件覆盖临时文件后保留旧 expiry”与“同名覆盖复用分享 token”的上述结论来自调用链，不假冒专门回归通过。真实上传、公开分享与删除涉及文件和访问权限副作用，应使用获授权的隔离目录与合成数据分别验证。
