---
knowledge_id: "algorithms.geometric-transform"
knowledge_type: "reference"
status: "current"
summary: "GeometricTransform 的输入、参数、结果、宿主接入与定向验证契约。"
aliases: ["如何应用仿射透视矩阵并保持坐标和有效区语义","GeometricTransform","GeometricTransformAlgorithmProvider"]
code_paths: ["UI/ColorVision.ImageEditor/Algorithms/GeometricTransformAlgorithmProvider.cs","UI/ColorVision.ImageEditor/Algorithms/StandardAlgorithmCatalog.cs","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: ["Test/ColorVision.UI.Tests/GeometricTransformV1Tests.cs"]
related: ["algorithms.platform","algorithms.index"]
---

# 几何变换 V1（M7）

## 阶段边界与已有能力盘点

M7 提供稳定 ID `colorvision.geometry.transform`。仓库此前只有 POI 子系统私有的四点单应点位重映射，它同时依赖模板、数据库和业务对象，不能作为统一图像算法 provider；Hough、对齐预检和客户算法也不执行通用 image warp。本阶段实现显式矩阵变换，不包含自动求配准对应点、特征/相关性配准或镜头畸变参数，这些仍属于 M8。

Descriptor 与本地 OpenCV CPU provider 元数据分离。公共参数、Invocation、preset 和 Result 不依赖 WPF、OpenCvSharp、HImage、Flow 或设备类型。provider 通过 `AlgorithmImageMatLease` 只读借用输入缓冲区，不产生输入像素副本；OpenCV 输出在完成后复制一次进入由 Result 所有的 `AlgorithmImageBuffer`，有效区 mask 是独立的一字节每像素 artifact。

## 参数与坐标规则

`GeometricTransformParameters` 使用 3×3、row-major 的 source-to-destination 矩阵。坐标是统一 pixel-center 坐标：整数 `(x,y)` 表示像素中心，正方向向右/向下。Affine 模式强制最后一行为 `[0,0,1]`；Perspective 模式允许完整、可逆的单应矩阵。provider 拒绝奇异矩阵、超过条件数上限的矩阵，以及在源图范围内穿越投影无穷远的分母。

画布策略：

- `SourceSize`：保持源宽高，矩阵不附加平移；超出画布的内容会裁剪。
- `ExplicitSize`：使用显式正宽高，适合已知目标坐标系。
- `FitTransformedBounds`：变换四个源像素中心角点，向外取整并附加可选 padding；provider 把左上包围坐标平移到输出 `(0,0)`，Result 同时记录请求矩阵和最终有效矩阵。

V1 支持最近邻/线性插值和常量/复制边界。常量边界以规范化 0..1 表示 B/Gray、G、R、A，分别映射到 8 位、16 位或 float 的标称范围，不按图像内容重新归一化。`MaximumOutputPixels` 和 `MaximumConditionNumber` 是确定性的资源/数值门禁。

## 有效区域 mask 与 Result

`valid-region-mask` 是 Gray8：255 表示该输出像素中心经有效矩阵的逆映射后位于闭合源 pixel-center 范围 `[0,width-1] × [0,height-1]`，否则为 0。该定义与边界填充、插值核彼此独立，因此使用 Replicate 时仍能区分真实源覆盖与外推像素。

| Artifact | 内容 |
| --- | --- |
| `transformed-image` | role=`primary`，尺寸按画布策略，格式、通道、位深、直通 alpha 和 DPI 与输入一致 |
| `valid-region-mask` | role=`validity-mask`，Gray8 有效像素中心掩码 |
| `geometric-transform-summary` | 输出尺寸、有效/无效像素、比例、请求/有效行列式、条件数和正逆残差 |
| `geometric-transform-matrix` | 有效 3×3 正矩阵与逆矩阵逐行表 |
| `geometric-transform` Geometry | `Transform` 矩阵与变换后的源四角多边形 |
| `geometric-transform` StructuredData | `colorvision.geometry.transform/v1` 完整参数语义、矩阵、画布、mask 规则、preset 和版本来源 |

正逆验证使用 `maxAbs(M × inverse(M) - I)`；它作为数值残差返回。条件数质量只用于诊断，不伪装成标定置信概率。

## Preset 与宿主接入

`AlgorithmParameterPreset` 是 provider-neutral 的 V1 文档，保存稳定 AlgorithmId、算法版本、参数 schema 版本、JSON 参数、PresetId 和元数据。`GeometricTransformPresetSerializer` 严格检查 ID/版本/schema 和参数验证结果，避免把未来或其他算法的 preset 静默套用。

- ImageView：“算法 → 几何变换...”可编辑 PropertyGrid 参数、加载/保存不覆盖已有文件的 JSON preset，并通过统一 preview session 执行和提交。结果窗口同时显示已提交图像、mask、正逆矩阵，可分别保存 PNG 或导出结构化 JSON。
- Batch：Catalog 投影自动显示该算法，使用相同默认值、参数类和 primary image artifact；格式转换仍是独立输出策略。
- Flow：`LocalFlowImageAlgorithmAdapter` 可复用同一 Invocation/Result；没有新增生产 STNode，也不改变旧远端 MQTT/device execution plane。
- Copilot：M7 未加入显式白名单，alias、Catalog 或反射发现不会自动暴露执行入口。

## 验证与性能

`GeometricTransformV1Tests` 覆盖九种规范格式 identity golden、仿射平移与边界值、自动包围 90° 旋转、透视正逆点往返、有效 mask、preset JSON、奇异/投影无穷远/输出预算拒绝、取消、输入只读、成功/取消/Result 释放、Batch/Flow，以及 ImageView session 提交和结果窗口资源释放。

可选性能门禁 `GeometricTransformPipelineProbe` 在 4K Gray16/Bgra32 上验证 retained managed 图像只包含一份输出和一份 mask（另加固定 16 MiB 容差），并限制单次执行在 20 秒内。实际门禁使用的 native warp 读取 lease，无 `input → byte[] → Mat` 复制；剩余不可消除的边界是 native 输出到 Result-owned buffer 的一次复制，以及单字节 mask。

V1 不自动估计矩阵、不处理镜头畸变、不输出相机标定误差，也不声称 DirectML/CUDA provider。配准与畸变校正在 M8 单独验收。
