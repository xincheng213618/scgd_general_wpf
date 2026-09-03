# ColorVision.SocketProtocol

ColorVision 桌面端 TCP 服务模块，负责监听、JSON/Text 分发、SQLite 消息记录和调试窗口。目标框架为 Windows WPF `net8.0-windows7.0` / `net10.0-windows7.0`；依赖以 `ColorVision.SocketProtocol.csproj` 为准。

## 使用前提

- 宿主需要 `ColorVision.UI`、`ColorVision.Database` 及项目声明的运行时依赖。
- `SocketConfig.IsServerEnabled` 默认关闭；启用后按启动时的地址、端口、缓冲区和解析模式建立服务。修改配置后需要重新启动服务才能应用。
- 默认地址为 `0.0.0.0`、端口为 `6666`，监听可能对其他设备开放；此模块不提供通用鉴权、TLS 或 TCP 分帧协议。
- 消息保存在 `%AppData%/ColorVision/Config/SocketMessages.db`；管理器初始化和收发会写入本地数据库。重发可能选择首个可写客户端并触发业务动作，必须确认目标与授权。

## 配置与扩展入口

| 任务 | 入口 |
| --- | --- |
| 配置监听、启停服务 | `SocketConfig`、`SocketInitializer`、`SocketManager` |
| 实现 JSON 指令 | `ISocketJsonHandler.EventName` 与 `Handle(NetworkStream, SocketRequest)` |
| 实现文本分发 | `ISocketTextDispatcher`、`SocketTextDispatcher` |
| 查询消息与连接诊断 | `SocketManagerWindow`、`SocketMessageManager` |

完整的配置默认值、分发、消息边界、重发和失败语义见[TCP 监听、协议分发与消息记录](../../docs/04-api-reference/ui-components/ColorVision.SocketProtocol.md)。JSON handler 在 dispatcher 构造前完成程序集加载；文本模式只调用首个发现的 handler。消息记录中的 `Sent` 不代表对端完成业务。

## 构建

在仓库根目录运行，生成本地构建和包产物：

```powershell
dotnet build .\UI\ColorVision.SocketProtocol\ColorVision.SocketProtocol.csproj -p:Platform=x64
```

[SQLite 存储与维护](../../docs/04-api-reference/ui-components/sqlite-storage.md)说明消息正文迁移、备份与空间回收；客户业务命令由所属项目协议定义。仓库文档链接需要匹配版本的完整源码，单独使用发布包时应同时查阅对应版本的文档。
