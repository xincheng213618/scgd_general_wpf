---
knowledge_id: "algorithms.grid-distortion"
knowledge_type: "topic"
status: "current"
summary: "本地点阵畸变 V2 单次定位、TV/九点多口径及相对光学估计，覆盖 ImageView、Flow 和 ARVR 2.0 适配；光学估计不等同于标定结果。"
aliases: ["点阵畸变分析(V2)","本地点阵畸变(V2)","漏光畸变失败","7x7畸变","GridDistortionAnalysis","LocalGridDistortionNode","M_CalDistortionGridV2","HorizontalTVDistortion","VerticalTVDistortion","Optic_Distortion","KeystoneHoriz","KeystoneVert","DIFF_H","DIFF_V"]
code_paths: ["Native/opencv_helper/algorithm/distortion","Native/include/opencv_media_export.h","Native/opencv_helper/opencv_media_export.cpp","UI/ColorVision.Core/GridDistortion.cs","UI/ColorVision.Core/GridDistortionAnalysis.cs","UI/ColorVision.Core/GridDistortionOpticalModel.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/GridDistortion","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/AlgorithmResultOverlay.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalGridDistortionNode.cs","Engine/ColorVision.Engine/Templates/Jsons/Distortion2","Projects/ProjectARVRPro/Process/Distortion"]
test_paths: ["Test/opencv_helper_test/test_grid_distortion_v2.cpp","Test/opencv_helper_test/benchmark_grid_distortion.py","Test/opencv_helper_test/benchmark_public_grid_distortion.py","Test/ColorVision.UI.Tests/GridDistortionTests.cs","Test/ColorVision.UI.Tests/GridDistortionAnalysisTests.cs","Test/ColorVision.UI.Tests/LocalGridDistortionNodeTests.cs"]
related: ["algorithms.arvr","algorithms.find-light-area","algorithms.find-cross","engine.native-integration","engine.results","flow.node-extension"]
---

# 本地点阵畸变 V2

V2 用一次点阵定位得到完整点位，再由 `GridDistortionAnalysis.Calculate` 同时计算 TV 的两种口径、九点的两种口径、四角几何和居中投影径向模型的光学估计。ImageView 主指标表展示 TV、对边均值九点、四角几何和相对光学估计；旧 P9 三跨度保留在全部分析 JSON。Flow 的兼容结果字段固定采用标准 TV 和对边均值九点。

输入是完整的规则圆点阵，行列分别为 3～15 的奇数，可以是 3×3、5×7、7×7 或其他非正方形奇数阵列。多点图卡的九点指标取首行、中行、末行与首列、中列、末列的交点。缺点时拒绝计算，不用拟合点冒充实测点；当前不计算左右眼 `DIFF_H`/`DIFF_V`，这还需要配对输入和明确差值定义。

## 使用入口

### ImageView

打开图像，在图像右键 **分析测量 → 视场与畸变 → 点阵畸变测量...** 设置行列数等参数并计算；矩形区域右键直接选择同名命令。菜单通过 `IIEditorToolContextMenu` 发现，无需在 `ImageView.xaml` 硬编码。结果窗口包含多口径指标、光学估计及 JSON，可复制分析结果；图上显示点位及代表九点。图像被替换或较新任务启动后，过期计算不能覆盖当前叠图。

图像分析和流程节点均提供 **亮点模式**：默认勾选，检测暗背景上的亮点，适用于发光屏幕；取消勾选，检测亮背景上的暗点，适用于暗点反射图卡。`BrightTarget` 只改变背景残差与圆心提取的亮暗方向，有序圆心之后共用相同几何计算。模式随配置和运行参数保存；未包含该字段的旧节点配置仍按亮点运行。光晕、漏光、印刷反射与照明不均会影响圆心提取，暗点照片通过不能替代发光屏幕的实拍验证。

**九点畸变测量** 位于同组。它的原生 `M_CalDistortionP9` 优先使用既有阈值分割和尺寸筛选；自动阈值和默认候选点筛选未找齐 3×3 时，使用 V2 的完整点阵定位补充，成功后仍按九点旧公式计算。显式阈值或自定义候选点筛选保持原有语义。补充定位点没有旧分割器的外接矩形，结果中的 `boundingRect` 为 null；`candidateCount` 与警告沿用 V2 的候选统计，`candidatePoints` 为空，不伪造额外候选坐标。直接将旧配置改为 7×7 不能获得有效的 49 点畸变指标。

### Flow 和 ARVR

使用 **本地点阵畸变(V2)** 节点，输入、结果目录和搜索区域沿用 Engine 本地节点约定。优先使用上游内存帧，没有内存帧时读取 IN 图像结果对应的文件；本地图片通过“加载图片”节点输入。彩色帧按 BGR 灰度化，CIE 取 Y 平面，输入只读。借用帧必须满足已有方向标记，节点不重复翻转。ROI 输出坐标还原到整图。

在节点右侧的 **说明** 中点击 **查看算法原理**，打开随软件提供的离线图文帮助，包含公式、指标判读和点阵交互示意。帮助主题 ID 为 `algorithms.grid-distortion`，与本页共用内容；窗口支持打开对应网站页面、复制链接和打印或保存 PDF。

离线正文由本页 `algorithm-help` 标记段及共享示意组件生成到 `UI/ColorVision.UI.Desktop/Assets/Help`。修改正文或共享组件后运行 `npm run docs:help` 生成帮助包，`npm run docs:help:check` 检查内容是否同步；这两个命令仅处理本地帮助文件，不上传网站。普通 `dotnet build` 只复制已生成的帮助资源，不要求安装 Node。

“搜索区域关注点”可选择寻找发光区写入的 POI 模板，每次执行读取数据库最新内容，优先于固定“搜索区域”；两者属于同一配置组，选中关注点后隐藏手动区域，清空后恢复显示且保留原矩形值。结果目录支持文件夹选择器。支持单个中心矩形 Rect、左上角矩形 LTRect、按 LT/RT/RB/LB 保存的四角点外接矩形（宽高包含两端像素），不做透视裁正。模板不存在、形状无效或区域越界时停止，不回退全图。结果参数记录模板名和有效搜索矩形。模板没有帧身份，需让前序定位成功后串行执行，并行流程使用独立模板。

节点自动输出 TV、九点及有效的相对光学估计，完整分析保存全部口径，无需配置输出选择。`TV_distortion` 和 `LocalGridDistortionMetrics.TV` 固定使用标准 TV，`Point9_distortion` 和 `LocalGridDistortionMetrics.Point9` 固定使用对边均值九点，沿用原默认口径供后续 ARVR 读取和判定。

流程执行会写入既有结果数据库和节点结果目录，需要已有流程批次及数据库连接；ImageView 单次分析不要求数据库。成功记录的类型为 `Distortion`（9）、版本为 `2.0`，一条 `DetailCommon` 指向唯一结果 JSON 文件。默认把对边均值九点的六项数值写入既有 `Point9_distortion` 字段名，`TV_distortion`、`Point9_distortion`、`Optic_Distortion` 保持 `Distortion2View` 与 ProjectARVRPro 消费的 JSON 结构；兼容的是读取格式，不代表沿用旧 P9 公式或旧梯形轴定义。数值已经是百分数，不再乘 100。

全部分析同时保存在 `LocalGridDistortionAnalysis`、结果文件和主记录参数中。节点共用一次定位和分析，自动输出 TV、九点以及通过残差校验的相对光学估计；`Optic_Distortion` 仍标明是未标定估计，并省略含义未确认的 `t`。光学模型无效时该字段为 null，CSV 留空，不补零。旧节点配置可继续加载，其中的光学输出开关和 `TvFormula` / `Point9Formula` 选择均忽略，重新保存后不再写入；原先选择半值 TV 或旧 P9 三跨度的节点统一使用上述默认口径，其他口径保留在完整分析中。原有客户配方、判定限和协议字段由 ProjectARVRPro 负责。

算法结构化拒绝和分析几何退化可保存失败主记录，不生成成功明细，也不替换上游有效主记录引用；事务提交后才更新当前结果引用。提交后的消息发布失败不撤销已保存结果。帧加载、方向/ROI 等前置错误及未包装的意外异常不保证产生失败主记录。`TotalTime` 记录定位调用耗时，不含派生分析、文件、数据库和通知。

<!-- algorithm-help:start -->

## 点阵畸变测量原理 {#algorithm-principles}

<span id="计算口径"></span>

本说明对应本地点阵畸变 V2，公式版本为 `point-grid-metrics/2`。同一次圆心定位产生 TV、九点四边、梯形、四角几何和相对径向估计。Flow 的兼容结果固定使用标准 TV 和对边均值九点，完整分析同时保留半值 TV 和旧本地 P9 三跨度口径。

### N×M 怎样取代表九点 {#grid-points}

N 是行数，M 是列数，当前各自支持 3～15 的奇数。先定位完整点阵，再取首、中、末行与首、中、末列的九个交点。例如 **5×7** 取第 **1、3、5 行**和第 **1、4、7 列**：

```text
A 左上 ─── B 上中 ─── C 右上
│           │           │
D 左中 ─── E 中心 ─── F 右中
│           │           │
G 左下 ─── H 下中 ─── I 右下
```

九点指标按这些实测点计算，不把所有 3×3 小格的结果平均；缺点时拒绝计算，不用拟合点冒充实测点。中心 E 不直接进入 TV、四边弯曲和梯形公式，它是径向模型的参考中心。点阵中其他点仍参与完整性、定位质量和全点模型检查。

### 观察几何变化 {#interactive-demo}

切换点阵规格或成像形状，观察代表点与指标变化。示意使用合成圆心坐标，只演示几何量，不执行图像定位或光学模型拟合。

<GridDistortionDemo />

### 六条参考跨度 {#reference-spans}

跨度使用两端圆心的欧氏距离，单位为 **px**。定义上、中、下宽度和左、中、右高度：

$$
W_t=\lVert A-C\rVert,\qquad W_m=\lVert D-F\rVert,\qquad W_b=\lVert G-I\rVert
$$

$$
H_l=\lVert A-G\rVert,\qquad H_c=\lVert B-H\rVert,\qquad H_r=\lVert C-I\rVert
$$

对边均值为：

$$
\bar W=\frac{W_t+W_b}{2},\qquad \bar H=\frac{H_l+H_r}{2}
$$

结果中的 `TopWidth`、`MiddleWidth`、`BottomWidth`、`LeftHeight`、`CenterHeight`、`RightHeight` 分别对应这六条跨度。

### TV 比较边缘和中间跨度 {#tv-distortion}

| 指标 | 数值计算公式 | 含义 |
| --- | --- | --- |
| 标准水平 TV | $100\times(\bar W-W_m)/W_m$ | 上下宽度均值相对中间宽度的变化 |
| 标准垂直 TV | $100\times(\bar H-H_c)/H_c$ | 左右高度均值相对中间高度的变化 |
| 半值水平 TV | 标准水平 TV 除以 2 | 同一次定位的另一种输出口径 |
| 半值垂直 TV | 标准垂直 TV 除以 2 | 同上 |

单位均为 **%**。正值表示边缘平均跨度大于中间跨度，负值表示小于。TV 描述整体跨度变化；透视和其他成像几何也可能影响它，不能仅由 TV 换算独立标定的径向畸变。

### 四边畸变测量中点偏离 {#point9-distortion}

四个边中点到对应角点连线的有符号垂距分别为 $d_t$、$d_b$、$d_l$、$d_r$。**向图卡内部弯曲为正，向外为负。**

| 指标 | 所用垂距 | 数值计算公式 |
| --- | --- | --- |
| 上边畸变 `TopPercent` | B 到直线 AC 的垂距 $d_t$ | $100d_t/\bar H$ |
| 下边畸变 `BottomPercent` | H 到直线 GI 的垂距 $d_b$ | $100d_b/\bar H$ |
| 左边畸变 `LeftPercent` | D 到直线 AG 的垂距 $d_l$ | $100d_l/\bar W$ |
| 右边畸变 `RightPercent` | F 到直线 CI 的垂距 $d_r$ | $100d_r/\bar W$ |

单位为 **%**。例如左右平均高度为 1000 px，上中点向内偏离上边角点连线 10 px，上边畸变即为 **+1%**。每条边只取对应的中点测量偏离，未在整条边上搜索最大弯曲点。

### 梯形比较两条对边长度 {#keystone-distortion}

| 指标 | 数值计算公式 | 正值表示 |
| --- | --- | --- |
| `KeystoneHorizontalPercent` | $100(H_l-H_r)/\bar H$ | 左边高度大于右边高度 |
| `KeystoneVerticalPercent` | $100(W_t-W_b)/\bar W$ | 上边宽度大于下边宽度 |

单位为 **%**。当前对边均值口径中，**Horizontal 比较左右高度，Vertical 比较上下宽度**。四边均为直线的梯形，四边弯曲值可以为零，梯形指标仍可不为零。

### 四边倾斜角与最大倾斜角 {#corner-inclination}

上下边相对图像水平线的倾斜角为：

$$
\theta_{t,b}=\operatorname{atan2}(\lvert\Delta y\rvert,\lvert\Delta x\rvert)\times\frac{180}{\pi}
$$

左右边相对图像垂直线的倾斜角为：

$$
\theta_{l,r}=\operatorname{atan2}(\lvert\Delta x\rvert,\lvert\Delta y\rvert)\times\frac{180}{\pi}
$$

这里的坐标差来自该边两端实测角点。`TopTiltDegrees`、`BottomTiltDegrees`、`LeftTiltDegrees`、`RightTiltDegrees` 保存四边角度，范围均为 **0～90°**；`MaximumTiltDegrees` 取四者最大值。

基准为原图水平、垂直方向，因此包含图卡整体旋转。例如规则矩形整体旋转 5°，最大倾斜角也为 5°。它描述图像上的边线方向，不能直接解释为器件实际的空间倾角。

### 最大边长差比例 {#maximum-edge-length-difference-percent}

`MaximumEdgeLengthDifferencePercent` 将两组对边长度差取绝对值，再取较大值：

$$
D_{\max}=100\max\left(\frac{\lvert H_l-H_r\rvert}{\bar H},\frac{\lvert W_t-W_b\rvert}{\bar W}\right)
$$

它也等于对边均值口径下两个梯形比例绝对值的最大值，单位 **%**，始终使用两对边平均长度。例如上边 1000 px、下边 1200 px，左右高度相等，结果为 $100\times200/1100\approx18.18\%$。

该值只使用四个角点。整体等比例缩放或旋转不改变它；中间点弯曲而四角不变时，它仍可为零。两项四角汇总不能替代中间点、四边弯曲和径向畸变分析，也不自动设置客户判定限。

四角几何整体保存在 `Analysis.Geometry`，ImageView 显示并可复制这些指标。全部分析 JSON、Flow 结果文件和主记录参数保存同一计算结果；Flow 同时提供 `LocalGridDistortionGeometry`、`LocalGridDistortionMaximumTiltDegrees`、`LocalGridDistortionMaximumEdgeLengthDifferencePercent`，并在 `LocalGridDistortionMetrics.Geometry` 中保留整体对象。原有百分比字段及 ARVR 默认口径不变，角度字段由 `Degrees` 明示单位。

### 旧本地 P9 三跨度口径 {#legacy-point9}

旧口径把中间跨度也计入分母：

$$
W_3=\frac{W_t+W_m+W_b}{3},\qquad H_3=\frac{H_l+H_c+H_r}{3}
$$

上下边弯曲量改为除以 $H_3$，左右边弯曲量改为除以 $W_3$，均乘 100。旧梯形还保留了不同的轴命名：

| 旧本地字段 | 数值计算公式 | 比较对象 |
| --- | --- | --- |
| 旧 `KeystoneHorizontalPercent` | $100(W_t-W_b)/W_3$ | 上下宽度 |
| 旧 `KeystoneVerticalPercent` | $100(H_l-H_r)/H_3$ | 左右高度 |

不能只改分母后沿用字段解释。旧口径兼容本仓库 `distortion_p9.cpp` 的数学约定；使用 V2 定位器后，点位和最终数值不保证与旧定位器逐位一致，也不宣称等同于不可见的供应商服务实现。对比历史结果时需确认分母和轴定义。

### 相对光学估计使用全部点 {#optical-estimate}

<span id="相对光学估计"></span>

方法 `CenteredProjectiveBrownK1/v2` 假设图卡为等间距平面点阵，光学中心位于实测中心点 E。使用完整点阵联合拟合无径向畸变投影参考 $p_0=H(u,v)$ 与一阶 Brown 径向模型：

$$
p_a=p_0\left(1+k_1\lVert p_0\rVert^2\right)
$$

二者均以中心点为原点，径向模型作用于投影后的图像坐标。中心相邻点只提供优化初值，最终节距来自拟合投影的局部导数，避免把已经畸变的中心节距直接当作理想放大率。

每个非中心点按实测半径 $r_a$ 与参考半径 $r_0$ 计算：

$$
D_i=100\frac{r_{a,i}-r_{0,i}}{r_{0,i}}
$$

| 光学结果 | 含义 |
| --- | --- |
| `ActualRadiusPixels` | 实测点到中心 E 的距离，对应 AD，单位 px |
| `ReferenceRadiusPixels` | 对应投影参考点到中心 E 的距离，对应 PD，单位 px |
| `ReferencePosition` | 对应投影参考点的原图坐标 |
| `RadialPercent` | 该点的径向偏差百分比，正值为枕形方向，负值为桶形方向 |
| `OpticRatioPercent` | 选取绝对百分比最大的点，并保留该点正负号 |
| `MaxAbsoluteRatioPercent` | 最大绝对径向偏差百分比，始终非负 |
| `MaxErrorPointId` | 上述最大偏差点的 ID |

最大点按绝对**百分比**选择，不按最大的像素位移选择。中心点参考半径为零，不生成逐点径向百分比；5×7 点阵对应 34 个非中心点样本。不能仅凭 TV 或四边畸变换算本模型的径向结果。

| 模型参数及诊断 | 含义 |
| --- | --- |
| `Origin` | 实测中心 E 的原图坐标，单位 px |
| `ColumnPitch`、`RowPitch` | 中心处无径向畸变参考的列、行节距向量，单位 px |
| `RadialCoefficientPerPixelSquared` | 一阶径向系数 $k_1$，单位 $\mathrm{px}^{-2}$ |
| `FitRmsPixels` | 全部点的二维拟合残差均方根，单位 px |
| `MaxResidualPixels` | 最大单点拟合残差，单位 px |
| `FitResidualFraction` | 拟合 RMS 除以最小实测相邻点距，是无量纲比例 |
| `FitIterations` | 数值拟合迭代次数 |
| `IsAvailable` | 是否通过收敛、可辨识性、几何和残差检查 |
| `IsCalibrated` | 当前固定为 false，表示未经独立标定 |

逐点参考位置包含投影，不能仅用行、列节距向量线性外推全图。径向偏差描述变形量，拟合残差描述模型预测位置与实测位置的差；低残差不能证明光学中心等假设成立。

优化未收敛、参数不可辨识、投影退化、径向映射非单调，或 RMS 超过最小实测相邻点距的 **1%**、单点残差超过 **3%** 时，`IsAvailable = false`，数值为空并给出原因。残差门限衡量模型符合程度，不是计量精度承诺；失效不阻断 TV、九点结果，Flow 只自动发布有效的光学项。结果窗口显示像素残差与迭代次数，分析 JSON 保存诊断。

参考由单张图估计，结果只覆盖实测点阵范围。模型可在居中一阶径向假设成立时恢复已知畸变并分离透视，但不估计光轴偏心、切向和高阶畸变；九点约束有限，不能据此当作独立标定或整幅图边缘畸变。独立标定及图卡、光轴信息仍用于验证实拍图的物理准确性。

### 单位与定位质量读数 {#metric-units}

上述 `Percent` 及兼容 JSON 中的比例数值已乘 100，例如 `1.5` 表示 **1.5%**。`Fraction` 字段为比例，例如 `0.01` 表示 **1%**。光学项不可用或旧结果缺少新字段时应保留为空，不能当作有效零。

| 诊断 | 含义 |
| --- | --- |
| 候选点数、选中点数 | 定位候选数量和最终组成完整点阵的数量 |
| `Quality.Score` | 启发式定位质量分数，不是正确率概率 |
| `MinimumContrast` | 最小归一化点对比度 |
| `GridResidualFraction` | 定位几何残差相对网格间距的比例 |
| `ProcessingScale` | 粗定位所用的处理缩放比例 |
| 各阶段耗时 | 预处理、候选检测、排序、原图精修、原生指标计算和原生检测合计，单位 ms |

单张图没有左右眼配对信息，当前不生成 `DIFF_H`、`DIFF_V`。最终合格判定使用项目约定的指标口径和限值。

<!-- algorithm-help:end -->

公式参考 [Edmund Optics 的径向与 TV 畸变说明](https://www.edmundoptics.com/knowledge-center/application-notes/imaging/distortion/)；规则点阵检测参考 [OpenCV findCirclesGrid](https://docs.opencv.org/4.10.0/d9/d0c/group__calib3d.html)，畸变参考网格与标定方法可参阅 [Discorpy 方法文档](https://discorpy.readthedocs.io/en/latest/tutorials/methods.html)。本实现未移植 Discorpy 的完整标定模型，附件中的公式也不作为行业标准认证依据。
## 定位、质量与接口

V2 先缩小图像作粗定位，通过局部背景扣除与局部对比归一化减轻漏光及高亮反光影响，生成圆点候选；结合行列拓扑和拟合残差选择完整点阵，再在原图局部窗口细化点中心。全局亮度阈值和固定像素面积不再是唯一选点条件，仍保留明确的对比度和几何质量门限。过强非投影变形、遮挡或缺点仍可能拒绝，质量分数不是正确率概率。

候选提取保留外框、圆环内部的独立前景点，排除孔洞轮廓，避免框住整张图时漏掉内部圆点。聚类排序失败时，使用 OpenCV 基于邻接图的排序假设，两者都必须通过相同的完整点数、几何残差和原图精修校验。额外点只有在竞争已占用格点，或紧邻行列至少三个不同格点形成扩展时才判为歧义；孤立远点偶然落在无限延伸的整数网格上，不足以否定当前点阵。该检查不保证识别相距较远的多套图卡，多个完整图卡同框时应通过搜索区域明确目标。

| Core 参数 | 默认值 | 范围 |
| --- | --- | --- |
| `ExpectedRows` / `ExpectedCols` | 3 / 3 | 3～15 的奇数 |
| `BrightTarget` | true | “亮点模式”勾选为亮点，取消为暗点 |
| `MaxProcessingSize` | 1600 | 256～4096 px |
| `MinimumContrast` | 0.02 | 0～1 |
| `MaximumGridResidualFraction` | 0.3 | 大于 0 且不超过 1，相对网格间距 |

Flow 当前直接开放行列数、亮点模式、搜索区域和最小对比度，其他检测参数使用 Core 默认值。原生导出 `M_CalDistortionGridV2` 接收 `HImage`、`RoiRect` 和 UTF-8 JSON，返回 JSON 缓冲区并由 `FreeResult` 释放。原有 P9 ABI 保持独立；调用 V2 需要同时交付具有新导出的 `opencv_helper.dll`。包装层校验结果完整性和有限值，DLL、入口、解析及释放错误返回失败，不能作为有效的零畸变。

## 验证和复现

原生定向测试覆盖输入类型、亮暗点、ROI 原图坐标、漏光、强反光、缺点拒绝及几何公式；托管测试覆盖 ABI 包装、多口径计算、结果窗口、Flow 参数快照与 ARVR JSON 映射；光学模型使用独立前向生成的 ±12% 真值（3×3、7×7、15×15 与矩形点阵）、旋转、透视及亚像素噪声验证，并覆盖非参考点离群、高阶模型失配及失效光学项不发布。测试目录的基准脚本分别调用同一 DLL 的旧、新 ABI，用固定种子合成图及可选实图比较。

以下命令只构建/运行本地测试并写入指定输出，不运行设备或生产数据库。Python 脚本需要已安装的 NumPy 和 OpenCV Python；DLL 先按原生构建约定编译 Release/x64。

```powershell
python .\Test\opencv_helper_test\benchmark_grid_distortion.py --dll .\x64\Release\opencv_helper.dll --sample C:\Samples\distortion.cvraw --output .\artifacts\grid-distortion-benchmark

python .\Test\opencv_helper_test\benchmark_public_grid_distortion.py --dll .\x64\Release\opencv_helper.dll --dataset C:\Samples\opencv-circles --output .\artifacts\grid-distortion-public

dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -c Release -p:Platform=x64 --filter 'FullyQualifiedName~GridDistortionTests'
```

当前托管套件不包含真实样本、原生合成图或 WPF 结果窗口渲染宿主；上述 Python 基准与托管契约测试也不是现有用户窗口截图。现场复核需要显式提供样本、DLL 与独立输出目录。

公开图脚本读取已准备好的 OpenCV `opencv_extra/testdata/cv/cameracalibration/circles` 中 14 对 `circlesN.png` 和 `circles_cornersN.dat`，不自动下载。使用固定 7×7、暗点和整图配置，同时比较新旧原生接口与 OpenCV 默认检测；输出目录必须为空，记录输入、参考和 DLL 哈希。参考点来自 OpenCV 回归数据，允许未标方向正方点阵的八种整体对称对齐，不做任意点重排或坐标拟合。参考差异用于回归比较，不代表有独立计量真值；用于修复的公开图应视为开发回归集，不能再作为未见数据的通过率证明。

合成正确性门限同时要求完整行列对应、最大点误差不超过 1 px、有效跨度、按各自公式计算的最大指标误差不超过 0.1 个百分点；不能只数 `success=true`。性能包含原生调用、JSON 字节复制和释放，不含读图、JSON 解析、派生分析、UI 或数据库。单张实图和合成扰动只能验证这些案例，不能推算量产失败率或承诺固定加速倍数。
