---
knowledge_id: "projects.kb"
knowledge_type: "reference"
status: "current"
summary: "ProjectKB 的宿主/独立启动依赖、Modbus/MES、Recipe 判定、背光修正与 CSV 输出边界。"
aliases: ["Modbus 写 1 不触发 KB 测试","KB MES 返回 N 是成功吗","ProjectKB","ProjectKBWindow","KBItemMaster","KBItemMasterExtensions","KB CSV","KBLVSacle"]
code_paths: ["Projects/ProjectKB/App.xaml.cs","Projects/ProjectKB/ProjectKBWindow.xaml.cs","Projects/ProjectKB/KBItemMasterExtensions.cs","Projects/ProjectKB/Modbus/","Projects/ProjectKB/MesDll.cs","Projects/ProjectKB/"]
test_paths: ["Test/ProjectKB.Tests/ProjectKB.Tests.csproj"]
related: ["projects.index","projects.capabilities"]
---

# ProjectKB

`Projects/ProjectKB/` 是键盘背光检测项目包，运行时加载 `ProjectKB.dll`。它把 FlowEngine、KB/POI 结果、Recipe 判定、背光自动修正、Modbus 触发、MES DLL 和 CSV/summary 留痕串成一条产线流程。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| Modbus 写 `1` 后不触发 | 连接状态、IP/端口、holding register、SN 空值策略 |
| 流程结束寄存器没回 `0` | `FlowCompleted` 是否走到最终回写、异常路径是否提前返回 |
| 输入 SN 被 MES 拦截 | `Summary.AutoUploadSN`、`Summary.Stage`、`CheckWIP` 返回值 |
| Flow 成功但没有键位结果 | 批次内是否有 `KB`/`KB_Raw` 和 `POI_Y`/`POI_Y_V2` |
| 某些按键缺失 | POI 名称、宽度、KB 模板键名、`KeyScale`、`KBLVSacle` |
| 亮度整体偏移 | `KBLVSacle`、Recipe 限值、背光自动修正 Q1/Q3 |
| CSV/summary 没生成 | `ViewResultManager.Config` 保存开关、路径权限、模型名 |
| MES 上传失败 | `FunTestDll.dll`、`FunTestDllConfig.INI`、`Collect_test` 参数 |

## manifest

项目身份为 `ProjectKB`，入口程序集为 `ProjectKB.dll`。发布版本和最低宿主版本会随交付变化，维护和打包时直接读取 `Projects/ProjectKB/ProjectKB.csproj` 与 `manifest.json`，不要在说明页复制一份版本号。

项目同时声明 `WinExe`，`App.xaml.cs` 有独立启动入口：初始化配置、授权、日志、主题与语言后，显式加载 `ColorVision.Engine.dll`，按 `Order` 执行发现的 `IInitializer.InitializeAsync()`，再打开 `ProjectKBWindow`。这不等于免依赖的独立交付；独立运行仍需相应程序集、配置及运行环境。当前 `.csproj` 的直接项目引用是 `Engine/ColorVision.Engine/ColorVision.Engine.csproj`；`ColorVision.Engine.Templates` 是命名空间，不是另一个被引用的项目。

## 运行链路

`ProjectKBWindow` 初始化 FlowEngine；选择 Flow 模板时，用模板名调用 `RecipeManager.SetCurrentTemplate(Name)`。`AutoModbusConnect` 打开时，窗口启动后连接 Modbus 并订阅 `StatusChanged`。PLC 把寄存器写为 `1` 后触发 `RunTemplate()`；SN 为空且启用 `IgnoreAutoRunWhenSnEmpty` 时，本次自动触发会被忽略并写回 `0`。Flow 完成后 `Processing()` 读取 `KB`/`KB_Raw` 和 `POI_Y`/`POI_Y_V2`，计算每个按键亮度、局部对比度、均匀性和不良点，保存数据库、txt、summary、CSV，最后 Modbus 写回 `0`，必要时调用 MES `Collect_test`。

## 外部集成

| 通道 | 关键点 |
| --- | --- |
| Modbus TCP | 默认 host `127.0.0.1`、port `502`、register `0x00`；读到 `1` 自动触发，结束写回 `0` |
| MES DLL | 固定从 `Plugins/ProjectKB/FunTestDll.dll` P/Invoke |
| TCP Socket | 可选 listener，消息必须包含 `#` 和 `*`，只作为轻量触发通道 |

MES 调用约定：`CheckWIP(Stage, SN)` 在 SN 上传或自动上传 SN 时执行，当前代码把返回 `"N"` 视为通过；`Collect_test(...)` 在测试完成且 `Summary.UseMes`、`IsCheckWIP` 为 true 时执行，参数含站别、SN、PASS/NG、设备号、线别、工号。

客户 DLL 返回约定变更时，先改 MES 判断，不要先改 Flow 或 Recipe。

## 结果和判定

`KBItemMaster` 是单次测试主结果，核心数据包括 SN、批次、模板、每个按键的亮度/色度/局部对比度、平均亮度、最小/最大亮度、亮度均匀性、不良点数量、结果图和最终 PASS/FAIL。

亮度计算链路里有两个容易漏掉的系数：

```text
按键亮度 = POI 亮度 * PixNumber / Area * KeyScale * KBLVSacle
```

背光自动修正由 `BacklightAutotuneService.Apply()` 处理，使用 Recipe 中的 Q1/Q3 和 sigmoid 斜率。它会保留 raw 值，最终显示和判定要分清 raw 与 adjusted。

`ProjectKBWindow.Processing()` 按 `EnableKeyLvLimit`、`EnableAvgLvLimit`、`EnableUniformityLimit`、`EnableKeyLcLimit` 分别启用判定；上下限使用 `>=` / `<=`，不是严格大于/小于。均匀性和局部对比度的 Recipe 百分数要除以 `100` 后比较。整体判定的最小亮度、平均亮度、均匀性通过 `BacklightAutotuneService.GetOriginal*` 取原始值，最大亮度与局部对比度则读取当前结果；不能把所有指标一概称为修正值判定。

CSV 契约由 `KBItemMasterExtensions.SaveCsv()` 维护：除固定列外，按 `Items` 追加键名列；`KBCsvDataType.Lv` 的动态列导出键位亮度，`Lc` 导出局部对比度乘 `100`，两者共享固定汇总字段，限值来自结果携带的 Recipe 快照。当前新生成的数据行中 `ColorDifference` 和 `StrayLight` 两列为空，占位字段不代表已完成色差/杂光测量；同表头的既有数据行会保留，不能据此推断所有历史行也为空。排查列内容时核对这个实现，不从表头推断能力。

## 关键配置

| 配置 | 作用 |
| --- | --- |
| `TemplateSelectedIndex` | 当前 Flow 模板 |
| `AutoModbusConnect` | 窗口打开后是否自动连接 Modbus |
| `IgnoreAutoRunWhenSnEmpty` | 自动触发时 SN 为空是否跳过 |
| `TryCountMax` | Flow 超时最大重试次数 |
| `KBLVSacle` | 键位亮度整体缩放系数，字段名按源码保留 |
| `SNlocked` | SN 上传成功或手动锁定后防止改写 |
| `Summary.UseMes` | 是否启用 MES DLL |
| `Summary.AutoUploadSN` | SN 改变后是否自动调用 `CheckWIP` |
| `AppendFalloutSummary` | CSV 是否附加失败汇总 |

Recipe 重点看单键亮度、平均亮度、亮度均匀性、局部对比度和背光自动修正。`KBLVSacle`、Recipe、历史数据解释要一起交付。

## 本地构建与测试

下列命令只编译和执行本地测试，不上传项目包；会写入本地构建/测试产物。真实 Modbus/MES 烟测另需现场操作授权，测试项目通过不证明生产设备链路已验证。

```powershell
dotnet build Projects/ProjectKB/ProjectKB.csproj -c Release -p:Platform=x64
dotnet test Test/ProjectKB.Tests/ProjectKB.Tests.csproj -c Release -p:Platform=x64
```

## 打包上传（需明确发布授权）

只有用户明确要求发布 ProjectKB 时执行。wrapper 会构建、上传并清理本地 `.cvxp`，不支持 `--no-upload`；仅询问构建或诊断不能运行该命令。

```powershell
.\Scripts\package_project.bat ProjectKB
```

交付包必须包含 `FunTestDll.dll`、`FunTestDllConfig.INI`、manifest、项目图标、默认配置，并和现场 Modbus 地址一致。

## 交付验收

| 验收项 | 通过标准 |
| --- | --- |
| 项目装载 | 主程序发现项目包，`ProjectKBWindow` 能打开 |
| Flow/Recipe 对齐 | 选择 KB Flow 后能切到对应 Recipe |
| 手动运行 | 输入 SN 后生成 `KBItemMaster` 和 PASS/FAIL |
| Modbus 触发 | 寄存器写 `1` 自动测试，结束写回 `0` |
| SN 空值策略 | 空 SN 自动触发被忽略时仍能复位寄存器 |
| MES CheckWIP | 返回 `"N"` 时 SN 锁定成功 |
| 结果解析 | `KB`/`POI_Y` 数据能填充每个键位 |
| 背光自动修正 | raw 和 adjusted 都保留，判定符合 Recipe |
| 输出留痕 | 数据库、txt、summary、`<Model>_<yyyyMMdd>.csv` 都生成 |
| 交付依赖 | MES DLL、INI、README、CHANGELOG 随包交付 |
