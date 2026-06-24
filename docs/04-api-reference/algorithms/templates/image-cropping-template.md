# ImageCropping 图像裁剪模板

`ImageCropping/` 负责发光区裁剪参数、手动裁剪算法入口、Flow 裁剪节点和裁剪结果展示。它和 `Jsons/ImageROI` 不是同一个模块：本页描述的是旧的强类型模板 `TemplateImageCropping` 和 `Event_Image_Cropping` 链路。

## 速查

| 项 | 值 |
| --- | --- |
| 模板类 / 参数类 | `TemplateImageCropping` / `ImageCroppingParam` |
| 模板字典 / 编码 | `TemplateDicId = 32` / `Code = "ImageCropping"` |
| 手动算法入口 | `AlgorithmImageCropping` |
| MQTT 事件 | `Event_Image_Cropping` |
| Flow 算子 | `OLED.GetRIAand` |
| 结果类型 / handler | `ViewResultAlgType.Image_Cropping` / `ViewHandleImageCropping` |

如果排查“裁剪没有结果”“裁剪结果表为空”或“Flow 里图像裁剪节点输入接不上”，先读本页，再对照 [Engine 结果展示链路](../../engine-components/result-handoff-chain.md)。

## 源码入口

| 文件 | 作用 |
| --- | --- |
| `TemplateImageCropping.cs` | 模板注册、`TemplateDicId`、`Code` 和 MySQL 恢复命令 |
| `ImageCroppingParam.cs` | 裁剪模板参数 |
| `AlgorithmImageCropping.cs`、`DisplayImageCropping.xaml(.cs)` | 手动算法入口、四点 ROI 和 MQTT 请求 |
| `ViewHandleImageCropping.cs` | 裁剪结果展示、表格和导出 |
| `AlgorithmNode.cs`、`OLEDImageCroppingNode.cs` | 通用算法节点和双输入裁剪节点 |
| `OLEDNodeConfigurators.cs` | Flow 属性面板绑定 `TemplateImageCropping` |

## 参数和输入

| 数据 | 来源 | 含义 |
| --- | --- | --- |
| `UnEgde` | 模板字段，默认 `1` | 裁剪边缘处理参数，拼写保持源码现状 |
| `O_Index` | 模板字段，默认 `[0,1,2,3]` | 输出顺序或点序索引，具体解释由算法服务决定 |
| `Point1..Point4` | `AlgorithmImageCropping` 手动执行时临时持有 | 四点 ROI，运行时输入，不是模板保存字段 |
| `ImgFileName` | 批次、服务文件或本地文件 | 若存在历史映射会替换成完整路径 |
| `FileType` | 文件扩展名 | `.cvraw`、`.cvcie`、`.tif` 或 `Src` |
| `DeviceCode` / `DeviceType` | 图像来源服务 | 发送算法请求时带给服务端 |

维护时要明确：模板管理页保存的是 `UnEgde/O_Index`，手动执行页发送的四点 ROI 是运行时输入。

## 执行链路

| 路径 | 当前行为 |
| --- | --- |
| 手动执行 | `DisplayImageCropping` 检查模板、选择图像输入、推断文件类型、读取图像服务，然后发送 `Event_Image_Cropping` |
| 通用 `AlgorithmNode` | `AlgorithmType.图像裁剪` 设置 `operatorCode = "OLED.GetRIAand"`，配置器绑定 `TemplateImageCropping` |
| `OLEDImageCroppingNode` | 节点名 `图像裁剪2`，输入口为 `IN_IMG` 和 `IN_ROI`，输出参数带 `ROI_MasterId` |

`OLEDImageCroppingNode.getBaseEventData(...)` 会从第 0 路输入取图像前序参数，从第 1 路输入取 ROI 前序 `MasterId`，再写入 `OLEDImageCroppingParam.ROI_MasterId`。它不依赖手动页的 `Point1..Point4`。

## 结果和恢复

| 主题 | 说明 |
| --- | --- |
| 结果读取 | `ViewHandleImageCropping` 支持 `ViewResultAlgType.Image_Cropping`；明细通过 `AlgResultImageDao.Instance.GetAllByPid(result.Id)` 读取 |
| 展示 | 打开 `result.FilePath` 指向的原始图像，表格列为 `file_name`、`order_index`、`FileInfo` |
| 导出 | `SideSave(...)` 导出 CSV，并尝试保存当前图像视图；现场要验证 CSV 和图片路径都正确 |
| MySQL 恢复 | `TemplateImageCropping.GetMysqlCommand()` 写入 `id = 32`、`code = ImageCropping`、`UnEgde`、`O_Index` |

如果模板列表没有 ImageCropping，先查 `TemplateDicId = 32` 字典主档和明细是否存在，再查 `TemplateImageCropping.Load()` 是否执行。

## 维护重点

- `ImageCropping` 当前是强类型模板，不是 `Jsons/ImageROI`。
- 手动四点 ROI 是运行时输入，不是 `ImageCroppingParam` 的持久字段。
- Flow 的 `OLEDImageCroppingNode` 依赖上游 ROI `MasterId`。
- 表格为空要追主结果 `Id`、明细表和 `ViewResultAlgType.Image_Cropping`。
- `UnEgde` 拼写沿用源码，不要在文档或配置里擅自改成 `UnEdge`。

## 验收

| 场景 | 验收点 |
| --- | --- |
| 模板管理 | 能看到 `UnEgde` 和 `O_Index`，默认值符合现场配置 |
| 手动裁剪 | 能选择图像来源、模板和四点 ROI，并发送 `Event_Image_Cropping` |
| Flow 裁剪 | `图像裁剪2` 节点能接 `IN_IMG` 和 `IN_ROI`，裁剪结果关联正确 ROI 主结果 |
| 结果展示 | 历史结果能打开图像，左侧表格能看到 `file_name/order_index/FileInfo` |
| 导出 | CSV 和图片保存路径都要实际验证 |
