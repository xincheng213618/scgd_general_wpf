---
knowledge_id: "engine.rc-registration"
knowledge_type: "topic"
status: "current"
summary: "RC注册令牌、启动早到服务快照与连接测试；连接标志不等于设备就绪，测试会影响运行单例，取消不回滚注册或订阅。"
aliases: ["注册中心", "RC连接", "RC服务列表", "服务状态快照", "启动早到消息", "RC测试连接", "注册令牌", "MqttRCService", "PendingServiceUpdateBuffer", "RCServiceConnect", "RCSetting", "TryGetUsableToken"]
code_paths: ["Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs", "Engine/ColorVision.Engine/Services/RC/PendingServiceUpdateBuffer.cs", "Engine/ColorVision.Engine/Services/RC/RCServiceConnect.xaml.cs", "Engine/ColorVision.Engine/Services/RC/RCServiceConnect.xaml", "Engine/ColorVision.Engine/Services/RC/RCSetting.cs", "Engine/ColorVision.Engine/Services/RC/RCServiceConfig.cs", "Engine/ColorVision.Engine/Services/RC/RCInitializer.cs", "Engine/ColorVision.Engine/Services/ServiceInitializer.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Devices/MQTTDeviceService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PendingServiceUpdateBufferTests.cs"]
related: ["engine.index", "engine.devices", "engine.mqtt", "operations.device-configuration", "operations.camera", "ui.configuration", "platform.runtime"]
---

# RC 注册、服务快照与连接测试

`MqttRCService`（文件名 `MQTTRCService.cs`）负责注册中心协议、客户端节点令牌、服务目录及状态同步。它复用 `MQTTControl`，不是另一个 broker 客户端，也不是设备采集执行器。**MQTT 已连接、RC 连接标志为真、令牌可用、设备状态与本次命令成功是不同证据。**

本页聚焦 `Services/RC/` 与本地服务树的交接。MQTT 传输/设备请求归[消息契约](../../02-developer-guide/engine-development/mqtt.md)，资源创建与保存归[设备配置](../../01-user-guide/devices/configuration.md)。注册、连接测试、重注册及重启都可能发送真实请求；文档问答不授权调用这些方法、启动 Windows 服务或操作设备。

## 对象、凭据与状态的责任

| 对象 | 保存或更新什么 | 不能据此推断 |
| --- | --- | --- |
| `MQTTControl` | broker 连接、订阅缓存和消息发布 | RC 已接受注册、设备准备好 |
| `RCSetting.Config` | 当前 RCName、AppId、AppSecret；另有共享配置列表 | 字段改变已重建运行主题或落盘 |
| `MqttRCService.Token` | RC 客户端节点 `NodeToken`；查询/心跳等使用 `AccessToken` | 所有设备的服务令牌也有效 |
| `ServiceTokens` / 设备 `Config.ServiceToken` | 服务列表下发的服务令牌、设备映射和通信主题 | 令牌与所有本地资源匹配、设备可采集 |
| `IsConnect` | 收到非空 Startup Token 后异步置真，断开时异步置假 | 与本次测试关联、此刻令牌未过期 |
| `DeviceStatus` | 状态回包按终端及设备 Code 更新的值 | 状态足够新、本次硬件动作成功 |

`RCInitializer.Order=4` 先尝试 `Connect()`；失败分支还可能经 ServiceHost 启动或安装本地服务，不是只读健康检查。`ServiceInitializer.Order=5` 在 MySQL 已连接时装配服务并应用早到更新。初始化器返回、进程启动和设备动作可执行也必须分别判断。

## 注册、令牌与保活

`ReRegist()` 先 `LoadCfg()` 再 `Regist()`。`LoadCfg` 从当前配置重算 RC 主题，更新 `RCFileUpload` 的主题并调用 `SubscribeCache`；没有在这里撤销旧订阅。直接改配置字段不自动执行这一过程。

`RegistCore` 清空节点 Token、`ServiceTokens`，并向 Dispatcher 排队更新 `IsConnect=false`，随后发起发布。公开 `Regist()` 是强制请求；自动 `RequestRegist()` 才受三秒间隔约束。返回 true 只表示走到发起发布，不等待注册中心接受；底层 `MQTTControl.PublishAsyncClient` 在客户端为空或未连接时可以直接返回。

收到当前 `SubscribeTopic` 上的 `Event_Startup` 且 `Data.Token` 非空后，回调记录 Token 和本地接收时间，排队置 `IsConnect=true`，再查询服务。此处理器没有把 Startup 与某个正在等待的注册/测试请求 ID 关联。`Connect()` 发起注册后最多循环 20 次、每次延迟 10ms，只观察共享连接标志，不等待服务树、设备初始化或采集完成。

查询、心跳及受令牌保护的重启入口另走 `TryGetUsableToken()`：

- 有正数 `Expires` 时，以接收时间计算到期时间，提前 `min(60, max(1, Expires / 10))` 秒刷新；这里是整数除法。
- 达到刷新时间会清连接状态并尝试重新注册，本次调用不继续使用旧令牌。
- `Expires<=0` 或没有有效接收时间时，不按本地时间判过期；这不是服务端有效性的证明。
- 定时器首次约一秒触发，之后约两秒调用 `KeepLive`；距 `LastAliveTime` 超过十秒会请求重注册。该时间在消息到达、JSON 解析之前就更新，不能把“有消息”当认证通过。

断开操作不在这里清空每个设备的 `DeviceStatus` 或既有配置里的服务令牌。异步 UI 更新、服务列表刷新和具体设备失败处理之间存在时间差，不应将连接图标当作统一状态机。

## 启动早到消息：两槽覆盖，不是可靠队列

`DoUpdateServices` / `UpdateServiceStatus` 检查 `ServiceManager.Current`，不会为处理回包提前创建管理器。仅当实例为 null 时，才分别调用 `PendingServiceUpdateBuffer.StoreServices` / `StoreStatuses`；实例存在时直接尝试应用，不另外等待服务树“完全就绪”。

缓冲持有两类对象的引用：同类新快照覆盖旧快照，不按设备合并、不逐条重放、不深拷贝或写盘。两个槽独立更新，所以可以取到服务列表 S2 和状态 T1；锁只保护存取，**不证明它们来自同一时刻、同一版本或同一远端事务。**

正常的 MySQL 已连接初始化分支依次执行：

```text
取得 ServiceManager → Take 两槽并清空 → 应用列表 → 应用状态 → 生成设备显示控件
```

`Take()` 在应用前已清空缓冲；后续应用异常没有自动重新入队或回滚保证。MySQL 未连接会跳过这条初始化分支。后续 `LoadServices()` 重建服务树本身不再次调用 `ApplyPendingServiceUpdates()`，因此不能概括为“任意重载、任意异常都绝不丢状态”。

## 服务列表、状态与本地资源的交接

`UpdateServices` 先清空并重建 `ServiceTokens`，再应用或缓冲服务列表，最后发布 `ServiceTokensUpdated`。服务令牌列表更新不是本地服务树已匹配完成的证明。

- 列表应用按服务类型找到已有 `TypeService`，再按 `ServiceCode` 找到已有终端；更新终端/设备的 Topic、服务 Token 并触发设备订阅。它不从远端目录自动创建设备资源。
- 状态应用按终端 `ServiceCode`、设备 `Code` 匹配，解析状态字符串后写入 `DeviceStatus`。找不到对应项会跳过，保留旧状态，不自动标成离线。
- `ApplyServiceStatus` 虽解析 `LiveTime`，但未用它验证快照新鲜度；无效状态字符串等异常也没有整批回滚保证。
- 服务树装配、实际设备许可/初始化、命令参数、匹配 MsgID 的返回结果各有检查。尤其不要把 RC 状态同步直接说成“相机已经能采集”，具体条件见[相机服务](../../01-user-guide/devices/camera.md)。

## RCServiceConnect 的编辑、测试与关闭

以下是仓库现存 `RCServiceConnect` 窗口的代码行为。当前源码检索未找到明确的直接打开调用，不能仅凭类和资源文本存在就承诺默认菜单可达，也不能把其它“注册中心配置”按钮当成同一入口。

| 操作 | 当前实现 | 影响边界 |
| --- | --- | --- |
| 打开 | 绑定 `RCSetting.Instance.Config` 活对象，另复制一次备份；复用共享配置列表 | 不是独立编辑会话，绑定写回就影响当前对象 |
| 切换配置项 | 直接将所选对象赋给 `RCSetting.Instance.Config` | 原始对象引用及配置列表不是事务快照 |
| 测试连接 | 写入密码框值，再在后台调用运行单例的 `TryRegist(cfg)` | 会改共享连接标志并发送真实注册请求，不是离线校验 |
| 取消按钮 | 将打开时备份 `CopyTo` 当前 `rcServiceConfig` 再关闭 | 切换过配置时，复制目标已变；不恢复整个列表、原引用、Token 或订阅 |
| 标题栏关闭 | `Closed` 从列表移除当前配置对象 | 没有调用取消按钮的恢复逻辑 |
| 确定 | 补名称、写密码、从列表移除当前对象，排队 `ReRegist()` 后关闭 | 不等待注册完成，窗口自身没有配置保存调用 |

`TryRegist` 从传入配置取注册目标主题和凭据，却继续携带运行单例当前的 `NodeName` / `SubscribeTopic`，不调用 `LoadCfg`。更换 RCName 时，注册目标与回复主题可能不是一套已同步配置。发送后最多循环 30 次、每次延迟 10ms，成功判据仅为观察到共享 `IsConnect=true`；没有本次请求 ID/候选配置关联，也没有隔离正常回包、自动重注册或并发测试。

因此成功不能证明候选凭据与这次回包唯一对应、全部主题已切换或设备就绪；失败也不能证明稍后没有成功回包。正常回调仍可更新 Token、服务列表、设备主题与订阅。关闭窗口不取消后台任务，不补偿已经发出的注册及其后续效果。取消后如果要恢复运行连接，应在获得明确授权后核对配置和活动主题，而不是声称关窗已经恢复。

内存编辑与落盘分开：已实例化的 `RCSetting` 可能由后续显式 `SaveConfigs` 或 `ConfigHandler` 在启用自动保存的进程退出路径保存；保存成功仍须按[配置持久化契约](../ui-components/configuration.md)核验。不要把其它设置窗口外层的保存逻辑推定到这个未确认调用入口的窗口。

## 排查、测试与权限边界

先区分失败在哪一层：broker、RC 注册、节点 Token、服务目录/服务 Token、本地资源匹配、设备状态还是具体命令。只读调查可检查已有脱敏日志和这些源码入口；不要以“刷新一下”为由重注册、重载资源、安装服务或触发设备。

`PendingServiceUpdateBufferTests.cs` 目前验证取出两份并清空、更新列表只保留最新且不丢另一槽，断言保留相同对象引用。它不覆盖真实 RC、完整启动、并发时序、异常回放、连接测试关联、窗口取消或主题切换。本页引用测试不表示已经运行。

后续获得相应授权的隔离验证应覆盖：状态先到/列表先到、多次覆盖、管理器已存在但树不匹配、应用异常、令牌刷新、迟到和并发 Startup、换 RCName、取消与标题栏关闭、订阅残留、显式保存与重启。真实 broker/设备和 Windows 服务另外验收。

不要把 AppSecret、AccessToken、ServiceToken 或带凭据的完整注册消息写进文档和问答记录。`RCSetting.Encryption` 只处理当前 `Config.AppSecret`，没有在该方法中遍历配置列表；不能仅凭实现 `IConfigSecure` 就断言所有历史配置或日志字段都已加密。
