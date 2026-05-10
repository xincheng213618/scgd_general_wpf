# POI 模板

本页只描述当前仓库里真实存在的 POI 模板族，不再维护“检测器接口大全 + 可插拔算法样例”式旧稿。

## 这个模板族当前在做什么

按当前源码状态，POI 不是一个孤立模板，而是一组围绕“点集数据”展开的模板和算法宿主：

- 主 POI 模板负责保存点集、尺寸和配置。
- 过滤、修正、标定、输出分别有自己的伴生模板。
- 运行时算法负责把这些模板拼成 MQTT 请求。
- Flow 节点和若干 JSON 算法会继续消费 POI 模板。

因此这页真正要讲的不是“某一种 POI 检测算法”，而是当前系统里 POI 模板怎样被创建、编辑、保存和复用。

## 当前最关键的文件

- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 当前主链怎么跑

### 主模板与数据模型

`TemplatePoi` 是主入口。它当前有几个很重要的实现特征：

- 继承 `ITemplate<PoiParam>`
- `IsSideHide = true`
- 模板代码固定为 `POI`
- 双击列表项时直接打开 `EditPoiParam`

和很多普通模板不同，POI 主模板不是单纯依赖右侧 `PropertyGrid`，而是有自己的编辑窗口。

`PoiParam` 则不是一个只存几个数值的简单参数类。它当前承载：

- 模板尺寸 `Width`、`Height`
- 四角坐标 `LeftTopX/Y`、`RightTopX/Y`、`RightBottomX/Y`、`LeftBottomX/Y`
- `CfgJson` 与 `PoiConfig` 的双向转换
- `ObservableCollection<PoiPoint> PoiPoints`

`PoiPoint` 本身保存的是当前系统真实在用的点信息：

- `Id`
- `Name`
- `PointType`
- `PixX`、`PixY`
- `PixWidth`、`PixHeight`

所以 POI 模板当前更接近“点集模板 + 配置模板”的组合。

### 当前持久化方式

POI 主模板不是普通 `ModMasterModel`/`ModDetailModel` 那套默认路径。当前它走的是专用表：

- `PoiMasterDao`
- `PoiDetailDao`

`PoiParam.LoadPoiDetailFromDB(...)` 会把点明细装回 `PoiPoints`；扩展方法 `Save2DB(...)` 则会：

- 保存主记录
- 删除旧点明细
- 用 BulkCopy 重写整组 `PoiDetailModel`

这也是 POI 页面最容易被写偏的地方之一：它不是“通用模板表里一组普通 detail 项”，而是自己带点表。

### 导入、复制与新建

`TemplatePoi` 当前支持：

- 从当前模板复制为 JSON 临时副本
- 从 `.cfg` 导入点集模板
- 导出前主动加载点明细
- 创建时把导入副本或空模板写回数据库

而且复制或导入后会把模板 `Id` 和每个点的 `Id` 重置为 `-1`，避免直接复用旧主键。

### 运行时算法链

`AlgorithmPoi` 是当前最主要的 POI 运行入口。它负责：

- 打开 POI 主模板编辑窗口
- 打开过滤、修正、输出模板编辑窗口
- 在文件模式下选择外部点文件
- 组装 `Event_POI_GetData` 的 MQTT 参数

当前发送的参数不只包含主模板，还可能包含：

- `FilterTemplate`
- `ReviseTemplate`
- `OutputTemplate`
- `POIStorageType`
- `POIPointFileName`
- `IsSubPixel`
- `IsCCTWave`

这说明 POI 运行链当前已经是“多模板组合请求”，不是单模板独跑。

### 布点与伴生模板

`AlgorithmBuildPoi` 是另一条关键链。它当前负责：

- 打开布点模板 `TemplateBuildPoi`
- 可选加载 CAD 文件
- 在 `POIBuildType == CADMapping` 时附带四点多边形和 `CADMappingParam`
- 发布 `Event_Build_POI`

除此之外，POI 族当前还包含多个伴生模板：

- `TemplatePoiFilterParam`：过滤模板，`Code = POIFilter`，使用自定义编辑控件
- `TemplatePoiReviseParam`：修正模板，`Code = PoiRevise`
- `TemplatePoiGenCalParam`：修正标定模板，`Code = POIGenCali`，使用自定义编辑控件
- `TemplatePoiOutputParam`：输出模板，`Code = PoiOutput`，使用自定义编辑控件

这些模板不是注释里的“可选扩展”，而是当前 Flow 和算法链里实际会被引用的对象。

### Flow 与其它算法怎样消费 POI

POI 现在已经是共享原语，而不是单算法私有模板。当前至少有三条明确的消费路径：

1. `POINodeConfigurators` 会把 `TemplatePoi`、过滤、修正、输出、标定等模板挂到 POI 节点属性面板。
2. `AlgorithmPoiAnalysis` 会在 JSON 分析模板之外继续附带 `POITemplateParam`。
3. `AlgorithmSFRFindROI`、`AlgorithmOLEDAOI` 这类算法也会额外引用 `TemplatePoi`。

## 当前几个最容易写错的点

### POI 不是一种单独算法

当前仓库里的 POI 更像共享点集模板体系，既能生成点、过滤点，也会被其它算法消费。

### 主存储不是普通 detail 表

主模板依赖 `PoiMasterDao` 和 `PoiDetailDao`，如果继续按通用模板表去解释，会漏掉点明细这一层。

### 主编辑器不是纯 `PropertyGrid`

`TemplatePoi` 双击后会进入 `EditPoiParam`；过滤和输出模板也带自己的 `UserControl` 编辑器。继续把它们写成统一右侧属性面板，会和真实界面不符。

### 文件模式和数据库模式并存

`AlgorithmPoi` 明确支持 `POIStorageModel.Db` 与 `POIStorageModel.File` 两条路径。文档不能再把 POI 写成“只存在数据库里”。

## 推荐阅读顺序

1. `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 继续阅读

- [POI 原语](../primitives/poi.md)
- [JSON 模板](./json-templates.md)
- [流程引擎](./flow-engine.md)