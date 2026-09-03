---
knowledge_id: "algorithms.image-profile"
knowledge_type: "reference"
status: "current"
summary: "灰度与颜色剖面的操作、采样/越界规则、2000行预览和完整JSON/CSV导出；多点入口受多边形选择器限制，MaximumSamples还受执行/字节预算限制，旧接口参数不同。"
aliases: ["灰度与颜色剖面", "水平剖面", "垂直剖面", "任意折线剖面", "切面图", "截面图", "剖面采样参数", "剖面导出", "剖面采样数据", "剖面曲线", "ImageProfile", "LineProfile", "SectionalDrawing", "ImageProfileAlgorithmProvider", "ImageProfileParameters", "ImageProfileEditorTool", "ImageProfileResultWindow", "ProfileDataExtractor", "ProfileData", "ProfileChartWindow", "ImageProfileInterpolation", "ImageProfileBoundaryMode", "SampleSpacingPixels", "IncludeLuminance", "IncludeAlpha", "ImageProfileParameters.MaximumSamples", "ImageProfileParameters.ClosePath", "ImageProfileParameters.BoundaryMode", "ImageProfileParameters.Interpolation", "profile_path_required", "profile_path_degenerate", "profile_path_point_limit_exceeded", "profile_sample_limit_exceeded", "profile_execution_sample_budget_exceeded", "profile_result_budget_exceeded", "profile_sample_out_of_bounds", "profile_no_samples"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/ImageProfileAlgorithmProvider.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageProfileParameters.cs", "UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs", "UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmInputFactory.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmImageInterop.cs", "UI/ColorVision.ImageEditor/Algorithms/AlgorithmResultExporter.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/ImageProfile", "UI/ColorVision.ImageEditor/TransientRoiSelectionSession.cs", "UI/ColorVision.ImageEditor/EditorToolFactory.cs", "UI/ColorVision.ImageEditor/Draw/Line/ProfileDataExtractor.cs", "UI/ColorVision.ImageEditor/Draw/Line/ProfileData.cs", "UI/ColorVision.ImageEditor/Draw/Line/ProfileChartWindow.xaml", "UI/ColorVision.ImageEditor/Draw/Line/ProfileChartWindow.xaml.cs", "UI/ColorVision.ImageEditor/Draw/Line/DVLineDVContextMenu.cs", "UI/ColorVision.ImageEditor/Draw/Polygon/DVPolygonDVContextMenu.cs", "UI/ColorVision.ImageEditor/BatchProcessing/BatchAlgorithmAnalysisProcessor.cs", "Engine/ColorVision.Engine/FlowProcessing/Algorithms/LocalFlowImageAlgorithmAdapter.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageProfileV1Tests.cs", "Test/ColorVision.UI.Tests/ProfileDataExtractorTests.cs", "Test/ColorVision.UI.Tests/TransientRoiSelectionSessionTests.cs"]
related: ["algorithms.platform", "algorithms.index", "algorithms.roi-statistics", "algorithms.image-comparison"]
---

# 灰度与颜色剖面：采样、曲线与数据导出

灰度与颜色剖面沿一条路径读取图像通道值，显示亮度变化，并输出每个采样点的位置、距离和数值状态。它支持水平、垂直和分段折线路径；矩形只用于定位水平/垂直线，不计算矩形区域平均值。区域统计见 [ROI 统计](./roi-statistics-v1.md)，两图差异见[图像比较](./image-comparison-v1.md)。

稳定算法 ID 为 `colorvision.analysis.image-profile`，算法版本 `1.1.0`、参数 schema 为 1。Catalog 别名包括 `ImageProfile`、`LineProfile`、`ProfileDataExtractor`、`SectionalDrawing`。Provider 只接收一张图和 `PolylineAlgorithmRoi`，输出结构化结果，不生成图像 artifact。

## 在 ImageView 中取得剖面

1. 打开图像，在图像区域菜单选择“算法调用 → 灰度与颜色剖面”。
2. 选择“水平剖面…”“垂直剖面…”或“任意折线剖面…”。在“剖面采样参数”窗口设置间距、插值、越界规则和通道，提交后开始选择路径；关闭参数窗口而不提交则结束。
3. 根据入口完成选择：

   | 入口 | 选择方式与实际路径 |
   | --- | --- |
   | 水平剖面 | 拖出矩形后松开，以矩形中心的 Y 坐标采样整幅图的 `x=0..宽-1`，不受矩形左右边界限制 |
   | 垂直剖面 | 拖出矩形后松开，以矩形中心的 X 坐标采样整幅图的 `y=0..高-1`，不受矩形上下边界限制 |
   | 任意折线剖面 | 逐次单击添加点，按 Enter/Space 或右键尝试结束，Esc 取消；最终是否闭合由 `ClosePath` 决定 |

4. 等待分析完成；进度窗口可以取消。结果窗口显示曲线、样本表和路径长度，图像上出现本次路径 overlay。分析不修改原图；关闭结果窗口会释放结果并移除该临时 overlay。

水平/垂直中心坐标可落在亚像素位置，读数由插值参数决定。矩形选择器要求显示坐标的宽和高都大于 1 DIP，过小选择会等待重新绘制。

**任意折线入口有选择器限制。** 当前复用 `TransientRoiSelectionSession` 的多边形模式，必须至少三点、闭合投影面积非零、相邻点不重复且不自交，即使 `ClosePath=false` 也执行这项检查。两点线和共线点无法在这个入口完成选择；需要局部直线时，可使用已有线图元右键“切面图”，或由调用方直接提供两点 Polyline ROI。底层算法支持两点路径，选择器限制是当前界面与算法之间的差异。

已有直线和多边形图元的“切面图”（资源键 `SectionalDrawing`，也常称截面图）进入同一参数窗口；直线默认不闭合，多边形按 `IsComple` 初始化闭合选项。工厂创建时注入当前图像/绘图上下文，旧构造方式的区别见兼容接口小节。

选择与执行通过图像 document/revision 绑定；切图、原图 revision 改变或新分析会按[平台 session 规则](./image-algorithm-platform-v1.md#m0-执行与所有权规则)失效旧请求，不能把旧路径直接用于新图。

## 读懂曲线与样本表

曲线 X 轴是 `Distance (px)`，表示沿原路径累计的像素距离；Y 轴为采样值。彩色图显示 B/G/R，可选 A 和 Luminance，灰度图显示 Gray。Luminance 使用 `0.114B + 0.587G + 0.299R`，不是经过色彩标定的物理亮度。

| 字段 | 含义 |
| --- | --- |
| `SampleIndex` | 实际返回行的连续序号，从 0 开始 |
| `RequestedIndex` | 原始请求采样序号；Skip 删除越界行后可以不连续 |
| `SegmentIndex` | 输入路径中的原始分段编号；忽略零长度段也不重新编号 |
| `DistancePixels` / `DistanceMillimetres` | 沿原路径的累计像素/mm 距离；毫米距离按各段 X/Y DPI 换算 |
| `XPixel` / `YPixel` | 实际取样坐标；Clamp 时是钳制后的坐标，距离字段仍沿原路径计算 |
| `Gray`、`B/G/R/A`、`Luminance` | 各曲线数值；浮点无效值写 null |
| 对应的 `*Status` | `Finite`、`NaN`、`+Infinity`、`-Infinity`，用于区分空数值的原因 |

窗口的表格和每条曲线最多预览 **2000 行/点**，超出时按行序均匀选取并保留首尾。摘要中的“采样点”是完整返回数，“界面预览”是显示数。预览不是峰值保留算法，可能漏掉窄尖峰或无效值所在行；被预览到的非有限值显示为曲线间断。检查完整数据时使用导出，不能把平滑预览当作所有采样点都正常的证据。

样本表的数字按 `G10` 显示，空数值单元格配合 Status 读取。Measurement 中的各曲线有限/无效数量、最小值、最大值、均值来自完整返回结果，不从预览点重新计算。

### 导出完整数据

点击“导出 JSON”或“导出 CSV”，选择新文件名。导出期间两个导出按钮禁用，并显示进度及“取消导出”；关闭结果窗口也会请求取消。

- **JSON** 保存算法 ID/版本、状态、诊断及全部五类结果 artifact，包括完整采样表、几何和参数来源。
- **CSV** 是四个文件。选择 `profile.csv` 时，主文件保存 Measurement，`profile_image-profile-samples.csv` 保存全部采样行，另有 `profile_image-profile-geometry.csv` 和 `profile_image-profile-provenance.csv`。最后一个文件的 `DataJson` 列保存来源信息。

统一导出使用 UTF-8 BOM，默认拒绝覆盖主文件或任何伴随目标。`AlgorithmResultExporter` 先写临时文件再提交；CSV 逐文件提交并在失败时尝试清理本次新建文件，不能保证异常清理始终成功。提交阶段不再检查取消，所以取消或关闭窗口不保证撤销已经提交的文件；以返回结果及实际文件内容核对是否完整。遇到“导出失败”时检查目标目录权限、已存在的同名/伴随文件，再选用新名称。

此结果窗口没有独立“保存曲线图片”按钮；旧 `ProfileChartWindow` 的图表保存不等于这里的完整数值导出。

## 参数与采样规则

`ImageProfileParameters` 使用以下默认值。间距必须有限，范围包含端点；路径点属于 `Invocation.Roi`，不存入参数对象。

| 参数 / 界面名称 | 默认值 | 范围与作用 |
| --- | --- | --- |
| `SampleSpacingPixels` / 采样间距 (px) | 1 | 0.01–1000000；沿路径累计像素距离采样 |
| `Interpolation` / 插值 | Bilinear | Nearest 或 Bilinear |
| `BoundaryMode` / 越界规则 | Reject | Reject、Clamp 或 Skip |
| `ClosePath` / 闭合路径 | false | 增加尾点到首点的线段，不在总长度处重复首点 |
| `IncludeLuminance` / 输出亮度曲线 | true | 彩色图增加 Rec.601 加权曲线；灰度图仍只有 Gray |
| `IncludeAlpha` / 输出 Alpha 曲线 | true | 四通道输入增加 A 曲线 |
| `MaximumSamples` / 最大采样点数 | 100000 | 2–1000000；这是请求数量上限，执行/字节预算还会进一步限制 |

输入路径可用 Pixel 或 Physical（毫米）坐标，后者按输入图像 X/Y DPI 转换为像素。核心格式支持 Gray、BGR、BGRA 的 8/16-bit 和 Float32；WPF 格式、调色板和 Alpha 的规范化由[平台输入契约](./image-algorithm-platform-v1.md#执行平面与兼容层)维护。

- 零长度分段被忽略，全部为零时返回 `profile_path_degenerate`。路径长度是各段欧氏长度之和，不是首尾直线距离。
- 开放路径按固定间距取样，末行强制使用精确尾点；闭合路径在 `[0,totalLength)` 取样。开放路径尾点去重采用 `1e-10 × max(1,路径长度)` 容差，极短非零路径可能只保留尾点，不能在该尺度上保证首尾各一行。
- 跨分段继续使用累计距离；采样点恰在连接处时归到前一个有效分段。间距按像素定义，DPI 非等向时毫米间距可能随路径方向变化。
- Nearest 对边界处理后的坐标取 `floor(value+0.5)`。Bilinear 逐通道对四邻域线性插值；即使是 8/16-bit 输入，插值结果也可以有小数，且不量化回整数。
- Float32 不隐式归一化；非有限结果以 value=null 和 Status 分类保留。恰好落在像素中心/边界时，插值端点分支保留该端点分类；内部插值仍遵循浮点运算，可能产生 NaN。

例如两点路径 `(0,0)→(2,0)`、间距 0.5、Bilinear，在 Gray 像素 `[10,20,30]` 上得到距离 `[0,0.5,1,1.5,2]` 和数值 `[10,15,20,25,30]`。使用同一条路径而改变间距，会改变请求数；不会只改变曲线画法。

### 越界规则

越界按连续坐标是否位于 `0..宽-1`、`0..高-1` 判断，在插值之前执行：

| 模式 | 行和坐标的处理 |
| --- | --- |
| Reject | 首个越界点返回 `profile_sample_out_of_bounds`，不返回之前已采到的部分表 |
| Clamp | 坐标限制到边界，保留请求行；记录 `profile_samples_clamped`，可能重复读取同一边界像素 |
| Skip | 删除越界行，保留剩余行的 RequestedIndex 和原路径距离；记录 `profile_samples_skipped`。全部被跳过则 `profile_no_samples` |

Geometry 保留原始路径转换后的 Pixel 点，不随 Clamp/Skip 裁剪。成功返回行也可能全部为非有限值；此时仍有完整 Status 和 invalid count，没有各曲线的有限 min/max/mean。

## 结果预算与失败检查

执行按顺序检查以下限制，超过任一门槛就拒绝，不自动稀疏算法结果：

| 检查 | 当前限制 | 失败代码 |
| --- | --- | --- |
| 输入路径点数 | 最多 4096 点 | `profile_path_point_limit_exceeded` |
| 计算出的请求数 | 不超过参数 MaximumSamples；整数计数也不能溢出 | `profile_sample_limit_exceeded` |
| 执行请求数 | 最多 50000 点 | `profile_execution_sample_budget_exceeded` |
| 预计 Table 内存 | 最多 64 MiB | `profile_result_budget_exceeded` |

预算按请求数计算，早于 Skip 丢弃越界行，也早于任何结果行分配；选择 Skip 不能降低预检数量。估算公式为 `请求行数 × (256 + 列数 × 256)` 字节，列数等于 `7 + 2×输出曲线数`。默认 Gray、BGR、BGRA 分别允许最多约 26214、16384、14563 行通过该字节门槛；它是估算值，不是进程内存上限。关闭可选 Alpha/Luminance 会减少列数；减小间距则增加请求数。

schema 1 继续接受 MaximumSamples=1000000，保留持久参数兼容；合法参数不保证对应请求能执行。若失败，先按错误代码检查路径点数、路径长度/间距、请求上限和输出曲线数，不应只把 MaximumSamples 调大。每 1024 个请求点检查取消并报告进度。

| 现象 | 检查顺序 |
| --- | --- |
| 框选松手或按完成键没有结果 | 矩形是否过小；多点选择是否达到三点、非零面积且不自交。失败的选择会继续等待，不代表 Provider 正在计算 |
| `profile_path_required` | 是否提供 PolylineAlgorithmRoi；Rectangle ROI 不能直接作为剖面路径 |
| 路径越界或没有样本 | 原路径坐标、输入尺寸/DPI及 BoundaryMode；不要把空数据当作全零图像 |
| 曲线只有 2000 点 | 查看摘要完整数量并导出采样 CSV；这是预览限制 |
| 图表距离和导出坐标看似不一致 | X 轴沿原路径累计距离，Clamp 坐标已改变；毫米距离还依赖输入 DPI |

## 调用、结构化结果与兼容接口

一次成功调用包含以下 artifact：

| artifact | 内容 |
| --- | --- |
| Measurement `image-profile` | 返回/请求/跳过/钳制数、像素/mm 路径长度、预计结果字节数，以及各曲线统计 |
| Table `image-profile-samples` | 全部返回行，字段见样本表说明 |
| Geometry `image-profile-geometry` | Pixel Polyline；ClosePath=true 时为 Polygon |
| Overlay `image-profile-overlay` | transient 路径显示 |
| StructuredData `image-profile-provenance` | schema `colorvision.analysis.image-profile/v1`，输入格式/DPI、原 Invocation ROI、参数、请求/返回数和采样规则 |

ImageView、Batch 与本地 Flow adapter 复用同一 Provider，但参数、格式和 DPI 必须一致才能比较结果：

- `BatchAlgorithmAnalysisProcessor` 复用保存的 Polyline Invocation，逐文件导出结构化结果；ROI 不随图片尺寸缩放。其 Mat 输入转换使用默认 96 DPI，不能假定保留文件物理标定；默认 `_analysis` 后缀、JSON 输出、拒绝覆盖，失败项可继续下一项，取消也不撤销已导出项。
- `LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 复制进程内 RAW 帧，不取得外层 frame lease 所有权；当前该输入桥接支持 8/16-bit、1/3/4 通道，默认 96 DPI。它是直接 API，尚未注册为生产 Flow 画布节点；生产接入边界见[平台 Flow 说明](./image-algorithm-platform-v1.md#flow-与发布适配)。
- ImageProfile 未进入 Copilot 白名单。Batch/Flow 能力不能推导出 Copilot 已有经审批的折线路径输入。

### 旧 ProfileDataExtractor 与图表窗口

`ProfileDataExtractor.ExtractAlongPath` 保留默认 `totalSteps=500` 和同步返回 `ProfileData` 的接口，内部调用同一 Runner，但使用兼容参数：间距为 `路径长度/(totalSteps-1)`，Nearest、Skip、IncludeAlpha=false、IncludeLuminance=true。它不使用新窗口的 Bilinear/Reject 默认行为，闭合或越界路径的实际数量也不保证等于 totalSteps。

点数不足、退化路径、无法映射的位图格式或 Runner 失败会返回空的单通道数据，调用方无法从该返回值取得原始诊断。默认 500 步下，1 px 路径的间距约 0.002，小于合法下限 0.01，因此可能得到空结果。需要明确控制参数和错误时使用 Invocation/Runner。

另有 Indexed8 兼容缺口：公共输入工厂将调色板展开为 BGRA，但旧 Extractor 的多通道判断不包含 Indexed8，成功分析后仍尝试读取不存在的 Gray 列，可能抛出异常。不能把新 ImageView 支持某种格式等同于旧返回模型也支持它。

工厂创建的线/多边形菜单优先使用上下文构造，进入 `ImageProfileResultWindow`；仅用 DrawCanvas 的旧菜单构造仍走 Extractor 和 `ProfileChartWindow`。旧窗口 X 轴为样本序号，提供 `Save Chart as Image...`、`Save Data as CSV...` 和通道显示开关；旧 CSV 只含序号与曲线值，按当前区域设置格式化，直接写目标并可覆盖文件，不包含统一结果的坐标、距离、Status 和诊断。

## 验证范围

`ImageProfileV1Tests` 检查采样/插值、分段与端点、DPI/物理坐标、颜色和非有限值、三种边界模式、预算、取消/释放、图表/overlay、3001 行有界预览及 2501 行完整 CSV，并用合成输入比较 Batch/Flow 结果。`ProfileDataExtractorTests` 覆盖旧接口的 Gray8、Bgr24、Rgb48、闭合和 Skip；未覆盖 Indexed8 返回模型和默认 500 步的极短路径。

`TransientRoiSelectionSessionTests` 明确测试两点、共线和自交多边形被拒绝；这验证共享选择器，不证明“任意折线”入口已支持这些路径。现有用例也不能替代完整鼠标流程、预览遗漏尖峰、导出关闭/提交竞态和极短路径容差的验证。交付门禁见[统一平台](./image-algorithm-platform-v1.md#m0-验收门禁)。
