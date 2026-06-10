# ProjectKB

`Projects/ProjectKB/` 是键盘背光检测项目包，运行时以 `ProjectKB.dll` 加载。它不是单纯的键位亮度查看器，而是把 FlowEngine 取图、KB 模板、POI 亮度结果、Recipe 判定、背光自动修正、PLC/Modbus 触发、MES DLL 上传和 CSV/summary 留痕串成一条产线流程。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `ProjectKB` |
| `version` | `1.0` |
| `dllpath` | `ProjectKB.dll` |
| `requires` | `1.3.15.10` |

## 业务范围

ProjectKB 面向键盘背光质量检测，核心关注每个按键的亮度、局部对比度、整体平均亮度、最小/最大亮度、亮度均匀性、不良点数量和产线过站结果。

当前主链路支持三类入口：

| 入口 | 说明 |
| --- | --- |
| 手动运行 | 用户输入 SN、选择 Flow 模板后在窗口内执行 |
| Modbus 自动触发 | PLC/上位机把指定 holding register 置为 `1` 后触发拍照，流程结束后项目写回 `0` |
| MES/SN 上传 | `Summary.AutoUploadSN` 打开后，SN 改变会调用 `FunTestDll.dll` 的 `CheckWIP`；测试完成后调用 `Collect_test` |

## 主要源码入口

| 文件/目录 | 作用 |
| --- | --- |
| `ProjectKBWindow.xaml(.cs)` | 主测试窗口、Flow 执行、结果解析、CSV/MES/Modbus 回写 |
| `ProjectKBConfig.cs` | 项目配置、模板编辑、Modbus/Socket 配置入口 |
| `KBRecipeConfig.cs` | 亮度、均匀性、局部对比度和背光自动修正上下限 |
| `BacklightAutotuneService.cs` | 按 Q1/Q3 和 sigmoid 斜率修正平均亮度、最小亮度、均匀性 |
| `KBItemMaster.cs` | 单次测试主结果，落库表 `KBItemMaster` |
| `KBItem.cs` | 单个按键的亮度、色度和局部对比度结果 |
| `Summary.cs` | 产量、良率、MES 站别/线别/工号/设备号配置 |
| `Modbus/ModbusControl.cs` | Modbus TCP 连接、轮询和寄存器写回 |
| `Services/SocketControl.cs` | 可选 TCP socket 触发通道 |
| `MesDll.cs` | `FunTestDll.dll` P/Invoke 封装 |
| `Recipes/RecipeManager.cs` | 按 Flow 模板名切换对应 Recipe |

## 业务链路

1. 窗口初始化时调用 `InitFlow()`，设置 MQTT 默认配置，创建 `FlowEngineControl` 和 `STNodeEditor`，并加载 `FlowEngineLib.dll`。
2. 选择 Flow 模板时，项目用模板名调用 `RecipeManager.SetCurrentTemplate(Name)`，让当前流程和当前 Recipe 对齐。
3. 若 `AutoModbusConnect` 打开，窗口启动后连接 Modbus，并订阅 `StatusChanged`。
4. PLC 把当前寄存器写成 `1` 后，`ProjectKBWindow_StatusChanged` 触发 `RunTemplate()`；如果 `IgnoreAutoRunWhenSnEmpty` 打开且 SN 为空，会忽略本次自动触发并写回 `0`。
5. `RunTemplate()` 创建测量批次，锁定 SN，调用 `flowControl.Start()` 执行当前 Flow。
6. `FlowCompleted` 为 `Completed` 时进入 `Processing()`；`OverTime` 会按 `TryCountMax` 重试；其他失败会保存失败结果，必要时从批次图像里取一张图用于排查。
7. `Processing()` 根据批次号读取 `AlgResultMasterDao`，从 `KB` / `KB_Raw` 结果中找到 KB 模板，从 `POI_Y` / `POI_Y_V2` 中读取每个按键的亮度值。
8. 每个按键亮度按 `Y * PixNumber / Area * KeyScale * KBLVSacle` 计算，再计算局部对比度 `Lc`。
9. 项目先按单键亮度、单键局部对比度判定每个 `KBItem.Result`，再汇总 `AvgLv`、`MinLv`、`MaxLv`、`LvUniformity`、最亮/最暗按键。
10. 若 Recipe 打开背光自动修正，`BacklightAutotuneService.Apply()` 会保留 raw 值，并按 Q1/Q3 与 sigmoid 斜率生成 adjusted 值。
11. 主结果再按平均亮度、最小亮度、最大亮度、均匀性、局部对比度、不良点数量生成 PASS/FAIL。
12. 结果保存到数据库、文本、summary、CSV；最后把 Modbus 寄存器写回 `0`，并在 MES 可用时调用 `Collect_test`。

## 外部集成

### Modbus

`ModbusControl` 使用 Modbus TCP，默认配置来自 `ModbusSetting.Instance.ModbusConfig`：

| 配置 | 默认/作用 |
| --- | --- |
| `Host` | 默认 `127.0.0.1` |
| `Port` | 默认 `502` |
| `RegisterAddress` | 默认 `0x00` |
| 轮询周期 | 连接后约每秒读取一次 holding register |
| 触发值 | 当前值为 `1` 时自动执行流程 |
| 完成回写 | 流程结束后调用 `SetRegisterValue(0)` |

现场流程不触发时，先查 Modbus 是否连接、寄存器地址是否一致、寄存器值是否从 `0` 变成 `1`、SN 空值策略是否把自动触发忽略。

### MES DLL

`MesDll.cs` 通过 P/Invoke 调用插件目录下的：

```text
Plugins/ProjectKB/FunTestDll.dll
```

主链路用到的接口：

| 接口 | 调用时机 | 关键参数 |
| --- | --- | --- |
| `CheckWIP(Stage, SN)` | SN 上传或自动上传 SN 时 | `Summary.Stage`、当前 SN |
| `Collect_test(...)` | 测试完成且 `Summary.UseMes`、`IsCheckWIP` 为 true 时 | 站别、SN、PASS/NG、设备号、线别、工号 |

当前代码把 `CheckWIP` 返回 `"N"` 视为通过；其他返回会弹窗并阻止 SN 锁定。交付时必须确认客户 DLL 的返回约定没有变化。

### TCP Socket

`Services/SocketControl.cs` 提供一个可选 TCP listener：

| 配置 | 默认/作用 |
| --- | --- |
| `IsUseSocket` | 是否启用 socket 服务 |
| `Host` | 默认 `127.0.0.1` |
| `Port` | 默认 `6666` |

当前 socket 代码只做简单格式判断：收到的消息必须包含 `#` 和 `*`，否则返回错误帧；合法消息触发 `StatusChanged`。它不是完整 MES 协议，维护时不要把它和 Modbus 自动触发混写。

## 结果和报告

`KBItemMaster` 是单次测试的主结果，包含：

| 数据 | 来源 |
| --- | --- |
| `Model`、`SN`、`BatchId`、`KBTemplate` | 当前 Flow、输入 SN、批次、KB 模板 |
| `Items` | 每个按键的 `Name`、`Lv`、`Cx`、`Cy`、`Lc`、`Result` |
| `AvgLv`、`MinLv`、`MaxLv`、`LvUniformity` | 按键亮度汇总 |
| `BrightestKey`、`DrakestKey` | 最亮和最暗按键名称 |
| `NbrFailPoints` | 单键判定失败数量 |
| `BacklightAutotune*` | 背光自动修正是否启用、是否应用、raw/adjusted 值和 Q1/Q3 |
| `ResultImagFile` | 算法输出图 |
| `Result` | 最终 PASS/FAIL |

输出路径由 `ViewResultManager.Config` 控制：

| 开关/路径 | 输出 |
| --- | --- |
| `SaveText` / `TextSavePath` | `<SN>-<timestamp>.txt`，内容为 SN 和 Pass/Fail |
| `SaveSummary` / `SummarySavePath` | 按模型分目录的 summary 文本 |
| `CsvSavePath` | `<Model>_<yyyyMMdd>.csv` |
| `AppendFalloutSummary` | 控制 CSV 是否附加失败汇总 |

## 关键配置

| 配置 | 作用 |
| --- | --- |
| `TemplateSelectedIndex` | 当前 Flow 模板 |
| `AutoModbusConnect` | 打开窗口后是否自动连接 Modbus |
| `IgnoreAutoRunWhenSnEmpty` | PLC 自动触发时 SN 为空是否跳过 |
| `TryCountMax` | Flow 超时后的最大重试次数 |
| `KBLVSacle` | 键位亮度整体缩放系数，字段名按源码保留 |
| `SNlocked` | SN 上传成功或手动锁定后阻止 SN 被改写 |
| `LogControlVisibility` | 是否显示主界面日志区 |
| `Summary.UseMes` | 是否启用 ShopFloor/MES DLL |
| `Summary.AutoUploadSN` | SN 改变后是否自动调用 `CheckWIP` |
| `Summary.Stage`、`LineNO`、`Opno`、`MachineNO` | MES 上传字段 |

`KBRecipeConfig` 的核心判定项：

| 类别 | 字段 |
| --- | --- |
| 单键亮度 | `EnableKeyLvLimit`、`MinKeyLv`、`MaxKeyLv` |
| 平均亮度 | `EnableAvgLvLimit`、`MinAvgLv`、`MaxAvgLv` |
| 亮度均匀性 | `EnableUniformityLimit`、`MinUniformity` |
| 局部对比度 | `EnableKeyLcLimit`、`MinKeyLc`、`MaxKeyLc` |
| 背光自动修正 | `EnableBacklightAutotune`、`BacklightAutotuneSteepness`、各指标 Q1/Q3 |

## 构建与交付

```powershell
dotnet build Projects/ProjectKB/ProjectKB.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectKB --no-upload
```

交付包必须确认 `FunTestDll.dll`、`FunTestDllConfig.INI`、manifest、项目图标、配置默认值和目标现场的 Modbus 地址一致。

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 项目装载 | 检查 `manifest.json`、`ProjectKB.dll` 和菜单入口 | 主程序能发现项目包，`ProjectKBWindow` 能打开 |
| Flow/Recipe 对齐 | 选择一个 KB Flow 模板 | `RecipeManager.SetCurrentTemplate(Name)` 能切到对应 Recipe |
| 手动运行 | 输入 SN 后执行当前模板 | Flow 完成后生成 `KBItemMaster`，结果列表显示 PASS/FAIL |
| Modbus 触发 | 连接 PLC，把配置寄存器写为 `1` | 自动触发测试，结束后写回 `0` |
| SN 空值策略 | 打开 `IgnoreAutoRunWhenSnEmpty` 后用空 SN 触发 | 自动触发被忽略，寄存器仍能复位 |
| MES CheckWIP | 打开 `Summary.AutoUploadSN` 后输入 SN | 调用 `FunTestDll.dll`，返回 `"N"` 时 SN 锁定成功 |
| 结果解析 | 运行含 `KB` / `POI_Y` 的 Flow | 每个键位亮度、色度、局部对比度和失败点数量被填充 |
| 背光自动修正 | 打开 Recipe 中的自动修正 | raw 值和 adjusted 值都保留，最终判定使用配置后的修正结果 |
| 输出留痕 | 打开文本、summary、CSV 保存 | 数据库、txt、summary、`<Model>_<yyyyMMdd>.csv` 都生成 |
| 交付依赖 | 检查打包输出 | `FunTestDll.dll`、`FunTestDllConfig.INI`、manifest、README、CHANGELOG 随包交付 |

## 故障首查

| 现象 | 先查什么 |
| --- | --- |
| Modbus 写 `1` 后不触发 | Modbus 连接状态、IP/端口、holding register 地址、轮询日志和当前 SN 空值策略 |
| 流程结束后寄存器没回 `0` | `FlowCompleted` 是否进入最终回写、异常路径是否提前返回、`SetRegisterValue(0)` 返回码 |
| 输入 SN 被 MES 拦截 | `Summary.AutoUploadSN`、`Summary.Stage`、`CheckWIP` 返回值和客户 DLL 返回约定 |
| MES 上传失败 | `FunTestDll.dll` 路径、`FunTestDllConfig.INI`、`Summary.UseMes`、`IsCheckWIP` 和 `Collect_test` 参数 |
| Flow 成功但没有键位结果 | 批次内是否有 `KB` / `KB_Raw`、`POI_Y` / `POI_Y_V2`，KB 模板名是否匹配 |
| 某些按键缺失 | POI 名称、宽度、KB 模板键名、`KeyScale` 和 `KBLVSacle` |
| 亮度整体偏高或偏低 | `KBLVSacle`、Recipe 上下限、背光自动修正 Q1/Q3 和 sigmoid 斜率 |
| CSV 或 summary 没生成 | `ViewResultManager.Config` 保存开关、路径权限、模型名分目录 |
| Socket 触发异常 | 当前现场是否真的启用 TCP Socket，消息是否同时包含 `#` 和 `*` |
| 结果图缺失 | Flow 失败路径是否复制批次图像，`ResultImagFile` 是否指向存在文件 |

## 交接注意事项

- `FunTestDll.dll` 路径按 `Plugins/ProjectKB/FunTestDll.dll` 解析，缺 DLL 会导致 MES 上传直接失败。
- `CheckWIP` 只有返回 `"N"` 才被当前代码视为通过，客户 DLL 版本变化时先确认返回约定。
- `KBLVSacle` 是现场标定敏感项，改动后要同步 Recipe、历史数据解释和验收规范。
- `POI_Y` / `POI_Y_V2` 的 POI 名称、宽度必须能匹配 KB 模板里的按键，否则会找不到对应按键。
- Modbus、Socket、MES 是三条不同外部链路；排查时先确认当前现场到底启用了哪一条。
