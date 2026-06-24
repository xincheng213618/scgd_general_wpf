# ProjectHeyuan

`Projects/ProjectHeyuan/` 是河源精电客户定制项目包，运行时以 `ProjectHeyuan.dll` 加载。它把四点颜色/亮度测试、Flow 结果整理、CSV 留痕、串口上传和 NG 过站确认放在同一个窗口里。

## 先查什么

| 现象 | 优先看 |
| --- | --- |
| 项目菜单没有出现 | `manifest.json`、`ProjectHeyuan.dll`、宿主版本 |
| 串口打开但没回包 | 38400 波特率、STX/ETX 包边界、`DeviceId` |
| 上传 SN 失败 | `CSN,S` 末尾字段是否包含 `0` |
| 结果数量不足 | `POI_XYZ` 与 `Compliance_Math_CIE_XYZ` 是否都存在 4 个颜色点 |
| PASS 后不过站 | `CMI,S` 是否成功，`SendPost()` 是否发送 `CPT` |
| CSV 和 MES 字段不一致 | `CsvHandler` 表头和 `UploadMes()` 拼接顺序 |

## 清单和源码

| 项 | 当前值 |
| --- | --- |
| `Id` / `version` / `dllpath` | `ProjectHeyuan` / `1.0` / `ProjectHeyuan.dll` |
| `requires` | `1.3.15.10` |
| 固定颜色顺序 | `White, Blue, Red, Orange` |
| 主窗口 | `ProjectHeyuanWindow.xaml(.cs)` |
| 菜单 | `MenuItemHeyuan.cs` |
| MES/串口 | `HYMesManager.cs`、`SerialMsg.cs` |
| 结果模型 | `TempResult.cs` |
| 配置辅助 | `ConnectConverter.cs`、`NumSet.cs`、`manifest.json` |

## 业务链路

工具菜单打开窗口后，DataContext 指向 `HYMesManager`。现场选择串口并以 38400 波特率连接，再选择 `TemplateFlow.Params` 中的 Flow 模板。执行前检查 SN，必要时先 `UploadSN()`；`FlowControl.Start(sn)` 创建批次并运行流程。`FlowCompleted` 后读取 `POI_XYZ` 和 `Compliance_Math_CIE_XYZ`，整理出 White、Blue、Red、Orange 四个 `TempResult`，再判定 PASS/FAIL、保存 CSV、上传 MES 或 NG 信息。

## 串口和结果

报文格式固定为：

```text
0x02 + ASCII 文本 + 0x03
```

| 指令 | 作用 |
| --- | --- |
| `CSN,C,{DeviceId},{SN}` | 上传条码或产品编号 |
| `CMI,C,{DeviceId},{TestName},White,...,Blue,...,Red,...,Orange,...` | 上传四点测量结果 |
| `CGI,C,{DeviceId},Default,{Msg}` | 上传 NG 信息 |
| `CPT,C,{DeviceId}` | 发送过站/后处理指令 |

接收侧处理 `CSN,S`、`CPT,S`、`CGI,S`、`CMI,S`。末尾字段包含 `0` 时按成功处理，`CMI,S` 成功后继续调用 `SendPost()`。

| 输出字段 | 来源 |
| --- | --- |
| `x`、`y`、`Lv`、`Dw` | `PoiResultCIExyuvData` 的 x/y/Y/Wave |
| `Result` | `ComplianceXYZModel.ValidateResult` |
| CSV | `HYMesConfig.DataPath/yyyy-MM-dd_{TestName}_{MachineName}.csv` |

## 配置和构建

| 配置 | 作用 |
| --- | --- |
| `PortName` / `DeviceId` | 串口名和客户设备编号 |
| `TestName` | 上传 MES 和 CSV 文件名中的测试名称，默认 `WBROtest` |
| `DataPath` | CSV 输出目录 |
| `IsAutoUploadSn` / `IsOpenConnect` | SN 改变时自动上传、启动时自动打开串口 |

```powershell
dotnet build Projects/ProjectHeyuan/ProjectHeyuan.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectHeyuan --no-upload
```

## 验收和注意事项

| 验收项 | 通过标准 |
| --- | --- |
| 串口连接 | `IsConnect=true`，接收缓冲能解析 STX/ETX 包 |
| SN 上传 | 发送 `CSN,C,{DeviceId},{SN}`，收到 `CSN,S` 成功 |
| Flow 执行 | `FlowCompleted` 后整理出 White、Blue、Red、Orange 四个 `TempResult` |
| PASS 上传 | 发送 `CMI,C,...`，收到 `CMI,S` 成功后继续 `CPT,C,{DeviceId}` |
| NG 上传 | 发送 `CGI,C,{DeviceId},Default,{Msg}`，必要时按现场策略过站 |
| CSV 留痕 | CSV 文件生成，字段顺序与客户表头一致 |

串口和 MES 协议属于客户现场边界，修改前要确认设备和客户系统版本。Flow 结果必须包含 4 个 POI；`Button_Click` 当前会调用两次 `flowControl.Start(sn)`，现场出现重复流程或重复批次时优先检查这里。修改 `TestName`、颜色顺序或字段格式时，同步 CSV、`UploadMes()` 和客户协议文档。
