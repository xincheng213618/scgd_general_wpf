## FOV 测量原理 {#algorithm-principles}

本地 FOV V2 在 ColorVision 进程内完成发光区定位和水平、垂直、对角视场角计算，不依赖 `CV_algorithm.dll` 或远端算法服务。原有 FOV 2.0 模板和远端链路继续保留；本地流程节点以相同结果类型、版本和 JSON 字段写入现有结果链，供历史结果及 ProjectARVRPro 继续读取。

FOV 节点把所选几何跨度换算为角度，单位为 **°**。四角模式可以复用上游定位的四角；轮廓模式必须读取同源图像，重新寻找真实外边界。两者使用相同相机参数，但选取跨度的方式不同。

<figure class="algorithm-figure" style="overflow-x:auto">
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 310" role="img" aria-labelledby="fov-principle-title fov-principle-desc" style="display:block;min-width:720px;width:100%;height:auto;color:#1f2937">
    <title id="fov-principle-title">FOV 四角模式与轮廓模式的跨度取法</title>
    <desc id="fov-principle-desc">左图四角模式对上下边、左右边和两条对角线换算角度，再分别平均，方向视场使用对边中点连线。右图轮廓模式使用实测轮廓的包络与通过包络中心的实际截线端点，不把包络框角当作实测角点。</desc>
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="17"><text x="35" y="29">四角模式</text><text x="515" y="29">轮廓模式</text></g>
    <polygon points="65,76 350,94 326,241 85,246" fill="#e8f2ff" stroke="#2175d9" stroke-width="2" />
    <g stroke="#8b6ec6" stroke-width="1.5" stroke-dasharray="6 4"><path d="M65 76 L326 241 M85 246 L350 94" /></g>
    <path d="M75 161 L338 167.5" fill="none" stroke="#dc8b19" stroke-width="3" />
    <path d="M207.5 85 L205.5 243.5" fill="none" stroke="#26805a" stroke-width="3" />
    <g fill="#2175d9"><circle cx="65" cy="76" r="4" /><circle cx="350" cy="94" r="4" /><circle cx="326" cy="241" r="4" /><circle cx="85" cy="246" r="4" /></g>
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="15"><text x="43" y="61">LT</text><text x="347" y="78">RT</text><text x="327" y="265">RB</text><text x="63" y="270">LB</text><text x="120" y="155">方向 H</text><text x="218" y="130">方向 V</text></g>
    <rect x="525" y="70" width="300" height="175" fill="none" stroke="#94a3b8" stroke-dasharray="6 4" />
    <polygon points="525,160 570,70 740,85 825,155 760,245 580,225" fill="#e8f2ff" stroke="#2175d9" stroke-width="2" />
    <path d="M526.25 157.5 L823.194 157.5" fill="none" stroke="#dc8b19" stroke-width="3" />
    <path d="M675 79.265 L675 235.556" fill="none" stroke="#26805a" stroke-width="3" />
    <circle cx="675" cy="157.5" r="4.5" fill="#1f2937" />
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="15"><text x="545" y="56">X 包络</text><text x="835" y="160">Y 包络</text><text x="686" y="181">中心 O</text><text x="551" y="151">真实截线 H</text><text x="686" y="112">真实截线 V</text></g>
    <g fill="currentColor" font-family="Segoe UI,Microsoft YaHei,sans-serif" font-size="14"><text x="35" y="298">对边中点连线与单条边、对角线分别使用</text><text x="515" y="298">灰框只提供包络和中心，不生成四角</text></g>
  </svg>
  <figcaption>橙色为方向水平跨度，绿色为方向垂直跨度；非四边形轮廓使用实际截线交点。</figcaption>
</figure>

## 参数归属

`FovDist` 和 `cameraDegrees` 都是与相机及镜头标定相关的 FOV 运行参数。`FovDist` 是参考像素跨度（px），`cameraDegrees` 是该参考跨度对应的标定角度（°），两者必须配对使用。Flow 和 ImageView 均直接展示并允许调整，默认值分别为 `9410` 和 `74.2`；不从物理相机配置隐式覆盖。

本地 FOV 默认测量稳定发光区四角对应的几何视场。`LuminanceBoundaryRatio` 仅保留旧配置读取兼容，不显示为 FOV 参数，也不参与定位或计算。画面内部亮度下降不能自动改写几何边界；若产品规范要求亮度或对比度限定的有效视场，应另行确认参考量、阈值、扫描方向和测量条件，不能将任意等亮度线的四边拟合视为标准测量。

Flow 与 ImageView 增加“测量模式”：`FourCorners`（四角，默认）保留上述行为，旧配置未保存该字段时仍取默认；`CurvedBoundary`（轮廓，曲边/非四边形）独立读取图像，支持单块、完整填充且从中心向外每个方向仅有一次外边界交点的发光轮廓，例如枕形、圆形、椭圆和多边形。轮廓模式不使用上游角点、不拟合四条直线，也不伪造 LT/RT/RB/LB；其 `Corners` 为空。

它以缩采样、空间中值采样、Otsu 和最大亮连通区确定搜索种子；Otsu 暗类再分割并以实测背景噪声限制扩展，使暗角不直接变成内部边界。精定位仍从原始像素中寻找有持续内外对比支持的局部明暗梯度，搜索范围由亮、暗种子限定，避免追到外围独立暗环，不以中心亮度的某一比例定义边界。完整轮廓靠近 ROI 边缘、外侧背景少于常规采样宽度时，使用实际可用背景；种子接触 ROI 边缘仍视为裁切并拒绝。

空间中值采样按种子步长抑制细文字和细线，不修改输入像素、不执行整孔填充或轮廓拟合。孔洞检查遍历种子图的外部背景连通性，避免漏掉两条角度射线之间仍可分辨的孔洞；被缩采样或中值滤波消除的细小缺陷不在检出保证内。裁边、中心不发光、可分辨的孔洞或同方向多段边界、低可信度时拒绝输出。最大连通区选择不识别测试图案的业务含义：图中有多块分离亮区时，仅测其中最大的一块，不能声称测得所有亮区的完整 FOV，应通过 ROI 明确唯一目标。环形图案不适用此模式。

两种模式沿用相同的 `FovDist` / `cameraDegrees` 换算，不新增相机内参、畸变或眼位标定。轮廓模式属于可见外边界的几何估计，不是 IDMS 亮度/对比度有效 FOV 方法，也不声明 IEC 符合性。测量相机的畸变、光轴偏心及眼位仍需独立确认；不能把被测显示系统自身的畸变人为拉直后再声称测得实际覆盖范围。

`cameraDegrees` 输入框右侧提供统一的计算入口，Flow 和 ImageView 使用同一个属性编辑器。计算窗口可输入传感器有效宽度、有效高度和镜头有效焦距，并按水平、垂直或对角方向选择回填值：

$$
cameraDegrees = 2\arctan\left(\frac{SensorLength}{2FocalLength}\right)\times\frac{180}{\pi}
$$

这里传感器有效长度和焦距使用相同的 mm 单位，计算结果为度。

输入变化后，窗口立即显示水平、垂直和对角 FOV，并根据“参考方向”显示将写回的 `cameraDegrees`；确认后只回填 `cameraDegrees`，不会修改 `FovDist`。传感器宽度和高度各保留一个：分别用于水平、垂直计算，并共同用于对角计算。所选传感器方向必须与 `FovDist` 代表的参考像素跨度一致；使用相机 ROI 或裁图时，应填写有效成像区域的传感器尺寸，而不是未经裁切的名义尺寸。

两项参数都必须是大于零的有限数值，且 `cameraDegrees` 小于 `180` 度。参数虽然可调，但同一台相机的生产流程应使用已确认的配对值，不应用调参吸收发光区定位误差。

旧模板中的 `DarkRatio`、`threshold`、`ExactCorner`、`AnglesFov`、横竖开关等字段不迁入本地节点。它们属于旧服务的定位或开关配置，不参与本地角度换算。读取旧模板和调用旧服务的兼容行为不变。

## Flow 使用

在流程中加入 **本地FOV计算(V2)** 并接入图像或发光区结果，默认四角模式如下：

1. 上游是 `FindLightArea` 或 `LightArea` 且已有四条有效角点明细时，节点直接使用 LT、RT、RB、LB 计算，保留原始四角，不再读取像素重定位。
2. 上游只有图像时，节点对当前内存帧运行与本地发光区相同的 `RobustV2`，直接使用成功结果的四角；没有内存帧时再读取上游结果对应的图像文件。
3. 两种输入使用相同的四角角度公式，不经过 `FovLuminanceBoundary`。
4. 相机优先取上游图像记录的设备编码；旧发光区结果没有 `SourceMasterId` 时，会按其图像路径反查最近的图像记录。这个相机关联只用于流程结果追溯，不改写节点中的两项 FOV 参数。
5. 成功后写入现有 FOV 2.0 结果并将本次主结果传给后续节点。

发光区明细只有有效数据库角点，没有可用内存帧或图像文件时仍可计算。需要自行定位但没有图像、`RobustV2` 拒绝结果、角点不是四个、顺序无效或 FOV 参数无效时拒绝执行。Flow 的自动定位最低可信度固定为 `0.25`；四角模式只约束自动运行的 `RobustV2`，不会用图像中的亮度再次筛选有效上游角点。

选择轮廓模式时应直接接取图/图像节点，始终重新定位。若接入发光区结果，只用主记录追溯对应图像，不读取四角明细，也不要求明细存在四角。优先复用匹配该源图记录的内存帧，否则读取对应图像文件；禁止借用其他图像的当前帧。只有角点、没有对应像素时失败，不回退到四角模式。

## ImageView 使用和绘制

打开图像，在图像右键选择 **分析测量 → 视场与畸变 → 视场角测量（FOV）...**；矩形区域右键直接选择同名命令。矩形入口将选择框作为 `RobustV2` 搜索区域；整图入口搜索完整图像。独立入口自行定位并直接计算，ImageView 绘制本次几何边界：

- LT、RT、RB、LB 四角标签及蓝色四边形；
- 橙色水平中轴、绿色垂直中轴；
- 两条紫色虚线对角线；
- H、V、D 三项角度标签。

FOV 标签圆的直径与当前结果文字字号一致；修改固定结果字号时圆和文字同步缩放，启用图层自动刷新时二者也随视图缩放保持一致。该规则只用于 FOV 结果标签，不改变普通测量圆的几何半径。

ImageView 的 FOV 选项包含 `FovDist`、`cameraDegrees`、“测量模式”和“最低可信度”。最低可信度约束所选定位算法；矩形入口本身提供搜索区域。相机选择、流程批次和数据库结果不属于这个独立图像工具。轮廓模式绘制蓝色实测轮廓和橙/绿中心截线、紫色中心斜向截线，不显示四角标签；结果窗口注明包络和中心斜向口径。

新的 FOV 运行会清除上一组 FOV 叠图，打开新图也会清除旧结果。ImageView 只计算、绘制并显示本次结果，不查询测量批次、不写数据库，也不发布流程结果。流程节点继续使用严格结果链；持久化失败会使节点失败，不能把只有内存数值的运行交给后续节点。

## 计算口径

`RobustV2` 寻找稳定几何边缘；FOV 不再改变其四角。优先复用上游角点是为了让定位、绘图和角度换算保持一致，而非宣称几何四角等于所有产品规范中的有效视场。上游采用不同定位方法时，其差异也会保留到 FOV 中。

独立保留的 `FovLuminanceBoundary` 仅是实验等亮度线拟合：内部暗角低于中心亮度比例时，外沿可能根本没有该比例交点，算法会命中内部渐变并产生倾斜边。增加交点数量或放宽置信度不能修复测量对象的改变。它不是本地 FOV 的默认路径，也不代表 IEC/IDMS 符合性。

像素线段长度为 $L$，参考像素跨度为 $D=FovDist$，相机参考角为 $C=cameraDegrees$ 时，使用针孔模型换算式：

$$
\alpha(L)=2\arctan\left(\frac{L}{D}\tan\frac{C\pi}{360}\right)\times\frac{180}{\pi}
$$

这里 $L$、$D$ 均为 px，$C$ 和结果均为度。长度相同但参考标定不同，角度也会不同；不能把 `FovDist` 当作相机到目标的物理距离。若 $L=D$，换算角恰为 $C$，例如跨度 9410 px 配对参考角 74.2° 时，对应 74.2°。

弧度到角度使用精确的 `180 / PI`，不保留旧实现的 `57.3` 近似常量。因此相同角点和标定参数下，本地结果会比旧 DLL 数值低约 `0.00737%`；这属于换算精度修正，不应用参数补偿。四角顺序固定为 LT、RT、RB、LB：

| 结果 | 线段与汇总方式 |
| --- | --- |
| `H_Fov` | 上边 LT-RT 与下边 LB-RB 的角度均值 |
| `V_FOV` | 左边 LT-LB 与右边 RT-RB 的角度均值 |
| `leftDownToRightUp` | 对角线 LB-RT |
| `leftUpToRightDown` | 对角线 LT-RB |
| `D_Fov` | 两条对角线角度均值 |
| `clolorVisionH_Fov` | 左右边中点之间的水平中轴 |
| `clolorVisionV_Fov` | 上下边中点之间的垂直中轴 |

后两个字段保留历史拼写和存储位置，但本地用对边中点，旧 DLL 另有中心方向边界定位，两者不能宣称数值或测量方法等价。ProjectARVRPro 的 W51 正是读取这两个字段及 `D_Fov`，经过 Recipe 修正后比较上下限；角度合格阈值不定义像素边界的亮度阈值。替换前应在固定曝光、测试图案和相机标定下用现场图集比较这三项及单独对角线，确认原 Recipe 的适用性；不能调整 `FovDist` 或放宽出厂限值吸收定位差异。

四角模式对上下边、左右边、两条对角线均**先分别换算角度，再求角度均值**。例如示意矩形宽 800 px、高 600 px，采用示例参考跨度 1000 px、参考角 90°，得到水平约 77.32°、垂直约 61.93°、对角 90°；这些示例参数不是设备默认值。

轮廓模式的中心为实测轮廓外接框中心，水平/垂直方向固定为图像 X/Y，不从不存在的四边恢复旋转。`clolorVisionH_Fov` / `clolorVisionV_Fov` 使用通过此中心的真实左右/上下交点；`H_Fov` / `V_FOV` 使用整个实测轮廓的 X/Y 包络跨度。两个历史对角字段使用通过中心、平行于轮廓外接框两条对角方向的**真实轮廓交点**，`D_Fov` 仍为两项角度均值；这些交点不是角点，该均值也不是任意形状的最大跨度。圆形斜向跨度是圆的截线直径，不是外接正方形的对角线。轮廓模式的七个字段是明确约定的形状摘要，不能仅凭这七项等同整个二维角域；完整轮廓及截线端点保存在 `Boundary` 中。客户规格使用最大跨角、固体角或亮度/对比度有效视场时，应另定指标，不能默默套用上述中心斜向定义。

## 结果兼容

成功结果仍创建类型 `FOV`、版本 `2.0` 的主记录和一条 `DetailCommon` 明细。明细 JSON 的 `ResultFileName` 指向本地结果文件，文件的 `result` 对象保留以下字段及大小写：

```text
D_Fov, H_Fov, V_FOV, clolorVisionH_Fov, clolorVisionV_Fov,
leftDownToRightUp, leftUpToRightDown, message
```

流程运行的七项测量值保存在结果文件中；角点、相机编码、`FovDist`、`cameraDegrees`、`MeasurementMode`、`BoundaryMode`（`UpstreamCorners`、`RobustV2` 或 `CurvedBoundary`）及定位诊断保存在主记录参数中，便于追溯。轮廓模式额外保存 `Boundary`（轮廓、中心及水平/垂直/斜向端点），角点为空。下游业务判定、导出和协议字段仍由原有结果处理器及客户项目负责。流程保存要求已有批次和可用数据库连接；ImageView 不进入这条持久化链。

内存帧若直接来自普通图片且未经过校正或翻转，主记录 `ImgFile` 会保存该原始图片路径，结果列表的“文件”列可正常关联原图。若当前主缓冲区已校正或翻转，只使用与当前缓冲区严格对应的 `CvRawFilePath` / `CvCieFilePath`；没有这类已保存文件时保持为空，避免历史叠图错误加载变换前的源图。