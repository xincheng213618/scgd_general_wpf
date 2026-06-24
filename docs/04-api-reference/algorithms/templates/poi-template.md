# POI 模板

POI 是“点集模板体系”，不是单个检测算法。维护时先分清：主 POI 点集、伴生模板、运行事件、BuildPOI 生成事件、结果 handler。

## 先查什么

| 问题 | 第一检查点 |
| --- | --- |
| 新建 POI 后点位丢失 | `t_scgd_algorithm_poi_template_master` 和 `t_scgd_algorithm_poi_template_detail` |
| 复制/导入后覆盖旧点位 | 模板和点明细 `Id` 是否重置为 `-1` |
| POI 运行没带过滤/修正/输出 | 请求里是否出现 `FilterTemplate`、`ReviseTemplate`、`OutputTemplate` |
| 文件模式找不到点 | `POIStorageType` 和 `POIPointFileName` |
| BuildPOI 和 POI 参数混用 | `Event_Build_POI` 与 `Event_POI_GetData` 是否分清 |
| Flow 节点模板下拉不对 | `POINodeConfigurators.cs` 对应分支 |
| UI 展示和导出不一致 | `ViewResultAlgType` 命中了哪个 result handler |

## 模板族

| 模板 | 字典/代码 | 用途 |
| --- | --- | --- |
| `TemplatePoi` | `TemplateDicId = -1`，`Code = POI` | 主点集模板，保存尺寸、四角、配置 JSON 和点明细 |
| `TemplateBuildPoi` | `TemplateDicId = 16`，`Code = BuildPOI` | 按规则或 CAD 映射生成 POI |
| `TemplatePoiFilterParam` | `TemplateDicId = 23`，`Code = POIFilter` | 运行 POI 时过滤点 |
| `TemplatePoiReviseParam` | `TemplateDicId = 24`，`Code = PoiRevise` | 运行 POI 时修正点 |
| `TemplatePoiGenCalParam` | `TemplateDicId = 25`，`Code = POIGenCali` | POI 修正标定 |
| `TemplatePoiOutputParam` | `TemplateDicId = 27`，`Code = PoiOutput` | POI 文件输出 |
| `TemplateBuildPOIAA` | `TemplateDicId = 41`，`Code = BuildPOI` | JSON V2 / AA 找点构建 POI |

主 POI 模板保存真实点位；其它模板描述如何生成、过滤、修正、标定或输出点位。不要把它们当成同一种持久化模型。

## 主模板存储

`TemplatePoi` 双击打开 `EditPoiParam`，不是普通右侧 PropertyGrid。`PoiParam` 里保存画布尺寸、四角、配置 JSON 和 `ObservableCollection<PoiPoint>`。POI 主模板走专用表：

| 表 | 作用 |
| --- | --- |
| `t_scgd_algorithm_poi_template_master` | 模板主体、尺寸、四角、配置 JSON |
| `t_scgd_algorithm_poi_template_detail` | 每个点的类型、像素位置、宽高和备注 |

保存时会写主记录、删除旧点明细，再批量重写点明细。导入或复制模板时必须把主模板和点明细的 `Id` 都重置，否则容易覆盖旧模板。

## 运行事件

`AlgorithmPoi` 发布 `Event_POI_GetData`，用于按已有点集取值或输出，常带 `TemplateParam`、`FilterTemplate`、`ReviseTemplate`、`OutputTemplate`、`POIStorageType`、`POIPointFileName`、`IsSubPixel`、`IsCCTWave`。`AlgorithmBuildPoi` 发布 `Event_Build_POI`，用于生成点集；CAD Mapping 分支还要带四点多边形和 `CADMappingParam`。

## Flow 消费

`POINodeConfigurators.cs` 是 Flow 里最重要的入口：

| 分支 | 会选择什么 |
| --- | --- |
| POI 修正标定 | `TemplatePoiGenCalParam` |
| POI 过滤/修正/输出 | `TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` |
| POI 运行 | `TemplatePoi` + 过滤/修正/输出 |
| BuildPOI | `TemplateBuildPoi` 或 `TemplateBuildPOIAA`，以及 `RePOI`、`LayoutROI`、`SavePOI` |
| PoiAnalysis | `TemplatePoiAnalysis` |

POI 也会被 `AlgorithmPoiAnalysis`、SFR ROI、OLED AOI、项目包等继续消费。它是共享原语，不是某个模板私有能力。

## 结果 handler

| 结果类型 | Handler |
| --- | --- |
| `POI`、`POI_Y` | `ViewHanlePOIY` |
| `POI_XYZ` | `ViewHanlePOIXZY` |
| `POI_XYZ_File`、`POI_Y_File`、`POI_CIE_File` | `ViewHanlePOIXZYFile` |
| `RealPOI`、`POI_XYZ_V2`、`POI_Y_V2`、`KB_Output_Lv`、`KB_Output_CIE` | `ViewHandleRealPOI` |
| `BuildPOI` | `ViewHandleBuildPoi` |
| `BuildPOI_File` | `ViewHandleBuildPoiFile` |

展示或 CSV 字段不一致时，先看实际 `ResultType`，再查对应 handler 和明细/文件表。

## 验收

| 场景 | 通过标准 |
| --- | --- |
| 新建/保存 POI | master 有主记录，detail 有对应点明细 |
| 复制/导入 POI | 新模板不覆盖旧模板，点明细重新生成 |
| DB 模式运行 | 请求不依赖外部点文件 |
| 文件模式运行 | 请求包含 `POIStorageType` 和 `POIPointFileName` |
| 过滤/修正/输出 | 选中伴生模板后请求带对应模板 |
| BuildPOI CADMapping | 请求带四点 ROI、CAD 文件和映射参数 |
| 结果展示 | `ViewResultAlgType` 命中正确 handler，导出字段和结果一致 |

## 源码入口

| 任务 | 先看 |
| --- | --- |
| 主点集和点明细 | `TemplatePoi.cs`、`PoiParam.cs`、`PoiPoint.cs` |
| 运行取值 | `AlgorithmImp/AlgorithmPOI.cs` |
| 生成点集 | `BuildPoi/AlgorithmBuildPoi.cs` |
| 过滤/修正/输出/标定 | `POIFilters/`、`POIRevise/`、`POIOutput/`、`POIGenCali/` |
| Flow 节点选择 | `Templates/Flow/NodeConfigurator/POINodeConfigurators.cs` |
