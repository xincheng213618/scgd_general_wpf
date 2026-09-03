---
knowledge_id: "delivery.backend-copilot-sync"
knowledge_type: "topic"
status: "current"
summary: "Backend Copilot配置管理、AES-GCM密钥存储与全量同步；版本HMAC不是独立设备身份，nonce不去重，成功读取会交付provider秘密。"
aliases: ["Copilot Desktop Sync", "Copilot后台同步", "CopilotConfigService", "CopilotProfileInput", "CopilotConfigPage", "verify_copilot_device", "CopilotDeviceIdentity", "copilot_sync", "version_keys", "copilot:config:read", "copilot:manage", "X-ColorVision-Signature", "X-ColorVision-Nonce", "defaultProfileId", "hasApiKey", "api_key_encrypted"]
code_paths: ["Web/Backend/routes/copilot_config_api.py", "Web/Backend/services/copilot_config_service.py", "Web/Backend/services/copilot_device_auth.py", "Web/Backend/routes/admin_api.py", "Web/Backend/services/api_key_service.py", "Web/Backend/services/permission_service.py", "Web/Backend/app_setup.py", "Web/Backend/db/schema_version.py", "Web/Backend/db_cache.py", "Web/Frontend/src/pages/CopilotConfigPage.tsx", "Web/Frontend/src/services/admin.ts", "Web/Frontend/src/types/admin.ts"]
test_paths: ["Web/Backend/test_copilot_config_api.py"]
related: ["delivery.backend", "delivery.backend-auth", "copilot.configuration"]
---

# Backend Copilot配置管理与敏感配置交付

本页负责 Web Backend 的模型Profile持久化、管理API、同步响应和设备proof校验。它不负责桌面设置草稿、保存或聊天运行态发布；成功下载不证明桌面已保存或生效，后续边界见[Copilot配置与持久化](../core-concepts/copilot-configuration.md)。数据库与启动前提见[Backend组成](./README.md)。

同步是敏感信息交付：合法请求得到全部启用Profile及其解密后的provider API key，不是只取公开模型列表。管理写入、删除、GET同步审计及凭据使用记录都可能改变本地状态；本页的协议说明不授予执行、读取实际秘密或分发凭据的权限。

## 四种密钥与权限不能混用

| 对象 | 真实用途 |
| --- | --- |
| `copilot_sync.version_keys` | 后端接受的共享发布版本key，用于验证桌面metadata的HMAC；不是逐设备私钥 |
| Backend `secret_key` | 派生Profile中provider API key的AES-GCM加密密钥；不是设备proof的版本key |
| Bearer API key | 兼容同步认证，要求 `copilot:config:read` 或 `admin:*`；不是返回给模型provider的秘密 |
| Profile `apiKey` | provider调用凭据：管理时提交并加密入库，同步时解密放入响应 |

`routes/admin_api.py::ENDPOINT_SCOPES` 将四个Profile管理handler映射到 `copilot:manage`。普通Session持有该permission也可管理；配置Basic、管理员Session和Bearer的共用顺序见[HTTP认证](./authentication.md)。当前API key可申请scope目录没有 `copilot:manage`，因此不能把角色permission直接当成可创建的同名key scope；现有Bearer管理通常使用 `admin:*`，仅 `copilot:config:read` 不授予管理权。

## 管理接口与保存契约

| 方法与路径 | 行为 |
| --- | --- |
| `GET /api/admin/copilot/profiles` | 返回全部Profile，含停用项，按sortOrder、名称不区分大小写、ID排序；不解密或返回provider key |
| `POST /api/admin/copilot/profiles` | 创建，成功201；即使enabled=false也要求非空apiKey |
| `PUT /api/admin/copilot/profiles/<id>` | 更新整组业务字段，成功200；空/省略/null apiKey保留原密文，不是清空密钥 |
| `DELETE /api/admin/copilot/profiles/<id>` | 删除记录，成功200；不撤销provider端key，也不回收已经交付的副本 |

ID是32位十六进制，服务会trim并转小写；非法ID返回400，合法但不存在返回404。写接口捕获输入 `ValueError` 为400，数据库或密钥处理失败不能假定都是参数错误。管理列表、创建和更新正常响应显式设置 `Cache-Control: no-store`。

`CopilotProfileInput.from_payload` 的必要约束：name非空且最多200字符，model非空且最多300，baseUrl最多2048，非空apiKey最多8192；文本会trim。vendorType取 `VENDOR_TYPES`，providerType仅OpenAICompatible/AnthropicCompatible，reasoningMode仅Default/Disabled/Enabled/High/Max，枚举忽略大小写归一化。sortOrder经 `int()` 转换后须在-100000至100000。

PUT不是部分PATCH：name/vendorType/providerType/baseUrl/model仍必需；省略reasoningMode、enabled、isDefault、allowInsecureHttp、sortOrder分别使用Default、true、false、false、0，而非保留旧值。布尔字段当前使用Python `bool(...)`，不是严格JSON布尔验证，字符串 `"false"` 会被当成true；调用方应使用真实布尔值。

baseUrl必须是绝对HTTP/HTTPS地址，有hostname，不含userinfo、query或fragment；结尾斜杠被去掉。HTTP仅对localhost或字面loopback IP默认允许，其它HTTP地址须 `allowInsecureHttp=true`。这里只检查URL形式与该开关，不探测DNS、连接、模型是否存在或provider key是否有效；该开关控制交付给桌面的模型地址策略，不是同步端点自身的TLS配置。

创建/更新设isDefault时，在同一写事务中清除其它记录的默认标记。允许默认项同时停用，也允许没有默认项；删除或取消默认不自动选另一个。其它被清默认的记录不会顺带更新updated_at。没有revision/If-Match并发前置条件，不提供跨客户端编辑冲突保护。

`CopilotConfigPage.tsx` 编辑时把apiKey初始化为空，符合“留空保留”；“已保存”仅来自hasApiKey。页面的旧Bearer填写指引和无条件展示的“当前站点使用HTTP”提示，不证明当前桌面采用Bearer或当前请求实际使用HTTP；应分别核对桌面权威主题与实际部署。删除提示也不能替代桌面下一次同步、确认及保存的真实完成边界。

## 密文存在不等于可解密或provider可用

schema v5的 `copilot_profiles.api_key_encrypted` 保存 `aesgcm:v1:` 格式。`CopilotConfigService` 用固定context和trim后的Backend secret_key经SHA-256派生密钥，使用随机12字节nonce及AES-GCM保存provider key；缺secret_key不能执行加解密。这里的加密nonce与HTTP proof nonce不是同一个字段。

管理序列化只返回 `hasApiKey=密文字段非空`，不返回明文或密文，也不验证解密；修改secret_key、损坏密文或不支持的格式都可能让管理页仍显示“已保存”，但同步失败。当前没有自动重加密/旧secret回退流程，不能把直接更换secret_key当作透明的provider密钥轮换。

同步对全部启用行逐个解密，任一行失败会中止整次响应，而非跳过坏Profile后返回其余成功项。管理提交、后续同步、实际provider连接是不同证据。停用/删除只影响以后后端读取到的列表，不等于已经返回的provider key被撤销。

## 同步凭据分支独立于管理Session

`GET /api/copilot/config` 使用 `_authorize_sync_request`，不是共用AuthPolicy的Session→Basic→Bearer顺序：

1. Authorization精确以 `Bearer ` 开头时，只验该key。有效且满足scope返回配置；有效但缺scope为403，非法/过期为401。即便同时有有效device proof，Bearer失败也不回退。
2. 其它情况走device proof；管理员cookie或Basic不能独立授权此接口。`copilot_sync` 缺失不妨碍合法Bearer兼容分支，但设备分支没有可用版本key时返回503。

Bearer验证可能更新last_used_at，缺scope时为区分401/403还会再作一次无scope验证。成功同步尝试写 `copilot_config_sync` 审计：记录key prefix或设备ID前16位、Profile数量、revision及设备版本/架构，并带IP/User-Agent；不把provider明文写入该审计detail。审计写失败只打印，不与响应交付构成事务。

## 设备proof校验：共享版本HMAC与时间窗口

后端从配置 `copilot_sync.version_keys` 取接受的发布版本key，应与获准桌面版本所用key匹配。可配置字符串或list/tuple；数组仅取前16项，再转字符串、trim并去空项，后面的key不参与验证。这不是按app_version建立的key映射，也没有查询已注册设备、设备吊销表或用户批准记录。

`verify_copilot_device` 读取下列header，先trim；任一值超过256字符或含CR/LF按空值处理：

| Header后缀（均以 `X-ColorVision-` 开头） | 当前校验 |
| --- | --- |
| Product | 必须为ColorVision |
| Version | 2–4段点分数字，仅格式检查，不核发行版本清单 |
| Device-Id | 64位十六进制；通过后转大写作为身份metadata，不验证硬件来源 |
| OS-Version | 非空、最多64字符，不核操作系统真实性 |
| Architecture | 精确为X64或Arm64 |
| Timestamp | 整数Unix秒，与服务时间绝对差<=300秒；过去与未来都受限制 |
| Nonce | 32位小写十六进制，仅格式与签名覆盖 |
| Signature | 转小写后要求64位十六进制，并比较HMAC-SHA256 |

canonical按Product、Version、原Device-Id、OS-Version、Architecture、原Timestamp文本、Nonce顺序以换行连接、UTF-8编码；任一已配置版本key的HMAC匹配即可。版本key本身不发送，但method、URL path与body不在该canonical内，不能把这套校验等同于其它设备协议的请求签名。

当前nonce不存储、不查重，也不记录已用proof；所以时间窗口内重复同一proof不会仅因重复而被拒绝。共享key签名不证明独立硬件私钥、设备注册或硬件真实性，也不能因字段叫hardware fingerprint就推断不可伪造。配置缺失返回503；metadata、签名或时间检查失败通常401，均发生在读取解密配置之前。

## 返回集合、revision与敏感副作用

`list_client_profiles` 只筛 `is_enabled=1`，按默认项优先、sortOrder、名称、ID排序；不按device ID、用户或Bearer key划分不同可见Profile。合法请求返回 `schemaVersion=1`、revision、generatedAt、defaultProfileId和profiles；每个Profile都含明文apiKey，defaultProfileId可为null。

revision是有序 `(id, updatedAt)` 列表的SHA-256前24位，不是全字段内容hash或乐观并发令牌。一个源码可见例外是：新增停用但设默认的Profile，会清掉原启用项的默认标记，却不更新其updated_at；如果启用列表顺序未变，响应的默认信息可以改变而revision不变。不能只凭revision相同证明所有返回字段相同。

正常同步响应设置 `Cache-Control: no-store` 与 `Pragma: no-cache`，但仍在响应体交付秘密；HMAC不加密响应，也不强制后端走HTTPS。这些header不是客户端不保存、代理不记录或秘密未暴露的证明。返回成功亦没有桌面收齐、确认、持久化或运行态生效回执，详见[桌面配置同步与保存](../core-concepts/copilot-configuration.md)。

## 失败定位与现有验证

- 管理成功、同步失败：检查启用列表中的每份密文、当前secret_key和格式；hasApiKey不证明可解密，不应为诊断打印明文provider key。
- 带device proof仍401/403：先看是否精确Bearer前缀抢先进入兼容分支；再区分版本key配置503、metadata/签名/时钟401和scope403。
- 停用、删除或revision未变：确认后端筛选和默认项变化，再到桌面主题核对草稿/保存；不能假设已交付的key或运行中会话自动撤销。

`test_copilot_config_api.py` 覆盖管理未认证、CRUD不回显provider key、空key更新保留、AES密文与同步解密、合法设备proof、缺失/坏签名/过期proof、缺版本key503、Bearer缺scope403、停用项不下发及远程HTTP显式允许。测试使用合成凭据。

现有这些用例不证明nonce防重放、逐设备身份/吊销、Bearer与proof混合分支、future时钟边界、共享key轮换部署、secret_key变更后的恢复、坏行导致整批失败、revision例外或客户端落盘。本文按当前分支记录这些限制，不将普通成功用例当作安全认证或多端交付验收。
