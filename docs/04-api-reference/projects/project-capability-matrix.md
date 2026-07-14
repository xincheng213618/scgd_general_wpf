# 项目横向速查

本页给维护人员做横向排查，不作为普通读者入口。第一次找客户项目请先看 [项目说明](../../00-projects/README.md) 和 [项目包总览](./README.md)，再进入具体项目页；只有需要比较协议、触发方式、结果出口、流程组织或发版验收时，再回到本页。

它回答三个问题：这个项目解决什么现场问题，外部系统怎么触发，结果最终交给谁。

## 项目包总表

| 项目 | 业务定位 | 外部触发 | 主要结果 | 最先看的代码 |
| --- | --- | --- | --- | --- |
| `ProjectARVRLite` | 轻量 AR/VR 快速测试，可配置启用项 | Socket JSON，初始化时可自动开窗 | `ObjectiveTestResult`、按 SN/日期保存 CSV、Socket 结果 | `ARVRWindow.xaml.cs`、`TestTypeConfig.cs`、`Services/SocketControl.cs` |
| `ProjectARVRPro` | 当前主力 AR/VR 专业流程组项目 | Socket JSON、`RunAll`、`SwitchGroup`、雷鸟串口切图、AOI Relay | SQLite、CSV、Legacy CSV、客户 XLSX、Socket 结果 | `ARVRWindow.xaml.cs`、`Process/`、`Recipe/`、`Services/SocketControl.cs`、`SocketRelay/` |
| `ProjectARVRPro.IntegrationDemo` | 给客户/上位机验证 ARVRPro TCP JSON 的示例 | 客户端主动连 `6666`，发送 JSON 事件 | 原始 JSON、解析后的结果表、CSV | `Program.cs`、`MainWindow.xaml.cs`、`Contracts/` |
| `ProjectKB` | 键盘背光亮度、均匀性和自动修正 | Modbus TCP、MES DLL、可选 TCP Socket | SQLite、文本、summary、CSV、MES 上传 | `ProjectKBWindow.xaml.cs`、`Modbus/`、`MesDll.cs`、`BacklightAutotuneService.cs` |
| `ProjectLUX` | LUX 亮度、色彩、MTF、畸变等自动化测试 | 文本 Socket：`T00XX,SN;` | `ProjectLUX.db`、`C_*.csv`、`B_*.csv`、`D_*.csv`、PDF/CSV | `LUXWindow.xaml.cs`、`Process/`、`Recipe/`、`Fix/`、`Services/SocketControl.cs` |

## 按协议和触发方式分类

| 类型 | 项目 | 入口 | 对接要点 |
| --- | --- | --- | --- |
| JSON Socket 项目 | `ProjectARVRLite`、`ProjectARVRPro` | `Services/SocketControl.cs`、各类 handler | `EventName`、SN 初始化、切图确认、最终 `ProjectARVRResult` 字段 |
| 文本 Socket 项目 | `ProjectLUX` | `Services/SocketControl.cs` | `T00XX` 和 `ProcessMeta.SocketCode` 的对应关系 |
| PLC/Modbus 项目 | `ProjectKB` | `Modbus/ModbusControl.cs` | holding register 地址、触发值 `1`、完成回写 `0`、SN 为空策略 |
| 客户对接示例 | `ProjectARVRPro.IntegrationDemo` | `Contracts/`、`Program.cs` | 只保留公开 JSON 契约，不引入 ColorVision 内部逻辑 |

如果某个项目同时支持多种入口，现场排障时要先问清楚当前客户实际启用哪条链路。比如 `ProjectKB` 可能是 Modbus 自动触发，也可能只启用了 MES/SN 上传，不能把所有外部入口混成一条协议。

## 按结果交付方式分类

| 输出类型 | 项目 | 关键文件或配置 | 验收点 |
| --- | --- | --- | --- |
| 整机 CSV | `ProjectARVRLite`、`ProjectARVRPro`、`ProjectLUX` | `ObjectiveTestResult`、CSV exporter、`ViewResultManager.Config` | 文件名、目录、字段顺序、PASS/FAIL、旧格式兼容 |
| SQLite/本地结果 | `ProjectARVRPro`、`ProjectLUX`、`ProjectKB` | `ViewResultManager`、`Project*Reuslt`、`KBItemMaster` | 能按 SN/时间查询，批次和流程模板能对上 |
| Excel/XLSX | `ProjectARVRPro` | `CustomTestResultExportService` | 模板字段、客户标题、路径、依赖库 |
| MES 上传 | `ProjectKB` | `MesDll.cs`、`Summary` | 返回码约定、设备编号、工站/线别/工号 |
| Socket 结果返回 | `ProjectARVRLite`、`ProjectARVRPro`、`ProjectLUX` | Socket handler、`ObjectiveTestResult`、Legacy converter | `Data` 字段结构、状态码、超时/失败返回 |
| Summary/文本 | `ProjectKB` | `Summary.cs`、`ViewResultManager.Config` | 模型分目录、良率汇总、失败项附加 |

修改结果字段时不要只改一个出口。ARVRPro 典型要同时核对标准 CSV、Legacy 输出、Socket `Data` 和客户定制 XLSX；KB 典型要同时核对 CSV、summary、MES `Collect_test` 和数据库字段。

## 流程组织能力对照

| 项目 | 流程组织方式 | 配置文件/对象 | 风险点 |
| --- | --- | --- | --- |
| `ProjectARVRLite` | 启用项配置决定顺序 | `ProjectARVRLiteTestTypeConfig.json`、`ARVR1TestType` | 配置启用未实现分支会导致自动流程异常 |
| `ProjectARVRPro` | `ProcessGroup` + `ProcessMeta` | `ProcessGroups.json`、`PictureSwitchConfig` | 流程组、切图、Recipe、输出格式必须一起迁移 |
| `ProjectLUX` | `ProcessGroup` + `SocketCode` | `ProcessGroups.json`、`ProcessMeta.SocketCode` | 改流程名但不改 SocketCode 会导致外部命令找不到步骤 |
| `ProjectKB` | 当前 Flow 模板 + Recipe | `RecipeManager`、`KBRecipeConfig`、Modbus 配置 | POI 名称/宽度必须和 KB 模板匹配 |

流程组项目优先维护 `Process/`、`Recipe/`、`Fix/` 和 `ObjectiveTestResult`；固定流程项目优先维护主窗口状态机和模板关键字。

## 最小验收清单

| 项目 | 冒烟验收 |
| --- | --- |
| `ProjectARVRLite` | 检查启用项配置，跑一轮启用测试，确认预处理、CSV、Socket 返回都成功 |
| `ProjectARVRPro` | 切换一个流程组，跑 `RunAll` 或完整 `ProjectARVRInit` 链路，确认 PictureSwitch、Recipe、CSV/Legacy/Socket 输出 |
| `ProjectARVRPro.IntegrationDemo` | 解析样例 JSON，联机发送 `ProjectARVRInit` 或 `RunAll`，确认半包/粘包 reader 能读完整结果 |
| `ProjectKB` | Modbus 写 `1` 能触发流程，结束写回 `0`，CSV/summary/MES 输出符合当前配置 |
| `ProjectLUX` | 发送一个 `T00XX,SN;`，能匹配当前组的 `SocketCode`，生成对应 CSV/SQLite 结果 |

## 打包

构建和打包命令：`dotnet build Projects/<Project>/<Project>.csproj -c Release -p:Platform=x64`，再运行 `Scripts\package_project.bat <Project>`。

交付时额外确认：ARVR 系列看 Socket、切图、CSV/Legacy；KB 看 `FunTestDll.dll`、Modbus 和 MES；LUX 看 `SocketCode`、Recipe/Fix 和输出目录。`ProjectARVRPro.IntegrationDemo` 不是插件包，发布给客户时走 `dotnet publish`。

## 变更归属

| 变更 | 优先修改 | 同步文档 |
| --- | --- | --- |
| 新增客户测试项 | 对应项目的 `Process/`、`Recipe/`、`Fix/`、结果模型 | 对应项目页、本页流程组织能力 |
| 新增外部事件或命令 | 项目 `Services/SocketControl.cs`、`HYMesManager`、`ModbusControl` | 对应项目页、本页协议分类 |
| 修改结果字段 | `ObjectiveTestResult`、exporter、Socket response、SQLite model | 对应项目页、本页结果交付方式 |
| 修改流程组或模板名 | `ProcessGroup` / `ProcessMeta` / 主窗口模板关键字 | 项目包总览、具体项目页 |
| 修改交付包内容 | `manifest.json`、`.csproj`、打包脚本、README/CHANGELOG | 项目包总览、构建脚本文档 |
| 通用能力沉淀 | Engine、UI 或通用插件 | Engine 业务链路、UI DLL 发布、插件横向速查 |

客户专用逻辑默认留在项目包。只有多个项目确认会复用，并且能抽象成稳定能力时，才考虑下沉到 Engine、UI 或通用插件。
