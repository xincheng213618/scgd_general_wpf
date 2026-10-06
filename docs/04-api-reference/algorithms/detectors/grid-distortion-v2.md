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

V2 用一次点阵定位得到完整点位，再由 `GridDistortionAnalysis.Calculate` 同时计算 TV 的两种口径、九点的两种口径和居中投影径向模型的光学估计。ImageView 主指标表展示 TV、对边均值九点和相对光学估计；旧 P9 三跨度保留在全部分析 JSON，Flow 节点仍可选它写入 ARVR 字段。参数选择不重新找点。

输入是完整的规则圆点阵，行列分别为 3～15 的奇数，可以是 3×3、7×7 或非正方形奇数阵列。多点图卡的九点指标取首行、中行、末行与首列、中列、末列的交点。缺点时拒绝计算，不用拟合点冒充实测点；当前不计算左右眼 `DIFF_H`/`DIFF_V`，这还需要配对输入和明确差值定义。

## 使用入口

### ImageView

打开图像，在图像右键 **分析测量 → 视场与畸变 → 点阵畸变测量...** 设置行列数等参数并计算；矩形区域右键直接选择同名命令。菜单通过 `IIEditorToolContextMenu` 发现，无需在 `ImageView.xaml` 硬编码。结果窗口包含多口径指标、光学估计及 JSON，可复制分析结果；图上显示点位及代表九点。图像被替换或较新任务启动后，过期计算不能覆盖当前叠图。

图像分析和流程节点均提供 **亮点模式**：默认勾选，检测暗背景上的亮点，适用于发光屏幕；取消勾选，检测亮背景上的暗点，适用于暗点反射图卡。`BrightTarget` 只改变背景残差与圆心提取的亮暗方向，有序圆心之后共用相同几何计算。模式随配置和运行参数保存；未包含该字段的旧节点配置仍按亮点运行。光晕、漏光、印刷反射与照明不均会影响圆心提取，暗点照片通过不能替代发光屏幕的实拍验证。

**九点畸变测量** 位于同组。它的原生 `M_CalDistortionP9` 优先使用既有阈值分割和尺寸筛选；自动阈值和默认候选点筛选未找齐 3×3 时，使用 V2 的完整点阵定位补充，成功后仍按九点旧公式计算。显式阈值或自定义候选点筛选保持原有语义。补充定位点没有旧分割器的外接矩形，结果中的 `boundingRect` 为 null；`candidateCount` 与警告沿用 V2 的候选统计，`candidatePoints` 为空，不伪造额外候选坐标。直接将旧配置改为 7×7 不能获得有效的 49 点畸变指标。

### Flow 和 ARVR

使用 **本地点阵畸变(V2)** 节点，输入、结果目录和搜索区域沿用 Engine 本地节点约定。内存帧优先，文件为后备；彩色帧按 BGR 灰度化，CIE 取 Y 平面，输入只读。借用帧必须满足已有方向标记，节点不重复翻转。ROI 输出坐标还原到整图。

“搜索区域关注点”可选择寻找发光区写入的 POI 模板，每次执行读取数据库最新内容，优先于固定“搜索区域”；两者属于同一配置组，选中关注点后隐藏手动区域，清空后恢复显示且保留原矩形值。结果目录支持文件夹选择器。支持单个中心矩形 Rect、左上角矩形 LTRect、按 LT/RT/RB/LB 保存的四角点外接矩形（宽高包含两端像素），不做透视裁正。模板不存在、形状无效或区域越界时停止，不回退全图。结果参数记录模板名和有效搜索矩形。模板没有帧身份，需让前序定位成功后串行执行，并行流程使用独立模板。

| 输出参数 | 默认值 | 作用 |
| --- | --- | --- |
| `TvFormula` | `Standard` | 选择标准 TV 或其半值 |
| `Point9Formula` | `OppositeEdgeMean` | 新建节点默认输出对边均值九点；旧本地三跨度仍可选，已保存节点继续使用各自记录的口径 |

流程执行会写入既有结果数据库和节点结果目录，需要已有流程批次及数据库连接；ImageView 单次分析不要求数据库。成功记录的类型为 `Distortion`（9）、版本为 `2.0`，一条 `DetailCommon` 指向唯一结果 JSON 文件。默认把对边均值九点的六项数值写入既有 `Point9_distortion` 字段名，`TV_distortion`、`Point9_distortion`、`Optic_Distortion` 保持 `Distortion2View` 与 ProjectARVRPro 消费的 JSON 结构；兼容的是读取格式，不代表沿用旧 P9 公式或旧梯形轴定义。数值已经是百分数，不再乘 100。

全部分析同时保存在 `LocalGridDistortionAnalysis`、结果文件和主记录参数中。节点共用一次定位和分析，自动输出 TV、九点以及通过残差校验的相对光学估计；`Optic_Distortion` 仍标明是未标定估计，并省略含义未确认的 `t`。光学模型无效时该字段为 null，CSV 留空，不补零。已保存节点中的旧光学输出开关不再生效，重新保存后不再写入；TV 和九点口径仍按各自配置输出。原有客户配方、判定限和协议字段由 ProjectARVRPro 负责。

算法结构化拒绝和分析几何退化可保存失败主记录，不生成成功明细，也不替换上游有效主记录引用；事务提交后才更新当前结果引用。提交后的消息发布失败不撤销已保存结果。帧加载、方向/ROI 等前置错误及未包装的意外异常不保证产生失败主记录。`TotalTime` 记录定位调用耗时，不含派生分析、文件、数据库和通知。

## 计算口径

设九个代表点按行列排序；上、中、下水平跨度为 `Wt/Wm/Wb`，左、中、右垂直跨度为 `Hl/Hc/Hr`，跨度使用两端实测点的欧氏距离。四条边的弯曲量使用中点到对应端点连线的有符号垂距，向图卡内部为正。

| 方案 | 水平或左右 | 垂直或上下 |
| --- | --- | --- |
| 标准 TV (%) | `100 × ((Wt+Wb)/2-Wm)/Wm` | `100 × ((Hl+Hr)/2-Hc)/Hc` |
| 半值 TV (%) | 标准水平 TV 除以 2 | 标准垂直 TV 除以 2 |
| 两对边参考九点 (%) | 左右弯曲量除以 `(Wt+Wb)/2`，乘 100 | 上下弯曲量除以 `(Hl+Hr)/2`，乘 100 |
| 两对边参考梯形 (%) | `KeystoneHoriz = 100 × (Hl-Hr)/((Hl+Hr)/2)` | `KeystoneVert = 100 × (Wt-Wb)/((Wt+Wb)/2)` |
| 旧本地九点 (%) | 宽度分母使用 `(Wt+Wm+Wb)/3` | 高度分母使用 `(Hl+Hc+Hr)/3` |

旧本地梯形字段还保留既有命名：`KeystoneHoriz` 对应上、下宽度差，`KeystoneVert` 对应左、右高度差。它与两对边参考口径的轴名不同，不能只改分母后沿用字段解释。旧口径兼容的是本仓库 `distortion_p9.cpp` 的数学约定；使用 V2 定位器后，点位和最终数值不保证与旧定位器逐位一致，也不宣称等同于不可见的供应商服务实现。

### 相对光学估计

`CenteredProjectiveBrownK1/v2` 假设图卡为等间距平面点阵，光学中心位于实测中心点。用全部点联合拟合无径向畸变的投影参考 `p = H(u,v)` 与一阶 Brown 模型 `p_actual = p × (1 + k1 × |p|²)`，两者均以中心点为原点。中心相邻点只提供优化初值，最终节距来自拟合的投影局部导数，避免把已畸变的中心节距直接当作理想放大率。参考包含透视；径向模型作用于投影后的图像坐标。

每个非中心点按 `100 × (AD-PD)/PD` 计算径向百分比，`AD` 为实测半径，`PD` 为拟合参考半径；按最大绝对**百分比**选择点并保留符号（正值为枕形、负值为桶形）。分析版本为 `point-grid-metrics/2`，TV 与两种九点公式不变；JSON 保留原有光学字段并新增 `RadialCoefficientPerPixelSquared`、`FitRmsPixels`、`MaxResidualPixels`、`FitResidualFraction` 和 `FitIterations`。列、行节距表示中心处的无径向畸变局部导数，逐点参考位置包含投影，不能用这两个向量线性外推全图。

优化未收敛、参数不可辨识、投影退化、径向映射非单调，或模型 RMS 超过最小实测相邻点距的 1%、单点残差超过 3% 时，`IsAvailable = false`，数值为空并给出原因。残差门限衡量模型与点位的符合程度，不是计量精度承诺；失效不阻断 TV/九点结果，Flow 即使开启光学输出也不会发布失效的光学项。拟合成功时结果窗口显示像素残差与迭代次数，分析 JSON 保存诊断。

该参考从单张图估计，`IsCalibrated` 固定为 false。模型可在居中一阶径向假设成立时恢复已知畸变并分离透视，但不估计光轴偏心、切向或高阶畸变；九点约束有限，低残差也不能证明假设成立。数值仅覆盖实测点阵范围，不能当作整幅图边缘畸变。独立标定及图卡/光轴信息仍用于验证实拍图的物理准确性，不能仅凭 TV 或四边畸变换算。

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
