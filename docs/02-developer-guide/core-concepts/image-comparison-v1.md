# 图像比较基础 V1（M3）

M3 提供可实际使用的双输入基础比较，稳定算法 ID 为 `colorvision.analysis.image-comparison`。M3 首次发布时行为版本和参数 schema 均为 V1；[M4](./image-comparison-advanced-v1.md) 已在同一身份上把行为版本升级到 1.1、schema 升级到 2，并为旧 schema 提供显式迁移。本页保留 M3 的兼容基线。

## 仓库盘点和阶段边界

实施前盘点没有发现可复用的通用比较算法；P2 下的双图代码是专用调试路径，不能作为公共契约。统一平台已经具备具名多输入、多个 Image artifact、Measurement、Table、StructuredData、取消和转移所有权，M3 在这些边界上实现 CPU provider。

M3 比较编码后的设备样本值，不执行缩放、裁剪、位深转换、通道转换、ICC/颜色空间转换或配准。两个输入必须：

- 分别命名为 `reference` 和 `candidate`，有符号差固定为 `reference - candidate`；
- 尺寸完全一致；
- 核心格式完全一致，即位深和通道数一致；
- 都提供相同的显式 `ColorSpace` 标签。ImageView V1 使用 `encoded-device-values`，表示按解码后通道样本直接比较。

违反约束时返回 `invalid_input_names`、`dimension_mismatch`、`format_mismatch`、`color_space_unspecified` 或 `color_space_mismatch`，不会偷偷转换。DPI 不同不改变像素阵列比较，但产生 `dpi_mismatch` warning。SSIM、ROI 比较和对齐前质量诊断随后由 M4 在这些约束上扩展。

## 参数与数值语义

schema 1 的 `ImageComparisonParameters` 有三个参数：

- `IncludeAlphaInMetrics` 默认 `true`，只控制指标和热力图是否统计 alpha；精确差分仍保留所有通道。
- `FloatPeakValue` 默认 `1`，是 float 输入计算 PSNR 的显式峰值；8/16-bit 固定使用 255/65535。
- `HeatmapMaximum` 默认 `0`，表示使用上述峰值作显示归一化上限；正值用于固定可复现的显示量程。

对所有被统计通道的有限样本对，`MSE = mean((reference-candidate)^2)`，`RMSE = sqrt(MSE)`，`PSNR = 20 log10(peak/RMSE)`。完全相同时 PSNR 为 `Infinity`，JSON 以命名浮点字符串安全导出。float 按 IEEE 32-bit 语义做差；输入非有限或差值溢出的样本不进入指标并计入 invalid count，精确差分保留相应 NaN/Infinity，显示图以洋红标识。

## Result artifacts

一次成功执行返回：

- `absolute-difference`：精确绝对差，保持输入尺寸、位深和通道；
- `signed-difference`：精确有符号差，统一为同通道 32-bit float；
- `absolute-difference-visualization`：BGR24 显示归一化；
- `signed-difference-visualization`：BGR24，中性灰表示零；
- `difference-heatmap`：BGR24 差值热力图；
- `image-comparison` Measurement 与 StructuredData；
- `image-comparison-channels` Table，包含逐通道 MSE、RMSE、PSNR、最大差值及有效/无效计数。

精确 artifact 和显示 artifact 故意分离；不得用截图或热力图反推数值。所有图像由 `AlgorithmResult` 统一持有并释放，Runner 仍负责释放 transferred 双输入。

## ImageView 使用

在“算法调用 → 图像比较”中选择“全图比较...”，设置参数后选择候选文件。当前 ImageView 和候选文件都会形成不可变快照，再通过同一 Runner 执行。结果窗提供差分热力/绝对差/有符号差显示、可调 blink、交互 split、逐通道指标、CSV/JSON 导出和当前显示 PNG 保存。导出默认拒绝覆盖已有文件；M4 另增加三种 ROI 入口和质量诊断页。

交互调用复用共享 analysis session。取消、关闭进度窗、当前文档 revision 改变、切图或较新的 Invocation 都会阻止迟到结果显示；关闭结果窗会释放全部 result image。

## 能力声明

Descriptor 声明 `Interactive | Headless | Local | Deterministic | MultiInput`。M3 没有声明 Batch、Flow 或 Copilot：现有 Batch/Flow 适配器是单输入，缺少显式成对策略；Copilot 也没有加入白名单。未来只有在宿主提供明确的双输入绑定、配对和权限策略后才能增加相应 capability，不能借文件名或目录顺序隐式配对。

## 验证边界

回归覆盖 8-bit golden、16-bit/BGRA、float/NaN、alpha 策略、精确差分、MSE/RMSE/PSNR 数值容差、输入只读、命名方向、尺寸/格式/颜色标签结构化拒绝、DPI warning、完美匹配 JSON、取消、双输入释放、结果统一释放、Catalog/白名单和 WPF 结果窗。阶段验收的具体命令与通过数量记录在任务报告中。

高级比较的当前契约、数值规则和门禁见 [图像比较高级 V1（M4）](./image-comparison-advanced-v1.md)。
