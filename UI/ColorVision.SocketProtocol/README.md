# ColorVision.SocketProtocol

> 版本: 1.5.5.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows

## 功能定位

JSON 消息协议层，基于 Socket 的设备通信模块。定义了统一的消息格式和处理器接口，用于与外部设备（如光源控制器、相机等）通信。

## 主要功能

### 消息系统
- **SocketMessage** — JSON 消息实体（IEntity），存储在数据库中
- **SocketMessageManager** — 消息管理器（SQLite 存储），提供消息的收发和持久化
- **ISocketJsonHandler** — 消息处理器接口，支持插件化扩展

### 配置管理
- **SocketConfig** — Socket 通信配置（IP、端口、超时等）
- **SocketConfigProvider** — 配置提供者，集成到设置系统

### 初始化
- **SocketInitializer** — 通信模块初始化器

## 文件清单

| 文件 | 说明 |
|------|------|
| `SocketMessage.cs` | 消息实体（ViewEntity） |
| `SocketMessageManager.cs` | 消息管理器（SQLite 存储） |
| `ISocketJsonHandler.cs` | 消息处理器接口 |
| `SocketConfig.cs` | 通信配置 |
| `SocketConfigProvider.cs` | 配置提供者 |
| `SocketInitializer.cs` | 初始化器 |

## 依赖关系

- **引用**: ColorVision.UI, ColorVision.Database, log4net, Newtonsoft.Json
- **被引用**: ColorVision.Engine（设备通信）

## 构建

```bash
dotnet build UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj
```
