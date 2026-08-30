---
knowledge_id: "ui.socket-protocol"
knowledge_type: "topic"
status: "current"
summary: "TCP网络通信的监听快照、窗口关闭与服务停止、JSON/Text分发及消息记录；Sent不证明对端执行，重发可能换客户端并追加记录。"
aliases: ["Socket端口连不上或消息没响应", "网络通信", "文本模式", "发送消息记录", "消息重发", "ColorVision.SocketProtocol", "SocketManager", "SocketConfig", "SocketServerLifecycle", "SocketServerSettings", "SocketJsonDispatcher", "SocketTextDispatcher", "ISocketJsonHandler", "ISocketTextDispatcher", "SocketMessageManager"]
code_paths: ["UI/ColorVision.SocketProtocol/SocketManager.cs", "UI/ColorVision.SocketProtocol/SocketServerLifecycle.cs", "UI/ColorVision.SocketProtocol/SocketInitializer.cs", "UI/ColorVision.SocketProtocol/SocketConfig.cs", "UI/ColorVision.SocketProtocol/SocketJsonDispatcher.cs", "UI/ColorVision.SocketProtocol/SocketTextDispatcher.cs", "UI/ColorVision.SocketProtocol/ISocketJsonHandler.cs", "UI/ColorVision.SocketProtocol/ISocketTextDispatcher.cs", "UI/ColorVision.SocketProtocol/SocketMessageManager.cs", "UI/ColorVision.SocketProtocol/SocketMessagePayloadStorage.cs", "UI/ColorVision.SocketProtocol/SocketManagerWindow.xaml.cs", "ColorVision/App.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SocketServerLifecycleTests.cs", "Test/ColorVision.UI.Tests/SocketShutdownTests.cs", "Test/ColorVision.UI.Tests/SocketManagerProjectionTests.cs", "Test/ColorVision.UI.Tests/SocketMessageStorageTests.cs"]
related: ["ui.index", "ui.discovery", "ui.database-query", "ui.sqlite-storage"]
---

# TCP 监听、协议分发与消息记录

`ColorVision.SocketProtocol` 是桌面宿主中的 TCP 服务模块。配置已启用、服务在监听、客户端已连接、handler 已完成、对端已收到是不同状态。它不是外部设备协议规范，也不是项目业务协议全集；项目自己的 MES、PLC、客户设备协议应放在项目包文档里。

## 什么时候看这页

| 场景 | 先看哪里 |
| --- | --- |
| 现场说端口连不上 | `SocketConfig`、`SocketInitializer`、`SocketManager` |
| JSON 指令没有响应 | `SocketPhraseType.Json`、`EventName`、`ISocketJsonHandler` |
| 文本指令没有响应 | `SocketPhraseType.Text`、`ISocketTextDispatcher` |
| 要查收发原文 | `SocketMessageManager` 和 `SocketManagerWindow` |
| 状态栏没有 Socket 图标 | `SocketConfig.IsServerEnabled`、`SocketStatusBarProvider` |
| 升级后历史消息没了 | `%AppData%/ColorVision/Config/SocketMessages.db` |

查询窗口的条件、结果与整表操作见[通用查询](./database-query.md)；正文 gzip 按 ID 读取、旧 TEXT 迁移、备份、维护锁和空间回收统一见 [SQLite 存储与维护](./sqlite-storage.md)。这些数据库维护操作不等同于停止 TCP 服务或完成业务恢复。

## 监听配置与停止边界

`SocketInitializer` 启动时读取 `SocketConfig.Instance.IsServerEnabled`，启用后调用 `StartServer()`，并只订阅 `ServerEnabledChanged` 来启停。地址、端口、缓冲区和解析模式在启动时通过 `SocketServerSettings.Capture` 形成该次服务的配置快照；后续接入的客户端也使用这一快照，不是每次收包重读配置。

只修改或保存配置中的地址、端口、模式，不会自动改变正在运行的服务。切到 Text 后，旧服务中的连接仍按原模式处理；需在授权范围内明确停止/重新启动并验证实际监听状态，不能通过继续发送测试指令来假定新配置已生效。管理器的 `ListenAddress` 文字直接来自当前配置，可能与旧监听快照不同；`IsConnect` 只检查 `ServerState == Running`，不表示已有客户端或业务连接健康。

| 动作 | 实际责任 | 不代表什么 |
| --- | --- | --- |
| 关闭 `SocketManagerWindow` | 解除消息视图订阅、清理窗口集合视图 | 不停止服务、不关闭客户端 |
| 禁用配置 / `StopServer()` | 请求停止该次服务，关闭监听和客户端资源，后续投递停止状态 | 不等待正在执行的业务 handler 完成；handler 接口无取消令牌，设备/业务副作用不能据此推断已停止 |
| 应用退出 / `ShutdownExisting` | 进入终态并在共用期限内等待已跟踪 worker；主程序传入 2 秒预算 | 超时或清理异常会返回 false，不承诺所有业务副作用收敛；结果日志异步排队也不是持久完成证据 |

`StartServer` / `StopServer` 是 void 请求入口，不是带最终验收结果的 API。应核对 `ServerState`、最后错误和真实监听/连接；配置显示禁用也不能掩盖释放端口失败。停止/退出不提供业务回滚。

## 关键文件

| 文件 | 用途 |
| --- | --- |
| `SocketConfig.cs` | 开关、监听地址、端口、Buffer、JSON/Text 模式 |
| `SocketInitializer.cs` | 应用启动及启用开关事件的接入 |
| `SocketManager.cs` | TCP 监听、客户端列表、JSON/Text 分发、错误状态 |
| `SocketServerLifecycle.cs` | 服务配置快照、代次切换、资源关闭与 worker 收敛 |
| `SocketJsonDispatcher.cs` / `SocketTextDispatcher.cs` | 构造时发现 handler 与具体分发行为 |
| `ISocketJsonHandler.cs` | JSON 业务处理器扩展点 |
| `SocketMessage.cs` | 收发消息实体，记录方向、内容、时间、EventName、MsgID、响应码 |
| `SocketMessageManager.cs` | SQLite 持久化、查询、删除和数据库入口 |
| `SocketManagerWindow.xaml.cs` | 管理窗口、过滤、重发、详情和诊断 |
| `SocketStatusBarProvider.cs` | 状态栏图标和管理窗口入口 |

## 配置事实

当前 `SocketConfig` 只有这些通信字段：

| 字段 | 默认/说明 |
| --- | --- |
| `IsServerEnabled` | 默认 false；赋值触发启用开关事件，不等于监听成功 |
| `IPAddress` | 默认 `0.0.0.0` |
| `ServerPort` | 默认 `6666`，范围被限制到 `0..65535` |
| `SocketBufferSize` | 默认 `10240`，实际读取时最小按 `1024` |
| `SocketPhraseType` | `Json` 或 `Text`，默认 `Json` |

不要在文档或项目对接说明里承诺当前类没有的超时、自动重连、鉴权、TLS、保留策略等能力。

默认监听地址 `0.0.0.0` 不是仅限 loopback。不要因模块位于桌面进程就把它当成只对本机可达的安全边界；测试监听应使用明确授权的地址/端口和隔离 handler。

## JSON Handler

新增 JSON 指令时只做最小闭环：

1. 在 dispatcher 构造前已加载的程序集里实现 `ISocketJsonHandler`，并可被无参构造。
2. 给 `EventName` 一个唯一、稳定且大小写准确的值。
3. 在 `Handle(NetworkStream stream, SocketRequest request)` 里返回 `SocketResponse`。
4. 让业务失败显式写入 `Code` 和 `Msg`，不要只吞异常。
5. 用管理窗口确认收到的 `EventName`、`MsgID`、响应码和响应内容。

| 模型 | 关键字段 |
| --- | --- |
| `SocketRequest` | `Version`、`MsgID`、`EventName`、`SerialNumber`、`Params` |
| `SocketResponse` | `Version`、`MsgID`、`EventName`、`SerialNumber`、`Code`、`Msg`、`Data` |

如果返回 `Code = 404` 且提示 handler 不存在，优先查 `EventName` 拼写和 handler 所在程序集是否已经被 `AssemblyService` 加载。

`SocketJsonDispatcher` 在构造时一次性扫描程序集，没有随模块加载自动刷新；重复 `EventName` 保留先发现者，字典匹配区分大小写。handler 自行负责请求/响应关联字段，dispatcher 不统一回填。内置 400/404 只设置 `Code/Msg`；`SocketManager` 捕获异常时生成的 `Code=-1` 响应则尝试保留已解析请求的关联字段，不能把两类错误响应视为同一关联保证。

## Text 分发与 TCP 消息边界

`SocketTextDispatcher` 同样在构造时发现 `ISocketTextDispatcher`。当前循环在第一个 handler 返回非空字符串时立即返回，返回空/空白时也立即返回 `null`；因此实际上只调用发现顺序中的第一个 handler，不会把“未处理”请求传给后面的 handler。没有 handler 时返回字面字符串 `No Dispatcher Hanle`。新增第二个处理器不是可靠的后备路由，且扫描顺序没有声明业务优先级契约。

`HandleClientCore` 把每次 `NetworkStream.Read` 得到的字节段直接按 UTF-8 解码，再按服务快照选择 JSON 或 Text。没有跨次读取的帧累积、长度协议或多消息拆分；大包、粘包、半包以及跨读取分隔的 UTF-8 字符都不能当作已经受支持。项目需要可靠消息边界时必须明确分帧实现，不能仅在说明里约定一次 write 对应一次 read。

## 记录、网络写入和对端执行

正常 JSON/Text 及错误响应路径，均先创建 `Sent` 行并调用 `MessageManager.AddMessage`，之后才 `NetworkStream.Write`。因此数据库或界面里已有 `Sent`，仍可能发生随后的网络写入失败；`ResponseCode` 是生成响应的内容，不是对端 ACK。接收记录也以读取片段为单位，不是独立业务操作的完成账本。

`AddMessage` 将消息元数据和压缩正文放入同一数据库事务，提交后才向 WPF 消息集合发布；数据库或 UI 发布异常都被捕获并记录，方法不返回可区分的结果。其返回不证明落库；界面未出现不证明事务未提交；调用方不会因此自动停止发送。正常分支写入/handler 异常进入错误分支后，还可能再次尝试登记接收记录，不能仅以行数统计唯一请求数量。

文件仍由 `SocketMessageManager` 管理于 `%APPDATA%/ColorVision/Config/SocketMessages.db`。正文按消息 ID 延迟读取/解压，预览和元数据不是完整正文；持久化问题应核对消息记录、正文存储及日志，不修改真实库来验证说明。

## 重发不是原会话中的可靠重试

`SocketManagerWindow.ResendMessageToClient` 加载所选记录的原始正文，无论该行原来是 Received 还是 Sent，都会把它写给客户端；不会重新进入本地 JSON/Text handler。目标选择先尝试匹配原记录的端点文字，找不到可写连接就使用客户端集合中第一个可写连接，没有选择/确认目标的步骤。

网络 write 返回后，重发追加一条 `Sent`，沿用原 `EventName/MsgID`、使用当前时间，不更新原记录、不生成新业务请求 ID，也不复制原 `ResponseCode`。端点字段优先读取当前连接的远端，可能回退本地端点，读取失败还会沿用原记录；它不能独立证明实际接收方。随后显示的成功提示既不等待对端确认，也不保证 `AddMessage` 已持久化。重复消息的幂等性由具体协议负责，当前模块没有统一保证。

重发是外部写入，可能触发业务动作且目标可能改变；仅排查消息时不要自动点击。需要重放时先明确目标连接、正文、重复执行风险和授权，不能把它当成无副作用的日志查看功能。

## 现场排障

| 现象 | 第一判断 |
| --- | --- |
| 端口没有监听 | 配置是否启用、端口是否被占用、管理窗口诊断页的最后错误 |
| 能连接但无响应 | 当前模式是否选错，JSON/Text handler 是否存在 |
| JSON 返回格式异常 | 管理窗口查看原始请求、异常响应和 `SocketResponse.Code` |
| 消息列表为空 | 数据库/正文读取、UI发布和过滤分别检查；空列表不证明未收发 |
| 有 Sent 但对端未执行 | 核对网络写入异常、实际目标和业务回执，不凭记录判成功 |
| 重发到了不同连接 | 原端点匹配失败后的首个可写客户端回退，不是用户选择的目标 |
| 改了端口/模式仍用旧值 | 当前服务使用启动快照；配置显示与实际监听分别确认 |

## 发布检查

| 检查项 | 通过标准 |
| --- | --- |
| DLL 目标框架 | `net8.0-windows7.0` 或 `net10.0-windows7.0` 能被主程序加载 |
| 依赖 | `ColorVision.UI`、`ColorVision.Database`、`log4net`、`Newtonsoft.Json` 齐全 |
| 服务生命周期 | 启用后核对真实监听，禁用后核对端口释放；在途handler另行确认 |
| 协议模式 | JSON 和 Text 不互相误用 |
| Handler 扫描 | 目标 `EventName` 能进入业务处理器 |
| 消息库 | 核对元数据、压缩正文及界面发布，不以 Sent 行替代对端回执 |
| UI 入口 | 状态栏图标和管理窗口可打开，诊断信息可读 |

## 边界

- 不把这个模块写成通用网络协议框架。
- 不在这里维护项目私有协议字段。
- 不承诺鉴权、TLS、自动重连、长连接心跳等未落地能力。
- 不把 handler 当成设备控制的唯一保护层；设备动作仍应走项目自己的权限和流程校验。

## 验证入口与缺口

`SocketServerLifecycleTests` 覆盖配置快照、监听代次及客户端清理；`SocketShutdownTests` 覆盖共用关闭期限与 worker 等待；`SocketManagerProjectionTests` 覆盖停止失败不会被“禁用”配置掩盖等状态投影；`SocketMessageStorageTests` 覆盖压缩正文和按 ID 读取等存储行为。这些测试引用不是本轮已经运行的声明。

当前未登记直接验证分发器大小写/重复名/Text首个handler、网络写失败后的 Sent 记录、重发目标选择和 ACK 的专项测试；现有生命周期/存储测试不能代替这些行为，更不能证明项目业务 handler、设备安全或 TCP 分帧协议已完成验收。验证这些缺口应使用隔离 loopback 客户端、临时库和无设备副作用的 handler，而非生产服务。
