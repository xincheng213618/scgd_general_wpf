# Spectrum Socket API 文档

## 概述

Spectrum 插件通过 `ColorVision.SocketProtocol` 框架对外暴露了 5 个 Socket JSON 指令，允许外部客户端（上位机、自动化脚本等）通过 TCP 连接远程控制光谱仪的连接、校零、测量和状态查询。

### 通信架构

```
┌──────────────┐    TCP/JSON     ┌─────────────────────────────┐
│  外部客户端   │ ◄────────────► │  SocketManager (TCP Server)  │
│  (上位机等)   │                │  端口: 配置中指定             │
└──────────────┘                └──────────┬──────────────────┘
                                           │ 根据 EventName 路由
                                           ▼
                                ┌─────────────────────────┐
                                │  SocketJsonDispatcher    │
                                │  (反射自动发现 Handler)   │
                                └─────────┬───────────────┘
                                          │
                    ┌─────────────────────┼─────────────────────┐
                    ▼                     ▼                     ▼
          SpectrumConnect      SpectrumMeasure       SpectrumStatus
          SpectrumDarkCal      SpectrumAutoIntTime
```

**自动注册机制**：所有实现 `ISocketJsonHandler` 接口的类在程序启动时通过程序集扫描自动注册到 `SocketJsonDispatcher`，无需手动配置。

---

## 通信协议

### 传输层

- **协议**: TCP
- **编码**: UTF-8
- **格式**: JSON（单行，无换行分隔符）

### 请求格式 (SocketRequest)

```json
{
    "EventName": "指令名称",
    "MsgID": "消息唯一标识",
    "Version": "1.0",
    "SerialNumber": "可选-设备序列号",
    "Params": "指令参数（字符串）"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `EventName` | string | **是** | 指令名称，用于路由到对应的 Handler |
| `MsgID` | string | 是 | 消息 ID，响应中原样返回用于匹配 |
| `Version` | string | 否 | 协议版本，默认 `"1.0"` |
| `SerialNumber` | string | 否 | 设备序列号 |
| `Params` | string | 否 | 指令参数，不同指令含义不同 |

### 响应格式 (SocketResponse)

```json
{
    "EventName": "指令名称",
    "MsgID": "与请求对应的消息ID",
    "Code": 200,
    "Msg": "操作结果描述",
    "Data": { ... }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `EventName` | string | 与请求中的 EventName 对应 |
| `MsgID` | string | 与请求中的 MsgID 对应 |
| `Code` | int | 状态码（见下方状态码表） |
| `Msg` | string | 操作结果描述 |
| `Data` | object | 返回数据，各指令不同 |

### 通用状态码

| Code | 含义 |
|------|------|
| `200` | 成功 |
| `-1` | 光谱仪窗口未打开（MainWindow 实例不存在） |
| `-2` | 光谱仪未连接 |
| `-3` | 操作执行失败（具体原因见 Msg） |
| `-4` | 操作超时 |
| `-99` | 未知异常（Msg 中包含异常信息） |

---

## 指令详解

### 1. SpectrumConnect — 连接/断开光谱仪

控制光谱仪的连接和断开，在 UI 线程执行以确保 WPF 绑定安全。

**请求**

```json
{
    "EventName": "SpectrumConnect",
    "MsgID": "1",
    "Version": "1.0",
    "Params": "connect"
}
```

**Params 取值**

| 值 | 说明 |
|----|------|
| `"connect"` | 连接光谱仪（默认值，Params 为空时默认连接） |
| `"disconnect"` | 断开光谱仪 |

**响应示例 — 连接成功**

```json
{
    "EventName": "SpectrumConnect",
    "MsgID": "1",
    "Code": 200,
    "Msg": "光谱仪连接成功",
    "Data": { "IsConnected": true }
}
```

**响应示例 — 断开成功**

```json
{
    "EventName": "SpectrumConnect",
    "MsgID": "2",
    "Code": 200,
    "Msg": "光谱仪已断开",
    "Data": null
}
```

**注意事项**
- 如果光谱仪已经连接，再次发送 connect 会直接返回 `Code: 200`、`Msg: "光谱仪已经连接"`。
- 连接/断开操作通过 `Application.Current.Dispatcher.Invoke()` 在 UI 线程执行。

---

### 2. SpectrumStatus — 查询光谱仪状态

查询光谱仪当前的连接状态和配置参数。不需要光谱仪已连接即可调用。

**请求**

```json
{
    "EventName": "SpectrumStatus",
    "MsgID": "1",
    "Version": "1.0",
    "Params": ""
}
```

**响应示例**

```json
{
    "EventName": "SpectrumStatus",
    "MsgID": "1",
    "Code": 200,
    "Msg": "OK",
    "Data": {
        "IsConnected": true,
        "IntTime": 100,
        "Average": 1,
        "SerialNumber": "SP100-001",
        "EnableAutodark": true,
        "EnableAutoIntegration": false,
        "EnableAdaptiveAutoDark": false,
        "MeasurementInterval": 100,
        "MeasurementNum": 5,
        "WindowOpen": true
    }
}
```

**Data 字段说明**

| 字段 | 类型 | 说明 |
|------|------|------|
| `IsConnected` | bool | 光谱仪是否已连接 |
| `IntTime` | float | 当前积分时间（ms） |
| `Average` | int | 平均次数 |
| `SerialNumber` | string | 设备序列号 |
| `EnableAutodark` | bool | 是否启用自动校零 |
| `EnableAutoIntegration` | bool | 是否启用自动积分时间 |
| `EnableAdaptiveAutoDark` | bool | 是否启用自适应自动校零 |
| `MeasurementInterval` | int | 测量间隔（ms） |
| `MeasurementNum` | int | 测量次数 |
| `WindowOpen` | bool | 光谱仪窗口是否已打开 |

---

### 3. SpectrumDarkCalibration — 暗电流校准（校零）

执行暗电流校准操作，自动控制快门（如果已配置）。操作超时为 **30 秒**。

**请求**

```json
{
    "EventName": "SpectrumDarkCalibration",
    "MsgID": "1",
    "Version": "1.0",
    "Params": ""
}
```

**响应示例 — 成功**

```json
{
    "EventName": "SpectrumDarkCalibration",
    "MsgID": "1",
    "Code": 200,
    "Msg": "校零成功"
}
```

**响应示例 — 超时**

```json
{
    "EventName": "SpectrumDarkCalibration",
    "MsgID": "1",
    "Code": -4,
    "Msg": "校零操作超时"
}
```

**注意事项**
- 校零过程中会自动控制快门（关闭→校零→恢复）。
- 底层调用 `SpectrometerManager.PerformDarkCalibrationAsync()`，校零结果通过 `Spectrometer.GetErrorMessage()` 映射为可读错误消息。
- 校零内部返回值 `1` 表示成功，其他值为错误码。

---

### 4. SpectrumAutoIntTime — 获取自动积分时间

获取光谱仪的自动积分时间，成功后自动将其应用到当前配置。

**请求**

```json
{
    "EventName": "SpectrumAutoIntTime",
    "MsgID": "1",
    "Version": "1.0",
    "Params": ""
}
```

**响应示例 — 成功**

```json
{
    "EventName": "SpectrumAutoIntTime",
    "MsgID": "1",
    "Code": 200,
    "Msg": "自动积分时间获取成功",
    "Data": { "IntTime": 150.5 }
}
```

**Data 字段说明**

| 字段 | 类型 | 说明 |
|------|------|------|
| `IntTime` | float | 自动计算的积分时间（ms），已自动应用到 SpectrometerManager |

**注意事项**
- 返回的积分时间已自动设置到 `SpectrometerManager.IntTime`，无需客户端额外操作。
- 支持同步频率自适应调整。

---

### 5. SpectrumMeasure — 执行光谱测量

触发一次单次光谱测量，返回完整的色度参数数据。操作超时为 **60 秒**。

**请求**

```json
{
    "EventName": "SpectrumMeasure",
    "MsgID": "1",
    "Version": "1.0",
    "Params": ""
}
```

**响应示例 — 成功**

```json
{
    "EventName": "SpectrumMeasure",
    "MsgID": "1",
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

**Data 字段说明**

| 字段 | 类型 | 说明 |
|------|------|------|
| `Lv` | float | 亮度值 (cd/m²) |
| `x` | float | CIE 1931 色坐标 x |
| `y` | float | CIE 1931 色坐标 y |
| `u` | float | CIE 1976 色坐标 u' |
| `v` | float | CIE 1976 色坐标 v' |
| `CCT` | float | 相关色温 (K) |
| `Duv` | float | 距离普朗克轨迹偏差 Duv |
| `DominantWavelength` | float | 主波长 (nm) |
| `PeakWavelength` | float | 峰值波长 (nm) |
| `HalfBandwidth` | float | 半带宽 (nm) |
| `ColorPurity` | float | 色纯度 |
| `Ra` | float | 显色指数 Ra |
| `IP` | string | IP 指标 |
| `Blue` | string | 蓝光占比 |
| `IntTime` | float | 本次测量使用的积分时间（ms） |

**注意事项**
- 测量通过监控 `MainWindow.ViewResultSpectrums` 集合的变化来获取最新结果。
- 支持排序模式（正序/倒序），根据配置中的 `OrderByType` 取最新一条记录。
- `Code: -3` 表示测量流程完成但未能从结果集合中获取数据。

---

## 使用示例

### Python 客户端示例

```python
import socket
import json

def send_command(host, port, event_name, params="", msg_id="1"):
    """发送 Socket 指令并接收响应"""
    request = {
        "EventName": event_name,
        "MsgID": msg_id,
        "Version": "1.0",
        "Params": params
    }

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.connect((host, port))
        s.sendall(json.dumps(request).encode('utf-8'))
        data = s.recv(65536)
        return json.loads(data.decode('utf-8'))

# 服务器地址（需与 SocketConfig 中配置一致）
HOST = "127.0.0.1"
PORT = 9090

# 1. 查询状态
status = send_command(HOST, PORT, "SpectrumStatus")
print(f"连接状态: {status['Data']['IsConnected']}")

# 2. 连接光谱仪
result = send_command(HOST, PORT, "SpectrumConnect", "connect", "2")
print(f"连接结果: {result['Msg']}")

# 3. 校零
result = send_command(HOST, PORT, "SpectrumDarkCalibration", msg_id="3")
print(f"校零结果: {result['Msg']}")

# 4. 获取自动积分时间
result = send_command(HOST, PORT, "SpectrumAutoIntTime", msg_id="4")
if result['Code'] == 200:
    print(f"积分时间: {result['Data']['IntTime']} ms")

# 5. 执行测量
result = send_command(HOST, PORT, "SpectrumMeasure", msg_id="5")
if result['Code'] == 200:
    data = result['Data']
    print(f"亮度: {data['Lv']}, CCT: {data['CCT']}, x={data['x']}, y={data['y']}")
```

### C# 客户端示例

```csharp
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

// 连接服务器
using var client = new TcpClient("127.0.0.1", 9090);
using var stream = client.GetStream();

// 构造请求
var request = new
{
    EventName = "SpectrumMeasure",
    MsgID = "1",
    Version = "1.0",
    Params = ""
};

// 发送
byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
stream.Write(data, 0, data.Length);

// 接收
byte[] buffer = new byte[65536];
int bytesRead = stream.Read(buffer, 0, buffer.Length);
string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

dynamic result = JsonConvert.DeserializeObject(response);
Console.WriteLine($"Code: {result.Code}, Msg: {result.Msg}");
```

---

## 典型工作流

一个完整的自动化测量流程通常按以下顺序执行：

```
1. SpectrumStatus        → 检查窗口和连接状态
2. SpectrumConnect       → 连接光谱仪（如果未连接）
3. SpectrumDarkCalibration → 校零（建议每次开机后执行）
4. SpectrumAutoIntTime   → 获取最佳积分时间
5. SpectrumMeasure       → 执行测量（可重复多次）
   ...
6. SpectrumConnect       → 断开（Params="disconnect"）
```

---

## 开发扩展

### 添加新的 Socket 指令

1. 在 `Plugins/Spectrum/Socket/` 目录下创建新类。
2. 实现 `ISocketJsonHandler` 接口：

```csharp
using ColorVision.SocketProtocol;
using System.Net.Sockets;

namespace Spectrum.Socket
{
    public class MyNewSocketHandler : ISocketJsonHandler
    {
        public string EventName => "MyNewCommand";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            // 标准前置检查
            if (MainWindow.Instance == null)
                return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -1, Msg = "光谱仪窗口未打开" };

            if (!SpectrometerManager.Instance.IsConnected)
                return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -2, Msg = "光谱仪未连接" };

            try
            {
                // 业务逻辑...
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = 200,
                    Msg = "操作成功",
                    Data = new { /* 返回数据 */ }
                };
            }
            catch (Exception ex)
            {
                return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -99, Msg = $"操作异常: {ex.Message}" };
            }
        }
    }
}
```

3. **无需注册**：`SocketJsonDispatcher` 会在启动时通过反射自动扫描所有程序集，发现并注册实现了 `ISocketJsonHandler` 的类型。

### 开发注意事项

- **UI 线程安全**：涉及 WPF UI 操作（如连接/断开设备）时，必须通过 `Application.Current.Dispatcher.Invoke()` 调度到 UI 线程。
- **异步桥接**：`ISocketJsonHandler.Handle()` 是同步接口，但底层操作往往是异步的，使用 `Task.Run(async () => await ...)` 配合 `task.Wait(timeout)` 进行桥接。
- **超时控制**：为每个耗时操作设置合理的超时（校零 30s，测量 60s），避免客户端无限等待。
- **错误码统一**：遵循通用状态码规范，保持一致的错误处理模式。

---

## 相关文件

| 文件 | 说明 |
|------|------|
| `Plugins/Spectrum/Socket/SpectrumConnectSocketHandler.cs` | 连接/断开指令处理器 |
| `Plugins/Spectrum/Socket/SpectrumStatusSocketHandler.cs` | 状态查询处理器 |
| `Plugins/Spectrum/Socket/SpectrumDarkCalibrationSocketHandler.cs` | 校零指令处理器 |
| `Plugins/Spectrum/Socket/SpectrumAutoIntTimeSocketHandler.cs` | 自动积分时间处理器 |
| `Plugins/Spectrum/Socket/SpectrumMeasureSocketHandler.cs` | 测量指令处理器 |
| `Plugins/Spectrum/SpectrometerManager.cs` | 光谱仪设备管理（底层能力提供方） |
| `UI/ColorVision.SocketProtocol/SocketManager.cs` | TCP 服务器 & JSON 分发器 |
| `UI/ColorVision.SocketProtocol/ISocketJsonHandler.cs` | Handler 接口定义 |
