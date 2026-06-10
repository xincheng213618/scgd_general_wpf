# 项目包运行与交接场景手册

这页面向接手客户项目包、排查现场协议/结果/流程问题、发布项目 `.cvxp` 的维护人员。它不替代具体项目页，而是把项目包常见的触发、流程、模板、Recipe/Fix、结果导出、外部系统返回、交付打包整理成可执行场景。

如果你还不知道每个项目负责什么，先看 [项目包能力与交接矩阵](./project-capability-matrix.md)。如果你要发布、替换或回退项目包，直接补 [项目包发布证据与版本核查表](./project-release-evidence.md)。如果你要理解通用执行链和 `ProcessGroup`、`IProcess` 模型，再看 [项目包交接手册](./project-handoff.md)。

## 使用方法

1. 先用 [场景入口](#场景入口) 判断问题属于加载入口、外部触发、流程模板、判定配置、结果导出、设备/MES、打包交付还是客户对接 Demo。
2. 按对应场景检查项目目录、配置、主窗口、`Process/`、`Recipe/`、`Fix/`、`Services/` 和 exporter。
3. 最后填写 [项目包交接记录](#项目包交接记录)，把客户协议、输出、配置路径、包版本和验收结果留下。

## 场景入口

| 接到的问题 | 先看场景 | 典型项目 |
| --- | --- | --- |
| 项目包装上后菜单或窗口打不开 | [场景 A](#场景-a：项目包入口或窗口打不开) | 全部项目包 |
| 外部系统发命令后没有启动测试 | [场景 B](#场景-b：外部触发没有启动测试) | ARVR、ARVRLite、ARVRPro、LUX、KB、BlackMura、Heyuan |
| 流程启动了，但跑错测试项或模板找不到 | [场景 C](#场景-c：流程组或模板绑定错误) | ARVRPro、LUX、KB、Shiyuan |
| 算法有结果，但 PASS/FAIL 或客户字段不对 | [场景 D](#场景-d：判定配置或结果字段不对) | ARVRPro、LUX、KB、BlackMura、Heyuan |
| CSV/XLSX/PDF/SQLite/MES 输出缺字段 | [场景 E](#场景-e：结果导出或客户回传不对) | ARVR 系列、LUX、KB、BlackMura、Heyuan、Shiyuan |
| 串口、Modbus、MES、PG、切图异常 | [场景 F](#场景-f：设备mes或切图链路异常) | BlackMura、Heyuan、KB、ARVRPro |
| 要打包项目 `.cvxp` 交付现场 | [场景 G](#场景-g：打包和交付项目包) | 除 IntegrationDemo 外全部项目包 |
| 客户只要对接示例或协议验证 | [场景 H](#场景-h：客户对接-demo-验证) | ProjectARVRPro.IntegrationDemo |

## 项目包运行模型

项目包运行时通常也进入主程序 `Plugins/<Name>/`，但它和通用插件的关注点不同：插件偏工具能力，项目包偏客户生产闭环。

通用链路如下：

```text
manifest / PluginConfig
  -> 项目窗口
  -> 外部命令或人工启动
  -> 当前流程组 / 固定流程
  -> FlowTemplate
  -> Engine Flow 执行
  -> IProcess 读取结果并应用 Recipe/Fix
  -> ObjectiveTestResult 聚合
  -> SQLite / CSV / XLSX / PDF / MES / Socket 返回
```

排查时不要只看 exporter 或 CSV。很多项目问题发生在更前面：外部命令没有匹配、流程组当前项不对、`FlowTemplate` 名称变了、Recipe/Fix 没读到、`IProcess.Execute()` 没跑。

## 场景 A：项目包入口或窗口打不开

处理步骤：

1. 确认项目目录在主程序输出目录 `Plugins/<ProjectName>/` 下。
2. 检查 `manifest.json` 的 `id`、`dllpath`、`requires`。
3. 检查 `PluginConfig/` 或项目根目录 `Menu*.cs` 是否注册菜单和窗口单例。
4. 看主程序日志是否有插件加载、依赖 DLL、manifest 或窗口构造异常。
5. 打开项目页，确认该项目的入口菜单名称和是否需要管理员/设备环境。

当前项目包入口：

| 项目 | 入口重点 |
| --- | --- |
| ARVR 系列 | `PluginConfig/ProjectARVRMenu.cs`、窗口单例 |
| LUX | `PluginConfig/ProjectLUXMenu.cs`、`LUXWindow` |
| KB | `PluginConfig/KBMenu.cs`、`ProjectKBWindow` |
| BlackMura | `PluginConfig/BlackMuraMenu.cs`、`MainWindow` |
| Heyuan | `MenuItemHeyuan.cs`、`ProjectHeyuanWindow` |
| Shiyuan | 项目窗口和固定导出配置 |

## 场景 B：外部触发没有启动测试

先判断外部触发类型：

| 类型 | 项目 | 关键入口 | 最先确认 |
| --- | --- | --- | --- |
| JSON Socket | ProjectARVR、ProjectARVRLite、ProjectARVRPro | `Services/SocketControl.cs`、handler | `EventName`、SN、窗口是否已创建、是否需要切图确认 |
| 文本 Socket | ProjectLUX | `Services/SocketControl.cs` | `T00XX,SN;` 中 `XX` 是否匹配 `ProcessMeta.SocketCode` |
| Modbus TCP | ProjectKB | `Modbus/ModbusControl.cs` | holding register、触发值 `1`、完成回写 `0`、SN 来源 |
| 串口/MES | ProjectBlackMura、ProjectHeyuan | `HYMesManager.cs`、`SerialMsg.cs` | STX/ETX、设备编号、动作码、返回码 |
| 手动或离线 | ProjectShiyuan | 主窗口按钮和 Flow 选择 | `DataPath`、模板选择、固定图像路径 |

处理步骤：

1. 确认主程序 Socket 或串口/Modbus 服务已启用。
2. 确认当前项目窗口已打开，或者源码支持命令自动创建窗口。
3. 查外部命令字段是否与当前项目页一致。
4. 确认 SN、流程组、模板和当前状态允许启动。
5. 如果触发只完成初始化但不继续跑，看是否还在等待 `SwitchPGCompleted`、PG 返回、Modbus 状态或 MES 放行。

## 场景 C：流程组或模板绑定错误

适用：ARVRPro、ProjectLUX、KB、Shiyuan 等需要选择 Flow 模板或流程组的项目。

处理步骤：

1. 找当前项目的流程组织方式：`ProcessGroup`、固定枚举、启用项配置或手动模板选择。
2. 检查当前配置文件，例如 `ProcessGroups.json`、`ProjectARVRLiteTestTypeConfig.json`、KB Recipe 配置、Shiyuan `TemplateSelectedIndex`。
3. 确认 `FlowTemplate` 字符串能在 Engine `TemplateFlow.Params` 中找到。
4. 如果类名或命名空间改过，检查 `ProcessTypeFullName` 是否还能反序列化。
5. 如果客户说跑错项目，优先检查当前激活组、启用步骤和 `SocketCode`，不要先改算法。

高风险字段：

| 字段 | 影响 |
| --- | --- |
| `ProcessMeta.FlowTemplate` | 名称不匹配会导致 Flow 不启动 |
| `ProcessMeta.ProcessTypeFullName` | 类名变动会导致旧配置加载失败 |
| `ProcessMeta.IsEnabled` | 影响自动流程和结果完整性 |
| `ProcessMeta.SocketCode` | ProjectLUX 外部命令能否找到步骤 |
| `PictureSwitchConfig` | ARVRPro 切图串口、返回值和延时 |

## 场景 D：判定配置或结果字段不对

项目包的判定通常发生在 `IProcess.Execute(ctx)` 里，而不是 Engine 通用模板里。

处理步骤：

1. 确认 Flow 已经完成并产生 Engine 结果。
2. 确认匹配到了正确的 `IProcess` 实现。
3. 查 `Recipe/` 的上下限、客户规格、启用项。
4. 查 `Fix/` 的修正因子、校准参数或客户补偿。
5. 查 `ObjectiveTestResult` 是否写入正确字段。
6. 如果是客户定制输出，查 exporter 或 Legacy converter 有没有使用旧字段。

按项目看重点：

| 项目 | 判定重点 |
| --- | --- |
| ARVR/ARVRLite | 固定测试类型、`ObjectiveTestResult`、CSV 开关 |
| ARVRPro | `Process/`、`Recipe/`、Legacy CSV、客户 XLSX |
| LUX | `Process/`、`Recipe/`、`Fix/`、`SocketCode` |
| BlackMura | 五色流程、Mura 结果、Excel 报告 |
| Heyuan | WBRO 四点顺序、`TempResult`、MES 返回 |
| KB | POI 名称/宽度、背光自动修正、MES DLL 字段 |
| Shiyuan | JND/POI CSV、固定图像和伪彩图 |

## 场景 E：结果导出或客户回传不对

结果通常有多个出口，改字段时要一起验：

| 出口 | 典型项目 | 验收 |
| --- | --- | --- |
| SQLite | ARVRPro、LUX、KB | 能按 SN、时间、流程或模型查询 |
| CSV | ARVR 系列、LUX、KB、Heyuan、Shiyuan | 文件名、目录、字段顺序、PASS/FAIL |
| XLSX/Excel | BlackMura、ARVRPro | 模板字段、客户标题、图片/POI |
| PDF | LUX | 输出路径、图片资源、客户字段 |
| MES/串口上传 | BlackMura、Heyuan、KB | 返回码、设备号、线别/工站/工号 |
| Socket 返回 | ARVR 系列、LUX | `Code`、`Msg`、`Data` 结构和超时 |

处理步骤：

1. 确认本地结果已经聚合到 `ObjectiveTestResult` 或项目结果模型。
2. 查当前项目是否有 Legacy/客户定制输出开关。
3. 同时验证文件输出、Socket/MES 返回和本地数据库。
4. 如果只改 CSV，要明确其它出口是否保持旧格式。

## 场景 F：设备、MES 或切图链路异常

| 项目 | 外部边界 | 先查 |
| --- | --- | --- |
| ARVRPro | 图片切换串口、AOI Relay、Socket JSON | `PictureSwitchConfig`、`SocketRelay/`、切图完成事件 |
| BlackMura | PG 串口、MES、五色图 | `HYMesManager.cs`、`SerialMsg.cs`、PG 返回 |
| Heyuan | STX/ETX 串口、WBRO 上传 | `HYMesManager.cs`、`TempResult.cs` |
| KB | Modbus TCP、MES DLL、FunTestDll | `Modbus/`、`MesDll.cs`、`FunTestDllConfig.INI` |
| LUX | 文本 Socket、流程组命令码 | `SocketCode`、`ProcessGroup`、输出目录 |

处理原则：

- 先确认项目窗口状态、设备连接和外部服务，再判断业务代码。
- 串口/MES 问题必须记录原始命令、返回码、超时和设备号。
- Modbus 问题必须记录地址、触发值、完成写回值和 SN 来源。
- 切图问题必须记录发送命令、期望返回、实际返回和延时。

## 场景 G：打包和交付项目包

通用命令：

```powershell
Scripts\package_project.bat ProjectLUX --no-upload
```

`package_project.bat` 会调用 `Scripts/package_cvxp.py`，逻辑和插件打包相同：构建项目、收集输出、剔除宿主共享文件、复制项目根目录的 `README.md`、`CHANGELOG.md`、`manifest.json`、`PackageIcon.png`，再用主 DLL `FileVersion` 生成 `.cvxp`。

交付前检查：

| 项 | 检查内容 |
| --- | --- |
| manifest | `id`、`dllpath`、`version`、`requires` |
| README/CHANGELOG | 是否对应当前客户版本 |
| 配置 | 流程组、Recipe、Fix、Socket/MES、路径配置是否需要随包或现场导入 |
| native/外部 DLL | `FunTestDll.dll`、MES DLL、串口/设备 SDK 是否已说明 |
| 输出路径 | CSV/XLSX/PDF/SQLite/MES 目录是否可写 |
| 主程序依赖 | `ColorVision.*.dll` 是否满足项目 `.deps.json` |
| 回退包 | 上一版 `.cvxp`、配置备份和现场项目目录 |

IntegrationDemo 不是项目插件包，发布时使用：

```powershell
dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -f net48 -c Release -p:Platform=x64 -o artifacts/ProjectARVRPro.IntegrationDemo
```

## 场景 H：客户对接 Demo 验证

ProjectARVRPro.IntegrationDemo 是给客户或上位机验证 ARVRPro JSON/TCP 协议的示例，不应该引入 ColorVision 内部业务逻辑。

验收步骤：

1. 能读取样例 JSON，例如 `Samples/project-arvr-result.json`。
2. 能连接测试服务端或现场 ARVRPro Socket。
3. 能发送 `ProjectARVRInit`、`SwitchPGCompleted`、`RunAll` 或约定事件。
4. 能处理半包/粘包并展示完整响应。
5. 能导出客户可读的 CSV 或结果表。

如果 Demo 和项目包协议不一致，要同步更新 Demo、ARVRPro 项目页、协议手册和客户交付说明。

## 项目包交接记录

每次项目包交接或发布至少记录：

| 项 | 内容 |
| --- | --- |
| 项目 | 名称、源码目录、manifest id |
| 客户/场景 | 客户名称、产品型号、现场触发方式 |
| 版本 | `manifest.version`、`.csproj VersionPrefix`、输出 DLL `FileVersion`、`.cvxp` 文件名 |
| 构建命令 | `dotnet build`、`Scripts\package_project.bat ... --no-upload` 或 Demo publish |
| 协议 | Socket/MES/串口/Modbus 事件、命令、返回码、超时 |
| 流程 | 当前流程组、启用步骤、模板名、`SocketCode` 或固定流程顺序 |
| 配置 | Recipe/Fix、路径、SN、输出模式、设备配置 |
| 输出 | SQLite、CSV、XLSX、PDF、MES、Socket 返回字段 |
| 验收 | 最小冒烟结果、客户字段核对、现场依赖 |
| 回退 | 上一版包、配置备份、数据库备份、客户协议版本 |
| 已知限制 | 未验证设备、旧格式兼容、权限、人工步骤 |

## 继续阅读

- [项目包能力与交接矩阵](./project-capability-matrix.md)
- [项目包发布证据与版本核查表](./project-release-evidence.md)
- [项目包交接手册](./project-handoff.md)
- [ProjectARVR](./project-arvr.md)
- [ProjectARVRLite](./project-arvr-lite.md)
- [ProjectARVRPro](./project-arvr-pro.md)
- [ProjectARVRPro.IntegrationDemo](./project-arvr-pro-integration-demo.md)
- [ProjectBlackMura](./project-black-mura.md)
- [ProjectHeyuan](./project-heyuan.md)
- [ProjectKB](./project-kb.md)
- [ProjectLUX](./project-lux.md)
- [ProjectShiyuan](./project-shiyuan.md)
