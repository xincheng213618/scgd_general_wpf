---
knowledge_id: "algorithms.light-beads"
knowledge_type: "topic"
status: "current"
summary: "灯珠亮点定位、完整规则网格与不亮偏暗判定，覆盖运行内存 POI、可选 CSV 及旧 native 暗区候选的限制。"
aliases: ["灯珠不良", "坏点坐标", "LightBeadsInspector", "LocalLightBeadsNode", "DensePointVisual", "FindLightBeads", "M_FindLightBeads", "FindLightBeadsConfig", "BlackCenters", "MissingCount", "本地灯珠检测", "灯珠数量统计", "缺失数量", "灯珠定位算法原理", "偏暗比例", "标准行列数"]
code_paths: ["UI/ColorVision.Core/LightBeadsDetection.cs", "UI/ColorVision.Core/LightBeadsInspection.cs", "UI/ColorVision.ImageEditor/Draw/DensePointVisual.cs", "UI/ColorVision.ImageEditor/Draw/ImageDrawingPresentation.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/FindLightBeads/LightBeadsResultWindow.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalLightBeadsNode.cs", "Engine/ColorVision.Engine/Services/POI/LocalFlowPoiPointSet.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalPoiNode.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalRealPoiNode.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/FindLightBeads/FindLightBeadsCM.cs", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/FindLightBeads/README.md", "UI/ColorVision.ImageEditor/EditorTools/GraphicEditing/GraphicEditingWindow.xaml.cs", "Native/opencv_helper/algorithm.cpp", "Native/opencv_helper/opencv_media_export.cpp", "Native/include/opencv_media_export.h"]
test_paths: ["Test/ColorVision.UI.Tests/LocalLightBeadsTests.cs", "Test/ColorVision.UI.Tests/LightBeadsInspectionTests.cs"]
related: ["flow.node-extension", "algorithms.led", "ui.image-editor", "engine.native-integration"]
---

# 灯珠定位与完整网格判定

本主题维护 ImageEditor 灯珠分析和 Flow 灯珠定位的共同计算口径。共享定位器返回亮点，完整网格检验再判断每格状态；本地 `FindLightBeads` 与远端 `FindLED`/MQTT 模板具有不同结果和生命周期。

<!-- algorithm-help:start -->

## 灯珠定位算法原理 {#algorithm-principles}

灯珠定位分为两层：先从阈值图找到合格亮轮廓，再在开启不良检测时，以这些亮点建立完整规则网格，对每一个网格位置重新采样。不亮灯珠可能没有亮轮廓，因此不能只从检出的亮点数量推算完整坏点坐标。

<figure class="algorithm-figure">
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 660 300" role="img" aria-labelledby="lightbeads-principle-title lightbeads-principle-desc">
  <title id="lightbeads-principle-title">完整网格采样后区分正常不亮和偏暗灯珠</title>
  <desc id="lightbeads-principle-desc">左侧五列四行共二十格，第二行第三列不亮，第三行第四列偏暗。右侧以不亮阈值二十和参考亮度二百的一半作为界限，说明每格采样亮度的判定规则。</desc>
  <g font-family="Segoe UI, Microsoft YaHei, sans-serif" font-size="16" fill="#1f2937">
    <text x="140" y="26" text-anchor="middle">完整网格 4 行 × 5 列</text>
    <path d="M50 65 V210 M90 65 V210 M130 65 V210 M170 65 V210 M210 65 V210 M40 80 H220 M40 120 H220 M40 160 H220 M40 200 H220" fill="none" stroke="#cbd5e1" stroke-dasharray="3 4" />
    <g fill="#2563eb"><circle cx="50" cy="80" r="7" /><circle cx="90" cy="80" r="7" /><circle cx="130" cy="80" r="7" /><circle cx="170" cy="80" r="7" /><circle cx="210" cy="80" r="7" /><circle cx="50" cy="120" r="7" /><circle cx="90" cy="120" r="7" /><circle cx="170" cy="120" r="7" /><circle cx="210" cy="120" r="7" /><circle cx="50" cy="160" r="7" /><circle cx="90" cy="160" r="7" /><circle cx="130" cy="160" r="7" /><circle cx="210" cy="160" r="7" /><circle cx="50" cy="200" r="7" /><circle cx="90" cy="200" r="7" /><circle cx="130" cy="200" r="7" /><circle cx="170" cy="200" r="7" /><circle cx="210" cy="200" r="7" /></g>
    <path d="M123 113 L137 127 M137 113 L123 127" stroke="#dc2626" stroke-width="3" />
    <circle cx="170" cy="160" r="7" fill="#d97706" />
    <text x="140" y="240" text-anchor="middle">R2C3 不亮   R3C4 偏暗</text>
    <text x="470" y="26" text-anchor="middle">参考亮度 200   偏暗比例 0.5</text>
    <text x="314" y="65">采样亮度 B</text>
    <rect x="315" y="78" width="24" height="24" fill="#334155" /><rect x="339" y="78" width="96" height="24" fill="#f59e0b" /><rect x="435" y="78" width="186" height="24" fill="#60a5fa" />
    <text x="315" y="123" text-anchor="middle">0</text><text x="339" y="123" text-anchor="middle">20</text><text x="435" y="123" text-anchor="middle">100</text><text x="621" y="123" text-anchor="middle">255</text>
    <text x="315" y="158">B ≤ 20  不亮 Dark</text>
    <text x="315" y="187">20 &lt; B &lt; 100  偏暗 Dim</text>
    <text x="315" y="216">B ≥ 100  正常 Normal</text>
    <text x="330" y="279" text-anchor="middle">示例 20 格 = 18 正常 + 1 不亮 + 1 偏暗</text>
  </g>
</svg>
<figcaption>图中数值为 Gray8 量程示例。边界无法覆盖或采样非有限时为 Unknown，不计为正常。</figcaption>
</figure>

### 定位与采样步骤 {#light-beads-processing}

1. 从上游相机、校正或“加载图片”取得图像，要求方向变换已经完成。没有内存帧时才按输入图像结果加载文件。
2. 在有效搜索区域内转为 Gray8、按阈值二值化，经 2×2 椭圆腐蚀、4×4 椭圆膨胀、2×2 椭圆腐蚀后，提取外轮廓。亮点初始中心是整数包围框中心，不是强度质心或亚像素中心。
3. 开启不良检测时，使用亮点拟合规则网格，再在每格约四分之一灯距的局部范围内搜索最高 3×3 平均亮度。亮点使用局部峰值坐标，不亮点保留拟合网格坐标。
4. 按本图亮度参考给出状态和完整网格点集，可直接交给后续本地 POI；选择结果目录时另写全点与不良 CSV。

### 节点参数 {#light-beads-options}

| 参数 | 节点默认值与范围 | 作用 |
| --- | --- | --- |
| `Threshold` | 20；0～255 | Gray8 亮点定位阈值，同时作为不良检验的不亮阈值数值 |
| `MinSize` / `MaxSize` | 2 / 0；非负，非零上限须大于下限 | 亮轮廓包围框的宽和高均严格大于下限、严格小于非零上限；0 上限不限 |
| `SearchRegion` | 全零为整图 | 非空 ROI 须完整位于图像内，坐标统一返回原图像素 |
| `InspectDefects` | true | 开启完整网格与不良检验；关闭时仅输出检出的亮点 |
| `ExpectedRows/Columns` | 0 / 0 | 0 自动估计可见网格；正数校验识别跨度，不用于猜测全暗边界 |
| `DimRatio` | 0.5；大于 0 且小于 1 | 同图有效亮点中位亮度的偏暗比例 |
| `PointShape` | Circle | 下游 POI 测量形状，可选 Point、Circle、Rectangle |
| `PoiWidth/PoiHeight` | 3 / 3 px；至少 1 的有限值 | 圆的直径或矩形测量宽高，与检测轮廓尺寸不是同一个参数 |
| `ResultDirectory` | 留空 | 非空时须为绝对路径；开启检验时导出全点和不良 CSV，留空只保留运行内存 |

### 完整网格与不亮和偏暗判定

`LightBeadsInspector` 从亮点估计相邻的横向、纵向间距，用周期相位和稳健的仿射配准建立近水平/垂直的规则二维网格，剔除网格外、重复或不匹配的亮点。不再把旧 native 的 `BlackCenters` 或 `MissingCount` 当作完整坏点清单。极少亮点、非规则排布、过大旋转或低配准质量会拒绝判断，不静默给出“无坏点”。

每个网格位置均输出从 1 开始的行、列、绝对像素 X/Y、状态、采样亮度和原始亮点是否检出。每格在约四分之一灯距的局部范围，用小窗口积分和查找最高的 3×3 平均亮度，降低形态学轮廓中心偏移和单热像素的影响；亮点使用局部峰值坐标，不亮点保留拟合网格坐标。8/16 位输入采用 Gray8 归一化采样，32 位 Y 平面保留实际浮点量程。

默认不亮阈值沿用检测阈值 20；低于或等于此值为 `Dark`，高于该阈值但低于本图有效亮点中位亮度的 50% 为 `Dim`，其它有效点为 `Normal`。参考亮度与偏暗阈值随结果给出，比例可以配置。非有限采样或无法覆盖的图像边界标为未确定，不计入正常点。这是当前图像的相对不良候选，不判断偏色、偏亮或产品 PASS/FAIL；大面积共同变暗、明显渐变和输入量程仍需按实际采集条件核对。

自动行列数以当前可见的有效网格边界估计；整行、整列或边缘全暗无法仅靠一张亮点图保证总数。用户可输入已知标准行列数校验，观测跨度不足或不符时失败，不猜测缺失边界的位置。Flow 默认输出完整网格点（含不良点），供后续本地 POI 测量；填写节点“本地结果目录”会生成全点和不良点 CSV，不写模板或结果数据库。留空仅保留运行期数据，结束后释放。

### Flow 内存点集与本地测量

“本地算法 → 灯珠定位”（`LocalLightBeadsNode`）读取上游相机、校正或“加载图片”的内存帧；没有内存帧时才按输入图像结果加载文件。参数随节点保存，不创建灯珠算法模板或 POI 模板，不写逐点数据库明细。RAW 按原始 8/16 位 BGR/灰度布局借用；CIE 按主缓冲区借用 Y 平面，不把平面 XYZ 当成交错彩色数据。阈值沿用 native 的 Gray8 量程，浮点 CIE 的物理亮度仍需按实际数据设置。

输出是本次运行拥有的 POI 点集，流程消息只传唯一句柄和数量摘要。点集随运行结束或重复轮次回收；跨运行、过期句柄和图像尺寸/镜像方向不一致会被拒绝，不回退查询数据库。圆、矩形或点形 POI 的尺寸是测量区域，和检测轮廓的最小/最大尺寸分别配置。

单输入链为 `校正图像 → 灯珠定位 → 本地 POI`，后者无需选择 POI 模板；双输入链把定位结果接“本地实时 POI”的 `IN_POI`，相同尺寸、方向的校正图像接 `IN_CIE`。原“关注点算法”和“实时关注点算法”直接连接该定位节点时也使用本地分支。灯珠点集对应的测量结果继续保存在运行内存，不写 POI 结果主从表，也不把完整点数组复制到节点诊断 JSON；可选“本地结果目录”将测量值导出 CSV。普通已保存 POI 模板与旧数据库布点结果保留原持久化行为。历史查询和仅接受持久化 MasterId 的服务端分析不消费这些运行期句柄。

### 判定公式与结果读数 {#light-beads-metrics}

用网格原点 $o$、列方向向量 $v_c$、行方向向量 $v_r$ 表示从 1 开始的行列位置：

$$
p_{r,c}=o+(c-1)v_c+(r-1)v_r
$$

令每格采样亮度为 $B$，不亮阈值为 $T$，已检出且亮度高于 $T$ 的有效亮点中位亮度为 $M$，则偏暗阈值为 $T_{\mathrm{dim}}=M\cdot\mathrm{DimRatio}$。采样有效时，先判断不亮，再判断偏暗：

$$
\mathrm{Status}(B)=\begin{cases}
\mathrm{Dark}, & B\le T \\
\mathrm{Dim}, & T<B<T_{\mathrm{dim}} \\
\mathrm{Normal}, & B>T,\quad B\ge T_{\mathrm{dim}}
\end{cases}
$$

非有限或无法覆盖的采样为 `Unknown`。例如 $T=20$、$M=200$、`DimRatio=0.5`，则偏暗阈值为 100：亮度 18 为 Dark，80 为 Dim，100 为 Normal。8/16 位输入按 Gray8 归一化采样；32 位 Y 平面保留实际浮点数值，同一阈值数值不能直接解释为跨量程的物理亮度标准。

| 输出 | 含义 |
| --- | --- |
| `PointSet` / `PointCount` | 本次运行的 POI 句柄与可测量点数；检验开启时包括 Dark、Dim，不包含 Unknown |
| `TotalCount` / `Rows` / `Columns` | 检验网格总格数与行列数；不等于仅检出亮点数量 |
| `NormalCount` / `BadCount` | 正常格数；不良格数为 Dark 与 Dim 之和，Unknown 不算正常也不算 BadCount |
| `DetectedCenterCount` / `UnmatchedCenterCount` | 原始亮点数与未被网格采用的亮点数，可用于检查噪声或误配 |
| `ReferenceBrightness` / `DimThreshold` | 本图参考中位亮度及实际偏暗阈值 |
| `AllPointsCsv` / `BadPointsCsv` | 可选文件路径；不良 CSV 同时保留 Unknown 异常项，不能仅凭行数推算 BadCount |
| `MemoryOnly` / `DatabasePersisted` | 未写 CSV 时仅在本次运行保留；此节点不写逐点结果数据库 |

关闭不良检测时，不生成完整网格或不良状态，相关摘要可为空，不应补成“零坏点”。旧 native 的 `BlackCenters` 是暗区候选，`MissingCount` 是期望数量减亮点数；二者都不是当前完整网格检验的坏点清单。

### 错误与适用边界 {#light-beads-limits}

运行需要 Windows/x64、匹配的 `opencv_helper.dll` 及依赖；加载或调用失败不构成有效的空点集，也不能据此报告“无坏点”。

建立完整网格至少需要 9 个有效亮点，并需要足够的横纵间距证据和亮度参考。ROI 越界、行列校验不符、非规则排布、过大旋转、过少亮点或配准质量不足时拒绝判断，不会输出“没有坏点”掩盖失败。

相对阈值不能独立发现整屏共同变暗、偏色或真实产品 PASS/FAIL；明显亮度渐变、热像素、漏光与采集量程都应通过实际样本核对。同步 native 调用不能在中途强制取消，停止后的结果不再交接；本地 POI 句柄结束后失效，不能交给只接受历史 MasterId 的服务端分析。

<!-- algorithm-help:end -->

## 执行灯珠检测

1. 在图像右键菜单选择“分析测量 → 定位与几何 → 灯珠定位...”，分析全图；只分析局部时，绘制矩形并右键该矩形，选择“灯珠定位...”。矩形与图像无有效交集时不提供该命令。
2. 在参数窗口调整阈值、尺寸和行列数，点击“确定”。参数窗口使用临时配置；关闭窗口而未提交不会发起检测，参数不在此保存。
3. 默认执行不亮/偏暗检测，显示总数、正常数、不亮/偏暗数和逐点表格；正常点为绿色，不良点为红色。表格支持只看不良/异常点，并分别导出全部或不良 CSV。关闭“不良检测”时仅显示亮点定位和旧 native 暗区候选。需要输出标注时，等 `ExecuteAsync` 完成后使用[图像编辑器输出](../../ui-components/ColorVision.ImageEditor.md#保存前分清输出语义)；检测不会改写源图。

`FindLightBeadsCM.cs` 的 `CMFindLightBeads` 提供全图命令（父项 `AnalysisLocalization` 即“定位与几何”），`DVCMFindLightBeads` 提供 `IRectangle` 右键命令；两者在属性窗口 `Submitted` 事件中调用 `FindLightBeads.ExecuteAsync`。

发现由 `EditorToolFactory.cs` 扫描其实际程序集集合并构造上下文实例。这两个菜单不实现 `IAlgorithmCatalogBoundMenu`，因此 `IsAlgorithmMenuExecutable` 不对它们执行统一 Runtime 的 provider 检查；实际发现集合仍限制哪些类可以显示。

ImageEditor 通过 `ProjectReference` 引用 `UI/ColorVision.Core`。`OpenCVMediaHelper.M_FindLightBeads` 以 Cdecl 调用 `opencv_helper.dll`；导出声明在 `Native/include/opencv_media_export.h`，实现位于 `Native/opencv_helper/opencv_media_export.cpp` 和 `Native/opencv_helper/algorithm.cpp`。

## 灯珠参数

参数类位于 `EditorTools/GraphicEditing/GraphicEditingWindow.xaml.cs`，当前新建实例的值如下。属性 setter 的限制不等于字段初值已经经过 setter，也不等于直接 native JSON 调用受到同样校验。

| 参数 | UI 新实例值与编辑限制 | native 或绘制含义 |
| --- | --- | --- |
| `Threshold` | `20`；setter 限制到 `0..255` | 转为 Gray8 后使用 `THRESH_BINARY` |
| `MinSize` | `2`；setter 最小为 `1` | 亮轮廓包围框的宽、高均须严格大于下限；直接 native 输入非正数时，亮点分支可忽略下限 |
| `MaxSize` | `0`；setter 最小为 `0` | 0 不限制上限，否则宽、高均须严格小于上限；旧 native 暗区分支的尺寸条件不同 |
| `Rows` / `Cols` | `0`；setter 最小为 `0` | 0 自动识别网格；正数校验已识别行列数，不匹配时拒绝给出完整坏点判断 |
| `Radius` | `20`；setter 没有范围限制 | 仅用于 UI 圆标注；native 不读取该字段，不是 native 的检测尺寸阈值 |
| `InspectDefects` / `DimRatio` | `true` / `0.5` | 按完整网格检测；偏暗比例须大于 0、小于 1，不是物理亮度校准系数 |

直接调用导出且 JSON 缺少字段时，native 的回退值为 `Threshold=20`、`MinSize=2`、`MaxSize=20`、`Rows=650`、`Cols=850`。UI 会序列化自身配置，不能把这些缺字段回退值当成 UI 默认值。这里没有 `MinSize < MaxSize` 或正行列数的统一业务校验。

## 灯珠输入、ROI 与像素布局

`ExecuteAsync` 从当前 `ViewBitmapSource` 经 `ImageAlgorithmInputFactory.Copy` 取得格式规范化快照，以图像 revision 拒绝过期结果。直接复制 WPF 源一次，不额外建立完整 HImage 缓存；后台只在 native 调用与采样期间固定该快照。RGB24/RGB48 交换为 BGR，调色板展开，预乘 Alpha 还原。这不是重新读取磁盘原始 TIFF，也不经过统一 Runner 的 provider 门禁。

- 矩形入口在生成右键菜单时，将矩形乘配置中的 `DpiX/DpiY ÷ 96` 并四舍五入到像素坐标，再与当时帧的图像边界求交；宽高非正或无交集时不添加菜单。保存下来的 ROI 到参数提交时不会再按新帧重新裁剪。
- native 只在 ROI 宽高为正且完整位于图内时裁剪；空 ROI、部分越界和完全越界都回退到全图，而不是自动求交或报 ROI 错误。全图菜单传默认零 ROI。
- native 坐标相对于实际使用的工作图。共享托管包装拒绝越界 ROI，并且只加一次有效 ROI 偏移；UI 和 Flow 消费绝对源像素坐标。直接绕过托管包装调用 native 的越界回退行为仍存在。

`HImage` 只有行列、通道数、位深、stride 和指针，不表达 RGB/BGR、调色板或预乘 Alpha。`HImageToMatView` 检查描述符和行跨度，并按给定 stride 创建 Mat 视图，不做颜色转换，也无法凭描述符证明外部指针实际拥有足够内存。

灯珠实现先将 16U 乘 `255/65535` 转为 8U；其他非 8U 深度直接 `convertTo`，没有把 `[0,1]` 浮点范围乘到 `0..255`。三、四通道按 `COLOR_BGR2GRAY` 转灰度，单通道直接使用；不能从通用 HImage 可表达更多通道推断灯珠算法支持它们。

灯珠 UI 复用统一输入工厂的像素布局规范化，但这不等于完整算法平台执行。native 灰度计算仍忽略 Alpha，32 位浮点亮点定位仍使用原导出的转换约定；透明 RGB 和 `[0,1]` 浮点输入须核对实际量程。Flow 文件加载同样将 RGB48 转成 BGR48；共享布局转换测试不能替代实际屏幕的检测精度验证。

## 灯珠 JSON 结果与已知不完整性

亮点来自阈值图经 `2×2` 椭圆腐蚀、`4×4` 椭圆膨胀、`2×2` 椭圆腐蚀后的外轮廓。符合尺寸条件时，以整数包围框中心填入 `Centers`；不是强度质心或亚像素中心。

| 输出 | 当前含义 |
| --- | --- |
| `Centers` / `CenterCount` | 合格亮轮廓的包围框中心及数组长度；没有行列排序或逐格匹配保证 |
| `BlackCenters` / `BlackCenterCount` | 暗区启发式候选点及数组长度，不是完整、逐格核验的缺失灯珠清单 |
| `ExpectedCount` | `size_t(Rows) * size_t(Cols)`；负数没有先被拒绝，不能将非正行列数产生的无符号运算结果视为有效预期数量 |
| `MissingCount` | `ExpectedCount > CenterCount ? ExpectedCount - CenterCount : 0`，不是 `BlackCenterCount`，也不是实测缺失数 |

暗区分支先取已检亮点的凸包，将凸包外阈值图设为白色，再做 `12×12` 矩形膨胀、反相和外轮廓检测。若没有凸包，外部区域屏蔽步骤不执行。符合严格尺寸条件的暗轮廓产生一个包围框中心；不符合时，只有凸包非空且 `Rows/Cols` 都为正才按凸包包围框宽/`Cols`、高/`Rows` 生成网格候选，起点偏移为 `4`，并用 `pointPolygonTest` 过滤。此处不是拿 `Centers` 与一套已配准的完整灯珠网格逐一相减。

`algorithm.cpp` 在遍历暗轮廓的循环体末尾就 `return 0`，因此只处理第一条暗轮廓，之后的暗轮廓被跳过。即使 native 成功返回 JSON，`BlackCenters` 也不保证覆盖所有暗区；这项源码可见限制不等于经过真实样本验证。

## 灯珠完成、叠加与失败边界

`ExecuteAsync` 返回可等待任务，租约覆盖整个后台检测和结果发布。`LightBeadsDetector` 负责 native 调用、UTF-8 JSON 读取、结果释放、坐标校验和排序；ROI 必须完整位于图像内，输出统一为绝对源像素坐标。后台调用 `M_FindLightBeads` 本身是同步的，返回正数表示 JSON 字符串分配成功，数值是含结束符的缓冲区长度，不是灯珠数量，也不是异步任务 ID。

native 在调用期间借用帧，托管 `using (lease)` 在调用结束或异常时释放租约。UI 发布前检查当前图像 revision：过期结果不绘制；同一 revision 多次执行没有统一 Runner 的 invocation/token 排他与取消机制，仍有效的结果会分别追加，不能宣称 latest-wins。调用不可在 native 中途取消，但停止后的 Flow 结果不再交接。

`DensePointVisual` 用一个批量图层和一次撤销记录承载整批点，避免每点一个编辑对象、事件通知和历史记录。检测完成时按源图位置分块，冻结圆轮廓并生成显示用的静态位图缓存；缩放和平移只变换已有图块，不重建点集合、圆轮廓或缓存，也不为此启动缩放刷新定时器。半径和线宽按源像素定义，随图像一起缩放。过大半径的图块保留静态矢量显示以限制位图分配；后台 rendered 保存使用完整矢量轮廓，全部坐标始终保留。此图层不支持逐圆编辑，仍可整批撤销、重做、清空及保存 rendered 图像；不会截获图像的鼠标命中。

这条链不创建中立 algorithm artifact，不调用 `AlgorithmOverlayManager`，不在执行前清理既有标注，也不提交源像素或自动保存文件。保存 source 与 rendered 的差别见[图像编辑器输出](../../ui-components/ColorVision.ImageEditor.md)，不能把画布上的圆等同于源图已改写或业务结果已落库。

导出先将输出指针置空。无效图像/空参数返回 `-1`，内部算法失败为 `-2`，分配失败为 `-3`，JSON 格式/类型异常为 `-4`，OpenCV、标准和其他 native 异常分别映射为 `-5/-6/-7`。UI 捕获整个异步任务的异常，只在源图仍有效时显示错误。

## 验证依据与缺口

`LightBeadsInspectionTests` 覆盖完整网格、不亮与偏暗、分数灯距、中心偏移、浮点亮度量程和整行缺失时的标准行列校验。`LocalLightBeadsTests` 覆盖运行期句柄隔离与过期、本地 POI/实时 POI 的真实采样与输入分支、离线流程、CSV 输出及静态批量图层的缩放不重建、一次撤销、跨图块显示与后台快照。这些用例不证明所有屏幕的检测精度或完整 UI 菜单已验收；多 ROI、同 revision 并发、换图丢弃、RGB/调色板/Alpha 和真实相机输入仍需对应样本核对。本说明不授权启动设备、发布 DLL 或改动用户图像。

远端灯条与灯珠契约见 [LED 检测模板](../templates/led-detection.md)。
