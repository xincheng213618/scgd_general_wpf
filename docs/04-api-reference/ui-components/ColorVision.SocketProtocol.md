---
knowledge_id: "ui.socket-protocol"
knowledge_type: "topic"
status: "current"
summary: "Socket连接管理器的监听配置、窗口关闭与服务停止、防火墙放行、消息查询和JSON/Text分发；清空消息只清列表，重发可能换客户端，Sent不证明对端执行。"
aliases: ["Socket端口连不上或消息没响应", "网络通信", "Socket 连接管理器", "Socket服务设置", "通信协议", "文本模式", "发送消息记录", "消息重发", "Socket消息搜索", "关闭Socket窗口", "停止Socket服务", "清空消息", "防火墙放行", "防火墙专用公用", "ColorVision.SocketProtocol", "SocketManager", "SocketManagerWindow", "SocketConfig", "SocketServerLifecycle", "SocketServerSettings", "SocketManagerApplicationLifetime", "SocketWorkerTracker", "SocketJsonDispatcher", "SocketTextDispatcher", "ISocketJsonHandler", "ISocketTextDispatcher", "SocketMessageManager", "SocketMessageManagerConfig", "SocketRequest", "SocketResponse", "SocketFirewallService", "WindowsFirewallStatusReader", "FirewallCommandService"]
code_paths: ["UI/ColorVision.SocketProtocol", "src/ColorVisionServiceHost/FirewallCommandService.cs", "ColorVision/App.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/SocketServerLifecycleTests.cs", "Test/ColorVision.UI.Tests/SocketShutdownTests.cs", "Test/ColorVision.UI.Tests/SocketManagerProjectionTests.cs", "Test/ColorVision.UI.Tests/SocketMessageStorageTests.cs", "Test/ColorVision.UI.Tests/SocketManagerWindowLayoutTests.cs"]
related: ["ui.index", "ui.discovery", "ui.database-query", "ui.sqlite-storage", "engine.database-maintenance", "platform.service-host"]
---

# TCP 监听、协议分发与消息记录

`ColorVision.SocketProtocol` 为桌面宿主提供 TCP 监听、JSON/Text 指令分发和收发记录。通过 **帮助 → Socket 连接管理器** 查看与管理；启用服务后，也可点击状态栏的 Socket 服务图标进入。MES、PLC 和客户设备的业务字段、权限及动作约束由各项目协议负责。

## 配置与启用服务

配置影响本机监听和现有客户端连接，操作前应确认允许中断当前通信。默认地址 `0.0.0.0` 面向本机所有 IPv4 网卡；只需本机通信时使用明确的 loopback 地址。模块没有内置 TLS、鉴权、自动重连或心跳协议，设备动作仍需项目自己的权限与流程校验。

1. 打开“Socket 连接管理器”。已有服务运行时，先关闭右上角启用开关，检查最后错误及端口释放情况。
2. 点击“服务设置”，调整地址、端口、缓冲区和解析模式。统一设置中“通信协议”也绑定同一个 `SocketConfig`。
3. 开启服务。窗口开关会保存配置；确认服务状态进入运行，并分别检查实际监听与客户端数量。出现错误时查看窗口中的错误详情。

| 配置项 | 默认值与含义 |
| --- | --- |
| `IsServerEnabled` | `false`；赋值触发启停事件，启用不等于监听成功 |
| `IPAddress` | `0.0.0.0`；启动监听时由 `IPAddress.Parse` 解析 |
| `ServerPort` | `6666`；设置值限制到 `0..65535` |
| `SocketBufferSize` | `10240` 字节；每次读取的缓冲区至少为 `1024` 字节 |
| `SocketPhraseType` | `Json`；另支持 `Text` |

`SocketInitializer` 在应用启动时读取启用配置，随后只订阅 `ServerEnabledChanged`。地址、端口、缓冲区和模式由 `SocketServerSettings.Capture` 在每次服务启动时形成快照，该次服务的客户端沿用它。**更改这些字段后需停止并重新启用服务**；保存配置不会更新已运行的监听或连接。

窗口的监听地址和模式文字来自当前配置，可能与仍在运行的旧快照不同。`SocketManager.IsConnect` 只表示 `ServerState == Running`，状态栏“已连接”也采用这一判断；它不表示已有客户端或业务通信正常。

### 关闭窗口、停止服务与应用退出

| 动作 | 完成范围 |
| --- | --- |
| 关闭管理窗口 | 解除该窗口的消息订阅与筛选视图；监听和客户端继续存在 |
| 关闭启用开关 / `StopServer()` | 请求关闭该次监听及客户端资源并投递状态；不等待在途业务 handler 完成，接口也没有取消令牌 |
| 应用退出 / `ShutdownExisting` | 禁止再次创建或启动管理器，在共用期限内等待已跟踪 worker；主程序给出 2 秒预算，超时或资源清理异常返回 `false` |

`StartServer` / `StopServer` 是 void 请求入口；应检查最终 `ServerState`、错误和实际端口。停止失败仍显示错误，不能仅凭配置已禁用判断资源释放成功。应用退出的等待结果只覆盖已跟踪工作，业务副作用、回滚和异步结果日志落盘不属于该保证。实现位于 `SocketServerLifecycle`、`SocketManagerApplicationLifetime` 和 `SocketWorkerTracker`。

## 查看与设置防火墙

顶部“防火墙 · 专用”和“防火墙 · 公用”分别显示规则匹配结果；将鼠标停在卡片上可查看规则名、远程地址范围和相反动作规则提示。

| 显示 | 读取依据 |
| --- | --- |
| 已允许 | 存在对应网络类型的允许规则，未匹配到阻止规则 |
| 可能被阻止 | 匹配到阻止规则，即使同时存在允许规则也显示此状态 |
| 未放行 | 未找到对应网络类型的应用入站允许规则 |
| 无法读取 | 防火墙策略对象不可用或读取失败；不提供放行按钮 |

`WindowsFirewallStatusReader` 只枚举已启用的入站应用规则，按当前可执行文件完整路径匹配，再区分专用/公用网络。它没有检查实际活动网络类型、规则端口/协议、全局策略或真实连通性；因此“已允许”不证明端口可达，“未放行”也不证明系统一定阻止连接。状态在管理器创建及放行操作后读取；外部更改规则不会自动刷新此处。

需要修改系统规则时：

1. 确认应允许的程序与网络类型，在对应卡片出现“放行”时点击。该操作会写入 Windows 防火墙，需具备相应管理授权。
2. 请求通过本机 `ColorVisionServiceHost` 执行，客户端不会在此自动安装或修复服务。不可用、版本过旧或票据失败的排查见[服务主机](../../03-architecture/components/service-host.md)。
3. 查看响应并核对系统实际规则。界面随后重新读取匹配状态；仍有阻止规则时，应检查具体冲突。

后端 `FirewallCommandService` 先尝试删除固定名称的旧规则，再添加当前程序在指定网络类型上的入站允许规则，使用 `protocol=any`，没有限定 Socket 端口。规则名由网络类型和可执行文件名组成，不区分同名程序的安装目录；同名旧规则可能被删除。操作失败没有自动回滚，也不会清除其它名称的阻止规则。客户端等待 15 秒，超时后命令仍可能执行，处理方式见[客户端超时与服务停止](../../03-architecture/components/service-host.md#客户端超时与服务停止)。

## 查询和查看消息

打开窗口时，`SocketMessageManager.LoadAll` 按消息 ID 排序加载记录。“消息设置”中默认查询数量为 `100`、排序为 `Desc`；传入正数时单次加载最多 `1000` 条，非正值会回退到配置值，配置本身未做有效范围校验。这些是查询参数，实时新增消息不会按此数量自动裁剪，也不构成数据库保留策略。

1. 点击“查询”或按 `F5`，按消息设置重新加载列表。需要数据库条件查询时选择“高级查询”，条件与整表操作见[通用查询](./database-query.md)。
2. 用搜索框与“全部 / 接收 / 发送”筛选。搜索不区分大小写，只匹配当前已加载记录的客户端端点、`EventName`、`MsgID`、响应码和 `ContentPreview`，不会搜索数据库全文。预览按最多 96 个 UTF-16 code unit 的前缀生成；截断规则见 [SQLite 正文存储](./sqlite-storage.md)。
3. 选中一条记录，在详情区查看端点、事件、消息 ID、响应码和按 ID 加载的完整正文。默认“格式化查看”仅改变 JSON 显示，纯文本保持原文；“复制原文”和“格式化复制”按各自方式复制，不修改存储内容。
4. 使用“重置筛选”或 `Esc` 清除关键词与方向，`Ctrl+F` 定位搜索框。默认启用“自动滚动”；多窗口共享消息集合，但筛选各自独立。

| 操作 | 影响范围 |
| --- | --- |
| 清空消息 | 仅清空共享的内存列表；数据库记录仍在，“查询”或 `F5` 可重新加载 |
| 右键“删除” / 非文本框中按 `Delete` | 按所选 ID 删除数据库记录并移出列表，没有额外确认步骤；失败记入日志 |
| 打开数据库 | 在资源管理器定位 `SocketMessages.db`，不会打开 SQL 编辑器 |
| 数据库维护 | 打开宿主提供的维护窗口；未找到 `ISocketDatabaseCleanupWindowLauncher` 时提示不可用。具体动作见[数据库维护](../engine-components/database-maintenance.md) |
| 右键“重发” | 将原始正文写给客户端，可能触发业务动作；目标规则见下文 |

默认数据库是 `%APPDATA%/ColorVision/Config/SocketMessages.db`。列表预览与完整正文分开读取；旧 TEXT 迁移、备份、锁和空间回收统一见 [SQLite 存储与维护](./sqlite-storage.md)，与 TCP 启停是独立操作。

## JSON 指令与扩展处理器

1. 在 dispatcher 构造前已加载的程序集里实现 `ISocketJsonHandler`，提供可用的无参构造函数。
2. 为 `EventName` 指定唯一、稳定且大小写准确的值。
3. 在 `Handle(NetworkStream stream, SocketRequest request)` 中返回 `SocketResponse`，明确填写业务结果与关联字段。
4. 用收发记录核对实际请求、响应和业务结果。

| 模型字段 | 类型与责任 |
| --- | --- |
| 共同字段 `Version`、`MsgID`、`EventName`、`SerialNumber` | `string`；由调用方和 handler 约定、填写 |
| 请求 `Params` | `string`；包含 JSON 参数时需作为字符串编码，不能直接替换为 JSON 对象 |
| 响应 `Code`、`Msg`、`Data` | `int`、`string`、`dynamic`；业务成功/失败码与数据由 handler 定义 |

以下仅展示字段形状，`Example.Echo` 需由接入方实现，并非内置指令：

```json
{
  "Version": "1.0",
  "MsgID": "example-1",
  "EventName": "Example.Echo",
  "SerialNumber": "demo",
  "Params": "{\"text\":\"hello\"}"
}
```

`SocketJsonDispatcher` 构造时从 `AssemblyService` 一次性发现处理器，没有随模块加载自动刷新。重复 `EventName` 保留先发现者，匹配区分大小写。返回 `404` 时先核对事件名称和程序集是否在扫描前加载。

dispatcher 不统一回填响应关联字段。内置空请求/空事件错误 `400` 与找不到 handler 的 `404` 只设置 `Code/Msg`；JSON 解析或业务处理等异常由 `SocketManager` 生成 `Code=-1`，尝试保留已解析请求的关联字段。三者不能视作相同的关联保证。

## Text 分发与 TCP 消息边界

`SocketTextDispatcher` 构造时发现 `ISocketTextDispatcher`。当前实现只调用发现顺序中的第一个 handler：返回非空白字符串时立即返回，空白时也立即返回 `null`，不会尝试后续处理器。没有 handler 时返回字面字符串 `No Dispatcher Hanle`。这是多处理器路由的实现缺口，新增第二个处理器不能作为后备路由，扫描顺序也没有业务优先级约定。

`HandleClientCore` 将每次 `NetworkStream.Read` 的字节段直接按 UTF-8 解码，再按服务快照进入 JSON 或 Text 分发。当前没有跨读取的帧累积、长度协议或多消息拆分；大包、粘包、半包及跨读取分隔的 UTF-8 字符存在处理缺口。接入协议需要可靠消息边界时必须实现并验证分帧，不能假定一次 write 对应一次 read。

## 发送记录与重发结果

正常 JSON/Text 及错误响应路径均先调用 `MessageManager.AddMessage` 创建 `Sent` 行，再写入网络。**有 Sent 不证明网络写入成功或对端已执行**；`ResponseCode` 是生成的响应内容，不是对端 ACK。接收行按读取片段登记，也不能直接用于统计业务操作次数。

`AddMessage` 在同一数据库事务内写入元数据和压缩正文，提交后才发布到 WPF 集合。数据库及 UI 发布异常被捕获记入日志，方法不返回可区分结果：返回不证明已落库，界面未出现也不证明事务未提交。JSON 正常分支出错后进入异常分支，还可能再次登记同一接收内容。

### 重新发送一条记录

重发可能触发外部业务动作。操作前应核对正文、当前连接和重复执行影响；原端点不存在且有多个连接时，界面没有供用户指定接收方的步骤。

`SocketManagerWindow.ResendMessageToClient` 的顺序是：

1. 加载所选记录的原始正文。Received 和 Sent 行都可重发，内容直接发给客户端，不会重新进入本地 handler。
2. 查找原记录端点文字包含的可写客户端远端地址；找不到则使用客户端集合中第一个可写连接。没有可用连接时提示未连接。
3. 将 UTF-8 正文写入所选连接，再追加一条 `Sent`。沿用原 `EventName/MsgID`，使用当前时间，不复制 `ResponseCode`，不修改原记录。
4. 显示重发结果。成功提示只表示该网络写入调用返回，不等待对端确认，也不保证新增记录持久化。

新记录的端点优先取当前连接远端，可能回退到本地端点或原记录文字，不能独立证明实际接收方。重发没有生成新的业务请求 ID；重复执行的幂等性由具体协议负责。

## 排查常见问题

| 现象 | 检查顺序 |
| --- | --- |
| 无法连接端口 | 启用状态 → 最后错误与实际监听 → 地址/端口 → 当前网络和系统规则；仅看防火墙卡片不足以判断 |
| 修改端口或模式后仍用旧值 | 确认旧服务已停止，再启用以捕获新快照；配置文字与运行状态分别检查 |
| 能连接但没有响应 | 检查模式、JSON 事件/程序集或 Text 首个 handler，再检查读取片段与分帧 |
| 搜索不到某条消息 | 先重置筛选，再核对查询数量/排序；关键词只搜索元数据与预览，正文读取失败另查存储日志 |
| 清空后记录又出现 | “清空消息”只影响内存列表，查询会重新读取数据库；删除单条和整表操作另有入口 |
| 有 Sent 或重发成功，对端没有结果 | 核对网络写入异常、当前目标和业务回执；检查是否因端点未匹配而选到其它客户端 |
| 升级后有列表、详情却为空 | 检查 gzip 正文与旧 TEXT 迁移情况，按 SQLite 维护主题处理 |

## 实现与验证

模块工程 `UI/ColorVision.SocketProtocol/ColorVision.SocketProtocol.csproj` 同时面向 `net8.0-windows7.0` 和 `net10.0-windows7.0`，依赖 `ColorVision.UI`、`ColorVision.Database`、`log4net`、`Newtonsoft.Json`。交付时应匹配宿主目标框架与依赖；项目可编译不替代真实监听、协议处理和业务响应验证。

| 现有测试 | 覆盖范围 |
| --- | --- |
| `SocketServerLifecycleTests` | 启停代次、配置快照、绑定/停止失败、客户端资源清理 |
| `SocketShutdownTests` | 共用关闭期限、延迟创建、worker 等待、清理重试和隔离 loopback 关闭 |
| `SocketManagerProjectionTests` | WPF 状态投影、旧状态抑制、停止错误不被禁用配置掩盖 |
| `SocketManagerWindowLayoutTests` | 筛选与空状态、详情格式化、多窗口独立筛选/关闭、维护入口及布局 |
| `SocketMessageStorageTests` | 临时库的 gzip 写入、列表不取正文、按 ID 读取和旧 TEXT 迁移 |

尚缺直接覆盖 JSON 大小写/重复名、Text 多处理器分发、TCP 分帧、网络写失败后的 Sent、重发目标与回执、防火墙规则修改的专项验证。补充验证应使用临时库、隔离客户端和无设备副作用的 handler；系统规则修改需独立验证环境。现有测试覆盖范围不等于现场业务已验收。
