# ProjectBlackMura

`Projects/ProjectBlackMura/` 是显示面板 Black Mura 缺陷检测项目包，运行时以 `ProjectBlackMura.dll` 加载。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `ProjectBlackMura` |
| `version` | `1.0` |
| `dllpath` | `ProjectBlackMura.dll` |
| `requires` | `1.3.15.10` |

## 业务范围

ProjectBlackMura 用于显示面板 Black Mura 检测。它不是单纯调用一个算法模板，而是把 PG 上电、PG 切图、五个画面流程、Engine 结果解析、POI overlay、Excel 报告和 MES/串口状态串成一个现场流程。

当前源码中的测试画面顺序是：

```text
None -> White -> Black -> Red -> Green -> Blue
```

其中 `White`、`Black` 用于亮度均匀性、梯度、对比度等核心判定，`Red`、`Green`、`Blue` 主要用于颜色和波长类结果汇总。

## 主要源码入口

| 文件/目录 | 作用 |
| --- | --- |
| `MainWindow.xaml(.cs)` | 主窗口 |
| `ProjectBlackMuraConfig.cs` | 项目配置 |
| `PluginConfig/BlackMuraProject.cs` | 功能启动器 |
| `PluginConfig/BlackMuraMenu.cs` | 工具菜单入口 |
| `ExcelReportGenerator.cs` | Excel 报告生成 |
| `HYMesManager.cs` | MES 相关管理 |
| `Config/EditARVRConfig.xaml(.cs)` | 项目配置窗口，维护结果路径、窗口恢复、重试次数等 |
| `manifest.json` | 插件装载清单 |

## 业务链路

### 手动单流程

1. 用户输入 SN。
2. 在 `FlowTemplate` 中选择包含 `White`、`Black`、`Red`、`Green` 或 `Blue` 的流程模板。
3. `RunTemplate()` 创建批次，调用 `FlowControl.Start(sn)`。
4. `FlowCompleted` 后从 `AlgResultMasterDao`、`PoiPointResultDao` 等 DAO 读取结果。
5. 窗口把 POI 结果画到 `ImageView` 上，并更新 `BlackMudraResult`。
6. 如果是单次模式，直接生成 Excel。

### 自动整机流程

1. `Test1_Click` 检查 SN，重置 `BlackMudraResult`，设置 `StepIndex=0`，并调用 `HYMesManager.PGPowerOn()`。
2. 串口返回 `CON,S` 后，`CONCompleted` 或后续 `CCPICompleted` 推动流程继续。
3. 每次 `CCPI,S` 表示 PG 切图完成，`MainWindow_CCPICompleted` 会切到下一个 `BlackMuraTestType`。
4. 代码按模板名关键字选择对应 Flow：`White`、`Black`、`Red`、`Green`、`Blue`。
5. `Blue` 完成后调用 `UploadSN(BlackMudraResult.SN)`，生成 Excel，并执行 `PGPowerOff()`。

## 串口和 MES 协议

`HYMesManager` 使用 38400 波特率串口，报文用 `0x02` / `0x03` 包裹。当前可见指令包括：

| 指令 | 方向 | 作用 |
| --- | --- | --- |
| `CON,C,{DeviceId}` | 发送 | PG 上电 |
| `COFF,C,{DeviceId}` | 发送 | PG 下电 |
| `CCPI,C,{DeviceId},{id}` | 发送 | PG 切图，`id` 由项目流程决定 |
| `CSN,C,{DeviceId},{sn}` | 发送 | 上传产品 SN |
| `CGI,C,{DeviceId},Default,{Msg}` | 发送 | 上传 NG 信息 |
| `CON,S` / `COFF,S` / `CCPI,S` / `CSN,S` | 接收 | 对应动作返回结果，末尾字段包含 `0` 时按成功处理 |

如果现场出现流程不往下跑，优先查 `CCPICompleted` 是否触发、串口日志是否收到完整 `STX/ETX` 报文、以及 `HYMesConfig.DeviceId` 是否和现场设备一致。

## 结果和报告

`ExcelReportGenerator.GenerateExcel()` 会输出 `<SN>.xlsx`，保存路径来自 `ProjectBlackMuraConfig.ResultSavePath`。报告包含：

| 数据块 | 来源 |
| --- | --- |
| SN、Model、时间 | `BlackMudraResult` |
| White/Black/Red/Green/Blue 的 Mean、Max、Min、Uniformity | Engine 结果解析后的 `Measurements` |
| x、y、Wavelength、Saturation | `PoiResultCIExyuvData` |
| White/Black 梯度和 Contrast | Black Mura JSON 结果和亮度均值 |
| Border size、Mura coordinate 等 | Black Mura 结果 JSON |

窗口结果区还会用 `ImageView` 打开结果图，并把 POI 点以圆或矩形 overlay 方式绘制出来。

## 关键配置

| 配置 | 作用 |
| --- | --- |
| `SNMax` | SN 长度截断，默认保留最后 17 位 |
| `ResultSavePath` | Excel 报告输出路径 |
| `ResultSavePath1` | 第二输出路径，当前配置窗口保留该字段 |
| `TryCountMax` | 流程失败重试次数 |
| `ViewImageReadDelay` | 结果图文件仍在写入时的延迟重读时间 |
| `HYMesConfig.IsSingleMes` | 单机/联机模式边界，会影响 `CCPICompleted` 的自动推进 |

## 构建与交付

```powershell
dotnet build Projects/ProjectBlackMura/ProjectBlackMura.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectBlackMura --no-upload
```

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 手动流程 | 选择包含颜色关键字的 Flow 模板并单次运行 | `CurrentTestType` 能被模板名识别，结果图和 POI overlay 正常 |
| PG 上电 | 自动流程点击测试后发送 `CON,C,{DeviceId}` | 收到 `CON,S` 成功后 `StepIndex=1`，继续触发 White |
| 五色切图 | 每次 `CCPI,S` 后推进 White/Black/Red/Green/Blue | `StepIndex` 依次为 1 到 5，模板关键字和切图 ID 一致 |
| 重试机制 | 让某次 Flow 失败 | `TryCount` 小于 `TryCountMax` 时重跑，超出后按失败处理 |
| 结果解析 | 完成 White/Black/RGB 流程 | `BlackMudraResult` 中亮度、均匀性、色坐标、波长和 Mura 字段被填充 |
| Excel 报告 | Blue 完成后检查输出目录 | `ResultSavePath\<SN>.xlsx` 存在，客户字段和结果图一致 |
| PG 下电 | 整机流程结束 | 发送 `COFF,C,{DeviceId}`，窗口状态复位 |

## 故障首查

| 现象 | 先查什么 |
| --- | --- |
| 点击测试后 PG 不上电 | 串口号/波特率、`HYMesConfig.DeviceId`、`CON,C,{DeviceId}` 是否发出、是否收到完整 `STX/ETX` |
| 收到 `CON,S` 后不进入 White | `CONCompleted` 事件、`StepIndex` 是否置为 1、`HYMesConfig.IsSingleMes` 模式 |
| `CCPI,S` 后流程不推进 | `CCPICompleted` 是否触发、当前 `BlackMuraTestType`、切图 ID 和 `StepIndex` |
| 提示找不到模板 | Flow 模板名是否包含 `White`、`Black`、`Red`、`Green`、`Blue` 关键字 |
| Flow 成功但报告字段为空 | `AlgResultMasterDao`、`PoiPointResultDao`、Black Mura JSON 和 `BlackMudraResult` 字段映射 |
| Excel 没生成 | `ResultSavePath` 权限、`SNMax` 截断后的 SN、EPPlus 依赖和 `ExcelReportGenerator` 异常 |
| POI overlay 位置异常 | 结果图路径、`ViewImageReadDelay`、POI 坐标系和 ImageEditor overlay 类型 |
| 重试没有发生 | `TryCount`、`TryCountMax`、Flow 完成状态和失败路径是否提前结束 |
| NG 信息没有上传 | `CGI,C,{DeviceId},Default,{Msg}` 是否发出，失败字段是否写入 `BlackMudraResult` |
| 结束后 PG 没下电 | Blue 完成链路、`PGPowerOff()` 调用和 `COFF,S` 返回 |

## 变更影响表

| 改动 | 必须同步检查 |
| --- | --- |
| 修改颜色顺序或新增画面 | `BlackMuraTestType`、`MainWindow_CCPICompleted`、PG 切图 ID、Excel 字段 |
| 修改模板命名 | `RunTemplate()` 中对 `White`、`Black`、`Red`、`Green`、`Blue` 的关键字判断 |
| 修改判定字段 | `BlackMudraResult`、`ExcelReportGenerator`、窗口结果区、MES NG 信息 |
| 修改串口协议 | `HYMesManager` 发送字段、接收结果解析、现场设备 `DeviceId` |
| 修改结果图读取 | `ViewImageReadDelay`、图像文件存在性、ImageEditor overlay 坐标 |

## 交接注意事项

- 该项目依赖 EPPlus 生成 Excel，升级 EPPlus 时要注意许可证和导出格式。
- MES/PG 串口协议是客户现场边界，协议字段不要下沉到 Engine 通用层。
- 修改模板命名时必须同步 `MainWindow_CCPICompleted` 中的关键字匹配，例如 `White`、`Black`、`Red`。
- 修改判定标准时同步更新 `BlackMudraResult`、Excel 字段和窗口输出文本。
- 结果图显示失败时，先查文件是否仍在写入，再调整 `ViewImageReadDelay`。
