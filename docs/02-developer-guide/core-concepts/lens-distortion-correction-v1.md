# 镜头畸变校正 V1（M8.2）

## 阶段边界与已有能力盘点

M8.2 提供稳定 ID `colorvision.geometry.lens-distortion-correction`，用显式相机内参和 Brown-Conrady 系数把畸变图像重采样到校正后的 pixel-center 坐标。仓库已有 FindCross 内部去畸变和 9 点畸变测量，但前者绑定该检测流程与 native JSON，后者测量畸变而不校正整幅图；它们不能替代统一 Descriptor/Invocation/Runner/Result。本阶段复用已验证的 Brown 系数顺序、OpenCV 映射能力、M7 的只读帧借用、提交、preset 与有效 mask 规则，不估计相机标定，也不实现鱼眼模型。

## 相机、坐标与参数契约

V1 是针孔相机的 Brown-Conrady/rational 模型。输入内参为 `FxPixels/FyPixels`；主点可显式给出 `Cx/Cy`，也可选 `ImageCenter`，其定义严格为 `((width-1)/2, (height-1)/2)`。畸变系数顺序固定为 `K1,K2,P1,P2,K3,K4,K5,K6`。输出每个整数 `(x,y)` 是校正图像的像素中心，经输出相机矩阵归一化、Brown 模型正向畸变后得到畸变源图上的采样坐标。

输出画布始终与输入同尺寸、同 DPI、同规范像素格式。`PreserveCalibratedIntrinsics` 继续使用输入相机矩阵；`OptimalNewCameraMatrix` 使用 `OptimalAlpha`（0 尽量只保留全有效像素，1 尽量保留完整源视场）并可要求输出主点居中。插值为 nearest/linear，边界为 constant/replicate，常量边界值按 0..1 归一化后映射到输入位深。

支持 Gray8、Gray16、Gray32Float、Bgr24、Bgr48、Bgr96Float、Bgra32、Bgra64、Bgra128Float。输入通过 `AlgorithmImageMatLease` 只读借用；RGB/BGR、直通 alpha 与数值范围已在统一规范化边界确定，provider 不再从 depth+channels 猜测。V1 不接受 ROI，Runner 返回结构化 `roi_kind_unsupported`，避免把全图内参与局部坐标误混。

## 校正来源与质量

`CalibrationSource`、`CalibrationVersion` 和可选 `CalibrationChecksum` 会进入 preset、图像 artifact 元数据及 StructuredData。若外部标定报告提供 RMS 和置信度，必须显式设置 `HasCalibrationQuality` 后填写；否则结果明确记录质量不可用，界面不会把几何位移或有效比例伪装成标定置信度。参数校验拒绝非有限/非正焦距、越界系数、无来源/版本以及未声明的质量值。

Preset 由 `LensDistortionCorrectionPresetSerializer` 保存，严格校验 AlgorithmId、算法版本、参数 schema 版本、PresetId 与参数 JSON。加载未来版本、其他算法或缺少算法版本的 preset 会被拒绝；保存使用 `CreateNew`，不覆盖已有文件。

## Result artifact

| Artifact | 内容 |
| --- | --- |
| `corrected-image` | role=`primary`，保持输入尺寸、DPI、位深、通道和像素格式 |
| `valid-region-mask` | role=`validity-mask`，Gray8；255 表示映射源坐标位于闭合像素中心范围，0 表示使用边界策略 |
| `lens-distortion-summary` | 有效/无效像素、有效比例、平均/最大重采样位移，以及显式标定质量字段 |
| `lens-distortion-camera-matrices` | 输入与实际输出 3×3 相机矩阵 |
| `lens-distortion-coefficients` | 固定顺序的八个 Brown/rational 系数 |
| `lens-distortion-valid-region` | Pixel 坐标下实际有效 mask 的轴对齐包围多边形 |
| `lens-distortion-correction` | `colorvision.geometry.lens-distortion-correction/v1`，记录模型、坐标语义、矩阵、系数、mask、采样、preset 与标定追溯 |

零畸变且保留原内参时走精确 identity 路径，输出逐字节等于输入并生成全有效 mask。非零路径先生成 `CV_32FC1` 的 X/Y native 映射，再逐行检查有限性、有效比例和位移，最后 remap；低于 `MinimumValidFraction` 时返回结构化失败而不提交图像。

## 宿主接入

- ImageView：“算法 → 镜头畸变校正...”打开参数/preset 窗口，通过统一 preview session 执行并提交。结果窗口显示校正图、mask、相机矩阵和系数，可保存 PNG、导出 CSV/JSON；关闭窗口释放 Result 图像。
- Batch：这是单输入、确定性、同尺寸像素算法，由 Catalog 自动投影到批处理列表，和交互入口使用同一参数对象与 provider；输出文件格式仍由 Batch 输出策略决定。
- Flow：`LocalFlowImageAlgorithmAdapter.ExecuteRawAsync` 复用同一 Invocation/Runner/Result 与本地 frame lease，不改变旧远端 MQTT/device `AlgorithmNode` execution plane。
- Copilot：未进入显式白名单；Catalog alias 或反射发现不会赋予执行权限。

## 验证、取消与资源预算

`LensDistortionCorrectionV1Tests` 覆盖 Catalog/alias/schema/preset、九种规范格式 identity golden、Brown 像素中心数值 golden、optimal 输出矩阵、DPI/输入只读、ROI 与退化有效区失败、预执行/执行中取消、成功/失败/取消的 transferred 输入释放、Result artifact 释放、ImageView 提交和结果窗口，以及 Batch/Flow 复用与不覆盖导出。

可选性能门禁 `LensDistortionCorrectionPipelineProbe` 在 4K Gray16/Bgra32 上执行非零畸变。管理内存预算只允许一份校正输出、一份 Gray8 mask 和固定 16 MiB 余量；native 侧仍需要两份 float map 与 OpenCV 输出工作区。取消在 map 前后、mask 每 32 行及 remap 前后检查；单次不可中断的 OpenCV 调用结束后会立即观察取消并释放临时 Mat。

M8.2 不从棋盘格/圆点板估计标定，不自动改变输入尺寸的内参，不实现 fisheye/omnidir 模型，也不声称 CUDA/DirectML provider。
