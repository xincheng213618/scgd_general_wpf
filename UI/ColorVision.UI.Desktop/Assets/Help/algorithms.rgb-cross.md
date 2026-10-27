## 十字 RGB 分离测量原理 {#algorithm-principles}

**十字 RGB 分离**对同一个亮十字分别测量 R、G、B 通道的原分辨率边缘，以 G 为参考，给出 R−G 和 B−G 两组偏移。输出单位是原图像素 px。它测量通道图案在相机图像中的套色差异，不能直接换算成波导出光角或标定色差。

### 输入和搜索区域 {#inputs}

输入必须为彩色 BGR/BGRA 8、16 或 32F 图像；灰度图无法分离三个通道。图像至少 32×32，单帧最多 67,108,864 像素和 512 MiB，每个目标原图 ROI 最多 8,388,608 像素。浮点通道须为有限 $[0,1]$ 数，Alpha 若存在必须完全不透明。亮十字应近水平和垂直，并与其他目标分开。

搜索矩形至少为 32×32 像素且必须完整位于原图内；框选坐标按当前帧 DPI 换算为像素，左上取 floor、右下取 ceil。只读取搜索区域内的信号，框外反光及其他目标不参与定位。输出 ROI、边缘、轴和叠图仍是原图坐标，定位预览通过 `sourceOriginX/Y` 和 `sourcePixelsPerPreviewPixel` 标明偏移与倍率。缩小搜索区域会改变定位采样倍率，因此候选包络与边缘公共采样位置可能变化；原分辨率边缘测量方式不变。框选不裁改原图，输入快照及尺寸预算仍按整帧计算。

节点图像优先取上游内存帧，没有内存帧时读取 IN 图像结果的文件；本地图片通过“加载图片”输入。搜索区域优先使用发光区关注点模板，其次固定矩形，最后整图。行列按实际图卡设置，默认 3×3，单十字设 1×1，各维支持 1～16；与畸变节点的奇数行列限制不同。

### 定位和原图边缘测量 {#measurement}

十字 RGB 分离模式（算法版本 `1.6.0`）先对各颜色通道分别最大值池化，再用通道并集定位搜索区域内候选，不依赖整幅图九等分。定位背景使用中位数，噪声使用 MAD，先在 `背景 + max(MinimumContrast, 6×MAD)` 处寻找发光轮廓，再按每个候选自身峰值提取包络；紧凑实心噪点通过行列投影筛除。亮目标不会以全图统一峰值阈值截断较暗目标。候选至少包含 5 个采样像素且宽高均不少于 3 个采样格；细于此尺度的图案不在当前检测能力内。候选根据互不重叠的行/列包络和配置行列数分组，按从上到下、从左到右编号 P1–PN。某个交点缺失时保留该点为空；无法确定配置的行列布局时拒绝整阵列索引，不猜测缺失行的位置。多目标、额外候选、排序歧义、ROI 与其他目标相交以及触边均有独立原因。

每个目标 ROI 内分别读取原始 R/G/B 信号。ROI 背景使用中位数，行列强度投影用于窄轴带初筛；测量在各半臂的公共采样位置进行，取候选跨度的 10%–25% 和 75%–90%，避开端点衰减及交叉中心。每个截面以中位数估计背景，信号跨度须超过 `max(6×MAD, 2 个量化步长)`，避免亮中心通过 `MinimumContrast` 后仍用固定满量程门槛拒绝较暗外臂；float32 的步长底值为 1e-6。随后按自身背景到峰值的比例阈值线性插值边缘，避免强横臂的峰值截掉弱竖臂。对应边缘为可测截面的中位数，轴位置为两侧中位边缘的中点。这是近水平/垂直十字的采样带代表值，不是旋转直线拟合或逐像素最大色边；明显倾斜、弯曲、宽条纹及行列包络重叠不承诺正确索引/测量。

<figure class="algorithm-figure">
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 760 280" role="img" aria-labelledby="rgb-cross-title rgb-cross-desc">
  <title id="rgb-cross-title">以绿色通道为参考的十字边缘偏移</title>
  <desc id="rgb-cross-desc">左图为三通道十字平移示意，右图是竖臂横截面的左右边缘。绿色边缘五十和七十，红色五十二和七十二，蓝色四十七和六十七，因此红绿最大边缘偏移二像素，蓝绿三像素。</desc>
  <g fill="none" stroke-width="2">
    <path d="M137 35 H157 V112 H234 V132 H157 V209 H137 V132 H60 V112 H137 Z" stroke="#387dcc" />
    <path d="M155 35 H175 V112 H252 V132 H175 V209 H155 V132 H78 V112 H155 Z" stroke="#188759" />
    <path d="M167 35 H187 V112 H264 V132 H187 V209 H167 V132 H90 V112 H167 Z" stroke="#cb4d58" />
  </g>
  <text x="165" y="239" text-anchor="middle" fill="currentColor" font-size="15">横向平移示例  上下边偏移为 0</text>
  <text x="450" y="26" fill="currentColor" font-size="15">竖臂横截面的两侧边缘</text>
  <rect x="450" y="55" width="120" height="28" fill="#188759" fill-opacity=".15" stroke="#188759" />
  <rect x="462" y="112" width="120" height="28" fill="#cb4d58" fill-opacity=".15" stroke="#cb4d58" />
  <rect x="432" y="169" width="120" height="28" fill="#387dcc" fill-opacity=".15" stroke="#387dcc" />
  <text x="405" y="75" fill="currentColor" font-size="15">G</text>
  <text x="405" y="132" fill="currentColor" font-size="15">R</text>
  <text x="405" y="189" fill="currentColor" font-size="15">B</text>
  <text x="450" y="48" text-anchor="middle" fill="currentColor" font-size="14">50</text>
  <text x="570" y="48" text-anchor="middle" fill="currentColor" font-size="14">70</text>
  <text x="462" y="105" text-anchor="middle" fill="currentColor" font-size="14">52</text>
  <text x="582" y="105" text-anchor="middle" fill="currentColor" font-size="14">72</text>
  <text x="432" y="162" text-anchor="middle" fill="currentColor" font-size="14">47</text>
  <text x="552" y="162" text-anchor="middle" fill="currentColor" font-size="14">67</text>
  <text x="610" y="132" fill="currentColor" font-size="15">R−G = +2 px</text>
  <text x="610" y="189" fill="currentColor" font-size="15">B−G = −3 px</text>
  <text x="510" y="241" text-anchor="middle" fill="currentColor" font-size="15">边缘坐标为原图像素  示意不按 1:1 显示</text>
</svg>
<figcaption>图 1 对应边缘必须比较同一侧。竖臂决定左右边缘，横臂决定上下边缘。</figcaption>
</figure>

对通道 $c\in\{R,B\}$，四个带符号偏移统一为被测通道减 G：

$$
\Delta e_{c,s}=e_{c,s}-e_{G,s},\qquad
E_{c-G}=\max_{s\in\{l,r,t,b\}}\left|\Delta e_{c,s}\right|
$$

左右边缘的中点为竖轴 x，顶底边缘的中点为横轴 y；轴偏移是两个通道对应轴坐标之差。例图中 R 的左、右边缘均比 G 右移 2 px，上下边缘不变，因此 `rToGMaximumEdge_px=2`；B 对 G 的左右偏移为 −3 px，因此 `bToGMaximumEdge_px=3`。

### 指标与有效状态 {#metrics}

托管算法 1.6.0 按“找到十字且有可测边缘就输出数值”处理多峰与覆盖不足：同一截面存在多个前景阈值段时，取全部阈值段的最外侧交点，包括被深暗谷分开的同强度亮带，不再挑选主峰或因多峰而拒绝通道。阈值仍默认 50%，不平滑信号或补零；轴投影多段记录 `<通道>:multiple_axis_bands`，截面多段记录 `valid_multi_peak_envelope` 及 `<通道>:multiple_arm_bands` 警告。每条半臂低于配置的覆盖率（默认 0.5）时记录 `<通道>:partial_arm_coverage`，使用其余可测截面；横臂或竖臂完全没有可测截面时仍留空。多峰外包络描述检测到的亮带边界，不据此推断哪一个峰是物理主像。目标阈值和最小跨度用于检测，覆盖率用于质量提示，均不是产品合格阈值。

`RGB-cross-separation` 以 G 为参考，分别给出 `rToGMaximumEdge_px` 与 `bToGMaximumEdge_px`：对应左、右、上、下边缘偏移的绝对值最大值。原三通道极差字段保留原义用于兼容。该表同时保留每通道边缘/轴坐标、R−G/B−G 轴偏移、覆盖率、剔除数、饱和样本数及逐点原因。`RGB-cross-profile-quality` 可追溯采样位置、阈值段数、可测截面边缘与剔除原因。结果窗口使用“RGB 测量完整点数 / RGB 测量不完整点数”，后者包括只缺一个通道的点；“十字候选数”表示定位候选，不是杂散光数。缺失、低对比度、非十字形态、触边或没有可测横/竖臂边缘输出 `INVALID` 与空分离值；多峰和覆盖不足警告本身不使已有数值失效。可测通道仍保留自身坐标及叠图，不能把部分通道成功当作三通道完整。饱和样本给出警告：其阈值边缘可能有偏，不据此宣称测量精度。零只表示计算出的对应边缘重合；没有有效点时仅输出计数和诊断，不输出虚构的零最大值/RMS。

| 输出 | 含义 |
| --- | --- |
| `rToGMaximumEdge_px` / `bToGMaximumEdge_px` | 对应通道对四条边缘偏移绝对值的最大值；通道对可以独立有效 |
| 每通道的 `Left/Right/Top/Bottom` | 原分辨率代表边缘坐标，单位 px |
| `rMinusG_dx/dy` / `bMinusG_dx/dy` | 通道代表轴的带符号坐标差；x 向右、y 向下为正 |
| `maximumAxisSeparation_px` | 三通道竖轴 x 的极差与横轴 y 的极差中较大者 |
| `maximumEdgeSeparation_px` | 四组对应边缘中最大的三通道极差；它是保留的兼容口径，不能等同于 R−G 或 B−G |
| `rms_cross_edge_separation` | 对 RGB 都完整的点，取上一项数值的均方根 |
| 覆盖率、剔除截面数、饱和样本数 | 定位与边缘质量诊断，不是客户判定限 |

若三通道左边缘为 R=52、G=50、B=47，则旧三通道极差为 5 px，而 R−G 最大偏移为 2 px，B−G 为 3 px；这几个数值不能混用。没有有效点时不生成最大值或 RMS 的假零。

ImageView 的“允许的最大边缘分离”默认留空，结果为 `MEASURED`，不输出整图合格结论。显式配置产品规格后，R-G、B-G 两组边缘分离均小于或等于该值为 OK，任意点 NG 或无效时总体为 NG；已有数值阈值 JSON 仍可读取。节点不接受该判定参数。R/G/B artifact 是带采样倍率元数据的 8-bit 最大值池化定位预览，不能用于亚像素计量；所有测量和叠图坐标来自原分辨率信号。

### 关键参数与节点使用 {#parameters}

| 参数 | 默认值 | 范围和用途 |
| --- | --- | --- |
| `Rows` / `Columns` | 3 / 3 | 各为 1～16 的整数，按实际图卡设置 |
| `TargetThreshold` | 0.5 | 0.1～0.9，每个目标和截面自身背景到峰值的阈值比例 |
| `MinimumArmSpanFraction` | 0.35 | 0.1～0.9，十字在候选 ROI 中的最小跨度 |
| `AxisBandThreshold` | 0.5 | 0.1～0.9，定位窄轴带的投影阈值 |
| `MinimumArmCoverage` | 0.5 | 0.1～1，低于此覆盖率给出警告，不等于自动判无效 |
| `MinimumContrast` | 0.02 | 0.000001～1，定位使用的最小满量程信号跨度 |
| `DecodeExponent` | 1 | 0.1～5；线性图用 1，仅在明确编码时修改 |

ImageView 和节点共用事务式 `RgbCrossConfigurationWindow`，常用区域显示行列，其余六项位于默认折叠的高级参数；表单与 JSON 校验一致，默认值取参数对象。两个入口不共享一次性编辑值。RGB 固定测量三通道，编辑器不提供单通道选择。节点的 `算法参数` 保存于 `ParameterJson`，不选择或读取 FindCross 模板，旧 `TemplateName` 加载时忽略。应用前与执行前均校验，取消不回写，无效或未知字段不静默忽略。ImageView 的本次像素阈值判定不写入节点参数，节点产品判定仍由 ARVRPro 配方负责。

### 导出与适用边界 {#limits}

ImageView 独立执行不入库；结果窗口关闭后保留 `rgb-cross-measurements` 持久叠图，由用户清除，换图时按文档生命周期移除。“导出十字 JSON”输出 `colorvision.rgb-cross-measurement`：新结果统一使用 `1.2.0`，包含 `grid.rows/columns` 和 `referenceChannel: "G"`，点数组必须覆盖配置的全部点位（缺失点保留 INVALID），坐标均为原图像素；固定导出字段不含产品阈值、OK/NG 或内部截面诊断。每点 `comparisons.R-G/B-G` 含 `status`、四条带符号的 `left/right/top/bottomEdgeOffsetPx`（被测通道减 G）和 `maximumAbsoluteEdgeOffsetPx`。通道对独立有效，缺 B 时仍可保留 R-G；无效对所有偏移为 null，不能填零。旧 `separation` 和汇总三通道极差不改变语义。公共 artifact 导出仍可用于内部诊断。

明显倾斜、弯曲、宽条纹、互相交叠的行列包络以及过细的候选不在当前可靠索引范围。输出仍是相机像素，不含像素到角度的自动换算；相机与镜头色差、模组姿态和眼点位置须通过实际采集与标定复核。合成图测量和导出成功不能替代现场误报、漏报和重复性验收。