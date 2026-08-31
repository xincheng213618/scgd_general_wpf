---
knowledge_id: "delivery.backend-auth"
knowledge_type: "topic"
status: "current"
summary: "Backend HTTP凭据优先级、Session权限与API key scopes、key轮换失败副作用和浏览器CSRF；认证成功不等于端点授权或全流程完成。"
aliases: ["AuthPolicy", "Session权限", "Bearer", "Basic Auth", "API key", "api_keys:manage", "ENDPOINT_SCOPES", "X-CSRF-Token", "CSRF", "Origin", "Sec-Fetch-Site", "WWW-Authenticate", "rotate_api_key", "last_used_at", "POST logout"]
code_paths: ["Web/Backend/services/auth_policy.py", "Web/Backend/routes/auth_adapters.py", "Web/Backend/routes/request_context.py", "Web/Backend/services/request_context.py", "Web/Backend/routes/browser_auth.py", "Web/Backend/routes/admin_api.py", "Web/Backend/routes/public_pages.py", "Web/Backend/services/api_key_service.py", "Web/Backend/services/permission_service.py", "Web/Backend/services/csrf_protection.py", "Web/Backend/app.py", "Web/Backend/app_setup.py"]
test_paths: ["Web/Backend/test_auth_policy.py", "Web/Backend/test_browser_auth.py", "Web/Backend/test_csrf_protection.py", "Web/Backend/test_contracts.py", "Web/Backend/test_plugin_index.py", "Web/Backend/test_access_analytics.py"]
related: ["delivery.backend", "delivery.backend-accounts", "delivery.file-transfer"]
---

# HTTP认证、API key与浏览器CSRF

`services/auth_policy.py:AuthPolicy` 判定凭据和scope；`routes/auth_adapters.py` / `routes/admin_api.py` 将判定变成Flask响应，`routes/browser_auth.py` 决定是否发Basic challenge。API key由 `api_key_service.py` 持久化，浏览器写请求由 `csrf_protection.py` 另设前置门禁。共用策略存在不表示所有端点都接受同一认证方式。

账号注册、角色修改、数据库Session版本/撤销及配置管理员例外统一见[账号生命周期](./accounts.md)。本页不复制响应缓存、压缩、CSP或制品传输规则；文件中转另见[Transfer](./file-transfer.md)。创建/轮换/撤销key、登录/退出和写接口会改变状态，以下契约不是执行授权，故不提供可直接误跑的管理或发布命令。

## 共用凭据判定顺序

`authorize` 的顺序是：允许的管理员Session → 允许的普通用户Session → 配置Basic → Bearer → 强制改密/未认证错误。其默认参数为 `allow_admin_session=true`、`allow_user_session=false`、`allow_basic=true`、`allow_bearer=true`；端点可覆盖。

| 凭据 | 判定与不能推断的内容 |
| --- | --- |
| 管理员Session | 必须允许此方式，且没有强制改密标记；共用策略授予管理员身份，不逐个查user角色授权 |
| 普通用户Session | 端点必须显式允许；每次读取live角色permission，缺少要求项为 `insufficient_scope`，不是登录即全权 |
| 配置Basic | 对照当前 `upload_auth`，不是任意数据库账号名/密码；只有允许Basic的端点可用，成功取得管理员身份 |
| Bearer API key | 验格式、状态/到期、secret hash，再按端点要求判scope；`admin:*` 满足共用scope检查，但不能覆盖“不允许Bearer”这一认证方式限制 |
| 强制改密Session | 不由前两项授予权限；共用策略可继续尝试独立Basic/Bearer，但实际HTTP入口可能先被账号前置门禁拒绝 |

显式 `Authorization` 不保证替换cookie身份：已有获准管理员Session可先返回成功；普通Session若缺scope也直接返回403，不继续尝试后面的Basic/Bearer。若要判断一次请求以哪个身份执行，应核对最终Actor/路由策略，而不是只看请求带了哪个header。协议专用签名、配对设备或仅Bearer入口不能套这个默认顺序。

## 端点permission不等于API key可申请scope

`routes/admin_api.py:ENDPOINT_SCOPES` 是管理API实际要求，前置校验允许普通用户Session参与；未映射端点按 `admin:*` 处理。`services/permission_service.py` 的角色permission目录与 `api_key_service.API_KEY_SCOPE_DEFINITIONS` 是两个集合，不能复制一份表同时充当它们的事实源。

| 管理职责 | 端点要求的permission/scope字符串 |
| --- | --- |
| 账号、登录/注册安全状态 | `users:manage` |
| 角色权限矩阵/修改 | `permissions:manage` |
| API key列表、目录、创建、撤销、轮换、usage | `api_keys:manage` |
| 注册和安全保留设置 | `settings:manage` |
| 数据库备份 | `backups:manage` |
| 审计、部署历史、运维概览 | 分别为 `audit:read`、`deployments:read`、`operations:manage` |
| 缓存读取/刷新、任务读取/写入、统计 | `cache:read` / `cache:refresh`、`jobs:read` / `jobs:write`、`stats:read` |

角色中具备相应permission的普通Session可以访问，不要求把角色改成admin。当前可申请的API key scope目录并不包含上述 `users:manage`、`permissions:manage`、`api_keys:manage`、`settings:manage` 等细粒度管理permission，所以不能据Session目录造出可创建的key scope；这些管理入口对现有Bearer key通常需要 `admin:*`。

API key权威目录由 `GET /api/admin/api-keys/scopes` 返回：包括 `admin:*`、cache/jobs/stats、plugin/release发布、`file:transfer`、`ops:relay`、`ops:operator` 和 `copilot:config:read` 等，附名称、分类、用途和 `default_scopes`。该接口自身也要求 `api_keys:manage`。这不是公开访客的授权目录，更不是任一key能使用所有列出的能力。

共用上传装饰器要求 `plugin:publish` 并启用user Session；其它上传/发布路径还应核对各自要求。Transfer、Operations Relay与Copilot设备协议的独立凭据边界各由对应主题负责，不从一个装饰器推导全站权限。

## API key存储、有效期与使用记录

| 方法与路径 | 行为 |
| --- | --- |
| `GET /api/admin/api-keys` | 列出公开元数据和有效状态，不返回secret或hash |
| `POST /api/admin/api-keys` | 创建key，成功201；完整明文只在创建响应给出 |
| `POST /api/admin/api-keys/<id>/revoke` | 将active标记关闭并写revoked_at；不存在或已撤销为404 |
| `POST /api/admin/api-keys/<id>/rotate` | 撤销旧key再创建新key，成功201；不是原ID原secret的就地更新 |
| `GET /api/admin/api-keys/<id>/usage` | 单个key公开元数据及最近审计写活动，不是所有HTTP请求计数 |

新生成key格式为 `cvmp_<8个十六进制字符的prefix>_<32个十六进制字符的secret>`；数据库保留prefix与secret hash，不保存可再次取回的完整明文。`created_by` 是元数据，不是随数据库用户Session/角色变更自动失效的账号绑定；key状态、scope与账号Session是独立控制面。

HTTP创建要求非空name，scopes可用逗号字符串或字符串数组，拒绝不在key目录中的scope。目录给出的 `default_scopes=["stats:read"]` 是默认提示，HTTP创建实现并不把省略的scopes自动补成它，省略时传空scope集合。不要把前端预选与服务端默认混为一谈。

`expires_at` 如给出有效时间须在未来，支持ISO8601，未附时区按UTC处理并归一化为UTC。HTTP创建时省略/null/空字符串等false值会使用90天默认；仅空白字符串经service归一化可变为无到期值，service本身也允许None，因此不能宣称所有key强制90天到期。已有非法到期记录显示 `invalid_expiry` 并拒绝认证；正常状态为active、expired或revoked，撤销优先于到期显示。

`verify_api_key` 先做便宜的状态/到期检查，再验hash。`last_used_at` 尝试按每key每分钟合并更新，并用旧值比较更新避免重复写；写记录失败不否定已验证凭据。AuthPolicy验key时尚未执行端点scope判定，因此后续返回403的请求也可能刷新last_used_at。它是建议性的使用时间，不是请求成功数。

usage以key prefix身份（兼容旧数字ID身份）筛审计，返回action、target、detail和时间，不返回该接口活动行的IP/User-Agent；默认最近20项。list/usage不暴露key_hash，但也不能假设最近审计写活动就是完整读取流水。

### rotate不是续期，也不是原子替换

`rotate_api_key` 先读取旧记录，调用独立提交的 `revoke_api_key`，再用原name、description、scopes和 **原expires_at** 创建新记录。description被保留，但不会从轮换时重新获得90天；轮换一个即将到期的key，新key也很快到期。

到期记录可以被找到，但新建校验会拒绝已过去的expires_at：此时旧key可能已提交撤销，再返回400。无此key为404，其它创建/数据库失败也没有恢复旧key的补偿步骤；重复轮换也不是幂等地重取同一个新secret。HTTP错误不能证明旧key仍有效。应分别核对旧key状态与是否取得新key，不在结果不明时盲目轮换。

创建/轮换成功持久化后还要写审计并发送只出现一次的明文响应；后续失败或响应丢失不能用列表恢复secret，也不代表数据库未产生新记录。本页记录代码顺序，不声称已经做过生产故障注入。

## 浏览器CSRF的准确条件

`app.py` 在注册业务蓝图前安装CSRF前置钩子。其识别及放行顺序为：

1. GET、HEAD、OPTIONS直接跳过该CSRF检查；这只是不做token校验，不保证所有读接口零副作用。
2. 对其它方法，只有非空 `Origin` 或 `Sec-Fetch-Site` 将请求归为这里的浏览器请求。若有Origin，比较scheme、hostname、port；无效或不同源为403。只有没有Origin时才检查Fetch Metadata，并仅放行same-origin/none；same-site也不等于same-origin。
3. `/api/v1/analytics/events` 在通过上述来源检查后豁免token，兼容pagehide的Beacon；不豁免跨源Origin。
4. 没有这两个来源header，或没有已登录Session标记，不检查Session token。
5. 有任意非空 `Authorization` 时也免token；这里仅检查header存在，不先验证Basic/Bearer是否有效。
6. 其余Session浏览器写请求必须提供与cookie中token匹配的 `X-CSRF-Token`，缺失或错误为403。

因此不能写成“所有Session认证的写请求必验token”，也不能写成“有cross-site Fetch标记无条件拒绝”：当Origin存在且匹配时，代码不再进入Fetch分支。尤其已有Session与非空Authorization同时出现时，CSRF可能免token而AuthPolicy仍先采用Session；这不是证明实际用该header凭据完成认证。以上是当前分支事实，不是安全评审通过或修改产品的授权。

Origin比较来自 `request.host_url`，按解析出的scheme/hostname/port元组而非宽泛域名或子域关系；部署代理时应核对服务实际看到的来源。普通User-Agent、Accept、`X-ColorVision-Web`、仅 `Sec-Fetch-Mode` 都不参与这个CSRF浏览器识别，不要与下面的Basic对话框识别混为一谈。

## 401、登录边界与POST退出

`app_setup.py` 显式设Session cookie为 `HttpOnly`、`SameSite=Lax`。这不等于启用Secure-only cookie，也不取代CSRF门禁；部署层的HTTPS和代理策略需分别确认。

`GET /api/auth/session` 会建立/返回本Session的随机CSRF token；成功登录先clear旧Session再发新token，`POST /api/auth/logout` clear后也返回新的匿名Session token。不是每次GET都轮换，且GET状态查询本身可能产生cookie或数据库活动写入。

管理API未认证返回JSON401，缺permission返回403 `insufficient_scope`，强制改密为403 `password_change_required`；来源/token错误可能在授权前先返回CSRF403。不能单看状态码就把所有403归为角色权限问题。

对使用 `apply_basic_auth_challenge` 的上传等适配器，含 `X-ColorVision-Web`、Origin、Sec-Fetch-Site或Sec-Fetch-Mode任一非空header视为浏览器，不加 `WWW-Authenticate`，避免浏览器弹Basic窗口；没有这些元数据的原生客户端仍可收到Basic challenge。管理REST入口本身统一JSON401，不能承诺全站401都带challenge。HTML页面的重定向又是独立路由契约。

`POST /api/auth/logout` 和 `POST /logout` 才调用 `_clear_login_session`；兼容 `GET /logout` 只重定向 `/`。数据库账号退出会尝试撤销当前 `login_session_id` 并清cookie；撤销数据库异常被捕获，不能把“清掉当前浏览器cookie”等同于成功撤销所有已复制cookie。配置管理员没有数据库会话记录，更不能套用数据库撤销保证。账号改密/强制下线和个人其它Session撤销见[账号主题](./accounts.md#密码与session状态转换)。

## 验证范围

`test_auth_policy.py` 覆盖凭据方式、普通Session opt-in、强制改密与独立凭据、key scope/wildcard；它不等于已通过所有HTTP前置门禁。`test_csrf_protection.py` 覆盖同源Session token、跨源Origin/Fetch、headerless和显式Authorization兼容；`test_browser_auth.py` 验证Basic challenge的元数据区别。`test_contracts.py` 有登录token轮换、同源管理写、401/403及key CRUD/有效期/目录用例，`test_plugin_index.py` 有明文/hash不泄漏、轮换、失效key、last_used合并和写失败容错；统计Beacon入口另有 `test_access_analytics.py` 用例。

这里只声明现存关联证据，本次未运行测试、浏览器、服务、数据库或联网动作。任意Authorization与Session优先级组合、来源header冲突、轮换已撤销后创建失败、响应丢失和旧cookie撤销失败等结论来自分支/提交顺序，未冒称已有专门回归或多进程/反向代理实测。修正这些契约时须补相应故障与身份组合测试，不能只复用正常登录成功作为证明。
