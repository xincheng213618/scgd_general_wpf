---
knowledge_id: "algorithms.image-registration"
knowledge_type: "reference"
status: "current"
summary: "ImageRegistration 的输入、参数、结果、宿主接入与定向验证契约。"
aliases: ["图像配准如何求变换和输出配准质量","ImageRegistration","ImageRegistrationAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/ImageRegistrationAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageRegistrationV1Tests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 图像配准 V1

## 适用范围

`colorvision.geometry.registration` 把 `moving` 图像变换到 `reference` 的 pixel-center 坐标系，复用[几何变换](./geometric-transform-v1.md)的矩阵数值校验、warp 和有效区域规则。镜头参数由[镜头畸变校正](./lens-distortion-correction-v1.md)处理。

Descriptor、Invocation、preset 与 Result 保持宿主中立。OpenCV CPU provider 通过 `AlgorithmImageMatLease` 只读借用两幅输入；输入不复制为中间 `byte[]`。输出在 native warp 后复制一次进入 Result 所有的 `AlgorithmImageBuffer`，另生成一字节每像素的有效区域 mask。V1 不修改 reference 或 moving。

## 输入、格式与坐标契约

Invocation 必须恰有两个具名输入：`reference` 和 `moving`。二者必须采用相同的规范格式和显式相同的 `ColorSpace` 标签；不进行隐式位深、通道或色彩空间转换。支持 Gray8、Gray16、Gray32Float、Bgr24、Bgr48、Bgr96Float、Bgra32、Bgra64、Bgra128Float，保留 moving 的格式、通道、直通 alpha 和数值范围，输出 DPI 使用 reference 的坐标标定。

坐标为统一 pixel-center 坐标：整数 `(x,y)` 表示像素中心，正方向向右/向下。返回的 3×3 row-major 矩阵始终是 `moving → reference`，逆矩阵是 `reference → moving`。`valid-region-mask` 为 Gray8；255 表示 reference 输出像素中心经逆矩阵落入 moving 的闭合范围 `[0,width-1] × [0,height-1]`，否则为 0。

V1 不接受 ROI。双输入 ROI 若只携带一套坐标会产生歧义，Runner 因而返回结构化 `roi_kind_unsupported`；后续如需 ROI 配准，必须先定义 reference/moving 各自区域及对应规则。

## 配准方法

### PhaseCorrelation

两幅图必须同尺寸。provider 按格式标称峰值把亮度归一化到 0..1，可选 Hann 窗，然后使用相位相关估计循环平移。报告的 `phase_shift_x/y` 是 reference 到 moving 的观测位移，因此输出矩阵使用其相反数把 moving 拉回 reference。执行前同时检查两幅图的纹理标准差；常量/近常量输入返回 `phase_insufficient_texture`。相关峰唯一性在最多 512×512 的有界诊断面上计算，并排除主峰的环绕邻域；周期纹理的次峰与主峰过近时返回 `phase_ambiguous_texture`，不会把任意一个等价平移当作可靠结果。`MinimumPhaseResponse` 与 `MaximumTranslationPixels` 继续作为结构化质量门禁；只有纹理与峰唯一性先通过后，完全相同的输入才走精确 identity。

PhaseCorrelation 不再把无量纲的 `1-response` 标成像素几何 RMSE。结果改报 `correlation_loss`（ratio）与 `phase_peak_uniqueness`（ratio）；没有点对应关系时 transform geometry 的 Residual 为 null。ORB 仍以真实内点重投影误差报告 `geometric_rmse`（px）。Phase 置信度由有界 response 与峰唯一性共同形成，表示确定性质量启发值，不是校准概率。

### OrbHomography

reference 与 moving 可以不同尺寸。provider 使用 ORB、双向最近邻与 Lowe ratio 得到互相一致的匹配；再用固定种子的均匀确定性四点采样计算共识内点，最后用全部内点最小二乘求单应矩阵。采样去重且覆盖全部匹配 rank，不会像旧字典序截断那样让 rank 0 固定进入所有候选；单个早序 outlier 不能垄断共识。该过程不使用随机 RANSAC，因而在同一输入和参数下可重复。

持久参数 schema v1 仍可读取历史上限，但执行前按最坏情况检查工作预算：每幅最多 5,000 个 ORB 特征、双向暴力匹配最多 50,000,000 次 descriptor comparisons、共识最多 2,000,000 次 candidate×match 评估；超限在创建 ORB/native matcher 前返回 `registration_work_budget_exceeded`。双向 KNN 按 256 个 query descriptor 分块，可在块间取消。共识复用四点缓冲并流式累计内点/残差，不再为每个候选分配完整 `RegistrationMatch[]`。低纹理、匹配不足、共识不足、病态矩阵或穿越投影无穷远会返回结构化失败，不返回貌似成功的结果。

`confidence` 是由响应或内点比例、描述子质量与残差形成的有界质量启发值，不是经过标定的概率。

## Result artifact

| Artifact | 内容 |
| --- | --- |
| `registered-image` | role=`primary`，moving 配准到 reference 尺寸，保持 moving 格式，使用 reference DPI |
| `valid-region-mask` | role=`validity-mask`，Gray8 有效像素中心掩码 |
| `image-registration-summary` | 尺寸、Phase 的平移/response/峰唯一性/correlation loss、ORB 的匹配/内点/几何 RMSE、光度 RMSE、置信度、有效比例、行列式、条件数、正逆残差 |
| `image-registration-matrix` | moving→reference 正矩阵及 reference→moving 逆矩阵 |
| `image-registration-matches` | moving/reference 匹配点、描述子距离、重投影残差与内点标志 |
| `image-registration` Geometry | Transform、moving 变换后轮廓与受限数量的内点；坐标空间为 Pixel |
| `image-registration-overlay` | transient 轮廓/内点显示声明 |
| `image-registration` StructuredData | `colorvision.geometry.registration/v1`，记录方法、输入格式/来源、矩阵、mask 规则、参数、preset 与算法版本 |

光度 RMSE 只在有效 mask 内以归一化亮度计算；ORB 几何 RMSE 以 inlier 的 reference 像素坐标计算，Phase 不产生伪造的像素残差。正逆残差是 `maxAbs(M × inverse(M) - I)`。

## Preset 与宿主接入

`ImageRegistrationPresetSerializer` 保存并严格验证 AlgorithmId、算法版本、参数 schema 版本、PresetId 与参数 JSON，拒绝缺失版本、其他算法或未来 schema 的 preset。

- ImageView：“算法 → 图像配准...”以当前图像为 reference，选择文件作为 moving；参数窗口可加载/保存不覆盖已有文件的 preset。统一 analysis session 负责取消、文档 revision 和 latest-wins。结果窗口显示配准图、mask、矩阵、匹配表和 transient overlay，可保存 PNG、导出 CSV/JSON；关闭窗口同时释放 overlay 和 Result 图像。
- Flow：`LocalFlowImageAlgorithmAdapter.ExecuteRawPairAsync` 是可执行的双帧 adapter，复用同一 Invocation/Runner/Result 和 frame lease。它不是新的生产 STNode，也不改变旧远端 MQTT/device execution plane。
- Batch：V1 没有定义文件夹内 reference/moving 配对规则，因此不声明 Batch 能力，也不会出现在单输入 Batch 列表。
- Copilot：配准未进入显式白名单。Catalog、alias 或反射发现都不会自动产生 Copilot 执行权限。

## 验证、取消与性能

`ImageRegistrationV1Tests` 覆盖 Catalog/alias/schema、preset 往返与版本拒绝、九种规范格式 identity golden、DPI 与输入只读、相位平移方向、确定性 ORB 单应性、格式/色彩空间/尺寸/ROI/NaN 结构化失败、预取消及执行中取消、成功/失败/取消的 transferred 输入释放、Result artifact 释放、Flow 双帧 adapter，以及 ImageView Catalog 入口、结果窗口和 Visual 级 transient overlay 回收。

可选性能门禁 `ImageRegistrationPipelineProbe` 在 4K Gray16/Bgra32 上执行非 identity 相位相关，预算只允许一份配准输出、一份 mask 与固定 32 MiB 管理内存余量，并把延迟限制为 30 秒。仍不可消除的边界是 OpenCV 的归一化亮度/频域工作区、native warp 输出到 Result buffer 的一次复制，以及 mask；两幅输入均通过 lease 只读借用。

图像配准不估计相机内参/畸变系数，不执行径向或切向畸变校正，也不声称 CUDA/DirectML provider。畸变处理见独立的[镜头畸变校正](./lens-distortion-correction-v1.md)。
