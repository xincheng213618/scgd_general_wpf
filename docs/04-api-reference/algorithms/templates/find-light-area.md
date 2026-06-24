# FindLightArea 发光区定位模板

本页说明 `Engine/ColorVision.Engine/Templates/FindLightArea/` 的真实处理链路。它不是通用 ROI SDK，而是“模板参数 -> 图像输入 -> MQTT 算法请求 -> 发光区点位结果 -> 图像凸包覆盖层”的业务模板。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 模板代码 | `FindLightArea` |
| 模板类 | `TemplateRoi : ITemplate<RoiParam>, IITemplateLoad` |
| 参数类 | `RoiParam` |
| 执行入口 | `AlgorithmRoi`，显示名“发光区定位1” |
| UI 面板 | `DisplayRoi.xaml(.cs)` |
| MQTT 事件 | `MQTTAlgorithmEventEnum.Event_LightArea2_GetData` |
| 结果处理 | `ViewHandleFindLightArea` |
| 结果表 | `t_scgd_algorithm_result_detail_light_area` |

## 源码入口

| 文件 | 用途 |
| --- | --- |
| `TemplateRoi.cs` | 注册 `FindLightArea` 模板，设置 `TemplateDicId = 31`，并通过 `MysqlRoi` 恢复模板字典。 |
| `ROIParam.cs` | 保存 ROI 参数：`Threshold`、`Times`、`SmoothSize`。 |
| `AlgorithmRoi.cs` | 组装算法请求，填入图像、设备和模板参数，并发布 MQTT 命令。 |
| `DisplayRoi.xaml.cs` | 提供模板选择、图像选择、批次号/Raw/本地文件输入和执行按钮。 |
| `AlgResultLightAreaDao.cs` | 定义结果模型、结果加载、图像覆盖层和列表展示。 |
| `MysqlRoi.cs` | 恢复 MySQL 字典和默认模板项。 |

## 执行链路

1. `TemplateRoi` 被模板系统扫描到后，进入 `TemplateControl` 的全局模板集合。
2. 用户在算法面板选择 `TemplateRoi.Params` 中的一个 `RoiParam`。
3. `DisplayRoi` 支持三类输入：批次号、算法服务 Raw/CIE 文件、本地图像文件。
4. 文件扩展名会被映射为 `Raw`、`CIE`、`Tif` 或 `Src`；如果算法服务的 `HistoryFilePath` 里能找到历史路径，会先替换成完整路径。
5. `AlgorithmRoi.SendCommand(...)` 组装参数：
   - `ImgFileName`
   - `FileType`
   - `DeviceCode`
   - `DeviceType`
   - `TemplateParam`，只传模板 `ID` 和 `Name`
6. 命令通过 `DService.PublishAsyncClient(...)` 发往 `Event_LightArea2_GetData`。
7. 结果回写后，`ViewHandleFindLightArea` 按 `ViewResultAlgType.LightArea` / `FindLightArea` 加载点位并展示。

## 参数说明

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `Threshold` | `1` | 发光区阈值。现场调整时要记录图像类型和曝光条件，否则阈值没有可复现意义。 |
| `Times` | `1` | 算法侧迭代/处理次数参数。具体语义由算法服务解释。 |
| `SmoothSize` | `1` | 平滑尺寸。会影响边界点稳定性，变更后要看结果凸包而不是只看点表。 |

## 结果展示

`AlgResultLightAreaModel` 只保存 `PosX`、`PosY` 和父结果 `Pid`。展示时会把所有点传给 `GrahamScan.ComputeConvexHull(...)`，再用蓝色透明 `DVPolygon` 画在图像上。

这意味着维护时要注意两个边界：

- 点位列表和凸包不是同一个概念。点很多但凸包异常，通常要回看输入图像、阈值和平滑参数。
- 当前 `SideSave(...)` 会创建导出文件，但实现中没有写入点位行。不要把它当作稳定 CSV 导出能力，若现场需要导出，应先补齐实现和验收样例。

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 模板下拉为空 | `TemplateRoi` 是否被程序集装载，`IITemplateLoad` 是否执行，`TemplateDicId = 31` 的字典是否恢复。 |
| 点击执行提示未选模板 | `TemplateRoi.Params` 是否加载到 `ComboxTemplate.ItemsSource`。 |
| 算法服务收不到图像 | `ImgFileName` 是本地路径、Raw 文件名还是历史路径；`FileType` 是否和扩展名匹配。 |
| 结果页无点位 | 结果类型是否是 `LightArea` 或 `FindLightArea`，`t_scgd_algorithm_result_detail_light_area.pid` 是否对应主结果。 |
| 覆盖层形状异常 | 先看 `Threshold`、`Times`、`SmoothSize` 和输入图像，再看 `GrahamScan` 凸包输入点。 |

## 检查清单

- 修改参数时，同时更新 `ROIParam.cs`、`MysqlRoi.cs` 和现场推荐值。
- 修改执行事件时，同时更新 `AlgorithmRoi.SendCommand(...)`、Flow 节点说明和本页。
- 修改结果结构时，同时更新 `AlgResultLightAreaModel`、结果表、展示列和导出逻辑。
- 若要把发光区结果交给项目包使用，项目文档必须说明读取的是点位、凸包还是原始图像区域。
