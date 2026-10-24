---
knowledge_id: "engine.mqtt"
knowledge_type: "guide"
status: "current"
summary: "Engine MQTT 的连接与订阅、异步发送、请求状态、迟到回包和 MsgID 复用限制；区分 Flow 客户端池与设备命令链。"
aliases: ["MQTT请求发出为什么没有结果","MQTTControl","MQTTServiceBase","MsgRecord","MsgID","MsgReturnReceived","MQTTClientPool","MQTT迟到回包","MQTT消息超时","SubscribeCache"]
code_paths: ["Engine/ColorVision.Engine/MQTT/MQTTControl.cs","Engine/ColorVision.Engine/MQTT/MqttInitializer.cs","Engine/ColorVision.Engine/Services/Devices/MQTTDeviceService.cs","Engine/FlowEngineLib/MQTTHelper.cs","Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs","Engine/FlowEngineLib/MQTTClientPool.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MQTTClientPoolTests.cs","Test/ColorVision.UI.Tests/MQTTControlTests.cs"]
related: ["engine.index","engine.devices","engine.rc-registration","flow.runtime"]
---

# Engine MQTT 消息处理指南

Engine 设备与注册中心通过 `MQTTControl` 共用 broker 连接；设备服务由 `MQTTServiceBase` / `MQTTDeviceService<T>` 发送命令、追踪回包。Flow 的 `MQTTHelper` / `MQTTClientPool` 使用另一条连接管理链，其就绪状态、订阅和测试不能直接代表 Engine 设备链可用。

注册中心使用同一传输连接，但 RC 节点令牌、服务目录、早到快照和连接测试另有契约，见[RC 注册与状态同步](../../04-api-reference/engine-components/rc-registration.md)。MQTT 连接成功不等于 RC 注册成功，更不等于设备可执行。

## 当前 MQTT 分层

| 层级 | 关键对象 | 职责 |
| --- | --- | --- |
| 全局连接 | `MQTTControl` | 复用一个 `IMqttClient`，固定匿名连接 `127.0.0.1:1883`，负责断线重连、订阅缓存、发布消息和最近 200 条 trace |
| 启动 | `MqttInitializer` | 先连接本机 broker；失败时检查 `mosquitto` Windows 服务，按状态尝试启动后重连 |
| 设备命令 | `MQTTServiceBase` | 构造 `MsgRecord`，发送 `MsgSend`，按 `MsgID` 匹配 `MsgReturn`，处理超时 |
| 设备配置绑定 | `MQTTDeviceService<T>` | 从设备 `Config` 读取 `SendTopic`、`SubscribeTopic` |
| 消息类型 | `MsgSend`、`MsgReturn`、`MQTTMessageLib/*EventEnum` | EventName、DeviceCode、Token、参数和返回码 |
| Flow 连接 | `MQTTHelper`、`MQTTClientPool` | 按连接身份复用客户端，管理引用、topic 订阅与重连；通过该 Helper 运行的节点沿此链排查 |
| 旧 Flow MQTT 节点 | `FlowEngineLib/MQTT/`、`MQTTCustom*Node.cs` | 4 个旧节点已标记 `Obsolete`，保留旧流程加载兼容，见 [Flow 节点兼容](../../04-api-reference/engine-components/FlowEngineLib.md#弃用节点兼容) |

Engine 不读取旧 MQTT 设置段，历史地址、端口和凭据不影响连接。服务管理器向现有 `MQTT.config` 同步相同的本机端点和空凭据。Flow 的独立连接管理及旧流程序列化契约保持原样，Engine 创建的 Flow 起点使用相同固定端点。

## 命令执行链

1. 调用具体设备的 `MQTT*` 方法，创建带 `EventName` 和参数的 `MsgSend`。
2. `MQTTServiceBase.PublishAsyncClient()` 仅为 null 字段补入 `MsgID`、`DeviceCode`、`Token`、`ServiceName`；默认生成新的 GUID，不会替换调用者提供的非 null ID。
3. 创建状态为 `Sended` 的 `MsgRecord`，安排后台消息入库并启动计时；默认等待 30000 ms，设备方法可以覆盖。入库与实际发布不是一个事务。
4. 发起 `MQTTControl.PublishAsyncClient(SendTopic, json, false)`，不等待其完成便返回记录。底层在客户端不存在或未连接时直接返回，没有离线发送队列；已有请求记录仍可能随后超时。
5. 远端服务处理请求后向返回主题发布消息，broker 将它转发给订阅者。`MQTTControl` 记录 RECV 并触发 `ApplicationMessageReceivedAsync`。
6. 设备基类仅接收与 `SubscribeTopic` 字符串相同的主题，解析 `MsgReturn`，再按 `MsgID` 更新等待记录。通用关联不额外校验 `DeviceCode` 或 `EventName`。

### 请求状态与迟到回包

| 状态或证据 | 能说明什么 |
| --- | --- |
| `Sended` | 已创建本地追踪记录，不证明消息实际发出 |
| SEND trace | 底层 `PublishAsync` 返回后已记录发布；不证明远端设备执行成功 |
| `Success` / `Fail` | 匹配等待记录的回包 `Code == 0` / 非零；具体业务是否完成还需按设备协议确认 |
| `Timeout` | 本地计时到期并移除等待记录；不发送取消命令，也不停止远端已开始的动作 |

`MsgReturnReceived` 在未找到等待记录时仍会触发。因此迟到回包不能再完成已移除的原请求，却仍可能影响具体设备的状态或界面；接收器应按自己的业务规则校验身份和内容。

重试应使用新的 `MsgID`，并先确认设备是否允许重复执行。若调用者复用旧 ID，新请求可能被旧的迟到响应匹配；超时清理没有提供防重放或业务幂等保证。同一 ID 的并发请求还会与计时器字典键冲突。

`MQTTServiceBase.Dispose()` 解除共享接收事件中基类自己的 `Processing` 订阅，并释放追踪计时器、清空等待记录；派生类追加的事件和后台任务需另行清理。设备或扩展必须实际调用释放逻辑，释放界面或结束流程不自动等同于该通信对象已释放。

## 修改 MQTT 行为时看哪里

| 目标 | 主要文件 | 验收重点 |
| --- | --- | --- |
| 改连接和重连 | `MQTTControl.cs`、`MqttInitializer.cs` | 断线后订阅恢复，trace 仍可读 |
| 新增设备命令 | 对应 `Services/Devices/*/MQTT*.cs` | `EventName`、参数 JSON、超时、返回码 |
| 改设备 topic | `DeviceServiceConfig`、设备配置 UI | `SendTopic` 和 `SubscribeTopic` 不要写反 |
| 改返回处理 | `MQTTServiceBase` 或具体 `MQTT*` 回调 | `MsgID` 匹配、失败码、超时状态 |
| 旧 Flow MQTT 节点兼容 | `FlowEngineLib/MQTT/`、`MQTTCustom*Node.cs` | 只维护旧流程反序列化和运行兼容，不再作为可新建节点暴露 |

## 新增设备命令模板

```csharp
public MsgRecord DoSomething(string value)
{
    var msg = new MsgSend
    {
        EventName = "Event_DoSomething",
        Params = new Dictionary<string, object>
        {
            ["Value"] = value
        }
    };

    return PublishAsyncClient(msg, timeout: 30000);
}
```

落地时要用当前设备目录里的写法为准。有些设备使用强类型参数，有些设备使用字典或 JSON 字符串，不要为了统一格式去改全局消息模型。

## 排查顺序

| 现象 | 排查顺序 |
| --- | --- |
| 主程序显示未连接 | 本机 `1883` 端口的 broker 状态、`MqttInitializer` 日志 |
| 命令发出但无返回 | `SendTopic`、设备服务是否在线、`SubscribeTopic` 是否订阅 |
| 返回到了但界面不更新 | 原始 topic、DeviceCode、EventName、MsgID，以及具体设备回调的筛选与更新条件 |
| 经常超时 | 先查 SEND trace，区分未连接/未发布、服务未响应和设备执行过久；再比较消息内设备超时与本地等待值 |
| 重连后无消息 | `SubscribeCache()` 是否调用，重连时 `ResubscribeTopics()` 是否尝试恢复、具体订阅是否失败；`IsConnect` 不能单独证明全部订阅完成 |
| Flow MQTT 节点无反应 | `FlowEngineLib/MQTT` 节点 topic、hub 订阅状态、连接状态 |

## 连接与订阅确认

`MQTTControl` 复用一个 `IMqttClient`，连接、订阅和退订操作串行执行。重复 `Connect()` 复用现有连接并补齐未成功的订阅；`RecoverConnectionAsync()` 还确认所需 topic 全部就绪。自动重连在断线回调之外执行，失败时每三秒重试；`DisconnectAsyncClient()` 停止自动重连，之后可显式 `Connect()` 恢复。

`SubscribeCache()` 保存所需 topic，已连接时异步尝试订阅。断线立即清空已生效的订阅列表，连接后从所需集合恢复；只有 broker 接受的订阅才计为生效，异常或拒绝会记日志。离线退订也会移除恢复目标，避免下次重连重新订阅。缓存存在或连接成功不代表每个 topic 已完成订阅，按实际订阅列表、日志和测试消息核对恢复结果。

`GetMessageTraceSnapshot()` 保留最近 200 条内存记录，可用于对照 SEND/RECV；设备 `MsgRecord` 的后台数据库记录是另一套追踪资料。查找一次请求时同时核对主题、设备、事件、消息 ID 和时间，避免只看状态文字。

## 相关文档

- [Engine 设备服务链路](../../04-api-reference/engine-components/device-service-chain.md)
- [Engine 组件总览](../../04-api-reference/engine-components/README.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [测试与验证](../testing.md)

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/MQTTControlTests.cs`、`Test/ColorVision.UI.Tests/MQTTClientPoolTests.cs`。

`MQTTControlTests` 使用模拟 `IMqttClient`，覆盖固定匿名端点、并发连接与订阅复用、订阅拒绝后的恢复、离线退订、恢复取消和显式断开后重连。保留这些回归是为了保护固定端点和连接/订阅生命周期边界。`MQTTClientPoolTests` 覆盖 Flow 池的引用与连接身份、重连串行化、订阅恢复及取消/释放边界；两者均未验证真实 broker、自动重连压力或设备 `MsgRecord` 链。

在获准的测试 broker 与设备模拟环境，应分别验证配置恢复、发布失败、匹配/不匹配回包、超时后迟到消息和订阅恢复；机械运动或相机采集需按对应设备的现场验收条件执行。源码核对和文档构建不提供这些运行结果。
