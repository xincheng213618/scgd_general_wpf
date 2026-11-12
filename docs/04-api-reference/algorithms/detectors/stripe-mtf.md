---
knowledge_id: "algorithms.stripe-mtf"
knowledge_type: "topic"
status: "current"
summary: "ImageView 与 Flow 共用的本地条纹亮暗对比度：取样、四部定位、比例与百分数、输入限制及 MTF 2.0 结果兼容。"
aliases: ["条纹MTF", "StripeMtfAnalyzer", "ImageView MTF", "H/V条纹", "本地MTF", "LocalMtfNode", "MTF计算(V2)", "StripeMtf.TrimmedHistogram/1"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/Mtf", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/Mtf", "Engine/ColorVision.Engine/PropertyEditor/LocalMtfConfigurationEditor.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalMtfNode.cs", "Engine/ColorVision.Engine/Services/Devices/Algorithm/LocalMtf"]
test_paths: ["Test/ColorVision.UI.Tests/StripeMtfLocatorTests.cs"]
related: ["algorithms.arvr", "engine.results", "flow.node-extension"]
---

# 本地条纹 MTF

<!-- algorithm-help:start -->
## 本地条纹 MTF 测量原理 {#algorithm-principles}

本地 **MTF计算(V2)** 测量框内条纹的明暗对比度，公式版本为 `StripeMtf.TrimmedHistogram/1`。它回答同一条纹图案的亮暗信号相差多少；未从条纹周期反推空间频率，也不生成斜边 SFR 曲线或 MTF50/MTF10。

### 输入和测量区域 {#inputs}

Flow 的 `LocalMtfNode` 位于自定义节点 **MTF计算(V2)**。图像连接 `IN_IMG`，运行时布点结果连接 `IN_POI`，两个输入须属于同一批次且均到达。直接编辑 **算法参数**；参数 JSON 随流程保存，**结果名称**仅用于结果主表命名。没有运行时布点时，把 Start 接到 `IN_POI`，配置 **测量区域**，`0,0,0,0` 表示整图。优先借用方向变换完成的上游 RAW 内存帧，无内存帧时才读取历史图像。接受 8/16 位、1/3 通道以及有行步长的 RAW，不把 CIE 或显示伪彩图当作测量输入。POI 中心矩形和左上角矩形按原服务的单精度坐标及 `Convert.ToInt32` 规则转换；名称须非空且唯一，矩形完整位于图内。

单独 H、V 把每个输入矩形作为实际测量框；四部模式把每个输入矩形作为一整组图案的搜索框，再定位四个小框。输入区域数量为 1～256，区域名称须非空且唯一，宽高为正且完整位于原图内，像素数须能用 Int32 表示。算法只读取测量区域，不修改或重采样其测量像素。

### 亮暗取样与对比度 {#contrast}

<figure class="algorithm-figure">
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 760 245" role="img" aria-labelledby="mtf-contrast-title mtf-contrast-desc">
  <title id="mtf-contrast-title">条纹亮暗信号与对比度</title>
  <desc id="mtf-contrast-desc">一组理想条纹的亮信号为五万，暗信号为一万；按明暗两端取样后，对比度为三分之二，即百分之六十六点六七。</desc>
  <line x1="78" y1="185" x2="695" y2="185" stroke="currentColor" />
  <line x1="78" y1="185" x2="78" y2="25" stroke="currentColor" />
  <line x1="78" y1="50" x2="695" y2="50" stroke="#8b96a4" stroke-dasharray="5 5" />
  <line x1="78" y1="155" x2="695" y2="155" stroke="#8b96a4" stroke-dasharray="5 5" />
  <polyline points="90,50 170,50 170,155 250,155 250,50 330,50 330,155 410,155 410,50 490,50 490,155 570,155 570,50 650,50" fill="none" stroke="#327bd0" stroke-width="3" />
  <text x="68" y="55" text-anchor="end" fill="currentColor" font-size="15">50000</text>
  <text x="68" y="160" text-anchor="end" fill="currentColor" font-size="15">10000</text>
  <text x="22" y="22" fill="currentColor" font-size="15">灰度信号</text>
  <text x="620" y="211" fill="currentColor" font-size="15">测量框内样本</text>
  <text x="345" y="36" fill="currentColor" font-size="15">亮信号 H</text>
  <text x="345" y="175" fill="currentColor" font-size="15">暗信号 L</text>
  <text x="380" y="238" text-anchor="middle" fill="currentColor" font-size="15">示例 C = (50000 − 10000) / (50000 + 10000) = 0.6667</text>
</svg>
<figcaption>图 1 理想亮暗条纹示意。实际亮暗代表值由配置的去噪及取样方式确定。</figcaption>
</figure>

横条纹、竖条纹都在测量框内计算 `(亮−暗)/(亮+暗)`，方向决定图案标记，不改变对比度公式。彩色输入先按 BGR 转灰度。均值法按像素计数舍弃最暗/最亮比例，再各取指定比例求均值；极值法取去噪后的两端点。直方图保留边界灰度中所需的像素个数，不因同值像素过多而整段丢弃；只处理 ROI，并复用直方图缓冲。纯黑、越界或取样不足会失败，不补零；有亮度的常量图输出零。原模板的额外 `sensorRatio` 校正及启用的 `mathMaskRect` 不受支持，导入时明确拒绝。

统一公式为：

$$
C=\frac{H-L}{H+L}
$$

| 计算方式 | H 与 L 如何取得 | 适用解释 |
| --- | --- | --- |
| 明暗两端均值 `TailMean` | 对 ROI 灰度排序，先按像素数舍弃暗端和亮端噪声，再分别对配置比例的两端求均值 | 默认每端 10%，默认最亮、最暗各舍弃 1%；不是先求空间条纹平均曲线 |
| 去噪后最亮和最暗 `TrimmedExtrema` | 经过同样的两端舍弃后，各取一个端点灰度 | 对局部极端像素更敏感；不要求与未公开供应商算法逐值相同 |

每端实际样本数为 $\max(1,\lfloor N d\rfloor)$，其中 $N$ 为测量框像素数、$d$ 为 `TailRatio`；极值法每端只取一个。两端去噪数分别向下取整。均值法要求 $0<d\le0.5$ 且 $2d+w+b\le1$，两个去噪比例均须为有限的 $[0,0.5)$ 数。样本集合不能重叠。

### 四部定位和 H V 汇总 {#four-part}

`pattern=5` 为四部横竖条纹：定位平面进行 5×5 高斯平滑、CLAHE、阈值分割和 15×15 椭圆闭运算，仅在满足最小面积、不贴边且能容纳四个取样框的完整候选中选择最大轮廓，过滤邻近图案的边缘残片与孤立小目标。参考阈值未定位到完整目标时，自动回退到未增强定位平面的 Otsu 分割，适应信号变暗及背景增强导致的误判；8 位图使用超出 255 的参考值时直接自动估算，不要求换算默认阈值。两次定位都找不到完整目标时提示圈住整组图案并留出边距。正常定位继续使用原处理路径，不增加用户参数。按轮廓质心及偏移量，沿四个对角方向放置测量框，编号为左上、右上、右下、左下；`distanceToRect` 是小框中心到目标中心的距离。测量仍使用未增强的原始灰度。`firstIsHor` 决定左上/右下归入 H 或 V。定位阈值 5000 为优先尝试的默认参考值；默认运行可自动重试，阈值仅在高级设置中供调整。`PercentageDisplay` 决定输出比例或百分数，下游判定限须使用同一单位。此测量不是斜边 SFR 曲线，也不输出 MTF50/MTF10。

四个框编号按 **0 左上、1 右上、2 右下、3 左下**。`FirstIsHorizontal=true` 时 H 为 0、2 两框的均值，V 为 1、3；为 false 时两组交换。它是配置的分组约定，不能凭输出名称证明软件已自动识别条纹方向。

$$
H_{\mathrm{avg}}=\frac{C_{h_1}+C_{h_2}}{2},\qquad
V_{\mathrm{avg}}=\frac{C_{v_1}+C_{v_2}}{2},\qquad
C_{\mathrm{all}}=\frac{C_0+C_1+C_2+C_3}{4}
$$

### 关键参数和输出 {#parameters}

| 参数 | 默认值 | 含义 |
| --- | --- | --- |
| `Pattern` | `FourPart` | 单独 H、单独 V 或四部图案 |
| `Method` | `TailMean` | 两端均值或去噪后端点 |
| `TailRatio` | 0.1 | 均值法每端的取样比例 |
| `WhiteNoiseRatio` / `BlackNoiseRatio` | 0.01 / 0.01 | 按像素数舍弃最亮和最暗的比例 |
| `PercentageDisplay` | false | false 输出比例；true 乘 100 输出百分数 |
| `Threshold` / `MinimumArea` | 5000 / 100 | 四部定位的参考阈值和最小目标轮廓面积 |
| `DistanceToRect` | 70 px | 目标中心到每个小框中心的距离 |
| `RectWidth` / `RectHeight` | 60 / 60 px | 四部取样框的宽高 |
| `OffsetX` / `OffsetY` | 0 / 0 px | 定位质心后的中心偏移 |
| `FirstIsHorizontal` | false | 四部 H/V 对角分组约定 |

`result[].mtfValue` 是各框数值；四部结果另有 `horizontalAverage`、`verticalAverage`、`Average`。例如 H=50000、L=10000，比例结果为约 0.6667，百分数结果为约 66.67；下游判定限必须采用相同单位。`结果名称`只为结果主表命名，不查询算法模板。

### 使用与无效边界 {#limits}

**本地条纹 MTF** 使用共用的 `StripeMtfAnalyzer`，运行不依赖供应商 MTF DLL。ImageView 的 **分析测量 → 清晰度与频域 → 条纹 MTF（H / V / 四部）** 与矩形右键菜单都可打开参数编辑器；主菜单使用画面上的矩形，没有矩形时使用整图。单独 H、V 图像使用测量矩形，四部模式每个矩形圈住一整组图案。参数窗口默认只显示“图案”和“高级设置”开关，计算使用当前配置值；展开高级设置后才显示计算方式、去噪、取样及输出单位。四部模式额外显示“四部定位”组，单独 H/V 隐藏该组；极值法隐藏“两端取样比例”，均值法才显示。收起高级设置或切换图案均保留已填写的值；开关只控制展示，不写入算法配置。自动定位重试不修改已保存的参考阈值或小框尺寸。ImageView 与节点参数窗口遵循相同规则。显示坐标按 DPI 转换为原图像素，旋转矩形拒绝测量。结果窗列出各框数值，四部模式另列 H、V、整体均值；叠图属于临时算法图层，切图或新请求使旧结果失效。此入口不需要设备或数据库。

测量框应覆盖需要评价的亮暗条纹。四部搜索框必须包含完整图案与边距，定位失败、小框越界、ROI 太小、无有效亮度或取样参数不合法时停止，不生成成功测量。恒定的非零亮度图因 H=L 输出零；纯黑图因 H+L=0 失败。这些语义与“未找到图案也输出零”不同。

<!-- algorithm-help:end -->

## 流程结果与兼容

成功结果保持类型 `MTF`、版本 `2.0`、一条含 `ResultFileName` 的 `DetailCommon` 明细及既有 JSON 字段：`result` 保存各矩形，四部模式另有 `resultChild`、`childRects`、`Average`、`horizontalAverage`、`verticalAverage`。`ViewHandleMTF2` 和客户解析可沿用此结构。自研算法版本、实际参数、ROI 和内存/文件来源记录在主表参数中。**结构兼容不代表数值逐位相同**：定位预处理及直方图端点处理可能与旧 DLL 有差异；本地极值法也不能假定等同于未公开的供应商 `CalcMethod=1`。替换生产流程前，应使用相同原图和 ROI 对照数值并核对判定限。

结果目录默认位于当前用户 `LocalAppData/ColorVision/Results/MTF`。先写文件，再事务保存主表与明细；数据库失败回滚并清理本次文件，持久化完成后发布 `local-flow` 通知并向后续节点输出结果 ID。计算失败不产生成功记录；业务上下限仍由客户流程处理。没有与内存帧对应的已保存图片时，历史结果可能没有原图。原有远端模板算法入口见 [ARVR 模板与请求](../templates/arvr-template.md)。

## 验证与现场复核

`StripeMtfLocatorTests` 使用合成图验证四部定位对背景增强、位深、亮度和不合适参考阈值的处理，并检查缺角图案仍会失败、输入像素不会被修改。套件还保留节点模板映射和结果叠图等相邻契约，不包含供应商 DLL 对照或现场样本宿主。替换生产流程前应在获授权环境使用相同原图、ROI、参数和判定限对照自研算法与目标 DLL，并把读图、数据库、UI 和整套流程耗时分别记录。
