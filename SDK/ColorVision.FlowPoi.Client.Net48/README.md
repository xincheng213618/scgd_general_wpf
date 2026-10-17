# ColorVision 流程 POI 调用客户端（net48 / x64）

独立的 .NET Framework 4.8 客户端，调用主程序通用 Socket 指令 `FlowRunPoi`。支持按流程名称或实际模板 ID 执行，等待本次流程及后处理完成，解析本次批次的 POI。运行时只需要 .NET Framework 4.8、客户端 DLL 和随包的 Newtonsoft.Json，不依赖 ColorVision 内部程序集或客户项目。

## 现场使用

1. 主程序应更新到包含 `FlowRunPoi` 的版本并启动。在“帮助 → 网络通信”启用 Socket 服务，选择 **JSON** 模式。确认实际监听端口（默认 `6666`）及现场可访问的 IP。
2. 在主程序连接 MySQL，加载待执行流程。流程需产生 `POI_XYZ` 结果，并满足其设备、服务和前后处理依赖；先确认流程及现场设备可以执行。
3. 将运行包整个 `Binaries` 目录复制到现场 Windows x64 电脑。需要安装 .NET Framework 4.8；运行示例不需要 .NET 10 SDK 或 Visual Studio。
4. 双击 `FlowPoiClientDemo.exe`，按提示输入主程序 IP、端口、流程名称或模板 ID。按名称与按 ID 二选一；ID 是模板实际 ID，不是列表位置，本地模板的负数 ID 可直接使用。
5. 查看返回码、批次编号和各点结果。交互模式会将完整 JSON 响应保存到程序旁的 `Results` 目录，请放到可写目录。

示例会真实执行流程及已配置的前后处理，可能触发采集、设备动作和数据库写入。`SerialNumber` 是请求关联编号；本次流程的执行编号在 `Data.ExecutionSerialNumber` 中返回。

也可以在 PowerShell 中调用：

```powershell
.\FlowPoiClientDemo.exe --host 192.168.1.10 --port 6666 --name "白画面" --timeout 600 --output .\result.json
.\FlowPoiClientDemo.exe --host 192.168.1.10 --port 6666 --id 42 --timeout 600 --sn "sample-1" --output .\result.json
.\FlowPoiClientDemo.exe --help
```

CLI 模式只在指定 `--output` 时写文件；已有同名输出文件会被覆盖。退出码 `0` 为业务成功，`2` 为主程序返回业务失败，`3` 为用户取消等待，`1` 为参数、连接、超时、协议或文件保存错误。

## 在自己的 net48 程序中调用

项目目标设置为 **.NET Framework 4.8 / x64**，引用 `ColorVision.FlowPoi.Client.Net48.dll` 和 `Newtonsoft.Json.dll`。也可复制 `FlowPoiClient.cs`、`FlowPoiResponse.cs` 到自己的项目，并通过 NuGet 引用 `Newtonsoft.Json 13.0.4`。已有应用使用其它 Newtonsoft.Json 版本时，应在调用方统一引用并配置相应绑定重定向。

```csharp
using System;
using System.Threading.Tasks;
using ColorVision.FlowPoi.Client;

public static async Task ExecuteAndReadPoiAsync()
{
    var client = new FlowPoiClient("192.168.1.10", 6666);

    FlowPoiResponse response = await client.RunByNameAsync(
        "白画面",
        serialNumber: "sample-1",
        timeout: TimeSpan.FromMinutes(10));

    // 按模板 ID 调用时，将上面的调用改为：
    // FlowPoiResponse response = await client.RunByIdAsync(42);

    if (!response.IsSuccess)
        throw new InvalidOperationException("Code=" + response.Code + ": " + response.Msg);

    foreach (FlowPoiPoint poi in response.Data.Poi)
    {
        Console.WriteLine(
            "{0}: X={1}, Y={2}, Z={3}, x={4}, y={5}, u={6}, v={7}, CCT={8}, Wave={9}",
            poi.Name, poi.X, poi.Y, poi.Z,
            poi.ChromaticityX, poi.ChromaticityY, poi.U, poi.V, poi.CCT, poi.Wave);
    }
}
```

业务调用处应处理 `TimeoutException`、网络/JSON 异常和 `OperationCanceledException`。两个方法均支持 `cancellationToken`，可用于取消客户端等待。WPF/WinForms 界面中使用 `await`，避免在界面线程调用 `.Result` 或 `.Wait()`。

线上字段大写 `X/Y/Z` 是三刺激值，小写 `x/y` 是色度坐标。客户端分别映射为 `X/Y/Z` 和 `ChromaticityX/ChromaticityY`；`u/v` 映射为 `U/V`，防止调用方混淆。每个点保留 `AlgorithmResultId/AlgorithmName/PoiId/Name` 和像素位置、尺寸。

## 完成与失败

客户端每次调用建立独立 TCP 连接，发送一次完整 JSON 请求；`Params` 按主程序要求编码成 JSON 字符串。使用 `EventName + MsgID` 匹配响应，持续读取完整 JSON，支持响应分包、连包、跨读取的 UTF-8 中文和没有换行符的响应。示例响应见 `Samples/flow-poi-response.json`，其中数据均为合成示例。

默认超时为 **600 秒**，覆盖连接、执行和响应接收。超时、主动取消或连接断开仅终止本地连接，不能证明主程序中的流程已停止；请先在主程序确认执行状态，必要时使用主程序“停止”操作。客户端不会自动重发执行命令。

| Code | 处理建议 |
| --- | --- |
| `200` | 读取 `Data.Poi`；`Status` 为 `Succeeded` 或 `SucceededWithWarnings` |
| `400` | 检查参数格式及主程序/客户端协议是否一致 |
| `404` | 核对流程名称或实际模板 ID，确认主程序版本支持该指令 |
| `409` | 主会话占用、同名模板或启动条件不满足；查看主程序提示，不自动重试 |
| `422` | 流程完成，但本次批次无 `POI_XYZ` 点位结果 |
| `499` / `504` | 主程序报告流程取消 / 超时 |
| `500` | 流程、必需后处理或 POI 查询解析失败，查看 `Msg` 和主程序日志 |
| `503` | 检查主程序就绪状态、MySQL 连接和结果批次 |

非 `200` 返回值仍会保留服务端提供的 `Data` 批次身份；不要将失败响应或缺少字段解释为零值测量。响应为 `200` 但无有效批次、成功状态或 POI 时，客户端报告协议错误。

旧主程序可能返回不带 `EventName/MsgID` 的通用 `404`，此时客户端立即报告缺少关联字段和服务端错误，不将它当作 POI 结果或一直等待。先确认现场主程序确实包含 `FlowRunPoi`。

当前主程序 Socket 接收端仍按单次网络读取分发请求，没有通用请求分帧。本客户端一次写出一个小请求，但 TCP 本身不保证一次 write 对应一次 read；客户端完整响应解析不能替代服务端请求分帧。现场网络与真实设备执行结果需要联调确认。

## 源码与编译

`Source` 中包含可独立编译的 SDK 风格项目，编译机需 Visual Studio/MSBuild 或支持 net48 的 .NET SDK，以及 .NET Framework 4.8 Developer Pack。还原 Newtonsoft.Json 需要包源访问或已有 NuGet 缓存。

在本目录执行：

```powershell
dotnet build .\Demo\FlowPoiClientDemo.csproj -c Release -p:Platform=x64
```

输出在 `Demo/bin/x64/Release/net48/`。复制时保留 EXE、EXE.config、客户端 DLL 和 Newtonsoft.Json.dll；源码包不包含仓库签名私钥。

仓库内网络回归入口：`Test/ColorVision.FlowPoi.Net48.Tests/`。使用本机回环模拟主程序验证请求、分包/连包、字段大小写、超时、取消和控制台输出，不控制设备，也不访问现场 MySQL；这些检查不能代替现场联调。
