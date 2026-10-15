---
knowledge_id: "algorithms.find-light-area"
knowledge_type: "topic"
status: "current"
summary: "AA 模板与本地发光区定位(V2)的入口、图像来源、POI保存模板和结果边界。"
aliases: ["寻找AA区","AA发光区定位","TemplateAAFindPoints","AaFindPointsTemplatePropertiesEditor","本地发光区定位(V2)","本地发光区定位V2为什么拒绝","原生亮区四角点置信度","发光区检测失败原因","POI保存模板","SavePOITempName","最小置信度","搜索区域","cvnative::luminous","FindLuminousAreaV2Result","hasCorners","LocalFindLuminousAreaNode","M_FindLuminousAreaV2","RobustV2"]
code_paths: ["Engine/ColorVision.Engine/Templates/Jsons/AAFindPoints","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Algorithm/AlgorithmFindLightAreaNode.cs","Engine/ColorVision.Engine/PropertyEditor/FlowTemplatePropertiesEditors.cs","Engine/ColorVision.Engine/Abstractions/ViewResultAlgType.cs","Engine/cvColorVision/CVCommCore/CVResultType.cs","Engine/FlowEngineLib/Node/Algorithm/CVResultType.cs","Engine/cvColorVision/MQTTMessageLib/Algorithm/MQTTAlgorithmEventEnum.cs","Engine/ColorVision.Engine/Services/Results/LightArea","Engine/ColorVision.Engine/Services/Results/LocalFindLuminousArea","Engine/ColorVision.Engine/Templates/ITemplate.cs","Engine/ColorVision.Engine/Templates/POI/LocalLuminousAreaPoiTemplateUpdater.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFlowResultPersistence.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs","UI/ColorVision.Core/LuminousAreaDetection.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/FindLuminousArea","UI/ColorVision.ImageEditor/EditorTools/GraphicEditing/GraphicEditingWindow.xaml.cs","Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.h","Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.cpp","Native/include/opencv_media_export.h","Native/opencv_helper/opencv_media_export.cpp"]
test_paths: ["Test/ColorVision.UI.Tests/CompatibilityNodeMigrationTests.cs","Test/ColorVision.UI.Tests/CompatibilityCameraPropertyEditorTests.cs","Test/ColorVision.UI.Tests/LocalFindLuminousAreaNodeTests.cs","Test/ColorVision.UI.Tests/LightAreaResultHandlerTests.cs","Test/ColorVision.UI.Tests/FindLuminousAreaManualResultTests.cs","Test/opencv_helper_test/test_find_luminous_area.cpp"]
related: ["algorithms.index","algorithms.roi-routes","algorithms.template-management","engine.native-integration","engine.results"]
---

# 发光区定位：AA 模板与本地 V2

发光区定位节点使用 AA JSON 模板；**参数模板**字段内直接选择模板，旁边的按钮提供参数编辑和模板管理。节点类型、已有连线、模板名称、图像文件、POI 保存模板及角点顺序的持久化契约不变。`TemplateAAFindPoints` 的字典号为 `42`、编码为 `FindLightArea`。

旧定位字典 `31` 与 FocusPoints 字典 `15` 的数据不会自动转换成 AA JSON，也不会由界面清理。保留旧模板的流程需要人工选择 AA 模板；服务端权限独立于主程序，本次主程序移除入口不代表服务端实现已移除。通用旧算法枚举编号 `7/8/9` 保留空位，其中 `9` 是已退役项目的 JND 计算入口；旧流程不能将它们误解释为后续算子，执行时明确拒绝。

**本地发光区定位(V2)** 使用本机 `RobustV2` 算法，不需要检测参数模板。本地计算不依赖远端算法服务；本地 Flow 节点的结果保存仍依赖 MySQL 和已有流程批次。两条 Engine 链路共用角点模型、DAO 和点位明细表；AA 与本地 V2 分别由各自的结果处理器加载和展示。

## 配置本地发光区定位(V2)

在流程中加入 **本地发光区定位(V2)**，默认使用上游图像：接入相机取图或校正节点，本地文件通过 **加载图片 → 发光区定位** 输入。图像方向变换必须已完成；节点借用当前图像计算，不修改源像素。没有可用上游图像时明确报错，并提示连接取图、校正或加载图片节点。

| 界面选项 | 属性 / 默认值 | 使用方式 |
| --- | --- | --- |
| 搜索区域 | `SearchRegion` / 整图 | `0,0,0,0` 表示整图；指定 ROI 时宽高必须大于零且完全位于图像内，输出还原为整图坐标 |
| 最小置信度 | `MinimumConfidence` / `0.25` | 必须是 `0` 到 `1` 的有限数值；低于门限拒绝结果，不会静默改用旧算法 |
| POI保存模板 | `SavePOITempName` / 空 | 可选的数据库回写目标，见下文；不是检测输入参数模板 |

运行包含该节点的流程后，检查 `Success`、置信度、警告与四角顺序，并在历史结果中核对本次主结果。成功四角按 **LT、RT、RB、LB** 保存；生成四个坐标本身不能作为成功判据。

### 图像来源与格式

节点按以下优先级使用上游图像：

1. 当前上游本地内存帧。
2. `IN` 输入图像结果；没有有效输入引用时读取流程数据中的 `MasterId`、`MasterResultType`。结果类型需为相机图像或算法校正，读取对应 `FileUrl`、`RawFile` 中存在的文件。

已选来源存在但无效时会报错，不会一路忽略错误尝试后续来源。例如上游图像结果指向的文件不存在时直接报错；声明 CIE 主缓冲却缺少 CIE 数据时，也不会改用 RAW。成功从文件加载的帧会交给后续节点，使角点与图像保持同源。

本地 RAW 输入支持 8/16 位、1/3 通道交错缓冲；CIE 支持 32 位浮点单平面 Y 或三平面 XYZ，三平面只借用第二个 Y 平面。文件加载器先识别 ColorVision 文件头，否则交给 WPF 位图解码器；支持的位图像素格式包括 Gray8、Gray16、Rgb48、24/32 位彩色，并非只接受 `.cvraw` / `.cvcie` 扩展名。

### 回写 POI 保存模板

填写 **POI保存模板** 会在定位成功后更新已有 POI 模板的数据库明细；留空则不回写。模板按去除首尾空白的名称、租户 `0`、未删除条件查询，同名时取最小 ID；`IsEnable` 不参与筛选。

- 一个 `Rect` 或 `LTRect` 明细：写入四角的外接矩形；前者记录中心，后者记录左上角。宽高采用 `floor(max) - floor(min) + 1`。
- 四个角点明细：按明细 ID 顺序写入 LT、RT、RB、LB，坐标转为整数，宽高归零。按服务的 `POIPointTypes` 判断类型；首行允许 `PolygonFour` 或兼容的 `LTRect` 数值表示，写回时统一为 `PolygonFour`。
- 其它形状、明细数量或不存在的模板会报错，不会自动创建新模板。

下游本地十字定位和本地点阵畸变 V2 的“搜索区域关注点”可选择同一模板，按最新保存明细生成搜索矩形，优先于节点固定区域。四角点取外接矩形，不透视裁正；模板读写遵循服务点类型数值。节点不缓存坐标，但模板不携带帧身份，也不能证明本轮前序检测成功；流程必须保证前序成功后再执行，并行流程使用不同模板以避免互相覆盖。

POI 回写有自己的事务，且在算法结果事务之前提交。后续结果保存失败不会撤销已提交的 POI 修改；这两步不是一个整体事务。

### 结果提交与失败状态

成功路径为：检测与四角校验 → 可选 POI 回写 → 主结果及四条明细提交 → 设置流程主结果引用 → 发布结果消息。主结果和明细使用同一事务，数据库表需支持事务；若保存或回滚失败，异常会向上抛出。

| 失败位置 | 可观察结果 |
| --- | --- |
| 检测返回拒绝或四角校验失败 | 尝试保存 `ResultCode = -1` 的失败主结果及诊断参数，不保存角点明细；发布该失败记录后仍按失败结束，不设置本次可用主结果引用 |
| 图像输入、ROI、方向变换或前置配置检查失败 | 尚未进入上述失败记录保存分支，不能保证历史结果中已有失败主记录 |
| POI 回写或结果保存失败 | 不继续发布成功结果；已先提交的 POI 回写不随结果保存失败回滚 |
| 成功结果提交后消息发布失败 | 已提交的结果和流程主结果引用保留，节点仍抛出异常；应核对结果 ID，避免重复执行造成重复数据 |

保存成功与保存失败主结果都需要按流程流水号找到批次。找不到批次、数据库不可用或消息发布异常时，不能假定失败记录已经出现在“算法结果管理”。

## RobustV2 的结果判据

V2 将输入归一化，在多尺度、多阈值候选中寻找四边形，再沿粗边用卡尺取样和鲁棒直线拟合确定边界。默认允许尝试边界目标并附加警告；角点几何、逐边证据和总置信度仍需通过检查。遮挡、裁边、多候选或无有效信号均可能导致拒绝，不应仅凭叠图判断业务可接受。

| 调用层 | 成功与结果边界 |
| --- | --- |
| C++ `cvnative::luminous::FindLuminousAreaV2` | 返回 `FindLuminousAreaV2Result`，检查 `success` 与 `hasCorners`；`corners` 是固定四元素数组，默认零坐标也占四项。拒绝仍可能保留诊断角点、置信度、原因和警告 |
| ABI `M_FindLuminousAreaV2` | 正返回值表示产生 JSON，不表示 `Success=true`；仅在 `hasCorners` 为真时填写 `Corners`。结果指针按 `FreeResult` 契约释放；参数或调用错误与算法拒绝分别处理 |
| 托管 `LuminousAreaNative.DetectV2` | 解析 JSON、校验成功结果几何、还原 ROI 坐标并执行置信度门限。`HasValidCorners` 要求 `Success` 且角点几何有效，不因诊断角点存在而把失败改为成功 |

内存所有权和 ABI 约定见 [OpenCV 和 native 集成](../../../02-developer-guide/engine-development/opencv-integration.md)。ImageEditor、POI 的配置对象默认使用 `RobustV2`，旧配置缺少 `Algorithm` 字段时也保持此默认值；显式选择经典兼容模式才显示 `Threshold`、`UseRotatedRect`。本地 V2 Flow 节点固定使用 RobustV2。

ImageEditor、POI 还可显式选择 `FovLuminanceBoundary`（界面名 **实验：中心亮度比例边界（非几何 FOV）**）。它先调用 `RobustV2`，再以中心亮度乘 `LuminanceBoundaryRatio`（默认 `0.5`）寻找亮度交点并拟合四边。内部暗角和渐变可能使角点明显内缩，该实验方法不代表标准有效视场。普通发光区和独立 FOV 均默认使用 `RobustV2` 几何边缘；FOV 有上游四角时直接复用，不调用此实验算法。

## 历史结果与 CSV

`AlgResultLightAreaModel` 和 `AlgResultLightAreaDao` 位于 `Engine/ColorVision.Engine/Services/Results/LightArea/` 公共结果目录，命名空间为与目录一致的 `ColorVision.Engine.Services.Results.LightArea`。模型的 `Id`、`Pid`、浮点 `PosX` / `PosY`、表名 `t_scgd_algorithm_result_detail_light_area` 及数据库列映射不变。ARVRPro 更新类型引用后继续通过 DAO 读取，并按项目原有方式绘制；其保存的 JSON 属性名也保持不变。

此命名空间调整改变了公开类型的完整名称。引用旧类型的项目插件需要同步更新引用、重新编译，并与包含新类型的主程序配套使用；数据库无需迁移，已有角点明细与项目 JSON 仍按原字段读取。

- AA 流程结果：`FindLightArea` 且版本 `AA`，由 AA 所属的 `ViewHandleAAFindPoints` 从公共角点 DAO 加载。
- 本地 V2 流程结果：`FindLightArea` 且版本 `RobustV2`，由 `ViewHandleLocalFindLuminousArea` 加载。已有本地记录未保存版本，使用空版本且模板名 `RobustV2` 的组合兼容；新成功与失败主记录均保存明确版本，避免按处理器发现顺序分派。
- 独立 JSON AA 入口：`ARVR_AAFindPoints` 且版本 `AA`，仍由 `ViewHandleAAFindPoints` 读取通用明细指向的结果文件；将 JSON `corner` 映射为同一个角点模型供展示。原始 JSON 和 `Points` 布点数据保留，不额外写入角点表，也不改变主结果类型。

两个处理器的匹配条件互斥。流程角点明细仅在 `ViewResults == null` 时加载，已有集合会复用；失败结果没有明细时不生成角点图元。

结果编号 `8` 已退役并保留空位；当前发光区流程编号为 `FindLightArea=36`，独立 AA JSON 入口为 `ARVR_AAFindPoints=61`。旧数据库记录不删除、不重编号；编号 `8` 加载为未定义枚举值，本地 FOV 和 POI 布点拒绝接收该类型。旧 JND、初版定位及无法判定归属的记录也不由当前两个处理器展示。恢复旧记录使用需要同时恢复其结果定义、版本专属处理器和下游接收分支，不能归给 AA 或本地 V2。

两条流程的列表展示保存的浮点点位；叠图先将坐标转为整数，再经 `GrahamScan.ComputeConvexHull()` 求凸包，绘制透明填充、蓝色轮廓的 `DVPolygon`。浮点明细、原始点序与屏幕上的整数凸包是不同表示，不能仅凭凸包还原原始结果。

**保存数据列** 尚未提供角点 CSV 导出：本地处理器沿用基类空实现，AA 处理器也未写入数据。按钮可用或调用返回不表示生成了角点 CSV。

## 按现象排查

| 现象 | 检查顺序 |
| --- | --- |
| AA 模板下拉为空或提示未选择 | 检查 MySQL 连接、字典 `42` 下的 JSON 模板及选择项 |
| 远端服务收不到图像 | 核对事件 `FindLightArea`、`ImgFileName`、`FileType` 与服务端文件可见性 |
| 结果页无点位或凸包异常 | 核对结果类型、主结果 ID、明细 `pid` 和浮点点位；区分检测失败、数据库未加载及整数凸包显示 |
| V2 逐边证据不足 | 查看 `SideQuality` 的覆盖率、内点比例、边缘对比度、残差、最大缺口；检查暗角、漏光、遮挡后再调整门限 |
| V2 多个相近候选 | 相近候选可产生 `AmbiguousCandidates` / `MultipleComparableCandidates` 警告并降低置信度；降分后低于门限仍返回 `LowConfidence`。需要唯一目标时缩小搜索 ROI 或消除第二个相似发光面 |
| V2 节点失败但没有失败记录 | 先判断是否进入检测结果校验分支，再检查批次、数据库保存和消息发布；输入或 POI 回写失败不保证创建失败主记录 |
| V2 找不到图像或来源不符 | 按上文来源顺序检查内存帧、配置文件、`IN` 结果与方向状态；不要只检查文件扩展名 |

## 验证入口与边界

`LocalFindLuminousAreaNodeTests.cs` 使用替代检测、保存、发布服务和事务对象检查节点输入优先级、POI 更新次序、四角顺序、失败主结果及提交/回滚流程，不能代替真实数据库或 native 验证。`FindLuminousAreaManualResultTests.cs` 检查手动诊断消息内容；`LightAreaResultHandlerTests.cs` 检查处理器归属互斥、旧本地记录兼容、公共模型的新命名空间与数据库契约及 JSON 角点映射，不连接真实数据库。

普通 ABI 反射检查不等于执行了导出函数。C++ 合成回归位于 `Test/opencv_helper_test/test_find_luminous_area.cpp`。

现场验收应保留有预期结果的图像集，覆盖透视、旋转、16 位输入、暗角、漏光、饱和、噪声、遮挡、裁边和多候选；分别检查成功角点误差、拒绝原因与事务落库。测试文件存在、native 返回正数或单张叠图都不表示这些链路已通过验收。
