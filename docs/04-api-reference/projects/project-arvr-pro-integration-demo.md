---
knowledge_id: "projects.arvr-pro-demo"
knowledge_type: "reference"
status: "current"
summary: "独立 net48 ARVRPro TCP/JSON Demo 的公开字段、ACK 与最终完成判据、半包粘包及离线验证。"
aliases: ["RunAll Code=0 是否已经测试完成","客户怎么对接 ARVRPro","最终结果MsgID为什么为空","ProjectARVRPro.IntegrationDemo","ProjectARVRResult","MsgID","TotalResult"]
code_paths: ["Projects/ProjectARVRPro.IntegrationDemo/Program.cs","Projects/ProjectARVRPro.IntegrationDemo/Contracts/","Projects/ProjectARVRPro.IntegrationDemo/Samples/project-arvr-result.json","Projects/ProjectARVRPro/Services/RunAllSocket.cs","Projects/ProjectARVRPro/ARVRWindow.xaml.cs"]
test_paths: ["Test/ProjectARVRPro.Tests/IntegrationDemoReleaseClientTests.cs"]
related: ["projects.arvr-pro","projects.index"]
---

# ProjectARVRPro.IntegrationDemo

`Projects/ProjectARVRPro.IntegrationDemo/` 是给客户、MES、PLC 上位机或自动化中控使用的最小 TCP/JSON 对接示例。它不是 ColorVision 插件，不依赖 ColorVision 主程序和内部算法 DLL。

## 项目定位

这是一个不依赖 ColorVision 内部项目的 .NET Framework 4.8 WPF + CLI 示例，Demo 产品版本为独立的 `1.0.0`。每次交付应从当次联调源码的 `Projects/ProjectARVRPro/ProjectARVRPro.csproj` 读取并单独记录插件 `VersionPrefix`，不要从 Demo 版本推断兼容性。

## 主要能力

- 连接 ARVRPro 默认 TCP 端口 `6666`，加载/查看结果 JSON，保存原始 JSON 和扁平 CSV，并演示半包/粘包读取。
- 发送 `ProjectARVRInit`、`SwitchPGCompleted`、`RunAll`、`AOITestSwitchImageComplete`。
- 同步发送 `SwitchGroup`、`GetProcessEnable`、`SetProcessEnable`，并读取匹配的响应。

## 公开代码边界

客户可复用的契约代码集中在 `Contracts/ObjectiveTestResult.cs`、`Contracts/ObjectiveTestItem.cs`、`Contracts/Process/`、`Contracts/Socket/` 和 `Contracts/MVVM/ViewModelBase.cs`。

这些代码只描述 JSON 字段，不依赖 ARVRPro 流程、算法、数据库或 UI。

标准 `ObjectiveTestResult` 当前包含：

| 结果类别 | 顶层字段 | 维护要点 |
| --- | --- | --- |
| 键化结果 | `FieldOfViewTestResults`、`LuminanceChromaticityTestResults`、`DynamicMTFHV058TestResults` | 第一层 Key 来自流程配置，应枚举实际返回值，不能写死为 `White`。 |
| 动态结果 | `DynamicTestResults`、`DynamicPoixyuvDatas`、`DynamicScreenDefectResults` | 分别承载动态测试项、POI 光色数据和屏幕缺陷汇总/缺陷框。 |
| 固定/兼容结果 | W51、W255、Black、Chessboard、MTF、Distortion、OpticCenter 等结果 | Key 为 `White` 的视场角和亮色度流程还会写入 W51/W255 兼容字段。 |

`ChessboardTestResult` 同时包含 `ChessboardContrast` 与 `AverageBlackLuminance`。随 Demo 提供的标准样例覆盖上述五个现代顶层字段及棋盘格字段。

### Legacy 输出边界

`UseLegacyARVROutput` 决定 `ProjectARVRResult.Data` 的整体形态：关闭时返回现代嵌套 `ObjectiveTestResult`，开启时返回独立的扁平 `LegacyARVRObjectiveTestResult`。两者不是同一 `Data` 中的新旧字段并集；Demo 样例只表示标准形态，Legacy 现场必须按实际扁平报文单独解析。

## 对接事件时序

服务端接收行为看 `Projects/ProjectARVRPro/Services/RunAllSocket.cs`，最终报文看 `Projects/ProjectARVRPro/ARVRWindow.xaml.cs`，Demo 接收判定看 `Program.cs`。`RunAll Code=0` 只是开始执行的 ACK，不是测试完成或成功，不能触发客户产线的完成放行。

| 阶段 | Demo 行为 | ARVRPro 期望 | 对接要点 |
| --- | --- | --- | --- |
| 建连 | `TcpClient` 连接 `host:port` | ARVRPro Socket 服务监听，默认端口 `6666` | 现场先确认端口、防火墙、宿主是否已加载 ProjectARVRPro |
| 初始化 | 发送 `ProjectARVRInit`，带 `SerialNumber` | 宿主建立当前 SN 和流程上下文 | SN 必须和客户 MES/上位机一致，后续结果靠它追溯 |
| 全流程 | 发送 `RunAll` | 宿主按当前流程组执行所有步骤 | 只适合已配置好流程组、Recipe、切图方式的现场 |
| 普通切图确认 | 收到 `SwitchPG` 后发送 `SwitchPGCompleted` | 宿主继续下一个流程节点 | Demo 会用 `MsgID`、SN 和 `ARVRTestType` 避免重复确认 |
| AOI 切图确认 | 收到 `AoiSwitchPG` 后发送 `AOITestSwitchImageComplete` | 宿主继续 AOI Relay 链路 | 如果现场没有 AOI Relay，不应强行发送该确认 |
| 切换流程组 | 同步发送 `SwitchGroup` | 宿主按名称切换活动组并返回 `{GroupName, MetaCount}` | `Params` 是非空组名字符串 |
| 查询流程启用状态 | 同步发送 `GetProcessEnable` | 宿主返回 `{ActiveGroupName, Count, Items}` | `Params` 留空，后续设置应复用返回的 `Index` |
| 设置流程启用状态 | 同步发送 `SetProcessEnable` | 宿主返回 `{ActiveGroupName, Applied, NotFound}` | `Params` 是 JSON 字符串，推荐 `{"Items":[{"Index":0,"IsEnabled":true}]}` |
| 结果解析 | 收到 `ProjectARVRResult` 后解析并保存 JSON/CSV | 宿主按配置返回标准或 Legacy `Data` | 标准字段变化时同步 `Contracts/`、样例 JSON 和 CSV 说明；Legacy 按独立形态维护 |

## 完成判据与结果关联边界

| 报文或分支 | 当前实现 | 不可推断的保证 |
| --- | --- | --- |
| `RunAll` ACK | 服务端接受后异步启动 `RunAllAsync()`，返回请求 `MsgID`、解析后的 SN 和 `Code=0`；忙时 `Code=-4` | ACK 不代表步骤、后处理或最终判定已完成 |
| 同步管理命令 | Demo 要求响应 `EventName` 与 `MsgID` 均匹配请求；匹配响应的负 `Code` 使 CLI 失败 | 此匹配逻辑不能套用到异步最终结果 |
| 最终 `ProjectARVRResult` | 服务端填 `MsgID=string.Empty`、当前 SN、最终 `Code` 和标准或 Legacy `Data` | 最终结果不回显原始 RunAll MsgID，不能用该 MsgID 一对一关联 |
| Demo 的测试流程分支 | 收到 `ProjectARVRResult` 后解析并结束；任意可解析负 `Code` 或 `TotalResult==false` 会失败 | 当前未核对最终 SN 是否等于请求 SN，也未核对原请求 MsgID |

`TotalResult` 在 Demo 解析模型里可为空。当前判断是 `parsed.TotalResult == false`，不是必须为 `true`；缺失或无法解析为布尔值不会在这条判断中被明确拒绝。因此“Demo 正常退出”不能直接解释为已经严格验证结果属于本次请求且明确 PASS。

客户对接应跟踪当前连接会话和服务端确认的 SN，核对最终结果确属预期测试，并按选定的标准/Legacy schema 验证必需字段与明确的最终判定。上述要求是对接契约与现有缺口，不表示 Demo 已实现严格会话匹配或缺失字段拒绝；不能把这些缺口写成已被测试覆盖。

## 命令与副作用

先区分副作用：`--parse-file` 是离线解析并写出本地 JSON/CSV；`--get-process-enable` 是联网只读查询。联机初始化、RunAll、切图确认会推进真实测试，切换组和设置启用状态会修改宿主运行配置；执行这些命令前必须获得对应现场操作授权并确认目标 host/port，不能作为普通文档检索的验证步骤。

| 场景 | 命令 |
| --- | --- |
| 离线解析样例 | `dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --parse-file Projects/ProjectARVRPro.IntegrationDemo/Samples/project-arvr-result.json` |
| 联机初始化 | `dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --sn SN001 --mode init` |
| 切换流程组 | `dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --switch-group Model_A_Group` |
| 查询流程启用状态 | `dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --get-process-enable` |
| 设置流程启用状态 | `dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --set-process-enable '{"Items":[{"Index":0,"IsEnabled":true}]}'` |

## 对接注意事项

- POI 和屏幕缺陷虽然不是 `ObjectiveTestItem`，Demo 仍会按专用规则展开：POI 输出光色项，缺陷结果输出汇总与每个缺陷框的标量字段，并保留原始 `Path`。
- WPF 中先点“仅连接”，再从“同步命令”选择事件并填写 Params；窗口保持连接，响应异步写入通信日志。
- CLI 的同步命令一次只能选一个，不能和 `--mode` 混用；仅在响应的 `EventName` 和 `MsgID` 都与请求一致时打印最终结果并结束，同名但 `MsgID` 不同的报文会跳过并继续等待。匹配响应的 `Code < 0` 时 CLI 返回非零退出码。
- `--mode init|runall` 会等待最终 `ProjectARVRResult`；`RunAll Code=0` 只是接收确认，不会提前结束。任意负 `Code`、最终 `TotalResult=false`、超时、连接提前断开或达到消息上限都返回非零退出码。
- `SetProcessEnable.Params` 是包含 JSON 的字符串。推荐使用 `Items` 外壳；先查询再复用服务端返回的 `Index`，避免 Legacy 索引偏移。
- 客户系统读取 TCP 时必须处理半包和粘包；本 demo 的 reader 可以作为参考实现。

## 离线验证与客户产物

`Test/ProjectARVRPro.Tests/IntegrationDemoReleaseClientTests.cs` 覆盖发布元数据读取、下载路径约束和包大小/SHA-256 校验，使用 HTTP stub；它不覆盖 TCP 协议、RunAll ACK 或最终结果关联。

验证缺口：当前未登记覆盖 `Program.cs` 的 ACK 后等待、负 Code、最终失败、超时/断连和 EventName+MsgID 匹配的专门协议自动化测试。下表是待执行的验收要求，不是既有测试成功记录。最终 SN 不匹配、缺失/不可解析 `TotalResult` 的场景还应单独建立验收，并先明确要保留还是修改当前宽松行为。

以下为本地构建/测试与客户产物生成，不上传到插件市场；会写入 bin/obj 或 publish 输出。Demo 不是 `.cvxp` 插件，不调用 `package_project.bat`。

```powershell
# 仅验证发布元数据与下载校验，不证明 TCP 协议通过
dotnet test Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj -c Release -p:Platform=x64 --filter FullyQualifiedName~IntegrationDemoReleaseClientTests
dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -c Release -f net48
```

## 对接检查表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 离线解析 | 用 `--parse-file` 解析样例或现场保存的 `ProjectARVRResult` | 能生成原始 JSON 副本和扁平 CSV，五个现代顶层字段、`AverageBlackLuminance`、`EventName`、SN、Code、Msg、TotalResult 可读 |
| 联机初始化 | 启动 ARVRPro 后执行 `--mode init --sn <SN>` | Demo 能收到 `SwitchPG` 或最终结果，日志没有 JSON 读取异常 |
| 联机全流程 | 执行 `--mode runall` | `RunAll Code=0` 确认后继续等待，直到收到最终 `ProjectARVRResult`；成功结果退出码为零且 CSV 字段不丢失 |
| CLI 失败退出 | 分别模拟负 `Code`、最终 `TotalResult=false`、超时、连接提前断开和消息上限 | 每种终态均以非零退出码结束，不会误报成功 |
| 同步管理命令 | WPF 仅连接后依次查询/设置，或分别运行三个 CLI 选项 | 匹配响应的 `EventName`、`MsgID` 均与请求一致；同名错误 `MsgID` 不会提前结束 CLI；`Code < 0` 时退出码非零，设置结果的 Applied/NotFound 可核对 |
| 切图确认去重 | 让宿主重复发送同一切图事件 | Demo 只确认一次，日志出现 duplicate skip 信息 |
| CSV 字段 | 打开导出的 CSV | `ObjectiveTestItem`、POI 光色项、缺陷汇总和 `Defects[i]` 标量字段均可见，并通过 Path 追溯原始 JSON |
| 客户发布包 | 执行 `dotnet publish ... -f net48` | 输出目录能独立运行，不需要 ColorVision 主程序 DLL |

## 故障首查

| 现象 | 先查哪里 | 判断 |
| --- | --- | --- |
| 连接不上 | ARVRPro 是否启动 Socket、端口是否仍为 `6666`、防火墙 | Demo 只负责客户端连接，不会自动启动宿主服务 |
| 收到 JSON 但不继续 | `EventName` 是否为 `SwitchPG` / `AoiSwitchPG`，`MsgID` 是否重复 | 需要确认上位机是否已经完成实际切图 |
| 管理命令无响应 | 请求与响应的 `EventName`、`MsgID`，以及 Params 字符串、超时和活动连接 | CLI 只接受 `EventName + MsgID` 都匹配的响应；WPF 必须先“仅连接” |
| 管理命令返回负 Code | 查看匹配响应的 `Code`、`Msg` 和完整 JSON | `Code < 0` 表示命令失败，CLI 应以非零退出码结束，调用方不能把它当作成功 |
| SetProcessEnable 部分应用 | 查看响应 `Applied`、`NotFound`，并重新查询当前组 | `Code=1` 表示至少一个 Index 不存在；应使用 GetProcessEnable 返回的 Index 重试 |
| 解析结果为空 | 现场是标准还是 Legacy `Data`，以及 `Data` 是否符合对应结构 | 常见原因是现场模式判断错误，或宿主字段变化后未同步 `Contracts/` |
| CSV 缺字段 | 先区分 `ObjectiveTestItem`、POI 与屏幕缺陷，再查对应展开规则和 `ResultParser.WriteCsv` | 专用展开只处理约定字段；新增结构化字段时必须同步解析规则和说明 |
| 客户要求改算法 | 不在本项目改 | Demo 只维护协议契约和解析展示，算法逻辑仍在 ProjectARVRPro |
