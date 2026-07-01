# ProjectShiyuan

`Projects/ProjectShiyuan/` 是视源客户定制项目包，运行时以 `ProjectShiyuan.dll` 加载。当前源码重点是 FlowEngine 模板执行、JND/POI 结果提取、客户数据目录输出和伪彩图保存。

## manifest 和源码

| 项 | 当前值 |
| --- | --- |
| `Id` / `name` / `version` | `ProjectShiyuan` / `视源项目` / `1.0` |
| `dllpath` / `requires` | `ProjectShiyuan.dll` / `1.3.15.10` |
| 主窗口 | `ShiyuanProjectWindow.xaml(.cs)` |
| 菜单 | `ShiyuanProjectExport.cs` |
| 配置 | `ProjectShiYuanConfig.cs` |
| 结果和数值 | `TempResult.cs`、`NumSet.cs` |
| 串口模型 | `SerialMsg.cs`，当前主窗口未形成完整上传链 |

## 业务链路

工具菜单打开项目窗口后，窗口初始化 MQTT 默认配置，创建 `FlowEngineControl` 和 `STNodeEditor`。选择 `TemplateFlow.Params` 中的 Flow 模板后加载 `DataBase64` 并绑定节点事件。运行时创建批次号，调用 `FlowControl.Start(sn)`，写入 `MeasureBatchModel`；`FlowCompleted` 后按批次查 `AlgResultMasterDao`，分支处理 JND、POI、OLED JND CalVas 等结果，并按 `DataPath` 保存 CSV、复制输入图或算法中间图。最终 OK/NG 主要由 JND 校验结果决定。

## 结果输出

| 结果类型 | 处理方式 |
| --- | --- |
| `Compliance_Math_JND` | 读取 `ComplianceJNDDao`，显示到 `ListViewJNDresult`，所有 `Validate` 都为 true 才保持 OK |
| `POI_XYZ` | 读取 `PoiPointResultDao`，转换成 `PoiResultCIExyuvData`，显示到结果列表 |
| `OLED_JND_CalVas` | 转成 `ViewRsultJND`，保存 `{timestamp}_{SN}_JND.csv`，并复制输入图 |
| Flow 中的 `TPAlgorithmNode.ImgFileName` | 复制到 `DataPath`，文件名追加时间戳 |
| POI 汇总 | 保存 `{timestamp}_{SN}_POI.csv` |

OK 时还会尝试处理固定路径下的图：

```text
C:\Windows\System32\pic\h_gap.tif
C:\Windows\System32\pic\v_gap.tif
C:\Windows\System32\pic\luminance.tif
```

`h_gap` 和 `v_gap` 会复制原图并用 `OpenCVMediaHelper.M_PseudoColor` 生成伪彩图；`luminance` 当前只复制原图。

## 配置和边界

| 配置或边界 | 说明 |
| --- | --- |
| `SN` | 输出文件名和客户数据标识 |
| `DataPath` | 所有 CSV、输入图、伪彩图的输出目录 |
| `TemplateSelectedIndex` | 打开模板编辑器或 FlowEngine 工具时使用 |
| `LastFlowTime` | 上一次流程耗时，用于界面估算剩余时间 |
| `TestName` | 默认 `WBROtest`，当前主链路使用较少 |
| `IsAutoUploadSn`、`PortName`、`DeviceId` | 配置类中保留，但当前窗口没有完整串口上传链 |
| `UploadSN` | 事件处理器目前为空，不能写成 SN 自动上传能力 |
| 固定图片路径 | 现场耦合点，交付前必须确认目标机器会生成这些文件 |

## 构建与验收

```powershell
dotnet build Projects/ProjectShiyuan/ProjectShiyuan.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectShiyuan
```

| 验收项 | 通过标准 |
| --- | --- |
| 菜单入口 | 工具菜单打开项目窗口，重复点击复用同一窗口实例 |
| 模板加载 | `FlowEngineControl` 能加载 `DataBase64`，节点编辑器可打开 |
| 批次执行 | 输入 SN 后生成批次，`FlowCompleted` 能拿到对应算法结果 |
| JND 判定 | 任意 `Validate=false` 会使最终结果变为 NG |
| POI/OLED JND 导出 | 生成 `{timestamp}_{SN}_POI.csv` 或 `{timestamp}_{SN}_JND.csv`，字段对应结果 |
| 固定图像处理 | h/v gap 生成原图和伪彩图，luminance 复制原图 |
| 输出目录 | 修改 `DataPath` 后，CSV、复制图和伪彩图都落到新目录 |

## 故障首查

| 现象 | 首查点 |
| --- | --- |
| 点运行没有结果 | `TemplateSelectedIndex`、`FlowCompleted`，先确认 Flow 模板能单独跑通 |
| JND 显示 NG | `ComplianceJNDDao` 结果里的 `Validate` |
| POI CSV 为空 | `AlgResultMasterDao` 是否查到 `POI_XYZ`，批次号是否正确 |
| 固定图没有生成 | `C:\Windows\System32\pic\*.tif` 是否存在，`DataPath` 是否可写 |
| 串口上传无效 | `UploadSN` 当前为空、串口链路未完成 |
