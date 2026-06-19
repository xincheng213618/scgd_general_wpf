# 当前算法模板覆盖清单

本文把源码中的 `Engine/ColorVision.Engine/Templates/` 目录和当前文档入口逐项对齐。它不是算法功能承诺表，而是交接时用来判断“这个模板目录先读哪篇文档、还缺什么说明”的覆盖清单。

## 覆盖状态说明

| 状态 | 含义 |
| --- | --- |
| 已有单页 | 已经有面向交接的专题页，能说明主要入口、运行链路和边界。 |
| 横向覆盖 | 目前归入模板管理、ROI/POI、通用算法模块或 Engine 链路页，还没有独立专题页。 |
| 待补单页 | 已能定位归属，但业务含义或验收口径需要后续拆成独立文档。 |

## Templates 目录覆盖

| 模板目录 | 业务角色 | 先读文档 | 状态 | 交接重点 |
| --- | --- | --- | --- | --- |
| `ARVR/` | AR/VR 检测模板族，连接模板参数、算法请求和结果展示。 | [ARVR 模板](./templates/arvr-template.md)、[结果交接链路](../engine-components/result-handoff-chain.md) | 已有单页 | 已覆盖模板矩阵、手动事件、Flow `operatorCode`、POI 依赖、结果表和 handler 验收项。 |
| `BuzProduct/` | 产品/业务参数模板，把产品主档、业务明细、POI 和 Validate 规则绑在一起。 | [BuzProduct 产品业务参数模板](./templates/buz-product-template.md)、[Validate 判定规则模板](./templates/validate-rules.md) | 已有单页 | 重点追踪 `BuzProduc` 源码拼写、主/明细表、`poi_id` 与 `val_rule_temp_id`。 |
| `Compliance/` | 合规结果展示与判定解释层，读取 Y/XYZ/JND 结果和 `ValidateResult`。 | [Compliance 结果交接](./templates/compliance-results.md)、[结果交接链路](../engine-components/result-handoff-chain.md) | 已有单页 | 重点追踪三张结果明细表、handler 结果类型映射和 `ValidateRuleResultType.M` 判定逻辑。 |
| `DataLoad/` | 数据加载模板，给 Flow 的 DataLoad 节点提供设备、批次、结果类型和 ZIndex 定位参数。 | [DataLoad 数据加载模板](./templates/data-load-template.md)、[模板与 Flow 链路](../engine-components/template-flow-chain.md) | 已有单页 | 重点区分 `AlgDataLoadNode` 模板路径和 `AlgDataLoadNode2` 显式参数路径。 |
| `FindLightArea/` | 发光区域/ROI 定位模板，和 OpenCV helper、ROI 结果强相关。 | [FindLightArea 发光区定位模板](./templates/find-light-area.md)、[ROI 原语](./primitives/roi.md) | 已有单页 | 重点追踪 `Event_LightArea2_GetData`、`RoiParam`、点位表和凸包覆盖层。 |
| `Flow/` | 流程模板，把模板系统和 `FlowEngineLib` 的可视化流程连接起来。 | [流程引擎](./templates/flow-engine.md)、[Engine 模板与 Flow 链路](../engine-components/template-flow-chain.md) | 已有单页 | 已覆盖 `TemplateFlow` 保存路径、`.cvflow` 包、导入导出、运行调度和节点配置器边界。 |
| `FocusPoints/` | 旧发光区/关注点参数模板，保存二值化、滤波、形态学、过滤和 ROI 边界。 | [FocusPoints 关注点模板](./templates/focus-points-template.md)、[FindLightArea 发光区定位模板](./templates/find-light-area.md) | 已有单页 | 重点区分 `Event_LightArea_GetData` 手动链路和 `operatorCode = "FocusPoints"` Flow 链路。 |
| `ImageCropping/` | 强类型图像裁剪模板，连接四点 ROI、Flow 双输入裁剪节点和裁剪结果展示。 | [ImageCropping 图像裁剪模板](./templates/image-cropping-template.md)、[结果交接链路](../engine-components/result-handoff-chain.md) | 已有单页 | 重点追踪 `Event_Image_Cropping`、`OLED.GetRIAand`、`ROI_MasterId` 和 `ViewHandleImageCropping`。 |
| `JND/` | JND 相关检测模板，通常与 AR/VR 或显示质量业务关联。 | [JND 模板](./templates/jnd-template.md)、[POI 模板](./templates/poi-template.md) | 已有单页 | 重点追踪 `CutOff`、`POITemplateParam`、`h_jnd/v_jnd` 和项目侧 OK/NG 边界。 |
| `Jsons/` | JSON 模板体系，提供文本/属性两种编辑和导入导出路径。 | [JSON 模板](./templates/json-templates.md)、[Templates API 参考](./templates/api-reference.md) | 已有单页 | 已覆盖当前 JSON 子模板目录、schema index、V2/旧强类型边界、handler 和验收项。 |
| `LedCheck/` | LED 检测模板族，面向灯珠/亮度/缺陷类检查。 | [LED 检测模板](./templates/led-detection.md)、[POI 模板](./templates/poi-template.md) | 已有单页 | 重点追踪 `FindLED` 新旧入口、POI 依赖、结果 handler 注册和导出边界。 |
| `LEDStripDetection/` | LED 灯条检测模板，常和 JSON 模板、条带定位、缺陷结果关联。 | [LED 检测模板](./templates/led-detection.md)、[JSON 模板](./templates/json-templates.md) | 已有单页 | 重点区分旧强类型 `Event_LED_StripDetection` 和 JSON V2 `Version = 2.0`。 |
| `Matching/` | 模板匹配/定位链路，包含手动算法页、Flow 节点、MQTT 请求和 AOI 结果展示。 | [Matching 模板匹配](./templates/matching-template.md)、[结果交接链路](../engine-components/result-handoff-chain.md) | 已有单页 | 重点追踪 `MatchTemplate`、`TemplateFile`、`t_scgd_algorithm_result_detail_aoi` 和四点 overlay。 |
| `Menus/` | 模板菜单/入口包装，决定模板菜单分组、父子关系和默认编辑窗口。 | [模板菜单入口](./templates/template-menu-entries.md)、[模板管理](./templates/template-management.md) | 已有单页 | 重点追踪 `OwnerGuid`、`Order`、`Header`、`Template` 和 `ShowTemplateWindow()`。 |
| `POI/` | POI 模板族，提供点位、区域和上游算法参数。 | [POI 模板](./templates/poi-template.md)、[POI 原语](./primitives/poi.md) | 已有单页 | 已覆盖主/伴生模板矩阵、专用点表、运行参数、BuildPOI、Flow 消费和结果 handler。 |
| `SysDictionary/` | 系统字典模板，维护 `mod_type = 7` 的算法默认字典主档和明细。 | [SysDictionary 系统字典模板](./templates/sys-dictionary-template.md)、[Templates API 参考](./templates/api-reference.md) | 已有单页 | 重点追踪 `TemplateModParam`、`symbol`、`default_val`、`val_type` 和迁移边界。 |
| `Validate/` | 判定规则模板体系，包含默认合规字典和实际判定模板两层。 | [Validate 判定规则模板](./templates/validate-rules.md)、[模板管理](./templates/template-management.md) | 已有单页 | 重点追踪 `mod_type = 110/111/120`、`CIEParams/JNDParams` 和规则主/明细表。 |

## 核心入口文件

| 文件 | 交接用途 |
| --- | --- |
| `TemplateContorl.cs` | 模板发现、`IITemplateLoad` 装载和注册入口。 |
| `TemplateManagerWindow.xaml(.cs)` | 模板管理窗口，适合追 UI 操作到模板数据的入口。 |
| `TemplateEditorWindow.xaml(.cs)` | 通用模板编辑窗口，适合追属性编辑、保存和校验。 |
| `TemplateSearchProvider.cs` | 模板搜索入口，适合追“为什么搜不到模板”。 |
| `TemplateSampleLibrary.cs` | 模板样例和复用入口，适合追默认模板来源。 |

## 维护规则

- 新增 `Templates/<Name>/` 目录时，先在本文补一行，再决定是否拆独立专题页。
- 如果目录包含 `Algorithm*`、结果视图或 MQTT 执行请求，文档必须说明参数来源、执行服务、结果字段和失败处理。
- 如果目录只是菜单、字典或包装层，也要写清它服务于哪个模板族，不要只写“辅助模块”。
- 当“待补单页”的目录进入客户项目交付、DLL 发布或现场验收范围时，应优先补成独立文档。

## 下一批优先补齐

1. Flow 转换/校准节点已经转入 [Flow 转换与校准节点](../engine-components/flow-conversion-calibration-nodes.md)：当前源码没有 `Templates/FileConvert/`、`Templates/ImageTransform/`、`Templates/Calibration/` 三个目录，后续按节点链路维护。
2. `Menus/`、`SysDictionaryMod/`：继续把菜单入口、字典默认值和模板窗口注册关系补成可交接清单。
3. `Projects/` 中仍未细化的客户项目包：继续把项目业务入口、依赖模板、插件能力和现场验收口径对齐。
