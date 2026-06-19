# ProjectShiyuan

`Projects/ProjectShiyuan/` 是视源客户定制项目包，运行时以 `ProjectShiyuan.dll` 加载。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `ProjectShiyuan` |
| `name` | `视源项目` |
| `version` | `1.0` |
| `dllpath` | `ProjectShiyuan.dll` |
| `requires` | `1.3.15.10` |

## 业务范围

ProjectShiyuan 面向视源客户定制测试流程，当前源码重点是 FlowEngine 模板执行、JND/POI 结果提取、客户数据目录输出和伪彩图保存。它不像 Heyuan/BlackMura 那样在当前代码里完成串口上传，而是更偏向“跑流程 -> 汇总算法结果 -> 复制和生成客户文件”。

## 主要源码入口

| 文件/目录 | 作用 |
| --- | --- |
| `ShiyuanProjectWindow.xaml(.cs)` | 主窗口 |
| `ShiyuanProjectExport.cs` | 功能启动器和工具菜单 |
| `ProjectShiYuanConfig.cs` | 项目配置 |
| `TempResult.cs`、`NumSet.cs` | 临时判定结果和数值范围 |
| `SerialMsg.cs` | 串口消息模型，当前主窗口未形成完整上传链 |
| `manifest.json` | 插件装载清单 |
| `README.md`、`CHANGELOG.md` | 运行时帮助和版本说明 |

## 业务链路

1. 通过工具菜单 `ProjectShiyuan` 打开项目窗口。
2. 窗口初始化 MQTT 默认配置，创建 `FlowEngineControl` 和 `STNodeEditor`。
3. 选择 `TemplateFlow.Params` 中的 Flow 模板后，窗口加载 `DataBase64`，并为 `CVCommonNode` 绑定运行事件。
4. 点击运行时创建批次号，调用 `FlowControl.Start(sn)`，并写入 `MeasureBatchModel`。
5. `FlowCompleted` 后按批次号查 `AlgResultMasterDao`。
6. 根据结果类型分支处理 JND、POI、OLED JND CalVas 等结果。
7. 按 `DataPath` 保存 CSV、复制输入图或算法中间图。
8. 根据 JND 校验结果设置 `OK` / `NG`。

## 结果输出

当前代码会按结果类型做不同输出：

| 结果类型 | 处理方式 |
| --- | --- |
| `Compliance_Math_JND` | 读取 `ComplianceJNDDao`，显示到 `ListViewJNDresult`，所有 `Validate` 都为 true 才保持 OK |
| `POI_XYZ` | 读取 `PoiPointResultDao`，转换成 `PoiResultCIExyuvData`，显示到结果列表 |
| `OLED_JND_CalVas` | 转成 `ViewRsultJND`，保存 `{timestamp}_{SN}_JND.csv`，并复制输入图 |
| Flow 中的 `TPAlgorithmNode.ImgFileName` | 复制到 `DataPath`，文件名追加时间戳 |
| POI 汇总 | 保存 `{timestamp}_{SN}_POI.csv` |

当判定为 OK 时，还会尝试处理固定路径下的图：

```text
C:\Windows\System32\pic\h_gap.tif
C:\Windows\System32\pic\v_gap.tif
C:\Windows\System32\pic\luminance.tif
```

`h_gap` 和 `v_gap` 会先复制原图，再用 `OpenCVMediaHelper.M_PseudoColor` 生成伪彩图；`luminance` 当前只复制原图。

## 关键配置

| 配置 | 作用 |
| --- | --- |
| `SN` | 输出文件名和客户数据标识 |
| `DataPath` | 所有 CSV、输入图、伪彩图的输出目录 |
| `TemplateSelectedIndex` | 打开模板编辑器或 FlowEngine 工具时使用 |
| `LastFlowTime` | 上一次流程耗时，用于界面估算剩余时间 |
| `TestName` | 默认值 `WBROtest`，当前主链路里使用较少 |
| `IsAutoUploadSn`、`PortName`、`DeviceId` | 配置类中保留，但当前窗口没有完整串口上传链 |

## 当前边界

- `UploadSN` 事件处理器目前为空，不能把它写成已经完成的 SN 自动上传能力。
- `SerialMsg.cs` 只说明项目内保留串口消息模型，不能证明当前流程有完整 MES 上传。
- 固定读取 `C:\Windows\System32\pic\*.tif` 是现场耦合点，交付前必须确认目标机器是否会生成这些文件。
- 结果成功与否主要受 JND `Validate` 影响；POI CSV 保存本身不等于 PASS。

## 构建与交付

```powershell
dotnet build Projects/ProjectShiyuan/ProjectShiyuan.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectShiyuan --no-upload
```

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 菜单入口 | 从工具菜单打开 `ProjectShiyuan` | 只打开一个项目窗口，重复点击复用同一窗口实例 |
| 模板加载 | 选择 `TemplateFlow.Params` 中的 Flow 模板 | `FlowEngineControl` 能加载 `DataBase64`，节点编辑器可打开 |
| 批次执行 | 输入 SN，点击运行 | `BeginNewBatch()` 生成批次，`FlowCompleted` 能拿到对应算法结果 |
| JND 判定 | 使用包含 `Compliance_Math_JND` 的流程 | JND 结果显示到列表，任意 `Validate=false` 会使最终结果变为 NG |
| POI 导出 | 使用包含 `POI_XYZ` 的流程 | 生成 `{timestamp}_{SN}_POI.csv`，字段能对应 POI x/y/Y/u/v 等结果 |
| OLED JND 导出 | 使用 `OLED_JND_CalVas` 结果 | 生成 `{timestamp}_{SN}_JND.csv`，并复制算法输入图 |
| 固定图像处理 | 准备 `C:\Windows\System32\pic\h_gap.tif`、`v_gap.tif`、`luminance.tif` | h/v gap 生成原图和伪彩图，luminance 复制原图 |
| 输出目录 | 修改 `DataPath` 后运行 | 所有 CSV、复制图和伪彩图都落到新目录 |

## 故障首查

| 现象 | 首查点 | 处理 |
| --- | --- | --- |
| 点运行没有结果 | `TemplateSelectedIndex` 是否指向有效模板、`FlowCompleted` 是否触发 | 先确认 Flow 模板能在通用 FlowEngine 中单独跑通 |
| JND 显示 NG | `ComplianceJNDDao` 结果里的 `Validate` | 不要只看 CSV 是否生成，最终 OK/NG 受 `Validate` 控制 |
| POI CSV 为空 | `AlgResultMasterDao` 是否查到 `POI_XYZ`，批次号是否正确 | 核对当前 SN、批次号和模板输出类型 |
| 固定图没有生成 | `C:\Windows\System32\pic\*.tif` 是否存在，`DataPath` 是否可写 | 这是客户现场耦合点，不是 Engine 通用缺陷 |
| 串口上传无效 | `UploadSN` 当前为空、串口链路未完成 | 需要补代码后才能写成正式能力 |

## 交接注意事项

- README 当前偏模板化，接手时应优先对照真实窗口和客户现场流程补充细节。
- 修改 `DataPath` 输出规则时，同步更新 JND CSV、POI CSV、输入图复制和伪彩图保存说明。
- 若要启用串口/MES 上传，需要先补齐 `UploadSN`、串口连接、协议返回和失败处理，不能只改文档。
- 固定路径图片依赖属于客户现场边界，不要下沉到 Engine 通用层。
- 不要把视源客户专用逻辑放入 Engine 通用层。
