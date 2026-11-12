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