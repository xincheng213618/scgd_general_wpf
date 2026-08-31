---
knowledge_id: "delivery.android-operations"
knowledge_type: "topic"
status: "current"
summary: "Android原生运维入口、现场HTTPS与固定签名中继的职责边界；连接、可见证据和操作授权不能互相替代。"
aliases: ["AndroidWebViewApp","Android运维伴侣","现场运维","OperationsActivity","AppNavigationPolicy","OperationsTriagePresentation","OperationsRelayPolicy","OperationsPinnedTlsPolicy"]
code_paths: ["AndroidWebViewApp/README.md","AndroidWebViewApp/app/build.gradle","AndroidWebViewApp/app/src/main/AndroidManifest.xml","AndroidWebViewApp/app/src/main/res/xml/network_security_config.xml","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/MainActivity.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/AppNavigationPolicy.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/OperationsActivity.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/OperationsTriagePresentation.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/OperationsRelayPolicy.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/OperationsPinnedTlsPolicy.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/OperationsWatchService.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/OperationsWatchPolicy.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/AndroidUpdateClient.java","AndroidWebViewApp/app/src/main/java/com/colorvision/xcviewer/AndroidUpdatePolicy.java","UI/ColorVision.UI.Desktop/Operations/OperationsSecureHostService.cs","UI/ColorVision.UI.Desktop/Operations/OperationsSecureApiRouter.cs","UI/ColorVision.UI.Desktop/Operations/OperationsRelayClientService.cs","Web/Backend/routes/operations_relay.py","Web/Backend/services/operations_device_relay.py"]
test_paths: ["AndroidWebViewApp/app/src/test/java/com/colorvision/xcviewer/AppNavigationPolicyTest.java","AndroidWebViewApp/app/src/test/java/com/colorvision/xcviewer/OperationsPinnedTlsPolicyTest.java","AndroidWebViewApp/app/src/test/java/com/colorvision/xcviewer/OperationsRelayPolicyTest.java","AndroidWebViewApp/app/src/test/java/com/colorvision/xcviewer/OperationsTriagePresentationTest.java","AndroidWebViewApp/app/src/test/java/com/colorvision/xcviewer/OperationsWatchPolicyTest.java","AndroidWebViewApp/app/src/test/java/com/colorvision/xcviewer/AndroidUpdatePolicyTest.java","Web/Backend/test_operations_relay.py"]
related: ["delivery.backend"]
---

# Android 运维伴侣

`AndroidWebViewApp/` 是当前原生 Android 现场运维客户端，不是通用 WebView 容器。目录名保留历史命名；当前不提供下载站页面、任意网址输入或通用远程命令入口。本主题负责当前入口、通道与能力边界；`AndroidWebViewApp/README.md` 保留独立构建说明、安全细节及历次版本演进，旧版本描述不能当作当前能力叠加使用。

## 启动与源码入口

`AndroidManifest.xml` 的 launcher 是 `MainActivity`。它按配对资料和目标页经 `AppNavigationPolicy` 路由到 `OperationsActivity`；未配对时保留安全扫码引导。当前四个一级目的地是“概览 / 问题 / 工具 / 设置”，不应为了旧 README 的版本记录恢复已删除的下载站导航。

`app/build.gradle` 是 Android 构建事实来源：Java 17、compile/target SDK 36、Build Tools 36.0.0、最低 API 23，应用 ID 为 `com.colorvision.xcviewer`。Release 任务要求有效本地签名配置。Android Studio/Gradle 同步可能下载依赖，构建写产物，Run 会安装并运行手机应用；签名、安装和发布不是文档检索验证步骤。

`OperationsWatchService.start` 与 `onStartCommand` 检查配对和用户守护开关；前台页面退出不等于后台守护停止。Manifest 声明 connected-device 前台服务和开机/更新接收器。`OperationsWatchPolicy` 定义健康检查 60 秒、失败退避 30 秒至 5 分钟；这与页面前台的详细观察不是同一个生命周期，也不保证 Android 系统一定持续调度。

## 现场通道与固定中继

| 边界 | 实际入口与责任 |
| --- | --- |
| 现场直连 | 手机使用短时二维码配对、固定电脑 HTTPS 证书和设备密钥签名；桌面 `OperationsSecureHostService` 提供安全监听，`OperationsSecureApiRouter` 处理 `/ops/v1` 配对与受认证请求 |
| 手机固定中继 | `OperationsRelayPolicy` 限制为 `AppNavigationPolicy.FIXED_SERVICE_ORIGIN` 和 `/api/ops/v1/device-relay/`；使用已配对设备签名请求与电脑签名快照/回执，不让用户选择中继地址 |
| 桌面与 Web 后端 | `OperationsRelayClientService` 上传签名主机快照与配对清单、轮询手机提交的任务、上传签名回执；`Web/Backend/routes/operations_relay.py` 和 `OperationsDeviceRelayService` 接收、验证并转交有界协议。Web 存储或转交成功不等于桌面执行成功 |
| 应用更新 | `AndroidUpdateClient.check` 从固定服务读取 `/api/android/update`；`AndroidUpdatePolicy` 负责 URL 构造、同源和发布格式校验。Client 再检查下载长度、摘要、包名、版本和安装签名，最后由用户操作进入系统安装器 |

固定更新/中继源目前为 `http://xc213618.ddns.me:9998/`，不能与现场 HTTPS 通道混为一谈。`network_security_config.xml` 默认禁止明文，但域名例外包含 `xc213618.ddns.me` 的子域名；实际更新/中继客户端另外限制精确主机、端口和路径。签名校验不等于该 HTTP 传输通道已经加密。

后端同时存在使用 API key 的 Web 运维路由和设备签名中继路由，不应把两者的凭据/授权模型互换；Android 已配对设备不是凭插件市场登录状态取得电脑控制权。插件市场存储和服务启动另见[插件市场后端](./README.md)。

## 可见能力不等于操作许可

`OperationsTriagePresentation.isSupportedAction` 与 `OperationsActivity.runTriageAction` 共同给出当前排障动作边界：事件、主窗口显示、审批、服务、设备、消息通道、失败证据、性能、应用详情，以及 MQTT 重启与消息通道恢复。未知 action 不执行；不是由服务端随意下发 URL、命令或服务名。

`OperationsRelayPolicy.isAllowedTaskCapability` 另限定中继可提交的能力，例如窗口操作、消息恢复、MQTT 重启、取消检测、应用重启、诊断与主窗口快照；它不是排障页面 action ID 的同一张表。具体动作继续检查能力是否开放、签名状态是否新鲜、检测是否活动等条件，并要求相应用户确认。能打开详情或显示“在线”不能证明维护允许，也不能把请求已受理当作作业完成。

诊断包、快照、恢复或重启是会触发电脑动作和可能写文件的操作；支持会话还需要电脑端本机同意。它们不是任意桌面、命令、文件或设备控制通道。不要以排查文档或检查连通性为由自动申请作业、下载证据、撤销配对或重启服务。

## 定位与验证边界

- 入口/导航不符：先查 `MainActivity`、`AppNavigationPolicy` 与保存的配对状态，而不是从目录名推断 WebView。
- 直连失败：查固定证书、配对和请求认证；中继失败：另查固定 URL 策略、签名与快照新鲜度，不用停留在“网络能通”。
- 动作缺失或禁用：查当前通道的 capability 与页面 action 的不同白名单、运行状态及确认条件；旧日志、旧快照与旧版本 README 均不直接授权操作。
- `AppNavigationPolicyTest`、`OperationsPinnedTlsPolicyTest`、`OperationsRelayPolicyTest`、`OperationsTriagePresentationTest`、`OperationsWatchPolicyTest`、`AndroidUpdatePolicyTest` 提供相应纯逻辑边界；后端 `test_operations_relay.py` 是另一层协议测试。文件存在不是本次运行通过的声明。

本主题不是 Android/桌面/后端全部运维 API 的完整安全审计。实际手机权限、系统后台限制、配对撤销、证书轮换、跨网络切换、更新签名和真实作业回执仍需分别取得授权后专项验收，不能由文档构建替代。
