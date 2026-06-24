# Engine MQTT 消息处理指南

本页说明 Engine 层 MQTT 的真实收发模型。当前主线不是“每个模块自己建客户端”，而是 `MQTTControl` 管连接、订阅和消息追踪，设备服务通过 `MQTTServiceBase` / `MQTTDeviceService<T>` 发送命令并等待返回。

## 当前 MQTT 分层

| 层级 | 关键对象 | 职责 |
| --- | --- | --- |
| 全局连接 | `MQTTControl` | 创建 `IMqttClient`、连接 broker、断线重连、订阅缓存、发布消息、保存最近 200 条 trace |
| 配置 | `MQTTSetting`、`MQTTConfig` | Host、Port、UserName、UserPwd，密码通过 `IConfigSecure` 加密保存 |
| 启动 | `MqttInitializer` | 主程序初始化时连接 MQTT，本地 broker 场景会尝试启动依赖服务 |
| 设备命令 | `MQTTServiceBase` | 构造 `MsgRecord`，发送 `MsgSend`，按 `MsgID` 匹配 `MsgReturn`，处理超时 |
| 设备配置绑定 | `MQTTDeviceService<T>` | 从设备 `Config` 读取 `SendTopic`、`SubscribeTopic` |
| 消息类型 | `MsgSend`、`MsgReturn`、`MQTTMessageLib/*EventEnum` | EventName、DeviceCode、Token、参数和返回码 |
| Flow MQTT 节点 | `FlowEngineLib/MQTT/` | 可视化流程中的 publish/subscribe hub |

## 命令执行链

1. 设备 UI、Flow 节点或项目包调用具体 `MQTT*` 方法。
2. `MQTT*` 方法创建 `MsgSend`，设置 `EventName` 和参数。
3. `MQTTServiceBase.PublishAsyncClient()` 自动补 `MsgID`、`DeviceCode`、`Token`、`ServiceName`。
4. 方法创建 `MsgRecord`，写入消息数据库，并启动超时计时器。
5. `MQTTControl.PublishAsyncClient(SendTopic, json, false)` 发布消息。
6. broker 返回消息到 `SubscribeTopic`。
7. `MQTTControl` 触发 `ApplicationMessageReceivedAsync`。
8. `MQTTServiceBase.Processing()` 解析 `MsgReturn`，按 `MsgID` 找到等待中的 `MsgRecord`。
9. 根据 `MsgReturn.Code` 标记 Success 或 Fail，并触发 `MsgReturnReceived`。

## 修改 MQTT 行为时看哪里

| 目标 | 主要文件 | 验收重点 |
| --- | --- | --- |
| 改 broker 配置 | `MQTTSetting.cs`、`MQTTConnect.xaml.cs` | 加密保存、测试连接、重启恢复 |
| 改连接和重连 | `MQTTControl.cs`、`MqttInitializer.cs` | 断线后订阅恢复，trace 仍可读 |
| 新增设备命令 | 对应 `Services/Devices/*/MQTT*.cs` | `EventName`、参数 JSON、超时、返回码 |
| 改设备 topic | `DeviceServiceConfig`、设备配置 UI | `SendTopic` 和 `SubscribeTopic` 不要写反 |
| 改返回处理 | `MQTTServiceBase` 或具体 `MQTT*` 回调 | `MsgID` 匹配、失败码、超时状态 |
| Flow MQTT 节点 | `FlowEngineLib/MQTT/` | 节点订阅、取消订阅、重连后恢复 |

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
| 主程序显示未连接 | `MQTTSetting.MQTTConfig`、broker 地址、`MqttInitializer` 日志 |
| 命令发出但无返回 | `SendTopic`、设备服务是否在线、`SubscribeTopic` 是否订阅 |
| 返回到了但界面不更新 | `MsgID` 是否匹配、`MsgReturnReceived` 是否注册 |
| 经常超时 | timeout 是否太短、设备耗时是否本来更长、broker 是否丢消息 |
| 重连后无消息 | `SubscribeCache()` 是否调用，`MQTTControl.ResubscribeTopics()` 是否执行 |
| Flow MQTT 节点无反应 | `FlowEngineLib/MQTT` 节点 topic、hub 订阅状态、连接状态 |

## 验收清单

- MQTT 配置保存后重启仍能连接。
- 设备命令发送后能在 `MQTTControl.GetMessageTraceSnapshot()` 或日志里看到 SEND/RECV。
- `MsgRecord` 有发送时间、接收时间、状态和返回内容。
- 失败返回不会被误标成成功。
- 超时后等待记录会清理，不会造成后续同 `MsgID` 误匹配。
- 断线重连后已缓存 topic 会重新订阅。

## 相关文档

- [Engine 设备服务链路](../../04-api-reference/engine-components/device-service-chain.md)
- [Engine 组件总览](../../04-api-reference/engine-components/README.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [测试与验证](../testing.md)
