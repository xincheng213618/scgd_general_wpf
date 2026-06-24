# ColorVision.SocketProtocol

`ColorVision.SocketProtocol` 是桌面侧本地 TCP 通信模块。读这页时只关心三件事：服务有没有监听、请求有没有分发到 handler、现场消息能不能追踪。它不是外部设备协议规范，也不是项目业务协议全集；项目自己的 MES、PLC、客户设备协议应放在项目包文档里。

## 什么时候看这页

| 场景 | 先看哪里 |
| --- | --- |
| 现场说端口连不上 | `SocketConfig`、`SocketInitializer`、`SocketManager` |
| JSON 指令没有响应 | `SocketPhraseType.Json`、`EventName`、`ISocketJsonHandler` |
| 文本指令没有响应 | `SocketPhraseType.Text`、`ISocketTextDispatcher` |
| 要查收发原文 | `SocketMessageManager` 和 `SocketManagerWindow` |
| 状态栏没有 Socket 图标 | `SocketConfig.IsServerEnabled`、`SocketStatusBarProvider` |
| 升级后历史消息没了 | `%AppData%/ColorVision/Config/SocketMessages.db` |

## 运行链路

`SocketInitializer` 启动时读取 `SocketConfig.Instance.IsServerEnabled`，启用后调用 `SocketManager.GetInstance().StartServer()`。`SocketManager` 按 `IPAddress` 和 `ServerPort` 监听 TCP，收到请求后按 `SocketPhraseType` 进入 JSON 或 Text 分发。JSON 请求按 `SocketRequest.EventName` 匹配 `ISocketJsonHandler`，收发消息写入 `SocketMessageManager` 管理的 SQLite，再由管理窗口和状态栏展示。

## 关键文件

| 文件 | 用途 |
| --- | --- |
| `SocketConfig.cs` | 开关、监听地址、端口、Buffer、JSON/Text 模式 |
| `SocketInitializer.cs` | 应用启动和配置变更时启停服务 |
| `SocketManager.cs` | TCP 监听、客户端列表、JSON/Text 分发、错误状态 |
| `ISocketJsonHandler.cs` | JSON 业务处理器扩展点 |
| `SocketMessage.cs` | 收发消息实体，记录方向、内容、时间、EventName、MsgID、响应码 |
| `SocketMessageManager.cs` | SQLite 持久化、查询、删除和数据库入口 |
| `SocketManagerWindow.xaml.cs` | 管理窗口、过滤、重发、详情和诊断 |
| `SocketStatusBarProvider.cs` | 状态栏图标和管理窗口入口 |

## 配置事实

当前 `SocketConfig` 只有这些通信字段：

| 字段 | 默认/说明 |
| --- | --- |
| `IsServerEnabled` | 是否启用服务 |
| `IPAddress` | 默认 `0.0.0.0` |
| `ServerPort` | 默认 `6666`，范围被限制到 `0..65535` |
| `SocketBufferSize` | 默认 `10240`，实际读取时最小按 `1024` |
| `SocketPhraseType` | `Json` 或 `Text`，默认 `Json` |

不要在文档或项目对接说明里承诺当前类没有的超时、自动重连、鉴权、TLS、保留策略等能力。

## JSON Handler

新增 JSON 指令时只做最小闭环：

1. 在已加载程序集里新增类实现 `ISocketJsonHandler`。
2. 给 `EventName` 一个唯一、稳定的值。
3. 在 `Handle(NetworkStream stream, SocketRequest request)` 里返回 `SocketResponse`。
4. 让业务失败显式写入 `Code` 和 `Msg`，不要只吞异常。
5. 用管理窗口确认收到的 `EventName`、`MsgID`、响应码和响应内容。

| 模型 | 关键字段 |
| --- | --- |
| `SocketRequest` | `Version`、`MsgID`、`EventName`、`SerialNumber`、`Params` |
| `SocketResponse` | `Version`、`MsgID`、`EventName`、`SerialNumber`、`Code`、`Msg`、`Data` |

如果返回 `Code = 404` 且提示 handler 不存在，优先查 `EventName` 拼写和 handler 所在程序集是否已经被 `AssemblyService` 加载。

## 现场排障

| 现象 | 第一判断 |
| --- | --- |
| 端口没有监听 | 配置是否启用、端口是否被占用、管理窗口诊断页的最后错误 |
| 能连接但无响应 | 当前模式是否选错，JSON/Text handler 是否存在 |
| JSON 返回格式异常 | 管理窗口查看原始请求、异常响应和 `SocketResponse.Code` |
| 消息列表为空 | 数据库路径和写入权限是否正常 |
| 重发失败 | 原客户端是否仍在线，或是否选中了可用客户端 |
| 改了端口仍旧端口工作 | 保存配置后重启 Socket 服务链 |

当前实现按一次 `NetworkStream.Read` 读取一段数据；对大包、粘包、半包敏感的业务协议，需要在项目协议层补充分帧或长度约定。

## 发布检查

| 检查项 | 通过标准 |
| --- | --- |
| DLL 目标框架 | `net8.0-windows7.0` 或 `net10.0-windows7.0` 能被主程序加载 |
| 依赖 | `ColorVision.UI`、`ColorVision.Database`、`log4net`、`Newtonsoft.Json` 齐全 |
| 服务生命周期 | 启用后能监听，禁用后能释放端口 |
| 协议模式 | JSON 和 Text 不互相误用 |
| Handler 扫描 | 目标 `EventName` 能进入业务处理器 |
| 消息库 | 收发消息写入 `%AppData%/ColorVision/Config/SocketMessages.db` |
| UI 入口 | 状态栏图标和管理窗口可打开，诊断信息可读 |

## 边界

- 不把这个模块写成通用网络协议框架。
- 不在这里维护项目私有协议字段。
- 不承诺鉴权、TLS、自动重连、长连接心跳等未落地能力。
- 不把 handler 当成设备控制的唯一保护层；设备动作仍应走项目自己的权限和流程校验。
