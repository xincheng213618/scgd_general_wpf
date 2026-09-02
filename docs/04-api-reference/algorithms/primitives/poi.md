---
knowledge_id: "algorithms.poi-routes"
knowledge_type: "topic"
status: "current"
summary: "说明 POI 点位、伴生模板、文件模式与 Flow 和 JSON 算法的消费关系。"
aliases: ["POI点集由谁生成和消费","POI 1x1中心点","POI 50%缩进","PoiPoint","PoiParam","AlgorithmPoi","AlgorithmBuildPoi"]
code_paths: ["Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs","Engine/ColorVision.Engine/Templates/POI/PoiParam.cs","Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs","Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs"]
test_paths: ["Test/ColorVision.UI.Tests/PoiPointModelTests.cs","Test/ColorVision.UI.Tests/PoiLayoutGeometryTests.cs"]
related: ["algorithms.index","algorithms.poi-template","flow.templates"]
---

# POI

POI 在当前系统里是一套共享点位原语，不是单个“检测算法”。它保存点集和模板配置，被布点、过滤、修正、分析、AOI、SFR 和 Flow 节点共同消费。

## 先查什么

| 现象 | 优先看 |
| --- | --- |
| 点位丢失 | `PoiMasterDao` / `PoiDetailDao` 是否有对应 `Pid`，`LoadPoiDetailFromDB` 是否回填 |
| 位置或 overlay 偏移 | `PixX`、`PixY`、`PixWidth`、`PixHeight` 和当前图像坐标系是否一致 |
| JSON 算法没有带点位 | 是否传入 `POITemplateParam`，模板是否选中 |
| Flow 里没有模板选择 | 常规字段检查节点的 PropertyEditor 类型和 `FlowPropertyEditorRegistry`；BuildPOI 的多模板入口再查 `POINodeConfigurators.cs` |
| Build POI 后没保存 | `SavePOITempName`、`LayoutROI`、`CADMapping` 和回写模板是否正确 |
| 过滤/修正结果异常 | `TemplatePoiFilterParam`、`TemplatePoiReviseParam`、`TemplatePoiOutputParam` 是否被联动选中 |
| 文件模式不生效 | `AlgorithmPoi` 的 `POIStorageModel.File` 路径和外部点文件是否有效 |

标准 CIE 批量测量的原生参数、32 位浮点布局、V1/V2 选项与返回值见 [opencv_helper POI Batch API](../../engine-components/opencv-helper-api.md#poi-batch-api)。这里的模板过滤/修正配置不能直接等同于原生 V2 options 的调用入口。

## 数据模型

| 对象 | 当前作用 |
| --- | --- |
| `PoiPoint` | 点位实体，保存 `Id`、`Name`、`PointType`、`PixX`、`PixY`、`PixWidth`、`PixHeight` |
| `PoiParam` | POI 模板参数，保存尺寸、类型、四角坐标、点集、`CfgJson`、`PoiConfig` |
| `TemplatePoi` | 主模板入口 |
| `PoiMasterDao` | POI 主记录 |
| `PoiDetailDao` | POI 点明细 |

`CfgJson` 不是普通备注字段，当前会和 `PoiConfig` 相互序列化、反序列化。修改配置结构时要同时验证数据库读取、保存和旧模板兼容性。

矩形/四边形点阵按行列方向插值。某一方向数量为 `1` 时，该方向固定取区域中线；`1×1` 因而位于区域中心。相对边距把四角同时缩进 `50%` 后形成的重合点只允许生成 `1×1` 点阵，不能作为多点区域继续插值。

## 存储方式

POI 有专门的主从表结构，不只是普通模板明细：

1. 主记录通过 `PoiMasterDao` 保存。
2. 点明细通过 `PoiDetailDao` 保存。
3. `PoiParam.LoadPoiDetailFromDB(...)` 按 `Pid` 回填点集合。
4. `Save2DB(...)` 会清空旧明细后整批写入新点。

## 消费链

`AlgorithmPoi` 是最直接的 POI 消费者和生产者。它当前支持：

- 主模板 `TemplatePoi`
- 过滤模板 `TemplatePoiFilterParam`
- 修正模板 `TemplatePoiReviseParam`
- 输出模板 `TemplatePoiOutputParam`
- 文件模式 `POIStorageModel.File`

最终通过 `Event_POI_GetData` 发布带多模板参数的 MQTT 请求。`AlgorithmBuildPoi` 负责把其它信息转成 POI 点集，支持普通布点、CADMapping、四点多边形 `LayoutPolygon`、`CADMappingParam` 和 `Event_Build_POI`。

下游算法里，POI 通常作为输入模板引用：

- `AlgorithmPoiAnalysis` 会附带 `POITemplateParam`
- `AlgorithmSFRFindROI` 会附带 `POITemplateParam`
- `AlgorithmOLEDAOI` 也会附带 `POITemplateParam`

Flow 节点里的常规 POI 字段由节点属性指定 PropertyEditor，并通过 `FlowPropertyEditorRegistry` 生成模板选择器；`POINodeConfigurators.cs` 只给 `BuildPOINode.TemplateName` 补充 BuildPOI/AA 两类模板选择：

- `POINode` 需要主模板、过滤、修正、输出模板
- `BuildPOINode` 会同时接布点模板、回写 POI 模板和布局 ROI 模板
- `POIReviseNode` 会接修正标定模板
- `POIAnalysisNode` 会接 JSON 分析模板

## 修改边界

- POI 不是单一检测算法的结果结构，它同时用于检测、布点、分析、AOI 和 Flow。
- 主模板走数据库，但 `AlgorithmPoi` 也支持文件模式和外部点文件。
- 过滤、修正、标定、输出模板都是当前系统的一等成员。
- `PoiAnalysis`、`SFR_FindROI`、`OLEDAOI` 是消费 POI，不是创建 POI 的主入口。

## 关键文件

| 文件 | 作用 |
| --- | --- |
| `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs` | 点位实体 |
| `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs` | 模板参数、点集加载和保存 |
| `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs` | POI 主模板 |
| `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs` | 主 POI 算法和 MQTT 请求 |
| `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs` | 布点算法 |
| `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs` | 过滤模板 |
| `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs` | 修正模板 |
| `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs` | 输出模板 |
| `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePOICalParam.cs` | 标定生成模板 |
| `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/AlgorithmPoiAnalysis.cs` | JSON POI 分析 |
| `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs` | SFR ROI 查找 |
| `Engine/ColorVision.Engine/Templates/Jsons/OLEDAOI/AlgorithmOLEDAOI.cs` | OLED AOI |
| `Engine/ColorVision.Engine/FlowProcessing/Editor/NodeConfiguration/POINodeConfigurators.cs` | `BuildPOINode` 的多模板补充面板 |

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/PoiPointModelTests.cs`、`Test/ColorVision.UI.Tests/PoiLayoutGeometryTests.cs`。

点模型和几何测试不覆盖数据库整批保存或远端 POI 请求；旧模板、文件模式及服务侧协议需另外回放。
