# ProjectARVRPro.IntegrationDemo

`Projects/ProjectARVRPro.IntegrationDemo/` 是给客户、MES、PLC 上位机或自动化中控使用的最小 TCP/JSON 对接示例。它不是 ColorVision 插件，不依赖 ColorVision 主程序和内部算法 DLL。

## 项目定位

| 项目 | 说明 |
| --- | --- |
| 目标框架 | .NET Framework 4.8 |
| 形态 | WPF 演示窗口 + CLI 参数 |
| 依赖 | 不依赖 ColorVision 内部项目 |
| 用途 | 演示 ARVRPro TCP 连接、发命令、解析结果、导出 CSV |

## 主要能力

- 连接 ARVRPro 默认 TCP 端口 `6666`。
- 发送 `ProjectARVRInit`、`SwitchPGCompleted`、`RunAll`、`AOITestSwitchImageComplete`。
- 加载样例 JSON 或现场保存的 `ProjectARVRResult` JSON。
- 查看 `ObjectiveTestResult` 和扁平测试项表格。
- 自动保存原始 JSON，并导出 CSV。
- 演示半包/粘包情况下的 JSON 对象读取。

## 公开代码边界

客户可复用的契约代码集中在：

- `Contracts/ObjectiveTestResult.cs`
- `Contracts/ObjectiveTestItem.cs`
- `Contracts/Process/`
- `Contracts/Socket/`
- `Contracts/MVVM/ViewModelBase.cs`

这些代码只描述 JSON 字段，不依赖 ARVRPro 流程、算法、数据库或 UI。

## 对接事件时序

| 阶段 | Demo 行为 | ARVRPro 期望 | 对接要点 |
| --- | --- | --- | --- |
| 建连 | `TcpClient` 连接 `host:port` | ARVRPro Socket 服务监听，默认端口 `6666` | 现场先确认端口、防火墙、宿主是否已加载 ProjectARVRPro |
| 初始化 | 发送 `ProjectARVRInit`，带 `SerialNumber` | 宿主建立当前 SN 和流程上下文 | SN 必须和客户 MES/上位机一致，后续结果靠它追溯 |
| 全流程 | 发送 `RunAll` | 宿主按当前流程组执行所有步骤 | 只适合已配置好流程组、Recipe、切图方式的现场 |
| 普通切图确认 | 收到 `SwitchPG` 后发送 `SwitchPGCompleted` | 宿主继续下一个流程节点 | Demo 会用 `MsgID`、SN 和 `ARVRTestType` 避免重复确认 |
| AOI 切图确认 | 收到 `AoiSwitchPG` 后发送 `AOITestSwitchImageComplete` | 宿主继续 AOI Relay 链路 | 如果现场没有 AOI Relay，不应强行发送该确认 |
| 结果解析 | 收到 `ProjectARVRResult` 后解析并保存 JSON/CSV | 宿主返回 `ObjectiveTestResult` | 对外字段变化时同步 `Contracts/`、样例 JSON 和 CSV 说明 |

`JsonStreamMessageReader` 是这个 Demo 的关键对接点。客户上位机如果直接按 `ReadLine()` 或固定长度读取 JSON，遇到半包/粘包就会误判；对外说明时应把这里作为 TCP reader 参考实现。

## 常用命令

| 场景 | 命令 |
| --- | --- |
| 启动窗口 | `dotnet run --project Projects/ProjectARVRPro.IntegrationDemo` |
| 离线解析样例 | `dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --parse-file Projects/ProjectARVRPro.IntegrationDemo/Samples/project-arvr-result.json` |
| 联机初始化 | `dotnet run --project Projects/ProjectARVRPro.IntegrationDemo -- --host 127.0.0.1 --port 6666 --sn SN001 --mode init` |
| 发布给客户 | `dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -f net48 -c Release -p:Platform=x64 -o artifacts/ProjectARVRPro.IntegrationDemo` |

## 对接注意事项

- 这是客户侧示例，不要把 ColorVision 内部业务逻辑引入这个项目。
- 对外字段变化时，先更新 `Contracts/`，再更新样例 JSON 和 README。
- 客户系统读取 TCP 时必须处理半包和粘包；本 demo 的 reader 可以作为参考实现。

## 对接检查表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 离线解析 | 用 `--parse-file` 解析样例或现场保存的 `ProjectARVRResult` | 能生成原始 JSON 副本和扁平 CSV，`EventName`、SN、Code、Msg、TotalResult 可读 |
| 联机初始化 | 启动 ARVRPro 后执行 `--mode init --sn <SN>` | Demo 能收到 `SwitchPG` 或最终结果，日志没有 JSON 读取异常 |
| 联机全流程 | 执行 `--mode runall` | 流程能跑到 `ProjectARVRResult`，结果 CSV 字段不丢失 |
| 切图确认去重 | 让宿主重复发送同一切图事件 | Demo 只确认一次，日志出现 duplicate skip 信息 |
| CSV 字段 | 打开导出的 CSV | 至少包含 Screen、Item、Description、Value、TestValue、Unit、LowLimit、UpLimit、TestResult、Path |
| 客户发布包 | 执行 `dotnet publish ... -f net48` | 输出目录能独立运行，不需要 ColorVision 主程序 DLL |

## 故障首查

| 现象 | 先查哪里 | 判断 |
| --- | --- | --- |
| 连接不上 | ARVRPro 是否启动 Socket、端口是否仍为 `6666`、防火墙 | Demo 只负责客户端连接，不会自动启动宿主服务 |
| 收到 JSON 但不继续 | `EventName` 是否为 `SwitchPG` / `AoiSwitchPG`，`MsgID` 是否重复 | 需要确认上位机是否已经完成实际切图 |
| 解析结果为空 | `Data` 是否符合 `ObjectiveTestResult` 结构 | 多半是宿主字段变化后未同步 `Contracts/` |
| CSV 缺字段 | `ResultParser.WriteCsv` 和 `ResultItem` | 对外字段新增后必须同步扁平化规则 |
| 客户要求改算法 | 不在本项目改 | Demo 只维护协议契约和解析展示，算法逻辑仍在 ProjectARVRPro |
