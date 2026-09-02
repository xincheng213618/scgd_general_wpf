---
knowledge_id: "projects.lux"
knowledge_type: "reference"
status: "current"
summary: "ProjectLUX 流程组、Recipe/Fix 共享配置、处理类型与 CSV/SQLite 结果链；文本协议有独立参考主题。"
aliases: ["T00XX 如何匹配 LUX 流程","LUX 结果和修正在哪里","ProjectLUX","LUXWindow","ProcessMeta.SocketCode","ARVRRecipe.json","ProjectARVRProFixConfig.json","ProjectLUXSummary.json","LUX Recipe","LUX Fix"]
code_paths: ["Projects/ProjectLUX/LUXWindow.xaml.cs","Projects/ProjectLUX/Services/SocketControl.cs","Projects/ProjectLUX/Process/","Projects/ProjectLUX/ViewResultManager.cs","Projects/ProjectLUX/Recipe/","Projects/ProjectLUX/Fix/","Projects/ProjectLUX/Summary.cs"]
test_paths: ["Test/ProjectLUX.Tests/ProjectLUX.Tests.csproj"]
related: ["projects.index","projects.capabilities","projects.lux-protocol","projects.arvr-pro-processes","ui.socket-protocol"]
---

# ProjectLUX

`Projects/ProjectLUX/` 是亮度、色彩、对比度、MTF、畸变、光学中心、VID、光通量等光学测试项目包，运行时加载 `ProjectLUX.dll`。它以文本 Socket 命令 `T00XX,SN;` 和流程组配置为核心。

## 故障定位

| 现象 | 第一检查点 |
| --- | --- |
| Socket 命令没有触发 | 当前活动组、`ProcessMeta.SocketCode`、窗口单例 |
| 找不到流程模板 | `ProcessMeta.FlowTemplate` 是否匹配 `TemplateFlow.Params` |
| 运行了错误步骤 | 活动组是否正确，是否多个步骤复用同一 `SocketCode` |
| CSV 没生成 | `ProjectLUXConfig.Instance.ResultSavePath` 是否存在且可写 |
| 结果全部失败 | Recipe 上下限、Fix 系数、`Process.Execute()` 读取字段 |
| VID 或光通量无响应 | 相机/光谱仪服务是否在线，专用命令链是否可用 |
| 重启后流程丢失 | `%APPDATA%\ColorVision\Config\ProcessGroups.json` 是否保存 |

## 项目入口与运行链路

| 项 | 当前值 |
| --- | --- |
| `Id` / `dllpath` | `ProjectLUX` / `ProjectLUX.dll` |
| 版本与宿主要求 | 读取 `ProjectLUX.csproj` 和 `manifest.json` |
| 主窗口 | `LUXWindow.xaml.cs` |
| 流程 | `Process/`、`ProcessGroup`、`ProcessMeta` |
| 判定和修正 | `Recipe/`、`Fix/` |
| Socket | `Services/SocketControl.cs` |
| 结果 | `ObjectiveTestResult.cs`、`ViewResultManager.cs` |

普通流程由当前 `ProcessGroup` 和步骤的 `FlowTemplate` 绑定 Engine Flow。Flow 完成后读取批次和算法结果，`IProcess.Execute()` 应用 Fix 修正和 Recipe 限值，写入聚合结果与 SQLite，再导出 CSV 并返回客户响应。Socket 入口先解析命令和 SN，再分派到下表路径；`T0000` 仅握手，`T0001` 与 `T0031` 分别使用相机和光谱仪专用链，不能把所有命令都理解为初始化后运行同一条流程。

## 配置流程与外部命令

1. 准备匹配现场设备的 Flow 模板，打开 `ProcessManager` 并选择活动流程组。
2. 为步骤设置显示名称、`FlowTemplate`、处理类型和 `SocketCode`；同一活动组的命令码保持唯一。
3. 按对应类型编辑 Recipe 限值与 Fix 修正，按步骤编辑 Process 私有参数。确认改动的共享范围后再保存。
4. 外部对接时在“通信协议”中选择 Text 模式，启用 Socket Server，并将实际命令映射交给客户端。
5. 在授权环境核对触发的 Flow、结果字段、CSV 和响应；确认持久化成功后再将方案投入使用。

下面的命令说明用于定位实现；完整请求、响应字段、共享会话限制及异步时序见 [LUX TCP 通讯协议](./project-lux-protocol.md)。


| 对象或命令 | 作用 |
| --- | --- |
| `ProcessGroup.Name` | 产品、机型或场景 |
| `ProcessGroup.ProcessMetas` | 当前组内有序步骤 |
| `ProcessMeta.FlowTemplate` | 要运行的 Flow 模板名 |
| `ProcessMeta.SocketCode` | 文本协议 `T00XX` 的 `XX` |
| `ProcessMeta.ProcessTypeFullName` | 结果解析和判定策略 |
| `ProcessMeta.ConfigJson` | 单步骤私有配置 |
| `T0000` | 设置 SN 并返回握手 ACK，保留累计测试结果 |
| `T0001` | VID，调用相机/自动对焦链，输出 `B_<SN>.csv` |
| `T0002` | 按 `SocketCode == "02"` 运行；`MachineNO == "H03AR"` 使用专用 OC 响应前缀 |
| `T0031` | 光通量，调用光谱仪，输出 `D_<SN>.csv` |
| `T00XX` | 在当前活动组查找 `SocketCode == XX` 的步骤并运行 |

`FindProcessMetaBySocketCode` 在活动组中按忽略大小写的精确命令码取第一项，不检查 `IsEnabled`。UI 中禁用步骤不能阻止这条 Socket 路径触发它。找到步骤后，`RunTemplateBySocketCode` 用 `Contains` 查找第一个包含 `FlowTemplate` 字符串的模板名，因此应使用完整且有辨识力的名称，避免短名命中其他模板。

Flow 已运行时，新的流程启动被忽略并记日志，但命令入口此前可能已经覆盖窗口中的 SN、Stream 和 ReturnCode。活动组、命令映射或模板不存在时，也可能返回现有响应前缀。收到通用 `00` 不能证明 Flow 已执行或 Recipe 判定 PASS；按协议串行操作并结合日志及结果记录确认。

## Process / Recipe / Fix

| 部分 | 职责 |
| --- | --- |
| `Process` | 从 Engine 批次结果读取算法输出，写入项目结果 |
| `TestResult` | 表示该测试项输出字段 |
| `RecipeConfig` | `RecipeManager` 按配置类型共享上下限 |
| `FixConfig` | `FixManager` 按配置类型共享校准或修正系数 |
| `ProcessConfig` | 单步骤私有行为参数，保存在 `ConfigJson` |

修改判定规则先改 Recipe，修改校准系数改 Fix，解析行为由 Process 或 ProcessConfig 定义。多个步骤读取相同配置类型时会使用同一 Recipe/Fix 对象；它们不随步骤复制成为独立限值。ARVRPro 的实例 Recipe 规则见[对应配置主题](./project-arvr-pro-processes.md)，不能用于解释 LUX 的保存行为。

## 配置与结果存储

以下文件默认位于 `%APPDATA%\ColorVision\Config\`，各管理器路径属性可以由宿主代码设置。

| 文件 | 所有者 / 内容 |
| --- | --- |
| `ProcessGroups.json` | `ProcessManager`：活动组、步骤、SocketCode 和步骤 ConfigJson |
| `ARVRRecipe.json` | `RecipeManager`：各类型的限值配置 |
| `ProjectARVRProFixConfig.json` | `FixManager`：各类型的修正配置；这是 LUX 实际使用的文件名 |
| `ProjectLUXSummary.json` | `SummaryManager`：设备号、产线、工人和生产摘要 |
| `ProjectLUX.db` | `ViewResultManager`：本地流程与聚合结果 |

CSV 写入 `ProjectLUXConfig.ResultSavePath`，普通流程、VID 和光通量分别使用 `C_<SN>.csv`、`B_<SN>.csv` 和 `D_<SN>.csv`。Engine 原始批次与算法数据仍在 MySQL，保存本地结果不等于备份完整 Engine 数据。

流程配置只在 `ProcessGroups.json` 不存在时从 `ProcessMetas.json` 迁移；新格式损坏不会自动回退旧文件。LUX 与 ARVRPro 默认使用同名的 `ProcessGroups.json`，内容和类型属于各自项目，不能直接跨项目互换。

这些管理器使用直接文件写入，保存异常主要记入日志，不提供跨文件事务。界面值改变或编辑窗口关闭不证明所有文件已保存。Recipe/Fix 初始化时若发现已存配置数量与当前发现的类型数量不同，会在内存中重建整组默认配置；升级后限值异常时应先保留原文件并核对类型及日志，不以“文件存在”判断原限值已成功加载。

## 内置处理类型

下表将测试能力定位到 `Projects/ProjectLUX/Process/` 中的实现。具体测量点、单位和结果字段由该类型及 Flow 输出定义；同名的 ARVRPro 类型不是同一实现。

| 能力 | 类型 / 位置 |
| --- | --- |
| 白场亮色度、均匀性与 FOV | `White255Process`，`W255/` |
| AR 白场 | `White255ARProcess` / `W51ARProcess`，`AR/` |
| RGB 色度 | `RedProcess` / `GreenProcess` / `BlueProcess` |
| 棋盘格对比度 | `ChessboardProcess` / `Chessboard55Process`；AR 使用 `ChessboardARProcess` |
| 通用与 AR MTF | `MTFHVProcess` / `MTFHVARProcess` |
| VR 条纹 MTF | `VRMTFHProcess` / `VRMTFVProcess`，`VR/` |
| 畸变 | `DistortionProcess`，`Distortion/` |
| 光学中心 | `OpticCenterProcess`，`OpticCenter/` |
| VID 虚像距 | `Services/SocketControl.cs` 的 `T0001` 分支，使用 `VID/` 的限值与修正模型 |
| 光通量 | `Services/SocketControl.cs` 的 `T0031` 分支，调用光谱仪 |
| 空处理 | `BlankProcess`，`Blank/` |

## 验证范围

`Test/ProjectLUX.Tests/` 中的 `LUXWindowLifecycleTests`、`ImageExportAlignmentTests` 和 `DrawingOverlayCompatibilityTests` 分别覆盖窗口生命周期、图像导出及绘制兼容。当前没有登记上述 Socket 命令、Recipe/Fix 持久化与类型数量变化的专门自动化测试；相关代码说明需与现场命令和配置验证分开评估。

## 本地构建与测试

以下命令编译和运行本地测试，不上传包；会写入本地构建/测试产物。发送 `T00XX` 会推进真实测试，必须单独确认现场操作授权。

```powershell
dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64
dotnet test Test/ProjectLUX.Tests/ProjectLUX.Tests.csproj -c Release -p:Platform=x64
```

## 打包上传（需明确发布授权）

只有明确要求发布 ProjectLUX 时执行。wrapper 会构建、打包上传并清理本地 `.cvxp`，不支持 `--no-upload`；本地编译成功不等于远端发布完成。

```powershell
.\Scripts\package_project.bat ProjectLUX
```

## 现场验收边界

硬件命令和配置修改只在获授权的测试环境执行。

| 验收项 | 通过标准 |
| --- | --- |
| 项目装载 | 主程序发现项目包，`LUXWindow` 能打开 |
| 流程组持久化 | 当前组、步骤、`SocketCode`、Recipe/Fix 重启后恢复 |
| Socket 握手 | `T0000,SN;` 返回可解析响应 |
| SocketCode 执行 | `T00XX,SN;` 能运行当前组对应 Flow |
| VID/光通量 | `T0001` 生成 `B_<SN>.csv`，`T0031` 生成 `D_<SN>.csv` |
| Flow 结果 | `IProcess.Execute()` 写入聚合结果和 SQLite |
| Recipe/Fix | 最终值、PASS/FAIL、CSV 和窗口显示一致 |
| 输出 | 普通流程生成 `C_<SN>.csv`，报告/CSV 可追溯 |
| 交付包 | `.cvxp` 内含 DLL、manifest、README、CHANGELOG 和配置说明 |
