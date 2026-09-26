---
knowledge_id: "algorithms.poi-template"
knowledge_type: "reference"
status: "current"
summary: "说明 POI 主从表、伴生模板、复制导入、运行事件与结果类型映射。"
aliases: ["POI复制会覆盖旧点位吗","TemplatePoi","PoiParam","FlowPackagePoiCodec","ViewHandleRealPOI","POI图标平铺","PoiTemplateManagerWindow"]
code_paths: ["Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs","Engine/ColorVision.Engine/Templates/POI/PoiTemplateManagerWindow.cs","Engine/ColorVision.Engine/Templates/Browser","Engine/ColorVision.Engine/Media/PoiImageViewComponent.cs","Engine/ColorVision.Engine/Templates/POI/PoiParam.cs","Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs","Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PoiTemplateBrowserTests.cs","Test/ColorVision.UI.Tests/LocalPoiTemplateStorageTests.cs","Test/ColorVision.UI.Tests/FlowPackagePoiCodecTests.cs","Test/ColorVision.UI.Tests/PoiPointModelTests.cs"]
related: ["algorithms.index","algorithms.poi-routes","flow.templates","engine.results"]
---

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
| Flow 节点模板下拉不对 | 常规字段检查 PropertyEditor 注册；BuildPOI 多模板选择再查 `POINodeConfigurators.cs` |
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

主菜单、图像工具栏、手动算法模板选择、流程节点属性面板和 Conoscope 对焦点管理通过 `PoiTemplateManagerWindow` 浏览主 POI 点集。窗口复用流程的 `TemplateBrowserWindow` 搜索、操作栏和浏览交互；默认紧凑图标平铺，统一点阵图标下方显示居中的单行名称，长名称悬停查看。图标不是点位预览，浏览时不为它额外读取或渲染点明细。列表切换保留筛选、勾选、选中对象及各自滚动位置；双击仍交给原 `EditPoiParam`。过滤、修正、输出等伴生模板维持旧编辑宿主，主 POI 也可从“更多 → 旧版管理”回退。

拖拽交换两个模板的数据库顺序，新旧管理窗口采用同一事务实现。MySQL 交换 POI 主记录 ID 并同步全部点明细 Pid，名称、尺寸、配置与点位内容不变；本地点集交换 SQLite sort_order，保持 ID。提交后共享下拉框集合同步并保留选中对象，保存失败提示重新打开核对。重新打开使用数据库顺序，不再读取旧 flow_template_browser_order 本机偏好。新建、复制、导入、导出仍使用 `TemplatePoi` 原操作；重命名经 `PoiTemplateStorage.SaveMetadata` 保留未加载的点位；删除处理实际勾选项，包括被筛选隐藏的条目。重新加载跳过位置不变的集合移动，以免清空绑定的 ComboBox 选择。

`TemplatePoi` 双击打开 `EditPoiParam`，不是普通右侧 PropertyGrid。`PoiParam` 里保存画布尺寸、四角、配置 JSON 和 `ObservableCollection<PoiPoint>`。POI 主模板走专用表：

| 表 | 作用 |
| --- | --- |
| `t_scgd_algorithm_poi_template_master` | 模板主体、尺寸、四角、配置 JSON |
| `t_scgd_algorithm_poi_template_detail` | 每个点的类型、像素位置、宽高和备注 |

保存时会写主记录、删除旧点明细，再批量重写点明细。导入或复制模板时必须把主模板和点明细的 `Id` 都重置，否则容易覆盖旧模板。

`EditPoiParam` 和键盘模板编辑器 `EditPoiParam1` 使用动态主题底色、分隔条和无外框点位列表；图像画布独立于窗口主题。界面验收检查浅色、深色及运行时主题切换。

## 运行事件

`AlgorithmPoi` 发布 `Event_POI_GetData`，用于按已有点集取值或输出，常带 `TemplateParam`、`FilterTemplate`、`ReviseTemplate`、`OutputTemplate`、`POIStorageType`、`POIPointFileName`、`IsSubPixel`、`IsCCTWave`。`AlgorithmBuildPoi` 发布 `Event_Build_POI`，用于生成点集；CAD Mapping 分支还要带四点多边形和 `CADMappingParam`。

## Flow 消费

Flow 中的常规 POI 模板字段由节点上的 PropertyEditor 类型和 `FlowPropertyEditorRegistry` 绑定；`POINodeConfigurators.cs` 只补充 `BuildPOINode.TemplateName` 的多模板选择：

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
| Flow 节点选择 | `Engine/ColorVision.Engine/PropertyEditor/FlowTemplatePropertiesEditors.cs`、`Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/POINodeConfigurators.cs` |

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/PoiTemplateBrowserTests.cs` 验证图标浏览不读明细、视图切换、编辑索引、排序隔离和重命名/删除；`Test/ColorVision.UI.Tests/LocalPoiTemplateStorageTests.cs` 验证本地点位保存。另有 `Test/ColorVision.UI.Tests/FlowPackagePoiCodecTests.cs`、`Test/ColorVision.UI.Tests/PoiPointModelTests.cs`。

包编解码与点模型测试不替代现场数据库保存、MQTT POI 服务和各结果 handler 回放；修改 ID 或引用名时补充旧模板集成验证。
