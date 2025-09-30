# ColorVision.SocketProtocol

## 🎯 功能定位

网络通信协议模块，提供Socket和串口通信功能。

## 作用范围

通信协议层，为设备通信提供统一的网络接口。

## 主要功能点

- **Socket通信** - TCP/UDP网络通信协议
- **串口通信** - RS232/RS485串口设备通信
- **协议封装** - 统一的通信协议接口
- **连接管理** - 自动重连和连接状态监控
- **数据缓冲** - 高效的数据收发缓冲机制
- **异步通信** - 支持异步通信避免界面阻塞

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.Engine 引用用于设备通信
- 各插件和项目引用用于外部通信

**引用的程序集**:
- System.IO.Ports - 串口通信
- System.Net.Sockets - Socket通信

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.SocketProtocol\ColorVision.SocketProtocol.csproj" />
```

### 在主程序中的启用
- 通过设备配置自动启用对应通信方式
- 支持插件化通信协议扩展

## 开发调试

```bash
dotnet build UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj
```

## 通信示例

### Socket通信示例
```csharp
// 创建TCP客户端
var client = new SocketClient("192.168.1.100", 8080);

// 连接服务器
await client.ConnectAsync();

// 发送数据
await client.SendAsync(data);

// 接收数据
var response = await client.ReceiveAsync();
```

### 串口通信示例
```csharp
// 配置串口
var serialPort = new SerialPortClient
{
    PortName = "COM3",
    BaudRate = 9600,
    DataBits = 8,
    Parity = Parity.None,
    StopBits = StopBits.One
};

// 打开串口
serialPort.Open();

// 发送数据
serialPort.Write(data);

// 接收数据
var response = serialPort.Read();
```

## 相关文档链接

- [设备通信文档](../../docs/engine-components/README.md)
- [网络通信指南](../../docs/getting-started/入门指南.md)

## 维护者

ColorVision 通信团队
