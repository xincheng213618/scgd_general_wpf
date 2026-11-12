---
knowledge_id: "algorithms.ring-ghost"
knowledge_type: "topic"
status: "current"
summary: "完整圆环与偏心弱环的区域平均信号比、GMR 阶次及 Flow 正常未检出零值约定；不自动证明光路阶次或现场精度。"
aliases: ["圆环鬼影", "LocalRingGhostNode", "GMR1", "GMR2", "RingGhostMeasurementParameters", "ring-ghost/1.0", "平均信号比"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/DisplayMetrology/DisplayRingGhost.cs", "UI/ColorVision.ImageEditor/Algorithms/DisplayMetrology/DisplayRingGhostRefinement.cs", "UI/ColorVision.ImageEditor/Algorithms/DisplayMetrology/DisplayRingGhostSaturation.cs", "UI/ColorVision.ImageEditor/Algorithms/DisplayMetrology/RingGhostMeasurementParameters.cs", "UI/ColorVision.ImageEditor/Algorithms/RingGhostOverlayRenderer.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalRingGhostNode.cs", "Engine/ColorVision.Engine/Templates/Jsons/RingGhost"]
test_paths: ["Test/ColorVision.UI.Tests/RingGhostMeasurementTests.cs", "Test/ProjectARVRPro.Tests/GhostFlowAndProcessTests.cs"]
related: ["algorithms.display-metrology", "projects.arvr-pro-processes", "engine.results", "flow.node-extension"]
---

# 圆环鬼影

<!-- algorithm-help:start -->
## 圆环鬼影测量原理 {#algorithm-principles}

**圆环鬼影**定位完整主亮环和其内侧的完整弱环，测量选定环带内的平均相对信号，再给出鬼环与主环的百分比。这里的 GMR 不是峰值亮度比，也不是环上最大亮点与主像的比值。

### 输入与节点参数 {#inputs}

节点优先借用方向变换已完成的上游原位深帧，没有内存帧时读取 IN 图像或校正记录对应的原图；本地图片通过“加载图片”输入。当前按线性 8/16 位 RAW、灰度 TIFF 或 RGB 图的 G 通道相对信号使用。主缓冲为 CVCIE 时拒绝，不能让显示绑定 CIE 原图而数值实际取 RAW。图像至少 32×32，像素布局必须合法且可由当前数组表示；不使用固定像素数量或 512 MiB 的预算上限，内存分配失败仍属于执行错误。

| 节点参数 | 默认值 | 含义 |
| --- | --- | --- |
| 一阶二阶对应 `OrderMapping` | 外圈一阶、内圈二阶 | 由已确认的客户光路定义设置；内圈一阶、外圈二阶也可选 |
| 结果目录 `ResultDirectory` | 留空 | 自动保存到当前用户 LocalAppData 下 `ColorVision/Results/RingGhost` |

节点不暴露亮区阈值、鬼影阈值、环中心和半径。共用算法还保留输入解码指数和测量通道；节点使用指数 1、彩色 G 通道的默认值。只有信号编码明确且采集链已校正时，区域信号比才可解释为亮度比。

### 主环与鬼环如何定位 {#detection}

实现先在最长边不超过 1600 像素的定位图上做自动阈值分割，以存在内孔、形状和中心一致的嵌套轮廓拟合主像椭圆，再在原始解码信号上按 720 个角度采样径向曲线。内侧搜索范围约为主像半径的 0.15～0.90 倍，并排除主像边缘邻域。弱环须在两种平滑尺度上保留径向峰，突出度超过径向噪声估计的六倍且至少为主像均值的 0.01%，并有至少三分之二圆周扇区支持。以上是自动检测的信号和分辨能力边界，不是客户 GMR 合格阈值；大尺度平滑为主像半径的 1%，窄弱环或背景中的肩峰可能无法分离。主像外围需保留约 30% 半径的完整图内边距。主像不完整、多个独立亮环、图案不符或主像测量区域满量程时拒绝，不能把图外补零用于测量。

偏心弱环按 72 个圆周扇区跟踪局部峰，使用有界一致性拟合及异常点剔除建立独立椭圆，确认位移超过轨迹误差后重新采样和计算测量环。圆周均值只提供可能被中位数展宽抹掉的候选，必须通过独立拟合才可加入结果；至少三分之二扇区须支持同一椭圆及局部对比，残弧不能仅凭均值曲线补成完整环。偏心环按各方向的局部谷值估计背景，避免主像光晕抬高某一侧背景而误拒绝；GMR 仍使用未扣背景的原始区域均值。候选诊断包含独立中心、椭圆轴和精修标记，叠图使用各鬼环自己的边界；原同心模型在没有显著几何位移时保留。

### 区域均值和 GMR {#metrics}

<figure class="algorithm-figure">
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 760 300" role="img" aria-labelledby="ring-ghost-title ring-ghost-desc">
  <title id="ring-ghost-title">主亮环与两个内侧弱环的区域平均信号比</title>
  <desc id="ring-ghost-desc">左侧三个带状环区域由外向内为主像、候选 A、候选 B。右侧示例主环均值零点八，A环零点零二四，B环零点零零八，对应平均信号比百分之三和百分之一。候选编号不自行确定物理阶次。</desc>
  <ellipse cx="220" cy="150" rx="180" ry="108" fill="none" stroke="#327bd0" stroke-width="13" />
  <ellipse cx="220" cy="150" rx="127" ry="76" fill="none" stroke="#b97a20" stroke-width="11" />
  <ellipse cx="220" cy="150" rx="79" ry="47" fill="none" stroke="#9762c0" stroke-width="10" />
  <text x="220" y="25" text-anchor="middle" fill="currentColor" font-size="15">测量完整环带  不是只取峰顶</text>
  <text x="220" y="285" text-anchor="middle" fill="currentColor" font-size="14">同心示意  实际可分别精修偏心椭圆</text>
  <text x="470" y="75" fill="currentColor" font-size="17">主环区域均值  0.8</text>
  <text x="470" y="125" fill="currentColor" font-size="17">候选 A 均值  0.024</text>
  <text x="470" y="158" fill="currentColor" font-size="16">A / 主环 × 100 = 3%</text>
  <text x="470" y="205" fill="currentColor" font-size="17">候选 B 均值  0.008</text>
  <text x="470" y="238" fill="currentColor" font-size="16">B / 主环 × 100 = 1%</text>
  <line x1="365" y1="86" x2="450" y2="70" stroke="#327bd0" />
  <line x1="341" y1="126" x2="450" y2="120" stroke="#b97a20" />
  <line x1="296" y1="171" x2="450" y2="200" stroke="#9762c0" />
</svg>
<figcaption>图 1 使用完整测量环带的均值。外内位置与一阶二阶的对应关系需要另外确认。</figcaption>
</figure>

测量环宽由径向峰的局部半突出度确定；鬼环平台取平台中心，并沿完整峰形搜索两侧最低点，遇到更高峰时停止，避免固定窗口落在宽环内部。仅合并半突出度环带相交的候选，不按固定半径距离吞并两个已分离的环。报告的是该环带内原始解码信号的面积加权极坐标均值，`平均信号比_% = 100 × 鬼环均值 / 主环均值`。另列以环带两侧作线性局部背景估计的扣背景对比，供观察弱环和光晕混合；这项值不替代 GMR。定位图的 8-bit 量化、平滑和圆周中位数仅用于找区域，不用于最终亮度均值。结果包含候选表、径向曲线、环带边界 overlay 与 `ring-ghost-measurement` 结构化数据，便于对照区域与重复计算。

对均匀角度和径向采样，面积权重包含半径 $r$：

$$
\bar S=\frac{\sum_{\theta,r} S(r,\theta)\,r}{\sum_{\theta,r}r},\qquad
\mathrm{GMR}=100\frac{\bar S_{\mathrm{ghost}}}{\bar S_{\mathrm{primary}}}
$$

| 指标 | 含义 |
| --- | --- |
| 平均信号比 `%` | 鬼环原始解码区域均值 ÷ 主环原始解码区域均值 ×100；GMR 采用此项 |
| 扣局部背景对比 `%` | $(\bar S_g-B_g)/\max(10^{-12},\bar S_p-B_p)\times100$，用于观察弱环与光晕混合，不替代 GMR |
| 内外边界、半径、中心、椭圆轴、旋转 | 原图像素域的测量几何；精修的鬼环有自己的中心与边界 |
| 圆周支持率 | 满足同一环形结构及局部对比条件的扇区比例，是检测质量证据 |
| 径向曲线 | 采样半径对应的圆周中位与平均信号，用于查看峰、谷和候选来源 |

图中主环均值 0.8、鬼环均值 0.024，GMR 为 3%。即使鬼环面积比主环小，也不能把积分能量比直接当作这里的平均信号比。

### 一阶二阶与正常未检出 {#order-and-zero}

图像编辑器中的候选 A/B 仅按半径从外到内排序。只有客户已确认对应关系且恰好检出两个候选时，编辑器才给出 `gmr1Percent/gmr2Percent`；其他情况留空。不能按明暗、环半径或反射次数自行推断物理阶次，`productPass` 始终为空。

Flow 节点采用当前客户光波导方案“一阶必有、正常未检出输出 0”的专用业务约定：

| 正常完成后的候选数 | Flow 输出 | 状态 |
| --- | --- | --- |
| 2 | 按确认的内外环映射输出 `GMR1`、`GMR2` | `MEASURED` |
| 1 | 唯一候选归一阶，`GMR2=0`，与内外映射选择无关 | `SECOND_NOT_DETECTED` |
| 0 | 主像及算法都有效时输出 `GMR1=0`、`GMR2=0` | `NOT_DETECTED` |
| 超过 2、映射未设置、主像无效或算法失败 | 报错，不生成有效零 | 无成功测量 |

这项零值约定不适用于只存在二阶或光路未知的样本。节点会先清除本轮旧 GMR 数据，成功持久化后才写入新值；不能把异常和未执行解释为零。阶次及正常未检出约定仍需客户用标注样本验证。

### 饱和及适用边界 {#limits}

主像饱和保护直接读取测量环内的原始像素，在解码指数变换前检查。存储满量程像素拒绝；16 位输入另将 65280～65534 视为疑似饱和并拒绝，以覆盖常见低有效位深左对齐存储的满码值，不增加现场参数。这是保守的曝光余量保护，可能拒绝实际未剪切的近满量程图像。输入没有 ADC 有效位深/满码元数据，不能仅凭图像最大值认定右对齐的 4095 等数值已饱和，也不能承诺识别全部采集剪切情况。

搜索从主像内侧约 0.15～0.90 倍半径的峰开始。外侧、交叠、只有残弧、圆周证据不足或不能从主像光晕中分离的弱环不保证检出。主像完整且算法正常时“未可靠分离”仍不等于物理上完全没有鬼影。原图未确认线性、黑电平和亮度校正时，结果只能称为相对设备信号比。

<!-- algorithm-help:end -->

## 入口与结果持久化

**圆环鬼影亮度比**是独立入口，位于 **分析测量 → 缺陷与鬼影**，适用于 AR/VR 光波导圆环测试图，算法版本 `1.1.1` 的输入、执行和结果持久化不设置固定像素数量上限，也不采用其他显示计量功能的 512 MiB 输入数据预算；图像仍须尺寸合法、像素布局完整并可由当前数组表示，内存分配失败作为执行错误处理，不补 0。使用默认参数即可检测，不配置亮区阈值、鬼影阈值、形态学核或圆环坐标；保留共用的输入解码指数、测量通道和 `RingGhostMeasurementParameters.OrderMapping` 阶次对应。既有 `ghost-measurement`、本地 native `M_DetectGhosts` 与远端 Ghost2.0 的调用和结果契约不变。

节点输入优先上游原位深内存帧，没有内存帧时从 IN 的图像/校正记录读取原图；本地图片通过“加载图片”节点输入。当前输入是线性 8/16 位 RAW、灰度 TIFF 或指定 RGB 的 G 通道相对信号；主缓冲为 CVCIE 时明确拒绝，避免把原图绑定到 CIE、数值却取自 RAW。成功后按 `Ghost` 类型、`ring-ghost/1.0` 版本保存主记录及唯一公共文件明细，结果文件含原图尺寸、测量区域与内部诊断；未配置目录时使用本机 LocalAppData 下的 `ColorVision/Results/RingGhost`。数据库事务提交后才更新流程输出和发布结果，保存失败清理本次未引用文件，不发布成功消息。历史处理器 `ViewHandleRingGhost` 只处理此版本，避免被远端 Ghost2.0 的处理器解析。项目配方、对外数据及判定见 [ARVRPro 流程与解析配置](../../projects/project-arvr-pro-processes.md)。

## 独立验证与现场一致性

瑞淀公开 Ghosting 示例报告 ghost/parent 亮度比与位移，但使用点阵，未公开本圆环图的物理阶次与 ROI 统计细节，因此不能将上述自动环带均值宣称为瑞淀等效实现。参考 [Radiant 官方资料第 63 页](https://www.radiantvisionsystems.com/sites/default/files/library-documents/Radiant_WBR_P2P-Automotive-Displays_%28GlobalSpec%29_FINAL.pdf#page=63)。`Test/ColorVision.UI.Tests/RingGhostMeasurementTests.cs` 覆盖弱环比值、曝光/尺寸/椭圆变化、阶次映射和残弧/缺环/主像无效边界；仍需客户标注的光路阶次和同图瑞淀输出验证现场一致性。搜索仍从主像内侧约 0.15～0.90 倍半径的峰开始；外侧、交叠、没有足够圆周证据或无法与主像光晕分离的鬼影不保证检出。新增大尺寸、偏心单/双环和偏心残弧回归不能代替现场计量验收。
