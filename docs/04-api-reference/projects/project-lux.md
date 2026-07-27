# ProjectLUX

`Projects/ProjectLUX/` 是亮度、色彩、对比度、MTF、畸变、光学中心、VID、光通量等光学测试项目包，运行时加载 `ProjectLUX.dll`。它以文本 Socket 命令 `T00XX,SN;` 和流程组配置为核心。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| Socket 命令没有触发 | 当前活动组、`ProcessMeta.SocketCode`、窗口单例 |
| 找不到流程模板 | `ProcessMeta.FlowTemplate` 是否匹配 `TemplateFlow.Params` |
| 运行了错误步骤 | 活动组是否正确，是否多个步骤复用同一 `SocketCode` |
| CSV 没生成 | `ProjectLUXConfig.Instance.ResultSavePath` 是否存在且可写 |
| 结果全部失败 | Recipe 上下限、Fix 系数、`Process.Execute()` 读取字段 |
| VID 或光通量无响应 | 相机/光谱仪服务是否在线，专用命令链是否可用 |
| 重启后流程丢失 | `%APPDATA%\ColorVision\Config\ProcessGroups.json` 是否保存 |

## manifest 和链路

| 项 | 当前值 |
| --- | --- |
| `Id` / `version` / `dllpath` | `ProjectLUX` / `1.0` / `ProjectLUX.dll` |
| `requires` | `1.3.15.10` |
| 主窗口 | `LUXWindow.xaml.cs` |
| 流程 | `Process/`、`ProcessGroup`、`ProcessMeta` |
| 判定和修正 | `Recipe/`、`Fix/` |
| Socket | `Services/SocketControl.cs` |
| 结果 | `ObjectiveTestResult.cs`、`ViewResultManager.cs` |

用户输入 SN，或外部发送 `T00XX,SN;` 后，项目初始化结果目录和 `ObjectiveTestResult`，选择当前 `ProcessGroup` 和启用步骤，按 `FlowTemplate` 运行 Engine Flow。Flow 完成后读取批次和算法结果，`IProcess.Execute()` 应用 Fix 修正和 Recipe 限值，写入 `ObjectiveTestResult` / `ProjectLUX.db`，再导出 CSV 并按 Socket 命令返回客户响应。

## 流程组和 Socket

| 对象或命令 | 作用 |
| --- | --- |
| `ProcessGroup.Name` | 产品、机型或场景 |
| `ProcessGroup.ProcessMetas` | 当前组内有序步骤 |
| `ProcessMeta.FlowTemplate` | 要运行的 Flow 模板名 |
| `ProcessMeta.SocketCode` | 文本协议 `T00XX` 的 `XX` |
| `ProcessMeta.ProcessTypeFullName` | 结果解析和判定策略 |
| `ProcessMeta.ConfigJson` | 单步骤私有配置 |
| `T0000` | 握手/初始化 |
| `T0001` | VID，调用相机/自动对焦链，输出 `B_<SN>.csv` |
| `T0002` | 光学中心，走 `RunTemplateBySocketCode("02")` |
| `T0031` | 光通量，调用光谱仪，输出 `D_<SN>.csv` |
| `T00XX` | 在当前活动组查找 `SocketCode == XX` 的步骤并运行 |

新建、复制、重命名或切换流程组后，要确认 `ProcessGroups.json` 能保存并在重启后恢复。旧 `ProcessMetas.json` 只是迁移来源。

## Process / Recipe / Fix

| 部分 | 职责 |
| --- | --- |
| `Process` | 从 Engine 批次结果读取算法输出，写入项目结果 |
| `TestResult` | 表示该测试项输出字段 |
| `RecipeConfig` | 上下限和 PASS/FAIL 判定 |
| `FixConfig` | 校准或修正系数 |
| `ProcessConfig` | 单步骤私有行为参数，保存在 `ConfigJson` |

修改判定规则先改 Recipe；修改校准系数改 Fix；只有解析逻辑变化才改 Process 或 ProcessConfig。

## 构建和验收

```powershell
dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectLUX
```

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
