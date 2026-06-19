# Engine MQTT Message Processing Handoff

This page documents the real MQTT model in the Engine layer. The main path is not one MQTT client per module: `MQTTControl` owns the connection, subscriptions, publishing, and trace buffer, while device services use `MQTTServiceBase` / `MQTTDeviceService<T>` to send commands and wait for responses.

## Current Layers

| Layer | Key Object | Responsibility |
| --- | --- | --- |
| Global connection | `MQTTControl` | Creates `IMqttClient`, connects, reconnects, caches subscriptions, publishes, keeps the latest 200 traces |
| Configuration | `MQTTSetting`, `MQTTConfig` | Host, port, username, password, secure config save |
| Startup | `MqttInitializer` | Connects MQTT during host initialization |
| Device command | `MQTTServiceBase` | Creates `MsgRecord`, sends `MsgSend`, matches `MsgReturn` by `MsgID`, handles timeout |
| Device binding | `MQTTDeviceService<T>` | Reads `SendTopic` and `SubscribeTopic` from device config |
| Message types | `MsgSend`, `MsgReturn`, `MQTTMessageLib/*EventEnum` | Event name, device code, token, parameters, return code |
| Flow MQTT nodes | `FlowEngineLib/MQTT/` | Visual Flow publish/subscribe hubs |

## Command Chain

1. Device UI, Flow node, or project code calls a concrete `MQTT*` method.
2. The method creates `MsgSend` with `EventName` and parameters.
3. `MQTTServiceBase.PublishAsyncClient()` fills `MsgID`, `DeviceCode`, `Token`, and `ServiceName`.
4. It creates `MsgRecord`, writes the message database, and starts a timeout timer.
5. `MQTTControl.PublishAsyncClient(SendTopic, json, false)` publishes the payload.
6. Broker response arrives on `SubscribeTopic`.
7. `MQTTControl` triggers `ApplicationMessageReceivedAsync`.
8. `MQTTServiceBase.Processing()` parses `MsgReturn` and matches by `MsgID`.
9. The record is marked Success or Fail and `MsgReturnReceived` is raised.

## Where to Change MQTT Behavior

| Goal | Files | Validation |
| --- | --- | --- |
| Broker config | `MQTTSetting.cs`, `MQTTConnect.xaml.cs` | Secure save, test connection, restart restore |
| Connection/reconnect | `MQTTControl.cs`, `MqttInitializer.cs` | Resubscribe after reconnect, readable traces |
| Device command | `Services/Devices/*/MQTT*.cs` | `EventName`, JSON parameters, timeout, return code |
| Device topic | `DeviceServiceConfig`, device config UI | Do not swap `SendTopic` and `SubscribeTopic` |
| Response handling | `MQTTServiceBase` or concrete callbacks | `MsgID` match, failure code, timeout |
| Flow MQTT node | `FlowEngineLib/MQTT/` | Subscribe, unsubscribe, reconnect behavior |

## Command Pattern

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

Use the style already present in the target device folder. Some services use strongly typed parameters; others use dictionaries or JSON strings.

## Troubleshooting Order

| Symptom | Check |
| --- | --- |
| Host is disconnected | `MQTTSetting.MQTTConfig`, broker address, `MqttInitializer` logs |
| Command sends but no response | `SendTopic`, device online state, `SubscribeTopic` subscription |
| Response arrives but UI does not update | `MsgID` match and `MsgReturnReceived` registration |
| Frequent timeout | Timeout value, actual device duration, broker loss |
| No messages after reconnect | `SubscribeCache()` and `MQTTControl.ResubscribeTopics()` |
| Flow MQTT node does nothing | `FlowEngineLib/MQTT` topic, hub subscription, connection state |

## Acceptance Checklist

- MQTT config persists and reconnects after restart.
- Device commands appear in `MQTTControl.GetMessageTraceSnapshot()` or logs as SEND/RECV.
- `MsgRecord` has send time, receive time, state, and return payload.
- Failed responses are not marked as success.
- Timeout clears pending state.
- Cached topics resubscribe after reconnect.

## Related Documents

- [Engine Device Service Chain](../../04-api-reference/engine-components/device-service-chain.md)
- [Engine Business Scenario Playbook](../../04-api-reference/engine-components/business-scenario-playbook.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [Testing and Validation Handoff](../testing.md)
