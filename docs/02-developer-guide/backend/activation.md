---
knowledge_id: "delivery.backend-activation"
knowledge_type: "topic"
status: "current"
summary: "用户中心的可选许可证激活、TPM 离线凭据、在线租约和后台设备名额。"
aliases: ["许可证激活", "联网申请并激活", "使用中的设备", "释放本机", "TPM 离线", "在线租约", "ActivationService", "COLORVISION_ACTIVATION_URL"]
code_paths: ["UI/ColorVision.Rbac/Activation", "UI/ColorVision.Rbac/ColorVision.Rbac.csproj", "UI/ColorVision.Rbac/RbacManagerWindow.xaml", "Web/Backend/routes/activation_api.py", "Web/Backend/services/activation.py", "Web/Backend/services/activation_http.py", "Web/Backend/db/repositories/activation_admin.py", "Web/Backend/routes/admin_api.py", "Web/Frontend/src/pages/ActivationPage.tsx"]
test_paths: ["Web/Backend/test_activation.py", "Web/Backend/test_contracts.py"]
related: ["delivery.backend", "platform.rbac"]
---

# 许可证激活与设备名额

在桌面端打开**用户中心 → 激活**，选择设备类型后点**联网申请并激活**。服务端自动签发许可证。申请设备可以复制许可证码，供其他设备使用**激活本机**加入。持码者可占用名额，因此仅向需要加入的设备分享。激活状态和操作都在该窗口内，独立于本地 RBAC 和 Web 账号，软件功能始终可用。

每张许可证默认有效 365 天，可同时占用 3 个设备名额。许可证到期后，签发设备可再次显式申请，延长同一许可证的有效期。设备列表显示在线查询时的占用情况、设备名、本机标记和占用到期时间。**释放本机**需要联网，成功后回收服务端名额并清除本机的有效凭据。设备无法联网释放时，名额随凭据或租约到期回收。

| 模式 | 设备身份和名额 |
| --- | --- |
| 我的电脑（TPM 离线） | Windows Platform Crypto Provider 生成不可导出的 P-256 密钥。服务端签名的离线凭据和名额有效期为 24 小时，最多至许可证到期。窗口联网时可续期。断网时只显示本机通过验签及密钥检查的凭据。 |
| 共享电脑（在线） | 支持使用 Windows 软件密钥。激活窗口打开时续租，关闭后服务器在 5 分钟租约到期时回收名额。重新打开时，如果仍有空位则恢复占用。 |

服务端使用单次签名挑战确认设备持有对应私钥，按公钥指纹计数。在桌面端，离线凭据与当前 Windows 用户、服务端地址和 TPM 密钥绑定。服务端验证的是密钥持有，离线模式是客户端声明，当前没有远程 TPM 硬件证明。已签发的离线凭据在有效期内可自行验证，服务器无法向断网设备即时撤销。

后台的**许可证激活**仅供配置管理员查看。可按许可证 ID、设备名称和有效状态筛选，并检查设备占用、续租、到期及释放时间。管理页面只读，不展示许可证码或签名私钥。SQLite 备份应一并保留许可证、设备与签名材料。签发和挑战按后端所见的连接来源限流，反向代理共用来源地址时共用限额。

## 使用现有 HTTP 入口

许可证码是可分享的凭据。HTTP 部署时，桌面端用预置服务端公钥保护每次请求的临时密钥，申请、加入、查询、续期和释放均经 `/api/activation/secure` 加密传输。服务器配置密钥文件后，只接受加密的桌面激活请求。传输密钥与 TPM 设备密钥、服务端离线凭据签名密钥分别管理。

1. 生成专用 RSA 3072 位密钥对。私钥保存为 PKCS#8 PEM，放在服务主机上由后台运行账户可读的受限目录，目录应在仓库、Web 静态目录和公开制品存储之外。
2. 在服务端本地 `config.json` 的 `activation_http_private_key_file` 设置私钥绝对路径，或为后台进程设置 `COLORVISION_ACTIVATION_HTTP_PRIVATE_KEY_FILE`。NAS 计划任务部署可使用会保留的本地配置。部署后重启服务，确认日志没有密钥加载错误。
3. 将同一密钥的 DER SPKI **公钥**通过可信渠道导出到 `UI/ColorVision.Rbac/Activation/HttpPublicKey.der`。常规构建会嵌入该公钥，客户端默认连接现有 HTTP 入口。专用构建可通过环境变量 `ColorVisionActivationHttpPublicKeyFile` 指定另一份 DER SPKI 公钥。使用新版客户端验收申请、设备列表和释放。重新生成密钥时需同步更新客户端公钥。

无预置公钥的构建使用可信 HTTPS。隔离验证可将 `COLORVISION_ACTIVATION_URL` 指向 loopback HTTP，配合 `COLORVISION_ACTIVATION_HTTP_PUBLIC_KEY` 指定 Base64 编码的 DER SPKI 公钥，并使用 `RuntimeOverrides` 隔离后台配置、存储和 SQLite。公钥通过构建或显式本地配置提供，首次请求不会从网络获取信任材料。HTTP 加密只覆盖桌面激活接口，Web 管理登录仍走站点自己的连接。
