# ColorVision.SocketProtocol

本页只描述 UI/ColorVision.SocketProtocol 当前已经落地的通信实现，不再延续旧文档里那种“通用 JSON 协议层示例”和不匹配的消息模型说明。

## 模块定位

ColorVision.SocketProtocol 当前是一个桌面侧本地 TCP 通信模块，主要负责：

- 启动和停止 Socket 服务器
- 分发 JSON 或纯文本请求
- 持久化消息记录到 SQLite
- 提供管理窗口和状态栏入口
- 接入设置系统

它不是一个抽象的“设备协议规范文档”，而是一套已经和 UI、配置、数据库浏览入口耦合在一起的实际模块。

## 当前最关键的文件

从项目目录看，最值得优先阅读的是：

- `SocketManager.cs`：服务器、客户端、分发器和消息管理主入口
- `SocketInitializer.cs`：启动时初始化和启停监听
- `SocketConfig.cs`：通信配置
- `ISocketJsonHandler.cs`：JSON 请求处理扩展点
- `SocketMessage.cs`：消息持久化实体
- `SocketMessageManager.cs`：SQLite 持久化和查询
- `SocketManagerWindow.xaml(.cs)`：管理和查看窗口
- `SocketStatusBarProvider.cs`：状态栏入口
- `SocketConfigProvider.cs`：设置系统接入点

## 关键入口类型

### SocketManager

`SocketManager` 是当前通信模块的中心对象。它负责：

- 持有 `SocketConfig`
- 创建 `SocketJsonDispatcher` 和 `SocketTextDispatcher`
- 管理 `SocketMessageManager`
- 启动和停止服务器
- 跟踪连接状态
- 暴露配置编辑命令

如果只读一个文件来理解整个模块，首选就是 `SocketManager.cs`。

### SocketInitializer

当前模块确实存在 `SocketInitializer`，而且它是实际启动入口之一。它会：

- 启动时读取 `SocketConfig.Instance.IsServerEnabled`
- 在启用时调用 `SocketManager.GetInstance().StartServer()`
- 订阅 `ServerEnabledChanged`，在运行中动态启停服务

这意味着通信服务是否上线，当前主要受配置驱动，而不是仅靠用户手动打开窗口。

### SocketConfig

`SocketConfig` 当前配置内容主要包括：

- 是否启用服务器
- 监听 IP
- 端口
- Buffer 大小
- 协议模式：`Json` 或 `Text`

旧文档里写的超时、自动重连等字段，并不是当前类里真实存在的配置项。

### SocketJsonDispatcher / SocketTextDispatcher

当前协议分发分成两套：

- `SocketJsonDispatcher`：扫描 `ISocketJsonHandler`
- `SocketTextDispatcher`：扫描 `ISocketTextDispatcher`

其中 JSON 处理器当前按 `EventName` 匹配，请求和响应的真实模型是：

- `SocketRequest`：`Version`、`MsgID`、`EventName`、`SerialNumber`、`Params`
- `SocketResponse`：`Version`、`MsgID`、`EventName`、`SerialNumber`、`Code`、`Msg`、`Data`

因此它不是旧文档里那种泛化的 `type/data/timestamp` 消息格式。

### SocketMessage / SocketMessageManager

当前消息持久化不是一个概念层功能，而是直接落地在 SQLite。`SocketMessage` 保存的主要是：

- 客户端地址
- 方向（接收/发送）
- 内容
- 时间
- EventName / MsgID / ResponseCode

`SocketMessageManager` 则负责：

- 初始化 `SocketMessages.db`
- 加载最近消息
- 插入、删除和查询消息
- 打开数据库文件位置
- 提供数据库浏览入口

数据库默认路径在：

- `%AppData%/ColorVision/Config/SocketMessages.db`

### SocketManagerWindow 与 SocketStatusBarProvider

当前用户侧主要入口不是一堆协议示例代码，而是两个 UI 接入点：

- `SocketManagerWindow`：查看历史消息、消息详情、复制、重发、删除
- `SocketStatusBarProvider`：在状态栏反映连接状态，并点击打开管理窗口

另外，`SocketManagerWindow.xaml.cs` 里还定义了一个菜单入口类 `MenuProjectManager`，当前挂在 Help 菜单下打开管理窗口。

当前管理窗口已经不只是“消息列表 + 详情”的最小形态。窗口顶部会显示服务启用状态、服务是否打开、监听地址、协议模式和客户端数量；打开失败时会直接显示最后一次错误信息。消息区支持文本过滤、方向过滤、自动滚动和列表虚拟化；右侧通过“消息详情 / 连接的客户端 / 服务诊断”标签页组织信息，详情区支持 JSON 格式化查看。重发消息时会优先按原始客户端地址匹配连接，找不到时可以使用当前选中的客户端作为兜底目标。

常用快捷键：

- `Ctrl+F`：聚焦过滤框
- `Esc`：清空过滤
- `F5`：重新加载最近消息
- `Ctrl+C`：复制选中消息内容
- `Delete`：删除选中消息

## 当前运行时主链

现有链路大致是：

1. `SocketInitializer` 启动并监听 `SocketConfig.Instance.IsServerEnabled`。
2. 服务启用时，`SocketManager` 启动 TCP 服务器。
3. 收到请求后，按当前配置的协议模式走 JSON 或 Text 分发。
4. JSON 请求按 `EventName` 匹配到 `ISocketJsonHandler` 实现。
5. 收发消息被写入 `SocketMessageManager` 管理的 SQLite 数据库。
6. `SocketStatusBarProvider` 和 `SocketManagerWindow` 从管理器读取状态与消息列表。

## 当前实现有哪些边界

### 不是纯 JSON 协议库

虽然 JSON 是主要模式之一，但当前实现同时支持 `SocketPhraseType.Text`。把整个模块写成“统一 JSON 协议层”会漏掉文本模式和状态栏、窗口、持久化这些真实职责。

### 不是只有处理器接口

旧文档把重点压在 `ISocketJsonHandler` 上，但当前模块的价值同样来自：

- 初始化器
- 管理窗口
- 状态栏入口
- SQLite 消息历史

如果只写 handler 扩展点，很容易把模块写扁。

### 配置字段要按真实类描述

当前 `SocketConfig` 没有旧文档里声称的 `ReceiveTimeout`、`SendTimeout`、`AutoReconnect` 这些字段。描述通信配置时必须以真实属性为准。

## 当前更适合怎样读这个模块

### 想看服务器和分发主链

先看：

- `SocketManager.cs`
- `SocketInitializer.cs`
- `ISocketJsonHandler.cs`

### 想看设置和状态栏接入

先看：

- `SocketConfig.cs`
- `SocketConfigProvider.cs`
- `SocketStatusBarProvider.cs`

### 想看消息历史和管理窗口

先看：

- `SocketMessage.cs`
- `SocketMessageManager.cs`
- `SocketManagerWindow.xaml.cs`

## 优化路线

这个模块后续优化建议分四层推进：

| 阶段 | 目标 | 重点 |
| --- | --- | --- |
| P0 稳定性 | 把服务生命周期和 TCP 边界收紧 | 防重复启动、取消令牌、统一停止路径、粘包/半包处理 |
| P1 可观测性 | 提高现场排查效率 | 消息导出、连接生命周期、错误统计、处理耗时 |
| P2 协议化 | 降低外部设备对接成本 | 错误码、Handler 元数据、JSON Schema、版本兼容 |
| P3 性能与容量 | 支持长期运行和更大历史量 | 分页加载、数据库索引、批量写库、保留策略 |

详细路线见 [Socket 通信模块优化路线](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md)。

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 编造的统一消息字段模型
- 与真实类不匹配的配置项列表
- 只有 handler 示例、没有管理窗口和持久化边界的介绍
- 把当前模块写成纯协议规范而不是实际 UI 通信模块

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [Socket 通信模块优化路线](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md)
