---
knowledge_id: "ui.core"
knowledge_type: "reference"
status: "current"
summary: "定位 HImage 所有权、OpenCV/CUDA PInvoke、ImageCompute 融合分流、位图桥接与默认关闭的原生日志。"
aliases: ["原生图像调用缺少DLL","ColorVision.Core","HImage","OpenCVMediaHelper","ImageCompute","NativeLogBridge","原生日志初始化","BmwSfrAnalyzer","SfrChromaticAberration"]
code_paths: ["UI/ColorVision.Core/HImage.cs","UI/ColorVision.Core/HImageExtension.cs","UI/ColorVision.Core/OpenCVMediaHelper.cs","UI/ColorVision.Core/OpenCVCuda.cs","UI/ColorVision.Core/ImageCompute.cs","UI/ColorVision.Core/NativeLogBridge.cs","UI/ColorVision.Core/ColorVision.Core.csproj","UI/ColorVision.Core/README.md","UI/ColorVision.Core/BmwSfrAnalyzer.cs","UI/ColorVision.Core/SfrChromaticAberration.cs"]
test_paths: ["Test/ColorVision.UI.Tests/HImageAbiTests.cs","Test/ColorVision.UI.Tests/HImageExtensionCopyTests.cs","Test/ColorVision.UI.Tests/NativeLogBridgeTests.cs","Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs","Test/ColorVision.UI.Tests/VideoFrameCopyTests.cs","Test/ColorVision.UI.Tests/BmwSfrAnalysisTests.cs","Test/CameraTest.Tests/ChromaticAberrationTests.cs"]
related: ["ui.index","engine.native-integration","ui.image-editor","ui.image-fusion","ui.image-frames"]
---

# ColorVision.Core

`UI/ColorVision.Core/` 提供原生图像/视频互操作、图像缓冲结构和 WPF 位图桥接。上层编辑工具、文档状态与结果叠加由 [ImageEditor](./ColorVision.ImageEditor.md) 等调用方负责；Core 的方法签名和返回值应按具体 native 函数核对。

## 能力与责任位置

| 能力 | 入口 | 使用边界 |
| --- | --- | --- |
| 原生图像结构 | `HImage.cs` | `HImage` 是含非托管指针的值类型；复制结构体不会复制像素或转移释放责任 |
| 位图转换与复制 | `HImageExtension.cs` | `ToPixelFormat`、`ToWriteableBitmap`、`ToHImage`、`UpdateWriteableBitmap` 等入口；复制、借用和释放语义需区分 |
| helper 导出 | `OpenCVMediaHelper.cs` | 伪彩、增强、滤波、阈值、SFR、聚焦评价、视频等 P/Invoke，参数与成功条件因函数而异 |
| CUDA 导出包装 | `OpenCVCuda.cs` | Fusion 系列、批量调用和输出释放；托管包装在调用前尝试准备 CUDA 日志 |
| Fusion 选择 | `ImageCompute.cs` | `UseCuda` 与 `Fusion()` 选路，不提供通用直方图、统计或滤波托管 API |
| 源帧与读者租约 | `SourceImageFrame.cs` | 引用计数、revision 与最后读者退出后的释放，完整规则见[源图像帧](./image-frame-lifetime.md) |
| 伪彩类型与日志 | `ColormapTypes.cs`、`NativeLogBridge.cs` | 颜色映射枚举与 native 日志桥接；日志默认关闭 |

`HImage` 的 ABI 布局、`isDispose` 含义及不同 native 函数的返回/释放约定由 [native 集成](../../02-developer-guide/engine-development/opencv-integration.md)维护。缓冲区不能仅因包装在 struct 中就按托管对象生命周期处理，也不能把不同函数族的整数返回值解释成统一成功码。

## 位图与调用结果

调用方先确认输入的尺寸、通道、位深、stride 和缓冲区有效期，再进入对应 native 接口。接口可能返回结构体、JSON 指针或写入输出参数，并非所有调用都经过同一条“返回 HImage → 显示位图”流程。

需要显示时，使用具体的位图桥接入口并遵守 WPF 线程和资源所有权要求。`ToPixelFormat` 的格式选择本身不是对任意像素布局的完整验证；异步复制与 native 调用返回也不能代替上层文档提交、叠加和渲染完成信号。

`HImageExtension` 中普通转换、带 Dispose 的转换、复制和借用入口的生命周期不同。`ToWriteableBitmapAndDispose` 会在 finally 中 Dispose 参数副本；若该副本负责释放缓冲，原副本仍保留的指针也会失效。完整转换契约及测试见[源图像帧与内存生命周期](./image-frame-lifetime.md)，不要为同一指针建立多个不协调的释放者。

`BmwSfrAnalyzer.Analyze(HImage, IReadOnlyList<BmwSearchRegion>, SfrAnalysisOptions)` 是人工框选 BMW／马蹄靶标的独立本地入口。每个搜索框必须带唯一非空 ID，并且只包含一个完整靶标；结果严格保持输入顺序和 ID，固定返回 Left、Top、Right、Bottom 四边。没有目标、多个候选、越界搜索框或单边失败均保留 INVALID 与原因，不删除或重新编号。新增接受 `BmwSfrRoiSettings` 的四参数重载；保留三参数方法以兼容已编译插件。测量框长宽与距中心距离以原图像素设置，0 保留自动值；沿刃边长与跨刃边宽随左/右、上/下方向交换。位置沿定位得到的轴线生成，超出所属搜索框时保留无效，不裁切或重试较小区域。`Located` 仅表示形状定位成功；`BmwEdgeAnalysis.Valid` 要求所有实际通道通过质量检查，单个通道的曲线和有效性仍独立保留。

定位由 `M_LocateBmwTargetV1`（`Native/opencv_helper/algorithm/sfr/sfr_bmw4.*`）验证对角扇区、外圈背景和近正交直线；只接受近水平／竖直、完整且半径至少约 55 px 的靶标。候选提取保留深色背景包围白色靶纸时的内部黑色连通分量，排除孔洞边界。闭运算仅在补背景边界的分割副本上进行，防止将贴近搜索边界的完整扇区扩展到边界；完整性依据前景是否实际接触搜索边界判断，不额外要求固定留白。接触边界的扇区仍按可能截断拒绝，补边像素不参与扇区验证、SFR 或灰度统计。候选轮廓的轴线在最长边不超过 256 px 的抗混叠定位副本上提取，避免高分辨率下浅角边缘的阶梯和轻微弯曲分散霍夫投票；轴线按像素中心映射回原图，再执行原分辨率的扇区与外圈验证。过小、明显透视、遮挡或与文字／边框相连的靶标可能无法定位；不要将其作为任意形状匹配器。四边 ROI 避开中心交叉与圆弧，坐标为原图像素；定位灰度归一化和副本缩小不影响测量，SFR 直接读取原始像素。旧 `M_CalSFRBmw4In1` ABI 与行为保持不变。

`BmwSfrRoiSettings.ChartType` 支持 `Bmw`（默认）、`Checkerboard` 和 `Auto`。默认与旧配置保留 BMW 行为；新图卡使用 `M_LocateSfrTargetV1`，旧定位和 SFR 导出接口保持兼容。自动模式优先验证完整 BMW 形状，仅在未找到 BMW 时尝试棋盘格；多个 BMW 候选仍报歧义，不重新解释为棋盘格。`BmwTargetAnalysis.DetectedChartType` 记录实际识别类型，失败不猜类型。

棋盘格定位位于 `Native/opencv_helper/algorithm/sfr/sfr_checkerboard.cpp`，识别搜索框中心附近的黑白交替交叉点，无需输入整板行列数；一个外框对应一个交叉点及其四条边。交叉点两两距离外框中心近似相同时返回 `ambiguous_checkerboard_corners`，需移动或缩小外框。交叉点定位与四边可测性分别判断，不以每侧固定 100 像素作为定位门槛。小框生成于交叉点与下一交叉点/边界之间，返回原图 `SupportRoi`，仍要求沿边至少 32、跨边至少 40 个原图像素；某侧安全范围不足时，该侧返回 `checkerboard_insufficient_edge_support` 和空测量框，保留已定位中心及其他可测边，提示向该侧增大外部选框。手动尺寸、距离和独立复测越出该局部安全区域时保留 `checkerboard_roi_crosses_junction`，不会裁切、移动到相邻格子或填入数值。灰度归一化、缩小和角点采样仅用于定位，不进入 SFR 测量。图卡应在拍摄时绕光轴倾斜约 5°；即使交叉点已定位，斜边仍须通过现有 1°～15°、平台信噪比、拟合和采样检查，不通过数字旋转原图或放宽质量门限补足倾角。

每条可定位边调用现有 `SfrAnalyzer` / `M_AnalyzeSfrV2`，完全沿用 `SfrAnalysisOptions` 的编码、黑白电平和质量门限。彩色输入返回 RGB 与 L（亮度组合信号），灰度只返回 L，不伪造 RGB；完整 MTF/ESF/LSF、MTF50/10 和指定频率查询使用 `SfrAnalysisResult` / `SfrCurveQueries`。未知编码保留诊断警告，不宣称 ISO 或 Imatest 一致性。调用方持有 `HImage` 的有效租约直至同步调用结束；Core 不依赖 Engine、DAO 或相机 SDK。

V2 斜边定位采用逐行低通后的导数峰值及亚像素插值，再拟合直线，避免整行导数质心被远端平台纹理牵动。滤波只用于定位，MTF/ESF/LSF、平台信噪比和多边检查仍使用原始信号；不跨行平滑、不剔除异常行、不借用其他通道，不以缩小 ROI 或放宽质量门限换取有效值。弯曲、锯齿、低信噪比和边缘支撑不足仍会失败。结果以 `edgeLocalization=lowpass_peak_v1` 记录定位方法；读取缺少该字段的旧 V2 结果时，`SfrAnalysisResult.EdgeLocalization` 保留 `centroid` 标识，JSON 结构版本仍为 `2.0`。这是参考 Kerr（2026）低通边缘定位研究的工程实现，不等同于完整 ISO 或 Imatest 实现；旧 SFR 接口及其质心算法保持不变。

`SfrChromaticAberration.Analyze(SfrAnalysisResult?, RoiRect)` 接受同一 ROI 的 V2 SFR 结果，计算 R−G、R−B、G−B 的 50% 边缘位移，单位为输入像素。V2 的 ESF 横坐标已经按各通道拟合线居中并投影，因此先用斜率、截距与 ROI 中间行恢复公共坐标，不能直接相减居中后的交点。公共法线优先采用有效 G，其次 L 或其余有效通道；90° 旋转方向按 native 定义恢复原图 X/Y 方向。结果保留轴向差、法线差、法线向量和对齐 ESF。仅接受中心 ±12 px 内的唯一 50% 交点，缺失通道、无效拟合、方向冲突、多交点均返回空值及原因，并保留 SFR 质量警告。该量不等同于 ΔE、面积色差或纯镜头径向色差。真实 native 合成位移验证见 `Test/CameraTest.Tests/ChromaticAberrationTests.cs`；产品阈值和存档由调用插件负责。

ImageEditor 使用普通绘制矩形作为搜索外框：一个外框包含一个完整 BMW，或将所需棋盘格交叉点放在框中心，定位后生成左、上、右、下四个内部 SFR 测量框。矩形右键只提供 **四边 SFR…** 一个入口，调用后先弹出“图卡与测量框”配置，在其中选择 BMW、棋盘格交叉点或自动识别，并可调整框尺寸和中心距离。确认后分析调用时的原图快照；取消不计算、不修改原设置。同一编辑器下次调用沿用上次确认的配置，结果窗口的“测量框与显示”仍可调整这些设置。矩形上执行时处理该框，右键命中多选中的矩形时处理选中组；图像空白处的 **算法调用 → 四边 SFR…** 使用相同流程，优先处理选中的矩形，无选中项时处理全部已画矩形。矩形在同一编辑器内重复执行、移动或单框执行仍保留 ID；越界外框保留 INVALID，不裁切后计算。旋转矩形以原图轴对齐包围框作为搜索范围。

主图显示四个内部测量框及各边的 MTF50（cy/px）。结果窗口的 **回显通道** 可选择 L、R、G、B，默认 L；切换后同步更新这组主图标注、汇总表、当前边的大号指标及预览拟合线，并供同一编辑器后续执行沿用，无需重新计算。选定通道无效、缺失或未交叉时保留原因或空值，不用其它通道代替。移动、删除外框或源图像变化后清除旧叠加；独立结果窗口继续查看其持有的固定快照。窗口汇总所有目标与边，选择一边即可查看原像素裁图、L/R/G/B 的 MTF50/10、指定频率响应、MTF/ESF/LSF 曲线及完整采样数据。曲线复选框独立控制各通道的显示，无效通道保留诊断行但不伪造曲线，部分通道失败不会隐藏其它有效通道。

主图处于绘图选择模式时，点击内部小矩形会优先选中该边并显示移动、缩放手柄，不会被已选中的搜索外框挡住；双击可打开或定位到该边的详情。小框使用临时选择句柄，不加入普通绘图矩形列表，也不作为下一次 BMW 的搜索外框。拖动或缩放限制在本目标外框内，调整时立即清除该边旧指标，松开后只重算被调整的边并同步主图与打开的详情窗口；源图或外框变化后释放句柄，过期计算不得回写。高 DPI 下句柄使用画布坐标，测量仍转换为原图像素。

通过窗口的 **调整当前 SFR 矩形…** 也可分别修改每一边的原图 X、Y、宽度和高度，内部框必须完整位于本目标外框内；确认后只重算该边并更新主图回显。**测量参数 / 重新分析** 使用当前四个内部框，因此同一结果窗口内的调整会保留；从主图重新执行 BMW 定位会按当前四边参数重新生成内部框。**测量框与显示** 将显示项和已有测量框参数集中在一个属性窗口；其中“四边测量框”设置四边共用的长度、宽度与中心距离，几何参数实际改变后重新定位及计算；默认全为 0，沿用自动大小和距离。“显示与指标”可设置固定屏幕字号、字号、点位名称、四边名称、刃边拟合虚线与显示指标。指标包括 MTF50（默认）、MTF10、指定频率 MTF（默认 0.25）及 MTF@0.5，频率响应为百分数。虚线来自当前通道的拟合结果并裁限在测量框内，未获得拟合时不绘制；默认显示数值和红色靶标中心十字；中心十字、原图中心坐标、测量框尺寸、框中心距靶标中心的距离分别受开关控制，定位失败不绘制中心。显示选项只刷新叠加层，不触发测量；取消窗口不提交测量框或显示参数。完整 JSON 保存调整后的 ROI、输入参数与全部结果，CSV 包含各通道指标及有效曲线采样。输入编码和质量门限在结果窗口设置，默认 Unknown 仅诊断；SNR、对比度、拟合残差等门限用于判断测量可靠性，不是产品合格判据，也不代表国标规定的统一 MTF50 下限。

原生验证入口为 `opencv_helper_test.exe --bmw-only`，V2 诊断与旧 SFR 回归为 `--sfr-only`，其中 `Test/opencv_helper_test/test_sfr_analysis.cpp` 以已知高斯传递函数验证带周期纹理的移框、变宽准确性，并验证曲线边缘和逐行抖动仍被拒绝；托管测试为 `BmwSfrAnalysisTests`、`BmwDrawingSelectionTests` 与 `BmwSfrUiTests`，真实 DLL 用例需 `COLORVISION_RUN_SFR_NATIVE_TESTS=1`。`Test/opencv_helper_test/verify_bmw_sfr.py` 接受显式原图 ROI，可用 `--dll` 指定待验证构建，输出原图和 DLL 哈希、参数、逐通道结果与叠图；原图不修改。合成用例或离线图验证不能替代真实交互、成像系统精度和现场验收。

## CUDA 选择与 Fusion

`ImageCompute.UseCuda` 的初始值来自 CUDA 驱动初始化与设备数量检查，上层配置可以覆盖它。这不是纯常量读取，也不校验 `opencv_cuda.dll` 的所有算法入口或本次输入。`Fusion` 根据该值直接选择 `OpenCVCuda.CM_Fusion` 或 `OpenCVMediaHelper.M_Fusion`；GPU 调用失败后没有自动 CPU 重试。

窗口和本地流程节点使用 `FileFusion` 公共文件执行器，提供输入预检、进程内串行调度、取消后的结果丢弃和冻结位图输出；Auto 对 2–4 张输入选择 CPU，强制 GPU 拒绝少于五张输入。旧 `ImageCompute.Fusion` 是保留的低层兼容入口，不提供这些门禁。完整输入限制、计时、显示与保存见[景深融合](./image-fusion.md)；GPU 失败后不会自动重试 CPU。

## 原生日志初始化

`InitializeWithResult()` 的 `enableLogs` 和 `enableNativeSink` 默认均为 false。仅订阅 `LogReceived` 不会初始化来源或启用捕获。初始化会尝试加载 helper，并接入已加载的 CUDA 来源；捕获已启用但 CUDA 尚未接入时，后续托管 CUDA 调用可尝试接入。

| 状态 | 能说明什么 |
| --- | --- |
| `IsInitialized` | 已执行初始化流程，不保证任一 DLL/日志 ABI 可用 |
| `IsEnabled` | 捕获是否启用，不能代替各来源状态 |
| `LastInitializationResult.HelperAvailable` / `CudaAvailable` | 本次记录的对应来源是否可用；一个来源可用不代表另一个可用 |
| `LastInitializationResult.Diagnostics` | 初始化/接入诊断，用于定位缺 DLL、导出或配置问题 |

CUDA 包装准备日志时隔离诊断异常，但实际算法调用仍有自己的失败路径。初始化日志可能加载 native DLL，不能把它当作纯元数据查询。

## 构建与运行依赖

Core 当前目标框架为 `net8.0-windows7.0;net10.0-windows7.0`，native 资产面向 Windows x64。`ColorVision.Core.csproj` 将 helper、CUDA 与列出的 OpenCV runtime 放入 `runtimes/win-x64/native` 并复制到输出；实际加载还取决于宿主输出和 DLL 依赖是否完整。

仓库构建优先复用符合路径选择条件的 `opencv_helper.dll`，缺失时可加入 C++ 项目引用，因此首次构建不能只假定需要 .NET SDK。当前 csproj 无条件声明 `opencv_cuda.dll` 打包输入：运行时不用 CUDA，不等于构建时可以缺少该文件。DLL 选择和工具链前提见 [native 集成](../../02-developer-guide/engine-development/opencv-integration.md)，NuGet 检查见[包构建与发布](./publishing.md)。

## 常见问题

| 现象 | 检查顺序 |
| --- | --- |
| `DllNotFoundException` | 对应 helper/CUDA DLL、它的运行依赖、宿主输出及 native 资产路径 |
| `EntryPointNotFoundException` | 托管声明的导出名、调用约定与实际 DLL 版本 |
| `BadImageFormatException` | 托管宿主、插件和 native DLL 的位数与文件有效性 |
| 黑屏、错色或行错位 | `HImage` 尺寸/通道/位深/stride、位图格式和复制入口，不先归因于算法 |
| 批处理后内存持续上涨 | 谁分配和释放输出、是否仍有帧租约，以及借用数据是否超出有效期 |
| CUDA Fusion 失败 | 部署依赖、驱动/设备和输入限制；`UseCuda=true` 不证明本次算法可用 |
| 原生日志未出现 | 捕获开关、来源可用性、日志等级及初始化诊断，不能只看 `IsInitialized` |

## 验证范围

相关测试位于 `Test/ColorVision.UI.Tests/`：

| 测试 | 覆盖范围 |
| --- | --- |
| `HImageAbiTests` | 托管声明的布局、大小和字段偏移，不能独自证明实际 native DLL 的 ABI |
| `HImageExtensionCopyTests`、`VideoFrameCopyTests` | 位图复制、行填充和格式等边界；后者不等于真实视频采集或解码验收 |
| `NativeLogBridgeTests` | 默认参数、导出前缀、回调解码与隔离；另含真实 helper 日志回传调用 |
| `LuminousAreaNativeInteropTests` | 声明检查及亮区 V2 集成；真实导出用例有 `COLORVISION_RUN_LUMINOUS_NATIVE_V2_TESTS=1` 门禁 |

运行前区分纯托管检查、加载真实 DLL 和实际设备验证。交付时还需核对包资产及上层实际打开、显示和释放结果；测试文件存在或托管构建成功，都不能证明相机、CUDA 设备和全部 native 导出已验证。
