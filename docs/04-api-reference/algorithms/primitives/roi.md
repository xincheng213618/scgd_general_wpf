# ROI

本页只描述当前仓库里真实存在的 ROI 相关原语，不再维护“统一 ROI 模块设计图”式旧稿。

## 先看当前仓库里 ROI 实际分成哪几支

按当前源码状态，ROI 并不是一个单独目录下的统一库，而是至少有三条相关分支：

1. 经典发光区定位模板，位于 `Templates/FindLightArea`
2. 图像裁剪 JSON 模板，位于 `Templates/Jsons/ImageROI`
3. ARVR 的 `SFR_FindROI` JSON 模板，位于 `Templates/Jsons/SFRFindROI`

所以这页更像“ROI 入口地图”，而不是“全局 ROI 抽象类说明”。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/ROIParam.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
- `Engine/ColorVision.Engine/Templates/FindLightArea/DisplayRoi.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/AlgorithmImageROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`

## 经典 ROI 链当前是什么样

### 模板入口

当前经典 ROI 实际落在 `FindLightArea` 这组代码里，而不是旧文档写的 `Templates/ROI`。

`TemplateRoi` 的实现特征很明确：

- `Name = FindLightArea`
- `Code = FindLightArea`
- `TemplateDicId = 31`
- 通过 `GetMysqlCommand()` 返回 `MysqlRoi`

因此这条链当前本质上是“发光区定位模板”，不是全系统统一 ROI 定义。

### 参数模型

`RoiParam` 当前非常直接，只暴露三项参数：

- `Threshold`
- `Times`
- `SmoothSize`

这和旧稿里那种通用矩形 ROI 或多边形 ROI API 不是一回事。它更像具体算法的阈值模板，而不是一个抽象几何对象。

### 运行与 UI

`AlgorithmRoi` 负责：

- 打开 `TemplateRoi` 的编辑窗口
- 获取 `DisplayRoi`
- 组装 `Event_LightArea2_GetData` 请求

`DisplayRoi` 则承担当前真实的用户输入流程：

- 选择模板
- 选择图像源服务
- 支持批次号、原始文件和本地图像三种输入
- 拉取 Raw 文件列表并支持直接打开

这说明当前经典 ROI 更接近“发光区检测算法的前端宿主”，而不是单独的绘图部件。

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

### ROI 不是统一基础库

当前仓库里的 ROI 相关实现分散在经典参数模板和 JSON 模板两条路径中，没有一个统一的 `ROI` 根模块负责所有场景。

### 经典 ROI 当前主要指发光区定位

如果不把 `FindLightArea` 当作主锚点，这页很容易写成一份不存在的“通用 ROI SDK”。

### JSON ROI 和经典 ROI 不是同一套配置模型

`TemplateImageROI`、`TemplateSFRFindROI` 都是 JSON 模板宿主，而 `TemplateRoi` 是传统参数模板。三者不能混成一张参数表。

### 某些 ROI 链已经和 POI 绑定

`AlgorithmSFRFindROI` 明确要求 `TemplatePoi`。在当前 ARVR 链里，ROI 和 POI 已经不是彻底分开的两个概念层。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/FindLightArea/TemplateRoi.cs`
2. `Engine/ColorVision.Engine/Templates/FindLightArea/AlgorithmRoi.cs`
3. `Engine/ColorVision.Engine/Templates/FindLightArea/DisplayRoi.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/ImageROI/TemplateImageROI.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 继续阅读

- [POI 原语](./poi.md)
- [POI 模板](../templates/poi-template.md)
- [ARVR 模板](../templates/arvr-template.md)