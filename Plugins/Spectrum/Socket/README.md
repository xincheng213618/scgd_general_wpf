# Spectrum Socket API

Spectrum 通过 `ColorVision.SocketProtocol` 暴露 5 个 TCP/JSON 指令，用于查询状态、连接设备、校零、自动积分和单次测量。当前 handler 直接调用 `SpectrometerManager`，不以 `MainWindow` 是否打开作为业务前置条件。

## 运行模型

```text
外部客户端
    ↓ TCP / UTF-8 JSON
SocketManager
    ↓ EventName 反射路由
Spectrum Socket Handler
    ↓ 设备门禁、超时和错误映射
SpectrometerManager
    ↓
native 光谱仪 / 标定快照 / 结果事务
```

- 默认端口是 `6666`，实际值以 `SocketConfig.ServerPort` 为准。
- 协议模式必须是 JSON。
- 当前服务器把一次 `NetworkStream.Read` 的内容直接当成一个完整 JSON，没有长度前缀、换行或缓冲解析。TCP 不保留写入边界，即使客户端只调用一次 `sendall`，请求仍可能被拆读或与后续数据合并；因此当前协议只适合受控网络中的短报文联调，不能视为可靠的生产 framing。生产使用前应在服务端增加长度前缀或换行分帧，并同步升级客户端。
- `WindowOpen` 只出现在状态响应中用于诊断，不参与连接、校零、自动积分或测量门禁。

## 通用消息

请求：

```json
{
  "EventName": "SpectrumStatus",
  "MsgID": "1",
  "Version": "1.0",
  "SerialNumber": "",
  "Params": ""
}
```

响应：

```json
{
  "EventName": "SpectrumStatus",
  "MsgID": "1",
  "Version": null,
  "SerialNumber": null,
  "Code": 200,
  "Msg": "OK",
  "Data": {}
}
```

| 字段 | 说明 |
| --- | --- |
| `EventName` | 路由名称；响应与请求一致 |
| `MsgID` | 客户端关联 ID；响应原样带回 |
| `Version` | 可选协议版本，当前示例使用 `1.0` |
| `SerialNumber` | 通用消息字段；当前 5 个 Spectrum handler 不据此选设备 |
| `Params` | 指令参数；只有 `SpectrumConnect` 使用 `connect` / `disconnect` |
| `Code` | 业务结果码 |
| `Msg` | 可直接记录或展示的结果说明 |
| `Data` | 指令返回对象；失败时通常为空 |

### 状态码

| Code | 含义 |
| --- | --- |
| `200` | 指令成功；连接成功但标定未就绪时仍为 200，必须继续检查 readiness |
| `400` / `404` | 请求缺少有效 `EventName`，或没有匹配的 handler |
| `-1` | 传输层 JSON 解析/分片等异常；不再表示“窗口未打开” |
| `-2` | 光谱仪未连接，或连接/断开失败 |
| `-3` | 已进入操作，但标定、快门、native 调用或结果生成失败；具体原因在 `Msg` |
| `-4` | 设备忙、原生会话被占用、超时或取消 |
| `-99` | 未预期异常 |

## 指令总览

| EventName | 前置条件 | 超时 | 结果 |
| --- | --- | --- | --- |
| `SpectrumStatus` | 无 | 无显式操作超时 | 当前连接与采集配置 |
| `SpectrumConnect` | 驱动和设备可用 | Manager 设备门禁 | 连接/断开状态和标定 readiness |
| `SpectrumDarkCalibration` | 已连接且快门可用 | 30 秒 | 无人值守暗场采集 |
| `SpectrumAutoIntTime` | 已连接 | 30 秒 | 自动积分时间 |
| `SpectrumMeasure` | 已连接且标定就绪 | 60 秒 | 本次测量结果 |

## SpectrumConnect

连接：

```json
{"EventName":"SpectrumConnect","MsgID":"1","Version":"1.0","Params":"connect"}
```

断开：

```json
{"EventName":"SpectrumConnect","MsgID":"2","Version":"1.0","Params":"disconnect"}
```

连接成功且可测量：

```json
{
  "EventName": "SpectrumConnect",
  "MsgID": "1",
  "Code": 200,
  "Msg": "光谱仪连接成功，标定已就绪",
  "Data": {
    "IsConnected": true,
    "IsCalibrationReady": true,
    "CalibrationStatus": "标定已加载：default"
  }
}
```

连接成功但暂不可测量：

```json
{
  "EventName": "SpectrumConnect",
  "MsgID": "1",
  "Code": 200,
  "Msg": "光谱仪已连接，但暂不可测量：标定不可用：...",
  "Data": {
    "IsConnected": true,
    "IsCalibrationReady": false,
    "CalibrationStatus": "标定不可用：..."
  }
}
```

`IsConnected` 只表示通信已经建立。客户端必须同时检查 `IsCalibrationReady`；标定路径、文件 SHA-256、native 已加载快照不一致，或标定加载/配置提交仍在进行时，测量会被拒绝。

断开响应也会返回 `IsConnected`、`IsCalibrationReady` 和 `CalibrationStatus`，便于客户端确认最终状态。

## SpectrumStatus

```json
{"EventName":"SpectrumStatus","MsgID":"3","Version":"1.0","Params":""}
```

当前 `Data`：

```json
{
  "IsConnected": true,
  "IntTime": 100.0,
  "Average": 1,
  "SerialNumber": "SP100-001",
  "EnableAutodark": true,
  "EnableAutoIntegration": false,
  "EnableAdaptiveAutoDark": false,
  "MeasurementInterval": 100,
  "MeasurementNum": 5,
  "WindowOpen": false
}
```

`SpectrumStatus` 当前不返回标定 readiness。需要在连接时取得 `IsCalibrationReady` / `CalibrationStatus`，真正测量时仍以 `SpectrumMeasure` 的门禁结果为准。

## SpectrumDarkCalibration

```json
{"EventName":"SpectrumDarkCalibration","MsgID":"4","Version":"1.0","Params":""}
```

Socket 和 Quartz 都是无人值守入口，固定使用 `requireShutter: true`：

1. 确认快门控制器可用。
2. 关闭快门，并且只接受明确的 `turn off` 响应。
3. 执行暗场采集。
4. 在结束路径重新打开快门，并且只接受明确的 `turn on` 响应。

快门未连接、关闭确认失败、暗场失败或恢复打开失败都会返回 `Code: -3`。设备忙、30 秒超时或取消返回 `Code: -4`。远程流程不会退回“人工遮光后继续”。

## SpectrumAutoIntTime

```json
{"EventName":"SpectrumAutoIntTime","MsgID":"5","Version":"1.0","Params":""}
```

成功响应：

```json
{
  "EventName": "SpectrumAutoIntTime",
  "MsgID": "5",
  "Code": 200,
  "Msg": "自动积分时间获取成功",
  "Data": { "IntTime": 150.5 }
}
```

成功值已经写回 `SpectrometerManager.IntTime`。设备忙、30 秒超时或取消返回 `-4`；native 没有给出有效积分时间返回 `-3`。

## SpectrumMeasure

```json
{"EventName":"SpectrumMeasure","MsgID":"6","Version":"1.0","Params":""}
```

handler 直接等待 `SpectrometerManager.MeasureAsync()` 返回 `SpectrumMeasurementResult`，不再监听窗口结果集合或按列表排序寻找“最新一条”。Manager 会在成功路径中把光谱结果和 `SpectrumMeasurementProfile` 放进同一数据库事务，再把结果异步投影给 UI。

成功响应：

```json
{
  "EventName": "SpectrumMeasure",
  "MsgID": "6",
  "Code": 200,
  "Msg": "测量完成",
  "Data": {
    "Lv": 123.45,
    "x": 0.312,
    "y": 0.329,
    "u": 0.198,
    "v": 0.468,
    "CCT": 6504,
    "Duv": 0.003,
    "DominantWavelength": 530.2,
    "PeakWavelength": 531.5,
    "HalfBandwidth": 2.1,
    "ColorPurity": 0.85,
    "Ra": 95,
    "IP": "85%",
    "Blue": "12.5%",
    "IntTime": 150.5
  }
}
```

未连接返回 `-2`；设备忙、60 秒超时或取消返回 `-4`；标定未就绪、无人值守自动暗场失败、native 采集失败或结果无效返回 `-3`，具体原因在 `Msg`。

## 推荐调用顺序

```text
SpectrumStatus
    ↓
SpectrumConnect(connect)
    ↓ 检查 Data.IsConnected + Data.IsCalibrationReady
标定未就绪 → 在 Spectrum 配置界面修复并重新连接/加载
    ↓
SpectrumDarkCalibration（如流程需要）
    ↓
SpectrumAutoIntTime（如流程需要）
    ↓
SpectrumMeasure（可重复，一次请求等待一次响应）
    ↓
SpectrumConnect(disconnect)
```

## Python 受控联调示例

下面只改善响应被拆成多次 `recv` 的情况，无法消除服务端把请求拆读的风险，不能直接当作生产客户端：

```python
import json
import socket

def send_command(event_name, params="", msg_id="1", host="127.0.0.1", port=6666, timeout=65):
    request = {
        "EventName": event_name,
        "MsgID": msg_id,
        "Version": "1.0",
        "SerialNumber": "",
        "Params": params,
    }
    payload = json.dumps(request, ensure_ascii=False).encode("utf-8")
    with socket.create_connection((host, port)) as client:
        client.settimeout(timeout)
        client.sendall(payload)
        response = bytearray()
        while True:
            chunk = client.recv(64 * 1024)
            if not chunk:
                raise ConnectionError("服务器在完整 JSON 响应前关闭连接")
            response.extend(chunk)
            try:
                return json.loads(response.decode("utf-8"))
            except (UnicodeDecodeError, json.JSONDecodeError):
                continue

connected = send_command("SpectrumConnect", "connect")
if connected["Code"] != 200:
    raise RuntimeError(connected["Msg"])
if not connected["Data"]["IsCalibrationReady"]:
    raise RuntimeError(connected["Data"]["CalibrationStatus"])

measurement = send_command("SpectrumMeasure", msg_id="2")
if measurement["Code"] != 200:
    raise RuntimeError(measurement["Msg"])
print(measurement["Data"]["Lv"])
```

## 新增 handler 的规则

1. 在 `Plugins/Spectrum/Socket/` 实现 `ISocketJsonHandler`；`EventName` 必须唯一，程序集扫描会自动注册。
2. 不得引用 `MainWindow`、`MessageBox`、文件对话框或同步 UI Dispatcher；后台入口只调用 Manager/控制器的语义 API。
3. 光谱仪 native 调用必须复用 `SpectrometerManager` 的 `RunExclusiveAsync` / `TryRunExclusiveAsync` 门禁，不能在 handler 中自行缓存 Handle。
4. 为耗时操作创建有界 `CancellationTokenSource`；同步 `Handle` 可用 `GetAwaiter().GetResult()` 桥接已有异步 API，不要另包一层 `Task.Run(...).Wait(...)`。
5. 把 `OperationBusy`、超时和取消映射为 `-4`；把已进入操作后的可解释失败映射为 `-3`，异常映射为 `-99`。
6. 测量直接使用 `SpectrumMeasurementResult`，不要监听 UI 集合推断完成状态。

## 相关文件

| 文件 | 说明 |
| --- | --- |
| `Plugins/Spectrum/Socket/SpectrumConnectSocketHandler.cs` | 连接、断开及 readiness 响应 |
| `Plugins/Spectrum/Socket/SpectrumStatusSocketHandler.cs` | 状态快照 |
| `Plugins/Spectrum/Socket/SpectrumDarkCalibrationSocketHandler.cs` | 强制快门的无人值守校零 |
| `Plugins/Spectrum/Socket/SpectrumAutoIntTimeSocketHandler.cs` | 自动积分 |
| `Plugins/Spectrum/Socket/SpectrumMeasureSocketHandler.cs` | 直接测量结果 |
| `Plugins/Spectrum/SpectrometerManager.cs` | 设备、标定和测量唯一入口 |
| `UI/ColorVision.SocketProtocol/SocketManager.cs` | TCP 读取、JSON 分发和响应写入 |
| `UI/ColorVision.SocketProtocol/ISocketJsonHandler.cs` | handler 接口 |
