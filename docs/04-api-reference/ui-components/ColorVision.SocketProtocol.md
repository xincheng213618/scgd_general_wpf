# ColorVision.SocketProtocol

## 目录
1. [概述](#概述)
2. [核心功能](#核心功能)
3. [主要组件](#主要组件)
4. [使用示例](#使用示例)
5. [最佳实践](#最佳实践)

## 概述

**ColorVision.SocketProtocol** 是 ColorVision 系统的 JSON 消息协议层，基于 Socket 的设备通信模块。定义了统一的消息格式和处理器接口，用于与外部设备（如光源控制器、相机等）通信。

### 基本信息

- **版本**: 1.5.5.1
- **目标框架**: .NET 8.0 / .NET 10.0 Windows
- **主要功能**: JSON 消息协议、设备通信
- **特色功能**: 消息持久化（SQLite）、插件化处理器

## 核心功能

### 消息系统
- **SocketMessage** — JSON 消息实体（ViewEntity），存储在数据库中
- **SocketMessageManager** — 消息管理器（SQLite 存储），提供消息的收发和持久化
- **ISocketJsonHandler** — 消息处理器接口，支持插件化扩展

### 配置管理
- **SocketConfig** — Socket 通信配置（IP、端口、超时等）
- **SocketConfigProvider** — 配置提供者，集成到设置系统

### 初始化
- **SocketInitializer** — 通信模块初始化器

## 主要组件

### SocketMessage

JSON 消息实体，继承自 `ViewEntity`，支持数据库持久化。

```csharp
[SugarTable("socket_messages")]
public class SocketMessage : ViewEntity
{
    public string MessageType { get; set; }
    public string JsonContent { get; set; }
    public string Source { get; set; }
    public string Target { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### SocketMessageManager

消息管理器，提供消息的发送、接收和持久化功能。

```csharp
public class SocketMessageManager
{
    public static SocketMessageManager Instance { get; }

    // 发送消息
    public void SendMessage(SocketMessage message);

    // 接收消息
    public event EventHandler<SocketMessage> MessageReceived;

    // 查询历史消息
    public List<SocketMessage> GetMessages(string source, int count = 100);
}
```

### ISocketJsonHandler

消息处理器接口，支持插件化扩展。

```csharp
public interface ISocketJsonHandler
{
    string HandlerName { get; }
    bool CanHandle(string messageType);
    void Handle(SocketMessage message);
}
```

### SocketConfig

Socket 通信配置。

```csharp
public class SocketConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
    public int ReceiveTimeout { get; set; }
    public int SendTimeout { get; set; }
    public bool AutoReconnect { get; set; }
}
```

### SocketConfigProvider

配置提供者，集成到 ColorVision 设置系统。

```csharp
public class SocketConfigProvider : IConfigSettingProvider
{
    public IEnumerable<ConfigSettingMetadata> GetConfigSettings() { ... }
}
```

## 文件清单

| 文件 | 说明 |
|------|------|
| `SocketMessage.cs` | 消息实体（ViewEntity） |
| `SocketMessageManager.cs` | 消息管理器（SQLite 存储） |
| `ISocketJsonHandler.cs` | 消息处理器接口 |
| `SocketConfig.cs` | 通信配置 |
| `SocketConfigProvider.cs` | 配置提供者 |
| `SocketInitializer.cs` | 初始化器 |

## 使用示例

### 1. 发送消息

```csharp
var message = new SocketMessage
{
    MessageType = "command",
    JsonContent = JsonConvert.SerializeObject(new { action = "set_brightness", value = 80 }),
    Source = "ColorVision",
    Target = "LightController",
    Timestamp = DateTime.Now
};

SocketMessageManager.Instance.SendMessage(message);
```

### 2. 接收消息

```csharp
SocketMessageManager.Instance.MessageReceived += (sender, message) =>
{
    Console.WriteLine($"收到消息: {message.MessageType} - {message.JsonContent}");
};
```

### 3. 自定义消息处理器

```csharp
public class LightControlHandler : ISocketJsonHandler
{
    public string HandlerName => "LightControl";

    public bool CanHandle(string messageType) => messageType == "light_response";

    public void Handle(SocketMessage message)
    {
        var data = JsonConvert.DeserializeObject<dynamic>(message.JsonContent);
        // 处理光源控制响应
    }
}
```

### 4. 查询历史消息

```csharp
var messages = SocketMessageManager.Instance.GetMessages("LightController", 50);
foreach (var msg in messages)
{
    Console.WriteLine($"[{msg.Timestamp}] {msg.MessageType}: {msg.JsonContent}");
}
```

## 依赖关系

- **引用**: ColorVision.UI, ColorVision.Database, log4net, Newtonsoft.Json
- **被引用**: ColorVision.Engine（设备通信）

## 最佳实践

1. **消息格式**: 使用统一的 JSON 消息格式，包含 type、data、timestamp 字段
2. **处理器注册**: 通过 `ISocketJsonHandler` 接口实现插件化消息处理
3. **持久化**: 重要消息自动存储到 SQLite，支持审计和回溯
4. **超时配置**: 合理设置连接超时和重连机制

## 构建

```bash
dotnet build UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj
```

## 相关资源

- [ColorVision.Database](ColorVision.Database.md) - 数据库支持
- [ColorVision.UI](ColorVision.UI.md) - 插件系统
