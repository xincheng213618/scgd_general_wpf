---
knowledge_id: "engine.rc-registration"
knowledge_type: "topic"
status: "current"
summary: "RC 默认注册参数、节点令牌、服务目录同步与状态快照。远端删除不清本地令牌和收发主题，更新可能部分生效。"
aliases: ["注册中心", "RC连接", "RC默认值", "RC服务列表", "服务目录同步", "远端设备删除", "终端移除", "服务状态快照", "状态新鲜度", "启动早到消息", "NodeToken", "AccessToken", "RCName", "RCNodeName", "AppId", "AppSecret", "RC重复注册", "RC释放", "CVServiceType", "注册令牌", "MqttRCService", "PendingServiceUpdateBuffer", "TryGetUsableToken", "ServiceTokensUpdated", "LiveTime", "LastAliveTime"]
code_paths: ["Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs", "Engine/ColorVision.Engine/Services/RC/PendingServiceUpdateBuffer.cs", "Engine/ColorVision.Engine/Services/RC/RCInitializer.cs", "Engine/cvColorVision/MQTTMessageLib/NodeToken.cs", "Engine/ColorVision.Engine/Services/Type/CreateType.xaml.cs", "Engine/ColorVision.Engine/MQTT/MQTTControl.cs", "Engine/ColorVision.Engine/Services/ServiceInitializer.cs", "Engine/ColorVision.Engine/Services/ServiceManager.cs", "Engine/ColorVision.Engine/Services/Devices/MQTTDeviceService.cs", "Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs", "ColorVision/StartWindow.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PendingServiceUpdateBufferTests.cs"]
related: ["engine.index", "engine.devices", "engine.mqtt", "operations.device-configuration", "operations.camera", "platform.runtime", "plugins.windows-service"]
---

# RC 注册与服务快照

`MqttRCService`（文件名 `MQTTRCService.cs`）负责注册中心协议、客户端节点令牌、服务目录及状态同步。它复用 `MQTTControl`，不是另一个 broker 客户端，也不是设备采集执行器。**MQTT 已连接、RC 连接标志为真、令牌可用、设备状态与本次命令成功是不同证据。**

MQTT 传输与设备请求见[消息契约](../../02-developer-guide/engine-development/mqtt.md)，资源创建与保存见[设备配置](../../01-user-guide/devices/configuration.md)。

## 对象、凭据与状态的责任

| 对象 | 保存或更新什么 | 不能据此推断 |
| --- | --- | --- |
| `MQTTControl` | broker 连接、订阅缓存和消息发布 | RC 已接受注册、设备准备好 |
| `MqttRCService.Token` | RC 客户端节点 `NodeToken`；查询/心跳等使用 `AccessToken` | 所有设备的服务令牌也有效 |
| `ServiceTokens` / 设备 `Config.ServiceToken` | 服务列表下发的服务令牌、设备映射和通信主题 | 令牌与所有本地资源匹配、设备可采集 |
| `IsConnect` | 收到非空 Startup Token 后置真，断开时置假 | 与本次注册请求关联、此刻令牌未过期 |
| `DeviceStatus` | 状态回包按终端及设备 Code 更新的值 | 状态足够新、本次硬件动作成功 |

桌面端直接使用 `MqttRCService` 的固定注册参数，旧客户端配置中的自定义值不会覆盖这套参数。注册主题、新建终端的收发主题与 Windows 服务管理器的配置同步共用同一注册中心名称。部署沿用自定义注册中心或注册凭据时，升级前需核对服务端 `NodeName`、`RCNodeName` 和数据库注册账户，确保与桌面端匹配。服务端文件同步见[Windows 服务管理器](../plugins/standard-plugins/windows-service.md#安装、数据库与配置顺序)。

`RCInitializer.Order=4` 先尝试 `Connect()`；失败分支还可能经 ServiceHost 启动或安装本地服务。`ServiceInitializer.Order=5` 装配服务树并应用早到更新。初始化器返回、进程启动和设备动作可执行也必须分别判断。

## 注册、令牌与保活

`MqttRCService` 构造时缓存订阅，MQTT 重连后恢复订阅并触发注册。

允许本次注册时，`RegistCore` 清空节点 Token、`ServiceTokens`，立即更新内部 `IsConnect=false` 并在需要时将界面通知投递到 Dispatcher，随后发起发布。`RequestRegist()` 对保活超时和令牌刷新检查三秒间隔。主动连接、MQTT 重连与 `Event_NotRegist` 回包走 `Regist()`。MQTT 断开时会取消排队中的重注册并清 RC 连接状态。`Regist()` 返回 true 表示已发起发布，注册结果由后续回包更新。

收到当前 `SubscribeTopic` 上的 `Event_Startup` 且 `Data.Token` 非空后，回调记录 Token 和本地接收时间，置 `IsConnect=true` 并将界面通知投递到 Dispatcher，再查询服务。`Connect()` 最多等待三秒，成功条件是共享连接标志置真。Startup 回包与等待中的注册请求没有请求 ID 关联，回调也不检查 `AccessToken` 内容。设备就绪需另行核对服务树、设备初始化及命令回包。

查询、心跳及受令牌保护的重启入口另走 `TryGetUsableToken()`：

- 有正数 `Expires` 时，以接收时间计算到期时间，提前 `min(60, max(1, Expires / 10))` 秒刷新；这里是整数除法。
- 达到刷新时间会清连接状态并尝试重新注册，本次调用不继续使用旧令牌。
- `Expires<=0` 或没有有效接收时间时，不按本地时间判过期；`TryGetUsableToken` 只检查对象与本地到期条件，不检查 `AccessToken` 内容或向服务端验证凭据。
- 定时器首次约一秒触发，之后约两秒调用 `KeepLive`；距 `LastAliveTime` 超过十秒会请求重注册。该时间在消息到达、JSON 解析之前就更新，不能把“有消息”当认证通过。

`SetDisconnectedState()` 清空节点 Token 和 `ServiceTokens` 目录，但不触发 `ServiceTokensUpdated`，也不清每个设备的 `DeviceStatus` 或既有配置里的服务令牌。异步 UI 更新、服务列表刷新和具体设备失败处理之间存在时间差，不应将连接图标当作统一状态机。

### 对象释放与延迟回调

构造单例就会装载主题、追加 MQTT 事件并启动保活定时器。`MqttRCService.Dispose()` 会幂等地释放定时器、取消排队中的延迟重注册、解除自身的 MQTT 消息与连接事件，再调用基类解除 `Processing` 回调。它仍不会清空静态单例，也不取消已经进入的注册发布或最多三秒的连接等待；因此释放后的单例不支持重新启动，Dispose 也不是远端已注销或设备状态已清理的证据。

## 启动早到消息：两槽覆盖，不是可靠队列

`DoUpdateServices` / `UpdateServiceStatus` 检查 `ServiceManager.Current`，不会为处理回包提前创建管理器。仅当实例为 null 时，才分别调用 `PendingServiceUpdateBuffer.StoreServices` / `StoreStatuses`；实例存在时直接尝试应用，不另外等待服务树“完全就绪”。

缓冲持有两类对象的引用：同类新快照覆盖旧快照，不按设备合并、不逐条重放、不深拷贝或写盘。两个槽独立更新，所以可以取到服务列表 S2 和状态 T1；锁只保护存取，**不证明它们来自同一时刻、同一版本或同一远端事务。**

服务初始化依次执行：

```text
取得 ServiceManager → Take 两槽并清空 → 应用列表 → 应用状态 → 生成设备显示控件
```

`Take()` 在应用前已清空缓冲。应用异常后需要新的 RC 快照。后续 `LoadServices()` 重建服务树本身不再次调用 `ApplyPendingServiceUpdates()`。

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

## 排查、测试与权限边界

先区分失败在哪一层：broker、RC 注册、节点 Token、服务目录/服务 Token、本地资源匹配、设备状态还是具体命令。只读调查可检查已有脱敏日志和这些源码入口；不要以“刷新一下”为由重注册、重载资源、安装服务或触发设备。

`PendingServiceUpdateBufferTests.cs` 验证快照覆盖、对象引用及取出后清空。部署验收需验证启动注册、断线后重新注册、服务目录刷新与实际设备命令。
