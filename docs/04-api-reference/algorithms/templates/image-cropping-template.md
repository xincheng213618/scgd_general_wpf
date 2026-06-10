# ImageCropping 图像裁剪模板

`ImageCropping/` 负责发光区裁剪参数、手动裁剪算法入口、Flow 裁剪节点和裁剪结果展示。它和 `Jsons/ImageROI` 不是同一个模块：本页描述的是旧的强类型模板 `TemplateImageCropping` 和 `Event_Image_Cropping` 链路。

## 先看结论

- 模板类：`TemplateImageCropping`
- 参数类：`ImageCroppingParam`
- 模板字典：`TemplateDicId = 32`
- 模板编码：`Code = "ImageCropping"`
- 手动算法入口：`AlgorithmImageCropping`
- MQTT 事件：`Event_Image_Cropping`
- Flow 算子：`OLED.GetRIAand`
- 结果类型：`ViewResultAlgType.Image_Cropping`
- 结果 handler：`ViewHandleImageCropping`

如果接手“裁剪没有结果”“裁剪结果表为空”或“Flow 里图像裁剪节点输入接不上”，先读本页，再对照 [Engine 结果展示与项目交接链路](../../engine-components/result-handoff-chain.md)。

## 源码入口

| 文件 | 作用 |
| --- | --- |
| `Engine/ColorVision.Engine/Templates/ImageCropping/TemplateImageCropping.cs` | 模板注册、`TemplateDicId`、`Code` 和 MySQL 恢复命令 |
| `Engine/ColorVision.Engine/Templates/ImageCropping/ImageCroppingParam.cs` | 裁剪模板参数 |
| `Engine/ColorVision.Engine/Templates/ImageCropping/AlgorithmImageCropping.cs` | 手动算法入口、四点 ROI 和 MQTT 请求 |
| `Engine/ColorVision.Engine/Templates/ImageCropping/DisplayImageCropping.xaml(.cs)` | 手动执行页 |
| `Engine/ColorVision.Engine/Templates/ImageCropping/ViewHandleImageCropping.cs` | 裁剪结果展示、表格和导出 |
| `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs` | `AlgorithmType.图像裁剪` 映射到 `OLED.GetRIAand` |
| `Engine/FlowEngineLib/Node/OLED/OLEDImageCroppingNode.cs` | 双输入裁剪节点，接收图像和 ROI 上游结果 |
| `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/OLEDNodeConfigurators.cs` | Flow 属性面板绑定 `TemplateImageCropping` |

## 参数模型

`ImageCroppingParam` 当前只有两个模板字段：

| 字段 | 类型 | 默认/来源 | 交接含义 |
| --- | --- | --- | --- |
| `UnEgde` | `int` | 代码默认 `1`，恢复 SQL 也写 `1` | 裁剪边缘处理参数，拼写保持源码现状 |
| `O_Index` | `string` | 恢复 SQL 默认 `[0,1,2,3]` | 输出顺序或点序索引参数，具体解释由算法服务决定 |

`PointFloat` 的 `X/Y` 不是模板保存字段，而是 `AlgorithmImageCropping` 手动执行时临时持有的四个 ROI 点：

- `Point1`
- `Point2`
- `Point3`
- `Point4`

因此交接时要明确：模板管理页保存的是 `UnEgde/O_Index`，手动执行页发送的四点 ROI 是运行时输入。

## 手动执行链路

手动页由 `AlgorithmImageCropping` 提供，显示名是 `发光区裁剪`，分类是 `数据提取算法`。

执行时 `DisplayImageCropping` 会：

1. 检查是否选择发光区裁剪模板。
2. 从批次、Raw/CIE 服务文件或本地文件中取图像输入。
3. 根据扩展名推断 `FileExtType`。
4. 从当前图像服务读取 `DeviceCode` 和 `DeviceType`。
5. 调用 `AlgorithmImageCropping.SendCommand(...)`。

发送给算法服务的参数包括：

| 参数 | 来源 |
| --- | --- |
| `ImgFileName` | 批次/服务文件/本地文件，若存在历史映射会替换成完整路径 |
| `FileType` | `.cvraw`、`.cvcie`、`.tif` 或 `Src` |
| `DeviceCode` | 图像来源服务 |
| `DeviceType` | 图像来源服务类型 |
| `TemplateParam` | 当前裁剪模板的 `Id` 和 `Name` |
| `ROI` | `Point1` 到 `Point4` 组成的 `PointFloat[]` |

事件名固定为 `MQTTAlgorithmEventEnum.Event_Image_Cropping`。

## Flow 接入

当前裁剪相关 Flow 路径有两类。

| Flow 路径 | 当前行为 |
| --- | --- |
| 通用 `AlgorithmNode` | `AlgorithmType.图像裁剪` 设置 `operatorCode = "OLED.GetRIAand"`，配置器绑定 `TemplateImageCropping` |
| `OLEDImageCroppingNode` | 节点名 `图像裁剪2`，输入口为 `IN_IMG` 和 `IN_ROI`，输出参数带 `ROI_MasterId` |

`OLEDImageCroppingNode.getBaseEventData(...)` 会从第 0 路输入取图像前序参数，从第 1 路输入取 ROI 前序 `MasterId`，再写入 `OLEDImageCroppingParam.ROI_MasterId`。这说明 Flow 里“图像裁剪2”不是靠手动四点 ROI，而是依赖上游 ROI 结果。

交接时要区分：

- 手动算法页：用户输入/界面绑定四个点。
- 通用 AlgorithmNode：按算法类型选择裁剪模板。
- OLED 双输入节点：从上游 ROI 结果拿 `ROI_MasterId`。

## 结果展示链路

`ViewHandleImageCropping` 支持 `ViewResultAlgType.Image_Cropping`。

加载时：

- 如果 `result.ViewResults == null`，通过 `AlgResultImageDao.Instance.GetAllByPid(result.Id)` 读取明细。
- 给结果右键菜单加“调试”，可回到 `AlgorithmImageCropping`。

展示时：

- 如果 `result.FilePath` 存在，打开原始图像。
- 左侧表格列为 `file_name`、`order_index`、`FileInfo`。
- 表格数据源是 `AlgResultImageModel` 明细集合。

`SideSave(...)` 当前会导出 CSV，并尝试保存当前图像视图。这里有一个需要交接验证的细节：代码同时把 `selectedPath` 当成 CSV 写入路径，又把它和 PNG 文件名 `Path.Combine`。如果外部调用传入的是文件路径而不是目录，PNG 保存路径可能不符合预期。现场验收导出时要单独验证 CSV 和图片是否都生成。

## MySQL 恢复

`TemplateImageCropping.GetMysqlCommand()` 返回 `MysqlImageCropping`，恢复 SQL 会写入：

- `t_scgd_sys_dictionary_mod_master`：`id = 32`、`code = ImageCropping`、`mod_type = 7`
- `t_scgd_sys_dictionary_mod_item`：`UnEgde` 和 `O_Index`

如果现场模板列表没有 ImageCropping，先查 `TemplateDicId = 32` 对应的字典主档和明细是否存在，再查 `TemplateImageCropping.Load()` 是否执行。

## 交接重点

- `ImageCropping` 当前是强类型模板，不是 `Jsons/ImageROI`。
- 手动四点 ROI 是运行时输入，不是 `ImageCroppingParam` 的持久字段。
- Flow 的 `OLEDImageCroppingNode` 依赖上游 ROI `MasterId`，不是依赖手动页的 `Point1..Point4`。
- 结果展示读取 `AlgResultImageDao`，排查表格为空要追主结果 `Id`、明细表和 `ViewResultAlgType.Image_Cropping`。
- `UnEgde` 拼写沿用源码，不要在文档或配置里擅自改成 `UnEdge`。

## 验收建议

| 场景 | 验收点 |
| --- | --- |
| 模板管理 | 能看到 `UnEgde` 和 `O_Index`，默认值符合现场配置 |
| 手动裁剪 | 能选择图像来源、模板和四点 ROI，并发送 `Event_Image_Cropping` |
| Flow 裁剪 | `图像裁剪2` 节点能接 `IN_IMG` 和 `IN_ROI`，裁剪结果关联正确 ROI 主结果 |
| 结果展示 | 历史结果能打开图像，左侧表格能看到 `file_name/order_index/FileInfo` |
| 导出 | CSV 和图片保存路径都要实际验证 |

## 继续阅读

- [Engine 结果展示与项目交接链路](../../engine-components/result-handoff-chain.md)
- [Engine 模板与 Flow 链路](../../engine-components/template-flow-chain.md)
- [ROI 原语](../primitives/roi.md)
- [JSON 模板](./json-templates.md)
- [当前算法模板覆盖清单](../current-algorithm-template-coverage.md)
