# ProjectLUX

`Projects/ProjectLUX/` 是亮度、色彩、对比度、MTF、畸变等光学参数自动化测试项目包，运行时以 `ProjectLUX.dll` 加载。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `ProjectLUX` |
| `version` | `1.0` |
| `dllpath` | `ProjectLUX.dll` |
| `requires` | `1.3.15.10` |

## 业务范围

ProjectLUX 适用于 AR/VR 头显、显示面板等设备，支持 W255、W51、RGB、棋盘格、MTF、畸变、光学中心、VID、光通量等测试。

它和 ARVRPro 最大的差异是通信方式：ProjectLUX 以文本命令 `T00XX,SN;` 为主，`XX` 会映射到当前流程组中某个 `ProcessMeta.SocketCode`。因此 LUX 的交接重点不是只看 FlowTemplate，而是同时看 SocketCode、当前流程组和客户返码。

## 主要源码入口

| 文件/目录 | 作用 |
| --- | --- |
| `LUXWindow.xaml(.cs)` | 主测试窗口 |
| `ProjectLUXConfig.cs` | 项目配置 |
| `PluginConfig/` | 功能启动器、菜单、窗口单例 |
| `Process/` | 测试流程框架和测试项 |
| `Recipe/` | 限值配置 |
| `Fix/` | 修正因子配置 |
| `Services/SocketControl.cs` | TCP 文本命令分发 |
| `ObjectiveTestResult.cs` | 聚合结果模型 |
| `ViewResultManager.cs` | SQLite 结果管理 |
| `TestResultViewWindow.xaml(.cs)` | 结果查看和导出 |

## 核心业务链路

1. 输入 SN 或接收 `T00XX,SN;` Socket 命令。
2. 初始化结果目录和 `ObjectiveTestResult`。
3. 选择当前 `ProcessGroup` 和步骤。
4. 根据步骤的 `FlowTemplate` 运行 Engine Flow。
5. Flow 完成后从批次和算法结果读取数据。
6. 对应 `IProcess.Execute()` 应用 Fix 修正和 Recipe 限值。
7. 写入 `ObjectiveTestResult` 和 SQLite。
8. 导出 CSV/PDF，并按命令返回客户响应。

## 流程组和步骤配置

`ProcessManager` 是 LUX 流程配置的核心单例。它会扫描已加载程序集中的 `IProcess` 实现，维护多个 `ProcessGroup`，并把当前活动组暴露给主窗口。

| 对象 | 字段 | 说明 |
| --- | --- | --- |
| `ProcessGroup` | `Name` | 产品、机型或场景名称 |
| `ProcessGroup` | `ProcessMetas` | 当前组内有序测试步骤 |
| `ProcessMeta` | `Name` | 步骤显示名 |
| `ProcessMeta` | `FlowTemplate` | 要运行的 FlowEngine 模板名 |
| `ProcessMeta` | `Process` / `ProcessTypeFullName` | 结果解析和判定策略 |
| `ProcessMeta` | `IsEnabled` | 是否启用该步骤 |
| `ProcessMeta` | `SocketCode` | 文本协议 `T00XX` 的 `XX` 部分 |
| `ProcessMeta` | `ConfigJson` | 该步骤私有配置 |

流程组持久化文件是 `ProcessGroups.json`，旧版步骤文件是 `ProcessMetas.json`。两者保存目录来自 `ViewResultManager.DirectoryPath`，默认在 `%APPDATA%\ColorVision\Config\`。新增、复制、重命名或切换组后，都要确认该文件能保存并在重启后恢复。

## IProcess 扩展模型

LUX 的 `IProcess` 除了 `Execute`、`Render`、`GenText`，还显式支持 `GetRecipeConfig()` 和 `GetFixConfig()`。这意味着一个测试项通常由四部分组成：

| 部分 | 职责 |
| --- | --- |
| `Process` | 从 Engine 批次结果读取算法输出，写入项目结果 |
| `TestResult` | 表示该测试项的输出字段 |
| `RecipeConfig` | 上下限、规格判定 |
| `FixConfig` | 校准或修正系数 |

`ProcessBase<TConfig>` 还支持类型化步骤配置。编辑步骤配置时，`ProcessMeta` 会打开 PropertyGrid，保存后把配置序列化到 `ConfigJson`；加载流程组时再通过 `SetProcessConfig()` 恢复。新增带配置的测试项时，必须保持配置类字段向后兼容。

## Socket 命令

| 命令 | 作用 |
| --- | --- |
| `T0000` | 握手/初始化 |
| `T0001` | VID 虚像距 |
| `T0002` | 光学中心 |
| `T0031` | 光通量 |
| `T00XX` | 按 `SocketCode` 匹配流程执行 |

处理入口是 `Services/SocketControl.cs`。请求会先拆成 `code` 和 `arg`，`arg` 作为 SN 写入 `ProjectLUXConfig.Instance.SN`。当命令以 `T00` 开头时，系统取最后两位作为 `lastTwo`，拼出客户返码前缀，并按不同命令进入专用逻辑。

| 路径 | 行为 | 结果 |
| --- | --- | --- |
| `lastTwo == "31"` | 调用光谱仪 `DeviceSpectrum.DService.GetData()` | 导出 `D_<SN>.csv`，返回光通量 |
| `lastTwo == "01"` | 调用相机自动对焦获取 VID | 导出 `B_<SN>.csv`，返回 VID 值 |
| `lastTwo == "02"` | AR 机台下执行光学中心流程 | 通过 `RunTemplateBySocketCode("02")` 触发 |
| 其他 `XX` | 在当前活动组查找 `SocketCode == XX` 的步骤 | 运行对应 FlowTemplate |

如果没有活动组、找不到 SocketCode、找不到 FlowTemplate，窗口会记录错误并尽量把当前 `ReturnCode` 写回 Socket。现场排查时先看当前组是否正确，再看 `SocketCode` 是否和客户命令一致。

## 结果处理和导出

Flow 完成后，`LUXWindow.Processing()` 会根据 `CurrentFlowResult.Model` 匹配 `ProcessMeta.FlowTemplate`，创建 `IProcessExecutionContext`，再调用 `meta.Process.Execute(ctx)`。

`IProcessExecutionContext` 里关键对象包括：

| 对象 | 说明 |
| --- | --- |
| `Batch` | MySQL 批次记录，用来查 Engine 算法结果 |
| `Result` | 当前流程的 `ProjectLUXReuslt` |
| `ObjectiveTestResult` | 一轮测试的聚合结果 |
| `FixConfig` | 全局修正配置 |
| `RecipeConfig` | 全局规格配置 |
| `ImageView` | 可选图像显示上下文 |
| `Logger` | 项目日志 |

每次 `IProcess.Execute()` 成功后，系统会尝试导出 `C_<SN>.csv` 到 `ProjectLUXConfig.Instance.ResultSavePath`，并把过程结果写入 `ProjectLUX.db`。VID 和光通量走 SocketControl 内的专用 CSV：`B_<SN>.csv` 和 `D_<SN>.csv`。

## Recipe 和 Fix

LUX 同时使用 Recipe 和 Fix：

| 配置 | 作用 | 维护入口 |
| --- | --- | --- |
| `RecipeBase` | `Min` / `Max` 上下限 | `Recipe/`、步骤的 Recipe 编辑命令 |
| `FixConfig` | 各测试项修正系数 | `Fix/`、步骤的 Fix 编辑命令 |
| `ProcessConfig` | 单步骤行为参数 | `ProcessMeta.ConfigJson` |

修改判定规则时优先改 Recipe；修改校准系数时改 Fix；修改某个步骤如何解析或映射结果时才改 ProcessConfig 或 Process 代码。三者混在一起会让现场很难判断问题属于规格、校准还是代码逻辑。

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 项目装载 | 检查 `manifest.json`、`ProjectLUX.dll` 和菜单入口 | 主程序能发现项目包，`LUXWindow` 能打开 |
| 流程组持久化 | 新建/切换流程组后重启 | 当前组、步骤、`SocketCode`、Recipe/Fix 配置能恢复 |
| 文本 Socket 握手 | 发送 `T0000,SN;` 或现场握手命令 | Socket 服务返回可解析响应，不误触发普通流程 |
| SocketCode 执行 | 发送一个 `T00XX,SN;` | 能在当前组找到 `SocketCode == XX` 的步骤并运行对应 Flow |
| VID 专用命令 | 发送 `T0001,SN;` | 调用相机/自动对焦链路，生成 `B_<SN>.csv` |
| 光通量专用命令 | 发送 `T0031,SN;` | 调用光谱仪链路，生成 `D_<SN>.csv` |
| Flow 结果处理 | 手动或 Socket 运行普通步骤 | `IProcess.Execute()` 写入 `ObjectiveTestResult` 和 `ProjectLUX.db` |
| Recipe/Fix | 修改上下限和修正系数后复测 | 最终值、PASS/FAIL、CSV 和窗口显示一致 |
| CSV/PDF 输出 | 检查 `ResultSavePath` | 普通流程生成 `C_<SN>.csv`，客户要求的 PDF/CSV 可追溯 |
| 交付包 | 执行 `Scripts\package_project.bat ProjectLUX --no-upload` | `.cvxp` 内含 DLL、manifest、README、CHANGELOG 和配置说明 |

## 故障首查

| 问题 | 优先检查 |
| --- | --- |
| Socket 命令没有触发测试 | 当前活动组、`ProcessMeta.SocketCode`、`ProjectWindowInstance.WindowInstance` |
| 提示找不到流程模板 | `ProcessMeta.FlowTemplate` 是否能匹配 `TemplateFlow.Params` |
| CSV 没生成 | `ProjectLUXConfig.Instance.ResultSavePath` 是否存在且可写 |
| 结果全部失败 | Recipe 上下限、Fix 系数、对应 `Process.Execute()` 读取的算法字段 |
| 光谱或 VID 无响应 | `DeviceSpectrum` / `DeviceCamera` 服务是否在线，模板索引是否正确 |
| 重启后流程丢失 | `%APPDATA%\ColorVision\Config\ProcessGroups.json` 是否保存成功 |
| 客户返码异常 | `Services/SocketControl.cs` 的 `ReturnCode`、命令 `lastTwo` 和客户协议字段 |
| 运行了错误步骤 | 当前活动组是否正确，多个步骤是否重复使用同一个 `SocketCode` |
| 结果值和客户不一致 | Fix 是否参与修正、Recipe 是否启用、ProcessConfig 是否从 `ConfigJson` 正确恢复 |
| 数据库没有记录 | `ViewResultManager`、`ProjectLUX.db` 路径、`Processing()` 是否在 Flow 完成后被调用 |

## 构建与交付

```powershell
dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectLUX --no-upload
```

## 推荐阅读顺序

1. `Projects/ProjectLUX/README.md`
2. `LUXWindow.xaml.cs`
3. `Process/ProcessManager.cs`
4. `Process/ProcessMeta.cs`
5. `Process/IProcess.cs`
6. `Recipe/RecipeManager.cs`
7. `Fix/FixManager.cs`
8. `Services/SocketControl.cs`
9. `ObjectiveTestResult.cs`

## 交接注意事项

- `Process/` 是业务核心，新增测试项优先按现有 Process/Recipe/Fix 模式扩展。
- Socket 是文本协议，和 ARVRPro 的 JSON 协议不同。
- 修改返回码或 `SocketCode` 时必须同步客户设备协议。
- 不要只改 `FlowTemplate` 名称；如果该流程由外部命令触发，还要同步 `SocketCode`。
- LUX 的 `FixConfig` 会参与最终值修正，现场校准问题不要误判成算法问题。
