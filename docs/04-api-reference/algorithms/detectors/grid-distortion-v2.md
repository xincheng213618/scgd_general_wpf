---
knowledge_id: "algorithms.grid-distortion"
knowledge_type: "topic"
status: "current"
summary: "本地点阵畸变 V2 单次定位、TV/九点多口径及相对光学估计，覆盖 ImageView、Flow 和 ARVR 2.0 适配；光学估计不等同于标定结果。"
aliases: ["点阵畸变分析(V2)","本地点阵畸变(V2)","漏光畸变失败","7x7畸变","GridDistortionAnalysis","LocalGridDistortionNode","M_CalDistortionGridV2","HorizontalTVDistortion","VerticalTVDistortion","Optic_Distortion","KeystoneHoriz","KeystoneVert","DIFF_H","DIFF_V"]
code_paths: ["Native/opencv_helper/algorithm/distortion","Native/include/opencv_media_export.h","Native/opencv_helper/opencv_media_export.cpp","UI/ColorVision.Core/GridDistortion.cs","UI/ColorVision.Core/GridDistortionAnalysis.cs","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/GridDistortion","UI/ColorVision.ImageEditor/EditorTools/Algorithms/Calculate/AlgorithmResultOverlay.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalGridDistortionNode.cs","Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewHandleDistortion.cs","Engine/ColorVision.Engine/Templates/Jsons/Distortion2","Projects/ProjectARVRPro/Process/Distortion"]
test_paths: ["Test/opencv_helper_test/test_grid_distortion_v2.cpp","Test/opencv_helper_test/benchmark_grid_distortion.py","Test/ColorVision.UI.Tests/GridDistortionTests.cs","Test/ColorVision.UI.Tests/GridDistortionAnalysisTests.cs","Test/ColorVision.UI.Tests/GridDistortionRealSampleTests.cs","Test/ColorVision.UI.Tests/LocalGridDistortionNodeTests.cs"]
related: ["algorithms.arvr","algorithms.find-light-area","algorithms.find-cross","engine.native-integration","engine.results","flow.node-extension"]
---

# 本地点阵畸变 V2

V2 用一次点阵定位得到完整点位，再由 `GridDistortionAnalysis.Calculate` 同时计算 TV 的两种口径、九点的两种口径和中心节距参考的相对光学估计。ImageView 展示全部方案；Flow 节点按参数选择写入 ARVR 的字段，同时保留全部分析。参数选择不重新找点。

输入是完整的规则圆点阵，行列分别为 3～15 的奇数，可以是 3×3、7×7 或非正方形奇数阵列。多点图卡的九点指标取首行、中行、末行与首列、中列、末列的交点。缺点时拒绝计算，不用拟合点冒充实测点；当前不计算左右眼 `DIFF_H`/`DIFF_V`，这还需要配对输入和明确差值定义。

## 使用入口

### ImageView

打开图像，在整图或矩形区域的右键 **算法调用 → 点阵畸变分析(V2)...** 设置行列数等参数并计算。菜单通过 `IIEditorToolContextMenu` 发现，无需在 `ImageView.xaml` 硬编码。结果窗口包含多口径指标、光学估计及 JSON，可复制分析结果；图上显示点位及代表九点。图像被替换或较新任务启动后，过期计算不能覆盖当前叠图。

原有 **9点畸变分析** 保留原行为。它的原生 `M_CalDistortionP9` 使用阈值分割和尺寸筛选，指标函数只处理九点；直接将旧配置改为 7×7 不能获得有效的 49 点畸变指标。

### Flow 和 ARVR

使用 **本地点阵畸变(V2)** 节点，输入、结果目录和搜索区域沿用 Engine 本地节点约定。内存帧优先，文件为后备；彩色帧按 BGR 灰度化，CIE 取 Y 平面，输入只读。借用帧必须满足已有方向标记，节点不重复翻转。ROI 输出坐标还原到整图。

| 输出参数 | 默认值 | 作用 |
| --- | --- | --- |
| `TvFormula` | `Standard` | 选择标准 TV 或其半值 |
| `Point9Formula` | `OppositeEdgeMean` | 选择两条对边均值参考口径或旧本地三条跨度均值口径 |
| `PublishOpticalEstimate` | `false` | 明确启用后才将相对估计映射到既有 `Optic_Distortion` 字段 |

流程执行会写入既有结果数据库和节点结果目录，需要已有流程批次及数据库连接；ImageView 单次分析不要求数据库。成功记录的类型为 `Distortion`（9）、版本为 `2.0`，一条 `DetailCommon` 指向唯一结果 JSON 文件。文件中的 `TV_distortion`、`Point9_distortion`、`Optic_Distortion` 保持 `Distortion2View` 与 ProjectARVRPro 消费的结构，数值已经是百分数，不再乘 100。

全部分析同时保存在 `LocalGridDistortionAnalysis`、结果文件和主记录参数中。默认 `Optic_Distortion` 为 null；显式启用后仍标明是未标定估计，并省略含义未确认的 `t`。CSV 对此缺失项留空，不补零。原有客户配方、判定限和协议字段由 ProjectARVRPro 负责。

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

`CentralPitchRadial/v1` 用实测中心为原点，中心相邻点差分的一半作为两个节距向量，按行列索引外推参考点。每个非中心点按 `100 × (AD-PD)/PD` 计算径向百分比，`AD` 为实测半径，`PD` 为外推参考半径；汇总保留绝对值最大点的有符号结果、点号及所有逐点数值。

该参考从同一张图估计，`IsCalibrated` 固定为 false。它能用于同口径比较，但不是有标定依据的绝对光学畸变；中心节距本身可能有畸变，透视、偏轴和镜头作用也没有独立分离。需要与供应商光学指标等价时，应补充真实图卡节距/视场、成像标定及原始算法定义，不能仅凭 TV 或四边畸变换算。

公式参考 [Edmund Optics 的径向与 TV 畸变说明](https://www.edmundoptics.com/knowledge-center/application-notes/imaging/distortion/)；规则点阵检测参考 [OpenCV findCirclesGrid](https://docs.opencv.org/4.10.0/d9/d0c/group__calib3d.html)，畸变参考网格与标定方法可参阅 [Discorpy 方法文档](https://discorpy.readthedocs.io/en/latest/tutorials/methods.html)。本实现未移植 Discorpy 的完整标定模型，附件中的公式也不作为行业标准认证依据。

## 定位、质量与接口

V2 先缩小图像作粗定位，通过局部背景扣除与局部对比归一化减轻漏光及高亮反光影响，生成圆点候选；结合行列拓扑和拟合残差选择完整点阵，再在原图局部窗口细化点中心。全局亮度阈值和固定像素面积不再是唯一选点条件，仍保留明确的对比度和几何质量门限。过强非投影变形、遮挡或缺点仍可能拒绝，质量分数不是正确率概率。

| Core 参数 | 默认值 | 范围 |
| --- | --- | --- |
| `ExpectedRows` / `ExpectedCols` | 3 / 3 | 3～15 的奇数 |
| `BrightTarget` | true | 亮点或暗点 |
| `MaxProcessingSize` | 1600 | 256～4096 px |
| `MinimumContrast` | 0.02 | 0～1 |
| `MaximumGridResidualFraction` | 0.3 | 大于 0 且不超过 1，相对网格间距 |

Flow 当前直接开放行列数、搜索区域和最小对比度，其他检测参数使用 Core 默认值。原生导出 `M_CalDistortionGridV2` 接收 `HImage`、`RoiRect` 和 UTF-8 JSON，返回 JSON 缓冲区并由 `FreeResult` 释放。原有 P9 ABI 保持独立；调用 V2 需要同时交付具有新导出的 `opencv_helper.dll`。包装层校验结果完整性和有限值，DLL、入口、解析及释放错误返回失败，不能作为有效的零畸变。

## 验证和复现

原生定向测试覆盖输入类型、亮暗点、ROI 原图坐标、漏光、强反光、缺点拒绝及几何公式；托管测试覆盖 ABI 包装、多口径计算、结果窗口、Flow 参数快照与 ARVR JSON 映射。测试目录的基准脚本分别调用同一 DLL 的旧、新 ABI，用固定种子合成图及可选实图比较。

以下命令只构建/运行本地测试并写入指定输出，不运行设备或生产数据库。Python 脚本需要已安装的 NumPy 和 OpenCV Python；DLL 先按原生构建约定编译 Release/x64。

```powershell
python .\Test\opencv_helper_test\benchmark_grid_distortion.py --dll .\x64\Release\opencv_helper.dll --sample C:\Samples\distortion.cvraw --output .\artifacts\grid-distortion-benchmark

$env:COLORVISION_GRID_DISTORTION_SAMPLE = 'C:\Samples\distortion.cvraw'
$env:COLORVISION_GRID_DISTORTION_EVIDENCE_DIR = '.\artifacts\grid-distortion-evidence'
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -c Release -p:Platform=x64 --filter 'FullyQualifiedName~GridDistortion'
```

真实样本检查默认按 3×3 运行；未指定样本时，可设置 `COLORVISION_RUN_GRID_DISTORTION_NATIVE_TESTS=1` 运行固定合成 7×7 及实际 WPF 结果窗口渲染。普通托管测试默认跳过该原生集成项。窗口证据是测试自行创建的 WPF 内容，不是现有用户窗口截图。

合成正确性门限同时要求完整行列对应、最大点误差不超过 1 px、有效跨度、按各自公式计算的最大指标误差不超过 0.1 个百分点；不能只数 `success=true`。性能包含原生调用、JSON 字节复制和释放，不含读图、JSON 解析、派生分析、UI 或数据库。单张实图和合成扰动只能验证这些案例，不能推算量产失败率或承诺固定加速倍数。
