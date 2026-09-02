---
knowledge_id: "delivery.backend-accounts"
knowledge_type: "topic"
status: "current"
summary: "Backend注册、角色权限、改密与找回、数据库Session撤销；配置管理员不走auth_version，跨服务安全操作可能部分成功。"
aliases: ["Web账号", "公开注册", "public_registration_enabled", "auth_version", "must_change_password", "配置管理员", "强制改密", "密码找回", "user_sessions", "角色权限", "replace_role_permissions", "force_logout", "password_changed_at"]
code_paths: ["Web/Backend/routes/public_pages.py", "Web/Backend/routes/admin_api.py", "Web/Backend/services/auth_service.py", "Web/Backend/services/session_service.py", "Web/Backend/services/permission_service.py", "Web/Backend/services/account_settings.py", "Web/Backend/services/config_persistence.py", "Web/Backend/services/login_throttle_service.py", "Web/Backend/services/registration_rate_limit_service.py", "Web/Backend/services/password_recovery_service.py", "Web/Backend/services/account_activity_service.py", "Web/Backend/services/account_security_cleanup.py", "Web/Backend/config_loader.py", "Web/Backend/db/schema_version.py", "Web/Backend/db_cache.py"]
test_paths: ["Web/Backend/test_contracts.py", "Web/Backend/test_auth_service.py", "Web/Backend/test_auth_policy.py", "Web/Backend/test_account_settings.py", "Web/Backend/test_login_throttle_service.py", "Web/Backend/test_registration_rate_limit_service.py", "Web/Backend/test_password_recovery_service.py", "Web/Backend/test_account_security_cleanup.py", "Web/Backend/test_schema_version.py"]
related: ["delivery.backend", "delivery.backend-auth"]
---

# Web账号、角色与会话生命周期

`routes/public_pages.py` 负责浏览器登录、注册和个人账号，`routes/admin_api.py` 负责账号管理；`auth_service.py`、`session_service.py` 和 `permission_service.py` 分别持有账号、登录会话与角色权限。这里的 Web 账号不是 WPF 本地 RBAC，也不代表外部 Codex 账号。HTTP凭据选择、API key和CSRF见[认证边界](./authentication.md)。

本页的启停、重置、撤销、删除和策略修改会改变数据库或配置，不是只读诊断，也不构成执行授权。数据库/配置路径及启动前提见[Backend组成](./README.md)。

## 两种管理员与真实默认值

| 身份或配置 | 当前契约 |
| --- | --- |
| 配置管理员 | 来自 `upload_auth`；登录时不创建数据库用户，也不给Session设置 `user_id` / `auth_version` / `login_session_id` |
| 数据库账号 | 只支持 `admin`、`user` 两种角色，来源为 `self_registered`、`administrator_created` 或迁移的 `legacy`；不是旧的admin/operator/viewer模型 |
| 默认注册策略 | `config_loader.DEFAULT_CONFIG` 与示例配置的 `public_registration_enabled` 均为 `true`；正常加载合并后的默认配置允许公开注册 |
| fail-closed检查 | `account_settings.is_public_registration_enabled` 收到缺失字段或非JSON布尔值时返回false；这不等于正常应用默认关闭注册 |
| 初始角色授权 | 首次建立权限目录时，`user` 和 `admin` 均获全部已定义功能权限；普通账号不是默认只读身份。后续可缩减 `user` 的权限 |

配置管理员用户名忽略大小写地保留，公开注册和后台创建不能占用它。旧数据库若有同名shadow记录，管理响应标记 `is_config_admin`，不能通过用户管理编辑、启停、改角色、重置密码、强制改密、强制下线或删除；新的浏览器登录以当前配置凭据为准，不退回shadow密码。

配置管理员没有数据库Session记录：修改 `upload_auth` 只保证之后的登录和Basic认证读取新凭据，不能推断已有配置管理员cookie会被 `auth_version` 校验撤销。`_synchronize_session_account` 对无 `user_id` 的Session直接返回；个人资料可以读取，但修改资料/密码、读取独立Session列表返回配置管理限制。数据库账号的撤销机制不可直接套用到这个身份。

## 注册与账号字段

| 接口 | 行为 |
| --- | --- |
| `GET /api/auth/session` | 返回登录状态、角色、有效权限、`can_access_admin`、强制改密标记、注册能力提示与CSRF token；提示不代替写接口重新检查策略 |
| `POST /api/auth/register` | 只创建 `user`，成功后建立当前登录Session，返回 `201`；策略关闭为 `403` |
| `GET /api/admin/settings/accounts` | 读取实际注册策略，要求 `settings:manage` |
| `PUT /api/admin/settings/accounts` | 请求体必须且只能含布尔 `public_registration_enabled`；持久化配置后更新进程内配置，不影响既有账号或后台创建账号 |
| `POST /api/admin/users` | 创建 `admin` 或 `user`，来源为 `administrator_created`，设置 `must_change_password=true`，要求 `users:manage` |

账号名为3–32个ASCII字母、数字、下划线、点或连字符，用户名重复忽略大小写。新数据库密码按Python字符串长度要求15–128个Unicode字符，允许空格和口令短语，不要求字符种类组合。该校验覆盖注册、后台创建/重置和自助改密；已有短密码hash和配置管理员凭据不会因此被迁移或自动拒绝。自助改密还检查当前密码，并拒绝新旧密码完全相同。

资料编辑只改 `display_name` 和 `email`：前者trim后最多64字符；后者trim并转小写，非空时检查格式，最多254字符。`password_changed_at` 表示秘密实际替换时间：新注册初始化，重置/自助改密更新，仅设置强制改密标记不更新。schema v26为旧记录从创建时间回填，不是证明当时发生过一次改密。

注册按 `request.remote_addr` 计数：每源10分钟最多20次尝试、每小时最多5次成功；进行中的预约也占成功窗口容量。计数和预约在SQLite中，重启不清空。超限为 `429` 并带 `Retry-After`，预约服务失败为 `503`；后台创建不占公开注册配额。来源地址是服务实际看到的远端地址，不能仅凭此保证代理后每个终端各有独立额度。

注册设置使用 `config_persistence.persist_config_values`：在进程内锁中合并已有JSON，以临时文件、flush/fsync及 `os.replace` 替换文件，再更新live mapping；不会允许该接口改 `upload_auth` 等其它字段。这是配置文件替换边界，不是配置、账号数据库和审计的统一事务。

## 角色授权与权限修改

`GET /api/admin/permissions` 和 `PUT /api/admin/roles/<role>/permissions` 要求 `permissions:manage`。矩阵含权限名称/分类、角色成员总数与活跃数据库账号数、权限列表和revision；配置管理员不是数据库成员计数的一部分。`admin` 权限固定不能编辑，只有 `user` 可整体替换权限集合，不支持任意新建角色。

PUT必须给字符串数组 `permissions`，可给由64个小写十六进制字符组成的SHA-256格式 `expected_revision`。传入旧revision会在事务内返回 `409 permission_revision_conflict`；不传则没有这一乐观并发前置条件。非法权限为 `400`，修改admin为 `409`，未知角色为 `404`。成功响应含增加/移除权限、受影响活跃账号数和新revision，并写审计。

普通Session每次授权读取live角色矩阵，因此移除权限后不必重新登录就会被对应接口拒绝；`can_access_admin` 只表示后台入口能力，不保证每个操作都获准。更改某人的角色与更改角色权限是不同操作：前者撤销该人的会话，后者影响该角色的所有账号而不要求全体下线。初始化不会恢复已删除的既有user授权，但新加入目录的权限会同时授予admin/user；不能把一次缩权当成未来新增权限永远默认拒绝。

角色permission和API key可申请scope不是同一目录，`admin:*` 也不是user角色中必须持有的普通权限。端点的真实要求及凭据差异见[认证与scope](./authentication.md#端点permission不等于api-key可申请scope)。

## 查询、个人资料和活动

| 接口 | 返回或限制 |
| --- | --- |
| `GET /api/admin/users` | 无查询参数时保留原数组；有查询参数时返回分页对象。支持 `q`、`role`、`origin`、`status`、`password_state`、`recovery_state`、`sort_by` / `sort_order`、`limit` / `offset` |
| `GET /api/admin/users/<id>/details` | 账号、待处理恢复申请、角色权限、有效Session和分页活动；`activity_limit` 默认8、范围1–50，offset非负 |
| `PUT /api/admin/users/<id>/profile` | 后台更新显示名/邮箱；不等于改用户名、角色或密码 |
| `GET/PUT /api/account` | 当前Session自己的资料及权限；数据库账号可编辑资料，配置管理员不可编辑 |
| `GET /api/account/sessions` | 当前数据库账号的未撤销Session、IP、User-Agent、时间与 `is_current`；不提供其它账号的列表 |
| `GET /api/account/activity` | 当前账号的隐私限定活动时间线；limit默认8、范围1–50，offset范围0–100000 |

用户分页查询的 `q` 最多100字符；role为admin/user，status为active/inactive，origin为三种账号来源，password_state为pending/ready，recovery_state为pending/none。排序只允许username、display_name、email、role、account_origin、is_active、active_session_count、created_at、last_login_at、password_recovery_requested_at；方向asc/desc，默认desc，limit默认20且1–100，offset非负。不是接受任意SQL排序表达式。

活动和Session读接口不等于零写入：Session列表会把旧auth_version记录标记撤销，正常Session校验也可能更新活动信息；待处理恢复查询会推进过期状态。此处的“活跃Session数”是数据库未撤销且版本匹配的记录，不证明浏览器仍在线。

## 密码与Session状态转换

数据库登录在cookie中保存 `user_id`、`auth_version` 和随机 `login_session_id`，同时在 `user_sessions` 建记录。前置校验只在 `_session_account_requires_validation` 列出的 `/admin`、`/account`、`/transfer`、`/browse`、`/upload` 页面及 `/api/` 等路径前缀上刷新，不是全站每个请求统一检查。它检查账号存在且启用、cookie与账号版本一致、对应Session记录未撤销且版本匹配；不满足时清cookie。检查的是后续请求，不主动中断已经执行中的请求。

兼容旧cookie时：缺 `auth_version` 只有账号版本仍为0才可升级；缺 `login_session_id` 的已签名且版本合法cookie会补建记录。普通强制下线操作撤销现存Session记录但不提升账号版本，不能宣称它等同于轮换账号全部认证材料，或撤销所有尚未升级的历史cookie。

| 操作 | 密码/标记 | Session结果 |
| --- | --- | --- |
| `PUT /api/admin/users/<id>/role` | 改角色时提升auth_version | 撤销目标现有Session；不能改当前Session账号角色，不能降级最后一个活跃数据库admin |
| `POST .../<id>/disable` / `enable` | 状态实际改变时提升auth_version，不换密码 | 路由撤销旧Session并清登录失败来源；禁用还结案恢复申请。启用不复活旧会话 |
| `POST .../<id>/password` | 重置hash、提升版本并更新password_changed_at；为别人重置设置强制改密 | 撤销旧会话；只有以目标本人的当前Session认证进行自重置时才恢复/补建当前会话，并不设置临时密码标记 |
| `POST .../<id>/password-change-required` | 不替换hash，只设标记并提升版本 | 撤销会话、清登录失败来源；用户再用原密码登录后仍须自行更换秘密 |
| `POST .../<id>/sessions/revoke` | 不改密码、角色或auth_version | 撤销现存Session记录；用户可用原密码重新登录 |
| `PUT /api/account/password` | 验旧密码，写新hash、提升版本、清强制改密标记 | 撤销旧会话，恢复/补建当前会话；其它浏览器后续请求失效 |
| `DELETE /api/account/sessions/<id>` | 不改账号版本 | 仅撤销属于自己的其它Session；当前会话返回409，不存在/不属于自己返回404 |
| `DELETE /api/account/sessions/others` | 不改账号版本 | 撤销除当前以外的记录，不停用账号 |

表中 `.../<id>` 均指 `/api/admin/users/<id>`。当前Session账号不能从后台被禁用、改角色、强制下线、强制改密或删除。最后一个活跃数据库admin的禁用/降级检查与账号更新在 `BEGIN IMMEDIATE` 内完成；配置管理员不计入这条数据库人数检查。

强制改密登录仍返回 `authenticated=true`，但权限列表为空、`can_access_admin=false`。前置门禁允许个人页面、Session状态、个人资料GET、Session/活动GET、退出及密码PUT；资料PUT和撤销其它Session不在这份放行列表。它约束已列出的管理/账号/Transfer路径及共用认证，不是封锁整个公开站点；其它协议是否接受独立Basic/Bearer仍看自己的前置门禁，不能只凭AuthPolicy单元测试推断所有HTTP入口会绕过标记。

Session活动最多按5分钟间隔更新，IP或User-Agent改变时也会更新；这些字段是记录信息，不用于把会话锁死在某个IP/浏览器。30天未活动记录由 `cleanup_account_security_data` 清理时撤销，旧撤销记录也有30天历史保留；不是每次 `validate_user_session` 都按闲置时间独立判过期，也不保证到点立即执行清理。

## 管理员辅助找回与限流

`POST /api/auth/password-recovery` 接受用户名或邮箱 `identifier`，trim后非空且最多254字符。它不发邮件、不直接重置密码；已接受的有效请求对匹配、不存在、停用和配置账号给相同 `202` 文案，不能据响应判断账号是否存在。缺失/过长输入为400，来源限流为429并带 `Retry-After`，服务失败可为503，浏览器请求还可能先被CSRF拒绝；不是所有请求无条件202。

每源15分钟最多10次找回尝试，SQLite计数跨重启保留。匹配启用的数据库账号才合并为一条pending请求；同账号1分钟内重复提交不重复计数，之后更新last_requested_at和request_count（最高999）。距最近一次实际记录7天的请求转为系统结案 `expired`，退出pending筛选/总数，可再提新申请；过期记录是历史，不立即全部删除。

后台重置密码后尝试以 `administrator_password_reset` 结案，自助改密尝试以 `self_password_change` 结案；禁用以 `account_disabled` 结案。仅要求下次改密不会假装已经替换密码。后台可读登录/注册安全状态并清理：

| 接口 | 含义 |
| --- | --- |
| `GET /api/admin/login-security` / `POST .../unlock` | 查看登录失败窗口，按username清来源计数；登录失败按规范化用户名跨来源聚合，15分钟5次失败触发15分钟锁定 |
| `GET /api/admin/registration-security` | 显示blocked/tracking、尝试/成功窗口、在途预约及到期信息；q最多64字符，limit默认20且1–100，offset非负 |
| `POST /api/admin/registration-security/clear` | 按非空且最多64字符的ip_address清已完成计数；保留在途预约，完成后仍记入额度，不是把并发请求抹掉 |

这些入口要求 `users:manage`。手工清理写审计。禁用/启用、后台密码重置和单个/批量强制改密也清登录失败来源；这与修改角色权限、撤销会话本身不同。

## 删除、批量与部分成功

`DELETE /api/admin/users/<id>` 要求先禁用数据库账号，再在JSON中确认当前 `username`（忽略大小写比较）；未禁用为409，缺少/不匹配确认值为400，不存在为404。删除包含账号/hash、Session历史、密码恢复记录及匹配登录失败来源，不进回收站；既有管理审计不是随账号级联删除，但仍受独立审计保留策略约束。

`POST /api/admin/users/bulk-security` 只接受 `action=force_logout` 或 `require_password_change`，`user_ids` 必须是1–100个正整数（bool不算整数），校验长度后去重。不存在、配置管理员、当前Session账号分别返回逐项失败；正常返回的HTTP200仍需检查 `succeeded`、`failed` 和 `results`，不是全体成功。每项独立推进，已成功项不会因后续项失败回滚。

单项安全操作也没有跨服务整体事务：密码/hash/version先由auth_service提交，再由session_service撤销/恢复记录、处理限流/恢复申请并写审计。后台重置和自助改密的恢复申请结案异常会被捕获并记录，不撤销已成功改密；其它后续异常也可能在部分提交后返回错误。批量某项标记 `operation_failed` 不证明该项未改变密码标记或Session。应重新读账号/会话状态核对，不能把“响应失败”当作可以无条件重试的未执行证明。

## 验证范围与缺口

`test_contracts.py` 有默认注册、强制改密、角色缩权即时生效、revision冲突、个人Session撤销、禁用/启用、配置shadow保护、当前管理员自重置保留当前会话、批量逐项处理及删除确认等HTTP用例。`test_auth_service.py` 覆盖密码长度、临时密码状态、秘密未改变时不能清门禁及删除；注册/登录限流、找回、账号清理和schema迁移各有元数据所列专项测试。

配置凭据轮换后旧管理员cookie仍不走数据库撤销、旧cookie自动补建Session、跨服务提交后失败等边界需要专门的故障注入、浏览器和多进程部署验证。账号行为变更需分别验证数据库账号、配置管理员、已有cookie和全新登录，不能用单一身份的成功替代全部身份。
