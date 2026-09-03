---
knowledge_id: "algorithms.find-light-area"
knowledge_type: "topic"
status: "current"
summary: "发光区定位1与本地发光区定位(V2)的使用、图像来源、POI保存模板和结果边界；区分算法拒绝、数据库提交与消息发布，并说明模板字典恢复不一致。"
aliases: ["发光区定位1","发光区检测模板","本地发光区定位(V2)","本地发光区定位V2为什么拒绝","原生亮区四角点置信度","发光区检测失败原因","POI保存模板","SavePOITempName","最小置信度","搜索区域","恢复Mysql发光区检测","cvnative::luminous","FindLuminousAreaV2Result","hasCorners","LocalFindLuminousAreaNode","M_FindLuminousAreaV2","TemplateRoi","RobustV2"]
code_paths: ["Engine/ColorVision.Engine/Templates/FindLightArea","Engine/ColorVision.Engine/Templates/ITemplate.cs","Engine/ColorVision.Engine/Templates/POI/LocalLuminousAreaPoiTemplateUpdater.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFindLuminousAreaNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFlowResultPersistence.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs","UI/ColorVision.Core/LuminousAreaDetection.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/FindLuminousArea","UI/ColorVision.ImageEditor/EditorTools/GraphicEditing/GraphicEditingWindow.xaml.cs","Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.h","Native/opencv_helper/algorithm/luminous_area/luminous_area_v2.cpp","Native/include/opencv_media_export.h","Native/opencv_helper/opencv_media_export.cpp"]
test_paths: ["Test/ColorVision.UI.Tests/LocalFindLuminousAreaNodeTests.cs","Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs","Test/ColorVision.UI.Tests/FindLuminousAreaManualResultTests.cs","Test/opencv_helper_test/test_find_luminous_area.cpp"]
related: ["algorithms.index","algorithms.roi-routes","algorithms.focus-points","algorithms.template-management","engine.native-integration","engine.results"]
---

# 发光区定位：远端模板与本地 V2

发光区定位用于从图像中取得发光面的点位或四角。**发光区定位1** 使用数据库模板并向算法服务发送请求；**本地发光区定位(V2)** 使用本机 `RobustV2` 算法，不需要检测参数模板。两条 Engine 链路共用点位明细表和历史结果处理器，但参数、执行端和成功判据不同。

本地算法计算不依赖远端算法服务；本地 Flow 节点的结果保存仍依赖 MySQL 和已有流程批次。名称相近的 **发光区1** 属于另一套 [FocusPoints 模板](./focus-points-template.md)。

## 使用远端发光区定位1

1. 在算法设备的通用手动面板中选择 **基础算法 → 发光区定位1**。
2. 选择 **发光区检测模板**。需要编辑时使用选择器旁的编辑命令并保存，具体步骤见[模板编辑与创建](./template-management.md)。
3. 设置算法服务可读取的图像路径，点击 **计算**。输入助手检查模板及非空路径，不检查服务端文件可见性。
4. 核对服务返回及相应历史结果。创建 `MsgRecord` 只表示建立请求记录并发起发送，不是算法计算或落库成功。

`TemplateRoi : ITemplate<RoiParam>` 的编码为 `FindLightArea`、字典号为 `31`。下表是无明细的新空参数对象初值，现有模板以保存的明细为准；`MysqlRoi` 中三个参数项的默认值也均为 `1`。

| 参数 | 类型 / 新空对象初值 | 含义 |
| --- | --- | --- |
| `Threshold` | `int` / `1` | 发光区阈值；比较结果时应保持图像类型、曝光条件一致 |
| `Times` | `int` / `1` | 算法服务参数，具体次数含义由服务解释 |
| `SmoothSize` | `int` / `1` | 平滑尺寸参数，实际效果由服务实现决定 |

`AlgorithmRoi.Execute()` 将图像路径、文件类型和选中参数交给 `SendCommand()`。请求含 `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType` 与 `TemplateParam = { ID: param.Id, Name: param.Name }`；手动入口传入的两个设备字段为空字符串，不在 `TemplateParam` 中展开三项参数。事件常量 `Event_LightArea2_GetData` 的值为 `OLED.GetRIAandPT`。

### 模板加载与恢复限制

模板加载按 `ModMasterModel.Pid == 31`、租户 `0`、未删除条件读取数据。程序集发现模板不代表数据库已有字典和模板；创建失败后，通用宿主可提示重置数据库相关项，需经用户确认才执行恢复 SQL。

`MysqlRoi.GetRecover()` 存在字典关系不一致：主字典写入 ID `15`，参数项的 `pid` 却为 `31`，也与 `TemplateRoi.TemplateDicId = 31` 不一致；参数项使用普通 `INSERT`，重复执行可能遇到已有主键。不能把点击恢复当作字典已经修复。模板为空或创建失败时，应先核对现有主字典、参数项和模板所属字典，再处理数据库，不应反复运行恢复来试错。

## 配置本地发光区定位(V2)

在流程中加入 **本地发光区定位(V2)**，接入本地取图或校正后的图像。图像方向变换必须已完成；节点借用当前图像计算，不修改源像素。

| 界面选项 | 属性 / 默认值 | 使用方式 |
| --- | --- | --- |
| 图像文件 | `ImageFilePath` / 空 | 可选文件来源；上游已有内存帧时不使用此项 |
| 搜索区域 | `SearchRegion` / 整图 | `0,0,0,0` 表示整图；指定 ROI 时宽高必须大于零且完全位于图像内，输出还原为整图坐标 |
| 最小置信度 | `MinimumConfidence` / `0.25` | 必须是 `0` 到 `1` 的有限数值；低于门限拒绝结果，不会静默改用旧算法 |
| POI保存模板 | `SavePOITempName` / 空 | 可选的数据库回写目标，见下文；不是检测输入参数模板 |

运行包含该节点的流程后，检查 `Success`、置信度、警告与四角顺序，并在历史结果中核对本次主结果。成功四角按 **LT、RT、RB、LB** 保存；生成四个坐标本身不能作为成功判据。

### 图像来源与格式

节点依次选择以下来源：

1. 当前上游本地内存帧。
2. 配置的非空 **图像文件**。
3. `IN` 输入图像结果；没有有效输入引用时读取流程数据中的 `MasterId`、`MasterResultType`。结果类型需为相机图像或算法校正，读取对应 `FileUrl`、`RawFile` 中存在的文件。

已选来源存在但无效时会报错，不会一路忽略错误尝试后续来源。例如非空配置路径不存在时，不再尝试 `IN` 图像结果；声明 CIE 主缓冲却缺少 CIE 数据时，也不会改用 RAW。成功从文件加载的帧会交给后续节点，使角点与图像保持同源。

本地 RAW 输入支持 8/16 位、1/3 通道交错缓冲；CIE 支持 32 位浮点单平面 Y 或三平面 XYZ，三平面只借用第二个 Y 平面。文件加载器先识别 ColorVision 文件头，否则交给 WPF 位图解码器；支持的位图像素格式包括 Gray8、Gray16、Rgb48、24/32 位彩色，并非只接受 `.cvraw` / `.cvcie` 扩展名。

### 回写 POI 保存模板

填写 **POI保存模板** 会在定位成功后更新已有 POI 模板的数据库明细；留空则不回写。模板按去除首尾空白的名称、租户 `0`、未删除条件查询，同名时取最小 ID；`IsEnable` 不参与筛选。

- 一个 `Rect` 或 `LTRect` 明细：写入四角的外接矩形；前者记录中心，后者记录左上角。宽高采用 `floor(max) - floor(min) + 1`。
- 四个角点明细：按明细 ID 顺序写入 LT、RT、RB、LB，坐标转为整数，宽高归零。按服务的 `POIPointTypes` 判断类型；首行允许 `PolygonFour` 或兼容的 `LTRect` 数值表示，写回时统一为 `PolygonFour`。
- 其它形状、明细数量或不存在的模板会报错，不会自动创建新模板。

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

## 历史结果与 CSV

`ViewHandleFindLightArea` 接受 `LightArea`、`FindLightArea` 两种结果。`AlgResultLightAreaModel` 将 `PosX`、`PosY` 和父结果 `Pid` 保存到 `t_scgd_algorithm_result_detail_light_area`；明细仅在 `ViewResults == null` 时加载，已有集合会复用。

列表展示保存的点位；叠图先将坐标转为整数，再经 `GrahamScan.ComputeConvexHull()` 求凸包，绘制透明填充、蓝色轮廓的 `DVPolygon`。浮点明细、原始点序与屏幕上的整数凸包是不同表示，不能仅凭凸包还原原始结果。

**保存数据列** 调用的 `SideSave()` 尚未写入表头或点位行，只向所选目录写入空内容的 `{ResultType}_{Batch}.csv`，并会覆盖同名文件。它不能作为有效点位 CSV 导出使用。

## 按现象排查

| 现象 | 检查顺序 |
| --- | --- |
| 远端模板下拉为空或提示未选择 | 检查 MySQL 连接、字典 `31` 下的模板及选择项；字典恢复限制见上文 |
| 远端服务收不到图像 | 核对事件 `OLED.GetRIAandPT`、`ImgFileName`、`FileType` 与服务端文件可见性 |
| 结果页无点位或凸包异常 | 核对结果类型、主结果 ID、明细 `pid` 和浮点点位；区分检测失败、数据库未加载及整数凸包显示 |
| V2 逐边证据不足 | 查看 `SideQuality` 的覆盖率、内点比例、边缘对比度、残差、最大缺口；检查暗角、漏光、遮挡后再调整门限 |
| V2 多个相近候选 | 相近候选可产生 `AmbiguousCandidates` / `MultipleComparableCandidates` 警告并降低置信度；降分后低于门限仍返回 `LowConfidence`。需要唯一目标时缩小搜索 ROI 或消除第二个相似发光面 |
| V2 节点失败但没有失败记录 | 先判断是否进入检测结果校验分支，再检查批次、数据库保存和消息发布；输入或 POI 回写失败不保证创建失败主记录 |
| V2 找不到图像或来源不符 | 按上文来源顺序检查内存帧、配置文件、`IN` 结果与方向状态；不要只检查文件扩展名 |

## 验证入口与边界

`LocalFindLuminousAreaNodeTests.cs` 使用替代检测、保存、发布服务和事务对象检查节点输入优先级、POI 更新次序、四角顺序、失败主结果及提交/回滚流程，不能代替真实数据库或 native 验证。`FindLuminousAreaManualResultTests.cs` 检查手动诊断消息内容。

`LuminousAreaNativeInteropTests.cs` 的真实 native 用例默认跳过，需要显式设置 `COLORVISION_RUN_LUMINOUS_NATIVE_V2_TESTS=1` 并具备兼容的 `opencv_helper.dll` 才会运行；普通 ABI 反射检查不等于执行了导出函数。C++ 合成回归位于 `Test/opencv_helper_test/test_find_luminous_area.cpp`。

现场验收应保留有预期结果的图像集，覆盖透视、旋转、16 位输入、暗角、漏光、饱和、噪声、遮挡、裁边和多候选；分别检查成功角点误差、拒绝原因与事务落库。测试文件存在、native 返回正数或单张叠图都不表示这些链路已通过验收。
