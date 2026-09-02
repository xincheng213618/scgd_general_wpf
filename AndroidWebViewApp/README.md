# ColorVision Android 运维伴侣

ColorVision Android 现场运维伴侣。已配对设备启动后直接进入现场运维，Material 3 底部导航提供“概览 / 问题 / 工具 / 设置”四个一级目的地。安全更新与远程中继使用应用固定、不可编辑的服务源。

当前源码入口、直连/中继职责和验证边界见[Android 运维伴侣](../docs/02-developer-guide/backend/android-operations.md)。本 README 保留本地构建前提和安全说明。

## 构建方式

使用 Android Studio 与 Java 17；当前 `app/build.gradle` 指定 compile/target SDK 36、Build Tools 36.0.0、最低 Android API 23。版本和签名配置以该文件为准。Gradle 同步可能下载依赖，构建写入本地产物，Run 会安装并运行手机应用；这些不是只读检查。

1. 用 Android Studio 打开 `AndroidWebViewApp` 目录。
2. 等待 Gradle 同步完成。
3. 点击 Run 安装到手机，或执行 `Build > Build Bundle(s) / APK(s) > Build APK(s)`。

固定更新/中继服务当前使用 `http://`，与现场 HTTPS 通道不同。权限和网络配置以 `app/src/main/AndroidManifest.xml` 及 `network_security_config.xml` 为准：

- `android.permission.INTERNET`
- `ACCESS_NETWORK_STATE` / `CHANGE_NETWORK_STATE`，用于网络状态与连接管理
- `android.permission.CAMERA`，仅用于扫描电脑端短时安全配对码
- `POST_NOTIFICATIONS`、`FOREGROUND_SERVICE` / `FOREGROUND_SERVICE_CONNECTED_DEVICE`、`RECEIVE_BOOT_COMPLETED`，用于持续守护、通知及重启后的恢复；实际启动仍检查配对与用户守护开关
- `android.permission.REQUEST_INSTALL_PACKAGES`，仅在用户从设置页明确选择“下载并安装”且更新包已通过完整性、包名、版本与签名校验后交给系统安装器；启动时不会弹授权页
- `android.hardware.camera` 标记为非必需；相机用于扫描电脑端短时安全配对码
- `network_security_config.xml` 默认拒绝明文，域名例外为 `xc213618.ddns.me` 且 `includeSubdomains="true"`；更新/中继客户端另检查固定主机、端口 `9998` 与允许的 API 路径，不能把 XML 本身说成只允许精确主机

现场运维通道使用电脑端短时二维码配对、HTTPS 证书固定和手机设备密钥签名。已配对手机可以读取脱敏状态、性能与告警、显示或最小化主窗口，并提交受控运维作业。固定 MQTT 恢复、ColorVision 应用重启、脱敏诊断包和单次主窗口快照由已配对手机明确确认后执行；支持会话仍需电脑端本机同意。

## 补充安全说明

“连接自检”会依次检查手机网络、主机解析、TCP 安全端口、TLS 证书固定、设备签名和电脑时间，并只展示不含密钥、证书指纹、设备 ID、用户名或机器名的调试摘要。临时断线不会要求用户删除配对资料。

“近期日志摘要”只读取固定大小的最新日志尾部，返回分级计数、来源分类和最多 12 条脱敏异常事件；不会返回日志文件名、路径或完整原始日志。手机可以通过系统分享面板转发同一份安全诊断摘要。

“远程排障中心”把脱敏证据汇总成固定建议。Android 端仅执行 `OperationsTriagePresentation.isSupportedAction` 与 `OperationsActivity.runTriageAction` 支持的内置动作：查看事件、显示主窗口、查看审批、服务健康、设备健康、消息通道、失败证据、性能和应用详情，以及确认重启 MQTT、确认恢复消息通道；未知动作不会成为可执行入口。详情可读不等于维护操作已获准，仍须检查当前能力和运行状态。MQTT 重启由已配对手机一次明确确认后执行，只能通过 ServiceHost 操作固定 Mosquitto 服务，不接受服务名、命令或路径等远程下发参数。

“白名单服务健康”只显示 ColorVision 后台服务与 MQTT 消息服务的规范化运行状态，不返回服务账户、程序路径或启动参数。排障中心只有在 Windows 服务管理器确认本机 MQTT 已停止或暂停时才建议维护；旧日志本身不会触发重启建议。

“作业与审批”使用面向手机的安全摘要：显示固定目标、风险、四阶段时间线和证据类型，不返回申请设备、理由、输入参数或电脑端内部收据 ID。安全分享摘要会附带同一份白名单服务状态。

手机明确确认后，电脑端会立即生成诊断 ZIP，发起作业的手机可在 24 小时内下载并分享。包内仅包含白名单运行状态、脱敏事件、规范化服务健康和去标识审计；不包含机器名、用户名、设备 ID、进程 ID、网络地址、凭据、原始日志、数据库、用户文档或图像。下载固定为 2 MiB 上限，响应与本机文件都会核对 SHA-256，其他已配对设备无法读取。

“引导支持会话”是最长 15 分钟的有限文本通道：会话只对发起它的已配对设备可见，必须先由电脑端本机同意，且只在激活且未过期时允许手机或 Web 中继交换消息。手机在等待本机同意时每 5 秒刷新，进入可输入状态后停止重建页面；单条现场消息最多 500 字。接口不会返回设备 ID、电脑账户、内部任务或审计标识，也不支持远程桌面、命令或任意文件。
