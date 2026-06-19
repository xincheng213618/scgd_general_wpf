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
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePOICalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 当前模板矩阵

POI 族里只有主 POI 模板走专用点表，其它伴生模板仍然是字典模板。交接时不要把它们混成同一种持久化方式。

| 模板 | 字典/代码 | 编辑入口 | 主要用途 |
| --- | --- | --- | --- |
| `TemplatePoi` | `TemplateDicId = -1`，`Code = POI` | `EditPoiParam` 独立窗口 | 保存点集、尺寸、四角、配置 JSON 和点明细。 |
| `TemplateBuildPoi` | `TemplateDicId = 16`，`Code = BuildPOI` | 模板编辑器/布点界面 | 按规则或 CAD 映射生成 POI。 |
| `TemplatePoiFilterParam` | `TemplateDicId = 23`，`Code = POIFilter` | 自定义过滤编辑控件 | 运行 POI 时可选过滤模板。 |
| `TemplatePoiReviseParam` | `TemplateDicId = 24`，`Code = PoiRevise` | 模板编辑器 | 运行 POI 时可选修正模板。 |
| `TemplatePoiGenCalParam` | `TemplateDicId = 25`，`Code = POIGenCali` | 自定义标定修正编辑控件 | Flow 中 POI 修正标定节点使用。 |
| `TemplatePoiOutputParam` | `TemplateDicId = 27`，`Code = PoiOutput` | 自定义输出编辑控件 | 运行 POI 时可选文件输出模板。 |
| `TemplateBuildPOIAA` | `TemplateDicId = 41`，`Code = BuildPOI` | JSON 模板编辑器 | AA 找点结果构建 POI 的 JSON V2 分支。 |

主模板与伴生模板的差异是 POI 文档的核心：主模板保存真实点位，伴生模板描述点位如何生成、过滤、修正和输出。

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

- `PoiMasterDao` -> `t_scgd_algorithm_poi_template_master`
- `PoiDetailDao` -> `t_scgd_algorithm_poi_template_detail`

`PoiParam.LoadPoiDetailFromDB(...)` 会把点明细装回 `PoiPoints`；扩展方法 `Save2DB(...)` 则会：

- 保存主记录
- 删除旧点明细
- 用 BulkCopy 重写整组 `PoiDetailModel`

这也是 POI 页面最容易被写偏的地方之一：它不是“通用模板表里一组普通 detail 项”，而是自己带点表。

| 表 | 关键字段 | 交接含义 |
| --- | --- | --- |
| `t_scgd_algorithm_poi_template_master` | `name`、`type`、`width`、`height`、四角坐标、`cfg_json`、`tenant_id`、`is_delete` | POI 模板主体、画布尺寸和配置 JSON。 |
| `t_scgd_algorithm_poi_template_detail` | `pid`、`pt_type`、`pix_x`、`pix_y`、`pix_width`、`pix_height`、`remark` | 每个 POI 点或区域的像素位置和尺寸。 |

删除模板时当前代码直接删除 master 记录并从列表移除，复制/导入时会把模板和点的 `Id` 重置为 `-1`。如果现场出现“复制模板后覆盖旧点位”，优先检查导入副本的 Id 是否被正确重置。

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

| 参数 | 来源 | 说明 |
| --- | --- | --- |
| `TemplateParam` | `TemplatePoi` | 必选主 POI 模板。 |
| `FilterTemplate` | `TemplatePoiFilterParam` | 可选，`Id != -1` 时发送。 |
| `ReviseTemplate` | `TemplatePoiReviseParam` | 可选，`Id != -1` 时发送。 |
| `OutputTemplate` | `TemplatePoiOutputParam` | 可选，`Id != -1` 时发送。 |
| `POIStorageType` | `POIStorageModel` | 文件模式时发送，区分 DB 点集和外部点文件。 |
| `POIPointFileName` | 文件选择器 | 文件模式时发送外部点文件路径。 |
| `IsSubPixel`、`IsCCTWave` | 算法界面配置 | 控制子像素/CCT 波形相关运行选项。 |

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

| Flow 配置器分支 | 设备/输入 | 模板选择器 | 交接重点 |
| --- | --- | --- | --- |
| POI 修正标定 | `DeviceAlgorithm` | `TemplatePoiGenCalParam` | 只处理标定修正模板，不直接选择主 POI。 |
| POI 过滤/修正/输出 | `DeviceAlgorithm` | `TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` | 用于已有 POI 结果的后处理组合。 |
| POI 运行 | `DeviceAlgorithm` + 图像路径 | `TemplatePoi`、过滤、修正、输出 | 对应 `Event_POI_GetData` 的完整运行链。 |
| BuildPOI | `DeviceAlgorithm` + 图像路径 | `TemplateBuildPoi` 或 `TemplateBuildPOIAA`，以及 `RePOI`、`LayoutROI`、`SavePOI` | 同时支持传统布点和 JSON AA 布点分支。 |
| PoiAnalysis | `DeviceAlgorithm` | `TemplatePoiAnalysis` | JSON 分析模板仍会消费 POI 相关结果。 |

## 结果落库与展示

POI 的结果不是单一种类，handler 会按 `ViewResultAlgType` 分流：

| 结果类型 | 展示/导出入口 | 结果表/文件线索 |
| --- | --- | --- |
| `POI`、`POI_Y` | `ViewHanlePOIY` | 可导出 CSV，结果点值来自 POI 明细结果。 |
| `POI_XYZ` | `ViewHanlePOIXZY` | 可导出 CSV，并和 XYZ 结果展示关联。 |
| `POI_XYZ_File`、`POI_Y_File`、`POI_CIE_File` | `ViewHanlePOIXZY` | 文件型结果，常落到 `t_scgd_algorithm_result_detail_poi_cie_file`。 |
| `RealPOI`、`POI_XYZ_V2`、`POI_Y_V2`、`KB_Output_Lv`、`KB_Output_CIE` | `ViewHandleRealPOI` | V2/项目输出链，排查时要看实际 `ResultType`。 |
| `BuildPOI`、`BuildPOI_File` | `ViewHandleBuildPoi`、`ViewHandleBuildPoiFile` | 布点结果或文件结果，可能生成新的 POI 数据。 |

点值明细表当前包括 `t_scgd_algorithm_result_detail_poi_mtf`，字段覆盖 `poi_id`、`poi_name`、`poi_type`、`poi_x/y`、`poi_width/height` 和 `value`。如果 UI 展示和导出不一致，先确认 result type 走到哪个 handler，再查对应明细或文件表。

## 当前几个最容易写错的点

### POI 不是一种单独算法

当前仓库里的 POI 更像共享点集模板体系，既能生成点、过滤点，也会被其它算法消费。

### 主存储不是普通 detail 表

主模板依赖 `PoiMasterDao` 和 `PoiDetailDao`，如果继续按通用模板表去解释，会漏掉点明细这一层。

### 主编辑器不是纯 `PropertyGrid`

`TemplatePoi` 双击后会进入 `EditPoiParam`；过滤和输出模板也带自己的 `UserControl` 编辑器。继续把它们写成统一右侧属性面板，会和真实界面不符。

### 文件模式和数据库模式并存

`AlgorithmPoi` 明确支持 `POIStorageModel.Db` 与 `POIStorageModel.File` 两条路径。文档不能再把 POI 写成“只存在数据库里”。

### BuildPOI 和 POI 运行不是同一个事件

`AlgorithmBuildPoi` 发布 `Event_Build_POI`，`AlgorithmPoi` 发布 `Event_POI_GetData`。前者偏生成点集，后者偏基于点集取值/输出。现场排查时不要把两个事件的模板参数混用。

## 验收建议

| 场景 | 必验项 |
| --- | --- |
| 新建/保存 POI | `t_scgd_algorithm_poi_template_master` 有主记录，`t_scgd_algorithm_poi_template_detail` 有对应点明细。 |
| 复制/导入 POI | 模板和点明细 `Id` 被重置，创建新模板后不会覆盖旧模板。 |
| 文件模式运行 | MQTT 参数包含 `POIStorageType` 和 `POIPointFileName`，DB 模式则不依赖外部点文件。 |
| 过滤/修正/输出 | 选中对应模板时请求里出现 `FilterTemplate`、`ReviseTemplate`、`OutputTemplate`。 |
| BuildPOI CADMapping | 请求包含 `LayoutPolygon` 和 `CADMappingParam`，四点 ROI 与 CAD 文件路径正确。 |
| 结果展示 | 根据 `ViewResultAlgType` 命中正确 handler，CSV 导出字段和结果表/文件一致。 |

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
