# ProjectBlackMura

`Projects/ProjectBlackMura/` 是显示面板 Black Mura 缺陷检测项目包，运行时以 `ProjectBlackMura.dll` 加载。它串联 PG 上电/切图、五色 Flow、Engine 结果解析、POI overlay、Excel 报告和 MES/串口状态。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| PG 不上电 | 串口号/波特率、`HYMesConfig.DeviceId`、`CON,C,{DeviceId}` 是否发出 |
| 收到 `CON,S` 不进 White | `CONCompleted`、`StepIndex=1`、`HYMesConfig.IsSingleMes` |
| `CCPI,S` 后不推进 | `CCPICompleted`、当前 `BlackMuraTestType`、切图 ID、`StepIndex` |
| 找不到模板 | Flow 模板名是否包含 `White`、`Black`、`Red`、`Green`、`Blue` |
| Flow 成功但报告空 | `AlgResultMasterDao`、`PoiPointResultDao`、Black Mura JSON、字段映射 |
| Excel 没生成 | `ResultSavePath` 权限、SN 截断、EPPlus、`ExcelReportGenerator` |
| overlay 位置异常 | 结果图路径、`ViewImageReadDelay`、POI 坐标系 |
| 结束后 PG 没下电 | Blue 完成链路、`PGPowerOff()`、`COFF,S` |

## 流程顺序

```text
None -> White -> Black -> Red -> Green -> Blue
```

`White`、`Black` 用于亮度均匀性、梯度、对比度等核心判定；`Red`、`Green`、`Blue` 主要用于颜色和波长类结果汇总。

## 业务链路

| 模式 | 当前链路 |
| --- | --- |
| 手动单流程 | 输入 SN -> 选择含颜色关键字的 Flow -> `RunTemplate()` -> `FlowControl.Start(sn)` -> 解析结果 -> POI overlay -> 单次模式生成 Excel |
| 自动整机 | `Test1_Click` -> `PGPowerOn()` -> `CON,S` -> 多次 `CCPI,S` 推进五色 Flow -> Blue 完成 -> `UploadSN(...)` -> 生成 Excel -> `PGPowerOff()` |

## 串口/MES 指令

| 指令 | 方向 | 作用 |
| --- | --- | --- |
| `CON,C,{DeviceId}` | 发送 | PG 上电 |
| `COFF,C,{DeviceId}` | 发送 | PG 下电 |
| `CCPI,C,{DeviceId},{id}` | 发送 | PG 切图 |
| `CSN,C,{DeviceId},{sn}` | 发送 | 上传产品 SN |
| `CGI,C,{DeviceId},Default,{Msg}` | 发送 | 上传 NG 信息 |
| `CON,S` / `COFF,S` / `CCPI,S` / `CSN,S` | 接收 | 对应动作返回，末尾字段含 `0` 时按成功处理 |

报文用 `0x02` / `0x03` 包裹，串口波特率为 38400。

## 结果、配置和文件

| 类别 | 入口 | 说明 |
| --- | --- | --- |
| 主窗口 | `MainWindow.xaml.cs` | 手动单流程、自动整机、五色推进 |
| 配置 | `ProjectBlackMuraConfig.cs`、`Config/EditARVRConfig.xaml.cs` | SN 截断、报告路径、重试次数、读图延迟、联机模式 |
| MES/串口 | `HYMesManager.cs` | `CON/COFF/CCPI/CSN/CGI` 指令收发 |
| Excel | `ExcelReportGenerator.cs` | 输出 `<SN>.xlsx` 到 `ResultSavePath` |
| 基础信息 | `BlackMudraResult` | SN、Model、时间 |
| 五色亮度 | `Measurements` | Mean、Max、Min、Uniformity |
| 色彩结果 | `PoiResultCIExyuvData` | x、y、Wavelength、Saturation |
| 梯度/对比 | Black Mura JSON | White/Black 梯度和 Contrast |
| Mura 位置 | Black Mura JSON | Border size、Mura coordinate |

## 版本和构建

`manifest.json` 当前为 `Id=ProjectBlackMura`、`version=1.0`、`dllpath=ProjectBlackMura.dll`、`requires=1.3.15.10`。

```powershell
dotnet build Projects/ProjectBlackMura/ProjectBlackMura.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectBlackMura
```

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 手动流程 | 模板关键字能识别，结果图和 POI overlay 正常 |
| PG 上电 | 收到 `CON,S` 成功后 `StepIndex=1` 并触发 White |
| 五色切图 | `CCPI,S` 后 White/Black/Red/Green/Blue 顺序推进 |
| 重试机制 | `TryCount < TryCountMax` 时重跑，超出按失败处理 |
| 结果解析 | 亮度、均匀性、色坐标、波长和 Mura 字段填充 |
| Excel | `ResultSavePath\<SN>.xlsx` 存在且字段正确 |
| PG 下电 | 整机结束后发送 `COFF,C,{DeviceId}` |

## 变更影响

| 改动 | 必须同步检查 |
| --- | --- |
| 修改颜色顺序/新增画面 | `BlackMuraTestType`、`MainWindow_CCPICompleted`、切图 ID、Excel 字段 |
| 修改模板命名 | `RunTemplate()` 的颜色关键字判断 |
| 修改判定字段 | `BlackMudraResult`、Excel、窗口结果区、MES NG 信息 |
| 修改串口协议 | `HYMesManager` 发送/接收解析、现场 `DeviceId` |
| 修改结果图读取 | `ViewImageReadDelay`、文件存在性、overlay 坐标 |
