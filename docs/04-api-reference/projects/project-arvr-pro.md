# ProjectARVRPro

`Projects/ProjectARVRPro/` 是当前 AR/VR 专业测试项目包，运行时以 `ProjectARVRPro.dll` 加载。它是接手 AR/VR 客户项目时最重要的目录之一。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `ProjectARVRPro` |
| `version` | `1.1.7.7` |
| `dllpath` | `ProjectARVRPro.dll` |
| `requires` | `1.3.15.15` |

## 业务范围

ProjectARVRPro 覆盖亮度、均匀性、色彩、FOFO 对比度、棋盘格、MTF、畸变、光学中心、OLED AOI 等测试。它以流程组和 Process 策略为核心，适合多产品、多流程、多客户协议的现场。

它和 ProjectLUX 最大的差异是通信方式和切图方式：ARVRPro 使用 JSON `EventName` 分发，支持外部系统按 `ProjectARVRInit` -> `SwitchPGCompleted` -> `ProjectARVRResult` 的节奏协同；每个步骤还可以配置 `PictureSwitchConfig`，在执行前通过雷鸟串口切换图案。

## 主要源码入口

| 文件/目录 | 作用 |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | 主测试窗口 |
| `ProjectARVRProConfig.cs` | 全局配置 |
| `PluginConfig/` | 功能启动器、菜单、窗口单例 |
| `Process/` | 测试流程框架和所有测试项 |
| `Recipe/` | 限值和一次函数修正配置 |
| `Services/SocketControl.cs` | TCP Socket 命令分发 |
| `Services/RunAllSocket.cs` | 一键执行处理 |
| `Services/SwitchGroupSocket.cs` | 外部切换流程组 |
| `SocketRelay/` | Socket 中转服务器 |
| `ObjectiveTestResult.cs` | 聚合结果模型 |
| `ViewResultManager.cs` | 本地结果查询和保存 |
| `TestResultViewWindow.xaml(.cs)` | 结果查看和导出 |

## 核心业务链路

1. 外部系统发送 `ProjectARVRInit`，或用户在窗口输入 SN。
2. 项目选择当前 `ProcessGroup`。
3. 系统找到第一个启用的 `ProcessMeta`。
4. 如启用 `PictureSwitchConfig`，先控制外部图案切换。
5. 运行绑定的 FlowEngine 模板。
6. 对应 `IProcess.Execute()` 读取 Engine 算法结果。
7. 应用 Recipe 修正和上下限判定。
8. 写入 `ObjectiveTestResult`。
9. 保存 SQLite/CSV/Text/XLSX，并通过 Socket 返回 `SwitchPG` 或 `ProjectARVRResult`。

## 流程组和步骤配置

`ProcessManager` 管理多个 `ProcessGroup`，当前激活组通过 UI 或 Socket `SwitchGroup` 切换。每个组内部是有序的 `ProcessMeta` 列表，ARVRPro 的自动流程顺序就来自这里。

| 对象 | 字段 | 说明 |
| --- | --- | --- |
| `ProcessGroup` | `Name` | 产品、机型、客户方案或调试场景 |
| `ProcessGroup` | `ProcessMetas` | 当前方案的测试步骤 |
| `ProcessMeta` | `Name` | 步骤显示名 |
| `ProcessMeta` | `FlowTemplate` | 绑定的 FlowEngine 模板 |
| `ProcessMeta` | `ProcessTypeFullName` | 绑定的结果解析策略 |
| `ProcessMeta` | `IsEnabled` | 是否参与自动链路和一键执行 |
| `ProcessMeta` | `ConfigJson` | 步骤私有配置 |
| `ProcessMeta` | `PictureSwitchConfig` | 执行前切图配置 |

持久化文件是 `ProcessGroups.json`，保存目录来自 `ViewResultManager.DirectoryPath`，默认在 `%APPDATA%\ColorVision\Config\`。旧步骤配置中如果只有 `ProcessMetas.json`，需要确认当前版本是否已经迁移到流程组结构。

## PictureSwitchConfig

`PictureSwitchConfig` 挂在每个 `ProcessMeta` 上，不是全局配置。它用于“执行该步骤前，先让外部显示设备切换到对应图案”。

| 字段 | 默认值或含义 |
| --- | --- |
| `IsEnabled` | 是否启用切图 |
| `Mode` | 当前支持 `Thunderbird` |
| `SendCommand` | 默认 `PIC1`，预置可选 `PIC1` 到 `PICD` |
| `ExpectedResponse` | 默认 `succeed` |
| `TimeoutMs` | 默认 1000 ms |
| `SuccessDelayMs` | 默认 500 ms，用于切图成功后等待稳定 |

`RunAllAsync()` 每步执行前都会调用 `ExecutePictureSwitchAsync(meta)`。如果串口未连接、返回值不匹配或超时，该步骤会失败；是否继续后续步骤由 `ProjectARVRProConfig.Instance.AllowTestFailures` 决定。

## IProcess 扩展模型

ARVRPro 的 `IProcess.Execute(ctx)` 是把 Engine 算法结果变成客户结果的业务入口。`ctx` 中至少包含批次、当前流程结果、聚合结果和图像视图。

| 方法 | 说明 |
| --- | --- |
| `Execute(ctx)` | 读取批次结果，解析算法 JSON，应用 Recipe，写入 `ObjectiveTestResult` |
| `Render(ctx)` | 可选的结果叠加或显示 |
| `GenText(ctx)` | 可选文本摘要 |
| `GetRecipeConfig()` | 返回当前测试项的 Recipe 配置 |
| `GetProcessConfig()` / `SetProcessConfig()` | 保存和恢复步骤私有配置 |

一些 MTF、Distortion、AOI 类流程会把结果写入 `ObjectiveTestResult.DynamicTestResults` 或多个列表，交接时不能只看固定属性。新增测试项时要同时补 `ObjectiveTestResult`、CSV exporter、客户协议字段。

## Socket 事件

| EventName | 作用 |
| --- | --- |
| `ProjectARVRInit` | 初始化测试并返回第一步切图信息 |
| `SwitchPGCompleted` | 外部系统确认切图完成，触发下一步 |
| `SwitchGroup` | 切换流程组 |
| `RunAll` | 一键执行所有启用流程 |
| `AOITestSwitchImageComplete` | AOI 图像切换完成中转 |

ARVRPro 的普通 Socket 入口在 `Services/SocketControl.cs`，每个事件由 `ISocketJsonHandler` 处理。

| Handler | 行为 |
| --- | --- |
| `FlowInit` | 打开窗口，调用 `InitTest(SN)`，找到第一个启用步骤，返回 `SwitchPG` |
| `SwitchPGSocket` | 外部系统确认切图完成后，调用 `ARVRWindow.SwitchPGCompleted()` |
| `SwitchGroupSocket` | 按 `request.Params` 查找流程组名并切换当前组 |
| `RunAllSocket` | 打开窗口、初始化 SN，然后异步调用 `RunAllAsync()` |
| `AOITestSwitchImageCompleteHandler` | 把外部 AOI 切图完成信号转发回 Flow |

完整外部自动化节奏是：

1. Client 发送 `ProjectARVRInit` 和 SN。
2. 软件返回 `SwitchPG`，`Data.ARVRTestType` 指示要切换的画面序号。
3. Client 完成切图后发送 `SwitchPGCompleted`。
4. 软件运行当前 Flow，调用对应 `IProcess.Execute()`。
5. 如果还有下一个启用步骤，继续返回 `SwitchPG`。
6. 全部完成后返回 `ProjectARVRResult`，`Data` 是新版 `ObjectiveTestResult` 或 Legacy 结果。

## RunAll 一键执行

`RunAllAsync()` 适合调试或不需要外部逐步确认切图的场景。它会顺序遍历当前组内所有启用的 `ProcessMeta`：

1. 调用 `InitTest()` 重置结果。
2. 查找每个步骤的 `FlowTemplate`。
3. 执行 `PictureSwitchConfig`，如启用则等待雷鸟串口返回。
4. 执行预处理并创建 MySQL 批次。
5. 启动 FlowEngine，并等待完成或 10 分钟超时。
6. 成功时调用 `Processing()`；失败时保存失败状态。
7. 如果 Socket 仍连接，最后调用 `TestCompleted()` 推送结果。

现场遇到“一键执行跑不完”时，先看 `_isRunAllRunning` 是否已有任务、`flowControl.IsFlowRun` 是否阻塞、哪个步骤的 `FlowTemplate` 或切图失败。

## SocketRelay 和 AOI 切图

`SocketRelay/` 是 ARVRPro 的特殊通信层，默认监听 `127.0.0.1:9200`。它的角色是让 Flow Engine 和外部 Client 间接通信：

| 方向 | 链路 |
| --- | --- |
| Flow 发起 AOI 切图 | Flow -> SocketRelayServer -> `SocketControl.Current.Stream` -> 外部 Client |
| Client 确认完成 | 外部 Client -> `AOITestSwitchImageComplete` -> Socket handler -> SocketRelayServer -> Flow |

中转服务器有独立配置 `SocketRelayConfig`：监听 IP、监听端口、超时和开机自启。调试 AOI 流程时，要同时看主 Socket 连接和 Relay 连接；只连 6666 不代表 AOI 中转可用。

## 结果导出

`ARVRWindow.TestCompleted()` 根据 `ViewResultManager.Config` 决定导出和 Socket 输出。

| 配置 | 作用 |
| --- | --- |
| `IsSaveCsv` | 是否保存标准 CSV |
| `CsvSavePath` | CSV 输出目录，默认桌面 `ARVR` |
| `SaveByDate` | 是否按日期创建子目录 |
| `UseLegacyARVROutput` | CSV 和 Socket `Data` 是否使用旧版扁平格式 |
| `IsSaveCustomXlsx` | 是否额外输出客户定制 XLSX |
| `CustomOutputProfile` | 定制 XLSX 的字段模板 |
| `CustomXlsxProjectName` | 定制文件名中的项目名 |

新版输出使用 `ObjectiveTestResultCsvExporter`；旧版输出通过 `LegacyARVRConverter` 和 `LegacyARVRCsvExporter`；定制 XLSX 使用 `CustomTestResultExportService`。修改字段时必须同时确认这三条输出路径。

## 交接重点

- `ProcessGroup` 是产品/场景维度的流程组织单位。
- `ProcessMeta` 决定某一步用哪个流程模板、是否启用、是否切图、用什么配置。
- 每个测试类型实现 `IProcess`，不要把客户判定逻辑写回 Engine 通用层。
- `RecipeBase` 负责上下限和 `y = Kx + B` 修正。
- `SocketRelay` 是对接外部系统时的双向转发层，和普通 Socket handler 分开看。
- `UseLegacyARVROutput` 会同时影响 CSV 和 Socket 结果格式，改字段前必须确认客户解析程序用哪一版。
- `PictureSwitchConfig` 是步骤级配置，复制流程组时要确认切图命令是否随步骤正确复制。

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 项目装载 | 检查 `manifest.json`、`ProjectARVRPro.dll` 和菜单入口 | 主程序能发现项目包，`ARVRWindow` 能打开 |
| 流程组持久化 | 新建或切换 `ProcessGroup` 后重启 | 当前组、步骤顺序、启用状态和 `ProcessGroups.json` 能恢复 |
| 初始化链路 | 发送 `ProjectARVRInit` 和 SN | 返回第一条启用步骤的 `SwitchPG`，SN 和聚合结果重置 |
| 切图确认 | 外部发送 `SwitchPGCompleted` | 当前步骤运行绑定 Flow，并调用对应 `IProcess.Execute()` |
| RunAll | 在当前组执行 `RunAll` | 所有启用步骤按顺序执行，遇到失败时遵循 `AllowTestFailures` |
| PictureSwitch | 给一个步骤启用 Thunderbird 切图 | 执行前发送 `SendCommand`，收到 `ExpectedResponse` 后等待 `SuccessDelayMs` 再运行 Flow |
| Recipe/修正 | 修改某个步骤上下限或 `K/B` | 结果值、PASS/FAIL 和窗口显示同步变化 |
| 输出格式 | 分别打开/关闭 `UseLegacyARVROutput`、`IsSaveCustomXlsx` | 标准 CSV、Legacy CSV/Socket `Data`、客户 XLSX 都符合当前配置 |
| SocketRelay/AOI | 启用 AOI 中转并执行 AOI 步骤 | 9200 Relay 能收到 Flow 请求，外部回 `AOITestSwitchImageComplete` 后流程继续 |
| 交付包 | 执行 `Scripts\package_project.bat ProjectARVRPro --no-upload` | `.cvxp` 内含 DLL、manifest、README、CHANGELOG 和必要配置说明 |

## 故障首查

| 问题 | 优先检查 |
| --- | --- |
| 初始化后没有下一步 | 当前流程组是否有启用步骤，Legacy 模式是否跳过第一个步骤 |
| 收到 `SwitchPGCompleted` 但不执行 | 项目窗口实例是否存在，当前 `CurrentTestType` 是否正确 |
| 某步骤切图失败 | 雷鸟串口连接、`SendCommand`、`ExpectedResponse`、`TimeoutMs` |
| RunAll 只跑一部分 | `AllowTestFailures`、Flow 超时、预处理失败、模板名匹配 |
| CSV 字段和客户不一致 | `UseLegacyARVROutput`、`ObjectiveTestResultCsvExporter`、定制 XLSX profile |
| AOI 流程卡住 | 6666 主 Socket、9200 Relay、外部 Client 是否每次 `AoiSwitchPG` 都回 `AOITestSwitchImageComplete` |
| `IProcess` 没有执行 | `ProcessMeta.ProcessTypeFullName` 是否能解析到当前程序集里的 `IProcess` 实现 |
| 重启后流程组丢失 | `%APPDATA%\ColorVision\Config\ProcessGroups.json` 保存路径和 JSON 兼容性 |
| 切换流程组无效 | `SwitchGroupSocket` 的 `request.Params` 是否与组名完全一致 |
| 客户只认旧格式 | `UseLegacyARVROutput` 是否打开，Legacy converter 是否覆盖新增字段 |

## 构建与交付

```powershell
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectARVRPro --no-upload
```

## 推荐阅读顺序

1. `Projects/ProjectARVRPro/README.md`
2. `ARVRWindow.xaml.cs`
3. `Process/ProcessManager.cs`
4. `Process/ProcessMeta.cs`
5. `Process/PictureSwitchConfig.cs`
6. `Recipe/RecipeManager.cs`
7. `Services/SocketControl.cs`
8. `Services/RunAllSocket.cs`
9. `SocketRelay/SocketRelayManager.cs`
10. `ObjectiveTestResult.cs`
