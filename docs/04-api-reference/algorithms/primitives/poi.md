# POI

本页只描述当前仓库里作为共享原语存在的 POI，不再维护“POI 检测算法百科”式旧稿。

## 先看 POI 现在在系统里扮演什么角色

按当前源码状态，POI 更像一套可复用的数据与模板体系，而不是单个算法结果：

- 主模板保存点集和配置。
- 布点、过滤、修正、标定、输出围绕这份点集工作。
- JSON 算法和 ARVR 算法会继续引用 POI 模板。
- Flow 节点也把 POI 当成共享输入输出对象。

因此本页重点不是“如何找特征点”，而是 POI 这个原语当前怎样被存储、传递和消费。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/AlgorithmPoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/OLEDAOI/AlgorithmOLEDAOI.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 当前数据长什么样

### 点对象

`PoiPoint` 现在保存的是一套很直接的显示与定位字段：

- `Id`
- `Name`
- `PointType`
- `PixX`、`PixY`
- `PixWidth`、`PixHeight`

它不是一个抽象“兴趣点接口”，而是已经贴近当前图像编辑和结果展示所需字段的具体对象。

### 模板对象

`PoiParam` 负责把点集、尺寸、角点和配置打包成一个模板。它当前至少包含：

- 模板尺寸 `Width`、`Height`
- 模板类型 `Type`
- 四角坐标
- `PoiPoints`
- `CfgJson` 与 `PoiConfig`

而且 `CfgJson` 不是单纯字符串缓存，当前会和 `PoiConfig` 相互序列化、反序列化。

## 当前怎么存储

POI 的一个核心现实是：它当前有自己专门的主从数据结构。

- 主记录通过 `PoiMasterDao` 保存
- 点明细通过 `PoiDetailDao` 保存

`PoiParam.LoadPoiDetailFromDB(...)` 会按 `Pid` 回填点集合；扩展方法 `Save2DB(...)` 则会清空旧明细后整批写入新点。

这使得 POI 与一般只依赖 `ModMasterModel`/`ModDetailModel` 的模板明显不同。

## 当前运行链怎么消费 POI

### 主 POI 算法

`AlgorithmPoi` 是最直接的 POI 消费者和生产者。它当前支持：

- 主模板 `TemplatePoi`
- 过滤模板 `TemplatePoiFilterParam`
- 修正模板 `TemplatePoiReviseParam`
- 输出模板 `TemplatePoiOutputParam`
- 文件模式 `POIStorageModel.File`

最终通过 `Event_POI_GetData` 发布带多模板参数的 MQTT 请求。

### 布点算法

`AlgorithmBuildPoi` 负责把其它信息转成 POI 点集。它当前支持：

- 普通布点
- CADMapping 布点
- 四点多边形 `LayoutPolygon`
- `CADMappingParam`
- `Event_Build_POI`

所以当前系统里“得到一份 POI”并不只靠检测，也可以靠构建。

### 下游算法引用

POI 现在已经被多条其它算法链消费：

- `AlgorithmPoiAnalysis` 会附带 `POITemplateParam`
- `AlgorithmSFRFindROI` 会附带 `POITemplateParam`
- `AlgorithmOLEDAOI` 也会附带 `POITemplateParam`

因此 POI 当前是其它算法的输入格式之一，不是结果页末端才出现的附属对象。

### Flow 节点引用

`POINodeConfigurators` 说明 POI 在 Flow 里已经成为共享节点资源：

- `POINode` 需要主模板、过滤、修正、输出模板
- `BuildPOINode` 会同时接布点模板、回写 POI 模板和布局 ROI 模板
- `POIReviseNode` 会接修正标定模板
- `POIAnalysisNode` 会接 JSON 分析模板

这也说明 POI 当前是流程设计期就要选定的核心原语。

## 当前几个最容易写错的点

### POI 不是单一检测算法的结果结构

当前它同时被用在检测、布点、分析、AOI 和 Flow 节点中，是一套共享数据模板。

### 存储不是只有数据库，也不是只有文件

主模板走数据库，但 `AlgorithmPoi` 也明确支持文件模式和外部点文件。

### 伴生模板是当前系统的一等成员

过滤、修正、标定、输出模板都有真实实现和编辑入口，不是注释里的“未来扩展”。

### 某些算法是消费 POI，而不是生产 POI

像 `PoiAnalysis`、`SFR_FindROI`、`OLEDAOI` 这些链，本质上是在读取和使用已有 POI 模板。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 继续阅读

- [POI 模板](../templates/poi-template.md)
- [JSON 模板](../templates/json-templates.md)
- [流程引擎](../templates/flow-engine.md)