---
knowledge_id: "algorithms.matching"
knowledge_type: "topic"
status: "current"
summary: "说明 Matching 通用配置宿主、运行时模板文件、Flow 请求和 AOI 结果绘制。"
aliases: ["模板匹配用错模板怎么查","MatchingDisplayAlgorithmConfig","AlgorithmMatching","AlgorithmTMNode","ViewHandleMatching"]
code_paths: ["Engine/ColorVision.Engine/Templates/Matching/AlgorithmMatching.cs","Engine/ColorVision.Engine/Templates/Matching/TemplateMatch.cs","Engine/ColorVision.Engine/Templates/Matching/ViewHandleMatching.cs","Engine/ColorVision.Engine/Templates/Matching/AlgResultAoiDao.cs","Engine/FlowEngineLib/Node/Algorithm/AlgorithmTMNode.cs"]
test_paths: []
related: ["algorithms.index","algorithms.template-primitives","engine.results"]
---

# Matching 模板匹配

本页定位 `TemplateMatch` 的参数、`AlgorithmMatching` 手动请求、`AlgorithmTMNode` 与 AOI 历史结果。当前前端适配器向服务发送 `Event_MatchTemplate`，不能据此推断服务内部的匹配实现。

## 契约与源码

| 事项 | 当前实现 |
| --- | --- |
| 模板 / 参数 | `TemplateMatch : ITemplate<MatchParam>, IITemplateLoad` / `MatchParam` |
| 字典 / 编码 | `TemplateDicId = 34` / `MatchTemplate` |
| 手动入口 | `AlgorithmMatching : DisplayAlgorithmBase<MatchingDisplayAlgorithmConfig>` |
| 配置宿主 | `Services/Devices/Algorithm/DisplayAlgorithmControl.xaml.cs` |
| Flow | `Engine/FlowEngineLib/Node/Algorithm/AlgorithmTMNode.cs` |
| 结果 | `ViewResultAlgType.AOI`、`ViewHandleMatching`、`AlgResultAoiDao` |
| 明细表 | `t_scgd_algorithm_result_detail_aoi` |

上述模板文件位于 `Engine/ColorVision.Engine/Templates/Matching/`。手动界面已使用通用配置宿主，不再有 `DisplayMatching.xaml` 或 `ExportMenuItemMatching` 入口；模板编辑见 [模板入口](./template-menu-entries.md)。

## 持久参数与运行时输入

| `MatchParam` 字段 | 默认值 | 参数描述 |
| --- | --- | --- |
| `MinReducedArea` | `256` | 取样细致度，描述范围 64–2048 |
| `ToleranceAngle` | `0` | 误差角度，描述范围 0–180 |
| `Similarity` | `0.7` | 相似度阈值，描述范围 0–1 |
| `MaxOverlapRatio` | `0` | 交叠比例，描述范围 0–0.8 |
| `TargetNumber` | `70` | 目标数量 |

这些范围来自参数描述，不等于服务或编辑器已经强制校验。匹配用 `TemplateFile` 属于 `MatchingDisplayAlgorithmConfig` 的运行时输入，不是 `MatchParam` 字段。

## 手动请求

`DisplayAlgorithmControl` 根据 `Configuration` 生成输入控件，执行按钮调用 `Execute()`：

1. 从 `Config.Template` 解析当前 `MatchParam`，从 `Config.ImageFilePath` 解析图像输入和文件类型。
2. `SendCommand` 发送 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateFile`、`TemplateParam`。
3. 当前手动 `Execute` 传入空设备 code/type；其他调用方可显式传值。图像路径对服务是否可访问仍须单独确认。

当前选择链是 `Config.Template.SelectedIndex → SelectedValue → MatchParam`，不再使用旧 `TemplatePoiSelectedIndex` 与 `TemplateSelectedIndex` 绑定组合。排查模板选错时检查该配置对象和实际请求，不要按旧文档修复已移除的 ComboBox。

## Flow 请求

`AlgorithmTMNode` 使用 `TempName` 选择参数模板，`TemplateFile` 表示匹配文件，`ImgFileName` 表示输入；`operatorCode = MatchTemplate`。节点构造 `TMParam(TemplateFile)`，再通过 `BuildImageParam(...)` 补全图像参数。它与手动入口共享服务语义，但参数来源不同，应分别验证。

## AOI 结果

`ViewHandleMatching.Load` 在 `result.ViewResults == null` 时从 `AlgResultAoiDao.GetAllByPid(result.Id)` 加载明细。handler 取 AOI 四角，经 `GrahamScan.ComputeConvexHull` 计算凸包，用蓝色 `DVPolygon` 叠加，并生成分数、角度、中心与四角表格。

当前末尾列分别为 `BottomLeftPointX` 和 `BottomLeftPointY`，不再是旧文档所述的重复 X 表头。源图恢复与 handler 选择遵循 [Engine 结果链](../../engine-components/result-handoff-chain.md)。

## 排查与变更约束

| 现象 | 证据入口 |
| --- | --- |
| 没有执行或路径无效 | 服务连接、请求事件、输入图/模板文件在服务端的可访问性 |
| 参数模板不生效 | `Config.Template` 的类型与选中值、实际 `TemplateParam.ID/Name` |
| 结果表为空 | 主结果类型 AOI、`Id/pid` 对应关系、`ViewResults` 是否已缓存 |
| overlay 不对应原图 | 四角坐标系、输入图尺寸、当前展示源 |

改参数或结果字段时保留固定输入、模板文件和预期 AOI 四角样例；同时检查手动路径、Flow 路径及受影响的项目导出。参数范围、编译成功或类名存在均不能代替算法正确性验证。

## 验证入口与缺口

验证缺口：未登记 Matching 手动请求与 AOI DAO 的专门自动化测试；需用固定模板文件、输入图和预期四角结果分别验证手动与 Flow 路径。
