---
knowledge_id: "engine.rc-registration"
knowledge_type: "topic"
status: "current"
summary: "RC注册、服务目录同步、状态快照与连接测试；远端删除不清本地令牌和收发主题，更新可能部分生效，连接或测试成功不等于设备就绪。"
aliases: ["注册中心", "RC连接", "RC服务列表", "服务目录同步", "远端设备删除", "终端移除", "服务状态快照", "状态新鲜度", "启动早到消息", "RC测试连接", "RC设置", "注册中心连接配置", "新建配置文件", "复制配置文件", "NodeToken", "AccessToken", "RCName", "AppId", "AppSecret", "RC重复注册", "RC释放", "CVServiceType", "注册令牌", "MqttRCService", "PendingServiceUpdateBuffer", "RCServiceConnect", "RCSetting", "TryGetUsableToken", "ServiceTokensUpdated", "LiveTime", "LastAliveTime"]
code_paths: ["Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs", "Engine/ColorVision.Engine/Services/RC/PendingServiceUpdateBuffer.cs", "Engine/ColorVision.Engine/Services/RC/RCServiceConnect.xaml.cs", "Engine/ColorVision.Engine/Services/RC/RCServiceConnect.xaml", "Engine/ColorVision.Engine/Services/RC/RCSetting.cs", "Engine/ColorVision.Engine/Services/RC/RCServiceConfig.cs", "Engine/ColorVision.Engine/Services/RC/RCInitializer.cs", "Engine/ColorVision.Engine/Services/RC/RCFileUpload.cs", "Engine/cvColorVision/MQTTMessageLib/NodeToken.cs", "Engine/cvColorVision/MQTTMessageLib/MQTTRCServiceTypeConst.cs", "Engine/cvColorVision/MQTTMessageLib/Util/EnumTool.cs", "Engine/ColorVision.Engine/Services/Type/TypeService.cs", "Engine/ColorVision.Engine/MQTT/MQTTControl.cs", "Engine/ColorVision.Engine/Services/ServiceInitializer.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Devices/MQTTDeviceService.cs", "Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs", "ColorVision/StartWindow.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PendingServiceUpdateBufferTests.cs"]
related: ["engine.index", "engine.devices", "engine.mqtt", "operations.device-configuration", "operations.camera", "ui.configuration", "platform.runtime", "plugins.windows-service"]
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

`RCSetting.Config` 是桌面客户端的注册配置：`RCName` 选择主题命名空间，`AppId` / `AppSecret` 用于注册身份，`Name` 仅标识配置项。它不是 MQTT 的主机/端口配置，也不是 Windows 服务目录中的 `cfg/WinService.config`；服务端配置文件的同步由 [Windows 服务管理器](../plugins/standard-plugins/windows-service.md#安装、数据库与配置顺序)负责，不由改客户端字段或 `ReRegist` 自动完成。

`RCInitializer.Order=4` 先尝试 `Connect()`；失败分支还可能经 ServiceHost 启动或安装本地服务，不是只读健康检查。`ServiceInitializer.Order=5` 在 MySQL 已连接时装配服务并应用早到更新。初始化器返回、进程启动和设备动作可执行也必须分别判断。

## 注册、令牌与保活

`ReRegist()` 先 `LoadCfg()` 再 `Regist()`。`LoadCfg` 从当前配置重算 RC 主题，更新 `RCFileUpload` 的主题并调用 `SubscribeCache`；没有在这里撤销旧订阅。直接改配置字段不自动执行这一过程。

允许本次注册时，`RegistCore` 清空节点 Token、`ServiceTokens`，并向 Dispatcher 排队更新 `IsConnect=false`，随后发起发布。`Regist()` 是强制请求；`RequestRegist()` 才检查三秒间隔。该限制不覆盖所有自动入口：保活超时和令牌刷新走 `RequestRegist`，`Event_NotRegist` 回包直接调用 `Regist`；每次 `MQTTConnectChanged` 还会排队一个延迟一秒的 `ReRegist`，不检查连接值，也不合并已排队任务。返回 true 只表示走到发起发布，不等待注册中心接受；底层 `MQTTControl.PublishAsyncClient` 在客户端为空或未连接时可以直接返回。

收到当前 `SubscribeTopic` 上的 `Event_Startup` 且 `Data.Token` 非空后，回调记录 Token 和本地接收时间，排队置 `IsConnect=true`，再查询服务。此处理器没有把 Startup 与某个正在等待的注册/测试请求 ID 关联，也不检查 Token 内 `AccessToken` 是否为空；Token 对象存在不是凭据校验通过。`Connect()` 发起注册后最多循环 20 次、每次延迟 10ms，只观察共享连接标志，不等待服务树、设备初始化或采集完成。

查询、心跳及受令牌保护的重启入口另走 `TryGetUsableToken()`：

- 有正数 `Expires` 时，以接收时间计算到期时间，提前 `min(60, max(1, Expires / 10))` 秒刷新；这里是整数除法。
- 达到刷新时间会清连接状态并尝试重新注册，本次调用不继续使用旧令牌。
- `Expires<=0` 或没有有效接收时间时，不按本地时间判过期；`TryGetUsableToken` 只检查对象与本地到期条件，不检查 `AccessToken` 内容或向服务端验证凭据。
- 定时器首次约一秒触发，之后约两秒调用 `KeepLive`；距 `LastAliveTime` 超过十秒会请求重注册。该时间在消息到达、JSON 解析之前就更新，不能把“有消息”当认证通过。

`SetDisconnectedState()` 清空节点 Token 和 `ServiceTokens` 目录，但不触发 `ServiceTokensUpdated`，也不清每个设备的 `DeviceStatus` 或既有配置里的服务令牌。异步 UI 更新、服务列表刷新和具体设备失败处理之间存在时间差，不应将连接图标当作统一状态机。

### 对象释放与延迟回调

构造单例就会装载主题、追加 MQTT 事件并启动保活定时器。`MqttRCService.Dispose()` 释放该定时器并调用基类清理，但没有解除自身的 `MqttClient_ApplicationMessageReceivedAsync` 或匿名 `MQTTConnectChanged` 订阅，也没有取消已经排队的重注册任务、清空静态单例或设置禁止后续调用的标志。基类只解除自己的 `Processing` 回调。因此 Dispose 不能作为“RC 已彻底停止”的保证，后续 MQTT 事件仍可能处理回包或发起重注册；此处是源码中的生命周期缺口，不是已完成的关闭验收。

## 启动早到消息：两槽覆盖，不是可靠队列

`DoUpdateServices` / `UpdateServiceStatus` 检查 `ServiceManager.Current`，不会为处理回包提前创建管理器。仅当实例为 null 时，才分别调用 `PendingServiceUpdateBuffer.StoreServices` / `StoreStatuses`；实例存在时直接尝试应用，不另外等待服务树“完全就绪”。

缓冲持有两类对象的引用：同类新快照覆盖旧快照，不按设备合并、不逐条重放、不深拷贝或写盘。两个槽独立更新，所以可以取到服务列表 S2 和状态 T1；锁只保护存取，**不证明它们来自同一时刻、同一版本或同一远端事务。**

正常的 MySQL 已连接初始化分支依次执行：

```text
取得 ServiceManager → Take 两槽并清空 → 应用列表 → 应用状态 → 生成设备显示控件
```

`Take()` 在应用前已清空缓冲；后续应用异常没有自动重新入队或回滚保证。MySQL 未连接会跳过这条初始化分支。后续 `LoadServices()` 重建服务树本身不再次调用 `ApplyPendingServiceUpdates()`，因此不能概括为“任意重载、任意异常都绝不丢状态”。

## 服务列表、状态与本地资源的交接

### 远端目录不是本地资源的全量替换

`DoUpdateServiceTokens` 根据远端 `nodeService.Devices` 重建独立的 `ServiceTokens` 设备映射；`ApplyServices` 则将 `CVServiceType` 的成员名解析为 `ServiceTypes`，找到已有 `TypeService` 后再按 `ServiceCode` 找到已有终端。两者的匹配粒度不同：

- 终端匹配时，更新终端及其下所有使用 `DeviceServiceConfig` 的本地设备的 Topic、服务 Token，并触发设备订阅；**不逐设备核对远端 `Devices` 成员**。远端移除单个设备但保留终端时，该本地设备仍可能获得此次下发的 Topic/Token。
- 远端列表缺少某类型或终端时，本次应用跳过这些本地资源，不清其旧 Topic/Token，也不删除本地终端或设备。远端新增项没有本地匹配资源时，同样不会自动创建。本地资源的增删与保存仍由[设备配置](../../01-user-guide/devices/configuration.md)负责。

这不是“目录移除即撤销本地访问”。`MQTTDeviceService` 的通信属性直接读取当前 `Config`；后续命令如果走到 `MQTTServiceBase.PublishAsyncClient`，消息未显式指定 Token 时使用 `ServiceToken`，发布目标使用 `SendTopic`。保留的旧值可能继续被取用，但目录更新本身不等于已经发送设备命令，更不能证明远端会接受旧凭据。不要为验证残留值而触发真实设备。

### 顺序更新、部分生效与通知

两种服务枚举不能直接按数字互换。`ApplyServices` 按名称调用 `Enum.Parse`，例如回包包含 `CVServiceType.Client` 或 `Archived` 时，本地 `ServiceTypes` 没有同名成员，会在查找本地类型之前抛出；这类条目不会因“没有本地类型”自动跳过。

`UpdateServices` 依次清空并重建 `ServiceTokens`、应用或缓冲服务列表，最后调用 `ServiceTokensUpdated`。这些步骤不是原子事务：目录重建中途异常可能留下空目录或部分目录，并阻止后续本地应用；本地应用异常可能留下新目录及部分已写入的配置，并阻止通知。通知失败也不会撤销之前的写入。因此未收到通知不等于状态未改变；收到通知也不是本地服务树已完整匹配的证明，管理器尚不存在时可能只是入槽。

列表/状态接收分支只检查反序列化出的响应对象非空，未以响应 `Code` 为成功门禁，也没有按本次查询的 MsgId 关联。`Data=null` 仍可能进入更新委托，不能把“收到查询回包”当作有效完整快照。

`ApplyServiceStatus` 按终端 `ServiceCode`、设备 `Code` 顺序匹配，解析状态字符串后交给通信对象的 `DeviceStatus` setter。找不到对应项会跳过，保留旧状态，不自动标成离线。遇到无法解析的状态字符串等同步异常时，没有逐设备捕获或整批回滚：先前写入可以保留，后续项不再由此次调用处理。

异常传播取决于入口，不能统一说成“消息回调已经捕获”或“一定导致程序退出”：

| 入口 | 当前异常边界 |
| --- | --- |
| 正常 MQTT 列表/状态回包 | 通过 `Dispatcher.BeginInvoke` 排队应用。稍后委托执行时的异常不在接收回调外层 `catch` 范围内；没有该回调的捕获日志不能排除应用失败 |
| 直接调用更新方法 | `UpdateServices`、`DoUpdateServices`、`UpdateServiceStatus` 不为整个应用过程统一切换线程或捕获异常；只有 `UpdateServices` 的最后通知显式使用 `Dispatcher.Invoke` |
| 启动消费早到槽 | `ServiceInitializer` 以同步 `Dispatcher.Invoke` 调用 `ApplyPendingServiceUpdates`。同步应用失败中断本初始化器后续显示生成、相机资源初始化；异常经初始化任务传到 `StartWindow.InitializedOver` 的逐初始化器 `catch`，记录后继续其它初始化器。已被 `Take` 清空的槽不会恢复 |

上表描述列表/状态的同步应用阶段，不是所有 UI 事件的统一捕获保证；例如 `MQTTDeviceService.DeviceStatus` 还会异步投递 `DeviceStatusChanged`，其订阅者异常属于后续委托。该 setter 每第 4 次收到 `Unknown` 会跳过赋值/通知并将计数归零，其他状态不会重置此计数；因此缺少一次状态通知也不能直接解释成没有收到回包。

### 存活时间不等于设备状态有效期

`ApplyServiceStatus` 尝试将远端 `LiveTime` 解析为 `DateTime`，但后续未使用解析值验证新鲜度，不据此计算单设备状态过期时间。`KeepLive` 的十秒阈值检查的是本地 RC 消息接收时间 `LastAliveTime`，用于触发重注册，不是设备状态 TTL；到达当前 RC 订阅主题的消息会在 JSON 解析前刷新该时间。

服务树装配、实际设备许可/初始化、命令参数、匹配 MsgID 的返回结果各有检查。尤其不要把存活时间变化或 RC 状态同步直接说成“相机已经能采集”，具体条件见[相机服务](../../01-user-guide/devices/camera.md)。

## RCServiceConnect 的编辑、测试与关闭

`RCServiceConnect` 的窗口标题是 **注册中心连接配置**，字段为连接名称、注册中心、AppId 和 AppSecret。仓库 C#/XAML 中没有找到它的直接打开调用；以下约束适用于由宿主或外部扩展显式打开此窗口的场景，不提供未经确认的默认菜单路径。Windows 服务安装和配置入口见 [Windows 服务管理器](../plugins/standard-plugins/windows-service.md)。

| 操作 | 当前实现 | 影响边界 |
| --- | --- | --- |
| 打开 | 绑定 `RCSetting.Instance.Config` 活对象，另复制一次备份；将当前对象插到共享配置列表第 0 项并选中 | 不去重，不是独立编辑会话；绑定写回就影响当前对象 |
| 切换配置项 | 直接将所选对象赋给 `RCSetting.Instance.Config` | 原始对象引用及配置列表不是事务快照 |
| 新建 / 复制配置文件 | 向共享内存列表添加默认配置或当前配置副本；本身不创建文件或自动切换到新增项 | 名称中的“文件”不代表已经保存到磁盘 |
| 测试连接 | 写入密码框值，再在后台调用运行单例的 `TryRegist(cfg)` | 会改共享连接标志并发送真实注册请求，不是离线校验 |
| 取消按钮 | 将打开时备份 `CopyTo` 当前 `rcServiceConfig` 再关闭 | 切换过配置时，复制目标已变；不恢复整个列表、原引用、Token 或订阅 |
| 标题栏关闭 | `Closed` 从列表移除当前配置对象 | 没有调用取消按钮的恢复逻辑 |
| 确定 | 补名称、写密码、从列表移除当前对象，排队 `ReRegist()` 后关闭 | 不等待注册完成，窗口自身没有配置保存调用 |

列表另有两个实现缺口：`ListViewRCBorder.PreviewKeyUp` 没有判断按键，只要事件到达且存在选中项就删除它；`ManipulationBoundaryFeedback` 处理器会新增默认配置并选中。取消不会撤销这些列表变更。确定按钮和 `Closed` 各移除一次当前对象，若列表本来就含同一引用，可能删除两处引用；不能将该列表当作独立、可靠的历史配置备份。

`TryRegist` 从传入配置取注册目标主题和凭据，却继续携带运行单例当前的 `NodeName` / `SubscribeTopic`，不调用 `LoadCfg`。更换 RCName 时，注册目标与回复主题可能不是一套已同步配置。它先等待发送调用返回，再最多循环 30 次、每次延迟 10ms；这些轮询间隔不是包含发布和 Dispatcher 等待的总超时。成功判据仅为观察到共享 `IsConnect=true`；没有本次请求 ID/候选配置关联，也没有隔离正常回包、自动重注册或并发测试。

因此成功不能证明候选凭据与这次回包唯一对应、全部主题已切换或设备就绪；失败也不能证明稍后没有成功回包。正常回调仍可更新 Token、服务列表、设备主题与订阅。关闭窗口不取消后台任务，不补偿已经发出的注册及其后续效果。取消后如果要恢复运行连接，应在获得明确授权后核对配置和活动主题，而不是声称关窗已经恢复。

内存编辑与落盘分开：已实例化的 `RCSetting` 可能由后续显式 `SaveConfigs` 或 `ConfigHandler` 在启用自动保存的进程退出路径保存；保存成功仍须按[配置持久化契约](../ui-components/configuration.md)核验。不要把其它设置窗口外层的保存逻辑推定到这个未确认调用入口的窗口。

## 排查、测试与权限边界

先区分失败在哪一层：broker、RC 注册、节点 Token、服务目录/服务 Token、本地资源匹配、设备状态还是具体命令。只读调查可检查已有脱敏日志和这些源码入口；不要以“刷新一下”为由重注册、重载资源、安装服务或触发设备。

`PendingServiceUpdateBufferTests.cs` 目前验证取出两份并清空、更新列表只保留最新且不丢另一槽，断言保留相同对象引用。它不覆盖目录删除与本地匹配、部分应用和通知中断、Dispatcher 异常传播、状态新鲜度，也不覆盖真实 RC、完整启动、并发时序、异常回放、连接测试关联、窗口取消、列表键盘/触摸事件、Dispose 后回调或主题切换。本页引用测试不表示已经运行。

后续获得相应授权的隔离验证应覆盖：状态先到/列表先到、多次覆盖、管理器已存在但树不匹配、远端移除设备/终端后的本地残留、中途应用异常与通知缺失、不同入口的异常传播、旧状态保留、令牌刷新、迟到和并发 Startup、换 RCName、取消与标题栏关闭、订阅残留、显式保存与重启。真实 broker/设备和 Windows 服务另外验收。

不要把 AppSecret、AccessToken、ServiceToken 或带凭据的完整注册消息写进文档和问答记录。`RCSetting.Encryption` 只处理当前 `Config.AppSecret`，没有在该方法中遍历配置列表；不能仅凭实现 `IConfigSecure` 就断言所有历史配置或日志字段都已加密。
