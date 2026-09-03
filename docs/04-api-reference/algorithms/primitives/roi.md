---
knowledge_id: "algorithms.roi-routes"
knowledge_type: "index"
status: "current"
summary: "按用途定位发光区、传统与 JSON 裁剪、SFR 寻边和中立算法 ROI 模型；各分支参数与坐标契约分别维护。"
aliases: ["ROI到底对应哪个模块","TemplateRoi","TemplateImageROI","TemplateSFRFindROI","AlgorithmRoi"]
code_paths: ["Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs","Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs","Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs","Engine/ColorVision.Engine/Templates/ImageCropping/TemplateImageCropping.cs","UI/ColorVision.Algorithms/AlgorithmInvocation.cs"]
test_paths: []
related: ["algorithms.index","algorithms.find-light-area","algorithms.image-cropping","algorithms.json-templates","algorithms.arvr","algorithms.platform"]
---

# ROI 模型与模板入口

ROI 表示感兴趣区域，在 ColorVision 中由不同能力定义参数与坐标语义。本页用于选择入口；共享名称不表示各分支的模板、坐标和持久化格式可以互换。

## 按用途选择

| 用途 | 模型或模板 | 操作与契约 |
| --- | --- | --- |
| 远端发光区定位 | `TemplateRoi : ITemplate<RoiParam>`；编码 `FindLightArea`，字典 `31` | **基础算法 → 发光区定位1**；阈值模板、服务请求与字典恢复限制见[发光区定位](../templates/find-light-area.md) |
| 本地发光区四角定位 | `RobustV2` 与 `LocalFindLuminousAreaNode`，不要求检测参数模板 | [本地 V2 的搜索区域、图像来源与 POI 回写](../templates/find-light-area.md#配置本地发光区定位-v2) |
| JSON 图像裁剪 | `TemplateImageROI`，编码 `Image.ROI`、字典 `52` | **Json → ImageCrop**，发送 `Image.ROI`；编辑方式见 [JSON 模板](../templates/json-templates.md) |
| 传统四点图像裁剪 | `TemplateImageCropping`，编码 `ImageCropping`、字典 `32` | 持久参数、运行时四点和 Flow 双输入见 [ImageCropping](../templates/image-cropping-template.md) |
| ARVR SFR 寻边 | `TemplateSFRFindROI`，JSON 编码 `ARVR.SFR.FindROI`、字典 `36` | **Json → SFR寻边**；参数模板与辅助 POI 选择见 [ARVR 算法](../templates/arvr-template.md) |
| 中立算法平台的输入区域 | `AlgorithmInvocation.Roi` 与 `AlgorithmRoi` 的矩形、圆、多边形、折线类型 | [算法平台](../../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) |

`TemplateRoi` 的三个参数是 `Threshold`、`Times`、`SmoothSize`；它不是通用矩形 ROI 配置。两个 JSON 模板通过 `EditTemplateJson` 编辑，分别提供 `ImageROIParam` 和 `SfrRoiParam` 的字段提示；提示文本不等于完整 Schema 校验。

SFR 寻边界面带关注点模板选择器，但手动请求只在选择有效时加入 `POITemplateParam`，宿主没有将它作为统一的发送前必填检查。服务要求与 Flow 公共 POI 字段应按 ARVR 主题分别核对。

## 定位实现与验证

模板与手动适配器分别位于 `Engine/ColorVision.Engine/Templates/FindLightArea/`、`Templates/Jsons/ImageROI/`、`Templates/Jsons/SFRFindROI/`；传统裁剪位于 `Templates/ImageCropping/`。中立 ROI 类型定义在 `UI/ColorVision.Algorithms/AlgorithmInvocation.cs`，本地 V2 由发光区主题指向节点与 native 实现。

先确定执行端和功能，再使用对应主题的参数、测试与结果样例。一次 ROI 测试不能证明远端定位、裁剪、SFR 寻边及本地算法都正确。
