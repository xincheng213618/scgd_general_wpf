---
knowledge_id: "algorithms.matching"
knowledge_type: "topic"
status: "current"
summary: "说明本地模板匹配的节点内置配置、cvtm 导入导出与无数据库执行，以及旧服务 Matching 的请求与 AOI 结果。"
aliases: ["模板匹配用错模板怎么查","MatchingDisplayAlgorithmConfig","AlgorithmMatching","AlgorithmTMNode","ViewHandleMatching","本地模板匹配","LocalTemplateMatchingNode","cvtm","TemplateMatchingWindow"]
code_paths: ["Engine/ColorVision.Engine/Templates/Matching/AlgorithmMatching.cs","Engine/ColorVision.Engine/Templates/Matching/TemplateMatch.cs","Engine/ColorVision.Engine/Templates/Matching/ViewHandleMatching.cs","Engine/ColorVision.Engine/Templates/Matching/AlgResultAoiDao.cs","Engine/FlowEngineLib/Node/Algorithm/AlgorithmTMNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalTemplateMatchingNode.cs","Engine/ColorVision.Engine/Services/Algorithms/LocalTemplateMatchingService.cs","Engine/ColorVision.Engine/PropertyEditor/LocalMatchingTemplatePropertiesEditor.cs","UI/ColorVision.Core/TemplateMatching.cs","UI/ColorVision.Core/TemplateMatchingFile.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/P2/TemplateMatchingWindow.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LocalTemplateMatchingTests.cs","Test/opencv_helper_test/test_p2_algorithms.cpp"]
related: ["algorithms.index","algorithms.template-primitives","engine.results"]
---

# Matching 模板匹配

本地模板匹配用参考图在输入图像中搜索相似目标，支持旋转、尺度、多目标、分数阈值和距离抑制。它与依赖亮暗边界的发光区定位用途不同，仍需具有可区分结构的参考图。现有 `AlgorithmMatching` / `AlgorithmTMNode` 继续向服务发送 `Event_MatchTemplate`；不能据此推断远端内核与本地算法相同，两种模板格式也不能互换。

## 本地模板、参数与使用步骤

本地窗口与 `LocalTemplateMatchingNode` 共用 `TemplateMatching.Run`，直接调用现有 `M_MatchRotatedTemplate`；需要 Windows/x64 的 `opencv_helper.dll` 及依赖。参考图和参数保存在流程节点内，随流程保存和复制；单独分享配置时才导出 `.cvtm`，默认导出位置为当前用户 `%LocalAppData%\ColorVision\Templates\Matching`。匹配执行无需 MySQL、参数模板数据库或外部参考图路径。

1. 在图像编辑器打开输入图，绘制目标矩形，右键“设为旋转匹配模板”；也可在管理窗口“导入参考图”。矩形模板从编辑器源帧复制像素，不从伪彩显示图截取；文件导入使用 WPF 第一帧解码，格式支持边界仍见本地分析说明。
2. 从“分析测量 → 定位与几何 → 旋转模板匹配...”进入窗口，或右键搜索矩形“在此 ROI 执行旋转模板匹配”。“导入模板”读取 `.cvtm` 时同时替换当前草稿的参考图、名称与参数。
3. 用属性编辑器设置角度、尺度、匹配分数、最多数量、目标最小间距、特征模式、遮挡比例、金字塔层数和亚像素定位。窗口“运行匹配”使用当前编辑值，显示分数、中心、角度、尺度、JSON 和临时四角叠加；运行前清空旧结果。换源图后需重新打开窗口核对 ROI，运行期间换图或关窗会丢弃显示结果。
4. 编辑器内用“导出模板”分享 `.cvtm`；节点配置窗口内用“应用到节点”提交草稿，再保存流程。“新建模板”开始新参考图和默认参数。节点窗口的“导出模板”只写文件，不代替“应用到节点”；取消或关闭窗口不会修改节点。

新模板默认分数 0.8、最多 10 个目标、最小间距 12 px；角度 -15..15°/步长 1°；尺度 0.9..1.1/步长 0.05；Gradient、遮挡比例 0.2、金字塔 2 层、亚像素开启。参数范围在托管层及 native 校验，角度与尺度组合最多 10000 个。遮挡容忍度可能增加误匹配，应通过包含无目标图像的业务样本校准。

节点的 `ParameterJson` 与 `.cvtm` 共用 schema `colorvision.template-matching/v1`，包含名称、宽高、位深、通道、Base64 原始像素和完整参数。支持 8 位灰度/彩色、16 位灰度/RGB、32 位浮点灰度，最多 1600 万像素与 128 MiB 像素数据；浮点预览归一化不改变保存样本。导入拒绝未知版本、缺失必填对象、未知字段、尺寸/像素长度不一致及无效参数。导出先写同目录临时文件、刷新后替换；校验失败不覆盖原文件。导入后修改或删除原 `.cvtm` 不会影响节点；换机器时参考图和参数随流程携带；通过“加载图片”输入的待测文件需在目标机器可用。

## 本地流程节点

在流程编辑器“本地算法”选择“本地模板匹配”（`NodeType=LocalTemplateMatching`），连接 `IN → OUT`。在“匹配配置”旁点击“编辑…”调整节点内的参考图与参数，或导入其他人分享的 `.cvtm`；待测图片通过上游相机取图、校正或“加载图片”节点传入；“搜索区域”采用像素 `X,Y,Width,Height`，空值或全零代表全图，越界时报错。

每次运行从节点内的 JSON 建立本次快照，执行期间编辑配置不会改变该次匹配。输入为上游本地内存帧；仅传入旧图像记录 ID 而没有内存帧时明确报错，不查询数据库。RAW 使用交错 8/16 位灰度或 RGB，CIE 使用 32 位 Y 平面；方向变换未完成时拒绝执行。节点不改变源像素或上游帧的所有权，原帧继续交给下游。

成功后下游可读取 `LocalTemplateMatchingResult`、`LocalTemplateMatchingMatches`、`LocalTemplateMatchingBestMatch`，其中含整图中心、角度、尺度、分数、可见比例和四角。节点执行结果同时返回配置名称、内容指纹、本次参数、输入帧身份与耗时，供流程执行记录查看。此节点不创建数据库主表 ID、不发布旧服务结果事件，上游图像身份随输入继续传递；匹配结果不进入 AOI 历史表。编辑器的试匹配表格和临时叠加继续可用。流程宿主和其他上游节点自身的存储方式不由本节点改变。

无匹配、常量参考图等 native 拒绝结果会停止流程，不设置成功输出；无效配置/ROI 或调用异常同样停止。执行开始清除该节点的旧匹配数据。本地服务适配器只负责文件读帧和 native 调用，结果通过现有流程运行通道传递，独立于下述旧服务的 AOI DAO 与结果 handler。

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

上述模板文件位于 `Engine/ColorVision.Engine/Templates/Matching/`；模板编辑见 [模板入口](./template-menu-entries.md)。

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

选择链是 `Config.Template.SelectedIndex → SelectedValue → MatchParam`；排查模板选错时检查该配置对象和实际请求。

## Flow 请求

`AlgorithmTMNode` 使用 `TempName` 选择参数模板，`TemplateFile` 表示匹配文件，`ImgFileName` 表示输入；`operatorCode = MatchTemplate`。节点构造 `TMParam(TemplateFile)`，再通过 `BuildImageParam(...)` 补全图像参数。它与手动入口共享服务语义，但参数来源不同，应分别验证。

## AOI 结果

`ViewHandleMatching.Load` 在 `result.ViewResults == null` 时从 `AlgResultAoiDao.GetAllByPid(result.Id)` 加载明细。handler 取 AOI 四角，经 `GrahamScan.ComputeConvexHull` 计算凸包，用蓝色 `DVPolygon` 叠加，并生成分数、角度、中心与四角表格。

末尾两列分别为 `BottomLeftPointX` 和 `BottomLeftPointY`。源图恢复与 handler 选择遵循 [Engine 结果链](../../engine-components/result-handoff-chain.md)。

## 排查与变更约束

| 现象 | 证据入口 |
| --- | --- |
| 没有执行或路径无效 | 服务连接、请求事件、输入图/模板文件在服务端的可访问性 |
| 参数模板不生效 | `Config.Template` 的类型与选中值、实际 `TemplateParam.ID/Name` |
| 结果表为空 | 主结果类型 AOI、`Id/pid` 对应关系、`ViewResults` 是否已缓存 |
| overlay 不对应原图 | 四角坐标系、输入图尺寸、当前展示源 |

改参数或结果字段时保留固定输入、模板文件和预期 AOI 四角样例；同时检查手动路径、Flow 路径及受影响的项目导出。参数范围、编译成功或类名存在均不能代替算法正确性验证。

## 验证入口与缺口

`LocalTemplateMatchingTests` 覆盖文件精度与版本/损坏校验、导出失败保留原文件、带步长的 ROI 复制、真实 native 合成图匹配、节点配置快照/序列化、删除导入文件后运行、无数据库执行、编辑草稿应用/取消、内存帧所有权和失败输出边界。native 的 `test_p2_algorithms.cpp` 另含旋转、缩放与遮挡用例。保留这些回归以保护可迁移配置及流程生命周期契约，测试文件存在不表示任意环境都已执行。

真实业务参考图、误检漏检范围与完整现场流程仍需单独验收；合成图不能替代这些证据。远端 Matching 请求、数据库结果以及流程宿主存储也不由本地用例验收。
