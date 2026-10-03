---
knowledge_id: "projects.arvr-pro-protocol"
knowledge_type: "reference"
status: "current"
summary: "ARVRPro TCP/JSON 对接：初始化与 RunAll、流程启用设置、切图确认、AOI 切图节点、状态码和最终结果关联；说明分帧与并发会话限制。"
aliases: ["ARVR TCP 协议","ARVRPRO TCP 通讯协议手册","ProjectARVRInit","SwitchPG","SwitchPGCompleted","SwitchGroup","RunAll","GetProcessEnable","SetProcessEnable","ARVRTestType","Partial applied","No enabled ARVR flow","ARVR test is busy","ProjectARVRResult","AoiSwitchPG","AOITestSwitchImageComplete","外部切图","ExternalImageSwitchNode","UseLegacyARVROutput","SNlocked"]
code_paths: ["Projects/ProjectARVRPro/Services/SocketControl.cs","Projects/ProjectARVRPro/Services/RunAllSocket.cs","Projects/ProjectARVRPro/Services/SwitchGroupSocket.cs","Projects/ProjectARVRPro/Services/ProcessEnableSocket.cs","Projects/ProjectARVRPro/Flow/ExternalImageSwitchNode.cs","Projects/ProjectARVRPro/Services/ExternalImageSwitchService.cs","Projects/ProjectARVRPro/Services/AOITestSwitchImageCompleteHandler.cs","Projects/ProjectARVRPro/ARVRWindow.xaml.cs","Projects/ProjectARVRPro/ProjectARVRProConfig.cs","Projects/ProjectARVRPro/ObjectiveTestResult.cs","Projects/ProjectARVRPro/LegacyARVR/","UI/ColorVision.SocketProtocol/SocketManager.cs","UI/ColorVision.SocketProtocol/SocketJsonDispatcher.cs","UI/ColorVision.SocketProtocol/SocketConfig.cs"]
test_paths: ["Test/ProjectARVRPro.Tests/ExternalImageSwitchTests.cs"]
related: ["projects.arvr-pro","projects.arvr-pro-processes","projects.arvr-pro-demo","ui.socket-protocol"]
---

# ARVRPro TCP 通讯协议

外部产线控制程序通过 TCP/JSON 初始化 ARVRPro 测试、切换流程组、确认画面切换并接收测试结果。本页说明服务端报文和执行条件；可复用的客户端、离线样例及其限制见 [Integration Demo](./project-arvr-pro-integration-demo.md)。接入前记录实际项目包版本，不能只凭报文中的 `Version` 判断兼容性。

## 建立连接

1. 在 ColorVision 中加载 `ProjectARVRPro`，确认[活动流程组、Flow 模板、解析器和 Recipe](./project-arvr-pro-processes.md)已配置，所需设备与 Flow 服务可用。
2. 在“通信协议”设置中启用 Socket Server，选择 **Json**。默认监听 `0.0.0.0:6666`，默认接收缓冲区为 10,240 字节；服务默认未启用。
3. 客户端连接现场配置的地址和端口，以 UTF-8 发送 JSON。端口、防火墙和协议模式不一致时，先排查[通用 Socket 服务](../ui-components/ColorVision.SocketProtocol.md)。
4. 保持一个控制连接，串行完成当前产品的测试，再开始下一个 SN。服务没有按连接隔离 ARVR 测试会话；这些 handler 会把当前连接写入共享的 `SocketControl.Current.Stream`，后续切图和结果使用该流发送。另一个客户端即使只查询启用状态，也可能改变推送目标。

### TCP 消息边界

主服务把每次 `NetworkStream.Read` 得到的内容直接反序列化为一条 JSON 请求，没有长度头、分隔符或半包缓存。客户端应串行发送小报文，避免主动拆分或拼接请求；**一次 TCP Write 仍不能保证服务端恰好一次 Read 完整收到**，这是当前接收实现的限制。

服务端发送的 JSON 也没有额外结束符。客户端接收必须缓存半包，并从连续字节流中提取完整 JSON 对象，不能把每次 Read 当成一条最终结果。Demo 提供这种客户端读取示例，但不会改变服务端的接收限制。联调应覆盖真实网络条件下的分包、粘包和断线；不能仅用本机一次往返证明可靠性。

## 报文字段

请求示例：

```json
{
  "Version": "1.0",
  "MsgID": "req-001",
  "EventName": "ProjectARVRInit",
  "SerialNumber": "SN12345678",
  "Params": null
}
```

| 字段 | 请求与响应约定 |
| --- | --- |
| `EventName` | 请求必填，按大小写精确匹配 handler；响应可能换成下一动作，例如初始化返回 `SwitchPG` |
| `MsgID` | 客户端可提供请求标识；直接响应通常回显，普通切图和最终结果推送使用空字符串，外部切图节点生成唯一请求 ID。它不是所有服务端事件的统一去重键 |
| `Version` | 客户端建议填 `1.0`；当前未做版本协商/校验，部分直接响应未赋值而返回 `null`，主动推送一般为 `1.0` |
| `SerialNumber` | 初始化或 RunAll 建立产品 SN；后续确认不负责切换 SN。最终报文携带当前 SN，客户端需核对归属 |
| `Params` | 字符串或 `null`。需要对象参数时，将 JSON 序列化后放入这个字符串，不能直接传嵌套对象 |
| `Code` / `Msg` | 响应状态和说明；按具体事件解释，不以正负号统一判断成功 |
| `Data` | 响应的附加对象；没有附加数据时可为 `null` |

下文示例只展示与动作有关的字段。未设置的引用字段可能序列化为 `null`，客户端不应把它们一律视为必填。

## 选择执行方式

| 方式 | 交互流程 | 适用条件 |
| --- | --- | --- |
| 外部逐步切图 | `ProjectARVRInit` → `SwitchPG` → 外部切图 → `SwitchPGCompleted`；重复到 `ProjectARVRResult` | 外部控制程序掌握每一步普通 PG 画面切换 |
| 一键运行 | `RunAll` → 开始确认 → 等待 `ProjectARVRResult` | 活动组及每步内部切图、设备和预处理已配置好 |

RunAll 自行初始化本轮会话，不需要先发 `ProjectARVRInit`，执行过程中也不等待普通 `SwitchPGCompleted`。AOI Flow 内部的外部切图节点可以出现在任一种方式中，见[AOI 切图](#aoi-切图)。

### 初始化：ProjectARVRInit

发送上面的初始化请求。存在启用步骤时，服务端打开/复用项目窗口，清空本轮累计结果与执行位置，尝试使用去除首尾空白后的 SN；SN 为空则自动生成。`SNlocked` 开启时配置保留原有 SN，客户端应核对服务端确认值，不假定请求 SN 已被采用。成功直接返回第一条启用步骤的切图请求：

```json
{
  "MsgID": "req-001",
  "EventName": "SwitchPG",
  "Code": 0,
  "SerialNumber": "SN12345678",
  "Data": { "ARVRTestType": 0 }
}
```

`ARVRTestType` 是活动组中步骤的**外部索引**，不保证从 0 开始，也不是固定测试类型枚举。首条启用步骤可能位于组中间；Legacy 输出还会使索引加 1，具体见[查询与设置启用状态](#查询与设置启用状态)。

没有启用步骤时，返回 `EventName=ProjectARVRInit`、`Code=-2`、`Msg="No enabled ARVR flow"`，不执行本轮初始化。结果输出等待后台空间清理或保存报表期间，初始化返回 `Code=-4`、`Msg="ARVR test is busy"`，保留原会话。其它执行阶段的初始化接口仍没有 RunAll 的完整忙检查；客户端必须在上一轮结束后才调用，不能用重复初始化探测设备是否忙。

### 普通切图：SwitchPG 与 SwitchPGCompleted

收到 `SwitchPG` 后，客户端按 `Data.ARVRTestType` 找到配置对应的画面，实际切图完成后发送：

```json
{
  "Version": "1.0",
  "MsgID": "req-002",
  "EventName": "SwitchPGCompleted",
  "SerialNumber": "SN12345678"
}
```

项目窗口存在时不返回单独 ACK。服务端从当前执行位置之后查找下一条启用步骤，按配置切图/预处理并启动 Flow，处理结果后推送下一条 `SwitchPG` 或最终 `ProjectARVRResult`。后续 `SwitchPG` 的 `MsgID` 为空，`Msg` 一般为 `Switch PG`，允许失败后继续时可能带上一流程失败说明。

确认请求没有用于定位步骤的索引校验，也不核验请求 SN 是否等于当前会话 SN。执行中重复确认通常只记录日志并忽略；延迟到下一空闲阶段的重复确认可能推进下一步，因此客户端必须管理自己的确认状态。项目窗口不存在时返回 `Code=-3`、`Msg="ProjectARVR Wont Open"`。

### 一键运行：RunAll

```json
{
  "Version": "1.0",
  "MsgID": "req-runall",
  "EventName": "RunAll",
  "SerialNumber": "SN12345678"
}
```

接受后返回 `EventName=RunAll`、相同 `MsgID`、解析后的 SN、`Code=0`、`Msg="RunAll started"`。窗口处于切图、流程启动、执行、结果处理或 RunAll 阶段时，返回 `Code=-4`、`Msg="ARVR test is busy"`。

**开始确认不等于测试完成或 PASS。** 客户端应从建连起持续处理报文并等待最终事件，不依赖开始确认与异步推送的固定先后顺序。组中没有启用步骤也可能先被接受，然后产生失败的最终结果。

RunAll 启动时取当前启用步骤列表，依次运行。`AllowTestFailures` 决定切图、预处理、执行或解析失败后是否继续；最终保留首次流程失败。测试中不要切换组或修改启用状态，管理接口并未统一拒绝这些操作。

## 切换流程组：SwitchGroup

```json
{
  "Version": "1.0",
  "MsgID": "req-group",
  "EventName": "SwitchGroup",
  "Params": "Model_A_Group"
}
```

组名按忽略大小写匹配，取第一条匹配组；不要配置重名组，也不要在名称两端添加额外空格。成功响应：

```json
{
  "MsgID": "req-group",
  "EventName": "SwitchGroup",
  "Code": 0,
  "Msg": "Switched to Model_A_Group",
  "Data": { "GroupName": "Model_A_Group", "MetaCount": 5 }
}
```

`MetaCount` 是该组全部步骤数，包括禁用步骤，不是本轮已执行数量。空名称返回 `-1 / GroupName is empty`；未找到返回 `-2 / Group not found: <名称>`；处理异常返回 `-99` 和异常消息。切换后重新查询索引，再开始新产品测试。

## 查询与设置启用状态

### GetProcessEnable

```json
{ "Version": "1.0", "MsgID": "req-get", "EventName": "GetProcessEnable" }
```

成功返回 `Code=0`、`Msg="OK"`，`Data` 包含：

| 字段 | 含义 |
| --- | --- |
| `ActiveGroupName` | 当前活动组名 |
| `Count` | 活动组全部步骤数 |
| `Items[]` | 每条步骤的 `Index`、`Name`、`FlowTemplate`、`ProcessTypeName` 和 `IsEnabled` |

外部索引取决于 `ViewResultManager.Config.UseLegacyARVROutput`：关闭时为组内零起始位置，开启时为组内位置加 1。禁用步骤仍占据位置；`SwitchPG.Data.ARVRTestType`、`GetProcessEnable.Items[].Index` 和 `SetProcessEnable` 使用同一约定。组排序、增删步骤或更换输出模式后要重新查询，不能长期把索引硬编码为某个画面。

### SetProcessEnable

推荐将 `Items` 对象序列化为 `Params` 字符串：

```json
{
  "Version": "1.0",
  "MsgID": "req-set",
  "EventName": "SetProcessEnable",
  "Params": "{\"Items\":[{\"Index\":0,\"IsEnabled\":true},{\"Index\":999,\"IsEnabled\":false}]}"
}
```

假设标准索引模式中存在步骤 0、不存在步骤 999，响应中的相关字段为：

```json
{
  "Version": "1.0",
  "MsgID": "req-set",
  "EventName": "SetProcessEnable",
  "Code": 1,
  "Msg": "Partial applied",
  "Data": {
    "ActiveGroupName": "Default",
    "Applied": [
      { "Index": 0, "Name": "White1", "FlowTemplate": "White1_Test", "ProcessTypeName": "PoiDynamicProcess", "IsEnabled": true }
    ],
    "NotFound": [999]
  }
}
```

- 全部索引存在时 `Code=0 / OK`；有任一不存在时 `Code=1 / Partial applied`。有效项已经生效，不会因另一项不存在而回滚。
- 请求先解析整批，再按顺序设置。重复索引会重复应用，最终状态取最后一项；`Applied` 会保留每次应用记录。
- 兼容参数形态包括根数组、单个条目对象，以及用 `Enabled` 代替 `IsEnabled`；两字段同时提供时优先 `IsEnabled`。新客户端统一使用上述 `Items` 形态。
- 缺少参数、空数组、缺少 `Index` / `IsEnabled` 等返回 `Code=-1` 和具体消息；类型转换等未被解析器归为参数错误的异常会进入 `-99` 分支。
- 应用后调用流程配置保存。保存方法可能只记录磁盘错误，成功响应因此不能证明重启后一定保留；需要持久化时同时核对[配置保存与恢复](./project-arvr-pro-processes.md)。

## AOI 切图

AOI 使用本地 Flow 的「外部切图」节点（右键添加节点 → ProjectARVRPro → 外部切图），复用已经由 `ProjectARVRInit` / `RunAll` 等建立的项目控制连接。无需另建 TCP 监听或传感器连接；外部程序仍须连接主 Socket。

```text
外部切图节点 -- AoiSwitchPG --> 外部控制程序
外部切图节点 <-- AOITestSwitchImageComplete -- 外部控制程序
```

节点发送以下报文，其中每次请求的 `MsgID` 都不同，`SerialNumber` 是当前产品 SN，而不是 Flow 内部的测量批次编号：

```json
{ "Version": "1.0", "MsgID": "aoi-request-001", "EventName": "AoiSwitchPG", "SerialNumber": "SN12345678", "Code": 0, "Msg": "AoiSwitchPG" }
```

外部程序实际切图完成后，使用同一连接回复：

```json
{ "Version": "1.0", "MsgID": "aoi-request-001", "EventName": "AOITestSwitchImageComplete", "SerialNumber": "SN12345678" }
```

完成事件不返回单独 ACK。节点收到有效确认并等待配置的稳定延时后才继续下游；写入 Socket 成功不等于切图完成。节点不改变上游图像或测量结果。

| 节点属性 | 行为 |
| --- | --- |
| 等待超时 `TimeoutMs` | 默认 5000 ms，必须大于零；覆盖发送请求和等待确认。到期失败，不自动重发 |
| 完成后延时 `DelayMs` | 默认 0 ms，不得为负；从收到确认后开始，等待画面稳定，不计入确认超时 |
| 严格匹配 `RequireMatchingMsgId` | 默认关闭以兼容旧客户端自建确认 ID；开启后必须回显请求的 MsgID |

全项目一次只允许一个切图节点执行，包含完成后延时；并行请求直接失败，不排队或覆盖前一个等待。确认必须来自本次发送使用的、仍处于活动状态的连接；报文携带非空 SN 且节点产品 SN 非空时还必须一致。没有等待、已经确认、错误连接或严格模式下 ID 不匹配的确认被忽略，确认事件本身不会切换项目的控制连接。

**旧客户端兼容模式不能区分同一连接上迟到的上一次确认与当前请求。** 客户端必须每次切图只确认一次；需要排除这种误确认时，开启严格匹配并让客户端回显 MsgID。现有 IntegrationDemo / SemiAuto 自动确认自行生成 MsgID，使用它们时保持兼容模式，或先适配客户端的回显行为。

流程停止会取消确认等待及完成后延时。主 Socket 检测断线并关闭流后，节点结束等待；其他客户端覆盖活动控制连接也会失败。主 Socket 的半包/粘包限制仍见本页「TCP 消息边界」，新增节点不改变其接收分帧实现。

迁移旧模板时，将专用于中转的通用传感器切图节点替换为「外部切图」，保留上下游连线并核对原来的超时和成功后延时。旧服务端 Flow 必须改用宿主进程中的 Flow 执行，不能在旧服务里直接运行这个项目节点。确认没有其它指令引用后，才清理该专用传感器连接配置。项目不再提供中转监听，也不再在启动时关闭并重开通用传感器；旧模板不会被自动改写。

## 最终结果：ProjectARVRResult

完成或终止本轮测试时，服务端尝试向当前控制连接推送 `ProjectARVRResult`。下面是标准模式的**结果摘要**，完整 `Data` 还包含各测试项：

```json
{
  "Version": "1.0",
  "MsgID": "",
  "EventName": "ProjectARVRResult",
  "SerialNumber": "SN12345678",
  "Code": 0,
  "Msg": "ARVR Test Completed",
  "Data": { "TotalResult": true, "TotalResultString": "PASS" }
}
```

| 条件 | 含义与处理 |
| --- | --- |
| `Code=0` | 未记录流程执行/解析失败；仍需检查最终判定。Recipe 不合格可以是 `Code=0`、`TotalResult=false` |
| `Code=-1` | 流程、启动、预处理、解析等失败的常用码，查看 Msg |
| `Code=-2` | 记录的流程超时失败；若此前已有另一失败，最终 Code/Msg 保留首次失败 |
| `MsgID=""` | 最终结果不回显原始请求 MsgID；按当前连接会话与确认的 SN 关联 |
| 断线或发送失败 | 界面/本地记录可能已经产生，客户端收不到并不代表没有测试；没有重连后自动补发的保证 |

`UseLegacyARVROutput=false` 时，Data 为 `ObjectiveTestResult`。亮色度、视场角和棋盘格等键化集合分别位于 `LuminanceChromaticityTestResults`、`FieldOfViewTestResults`、`ChessboardTestResults`；Key 来自流程配置，应枚举实际值。对应默认 Key 还可能写入 `W255TestResult`、`W51TestResult`、`ChessboardTestResult` 兼容字段。同一集合的同 Key 后写覆盖前值，独立测试项应使用可区分的 Key。

`UseLegacyARVROutput=true` 时，Data 整体换为扁平 `LegacyARVRObjectiveTestResult`，不能用标准嵌套样例直接解释。完整字段以 `ObjectiveTestResult.cs`、`LegacyARVR/LegacyARVRObjectiveTestResult.cs` 和对应处理器为准；客户端字段展开与样例见 [Demo 的公开代码边界](./project-arvr-pro-integration-demo.md#公开代码边界)。

客户程序应在所选 schema 中验证必需字段、预期 SN 和明确的最终判定后，再执行产线放行动作。当前 Demo 对缺失 `TotalResult` 与最终 SN 的校验有限，其正常退出不代表上述校验全部完成。

## 错误与无响应排查

| 现象 | 首查内容 |
| --- | --- |
| 返回 `400 / Invalid request` | 请求是否为空或缺少 EventName；这是 JSON 响应中的 Code，不是 HTTP 状态 |
| 返回 `404 / Handler not found for event: ...` | EventName 大小写、Json 模式、项目 handler 是否已加载 |
| 返回 `-1` 和 JSON/类型异常 | 报文是否完整，Params 是否误传成对象，是否出现半包或粘包 |
| 初始化 `-2 / No enabled ARVR flow` | 活动组是否存在启用步骤 |
| RunAll `-4 / ARVR test is busy` | 上一轮切图、Flow 或结果处理是否仍在执行，不要用初始化绕过忙状态 |
| 设置 `1 / Partial applied` | 对照 Applied/NotFound，重新查询活动组和索引；不能当成全量成功 |
| 确认后没有独立回包 | 两个切图完成事件成功路径本来不发 ACK，等待后续切图/结果并查执行日志 |
| 结果到了另一个连接 | 是否有其他客户端发送了 ARVR 请求，覆盖共享控制流 |
| AOI 卡住 | 模板是否使用外部切图节点、主 Socket 活动连接、完成事件及 SN/MsgID 是否匹配、节点超时和稳定延时 |

客户端需要业务超时和断线处理。超时后先确认当前项目执行状态与结果记录，再决定是否重试；服务端没有用 MsgID 保证幂等，重复命令可能再次初始化、改配置或推进流程。

## 实现与验证边界

初始化/确认看 `Services/SocketControl.cs`，RunAll 看 `Services/RunAllSocket.cs`，组切换和启用状态看 `SwitchGroupSocket.cs` / `ProcessEnableSocket.cs`，主动推送看 `ARVRWindow.xaml.cs`，AOI 节点看 `Flow/ExternalImageSwitchNode.cs`，发送等待看 `Services/ExternalImageSwitchService.cs`，完成确认看 `Services/AOITestSwitchImageCompleteHandler.cs`。共享 Socket 的分派、读取与错误包装由 `UI/ColorVision.SocketProtocol` 负责。

`ExternalImageSwitchTests` 验证切图请求/确认、旧协议兼容、严格关联、并发拒绝、超时不重发、停止取消、断开/切换连接、节点接线与保存恢复；不替代现场控制程序与设备验收。联调应覆盖标准/Legacy 索引、空组、忙状态、部分应用、普通切图和 AOI 确认、Code 为 0 但判定失败、最终 SN、半包/粘包、多连接及断线。会推进真实测试或修改配置的命令，应在获得对应现场操作授权后运行；文档和检索校验不替代这些验收。
