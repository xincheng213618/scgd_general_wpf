# ARVRPRO TCP 通讯协议手册

本文提供给 ARVRPRO 上位机或产线控制程序的开发人员，说明 ProjectARVRPro 当前对外使用的 TCP/JSON 通讯协议。

编写依据为 `ProjectARVRPro 1.1.8.28` 当前源码。现场接入前仍需核对实际安装的项目包版本。

## 1. 接入条件

1. ColorVision 已加载 `ProjectARVRPro`，并已配置流程组、Flow 模板、解析器和 Recipe。
2. 在 ColorVision“通信协议”设置中启用 Socket Server，协议模式选择 **Json**。
3. 默认监听地址为 `0.0.0.0:6666`；实际 IP 和端口以现场配置为准。
4. 客户端使用 UTF-8 编码发送 JSON。
5. 建议一个控制连接串行完成一台产品的整轮测试，再开始下一个 SN。

## 2. TCP 消息边界

当前服务没有长度头、换行分隔符或半包重组协议。服务端会把一次 TCP 读取到的内容直接当作一条 JSON 请求解析。

- 每个请求应单独、串行发送，并尽量保持报文较小。
- 不要把多条 JSON 拼接后一次发送。
- 一次客户端发送不能绝对保证服务端一次读取到完整报文，真实网络联调必须覆盖半包、粘包和断线场景。
- 服务端响应也没有额外结束符。客户端应缓存收到的字节，并按完整 JSON 对象解析，不能把每次读取都直接当作一条完整响应。

## 3. 通用报文格式

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

| 字段 | 说明 |
| --- | --- |
| `Version` | 建议填写 `1.0`。当前不进行版本协商；部分响应中可能为 `null` |
| `MsgID` | 客户端请求标识。直接响应通常原样返回；异步推送通常为空字符串 |
| `EventName` | 请求事件名，必填并区分大小写 |
| `SerialNumber` | 产品 SN，主要用于初始化或 RunAll |
| `Params` | 字符串或 `null`。需要传对象时，应先将对象序列化为 JSON 字符串 |
| `Code` | 响应状态码，应结合具体事件判断 |
| `Msg` | 响应说明或错误信息 |
| `Data` | 响应数据对象；无数据时可能为 `null` |

## 4. 事件总览

| 方向 | EventName | 用途 | 是否直接响应 |
| --- | --- | --- | --- |
| 客户端 -> ColorVision | `SwitchGroup` | 切换当前流程组 | 是 |
| 客户端 -> ColorVision | `GetProcessEnable` | 查询当前组的流程及启用状态 | 是 |
| 客户端 -> ColorVision | `SetProcessEnable` | 修改当前组的流程启用状态 | 是 |
| 客户端 -> ColorVision | `ProjectARVRInit` | 初始化逐步切图测试 | 返回第一条 `SwitchPG`，或错误响应 |
| 客户端 -> ColorVision | `SwitchPGCompleted` | 通知普通 PG 画面已切换完成 | 成功时无单独 ACK |
| 客户端 -> ColorVision | `RunAll` | 触发当前组一键运行 | 先返回开始确认，结束后再推送结果 |
| 客户端 -> ColorVision | `AOITestSwitchImageComplete` | 通知 AOI 画面已切换完成 | 成功时无单独 ACK |
| ColorVision -> 客户端 | `SwitchPG` | 请求外部控制程序切换普通 PG 画面 | 客户端完成后回复 `SwitchPGCompleted` |
| ColorVision -> 客户端 | `AoiSwitchPG` | AOI Relay 请求外部控制程序切图 | 客户端完成后回复 `AOITestSwitchImageComplete` |
| ColorVision -> 客户端 | `ProjectARVRResult` | 推送整轮测试最终结果 | 最终消息 |

## 5. 切换当前流程组：SwitchGroup

`Params` 填写 ColorVision 界面“当前组”下拉框中的组名。例如切换到 `MD138`：

```json
{
  "Version": "1.0",
  "MsgID": "req-group-001",
  "EventName": "SwitchGroup",
  "Params": "MD138"
}
```

成功响应：

```json
{
  "MsgID": "req-group-001",
  "EventName": "SwitchGroup",
  "Code": 0,
  "Msg": "Switched to MD138",
  "Data": {
    "GroupName": "MD138",
    "MetaCount": 5
  }
}
```

- 组名匹配不区分大小写，但前后不能多空格。
- 成功后 ColorVision 界面的“当前组”会立即切换，并尝试保存为当前活动组；`Code=0` 本身不证明磁盘保存一定成功。
- `MetaCount` 是该组的流程总数，包含已禁用流程。
- `Code=-1` 表示组名为空。
- `Code=-2` 表示找不到指定组。
- `Code=-99` 表示处理异常。
- 不要在测试执行过程中切换流程组。

切换成功后，建议先调用 `GetProcessEnable` 确认活动组及流程索引，再开始测试。

## 6. 查询流程启用状态：GetProcessEnable

请求：

```json
{
  "Version": "1.0",
  "MsgID": "req-get-001",
  "EventName": "GetProcessEnable"
}
```

响应示例：

```json
{
  "Version": "1.0",
  "MsgID": "req-get-001",
  "EventName": "GetProcessEnable",
  "Code": 0,
  "Msg": "OK",
  "Data": {
    "ActiveGroupName": "MD138",
    "Count": 2,
    "Items": [
      {
        "Index": 0,
        "Name": "MTF",
        "FlowTemplate": "MD138_MTF",
        "ProcessTypeName": "MTFProcess",
        "IsEnabled": true
      },
      {
        "Index": 1,
        "Name": "Chessboard",
        "FlowTemplate": "MD138_Chessboard",
        "ProcessTypeName": "ChessboardProcess",
        "IsEnabled": false
      }
    ]
  }
}
```

| Data 字段 | 说明 |
| --- | --- |
| `ActiveGroupName` | 当前活动组名 |
| `Count` | 当前组的流程总数 |
| `Items[].Index` | 外部通讯使用的流程索引 |
| `Items[].Name` | 流程显示名称 |
| `Items[].FlowTemplate` | 使用的 Flow 模板 |
| `Items[].ProcessTypeName` | 流程处理类型 |
| `Items[].IsEnabled` | 是否参与测试 |

索引规则受 `UseLegacyARVROutput` 配置影响：

- 标准输出模式：索引从 `0` 开始。
- Legacy 输出模式：索引为组内位置加 `1`。

禁用流程仍占据索引位置。流程组排序、增删流程或切换输出模式后，应重新查询，不要长期硬编码索引。

## 7. 设置流程启用状态：SetProcessEnable

`Params` 必须是字符串。推荐先把 `Items` 对象序列化，再放入 `Params`：

```json
{
  "Version": "1.0",
  "MsgID": "req-set-001",
  "EventName": "SetProcessEnable",
  "Params": "{\"Items\":[{\"Index\":0,\"IsEnabled\":true},{\"Index\":1,\"IsEnabled\":false}]}"
}
```

全部应用成功时：

```json
{
  "Version": "1.0",
  "MsgID": "req-set-001",
  "EventName": "SetProcessEnable",
  "Code": 0,
  "Msg": "OK",
  "Data": {
    "ActiveGroupName": "MD138",
    "Applied": [
      { "Index": 0, "Name": "MTF", "FlowTemplate": "MD138_MTF", "ProcessTypeName": "MTFProcess", "IsEnabled": true },
      { "Index": 1, "Name": "Chessboard", "FlowTemplate": "MD138_Chessboard", "ProcessTypeName": "ChessboardProcess", "IsEnabled": false }
    ],
    "NotFound": []
  }
}
```

- 全部索引存在时返回 `Code=0 / OK`。
- 有索引不存在时返回 `Code=1 / Partial applied`；有效项仍然已经生效，不会整批回滚。
- 缺少参数、空数组或缺少必要字段时返回 `Code=-1`。
- 兼容根数组、单个条目对象，以及使用 `Enabled` 代替 `IsEnabled`；新客户端建议统一使用上面的 `Items` 格式。
- 重复索引按请求顺序重复应用，最终值以最后一项为准。
- 修改后会尝试保存流程配置。需要确认重启后仍保留时，应在现场同时验证配置文件保存成功。

## 8. 执行方式一：外部逐步切图

交互流程：

```text
ProjectARVRInit
    -> SwitchPG
    -> 外部切图
    -> SwitchPGCompleted
    -> 下一条 SwitchPG（重复）
    -> ProjectARVRResult
```

### 8.1 初始化：ProjectARVRInit

```json
{
  "Version": "1.0",
  "MsgID": "req-init-001",
  "EventName": "ProjectARVRInit",
  "SerialNumber": "SN12345678",
  "Params": null
}
```

存在启用流程时，ColorVision 打开或复用项目窗口、初始化本轮测试，并返回第一条切图请求：

```json
{
  "MsgID": "req-init-001",
  "EventName": "SwitchPG",
  "Code": 0,
  "SerialNumber": "SN12345678",
  "Data": {
    "ARVRTestType": 0
  }
}
```

`Data.ARVRTestType` 与 `GetProcessEnable.Items[].Index` 使用相同索引规则。它是当前活动组中的外部索引，不是固定测试类型枚举。

没有启用流程时返回：

```json
{
  "MsgID": "req-init-001",
  "EventName": "ProjectARVRInit",
  "Code": -2,
  "Msg": "No enabled ARVR flow",
  "SerialNumber": "SN12345678"
}
```

SN 为空时软件可能自动生成；启用 `SNlocked` 时也可能保留软件中的原 SN。客户端应核对响应和最终结果中的 `SerialNumber`，不能假定请求值一定被采用。

初始化接口没有 RunAll 的忙状态保护。客户端必须等待上一轮结束后再初始化，不能用重复初始化探测是否空闲。

### 8.2 切图完成：SwitchPGCompleted

收到 `SwitchPG` 后，外部程序按 `ARVRTestType` 完成真实画面切换，再发送：

```json
{
  "Version": "1.0",
  "MsgID": "req-pg-001",
  "EventName": "SwitchPGCompleted",
  "SerialNumber": "SN12345678"
}
```

成功时没有单独 ACK。ColorVision 会执行对应流程，然后推送下一条 `SwitchPG` 或最终 `ProjectARVRResult`。

- 后续 `SwitchPG` 的 `MsgID` 通常为空字符串。
- 请求中的 SN 和索引不用于校验当前执行位置。
- 重复或延迟的确认可能错误推进流程，客户端必须维护自己的切图确认状态。
- 项目窗口不存在时返回 `Code=-3 / ProjectARVR Wont Open`。

## 9. 执行方式二：一键运行 RunAll

RunAll 会自行初始化本轮会话，不需要先发送 `ProjectARVRInit`：

```json
{
  "Version": "1.0",
  "MsgID": "req-runall-001",
  "EventName": "RunAll",
  "SerialNumber": "SN12345678"
}
```

接受后返回开始确认：

```json
{
  "MsgID": "req-runall-001",
  "EventName": "RunAll",
  "SerialNumber": "SN12345678",
  "Code": 0,
  "Msg": "RunAll started"
}
```

开始确认只表示请求已接受，不表示测试完成或判定 PASS。客户端必须继续等待最终 `ProjectARVRResult`。

窗口处于切图、流程启动、执行、结果处理或 RunAll 阶段时返回：

```json
{
  "MsgID": "req-runall-001",
  "EventName": "RunAll",
  "SerialNumber": "SN12345678",
  "Code": -4,
  "Msg": "ARVR test is busy"
}
```

RunAll 启动时读取当前组的启用流程列表，并按顺序执行。执行期间不要切换组或修改启用状态。`AllowTestFailures` 配置决定某一步失败后继续还是终止；最终结果会保留首次流程失败信息。

## 10. AOI 切图中转

AOI 使用独立的 Socket Relay 服务，默认地址为 `127.0.0.1:9200`，默认不自动启动。现场必须确认 Relay 已启用并且 Flow 已连接。

```text
Flow -- 文本 "1" --> Relay -- AoiSwitchPG --> 外部控制程序
Flow <-- 文本 "1" -- Relay <-- AOITestSwitchImageComplete -- 外部控制程序
```

当 Relay 收到 Flow 发来的精确文本 `1` 时，向外部程序推送：

```json
{
  "Version": "1.0",
  "MsgID": "",
  "EventName": "AoiSwitchPG",
  "Code": 0,
  "Msg": "AoiSwitchPG"
}
```

外部程序完成对应画面切换后发送：

```json
{
  "Version": "1.0",
  "MsgID": "req-aoi-001",
  "EventName": "AOITestSwitchImageComplete",
  "SerialNumber": "SN12345678"
}
```

成功时不会向外部程序返回单独 ACK，Relay 会向 Flow 转发文本 `1`。一轮 Flow 可能多次请求 AOI 切图，每次收到请求都应完成一次切换并回复一次确认。

Flow 发来的其他文本可能按原文转发，因此 Relay 发给外部程序的内容不保证永远都是 JSON。只连通主 Socket 也不能证明 Relay 已连接或 Flow 已收到确认。

## 11. 最终结果：ProjectARVRResult

整轮测试完成或终止时，ColorVision 会向当前控制连接推送最终结果。标准模式摘要示例：

```json
{
  "Version": "1.0",
  "MsgID": "",
  "EventName": "ProjectARVRResult",
  "SerialNumber": "SN12345678",
  "Code": 0,
  "Msg": "ARVR Test Completed",
  "Data": {
    "TotalResult": true,
    "TotalResultString": "PASS"
  }
}
```

| 项目 | 说明 |
| --- | --- |
| `Code=0` | 没有记录到流程执行或解析失败；仍需继续检查最终质量判定 |
| `Code=-1` | 流程、启动、预处理或解析失败的常用状态 |
| `Code=-2` | 记录到流程超时失败；若此前已有失败，可能保留首次失败码和说明 |
| `Data.TotalResult=true` | 最终质量判定 PASS |
| `Data.TotalResult=false` | 最终质量判定 FAIL；即使 `Code=0` 也不能按成功放行 |
| `MsgID=""` | 最终结果不回显原请求标识，应结合当前连接和 SN 关联 |

输出数据有两种模式：

- `UseLegacyARVROutput=false`：`Data` 为标准嵌套结果对象。
- `UseLegacyARVROutput=true`：`Data` 为旧版扁平结果对象，同时流程外部索引加 `1`。

客户端接入前应与现场确认所用输出模式，并按实际项目包版本取得结果字段定义。无论哪种模式，都必须核对最终 `SerialNumber`、`Code` 和最终判定字段。

断线或发送失败时，ColorVision 界面和本地数据库中可能已经生成结果，但客户端无法收到；当前没有重连后自动补发结果的保证。

## 12. 推荐调用顺序

一键运行：

```text
建立 TCP 连接
-> SwitchGroup
-> GetProcessEnable
-> 必要时 SetProcessEnable
-> 再次 GetProcessEnable 确认
-> RunAll
-> 等待 ProjectARVRResult
```

外部逐步切图：

```text
建立 TCP 连接
-> SwitchGroup
-> GetProcessEnable
-> 必要时 SetProcessEnable
-> ProjectARVRInit
-> 循环处理 SwitchPG / SwitchPGCompleted
-> 等待 ProjectARVRResult
```

## 13. 通用错误与排查

| 现象 | 优先检查 |
| --- | --- |
| `Code=400 / Invalid request` | JSON 是否为空，是否缺少 `EventName` |
| `Code=404 / Handler not found for event: ...` | `EventName` 大小写、Json 模式、ProjectARVRPro 是否正确加载、软件版本是否支持该事件 |
| `Code=-1` 且为 JSON 或类型错误 | 报文是否完整，`Params` 是否误传成嵌套对象，是否发生半包或粘包 |
| 初始化返回 `-2 / No enabled ARVR flow` | 当前活动组是否存在启用流程 |
| RunAll 返回 `-4 / ARVR test is busy` | 上一轮切图、Flow 或结果处理是否仍在执行 |
| 设置返回 `1 / Partial applied` | 检查 `Applied` 和 `NotFound`，重新查询当前组和索引 |
| 切图确认后没有单独响应 | 成功路径原本就没有 ACK，应等待下一条切图请求或最终结果 |
| 结果发到另一个连接 | 是否有其他客户端发送过 ARVR 请求并覆盖当前推送连接 |
| AOI 卡住 | Relay 是否启动、Flow 是否连接、请求是否为精确文本 `1`、确认是否真正转发到 Flow |

## 14. 并发与安全限制

- 当前测试会话和结果推送不按客户端连接隔离。任一 ARVR 请求都可能把后续推送目标改为该请求所在连接。
- 不要让多个客户端同时控制同一个 ProjectARVRPro 实例。
- 服务端不使用 `MsgID` 去重，重复发送可能再次初始化、修改配置或推进流程。
- 客户端必须实现业务超时与断线处理；超时后应先核对 ColorVision 当前状态和本地结果，再决定是否重试。
- 本协议可能推进真实设备测试或修改运行配置。正式使用前应确认设备状态、目标流程组和现场操作权限。
- 接入时记录现场安装的 `ProjectARVRPro` 项目包版本；不同版本之间的事件、索引或结果字段可能存在差异。
