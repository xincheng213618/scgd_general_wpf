---
knowledge_id: "projects.lux-protocol"
knowledge_type: "reference"
status: "current"
summary: "LUX TCP 文本协议的 T0000 握手、VID、光学中心、光通量与 SocketCode 流程，说明响应字段、状态码、分帧及共享会话限制。"
aliases: ["LUX TCP 协议","ProjectLUX TCP 通讯协议手册","T0000","T0001","T0002","T0031","T00XX","H03AR","MachineNO","No SN","No right Code","No Dispatcher Hanle","FlowFailed","LUX 光通量","LUX VID"]
code_paths: ["Projects/ProjectLUX/Services/SocketControl.cs","Projects/ProjectLUX/LUXWindow.xaml.cs","Projects/ProjectLUX/Process/ProcessManager.cs","Projects/ProjectLUX/Process/OpticCenter/","Projects/ProjectLUX/Process/VID/","Projects/ProjectLUX/Summary.cs","Projects/ProjectLUX/ProjectLUXConfig.cs","UI/ColorVision.SocketProtocol/SocketManager.cs","UI/ColorVision.SocketProtocol/SocketTextDispatcher.cs","UI/ColorVision.SocketProtocol/SocketConfig.cs"]
test_paths: []
related: ["projects.lux","projects.capabilities","ui.socket-protocol"]
---

# LUX TCP 通讯协议

外部产线控制程序通过 TCP 文本命令触发 ProjectLUX 测试并接收响应。本页描述服务端接口、返回值和执行条件，项目包版本以 `ProjectLUX.csproj` 和生成的 `manifest.json` 为准；报文没有版本协商字段。

接入前确认当前活动流程组及其 `SocketCode` 映射。Flow、Recipe/Fix 和文件保存位置见 [ProjectLUX 配置](./project-lux.md)。

## 建立连接

| 参数 | 当前约定 |
| --- | --- |
| 传输层 | TCP，ProjectLUX/ColorVision 为服务端，外部系统为客户端 |
| 默认监听地址 | `0.0.0.0` |
| 默认端口 | `6666`，可在“通信协议”设置中修改 |
| 文本编码 | UTF-8，无 BOM |
| 协议模式 | `Text`；不能选择 `Json` |
| 连接方式 | 建议保持长连接，并严格按“发送一条、等待该条最终响应、再发送下一条”串行通信 |
| 加密与认证 | 当前协议不提供 TLS 或应用层身份认证，应在受控产线网络中使用 |

服务端需要在 ColorVision 的“通信协议”设置中启用 Socket Server，并将协议模式设置为 `Text`。服务默认关闭，默认协议为 Json，部署时必须显式检查。共享文本分派器只调用首个发现的 handler，不会按命令遍历所有项目；同时加载多个文本协议项目时，须核对实际由哪个 handler 接收，详见[通用 Socket 服务](../ui-components/ColorVision.SocketProtocol.md)。

保持一个控制连接，按产品和命令串行通信。普通 Flow 使用共享的 `LUXWindow.Stream` 与 `ReturnCode`，后来的流程命令会在忙检查之前覆盖它们；`T0000` 也会更新当前 SN 和 ReturnCode。VID/光通量回调则捕获发起请求的连接。不能把多连接或上一流程未结束时的握手解释为独立、隔离的测试会话。

## 报文格式

### 客户端请求

```text
T00XX,<SN>;
```

| 字段 | 长度 | 必填 | 说明 |
| --- | ---: | :---: | --- |
| `T00XX` | 5 个 ASCII 字符 | 是 | 命令码；`XX` 为两位命令编号 |
| `,` | 1 | 是 | 命令码与 SN 的分隔符 |
| `SN` | 可变 | 是 | 产品序列号；不得为空 |
| `;` | 1 | 是 | 请求结束符 |

推荐约束：

- 命令码使用大写 `T` 和两位十进制编号，例如 `T0000`、`T0021`。
- SN 不要包含逗号、分号、回车、换行或 Windows 文件名非法字符；它还用于结果文件名，当前协议没有转义或完整的文件名校验。
- 每次 TCP 写入只发送一条完整命令，不要粘连多条命令，也不要主动拆分一条命令。
- 单条命令应远小于服务端默认的 10,240 字节接收缓冲区。

服务端先去除整段请求两端空白及末尾分号，再按第一个逗号分开命令与 SN；命令只检查 `T00` 前缀并取最后两字符，没有严格验证五字符或两位数字。上表是客户端应遵守的格式，不能将宽松解析当成额外命令保证。

当前服务端把一次 TCP `Read` 得到的内容直接当作完整请求，不按分号拆包，也不缓存半包。串行发送小报文可避免主动叠加请求，但**一次 Write 不保证对应一次完整 Read**。这属于服务端分帧限制，需要在真实网络条件下联调。

客户端接收也须处理半包。普通握手以分号结尾，但 VID/光通量把数值放在分号之后且没有最终结束符；Flow 失败也会在基础响应后追加文本。不能看到第一个分号就认为所有类型的响应已经完整。当前可变长数值响应没有可靠的长度/终止标记，静默等待只是接收策略，不是完整性证明；截断或缺失数值不能当成有效测量。

### 通用响应

除专用测量命令外，通用响应格式为：

```text
<MachineNO><XX>,<SN>,00;
```

默认 `MachineNO` 为 `H03`。例如请求 `T0021,SN123456;` 的正常回包为：

```text
H0321,SN123456,00;
```

| 字段 | 说明 |
| --- | --- |
| `MachineNO` | ProjectLUX“Summary”设置中的设备号，默认 `H03` |
| `XX` | 原请求命令码的最后两位 |
| `SN` | 原请求携带的 SN |
| `00` | 现有协议的固定状态字段 |

`00` 是固定确认值，不表示 Recipe 判定 PASS，也不等于整机最终测试合格；活动组、命令映射或模板缺失时也可能返回这一前缀。普通流程的测试值及 PASS/FAIL 记录在结果界面、SQLite 和 `C_<SN>.csv` 中，当前 TCP 通用响应不携带这些数据。

## 命令列表

| 请求命令 | 名称 | 响应时机 | 响应内容 |
| --- | --- | --- | --- |
| `T0000,<SN>;` | 握手并设置当前 SN | 立即 | 通用响应 |
| `T0001,<SN>;` | VID 虚像距/自动对焦位置测量 | 设备异步完成后 | VID 专用响应 |
| `T0002,<SN>;` | 光学中心流程 | 流程处理完成后 | `H03AR` 模式使用光学中心专用响应；其他模式按普通流程响应 |
| `T0031,<SN>;` | 光通量测量 | 光谱仪异步完成后 | 光通量专用响应 |
| `T00XX,<SN>;` | 当前活动组中的配置流程 | Flow 及结果处理完成后 | 通用响应；`XX` 匹配 `ProcessMeta.SocketCode` |

`00`、`01` 和 `31` 为固定用途，不应再分配给普通流程。设备号为 `H03AR` 时，`02` 也应保留给光学中心。

### 握手：T0000

请求：

```text
T0000,SN123456;
```

默认设备号响应：

```text
H0300,SN123456,00;
```

AR 设备号响应：

```text
H03AR00,SN123456,00;
```

`T0000` 更新当前运行中的 SN 并立即确认，不调用 `LUXWindow.InitTest`，也不会清空上一轮累计的 `ObjectiveTestResult`。SN 标注 `[JsonIgnore]`，这不是保存到磁盘的产品切换动作。若现场要求每个 SN 开始时重置整轮累计结果，应在正式联调前明确初始化方式；不能通过重复握手获得这一语义。

命令解析在检查 `T00` 前缀前就可能写入非空 SN。因此有回包、非法命令回包或握手成功都不证明测试状态没有变化。

### VID 测量：T0001

请求：

```text
T0001,SN123456;
```

测量成功响应：

```text
H0301,SN123456,00;<VIDValue>
```

示例：

```text
H0301,SN123456,00;12.3456
```

测量失败响应：

```text
H0301,SN123456,00;0
```

说明：

- `VIDValue` 是相机 `GetPosition.VidPos` 或 `AutoFocus.VidPosition` 乘以 VID Fix 系数后的数值，使用首个发现的相机和该相机显示配置中的自动对焦模板。
- 该响应在数值前使用分号，数值后没有结束分号。
- 设备回报失败时数值为 `0`，但固定状态字段仍为 `00`，客户端应同时检查数值及现场业务规则。
- 回调先写 `<ResultSavePath>\B_<SN>.csv`，再发响应；本分支不创建目录，路径或写入失败可能阻止回包。非 Success 回调写入当前 VID 数据后返回 `0`，文件存在不代表测量成功。

### AR 光学中心：T0002

此专用响应仅在 `MachineNO` 精确配置为 `H03AR` 时启用。

请求：

```text
T0002,SN123456;
```

成功响应：

```text
H03AR02,<SN>,<Rotation>,<TiltX>,<TiltY>,00;
```

示例：

```text
H03AR02,SN123456,0.0123,-0.0456,0.0789,00;
```

| 数据字段 | 单位 | 说明 |
| --- | --- | --- |
| `Rotation` | degree | 光学中心旋转角 |
| `TiltX` | degree | 光学中心 X 倾斜角 |
| `TiltY` | degree | 光学中心 Y 倾斜角 |
| `00` | - | 固定状态字段，不代表三个测量值均满足 Recipe |

现场配置必须同时满足以下条件：

1. `MachineNO` 为 `H03AR`。
2. 当前活动流程组内存在 `SocketCode = 02` 的流程。
3. 该流程绑定光学中心处理器及有效的 FlowTemplate。
4. 光学中心流程位于当前活动组的第 1 项（内部索引 0）。

追加三个数值的判断实际是 `MachineNO == "H03AR"` 且结果 `TestType == 0`，并未再次检查命令码是否为 `02`。因此该站第 1 项应固定为光学中心，FlowTemplate 使用完整精确名称；不要把其他命令映射到第 1 项或在运行中调整活动组。光学中心结果缺失时可能只收到 `H03AR02,<SN>` 前缀。

非 `H03AR` 模式下，`T0002` 按普通可配置流程处理，正常响应为 `<MachineNO>02,<SN>,00;`。数值取结果的 `Value`，不使用显示用的四位小数 `TestValue`；协议不保证固定小数位。

### 光通量：T0031

请求：

```text
T0031,SN123456;
```

测量成功响应：

```text
H0331,SN123456,00;<LuminousFlux>
```

示例：

```text
H0331,SN123456,00;125.42
```

测量失败响应：

```text
H0331,SN123456,00;0
```

说明：

- `LuminousFlux` 单位为流明（lm）。
- 该响应在数值前使用分号，数值后没有结束分号。
- 设备回报失败时数值为 `0`，但固定状态字段仍为 `00`。
- 使用首个发现的光谱仪发起测量，按返回 MasterId 从 MySQL 读取结果；成功取得光谱结果时先导出 `<ResultSavePath>\D_<SN>.csv`，再发响应。
- Success 回调但取不到有效 `ViewResultSpectrum` 时，数值部分可能为空，例如 `H0331,SN123456,00;`，且不导出结果文件；这与失败分支返回 `0` 不同。
- 目录创建失败会记日志；数据库查询、导出或异步回调异常可能阻止预期回包，不能从测量请求已发送推定文件或响应一定产生。

### 配置流程：T00XX

除固定命令外，`XX` 由 ProjectLUX“ProcessManager”中当前活动流程组的 `SocketCode` 决定。

示例配置：

| 流程名称 | FlowTemplate | SocketCode | 对应请求 |
| --- | --- | --- | --- |
| White255 | `White255_Test` | `21` | `T0021,<SN>;` |
| Distortion | `Distortion_Test` | `22` | `T0022,<SN>;` |

请求：

```text
T0021,SN123456;
```

流程及结果处理成功完成后的响应：

```text
H0321,SN123456,00;
```

处理链路如下：

1. 在当前活动流程组中按忽略大小写的精确 SocketCode 查找第一条流程。
2. 查找第一个名称包含该 FlowTemplate 字符串的模板并启动；结果处理再按完整模板名精确匹配 Process。因此配置应使用完整且唯一的模板名，避免仅靠子串触发了错误或无法解析的流程。
3. Flow 完成后读取算法结果，应用 Fix 系数和 Recipe 限值。
4. 在结果目录存在时导出 `<ResultSavePath>\C_<SN>.csv`，随后保存本地结果和累计结果；目录不存在可能只记日志并继续，CSV 成功不是固定回包的前提。
5. 通过窗口当前保存的 Stream 发送 ReturnCode；只有未被后续命令覆盖时，才仍是原请求的连接与前缀。

注意：

- `SocketCode` 是运行时配置，不存在由测试类型自动推导的固定表；交付时必须随设备导出实际映射表。
- 相同活动组内不要配置重复的 `SocketCode`，重复时只匹配第一条。
- 当前直接 Socket 查找不检查 `IsEnabled`，即使流程在 UI 中被禁用，只要 `SocketCode` 匹配仍可能执行。
- 命令发出后没有单独的“已开始”ACK；最终响应要等 Flow 和结果处理结束后才返回。
- 客户端应保持连接并等待完整响应，再发下一条命令；不要在等待期间发送握手或新流程覆盖共享响应状态。

## 异常响应与无响应场景

### 文本错误

| 场景 | 当前响应 |
| --- | --- |
| 命令码不是 `T00...` | `No right Code <code>` |
| 未提供逗号后的非空 SN | `No SN` |
| 没有已发现的文本 handler | `No Dispatcher Hanle`，保留实现中的拼写 |
| 同步 Socket 处理发生未捕获异常 | 异常消息原文；异步设备/Flow 回调错误不保证走该包装 |

上述错误文本没有统一字段结构，也不保证以分号结尾。活动组、SocketCode 或 FlowTemplate 缺失时，窗口可能直接返回已有 ReturnCode，包含通用 `00` 或 `H03AR02,<SN>` 前缀，而没有专用失败标记。需要结合“未设置活动流程组”“未在组…找到 SocketCode”“未找到 FlowTemplate”等日志判断是否真正执行。

### Flow 失败

Flow 超时且重试耗尽，或 Flow 返回其他失败事件时，在当前 ReturnCode 非空的条件下追加：

```text
FlowFailed:<EventName>,<Params>;
```

示例：

```text
H0321,SN123456,00;FlowFailed:OverTime,Timeout;
```

`H03AR` 光学中心的基础前缀没有状态字段和末尾分号，其失败报文也会直接拼接 `FlowFailed:`，不能套用普通流程的固定列数。`Params` 来源于 Flow，可能包含逗号或其他文本，当前协议没有转义规则。客户端可先识别 `FlowFailed:` 标记，不要仅按固定逗号列数解析失败响应。

### 可能不回包的场景

以下场景可能只记录日志而不返回标准错误：

- 收到流程命令时已有 Flow 正在运行。
- Flow 预处理失败。
- Flow 完成后找不到对应批次。
- 找不到精确匹配的 Process、自定义解析失败或返回 `false`。
- VID/光通量设备未触发可用回调，或回调中的数据库/文件写入失败。
- 客户端在异步测量完成前断开连接。

客户端必须设置业务超时；超时后先查 ProjectLUX 日志、执行状态和结果记录，不要立即重发。当前忙检查只覆盖已运行的 Flow，不能代表启动前异步准备、结果处理、VID 或光通量都有统一互斥和请求幂等。

设备分支对每次 `MsgRecordStateChanged` 回调判断 Success/非 Success，未在这里限定单次最终回调或主动移除订阅。现场还应检查是否出现多次状态变化导致的重复响应；不能据此接口宣称“一个命令恰好一个最终回包”。

## 交互时序

### 握手

```text
Client                                  ProjectLUX
  |                                         |
  |------ T0000,SN123456; ----------------->|
  |<----- H0300,SN123456,00; ---------------|
  |                                         |
```

### 普通流程

```text
Client                                  ProjectLUX
  |                                         |
  |------ T0021,SN123456; ----------------->|
  |                                         |-- 查找 SocketCode=21
  |                                         |-- 执行 Flow
  |                                         |-- 解析、判定并保存结果
  |<----- H0321,SN123456,00; ---------------|
  |                                         |
```

### 专用设备测量

```text
Client                                  ProjectLUX                 Device
  |                                         |                        |
  |------ T0031,SN123456; ----------------->|                        |
  |                                         |------ GetData -------->|
  |                                         |<----- measurement -----|
  |<----- H0331,SN123456,00;125.42 ---------|
  |                                         |                        |
```

## 联调准备

联调前逐项确认：

- Socket Server 已启用，监听地址和端口与客户端配置一致。
- `SocketPhraseType` 已设置为 `Text`。
- Windows 防火墙允许 ColorVision 主程序监听所选网络配置文件。
- Summary 中的 `MachineNO` 已确定；默认站使用 `H03`，AR 光学中心专用站使用 `H03AR`。
- ProcessManager 已切换到正确的活动组。
- 活动组内每个现场命令都有唯一 `SocketCode` 和存在的 FlowTemplate。
- `00`、`01`、`31` 未被普通流程占用；`H03AR` 站的 `02` 仅用于光学中心。
- 相机、光谱仪、MQTT/Flow 和 MySQL 等对应服务在线。
- `ResultSavePath` 存在且可写。
- 客户端已实现异步等待、业务超时、断线重连和重复 SN 的幂等策略。

## PowerShell 握手示例

以下示例会连接服务、打开/复用 LUX 窗口并更改当前 SN。仅在已获授权且无测试运行的联调环境中执行；它只接收以分号结束的握手响应，不是 VID/光通量的通用解析器：

```powershell
$luxClient = [System.Net.Sockets.TcpClient]::new()
try {
    $luxClient.Connect('127.0.0.1', 6666)
    $luxStream = $luxClient.GetStream()
    $luxStream.ReadTimeout = 5000
    $luxRequest = [System.Text.Encoding]::UTF8.GetBytes('T0000,SN123456;')
    $luxStream.Write($luxRequest, 0, $luxRequest.Length)

    $luxBuffer = [byte[]]::new(10240)
    $luxResponse = [System.Text.StringBuilder]::new()
    do {
        $luxCount = $luxStream.Read($luxBuffer, 0, $luxBuffer.Length)
        if ($luxCount -eq 0) { throw 'Connection closed before the handshake completed.' }
        [void]$luxResponse.Append([System.Text.Encoding]::UTF8.GetString($luxBuffer, 0, $luxCount))
    } while (-not $luxResponse.ToString().EndsWith(';'))
    $luxResponse.ToString()
}
finally {
    $luxClient.Dispose()
}
```

握手错误文本可能没有分号，此示例会等待到 ReadTimeout 后报错。正式客户端还需总业务时限、长度限制和错误文本识别；各测量命令的接收方式按前述报文边界单独处理。

## 联调验收

当前没有登记服务端文本协议和真实设备回调的专门自动化覆盖。下表是需在授权环境执行的验收要求，不是已有测试通过记录；分包/粘包、多客户端、响应被覆盖、缺失结果、文件写入失败和重复回调还需分别验证。

| 编号 | 用例 | 预期 |
| --- | --- | --- |
| LUX-TCP-001 | Text 模式下发送 `T0000,SN001;` | 返回 `<MachineNO>00,SN001,00;` |
| LUX-TCP-002 | 发送空 SN：`T0000,;` | 返回 `No SN` |
| LUX-TCP-003 | 发送非法命令：`X0000,SN001;` | 返回 `No right Code X0000` |
| LUX-TCP-004 | 发送已配置的普通流程命令 | 对应 Flow 运行，完成后返回通用响应并生成/更新 `C_<SN>.csv` |
| LUX-TCP-005 | 发送 `T0001` | 返回 VID 数值或 `0`，生成 `B_<SN>.csv` |
| LUX-TCP-006 | 发送 `T0031` | 返回光通量数值或 `0`；成功取得结果时生成 `D_<SN>.csv` |
| LUX-TCP-007 | `H03AR` 站发送 `T0002` | 返回 Rotation、TiltX、TiltY 和固定状态字段 |
| LUX-TCP-008 | Flow 超时且重试耗尽 | 响应中包含 `FlowFailed:OverTime` |
| LUX-TCP-009 | 前一 Flow 运行中再次发送流程命令 | 验证客户端超时与防重入策略，服务端当前可能不回包 |

## 实现与兼容边界

协议分派及 VID/光通量回调位于 `Services/SocketControl.cs`，普通流程启动与回包位于 `LUXWindow.xaml.cs`，SocketCode 匹配位于 `Process/ProcessManager.cs`。设备号来自 `Summary.cs`，共享文本读取与分派由 `UI/ColorVision.SocketProtocol` 负责。

数值回包使用插值/默认数字格式，没有在这些路径固定 InvariantCulture 或小数位；示例以小数点为约定，变更运行区域设置时需核对实际输出。引入统一错误码、请求 ID、可靠分帧、BUSY 响应、TCP PASS/FAIL 或初始化重置语义时，应明确新旧接口版本与客户端迁移策略，不能把未来行为写成现有保证。
