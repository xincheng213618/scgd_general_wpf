---
knowledge_id: "algorithms.image-cropping"
knowledge_type: "topic"
status: "current"
summary: "区分强类型 ImageCropping 的持久参数、运行时四点 ROI、Flow 双输入和图像结果。"
aliases: ["图像裁剪四点保存在哪里","TemplateImageCropping","ImageCroppingDisplayAlgorithmConfig","OLEDImageCroppingNode"]
code_paths: ["Engine/ColorVision.Engine/Templates/ImageCropping/TemplateImageCropping.cs","Engine/ColorVision.Engine/Templates/ImageCropping/AlgorithmImageCropping.cs","Engine/ColorVision.Engine/Templates/ImageCropping/ViewHandleImageCropping.cs","Engine/FlowEngineLib/Node/OLED/OLEDImageCroppingNode.cs"]
test_paths: []
related: ["algorithms.index","algorithms.roi-routes","engine.results"]
---

# ImageCropping 图像裁剪模板

本页描述 `Engine/ColorVision.Engine/Templates/ImageCropping/` 的强类型裁剪适配链，不是 `Jsons/ImageROI` 或统一算法平台的全部 ROI 能力。

## 契约与位置

| 事项 | 当前实现 |
| --- | --- |
| 模板 / 参数 | `TemplateImageCropping` / `ImageCroppingParam` |
| 字典 / 编码 | `TemplateDicId = 32` / `ImageCropping` |
| 手动配置 | `AlgorithmImageCropping`、`ImageCroppingDisplayAlgorithmConfig` |
| 手动事件 | `Event_Image_Cropping` |
| Flow 算子 | `OLED.GetRIAand` |
| 结果 | `ViewResultAlgType.Image_Cropping` / `ViewHandleImageCropping` |

手动界面来自通用 `Services/Devices/Algorithm/DisplayAlgorithmControl.xaml.cs`，不是已移除的 `DisplayImageCropping.xaml`。模板入口见 [模板编辑路由](./template-menu-entries.md)。

## 持久参数与运行时输入

| 数据 | 来源 | 边界 |
| --- | --- | --- |
| `UnEgde` | `ImageCroppingParam`，默认 1 | 服务参数；保持现有拼写，不改成 UnEdge |
| `O_Index` | `ImageCroppingParam`，默认 `[0,1,2,3]` | 点序/输出顺序具体语义以服务实现为准 |
| `Point1..Point4` | `ImageCroppingDisplayAlgorithmConfig` | 手动运行时四点，不属于模板持久字段 |
| `ImageFilePath` | 通用配置基类 | 手动输入图像路径 |
| `ROI_MasterId` | Flow 上游 ROI 主结果 | 双输入节点的关联 ID，不是四点数组 |

## 手动执行

`Execute()` 从 `Config.Template` 解析 `ImageCroppingParam`，检查图像输入，再调用 `SendCommand`。请求包含 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam` 和 `ROI = [Config.Point1, Config.Point2, Config.Point3, Config.Point4]`；手动入口当前传空设备 code/type。

修改模板管理页不会持久保存四点配置。需要复现实验时同时记录参数模板、四点、输入图和实际服务请求，不能仅导出模板。

## Flow 执行

通用 `AlgorithmNode` 的“图像裁剪”选择设置 `operatorCode = OLED.GetRIAand`。`OLEDImageCroppingNode` 名为“图像裁剪2”，有 `IN_IMG` 与 `IN_ROI` 两个输入；`getBaseEventData` 从第 0 路取图像参数，第 1 路取 ROI 的 `MasterId`，写到 `OLEDImageCroppingParam.ROI_MasterId`。

该路径不使用手动配置的四点。节点选择器与配置机制见 [PropertyGrid](../../ui-components/property-grid.md) 和 [模板与 Flow](../../engine-components/template-flow-chain.md)。

## 结果与模板恢复

| 问题 | 入口与约束 |
| --- | --- |
| 明细为空 | `AlgResultImageDao.Instance.GetAllByPid(result.Id)`，核对主结果 ID 与类型 |
| 图像和表格 | `ViewHandleImageCropping` 展示原始结果图像；列为 `file_name`、`order_index`、`FileInfo` |
| 导出 | `SideSave` 导出 CSV 并尝试保存当前视图；两个路径都需实际核对 |
| 模板缺失 | 字典 32、`TemplateImageCropping.Load` 与 `GetMysqlCommand` 的恢复定义 |

源图缺失与通用展示恢复见 [结果链](../../engine-components/result-handoff-chain.md)；生成可显示画布不等于恢复原始像素。验证应分别覆盖手动四点、Flow ROI 主结果关联、历史明细和导出。

## 验证入口与缺口

验证缺口：未登记强类型裁剪模板的专门自动化测试；需分别验证通用手动宿主的 ROI 输入与 Flow 的 ROI_MasterId，且核对历史图像与 CSV。
