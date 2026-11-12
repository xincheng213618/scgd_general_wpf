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