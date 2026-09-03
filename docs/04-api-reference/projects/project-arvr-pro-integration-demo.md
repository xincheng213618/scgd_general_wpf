---
knowledge_id: "projects.arvr-pro-demo"
knowledge_type: "reference"
status: "current"
summary: "独立 net48 ARVRPro TCP/JSON Demo 的公开字段、ACK 与最终完成判据、切图自动确认、逐条消息超时及 JSON/CSV 导出；正常退出不代表最终 SN 和明确 PASS 已核验。"
aliases: ["RunAll Code=0 是否已经测试完成","客户怎么对接 ARVRPro","最终结果MsgID为什么为空","ProjectARVRPro.IntegrationDemo","JsonStreamMessageReader","ArvrClient","ResultParser","DemoOptions","ProjectARVRResult","MsgID","TotalResult"]
code_paths: ["Projects/ProjectARVRPro.IntegrationDemo/Program.cs","Projects/ProjectARVRPro.IntegrationDemo/MainWindow.xaml.cs","Projects/ProjectARVRPro.IntegrationDemo/MainWindow.xaml","Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj","Scripts/publish_project_arvrpro_integration_demo.py","Scripts/publish_project_arvrpro_integration_demo.bat","Projects/ProjectARVRPro/Integration/IntegrationDemoReleaseClient.cs","Projects/ProjectARVRPro/Integration/IntegrationDemoPanel.xaml.cs","Projects/ProjectARVRPro.IntegrationDemo/Contracts/","Projects/ProjectARVRPro.IntegrationDemo/Samples/project-arvr-result.json","Projects/ProjectARVRPro/Services/RunAllSocket.cs","Projects/ProjectARVRPro/ARVRWindow.xaml.cs"]
test_paths: ["Test/ProjectARVRPro.Tests/IntegrationDemoReleaseClientTests.cs"]
related: ["projects.arvr-pro","projects.arvr-pro-protocol","projects.index"]
---

# ProjectARVRPro.IntegrationDemo

`Projects/ProjectARVRPro.IntegrationDemo/` 是给客户、MES、PLC 上位机或自动化中控使用的最小 TCP/JSON 对接示例。它不是 ColorVision 插件，不依赖 ColorVision 主程序和内部算法 DLL。服务端的完整请求/响应、索引、状态码和执行条件见 [TCP 通讯协议](./project-arvr-pro-protocol.md)。

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
| 键化结果 | `FieldOfViewTestResults`、`LuminanceChromaticityTestResults`、`LuminanceChromaticityYWTestResults`、`ChessboardTestResults`、`DynamicMTFHV058TestResults`、`MTFH07TestResults`、`MTFV07TestResults` | 第一层 Key 来自流程配置，应枚举实际返回值，不能写死为 `White`。 |
| 动态结果 | `DynamicTestResults`、`DynamicPoixyuvDatas`、`DynamicScreenDefectResults` | 分别承载动态测试项、POI 光色数据和屏幕缺陷汇总/缺陷框。 |
| 固定/兼容结果 | W51、W255、Black、Chessboard、MTF、Distortion、OpticCenter 等结果 | Key 为 `White` 的视场角和亮色度流程还会写入 W51/W255 兼容字段。 |

`ChessboardTestResult` 同时包含 `ChessboardContrast` 与 `AverageBlackLuminance`。随 Demo 提供的标准样例包含键化结果、YW 双 POI 组、动态测试项、动态 POI、屏幕缺陷和棋盘格字段；字段名与结构以样例 JSON 和 `Contracts/` 为准。

### Legacy 输出边界

`UseLegacyARVROutput` 决定 `ProjectARVRResult.Data` 的整体形态：关闭时返回现代嵌套 `ObjectiveTestResult`，开启时返回独立的扁平 `LegacyARVRObjectiveTestResult`。两者不是同一 `Data` 中的新旧字段并集；Demo 样例只表示标准形态，Legacy 现场必须按实际扁平报文单独解析。

## 对接事件时序

服务端接收行为看 `Projects/ProjectARVRPro/Services/RunAllSocket.cs`，最终报文看 `Projects/ProjectARVRPro/ARVRWindow.xaml.cs`，Demo 接收判定看 `Program.cs`。`RunAll Code=0` 只是开始执行的 ACK，不是测试完成或成功，不能触发客户产线的完成放行。

| 阶段 | Demo 行为 | ARVRPro 期望 | 对接要点 |
| --- | --- | --- | --- |
| 建连 | `TcpClient` 连接 `host:port` | ARVRPro Socket 服务监听，默认端口 `6666` | 现场先确认端口、防火墙、宿主是否已加载 ProjectARVRPro |
| 初始化 | 发送 `ProjectARVRInit`，带 `SerialNumber` | 宿主建立当前 SN 和流程上下文 | SN 必须和客户 MES/上位机一致，后续结果靠它追溯 |
| 全流程 | 发送 `RunAll` | 宿主按当前流程组执行所有步骤 | 只适合已配置好流程组、Recipe、切图方式的现场 |
| 普通切图确认 | 收到 `SwitchPG` 后发送 `SwitchPGCompleted` | 宿主继续下一个流程节点 | 自动确认按事件与MsgID去重，缺MsgID时回退SN和ARVRTestType；不核验实际切图完成 |
| AOI 切图确认 | 收到 `AoiSwitchPG` 后发送 `AOITestSwitchImageComplete` | 宿主继续 AOI Relay 链路 | 如果现场没有 AOI Relay，不应强行发送该确认 |
| 切换流程组 | 同步发送 `SwitchGroup` | 宿主按名称切换活动组并返回 `{GroupName, MetaCount}` | `Params` 是非空组名字符串 |
| 查询流程启用状态 | 同步发送 `GetProcessEnable` | 宿主返回 `{ActiveGroupName, Count, Items}` | `Params` 留空，后续设置应复用返回的 `Index` |
| 设置流程启用状态 | 同步发送 `SetProcessEnable` | 宿主返回 `{ActiveGroupName, Applied, NotFound}` | `Params` 是 JSON 字符串，推荐 `{"Items":[{"Index":0,"IsEnabled":true}]}` |
| 结果解析 | 收到 `ProjectARVRResult` 后解析并保存 JSON/CSV | 宿主按配置返回标准或 Legacy `Data` | 标准字段变化时同步 `Contracts/`、样例 JSON 和 CSV 说明；Legacy 按独立形态维护 |

## 完成判据与结果关联边界

| 报文或分支 | 当前实现 | 不可推断的保证 |
| --- | --- | --- |
| `RunAll` ACK | 服务端接受后异步启动 `RunAllAsync()`，返回请求 `MsgID`、解析后的 SN 和 `Code=0`；忙时 `Code=-4` | ACK 不代表步骤、后处理或最终判定已完成 |
| 同步管理命令 | CLI 一次只选一个命令，不能与 `--mode` 混用；响应 `EventName` 与 `MsgID` 均须匹配，匹配响应的负 `Code` 使 CLI 失败 | 此匹配逻辑不能套用到异步最终结果 |
| 最终 `ProjectARVRResult` | 服务端填 `MsgID=string.Empty`、当前 SN、最终 `Code` 和标准或 Legacy `Data` | 最终结果不回显原始 RunAll MsgID，不能用该 MsgID 一对一关联 |
| Demo 的测试流程分支 | 收到 `ProjectARVRResult` 后解析并结束；任意可解析负 `Code` 或 `TotalResult==false` 会失败 | 当前未核对最终 SN 是否等于请求 SN，也未核对原请求 MsgID |

同步设置的 `Code=1 / Partial applied` 不会仅因状态码而令 CLI 失败，客户程序仍需检查 `Applied` / `NotFound`。

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

## 接收等待、切图确认与导出

| 项目 | 当前行为与使用条件 |
| --- | --- |
| `--timeout-seconds` | 默认300秒，每次等待一个完整JSON对象重新计时；不包含建连、发送、人工确认，也不是整轮测试时限。超时释放本地流，没有发送停止测试命令 |
| `--max-messages` | 默认200条，包括无关事件和重复消息。CLI 达到上限报错；WPF 结束本轮接收，不把它报告为测试成功 |
| WPF 连接 | “仅连接”启动接收循环但不主动发送初始化。管理命令响应写入日志，没有 CLI 的逐请求匹配任务；收到最终结果或达到上限后，下一轮需重新连接 |
| 自动切图确认 | WPF 默认勾选 SwitchPG/AOI 两类确认，“仅连接”也会回复；CLI 默认询问，自动参数启用后直接回复。Demo 不控制或检查实际画面，需由现场系统保证切图已完成 |
| 确认去重 | 当前连接内优先用 `EventName + MsgID`，缺MsgID时用事件、SN和ARVRTestType。发送前登记，发送失败不恢复键；手动按钮不经过去重集合 |
| 解析与展开 | 强类型失败可继续通用展开。POI和屏幕缺陷走专用规则，输出光色项、汇总及缺陷框字段并保留 `Path`；不是把任意标量当测试项 |
| 自动保存 | 文件名含秒级时间，非空SN经清理后加入；同一目录同一SN同一秒会覆盖。先写JSON再写CSV，后者失败可能只留下JSON；`--parse-file` 不以报文负Code或最终失败决定退出码 |

TCP reader以UTF-8增量解码、字符串/转义感知的大括号配平处理半包和粘包，要求顶层JSON对象；它不是业务schema或必需字段验证器。需要人工切图时，应在连接前关闭WPF自动确认，完成切图后再发送确认。

## 离线验证与客户产物

`Test/ProjectARVRPro.Tests/IntegrationDemoReleaseClientTests.cs` 覆盖发布元数据读取、下载路径约束和包大小/SHA-256 校验，使用 HTTP stub；它不覆盖 TCP 协议、RunAll ACK 或最终结果关联。

`Scripts/publish_project_arvrpro_integration_demo.bat` 是独立 ZIP 发布入口：构建net48/x64、复制源码/README/样例、运行离线解析并检查CSV产物，打包后先上传并下载核对ZIP，再更新和核对 `latest.json`。`--validate-only` 仍构建和运行离线解析，但不上传。元数据 `verifiedProjectARVRProVersion` 只是构建时读取的插件版本，不是联机验收记录；客户交付仍需另存联调结果。源码旁README保留随包命令与字段说明，不能用发布脚本验证普通文档修改。

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
| 离线解析 | 用 `--parse-file` 解析样例或现场保存的 `ProjectARVRResult` | 能生成原始 JSON 副本和扁平 CSV，键化/动态字段、`AverageBlackLuminance`、`EventName`、SN、Code、Msg、TotalResult 可读 |
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
