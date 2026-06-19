# ProjectHeyuan

`Projects/ProjectHeyuan/` 是河源精电客户定制项目包，运行时以 `ProjectHeyuan.dll` 加载。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `ProjectHeyuan` |
| `version` | `1.0` |
| `dllpath` | `ProjectHeyuan.dll` |
| `requires` | `1.3.15.10` |

## 业务范围

ProjectHeyuan 面向河源精电现场，负责四点颜色/亮度类测试和客户串口过站。它把 FlowEngine 模板、POI/Compliance 结果、CSV 留痕、串口上传和 NG 过站确认放在同一个窗口里。

当前窗口固定关注四个测试点：

```text
White, Blue, Red, Orange
```

这四个点会被整理成 `TempResult`，并最终用于 PASS/FAIL、CSV 记录和 MES 上传。

## 主要源码入口

| 文件/目录 | 作用 |
| --- | --- |
| `ProjectHeyuanWindow.xaml(.cs)` | 主窗口 |
| `MenuItemHeyuan.cs` | 功能启动器和工具菜单 |
| `HYMesManager.cs` | MES 管理 |
| `SerialMsg.cs` | 串口消息 |
| `ConnectConverter.cs` | 连接状态转换 |
| `NumSet.cs` | 数值设定 |
| `TempResult.cs` | 临时结果 |
| `manifest.json` | 插件装载清单 |

## 业务链路

1. 工具菜单打开河源项目窗口，窗口 DataContext 指向 `HYMesManager`。
2. 选择串口，`HYMesManager.OpenPort()` 以 38400 波特率建立连接。
3. 选择 `TemplateFlow.Params` 中的 Flow 模板，窗口用 `FlowEngineControl` 加载 `DataBase64`。
4. 执行前检查 SN；如果没有勾选自动上传 SN，会先调用 `UploadSN()`。
5. `FlowControl.Start(sn)` 执行流程，并用 `BeginNewBatch()` 写入 `MeasureBatchModel`。
6. `FlowCompleted` 后读取批次对应的 `POI_XYZ` 和 `Compliance_Math_CIE_XYZ` 结果。
7. 结果必须整理出 4 个 POI，否则项目会上报 `流程结果数据错误`。
8. 按 `White -> Blue -> Red -> Orange` 顺序写入结果、判定 PASS/FAIL、保存 CSV。
9. PASS 时调用 `UploadMes()`；FAIL 时提示是否 NG 过站，并调用 `UploadNG()`。

## 串口协议

`HYMesManager` 把所有发送报文包装为：

```text
0x02 + ASCII 文本 + 0x03
```

当前主要命令：

| 指令 | 作用 |
| --- | --- |
| `CSN,C,{DeviceId},{SN}` | 上传条码或产品编号 |
| `CMI,C,{DeviceId},{TestName},White,...,Blue,...,Red,...,Orange,...` | 上传四点测量结果 |
| `CGI,C,{DeviceId},Default,{Msg}` | 上传 NG 信息 |
| `CPT,C,{DeviceId}` | 发送过站/后处理指令 |

接收侧会处理 `CSN,S`、`CPT,S`、`CGI,S`、`CMI,S`。末尾字段包含 `0` 时按成功处理。`CMI,S` 成功后会继续调用 `SendPost()`。

## 结果字段

每个颜色点会输出：

| 字段 | 来源 |
| --- | --- |
| `x`、`y` | `PoiResultCIExyuvData` |
| `Lv` | `PoiResultCIExyuvData.Y` |
| `Dw` | `PoiResultCIExyuvData.Wave` |
| `Result` | `ComplianceXYZModel.ValidateResult` |

CSV 文件保存在 `HYMesConfig.DataPath`，文件名为：

```text
yyyy-MM-dd_{TestName}_{MachineName}.csv
```

CSV 中包含序号、型号、产品编号、时间、White/Red/Orange/Blue 的 x/y/lv/wl 和最终结果。

## 关键配置

| 配置 | 作用 |
| --- | --- |
| `PortName` | 默认串口名 |
| `DeviceId` | 客户设备编号，直接进入协议字段 |
| `TestName` | 上传 MES 和 CSV 文件名中的测试名称，默认 `WBROtest` |
| `DataPath` | CSV 输出目录 |
| `IsAutoUploadSn` | SN 改变时是否自动发送 `CSN` |
| `IsOpenConnect` | 启动时是否自动打开串口 |

## 构建与交付

```powershell
dotnet build Projects/ProjectHeyuan/ProjectHeyuan.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectHeyuan --no-upload
```

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 串口连接 | 选择现场串口并打开 | `IsConnect=true`，接收缓冲能解析 STX/ETX 包 |
| SN 上传 | 输入 SN，手动或自动触发 `UploadSN()` | 发送 `CSN,C,{DeviceId},{SN}`，收到 `CSN,S` 成功 |
| Flow 执行 | 选择包含四点结果的模板并运行 | `FlowCompleted` 后能整理出 White、Blue、Red、Orange 四个 `TempResult` |
| PASS 上传 | 四点全部通过 | 发送 `CMI,C,...`，收到 `CMI,S` 成功后继续 `CPT,C,{DeviceId}` |
| NG 上传 | 任一点失败或流程异常 | 发送 `CGI,C,{DeviceId},Default,{Msg}`，必要时再按现场策略过站 |
| CSV 留痕 | 运行结束后检查 `DataPath` | 生成 `yyyy-MM-dd_{TestName}_{MachineName}.csv`，字段顺序与客户表头一致 |
| 数据顺序 | 打开 CSV 和上传报文 | 文档、CSV、`UploadMes()` 都按 White、Blue、Red、Orange 理解结果 |

## 故障首查

| 现象 | 首查点 | 处理 |
| --- | --- | --- |
| 串口能打开但没回包 | 波特率 38400、STX/ETX 包边界、`DeviceId` | 先抓原始串口日志，不要先改 Flow |
| 上传 SN 失败 | `CSN,S` 末尾字段是否包含 `0` | 失败时不要继续把流程结果当作已绑定 SN |
| 结果数量不足 | `POI_XYZ` 与 `Compliance_Math_CIE_XYZ` 是否都存在 | 模板必须输出 4 个可匹配颜色名称的结果 |
| PASS 后不过站 | `CMI,S` 是否成功、`SendPost()` 是否发送 `CPT` | 区分结果上传失败和过站失败 |
| CSV 和 MES 字段不一致 | `CsvHandler` 表头、`UploadMes()` 拼接顺序 | 修改颜色顺序时必须同时改两处 |

## 交接注意事项

- 串口和 MES 协议属于客户现场边界，修改前需要确认现场设备和客户系统版本。
- Flow 结果必须包含 4 个 POI；如果模板只输出部分颜色点，项目会按 NG 或错误处理。
- `Button_Click` 中当前会调用两次 `flowControl.Start(sn)`，如果现场出现重复流程或重复批次，应优先检查这里。
- 修改 `TestName`、颜色顺序或字段格式时，同步 CSV、`UploadMes()` 和客户协议文档。
- 窗口状态和连接状态问题优先查 `ProjectHeyuanWindow`、`SerialMsg` 和 `HYMesManager`。
