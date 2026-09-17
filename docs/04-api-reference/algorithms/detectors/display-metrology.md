---
knowledge_id: "algorithms.display-metrology"
knowledge_type: "topic"
status: "current"
summary: "本地显示图案计量：RGB套色、九点十字RGB分离、鬼影候选、亮暗点/线缺陷/Mura、双目信号与几何、Eyebox扫描和全视场斜边SFR；公开原理与可复现合成样本，不承诺现场精度。"
aliases: ["RGB套色", "RGB分通道", "九点十字", "RGB分离", "横向色差", "Eyebox", "眼盒", "低灰阶Mura", "显示计量", "全视场清晰度", "左右眼对准", "DisplayMetrologyProvider", "generate_display_metrology_samples"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/DisplayMetrology", "UI/ColorVision.ImageEditor/EditorTools/Algorithms/DisplayMetrologyEditorTool.cs", "UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs", "Scripts/generate_display_metrology_samples.py"]
test_paths: ["Test/ColorVision.UI.Tests/DisplayMetrologyTests.cs", "Test/ColorVision.UI.Tests/ImageAlgorithmPlatformTests.cs", "Test/ColorVision.UI.Tests/AlgorithmReleaseGateTests.cs"]
related: ["algorithms.platform", "algorithms.local-native-analysis", "algorithms.fov-local"]
---

# 显示图案计量

显示计量提供七个可执行的本地分析入口，用于离线评价 AR 波导、Micro OLED/Micro LED 与双目整机的指定测试图案。九点十字入口位于图像右键 **算法调用**，其余入口位于 **算法 → 显示计量**，通过统一 Catalog、Runner 和中立结果 artifact 执行，结果窗口提供测量汇总、逐项表格、图像、JSON/CSV 导出及临时叠图。

当前输出是像素坐标和经指定指数解码的相对设备信号。它们不带亮度/色度标定、角度标定、客户 Recipe、Engine 历史结果落库或硬件扫描。九点十字的 OK/NG 只是相对用户输入像素阈值的本次分析判定，不等于客户 Recipe 或量产结果。没有现场图像时，可以用公开原理及已知真值的合成图验证计算。**合成测试不证明真实模组的检出率、重复性、绝对测量精度或标准符合性。**

## 输入与操作

1. 打开对应测试图像。九点十字可框选矩形搜索区域，也可明确选择全图；已绘制的矩形右键也可执行九点十字。区域应包住完整九点图案并保留背景边距。其他功能先裁到完整图案或有效均匀显示区。
2. 九点十字在 **算法调用** 中选择“框选矩形”或“全图”；其他功能在 **算法 → 显示计量** 中选择。参数编辑是事务式提交，取消不运行。
3. 设置输入解码指数：线性信号为 `1`；仅在编码是已知幂函数时指定对应指数。普通照片、自动曝光、局部色调映射或显示拉伸不自动变成线性测量输入。
4. 选择图案网格、检测阈值或扫描清单。双目模式将当前图像作为 `left`，再选择 `right`。
5. 查看汇总和逐项有效状态。无效点以原因及空值输出，不能解释为零误差；九点十字即使全部无效也保留诊断表，此时没有分离最大值/RMS；其他模式全部无效时失败。

支持 Gray8/16/32F 与 BGR/BGRA 8/16/32F；两种 RGB 几何功能都要求彩色输入。浮点样本须有限且处于 `[0,1]`，四通道须完全不透明。灰度图取唯一通道，其他相对信号测量默认取 G 通道，可切 B/R；不将相机 RGB 或 G 称为 CIE Y、xy 或 ΔE。

单帧尺寸至少为 32×32。九点十字允许单帧最多 67,108,864 像素 / 512 MiB：搜索区域最长边按整数倍率池化到不超过 1600 个采样格，之后逐目标读取原分辨率 ROI，每个 ROI 最多 8,388,608 像素。其他功能单帧最多 8,388,608 像素，总输入最多 33,554,432 像素和 512 MiB。多帧要求同尺寸、同格式及相同且非空的编码标签，不自动缩放、配准或曝光归一化。实际导入也检查像素预算。JSON 扫描清单限 64 KiB，候选连通域和累计缺陷最多 2048；超限拒绝，不将截断结果报告成成功。

## 功能与测量口径

| 菜单 | 稳定 ID 后缀（前缀 `colorvision.display.`） | 输入与输出 |
| --- | --- | --- |
| RGB 图案套色 | `rgb-registration` | 单幅彩色图，每格一个亮目标；G 为参考，输出 R−G、B−G 的原图坐标差、距离和矢量叠图 |
| 九点十字 RGB 分离 | `rgb-cross-registration` | 自动定位 3×3 亮十字阵列；输出 R/G/B 定位预览、原分辨率臂边缘、通道偏移、逐点质量与可选阈值结果 |
| 鬼影与杂散光评价 | `ghost-measurement` | 单图与显式主像矩形/背景；输出外部候选位置、面积、峰值比和积分比 |
| 亮暗点 / 线缺陷 / Mura | `defects` | 均匀场；输出两种空间尺度上的缺陷候选、分析边界与区域框图 |
| 左右眼对准与信号一致性 | `binocular-quality` | 两图；逐格原始视差、相似变换尺度/旋转/残差及各通道相对信号比 |
| Eyebox 扫描评价 | `eyebox-scan` | 固定姿态下已知 XY 规则位置的多图；输出采样点覆盖率、相对信号和四角满足阈值的网格面积 |
| 全视场斜边 SFR | `field-sfr` | 每格一条斜边；输出逐格 MTF 曲线、MTF50、边缘拟合质量及格点分布图 |

### RGB 与双目几何

图像按给定行列等分，每格至少 24×24 像素。目标定位用本格最小值作为背景、峰值跨度乘目标阈值提取四连通区域，再求背景扣除后的强度质心。像素中心原点在左上，x 向右、y 向下。主要目标面积至少 3 像素；存在强度积分超过最大目标 5% 的第二个合格目标时拒绝，目标触边时拒绝，低于最小信号跨度时拒绝。

这适用于已知行列、每格一个完整亮点/亮十字/孤立亮图形；不提供自然图像匹配、任意点阵索引恢复或不同靶标形状间的对应保证。阈值截取质心会受光斑形状影响；每视场的像素位移要结合相机光学畸变及角度标定才能转成模块色差角。

九点十字模式（算法版本 `1.2.0`）先对各颜色通道分别最大值池化，再用通道并集定位搜索区域内候选，不依赖整幅图九等分。候选至少包含 5 个采样像素且宽高均不少于 3 个采样格；细于此尺度的图案不在当前检测能力内。候选按互不重叠的行/列包络分成 3×3，按从上到下、从左到右编号 P1–P9。某个交点缺失时保留该点为空；无法确定三行三列时拒绝整阵列索引，不猜测缺失行的位置。多目标、额外候选、排序歧义、ROI 与其他目标相交以及触边均有独立原因。

搜索矩形至少为 32×32 像素且必须完整位于原图内；框选坐标按当前帧 DPI 换算为像素，左上取 floor、右下取 ceil。只读取搜索区域内的信号，框外反光及其他目标不参与定位。输出 ROI、边缘、轴和叠图仍是原图坐标，定位预览通过 `sourceOriginX/Y` 和 `sourcePixelsPerPreviewPixel` 标明偏移与倍率。缩小搜索区域会改变定位采样倍率，因此候选包络与边缘公共采样位置可能变化；原分辨率边缘测量方式不变。框选不裁改原图，输入快照及尺寸预算仍按整帧计算。

每个目标 ROI 内分别读取原始 R/G/B 信号。行列强度投影仅用于确认唯一的窄轴带；测量在各半臂的公共采样位置进行，取候选跨度的 10%–25% 和 75%–90%，避开端点衰减及交叉中心。每个截面按自身背景到峰值的比例阈值线性插值边缘，避免强横臂的峰值截掉弱竖臂；多阈值段截面剔除，每条半臂均须满足可配置的最小覆盖率（默认 0.5）。对应边缘为有效截面的中位数，轴位置为两侧中位边缘的中点。这是近水平/垂直十字的采样带代表值，不是旋转直线拟合或逐像素最大色边；明显倾斜、弯曲、宽条纹及行列包络重叠不承诺正确索引/测量。目标阈值、最小跨度和覆盖率是检测参数，不是产品合格阈值。

`RGB-cross-separation` 保留每通道边缘/轴坐标、R−G/B−G、覆盖率、剔除数、饱和样本数及逐点原因。`RGB-cross-profile-quality` 可追溯采样位置、阈值段数和有效截面边缘。缺失、低对比度、臂覆盖不足和多峰歧义输出 `INVALID` 与空分离值；有效通道仍保留自身坐标及叠图，不能把部分通道成功当作三通道有效。饱和样本给出警告：其阈值边缘可能有偏，不据此宣称测量精度。没有有效点时仅输出计数和诊断，不输出虚构的零最大值/RMS。

允许的最大边缘分离默认留空，结果为 `MEASURED`，不输出整图合格结论。显式配置产品规格后，R/G/B 对应四条边缘最大极差小于或等于该值为 OK，任意点 NG 或无效时总体为 NG；已有数值阈值 JSON 仍可读取。R/G/B artifact 是带采样倍率元数据的 8-bit 最大值池化定位预览，不能用于亚像素计量；所有测量和叠图坐标来自原分辨率信号。

ImageView 独立执行不入库；结果窗口关闭后保留 `rgb-cross-measurements` 持久叠图，由用户清除，换图时按文档生命周期移除。“导出九点 JSON”输出 `colorvision.rgb-cross-measurement` / `1.0.0`，坐标均为原图像素；固定导出字段不含产品阈值、OK/NG 或内部截面诊断。公共 artifact 导出仍可用于内部诊断。

Flow 使用 `LocalRgbCrossNode`，复用 FindCross 模板（模板字典 45）并从可选模板的 `rgbCross` 参数段读取检测设置；未选模板使用默认测量参数。旧单十字参数不能隐式转换，选中的模板缺少 `rgbCross`、字段未知或带产品合格阈值时停止。搜索区域优先读取发光区 POI，其次固定矩形，最后整图。结果文件写入配置目录（默认 `%LOCALAPPDATA%\ColorVision\Results\FindCross`），主记录复用 `FindCross=63`、版本 `2.0`，保存 `TId/TName/BatchId/Zindex/ImgFile`，公共明细保存 `ResultFileName`；主表与明细使用同一事务，入库失败清理新建 JSON。检测完成但部分点无效仍保存可复核的 `INVALID` 测量，不伪造产品通过结论。只有持久化成功后才设置下游结果 ID 并发布通知。ARVRPro 的九点解析按同一类型和版本读取，原单十字使用原格式。

可独立交付的 C++ 测量实现位于 `Native/opencv_helper/algorithm/find_cross/rgb_cross.h/.cpp`，无 WPF、数据库或合格判定依赖，使用 C++17 与 nlohmann/json；`Test/opencv_helper_test/rgb_cross_standalone` 可用 CMake 编译 CLI 和合成自测。生产 ImageView/Flow 目前继续调用托管实现，C++ 是同口径独立入口，并非现有 `M_FindCrossLocal` 导出的替换。交付验证应逐点对比两端几何、有效性和分离值，不能只比较汇总计数。

没有直接调用单十字 `FindCrossLocal`：`pattern_cross.cpp` 的 `ConvertToGrayFloat` 会混合颜色，而 `PatternCrossResult` 提供轴交点、角度、端点和臂质量，不提供通道对应的双侧阈值边缘。其 `SampleAxis` 内部有原图背景滤除、单目标候选及光学坐标语义，也不是可直接调用的中立边缘 API。这里采用独立的逐通道截面质量检查与中位数测量；单十字功能及 Engine 结果链保持独立。当前没有像素到角度的自动换算，不能替代相机/镜头色差基线与现场误报漏报验收。

合成回归在 `DisplayMetrologyTests.cs`、`DisplayRgbCrossTests.cs`，包含平移紧凑阵列、相同外轮廓的臂分离、弱竖臂、缺点、缺通道、额外目标、触边、排序歧义，以及局部次峰剔除和逐半臂覆盖门槛。`DisplayRgbCrossSiteTests.cs` 是显式启用的 CVCIE v2 / BGR16 只读现场路径；默认跳过，不把缺少现场文件算作已验证。它使用生产 Runner，保存原文件前后 SHA-256、参数、每点 JSON/CSV、汇总、截面质量、通道预览及原图/叠图对照。PowerShell 示例：`./Scripts/validate_rgb_cross.ps1 -Source C:/samples/array.cvraw -OutputDirectory C:/validation/rgb-cross`。工作树没有 native DLL 时，可显式传入 `-OpenCvHelperBinary` 指向已有匹配 DLL；脚本不下载、不发布，也不改原文件。现场图没有人工真值，测试完成仅代表路径执行和原文件未变，不保证九点全部有效或满足产品规格。

双目至少需要三个有效且非共线的对应目标。用最小二乘拟合左图到右图的相似变换，同时保留原始逐格位移与拟合残差；不把对齐后的零误差当作产品误差。旋转正值为图像坐标下的顺时针。当前不做稳健外点剔除，残差须结合格点表解释。只比较均匀白场时关闭“测量目标位置”；信号比仍要求相同采集条件，彩色图额外给出 B/G/R 各通道比值，不输出校准色差。

### 鬼影与杂散光

主像矩形用全图归一化坐标指定，外部区域不能为空。背景为用户提供的解码后相对信号，建议来自独立暗场或已知空白区域。逐像素扣背景并将负值截为零；主像出现满量程像素或主像无有效信号时拒绝。

候选以 `RelativeThreshold × 主像区域峰值` 分割主像外信号。峰值比的分母是主像峰值，能量比的分母是主像矩形内积分；另外报告整个外部区域积分比与平均值比，两者有不同面积含义。候选形态区分连接泛光、细长光条和离散鬼影/杂散光，但不能仅凭一幅图确定光学成因；外部亮目标、相机自身杂散光和真实模组鬼影仍须通过测试图案及参考测量区分。

### 屏体缺陷

用 σ=2 像素的平滑图提取窄尺度残差，用配置背景尺度的高斯平滑图提取宽尺度残差：

- 窄尺度 `原图−σ2平滑`：按面积识别亮/暗点候选，按长度与长宽比识别亮/暗线候选。
- 宽尺度 `σ2平滑−背景平滑`：按面积识别 Mura 候选，排除细长区域。
- 两种残差均分别处理正负极性，阈值为绝对下限与相对背景阈值中的较大值。

排除宽度等于背景尺度的边界，并在结果中记录有效像素数；触及分析边界的候选标记为截断。背景尺度应大于待检异常尺度，过大范围的缓慢不均匀可能被背景模型吸收。输出的候选图是区域包围框图，不是像素分割真值；相邻缺陷可能合并，两尺度也可能对同一异常产生候选。面积单位是相机图像像素，不等于屏体物理像素或子像素数量。低灰阶检测需有合理噪声下限及相机坏点/暗场/平场校正。

### Eyebox

先在已知步距、相同 eye relief、相同姿态和曝光设置下采集规则 XY 网格。图像必须使用同一坐标方向和相同视场采样，算法不代替运动控制或瞳孔标定。提供清单：

```json
{
  "schemaVersion": 1,
  "parameters": {
    "columns": 2,
    "rows": 2,
    "stepXMillimeters": 1,
    "stepYMillimeters": 1,
    "referenceIndex": 0
  },
  "frames": ["r0c0.png", "r0c1.png", "r1c0.png", "r1c1.png"]
}
```

帧列表按行优先排列，路径相对清单目录；禁止重复文件或缺失位置。运行请求将它们命名为 `sample-0` 起的连续序列。参考位置坐标为零，x/y 坐标由行列与显式步距计算。必须选择可代表预期完整视场的参考图，参考帧中低于 `ReferenceSignalFloor` 的像素不属于本次有效测量域。

每点用参考有效域内平均信号比和逐像素信号比覆盖率评价。一个网格仅在四个角的采样点均满足阈值时计入 `four_corner_accepted_mesh_area`。这个值是指定阈值下的采样网格面积，不是连续 Eyebox 边界、不证明未采样内部合格，也不会跨越中心空洞计算包围框面积。需要连续边界时应加密扫描或另行定义有验证依据的插值模型。

### 全视场 SFR

每格至少 40×32 像素（水平模式先转置），包含一条斜率绝对值 0.02..0.35 的独立斜边，边缘两侧需保留约 15 像素支撑。用行梯度质心拟合边线，将样本沿边法线投影到 4 倍过采样 ESF，中心差分为 LSF，再加 Hamming 窗计算 DFT 和差分响应修正。固定支撑为边缘两侧约 12 像素，输出到 0.5 cycles/pixel，步距为 1/128 cycles/pixel。

结果中 `MTF50` 是 Nyquist 以内第一次下降穿过 0.5 的频率；未穿过时保留空值及原因，不伪造零或外推频率。多边缘、噪声过大、无斜率、低对比度、非直线和亚像素覆盖不足会拒绝该格。当前是有限图案范围的本地评价实现，不宣称 ISO 12233 符合性，不代替现有原生 SFR 的全部能力；无任意靶标自动寻边、扫描焦点控制、测量链去卷积或 cycles/degree 自动换算。

## 公开依据与可复现数据

本实现独立编写，公开资料用于原理和范围参考，没有复制商业 SDK 或将网络截图当作已标定测试数据：

- [OpenCV 连通域与图像矩](https://docs.opencv.org/4.x/d3/dc0/group__imgproc__shape.html)：区域、面积及质心基础。
- [FDA 近眼显示横向色差与眼点定位方法](https://cdrh-rst.fda.gov/head-mounted-display-eye-box-centering-using-transverse-chromatic-aberrations-0)：说明眼点位置对色差测量的影响；当前实现不包含该工具的完整定位程序。
- [Radiant 近眼显示检测项目](https://www.radiantvisionsystems.com/industries/augmented-virtual-reality)：鬼影、均匀性与显示缺陷的应用范围。
- [Gamma Scientific 近眼扫描测量](https://gamma-sci.com/products/ned-ar-vr-testing-collections/ned-lmd-e-series/)：Eyebox、视场、双目视差及其采集条件。
- [Imatest 斜边计算验证](https://www.imatest.com/imaging/validating_slanted_edge/)与[斜边测量原理](https://www.quickmtf.com/slantededge.html)：ESF/LSF、MTF 及独立验证方法。

在仓库根目录用 PowerShell 运行，无第三方 Python 包、网络访问或硬件动作：

```powershell
python Scripts/generate_display_metrology_samples.py
```

脚本写入 `artifacts/display-metrology/samples`：线性 16-bit 灰度 PNG、RGB 图、左右眼图、鬼影图、低灰阶缺陷图、斜边图、Eyebox 清单、真值 JSON、SHA-256 和操作说明。默认拒绝覆盖已有文件；只有明确指定 `--overwrite` 才替换这些生成文件。数据均为可控合成，不来自真实模组。

独立公开输入可使用 Imatest 验证页面提供的 [Slanted edge verification patterns 图包](https://www.imatest.com/wp-content/uploads/2012/02/Slanted_edge_verification_patterns.zip)。这也是数字生成的验证图，不是模组现场照片。图包包含 2953×981 的不同模糊程度图像，可在右侧弱抗锯齿竖直斜边取原图矩形 `(2515,555,165,220)`，裁图后以 1 行 × 1 列、解码指数 1 运行 SFR。核对模糊增加时曲线下降及 MTF50 趋势；原始锐利图未在 Nyquist 以内穿越 0.5 时应保留空值。保留来源、下载包哈希、ROI、参数及完整曲线；这个检查不等于与厂商软件逐值一致或现场精度验收。

## 验证

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~DisplayMetrologyTests|FullyQualifiedName~AlgorithmReleaseGateTests|FullyQualifiedName~ImageAlgorithmPlatformTests"
```

用例覆盖已知位移/旋转/倍率、不同位深、强度比、点线与 Mura 位置、Eyebox 空洞与网格面积、高斯理论 MTF50、无效样本、预算、取消、PNG 导入和结果窗口所有权。测试存在或文档构建成功不等于已经运行；本次执行结果以实际日志为准。现场仍需验证采集重复性、不同模组/背景/缺陷尺度、误报漏报、运动扫描标定以及完整量产结果交接。
