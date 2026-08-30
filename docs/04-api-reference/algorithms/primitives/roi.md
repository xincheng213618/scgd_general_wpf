---
knowledge_id: "algorithms.roi-routes"
knowledge_type: "index"
status: "current"
summary: "区分发光区定位、JSON 裁剪、SFR 找 ROI 与统一算法 ROI 数据模型。"
aliases: ["ROI到底对应哪个模块","TemplateRoi","TemplateImageROI","TemplateSFRFindROI","AlgorithmRoi"]
code_paths: ["Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs","Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs","Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs","UI/ColorVision.Algorithms/AlgorithmImages.cs"]
test_paths: []
related: ["algorithms.index","algorithms.find-light-area","algorithms.image-cropping","algorithms.json-templates","algorithms.platform"]
---

# ROI

本页路由 Engine 的 ROI 模板适配分支；中立区域与结果模型另见 [算法平台](../../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)。同名 ROI 不代表配置或坐标语义相同。

## 先看当前仓库里 ROI 实际分成哪几支

Engine 模板中的 ROI 至少有三条相关分支：

1. 经典发光区定位模板，位于 `Templates/FindLightArea`
2. 图像裁剪 JSON 模板，位于 `Templates/Jsons/ImageROI`
3. ARVR 的 `SFR_FindROI` JSON 模板，位于 `Templates/Jsons/SFRFindROI`

本地 Robust V2 发光区还有独立算法及适配路径，见 [FindLightArea](../templates/find-light-area.md)。上面三支不是仓库 ROI 能力的穷尽清单。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/ROIParam.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/AlgorithmImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`

## 经典 ROI 链当前是什么样

当前经典 ROI 实际落在 `FindLightArea`，不是旧文档写的 `Templates/ROI`。`TemplateRoi` 的关键特征是 `Name = FindLightArea`、`Code = FindLightArea`、`TemplateDicId = 31`，并通过 `GetMysqlCommand()` 返回 `MysqlRoi`。

`RoiParam` 只暴露 `Threshold`、`Times`、`SmoothSize` 三项参数。`AlgorithmRoi` 使用 `SingleTemplateDisplayAlgorithmConfig` 和通用 `DisplayAlgorithmBase` 取得模板与图像输入，再组装 `Event_LightArea2_GetData` 请求。

## 两条 JSON ROI 分支

### ImageROI

`TemplateImageROI` 是 JSON 模板分支，当前：

- `Code = Image.ROI`
- `TemplateDicId = 52`
- `IsUserControl = true`

它通过 `EditTemplateJson` 承载结构化裁剪参数，而 `AlgorithmImageROI` 则发布 `Image.ROI` 事件。

这条链讲的是图像裁剪配置，不是经典发光区模板的复刻。

### SFR_FindROI

`TemplateSFRFindROI` 也是 JSON 模板分支，当前：

- `Code = ARVR.SFR.FindROI`
- `TemplateDicId = 36`
- `IsUserControl = true`

它在说明文本里明确给出了 `SfrRoiParam` 结构提示；`AlgorithmSFRFindROI` 则除了 JSON 模板本身，还会额外附带 `POITemplateParam`，再发布 `ARVR.SFR.FindROI`。

这说明 ARVR 里的“找 ROI”已经不是单纯 ROI 模板，而是 ROI 与 POI 联动的一条算法链。

## 当前几个最容易写错的点

### 模板分支不等于全局 ROI 契约

本页的 Engine 适配实现分散在经典参数与 JSON 模板中；中立算法平台已存在，但不会自动让旧模板的参数、坐标和持久化格式变得互通。

### 经典 ROI 当前主要指发光区定位

如果不把 `FindLightArea` 当作主锚点，这页很容易写成一份不存在的“通用 ROI SDK”。

### JSON ROI 和经典 ROI 不是同一套配置模型

`TemplateImageROI`、`TemplateSFRFindROI` 都是 JSON 模板宿主，而 `TemplateRoi` 是传统参数模板。三者不能混成一张参数表。

### 某些 ROI 链已经和 POI 绑定

`AlgorithmSFRFindROI` 明确要求 `TemplatePoi`。在当前 ARVR 链里，ROI 和 POI 已经不是彻底分开的两个概念层。

## 按问题找源码

- 经典发光区参数或事件：`Templates/FindLightArea/TemplateRoi.cs`、`AlgorithmRoi.cs`。
- 手动模板选择和编辑：`Services/Devices/Algorithm/DisplayAlgorithmConfiguration.cs`。
- JSON 裁剪：`Templates/Jsons/ImageROI/TemplateImageROI.cs`。
- ARVR 找 ROI 与 POI 联动：`Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`、`AlgorithmSFRFindROI.cs`。

以上简写相对 `Engine/ColorVision.Engine/`；只进入与问题对应的一支。

## 验证入口与缺口

不同 ROI 分支不是同一参数契约；先确认执行端，再使用发光区或对应算法平台测试，不能用一次 ROI 测试替代所有分支。
