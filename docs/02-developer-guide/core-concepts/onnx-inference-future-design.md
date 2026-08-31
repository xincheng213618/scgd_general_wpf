---
knowledge_id: "algorithms.onnx"
knowledge_type: "decision"
status: "planned"
summary: "尚未实现的 ONNX 接入设计：保持基础产品无 ONNX 运行时，新增 adapter 前须明确模型与验收门禁。"
aliases: ["ONNX 未来如何接入","AI 推理是否已实现","Microsoft.ML.OnnxRuntime","InferenceSession","Execution Provider"]
code_paths: ["UI/ColorVision.Algorithms/ColorVision.Algorithms.csproj","UI/ColorVision.ImageEditor/Algorithms/ImageAlgorithmPlatform.cs"]
test_paths: []
related: ["algorithms.platform"]
---

# ONNX / AI 推理接入设计（Deferred）

该能力只保留未来设计，当前不实施。现有安装包、ImageEditor 和 `ColorVision.Algorithms` 的依赖图必须保持不含 `Microsoft.ML.OnnxRuntime`；不增加 CPU/DirectML/CUDA Execution Provider、模型文件、模型下载器或任何 ONNX 运行时代码。OpenCV 自带头文件中的 ONNX 声明不代表产品已启用该能力。

本页 `status: planned`，不得作为已经存在的接口或执行步骤。`code_paths` 指向待保持兼容的现有平台契约与 provider 注册边界，不表示这些文件中有 ONNX 实现。当前没有 ONNX 业务模型和对应实现测试，故 `test_paths` 留空；下面的门禁是未来实施前必须建立的验证要求，而不是已经通过的证据。

## 目标边界

ONNX 只是一种算法实现后端，不成为第二套算法平台。ImageView、Batch、Flow、Copilot 和后台任务仍提交统一 `AlgorithmInvocation`，并消费统一 `AlgorithmResult`；它们不直接认识 ONNX Runtime、模型会话或设备 provider。

## 未来实现顺序

1. 先选定一个具体业务模型和验收数据集，明确输入、输出、精度、延迟、内存、部署体积和许可证预算，不先搭空泛的“通用 AI 平台”。
2. 在 `ColorVision.Algorithms` 只增加与框架无关的模型元数据和张量契约，包括输入名称、维度、颜色顺序、归一化、动态尺寸、输出语义和模型版本；公共 API 不出现 `InferenceSession`、`OrtValue` 或 execution-provider 类型。
3. 新建独立 ONNX adapter/runtime 项目，由它引用 ONNX Runtime。CPU、DirectML、CUDA 作为互斥的可选部署变体，主程序与基础 ImageEditor 不获得传递引用；未安装对应变体时 Catalog 只返回 `provider_unavailable`，不能影响程序启动。
4. 用普通 `IImageAlgorithm` provider 封装预处理、推理和后处理。输入仍通过 `IImageFrame`/lease，输出仍使用 Image、Measurements、Annotations、Overlays 和 Diagnostics。
5. 模型使用显式 manifest，至少记录模型哈希、opset、输入输出 schema、许可证、来源、最小运行时版本、设备要求和 golden data。模型不默认嵌入基础安装包，也不允许算法静默联网下载。
6. 会话缓存、设备选择和内存池留在 adapter 内部；取消、超时、并发上限、显存不足降级和 session 释放通过现有 Runner 的资源调度与 Diagnostics 表达。

## 重新启用门禁

- 无 ONNX 变体的主程序安装与启动回归必须继续通过。
- CPU 建立可复现基线；DirectML/CUDA 只作为可选加速，不改变结果 schema。
- 覆盖 provider 加载失败、设备不可用、显存不足、取消和降级路径。
- 预处理与后处理使用 golden tests；模型哈希、版本或 schema 不匹配必须拒绝执行。
- 记录重复运行确定性、吞吐、P95 延迟、进程内存和显存峰值。
- 同一 Invocation 在 ImageView、Batch、Flow、Copilot 中产生一致的结构化结果。

只有业务模型、验收数据、部署变体和体积预算全部明确后，才允许增加实际 NuGet 引用并启动该里程碑。
