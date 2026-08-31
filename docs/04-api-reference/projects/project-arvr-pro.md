---
knowledge_id: "projects.arvr-pro"
knowledge_type: "reference"
status: "current"
summary: "ARVRPro 流程组、Recipe、Socket、输出与历史结果图原图到保存原图再到标记图的回退契约。"
aliases: ["ARVR 历史原图删了还能看结果吗","保存结果图会不会重复画标记","ProjectARVRPro","ResultImageFileCandidates","SavedSourceImageFileName","SavedResultImageFileName"]
code_paths: ["Projects/ProjectARVRPro/ARVRWindow.xaml.cs","Projects/ProjectARVRPro/ResultImagePresentation.cs","Projects/ProjectARVRPro/ProjectARVRReuslt.cs","Projects/ProjectARVRPro/ViewResultManager.cs","Projects/ProjectARVRPro/Services/SocketControl.cs"]
test_paths: ["Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj","Test/ProjectARVRPro.Tests/ResultImagePresentationTests.cs","Test/ProjectARVRPro.Tests/ResultJsonPayloadStorageTests.cs"]
related: ["projects.index","projects.arvr-pro-demo","projects.capabilities"]
---

# ProjectARVRPro

`Projects/ProjectARVRPro/` 是当前主力 AR/VR 专业测试项目包，运行时以 `ProjectARVRPro.dll` 加载。维护时优先看流程组、Socket 自动化、切图、Recipe 和输出格式。

## 先查什么

| 现场问题 | 第一检查点 |
| --- | --- |
| 项目包没出现 | `manifest.json`、`ProjectARVRPro.dll`、插件目录、主程序版本要求 |
| 初始化后没有下一步 | 当前 `ProcessGroup` 是否有启用的 `ProcessMeta` |
| 外部系统触发无反应 | Socket 服务、`EventName`、项目 handler 是否加载 |
| 切图失败 | `PictureSwitchConfig`、雷鸟串口、返回值和超时 |
| RunAll 只跑一部分 | `AllowTestFailures`、Flow 模板名、切图和预处理错误 |
| CSV 或 Socket 字段不对 | `UseLegacyARVROutput`、标准 CSV、Legacy 输出、客户 XLSX |
| AOI 流程卡住 | 主 Socket、`SocketRelay`、`AOITestSwitchImageComplete` |
| 重启后配置丢失 | `%APPDATA%/ColorVision/Config/ProcessGroups.json` 和 Recipe 配置 |

## 项目边界和版本

| 项目 | 通信方式 | 流程组织 | 典型风险 |
| --- | --- | --- | --- |
| `ProjectARVRPro` | JSON `EventName` | `ProcessGroup` + `ProcessMeta` | 切图、Legacy 输出、SocketRelay |
| `ProjectLUX` | 文本命令 | 流程组 + `SocketCode` | 文本返码、客户命令映射 |

客户项目判定逻辑应留在 `Projects/ProjectARVRPro/Process/` 和 Recipe 体系里，不要回写到 Engine 通用模板或 UI 基础库。插件版本和最低宿主要求以 [manifest.json](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Projects/ProjectARVRPro/manifest.json) 为准，程序集版本以 [ProjectARVRPro.csproj](https://github.com/xincheng213618/scgd_general_wpf/blob/master/Projects/ProjectARVRPro/ProjectARVRPro.csproj) 的 `VersionPrefix` 为准；发包前确认二者同步。

## 主链路

外部系统发送 `ProjectARVRInit`，或用户在窗口输入 SN 后，`ARVRWindow` 选择当前 `ProcessGroup` 并找到下一个启用的 `ProcessMeta`。步骤启用 `PictureSwitchConfig` 时先切图，再运行绑定的 FlowEngine 模板。对应 `IProcess.Execute(ctx)` 读取 Engine 结果并应用 Recipe，最后写入 `ObjectiveTestResult`，按配置保存 SQLite、CSV、Legacy CSV、客户 XLSX，并通过 Socket 返回下一步或最终结果。

## 关键目录和配置

| 目录/文件 | 作用 |
| --- | --- |
| `ARVRWindow.xaml.cs` | 主窗口、初始化、单步执行、RunAll、结果完成 |
| `ProjectARVRProConfig.cs` | 全局运行配置，例如 SN、重试、失败策略 |
| `Process/` | `IProcess`、流程组、流程步骤、各测试项解析 |
| `Recipe/` | 限值和 `y = Kx + B` 修正 |
| `Services/SocketControl.cs` | `ProjectARVRInit`、`SwitchPGCompleted` 等 JSON handler |
| `Services/RunAllSocket.cs` | Socket 触发一键执行 |
| `Services/SwitchGroupSocket.cs` | 外部切换流程组 |
| `SocketRelay/` | AOI Flow 与外部 Client 的中转层 |
| `ObjectiveTestResult.cs` | 聚合结果模型 |
| `ViewResultManager.cs` | 本地结果、SQLite、CSV 和输出配置 |
| `TestResultViewWindow.xaml.cs` | 结果查看和导出 |

`ProcessGroup` 是产品或场景方案，`ProcessMeta` 是单步测试。每步至少确认 `FlowTemplate`、`ProcessTypeFullName`、`IsEnabled`、`ConfigJson` 和 `PictureSwitchConfig`；复制流程组时重新核对 `SendCommand`、`ExpectedResponse`、`TimeoutMs`、`SuccessDelayMs`。

## Socket 自动化

ARVRPro 通过 `ColorVision.SocketProtocol` 的 JSON 模式接入外部系统。常规节奏是 `ProjectARVRInit` 初始化，软件返回 `SwitchPG`，外部切图后发 `SwitchPGCompleted`，软件运行当前 Flow 和 `IProcess`，全部完成后返回 `ProjectARVRResult`。

| EventName | 作用 |
| --- | --- |
| `ProjectARVRInit` | 初始化测试并返回第一步切图信息 |
| `SwitchPGCompleted` | 外部确认切图完成，触发当前步骤 |
| `SwitchGroup` | 切换当前流程组 |
| `RunAll` | 一键执行当前组内启用步骤 |
| `AOITestSwitchImageComplete` | AOI 切图完成信号，经 Relay 回给 Flow |

AOI 相关流程还要看 `SocketRelay/`。只连通主 Socket 端口不代表 Relay 已经可用。

## 输出和兼容

结果输出由 `ViewResultManager.Config` 控制，覆盖 SQLite、标准 CSV、Legacy CSV、客户 XLSX 和 Socket `ProjectARVRResult.Data`。`UseLegacyARVROutput` 会影响 CSV 和 Socket `Data`，改字段前先确认客户解析程序使用新版还是旧版。

## 历史结果图回退与持久化

历史记录的原始图像不在原路径时，不应立即判定“无法查看结果”。`Projects/ProjectARVRPro/ResultImagePresentation.cs` 中 `ResultImageFileCandidates.GetExisting` 按下列顺序收集存在且去重的路径，`OpenFirstAsync` 在解码失败、超时或未得到图像时继续尝试下一候选；取消仍中止当前请求。

| 顺序 | 字段/条件 | 显示规则 |
| --- | --- | --- |
| 1 | 原始 `FileName` | 打开原图并按历史结果重新绘制 overlay |
| 2 | `SavedSourceImageFileName` | 打开保存的原位深原图并重新绘制 overlay |
| 3 | `SavedResultImageFileName` | 直接显示已经含标记的结果图，`RequiresOverlayRendering=false`，不能重复绘制 overlay |
| 4 | 候选均不可用，但能确认结果宽高 | 使用相同尺寸的白色画布承载历史标记 |
| 5 | 候选均不可用且尺寸未知 | 清除旧底图并记录失败，不能继续显示上一条结果的图 |

`ARVRWindow.xaml.cs` 负责实际加载、请求版本仲裁和 `RenderResultImage` / `ShowSavedResultImage` 分流。两个保存路径是 `ProjectARVRReuslt` 的可空列；`ViewResultManager` 通过现有 `CodeFirst.InitTables<ProjectARVRReuslt, ObjectiveTestResultRecord>()` 为旧库补列，不能要求用户删库或手工编辑 SQLite。旧磁盘 PNG 不会被自动扫描回填；只有实际成功导出的通道才更新对应路径。

验证入口：`Test/ProjectARVRPro.Tests/ResultImagePresentationTests.cs` 覆盖候选顺序、缺失/重复路径、首图加载失败后继续及标记图不重复绘制契约；`ResultJsonPayloadStorageTests.cs` 覆盖结果持久化相关兼容。自动测试不等于已验证现场历史文件仍存在，现场排查还须只读核对记录路径与文件可读性。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 项目装载 | 菜单入口出现，`ARVRWindow` 能打开 |
| 流程组 | 切换、保存、重启后步骤顺序和启用状态恢复 |
| Socket 初始化 | `ProjectARVRInit` 返回第一条启用步骤的 `SwitchPG` |
| 切图确认 | `SwitchPGCompleted` 后运行绑定 Flow 和 `IProcess` |
| RunAll | 当前组启用步骤按顺序执行，失败策略符合配置 |
| Recipe | 限值、修正、PASS/FAIL 和窗口显示一致 |
| 输出 | SQLite、CSV、Legacy、客户 XLSX、Socket 结果都符合当前配置 |
| AOI Relay | Flow 请求、外部确认、Relay 转发三段都可追踪 |
| 交付包 | `.cvxp` 内含 DLL、manifest、README、CHANGELOG |

## 本地构建与测试

下列命令编译和运行本地测试，会写入本地构建/测试产物，不上传包。项目版本读取 `ProjectARVRPro.csproj` 的 `VersionPrefix`，与主程序版本独立。

```powershell
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -c Release -p:Platform=x64
dotnet test Test/ProjectARVRPro.Tests/ProjectARVRPro.Tests.csproj -c Release -p:Platform=x64
```

## 打包上传（需明确发布授权）

只有明确要求发布 ProjectARVRPro 时才运行以下命令。wrapper 会重新构建、生成并上传 `.cvxp`，随后清理本地包；不支持 `--no-upload`。打包器由主 DLL 的 `FileVersion` 同步 manifest 版本，不要另行手工同步 manifest。发布完成还需核对远端元数据和可下载包，不能以本地 build 成功代替。

```powershell
.\Scripts\package_project.bat ProjectARVRPro
```
