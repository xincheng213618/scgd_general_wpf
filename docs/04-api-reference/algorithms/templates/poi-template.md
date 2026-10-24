---
knowledge_id: "algorithms.poi-template"
knowledge_type: "reference"
status: "current"
summary: "说明 POI 主从表、伴生模板、复制导入、运行事件与结果类型映射。"
aliases: ["POI复制会覆盖旧点位吗","TemplatePoi","PoiParam","FlowPackagePoiCodec","ViewHandleRealPOI","POI图标平铺","PoiTemplateManagerWindow","键盘亮度校正","POINode","IsCCTWave","色温/波长"]
code_paths: ["Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs","Engine/ColorVision.Engine/Templates/POI/PoiTemplateManagerWindow.cs","Engine/ColorVision.Engine/Templates/POI/EditPoiParam1.xaml.cs","Engine/ColorVision.Engine/Templates/Browser","Engine/ColorVision.Engine/Media/PoiImageViewComponent.cs","Engine/ColorVision.Engine/Templates/POI/PoiParam.cs","Engine/ColorVision.Engine/Templates/POI/BuildPoi/TemplateBuildPoi.cs","Engine/ColorVision.Engine/Templates/Jsons/BuildPOIAA/TemplateBuildPOIAA.cs","Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/POI/POINode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/POI/BuildPOINode.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LocalPoiTemplateStorageTests.cs","Test/ColorVision.UI.Tests/FlowPackagePoiCodecTests.cs","Test/ColorVision.UI.Tests/PoiPointModelTests.cs","Test/ColorVision.UI.Tests/KeyboardLuminanceCalibrationTests.cs","Test/ColorVision.UI.Tests/CompatibilityNodeMigrationTests.cs","Test/ColorVision.UI.Tests/CompatibilityCameraPropertyEditorTests.cs"]
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

主菜单、图像工具栏、手动算法模板选择、流程节点属性面板和 Conoscope 对焦点管理通过 `PoiTemplateManagerWindow` 浏览主 POI 点集。窗口复用流程的 `TemplateBrowserWindow` 搜索、操作栏和浏览交互；固定使用紧凑图标平铺，按数据库保存顺序倒序显示，新建项排在前面。统一点阵图标下方显示居中的单行名称，长名称悬停查看。图标不是点位预览，浏览时不为它额外读取或渲染点明细。双击交给原 `EditPoiParam`；过滤、修正、输出等伴生模板维持各自的编辑宿主。

拖拽交换两个模板的数据库顺序。MySQL 交换 POI 主记录 ID 并同步全部点明细 Pid，名称、尺寸、配置与点位内容不变；本地点集交换 SQLite sort_order，保持 ID。提交后共享下拉框集合同步并保留选中对象，保存失败提示重新打开核对。管理窗口及重新打开时都按数据库顺序倒序展示，不反转或筛选共享下拉框集合，不读取旧 flow_template_browser_order 本机偏好。新建、复制、导入、导出仍使用 `TemplatePoi` 原操作；重命名经 `PoiTemplateStorage.SaveMetadata` 保留未加载的点位；删除处理实际勾选项，包括被筛选隐藏的条目。重新加载跳过位置不变的集合移动，以免清空绑定的 ComboBox 选择。

`TemplatePoi` 双击打开 `EditPoiParam`，不是普通右侧 PropertyGrid。`PoiParam` 里保存画布尺寸、四角、配置 JSON 和 `ObservableCollection<PoiPoint>`。POI 主模板走专用表：

| 表 | 作用 |
| --- | --- |
| `t_scgd_algorithm_poi_template_master` | 模板主体、尺寸、四角、配置 JSON |
| `t_scgd_algorithm_poi_template_detail` | 每个点的类型、像素位置、宽高和备注 |

保存时会写主记录、删除旧点明细，再批量重写点明细。导入或复制模板时必须把主模板和点明细的 `Id` 都重置，否则容易覆盖旧模板。

`EditPoiParam` 和键盘模板编辑器 `EditPoiParam1` 使用动态主题底色、分隔条和无外框点位列表；图像画布独立于窗口主题。界面验收检查浅色、深色及运行时主题切换。

`EditPoiParam1` 的键盘快速亮度校正使用 `opencv_helper.dll`，通过 Unicode 文件接口加载所选模板的单通道 Luminance 资源。有效按键的归一化灰阶均值仍先四舍五入并限制到 16 位范围，再按当前图像的 `Exp` / `exp` 曝光执行校正；缺少曝光元数据时使用有效模板曝光，否则取 1。结果映射回原按键顺序，无效按键保留缺失值。未配置资源、文件加载失败或执行失败时返回未校正灰阶；四色模板仍不支持这条单通道快速路径。`KeyboardLuminanceCalibrationTests` 验证灰阶量化、曝光缩放、按键顺序与失败边界，不替代真机键盘验收。

## Flow 消费

“关注点算法”（`POINode`）的“色温/波长”固定显示 `True` 只读文本，执行请求始终带 `IsCCTWave=true`。旧流程保存的 `false` 不会关闭计算，重新保存时写为 `True`；亚像素等其它选项仍按原配置执行。

Flow 中的常规 POI 模板字段由节点上的 PropertyEditor 类型和 `FlowPropertyEditorRegistry` 绑定；`POINodeConfigurators.cs` 只补充 `BuildPOINode.TemplateName` 的多模板选择：

| 分支 | 会选择什么 |
| --- | --- |
| POI 修正标定 | `TemplatePoiGenCalParam` |
| POI 过滤/修正/输出 | `TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` |
| POI 运行 | `TemplatePoi` + 过滤/修正/输出 |
| BuildPOI | `TemplateBuildPoi` 或 `TemplateBuildPOIAA`，以及 `RePOI`、`LayoutROI`、`SavePOI` |
| PoiAnalysis | `TemplatePoiAnalysis` |

POI 也会被 `POIAnalysisNode`、SFR ROI、OLED AOI、项目包等继续消费。它是共享原语，不是某个模板私有能力。

本地“光谱参考校正”（`LocalSpectrumCalibrationNode`）用于将本次光谱测量应用到整幅图像：`相机取图 → POI → IN_POI`，同一流程的光谱仪亮度结果接 `IN_SPE`，节点输出接 End。使用本地“相机取图”的按需色度路径并选择颜色校正模板；本地 POI 保留对应 RAW 帧，因此无需生成或保存 CVCIE。单个 POI 可不填点名，多个点必须明确选择与光谱仪视场对应的唯一参考区域；不取多个点的隐式平均。光谱输入可以来自本地或服务采集，但必须已入库且属于本批次，不接受光通量/EQE 数据。

该节点每次执行均按选择的亮度、色度或亮色度模式生成倍率，不使用旧“关注点修正标定”的触发阈值、修正输出模板或原地修改 POI 明细。倍率合入当前 RAW 的色度矩阵：三乘三矩阵按 XYZ 输出行缩放，曝光和增益已包含在原矩阵中，不再次归一化。只替换 CVRAW 尾部参数，保留 RAW 像素、原始色度系数、参考点与光谱结果标识；重复执行从原系数重新计算，避免累计倍率。仅缓存的相机图像在此节点同步落盘，已有文件只更新尾部。文件写入成功后才追加同批次图像主记录，写库失败尝试恢复原矩阵；成功后下游本地 POI 和 Conoscope 从新矩阵计算 XYZ，原 POI 记录继续表示修正前测量。相机校正模板不被修改，也不另存 CVCIE。

`SpectrumRawCalibrationTests` 覆盖矩阵布局、三种修正模式、重复写回、RAW 像素保持和写库失败回滚；Conoscope 的 `FlowCaptureSelectionTests` 与 `CalibratedRawDocumentTests` 覆盖最新成功记录选择及同一路径换矩阵后重新加载。实际精度仍需现场验证同一光源、光学路径及参考视场；单点倍率不能证明所有角度或不同光谱分布均获得同样精度。

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
| 运行取值与生成点集 | `Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/POI/POINode.cs`、`Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/POI/BuildPOINode.cs` |
| 过滤/修正/输出/标定 | `POIFilters/`、`POIRevise/`、`POIOutput/`、`POIGenCali/` |
| Flow 节点选择 | `Engine/ColorVision.Engine/PropertyEditor/FlowTemplatePropertiesEditors.cs`、`Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/POINodeConfigurators.cs` |

## 验证入口与缺口

`Test/ColorVision.UI.Tests/LocalPoiTemplateStorageTests.cs` 验证本地点位保存。另有 `Test/ColorVision.UI.Tests/FlowPackagePoiCodecTests.cs`、`Test/ColorVision.UI.Tests/PoiPointModelTests.cs`。

包编解码与点模型测试不替代现场数据库保存、MQTT POI 服务和各结果 handler 回放；修改 ID 或引用名时补充旧模板集成验证。
