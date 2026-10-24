# FlowRunPoi 客户对接接口

本接口通过 ColorVision 主程序运行一个流程，待流程和后处理完成后，返回**本次执行批次**的 POI_XYZ 结果。它不是客户端本地算法 DLL；客户端可独立运行，服务端仍需启动 ColorVision、启用 Socket 的 JSON 模式、连接 MySQL，并准备可执行的流程与设备。

客户端兼容 **.NET Framework 4.8 / Windows x64**，仅引用随包的客户端 DLL 和 Newtonsoft.Json。无需安装 .NET Core，不依赖 ARVRPro 或其它客户项目。

## 请求

使用 TCP，默认端口 `6666`，实际地址以主程序“帮助 → 网络通信”为准。协议版本为 `1.0`，事件名区分大小写：`FlowRunPoi`。

按流程名称：

```json
{"Version":"1.0","MsgID":"poi-request-1","EventName":"FlowRunPoi","SerialNumber":"sample-1","Params":"{\"FlowName\":\"白画面\"}"}
```

按模板实际 ID：

```json
{"Version":"1.0","MsgID":"poi-request-2","EventName":"FlowRunPoi","SerialNumber":"sample-2","Params":"{\"FlowId\":42}"}
```

| 字段 | 类型 | 约定 |
| --- | --- | --- |
| Version | string | 使用 `"1.0"` |
| MsgID | string | 本次请求唯一编号，响应原样回传；SDK 自动生成 |
| EventName | string | 固定为 `"FlowRunPoi"` |
| SerialNumber | string | 可选请求关联编号，响应原样回传，不是流程的执行编号 |
| Params | string | 内层 JSON **字符串**，只含 FlowName 或 FlowId，二选一 |
| Params.FlowName | string | 实际流程模板名称，服务端忽略名称大小写；同名冲突时使用唯一 ID |
| Params.FlowId | int | 模板实际 ID，不能为 0；支持本地模板的负数 ID，不是列表索引 |

调用会选择并运行主程序工作区流程，可能触发采集、设备动作、前后处理和数据库写入。SDK 每次只发送一次，不自动重试。

## 响应

完整合成样例见 [Samples/flow-poi-response.json](Samples/flow-poi-response.json)。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| Version / MsgID / EventName / SerialNumber | string | 请求协议版本及关联字段；以 EventName 和 MsgID 同时匹配响应 |
| Code | int | 200 表示流程及最终化成功，且有本次批次 POI；其它值为失败 |
| Msg | string | 完成说明或失败原因 |
| Data | object / null | 批次数据；执行前拒绝时可以为 null，失败时也可能保留已取得的批次身份 |
| Data.FlowId / FlowName | int / string | 本次实际执行的流程 |
| Data.BatchId | int | 本次 MySQL 测量批次 ID |
| Data.ExecutionSerialNumber | string | 主程序生成的本次执行编号，与请求 SerialNumber 不同 |
| Data.Status | string | 成功为 Succeeded 或 SucceededWithWarnings；失败保留实际最终状态 |
| Data.TotalTime | long | 主流程引擎用时，毫秒；响应需等到后处理完成，字段不代表客户端网络总等待时间 |
| Data.Poi | array | 本次批次中所有 POI_XYZ 点位，可能来自多个 POI 算法输出 |

每个 POI：

| 线上字段 | net48 属性 | 含义 |
| --- | --- | --- |
| AlgorithmResultId / AlgorithmName | 同名属性 | 算法结果 ID 与输出名称 |
| PoiId / Name | 同名属性 | 点位 ID（可空）与名称 |
| PixelX / PixelY / Width / Height | 同名属性 | 点位像素位置与区域尺寸 |
| X / Y / Z | X / Y / Z | 三刺激值 |
| x / y | ChromaticityX / ChromaticityY | 色度坐标，不能和大写 X / Y 混用 |
| u / v | U / V | 色度坐标 |
| CCT / Wave | 同名属性 | 相关色温与波长结果 |

SDK 对 X/Y/Z、x/y、u/v、CCT、Wave 使用必需字段校验。成功码但缺少有效批次、成功状态或 POI 时抛出协议异常；失败响应不会被解释成零值测量。请保留批次 ID 和执行编号用于追溯。

## 返回码与等待

| Code | 含义与处理 |
| --- | --- |
| 200 | 读取本次批次的 POI |
| 400 | 参数错误；检查 Params 是 JSON 字符串且选择器二选一 |
| 404 | 无此流程，或主程序没有加载该接口 |
| 409 | 模板不唯一、工作区占用或流程无法启动；确认主程序状态后再决定是否执行 |
| 422 | 流程结束，但本次批次没有 POI_XYZ 结果 |
| 499 / 504 | 主程序报告流程取消 / 超时 |
| 500 | 流程、必需后处理或 POI 查询解析失败 |
| 503 | 主程序、MySQL 或本次结果批次未就绪 |

SDK 默认总超时 600 秒，包括连接与读取。超时抛出 TimeoutException，调用方取消抛出 OperationCanceledException。两者都只关闭本次客户端连接，**不等于停止主程序流程**；未确认现场执行状态前不要重发。

SDK 支持分段 / 连包响应、跨读取 UTF-8 和无换行结尾的完整 JSON。当前主程序仍按单次网络读取分发请求，TCP 不保证一次 write 对应一次 read；服务端请求分帧的限制仍需在实际网络联调中确认。

## 最小 C# 调用

```csharp
var client = new ColorVision.FlowPoi.Client.FlowPoiClient("127.0.0.1", 6666);
var response = await client.RunByNameAsync("白画面",
    serialNumber: "sample-1", timeout: TimeSpan.FromMinutes(10));
// 按 ID：await client.RunByIdAsync(42);
if (!response.IsSuccess)
    throw new InvalidOperationException("Code=" + response.Code + ": " + response.Msg);
foreach (var poi in response.Data.Poi)
    Console.WriteLine("{0}: Y={1}, x={2}, y={3}",
        poi.Name, poi.Y, poi.ChromaticityX, poi.ChromaticityY);
```

窗口 Demo 演示同一套调用；“查看离线样例”及 `--parse` 只解析文件，不连接主程序，也不证明设备联调成功。
