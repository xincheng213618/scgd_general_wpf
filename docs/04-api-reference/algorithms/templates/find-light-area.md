---
knowledge_id: "algorithms.find-light-area"
knowledge_type: "topic"
status: "current"
summary: "区分远端 FindLightArea 模板与本地原生亮区检测 RobustV2；四角点不等于成功，须核对置信度、失败原因和各调用层的结果契约。"
aliases: ["本地发光区定位V2为什么拒绝","原生亮区四角点置信度","发光区检测失败原因","cvnative::luminous","FindLuminousAreaV2Result","hasCorners","LocalFindLuminousAreaNode","M_FindLuminousAreaV2","TemplateRoi","RobustV2"]
code_paths: ["Engine/ColorVision.Engine/Templates/FindLightArea","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs","UI/ColorVision.Core/LuminousAreaDetection.cs","Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.h","Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.cpp","Native/include/opencv_media_export.h","Native/opencv_helper/opencv_media_export.cpp"]
test_paths: ["Test/ColorVision.UI.Tests/LocalFindLuminousAreaNodeTests.cs","Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs","Test/ColorVision.UI.Tests/FindLuminousAreaManualResultTests.cs","Test/opencv_helper_test"]
related: ["algorithms.index","algorithms.roi-routes","engine.native-integration","engine.results"]
---

# FindLightArea 发光区定位模板

本页说明发光区定位的两条真实处理链路：`Engine/ColorVision.Engine/Templates/FindLightArea/` 是“模板参数 -> 图像输入 -> MQTT 算法请求 -> 发光区点位结果 -> 图像凸包覆盖层”的远端业务模板；`LocalFindLuminousAreaNode` 和 ImageEditor/POI 共用的 `RobustV2` 则是零模板、可离线执行的本地四边形定位。两者使用同一结果明细表，但参数和执行端不同。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 模板代码 | `FindLightArea` |
| 模板类 | `TemplateRoi : ITemplate<RoiParam>, IITemplateLoad` |
| 参数类 | `RoiParam` |
| 执行入口 | `AlgorithmRoi`，显示名“发光区定位1” |
| 运行配置 | `SingleTemplateDisplayAlgorithmConfig` + `DisplayAlgorithmBase` 通用界面 |
| MQTT 事件 | `MQTTAlgorithmEventEnum.Event_LightArea2_GetData` |
| 结果处理 | `ViewHandleFindLightArea` |
| 结果表 | `t_scgd_algorithm_result_detail_light_area` |
| 本地 Flow 节点 | `LocalFindLuminousAreaNode`，显示名“本地发光区定位(V2)” |
| 本地 native 入口 | `M_FindLuminousAreaV2`，算法标识 `RobustV2` |

## 源码入口

| 文件 | 用途 |
| --- | --- |
| `TemplateRoi.cs` | 注册 `FindLightArea` 模板，设置 `TemplateDicId = 31`，并通过 `MysqlRoi` 恢复模板字典。 |
| `ROIParam.cs` | 保存 ROI 参数：`Threshold`、`Times`、`SmoothSize`。 |
| `AlgorithmRoi.cs` | 组装算法请求，填入图像、设备和模板参数，并发布 MQTT 命令。 |
| `AlgResultLightAreaDao.cs` | 定义结果模型、结果加载、图像覆盖层和列表展示。 |
| `MysqlRoi.cs` | 恢复 MySQL 字典和默认模板项。 |
| `FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs` | 从上游内存帧或本地文件执行 V2，校验四角点并原子保存主结果和四条明细。 |
| `UI/ColorVision.Core/LuminousAreaDetection.cs` | 统一 native 调用、UTF-8 JSON 解析、ROI 坐标还原、置信度门限和失败契约。 |
| `Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.cpp` | 多候选粗定位、四边卡尺取样、鲁棒直线拟合、几何校验和置信度计算。 |

## 执行链路

`TemplateRoi` 进入全局模板集合后，用户选择 `RoiParam`；`DisplayAlgorithmBase` 提供图像，`AlgorithmRoi.SendCommand(...)` 组装 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType` 及模板 ID/名称，通过 `Event_LightArea2_GetData` 发布。结果回写后，`ViewHandleFindLightArea` 按 `LightArea` / `FindLightArea` 加载点位并展示。

## 本地鲁棒 V2

V2 不要求用户维护阈值模板。默认配置会先把 8/16 位 RAW 或 32 位浮点亮度图归一化，在多尺度、多阈值候选中寻找可能的凸四边形；随后沿四条粗边布置一维卡尺，保留多个边缘候选，并通过 RANSAC 和稳健加权拟合剔除暗角、漏光、局部遮挡和内部伪边。四条拟合直线的交点按 `LT、RT、RB、LB` 输出。

这套流程采用“粗定位 + 卡尺测边 + 鲁棒几何拟合”，尝试从独立几何证据推断外轮廓，并用置信度和警告标记遮挡、裁边或多候选。无有效信号、没有候选、逐边证据不足、角点/几何关系不稳定或整体置信度不足时可能拒绝；不能把生成了四角点当成检测成功，也不能仅凭叠图判断业务可接受。

| 调用层 | 成功与结果边界 |
| --- | --- |
| C++ `cvnative::luminous::FindLuminousAreaV2` | 返回 `FindLuminousAreaV2Result`，检查 `success` 和 `hasCorners`。`corners` 本身是固定四元素数组，默认零坐标也占四项；数量不是成功依据。`LowConfidence` 等拒绝仍可能保留诊断角点、`confidence`、`failureReason` 和 `warnings` |
| ABI `M_FindLuminousAreaV2` | 正返回值表示产生 JSON，不表示 JSON `Success=true`；`Corners` 只在 C++ `hasCorners` 为真时填充。仍须核对 `Success`、`Confidence`、`FailureReason` 和 `Warnings`，结果指针按 `FreeResult` 责任释放；调用/参数错误与算法拒绝不同 |
| 托管 `LuminousAreaNative.DetectV2` | 解析 JSON、校验成功结果几何、还原 ROI 坐标，再按调用门限执行置信度判定。`HasValidCorners` 要求 `Success` 且角点几何有效；托管层同样不会因角点存在而把失败改为成功 |

ABI 与托管内存所有权的完整约定见 [OpenCV 和 native 集成](../../../02-developer-guide/engine-development/opencv-integration.md)。直接 C++ 调用、跨 ABI 调用与托管最终结果不是同一返回类型，不应套用同一套“返回码加角点数量”的判据。

### 默认使用方式

| 项目 | 默认行为 |
| --- | --- |
| 搜索范围 | 整图；配置 `SearchRegion` 后只在 ROI 内搜索，托管层会还原为整图坐标。 |
| 用户参数 | 只需保留 `MinimumConfidence = 0.25`；默认偏向高召回，误检敏感时再调高。 |
| 图像来源 | 优先使用上游本地内存帧；仅在没有上游帧时读取配置的 `.cvraw` / `.cvcie` 后备文件，并把该帧交给后续节点，确保角点与图像始终同源。 |
| RAW | 支持 8/16 位、1/3 通道交错缓冲。 |
| CIE | 支持单平面 Y 或三平面 XYZ；三平面输入只借用第二个 Y 平面。 |
| 边界目标 | 默认尝试从仍可见的边段外推四角并附加警告；只有图像证据不足以唯一推断时才拒绝。 |
| 结果 | 成功时主结果类型为 `FindLightArea`，四条明细严格按 `LT、RT、RB、LB` 保存；检测被拒绝或返回内容校验失败时仍保存一条非零 `ResultCode` 的失败主结果和原因，但不保存角点明细，也不向下游输出可用主结果。 |

ImageEditor 和 POI 的旧配置缺少 `Algorithm` 字段时会自动采用 `RobustV2`；需要复现旧流程时可显式切换为“经典兼容”，此时才显示 `Threshold` 和 `UseRotatedRect`。V2 不会在失败后静默回退旧算法，因为静默回退会把低可信结果伪装成成功。

本地 Flow 保存成功主结果和四条角点明细时使用同一个数据库事务；生产表必须使用支持事务的存储引擎（通常为 InnoDB）。保存失败时不会发布半套结果。定位失败会先保存仅含失败状态、原因、源文件和诊断参数的主结果，再发布到“算法结果管理”，随后节点仍按失败结束；失败记录不含四角点明细，也不会写入 `action.MasterValue(...)` 供下游误用。

## 参数说明

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `Threshold` | `1` | 发光区阈值。现场调整时要记录图像类型和曝光条件，否则阈值没有可复现意义。 |
| `Times` | `1` | 算法侧迭代/处理次数参数。具体语义由算法服务解释。 |
| `SmoothSize` | `1` | 平滑尺寸。会影响边界点稳定性，变更后要看结果凸包而不是只看点表。 |

## 结果展示

`AlgResultLightAreaModel` 只保存 `PosX`、`PosY` 和父结果 `Pid`。展示时会把所有点传给 `GrahamScan.ComputeConvexHull(...)`，再用蓝色透明 `DVPolygon` 画在图像上。

维护时注意：点位列表和凸包不是同一概念，凸包异常要回看输入图像和参数；当前 `SideSave(...)` 只创建文件而未写入点位行，不能视作稳定 CSV 导出能力。

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 模板下拉为空 | `TemplateRoi` 是否被程序集装载，`IITemplateLoad` 是否执行，`TemplateDicId = 31` 的字典是否恢复。 |
| 点击执行提示未选模板 | `TemplateRoi.Params` 是否已经加载，通用模板选择项是否有有效 `Value`。 |
| 算法服务收不到图像 | 通用图像输入是否取得路径；`ImgFileName` 和 `FileType` 是否匹配。 |
| 结果页无点位 | 结果类型是否是 `LightArea` 或 `FindLightArea`，`t_scgd_algorithm_result_detail_light_area.pid` 是否对应主结果。 |
| 覆盖层形状异常 | 先看 `Threshold`、`Times`、`SmoothSize` 和输入图像，再看 `GrahamScan` 凸包输入点。 |
| V2 提示逐边证据不足 | 查看 `SideQuality` 中对应边的覆盖率、内点比例、边缘对比度、拟合残差和最大缺口；优先检查暗角、漏光或遮挡，不要先降低总置信度。 |
| V2 提示多个相近候选 | 算法选择排序最优的候选，相近候选满足歧义条件时附加 `AmbiguousCandidates` / `MultipleComparableCandidates` 并降低置信度；降分后低于门限仍返回 `Success=false / LowConfidence`，并非保证接受最佳候选。若业务必须唯一定位，可缩小 `SearchRegion` 或消除画面中的第二个相似发光面。 |
| V2 节点失败但结果页没有角点 | 检查同批次是否已有 `FindLightArea` 失败主记录及 `ResultDesc`；失败不生成角点明细。若连失败主记录也没有，继续检查批次是否存在、数据库保存是否报错以及结果消息是否发布。 |
| V2 在本地节点找不到图像 | 连接本地取图/校正节点，或配置有效文件；同时确认图像方向变换已经完成。 |

## 检查清单

- 修改参数时，同时更新 `ROIParam.cs`、`MysqlRoi.cs` 和现场推荐值。
- 修改执行事件时，同时更新 `AlgorithmRoi.SendCommand(...)`、Flow 节点说明和本页。
- 修改结果结构时，同时更新 `AlgResultLightAreaModel`、结果表、展示列和导出逻辑。
- 若要把发光区结果交给项目包使用，项目文档必须说明读取的是点位、凸包还是原始图像区域。
- 修改 V2 时至少覆盖透视、较大旋转、16 位输入、暗角、漏光、饱和、噪声、局部遮挡、边界裁切和多候选；成功样本要校验角点误差，失败样本要校验拒绝原因。
- 现场验收要保留带预期结果的真实样本集并做批量回放，不能只凭单张叠图或“算法有返回值”判断通过。

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/LocalFindLuminousAreaNodeTests.cs`、`Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs`、`Test/ColorVision.UI.Tests/FindLuminousAreaManualResultTests.cs`、`Test/opencv_helper_test`。

托管契约、真实 native 与远端模板是不同层；需记录 native 是否实际加载、真实样本角点误差与事务落库结果，不以 native 返回正数代表定位成功。
