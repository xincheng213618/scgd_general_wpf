**ARVRPRO TCP 通讯协议手册**

**1. 概述**

本手册定义了客户端与 ARVRPRO 软件之间的 TCP Socket 通讯规范。通过该协议，外部系统（如自动化产线中控、PLC 控制机等）可以远程控制 ARVRPRO 软件执行初始化、切换测试流程组、确认画面切换并获取最终的测试结果。

- **通讯方式**：TCP 服务端（ARVRPRO 软件作为 Server，客户端作为 Client 主动连接）。
- **默认端口**：6666（可在软件的 Socket 管理页面中配置）。
- **数据格式**：UTF-8 编码的 JSON 字符串。
- **通讯模式**：支持“请求-响应”模式以及服务端的“主动推送”模式。



**2. 基础数据格式**

所有通讯的 JSON 数据均基于以下基础结构：

**2.1 客户端请求 (Request)**

JSON

{

 "Version": "1.0",

 "MsgID": "唯一消息ID（可选）",

 "EventName": "事件名称（必填）",

 "SerialNumber": "产品SN码（可选，用于绑定测试数据）",

 "Params": "附加参数（视具体指令而定，字符串格式）"

}

**2.2 服务端响应/推送 (Response)**

JSON

{

 "Version": "1.0",

 "MsgID": "对应请求的MsgID",

 "EventName": "事件名称",

 "SerialNumber": "产品SN码",

 "Code": 0,

 "Msg": "执行结果描述",

 "Data": { 

   // 附加数据对象（视具体指令而定）

 }

}

**说明**：Code 字段为状态码，0 表示成功，< 0 表示失败（如 -1, -2, -3 等）。



**3. 接口列表**

**3.1 客户端请求：初始化测试 (ProjectARVRInit)**

**说明**：通知软件准备开始针对某个 SN 的产品进行测试。

- **请求 EventName**：ProjectARVRInit
- **请求示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-001",

 "EventName": "ProjectARVRInit",

 "SerialNumber": "SN12345678"

}

- **响应示例**：初始化成功后，服务端会立即返回需要切换的第一个画面指令 (SwitchPG)。

JSON

{

 "Version": "1.0",

 "MsgID": "req-001",

 "EventName": "SwitchPG",

 "Code": 0,

 "SerialNumber": "SN12345678",

 "Data": {

  "ARVRTestType": 0

 }

}

**3.2 客户端请求：确认画面切换完成 (SwitchPGCompleted)**

**说明**：当客户端收到 SwitchPG 画面切换指令，并在屏幕上成功点亮指定画面后，必须发送此指令告知软件，软件随后将触发拍照与算法分析流程。

- **请求 EventName**：SwitchPGCompleted
- **请求示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-002",

 "EventName": "SwitchPGCompleted",

 "SerialNumber": "SN12345678"

}

- **响应**：无特定响应字符串，软件会立即在后台启动测试算法流程。

**3.3 客户端请求：一键运行全部流程 (RunAll)**

**说明**：触发软件自动执行当前启用的所有测试项。

- **请求 EventName**：RunAll
- **请求示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-003",

 "EventName": "RunAll",

 "SerialNumber": "SN12345678"

}

- **响应示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-003",

 "EventName": "RunAll",

 "Code": 0,

 "Msg": "RunAll started",

 "SerialNumber": "SN12345678"

}

**3.4 客户端请求：切换测试流程组 (SwitchGroup)**

**说明**：通知软件切换到指定的测试配置组（适用于不同机种切换）。

- **请求 EventName**：SwitchGroup
- **请求参数 (Params)**：目标配置组的名称
- **请求示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-004",

 "EventName": "SwitchGroup",

 "Params": "Model_A_Group"

}

- **响应示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-004",

 "EventName": "SwitchGroup",

 "Code": 0,

 "Msg": "Switched to Model_A_Group",

 "Data": {

  "GroupName": "Model_A_Group",

  "MetaCount": 5

 }

}

**3.5 客户端请求：查询流程启用状态 (GetProcessEnable)**

**说明**：查询当前流程组中所有流程项的启用状态。返回的 `Index` 与服务端推送 `SwitchPG.Data.ARVRTestType` 使用同一套序号约定，客户端可直接使用该序号进行后续设置。

- **请求 EventName**：GetProcessEnable
- **请求示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-005",

 "EventName": "GetProcessEnable"

}

- **响应示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-005",

 "EventName": "GetProcessEnable",

 "Code": 0,

 "Msg": "OK",

 "Data": {

  "ActiveGroupName": "Default",

  "Count": 16,

  "Items": [

   {

    "Index": 10,

    "Name": "White1",

    "FlowTemplate": "White1_Test",

    "ProcessTypeName": "PoiDynamicProcess",

    "IsEnabled": true

   }

  ]

 }

}

**3.6 客户端请求：设置流程启用状态 (SetProcessEnable)**

**说明**：按 `GetProcessEnable` 返回的 `Index` 批量设置流程项启用/禁用。设置成功后立即更新软件界面勾选状态，并保存到本地流程配置，后续 `ProjectARVRInit` / `SwitchPGCompleted` 按最新状态执行。

- **请求 EventName**：SetProcessEnable
- **请求参数 (Params)**：JSON 字符串，包含 `Items` 数组
- **请求示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-006",

 "EventName": "SetProcessEnable",

 "Params": "{\"Items\":[{\"Index\":10,\"IsEnabled\":true},{\"Index\":15,\"IsEnabled\":false}]}"

}

- **响应示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-006",

 "EventName": "SetProcessEnable",

 "Code": 0,

 "Msg": "OK",

 "Data": {

  "ActiveGroupName": "Default",

  "Applied": [

   {

    "Index": 10,

    "Name": "White1",

    "FlowTemplate": "White1_Test",

    "ProcessTypeName": "PoiDynamicProcess",

    "IsEnabled": true

   }

  ],

  "NotFound": []

 }

}


**3.7 客户端请求：确认AOI切图完成 (AOITestSwitchImageComplete)**

**说明**：在测试流程中，软件内部的算法流程（Flow Engine）可能需要通过中转服务器向外部客户端发送 AOI 切图指令 `AoiSwitchPG`（见 4.3），要求客户端切换 AOI 测试画面。当客户端完成 AOI 画面切换后，必须发送此指令通知软件继续执行后续流程。此过程可能在一轮测试中重复多次。

- **请求 EventName**：AOITestSwitchImageComplete
- **请求示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "req-002",

 "EventName": "AOITestSwitchImageComplete",

 "SerialNumber": "SN12345678"

}

- **响应**：无特定响应字符串。软件收到后会通过中转服务器将确认消息转发给内部 Flow Engine，Flow 继续执行下一步（可能是再次推送 `AoiSwitchPG` 或完成本轮测试）。

**备注**：此指令通过 Socket 中转层工作。软件内部架构为：

```
Flow Engine (Client) ←→ 中转Server (端口9200) ←→ ARVRPRO主程序 ←→ SocketManager (端口6666) ←→ 外部Client
```

当 Flow 需要切图时，发送 `AoiSwitchPG` → 中转Server → 写到外部Client连接 → 外部Client 切图完成后发送 `AOITestSwitchImageComplete` → SocketManager → 中转Server → Flow。



**4. 服务端主动推送指令**

在自动化测试流程中，服务端（ARVRPRO软件）会主动向客户端推送以下事件，客户端需要对其进行监听和处理。

**4.1 服务端推送：要求切换****��试画面 (SwitchPG)**

**说明**：当前测试项完成，或者刚初始化时，软件要求客户端切换显示器的测试画面（PG）。

- **推送内容示例**：

JSON

{

 "Version": "1.0",

 "EventName": "SwitchPG",

 "Code": 0,

 "Msg": "Switch PG",

 "SerialNumber": "SN12345678",

 "Data": {

  "ARVRTestType": 1

 }

}

- **客户端动作**：解析 Data.ARVRTestType 并控制硬件切换到对应的画面索引，切换成功后回复 SwitchPGCompleted 指令（见 3.2）。

**4.2 服务端推送：输出最终测试结果 (ProjectARVRResult)**

**说明**：所有测试流程完成，或发生严重错误/超时时，服务端会主动推送最终的测试结果。

- **推送内容示例**：

JSON

{

 "Version": "1.0",

 "EventName": "ProjectARVRResult",

 "Code": 0,        // 0 为正常完成， -1 为失败终止， -2 为测试超时终止

 "Msg": "ARVR Test Completed",

 "SerialNumber": "SN12345678",

 "Data": {

  "TotalResult": true,  // true表示PASS, false表示FAIL

  "TotalResultString": "PASS",

  "LuminanceChromaticityTestResults": {

   "White": { ... },

   "W25": { ... }

  },

  "FieldOfViewTestResults": {

   "White": { ... }

  },

  "ChessboardTestResults": {

   "Chessboard": { ... },

   "ChessboardFar": { ... }

  },

  "W255TestResult": { ... }, // 亮色度 Key=White 时的兼容字段

  "W51TestResult": { ... },  // 视场角 Key=White 时的兼容字段

  "ChessboardTestResult": { ... }, // 动态棋盘格 Key=Chessboard 时的兼容字段；同Key后写入结果覆盖前值

  "MTFHVTestResult": { ... },

  "DistortionTestResult": { ... }

  // ... 其他具体测试项的数据

 }

}

- **客户端动作**：接收并记录测试结果（PASS/FAIL），将产品分拣或流转到下一工位。

**4.3 服务端推送：要求切换AOI测试画面 (AoiSwitchPG)**

**说明**：在算法流程执行过程中，Flow Engine 需要外部客户端切换 AOI 相关的测试画面。此指令由 Flow Engine 通过中转服务器转发至外部客户端。在一轮测试中，该指令可能被推送多次（对应多张 AOI 测试图片）。

- **推送内容示例**：

JSON

{

 "Version": "1.0",

 "MsgID": "",

 "EventName": "AoiSwitchPG",

 "Code": 0,

 "Msg": "AoiSwitchPG",

 "SerialNumber": null,

 "Data": null

}

- **客户端动作**：控制硬件切换到下一张 AOI 测试画面，切换成功后回复 `AOITestSwitchImageComplete` 指令（见 3.7）。



**5. 常见交互时序示例 (时序图说明)**

**5.1 标准测试流程（不含AOI）**

1. **Client** 发送 ProjectARVRInit 启动测试并传递 SN。
2. **Server** 响应 SwitchPG，要求切换到第 0 项测试画面。
3. **Client** 切换硬件画面完毕，发送 SwitchPGCompleted 确认。
4. **Server** 进行拍照与算法分析（此过程可能会耗时几秒到十几秒）。
5. 算法分析完毕，**Server** 主动推送 SwitchPG 要求切换到下一个画面（例如测试类型 1）。
6. **Client** 再次发送 SwitchPGCompleted。
7. （重复该循环直至所有勾选的测试项目执行完毕）
8. 所有流程完毕后，**Server** 主动推送 ProjectARVRResult 包含各项详细指标和最终判级（PASS/FAIL）。

**5.2 包含AOI切图的测试流程**

```
Client                    Server (ARVRPRO)                 Flow Engine
  |                            |                               |
  |--- ProjectARVRInit ------->|                               |
  |<-- SwitchPG (type=0) ------|                               |
  |--- SwitchPGCompleted ----->|                               |
  |                            |--- 启动算法流程 ------------->|
  |                            |                               |
  |                            |    (Flow需要AOI切图)          |
  |<-- AoiSwitchPG -----------[中转]<-- AoiSwitchPG -----------|
  |                            |                               |
  |--- AOITestSwitchImageComplete -->|                         |
  |                            |---[中转]--> Complete -------->|
  |                            |                               |
  |<-- AoiSwitchPG -----------[中转]<-- AoiSwitchPG -----------|
  |--- AOITestSwitchImageComplete -->|                         |
  |                            |---[中转]--> Complete -------->|
  |                            |                               |
  |                    (重复N次AOI切图循环)                     |
  |                            |                               |
  |<-- ProjectARVRResult ------|<-- 测试完成 ------------------|  
```

**说明**：
- AOI 切图循环发生在每个测试项的算法执行期间，次数取决于 Flow 流程配置。
- `AoiSwitchPG` 和 `AOITestSwitchImageComplete` 通过中转服务器（默认端口 9200）中继。
- 客户端每收到一次 `AoiSwitchPG` 就需回复一次 `AOITestSwitchImageComplete`。
- 所有 AOI 切图完成后，Flow 继续执行后续算法，最终由 Server 推送 `ProjectARVRResult`。



---

**6. PG GECS 透传补充协议**

本节是 ARVRPRO TCP 通讯协议的补充说明，用于外部客户端通过 ARVRPRO 操作由 ARVRPRO 所在设备管理的 PG。外部客户端不直接连接 PG，也不需要提供 PG 的 IP、端口或 Network Number。

**6.1 客户端请求：透传 PG GECS 指令 (PGPassThroughGECS)**

**请求 EventName**：`PGPassThroughGECS`

**请求参数 (Params)**：GECS V2.4 协议中的 `Message Text`，不包含 STX、Network Number、Message Length 和 ETX。ARVRPRO 根据本地 PG 配置完成 GECS 数据帧封装、发送和回包接收。

请求示例：

```json
{
  "Version": "1.0",
  "MsgID": "pg-001",
  "EventName": "PGPassThroughGECS",
  "SerialNumber": "SN12345678",
  "Params": "PG,01,PATTERN,INDEX,3"
}
```

ARVRPRO 实际发送给 PG 的 GECS 数据帧结构如下：

```text
STX(0x02) + Network Number + Message Length(4字节HEX-ASCII) + Message Text + ETX(0x03)
```

其中 Network Number 由 ARVRPRO 本地配置决定，默认值为 `0xFF`。`[ch]` 是 PG 编号，范围为 `01`～`54`。

**6.2 服务端响应**

ARVRPRO 等待 PG 返回一个完整的 GECS 数据帧后，将其中的 `Message Text` 原样放入 `Data.PGResponse`。

响应示例：

```json
{
  "Version": "1.0",
  "MsgID": "pg-001",
  "EventName": "PGPassThroughGECS",
  "SerialNumber": "SN12345678",
  "Code": 0,
  "Msg": "PG GECS command completed",
  "Data": {
    "PGCommand": "PG,01,PATTERN,INDEX,3",
    "PGResponse": "PG,01,PATTERN,INDEX,3,END,OK"
  }
}
```

`Code = 0` 表示 ARVRPRO 已成功将指令发送至 PG 并收到格式正确的 GECS 回包。PG 的实际执行结果以 `Data.PGResponse` 为准，例如 `END,OK`、`END,NG`、`STATE,OK`、`STATE,NG` 或 `ERROR,...`。

连接失败、等待回包超时、参数错误或指令不在允许范围内时，`Code < 0`，具体错误原因通过 `Msg` 返回。

**6.3 当前支持的 GECS 指令**

| 功能 | Params 格式 | PG 回包格式 |
|------|-------------|-------------|
| 查询 PG 连接状态 | `PG,[ch],STATE` | `PG,[ch],STATE,[OK/NG]` |
| PG 开电 | `PG,[ch],POWER,ON` 或 `PG,[ch],POWER,ON,[sn]` | `PG,[ch],POWER,ON,...,END,[OK/NG]` |
| PG 关电 | `PG,[ch],POWER,OFF` | `PG,[ch],POWER,OFF,END,[OK/NG]` |
| 查询 PG 开关电状态 | `PG,[ch],POWER,STATE` | `PG,[ch],POWER,STATE,[ON/OFF]` |
| 查询 PG 电压电流数据 | `PG,[ch],POWER,MEASURE` | `PG,[ch],POWER,MEASURE,[电压数据],[电流数据]` |
| 切换到下一画面 | `PG,[ch],PATTERN,NEXT` | `PG,[ch],PATTERN,NEXT,END,[OK/NG]` |
| 切换到上一画面 | `PG,[ch],PATTERN,BACK` | `PG,[ch],PATTERN,BACK,END,[OK/NG]` |
| 按序号切换画面 | `PG,[ch],PATTERN,INDEX,[n]` | `PG,[ch],PATTERN,INDEX,[n],END,[OK/NG]` |
| 按 RGB 值切换画面 | `PG,[ch],PATTERN,RGB,[r,g,b]` | `PG,[ch],PATTERN,RGB,[r,g,b],END,[OK/NG]` |
| 按名称加载画面 | `PG,[ch],PATTERN,LOAD,[name]` | `PG,[ch],PATTERN,LOAD,[name],END,[OK/NG]` |

说明：

- `[ch]`、`[sn]`、`[n]`、`[r,g,b]` 和 `[name]` 表示变量，实际指令中不包含方括号。
- `POWER,ON` 所需的 SN 应直接写在 `Params` 中；ARVRPRO 不会自动将外层 `SerialNumber` 拼接到 GECS 指令。
- 当前只开放本节列出的指令。`SENDFILE`、`GAMMA`、`PREGAMMA`、`DEMURA`、`AOI`、`KEY`、`FUNCTION`、`DICID`、`EFUSE` 和 `OSC` 等其他 GECS 指令不会被转发。
- 同一 PG 连接上的指令按接收顺序串行执行。客户端应等待当前 `PGPassThroughGECS` 响应后再发送下一条 PG 指令。

**6.4 错误响应示例**

```json
{
  "Version": "1.0",
  "MsgID": "pg-002",
  "EventName": "PGPassThroughGECS",
  "SerialNumber": "SN12345678",
  "Code": -2,
  "Msg": "Unsupported PG GECS command",
  "Data": {
    "PGCommand": "PG,01,DEMURA,WRITE,START",
    "PGResponse": null
  }
}
```
