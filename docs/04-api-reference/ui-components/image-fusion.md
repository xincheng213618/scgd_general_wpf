---
knowledge_id: "ui.image-fusion"
knowledge_type: "topic"
status: "current"
summary: "景深融合的CPU/CUDA调用、HImage显示和计时；自动模式不做失败回退，关窗不取消计算，GPU少量图片存在未修复的越界风险。"
aliases: ["景深融合", "焦点堆栈", "focus stacking", "FusionWindow", "FusionMode", "FusionFolderMenuContribution", "ImageCompute", "M_Fusion", "CM_Fusion", "CM_Fusion_Async", "融合耗时"]
code_paths: ["UI/ColorVision.ImageTools/Fusion/FusionWindow.xaml", "UI/ColorVision.ImageTools/Fusion/FusionWindow.xaml.cs", "UI/ColorVision.ImageTools/Fusion/FusionFolderMenuContribution.cs", "UI/ColorVision.ImageTools/ImageResourceFileTypes.cs", "UI/ColorVision.Core/ImageCompute.cs", "UI/ColorVision.Core/OpenCVMediaHelper.cs", "UI/ColorVision.Core/OpenCVCuda.cs", "Native/include/opencv_media_export.h", "Native/include/cuda_export.h", "Native/opencv_helper/opencv_media_export.cpp", "Native/opencv_helper/fusion.cpp", "Native/opencv_cuda/cuda_export.cpp", "Native/opencv_cuda/Fusion.h", "Native/opencv_cuda/cudamath.h"]
test_paths: ["Test/opencv_helper_test/test_find_luminous_area.cpp", "Test/opencv_helper_test/test_cuda_fusion.cpp"]
related: ["ui.image-tools", "ui.core", "ui.image-editor", "ui.documents", "engine.native-integration"]
---

# 景深融合：输入、执行与结果生命周期

`UI/ColorVision.ImageTools/Fusion/` 负责本地图像文件的景深融合窗口和 Solution 文件夹菜单；`ColorVision.Core` 选路并调用 native，CPU 与 CUDA 实现在各自 DLL 中。虽然窗口命名空间仍是 `ColorVision.Solution.Fusion`，它不属于 Solution 的工作区存储，也不进入 Engine 模板、MQTT 或历史结果 DAO 链。

这里的“融合完成”表示选定 native 调用返回成功并完成位图转换，不能推导为文件已保存、结果通过测量验收，或 CPU/GPU 数值完全相同。[多图查看与缓存](./ColorVision.ImageTools.md)是同一模块中的另一条独立链路，不为融合提供像素缓存。

## 文件列表与输入约束

- `FusionFolderMenuContribution` 只适用一个现有 `FolderNode`。只枚举该文件夹的直接文件，不递归子目录；按 `Shlwapi.CompareLogical` 进行文件名自然排序，过滤 `.bmp/.jpeg/.jpg/.png/.tif/.tiff` 后打开非模态窗口。
- 添加文件对话框保留返回的顺序，并提供“所有文件”过滤器；拖入则按上述扩展名过滤后用字符串排序。两条入口都不去重，窗口还允许上移、下移、移除和清空；不要假设所有入口具有相同的排序或内容校验。
- `Execute_Click` 要求列表至少两项，并逐项检查 `File.Exists`，再把当时的路径数组序列化为 JSON。两项不等于两张不同图片；存在和后缀匹配不证明可解码，也不证明读取期间文件不会变化。
- native 使用 `imread(..., IMREAD_UNCHANGED)` 读取源文件，不取多图窗口缩略图或图像编辑器中的修改。没有自动对齐、缩放、色彩标定或替换坏图的窗口预处理。

多图融合应使用同尺寸、同通道的 8-bit 单通道或 BGR 图像。CPU 的 `fusion` 对多图显式拒绝其它位深/通道及尺寸不匹配；其单图分支直接克隆，与窗口的至少两项门禁不同。CUDA `Fusion` 只显式比较尺寸和通道，没有同等的位深门禁，末段按 8-bit 像素上传和输出；因此不能承诺 GPU 会正确处理或安全拒绝任意高位深 TIFF、四通道或混合位深输入。格式出现在文件选择器中，不代表该格式的所有像素布局都受支持。

当前另有未修复的 GPU 图片数量风险：CUDA 固定 `STEP=2`，`find_max_and_prepare_kernel` 无条件读取中心前后各两个焦点平面，而窗口只要求至少两项。对 2–4 张图片可推导出越界读取路径；GPU、GPUAsync 及 Auto 选中 GPU 时均受影响，不能作为受支持输入直接执行。CPU 对小图组有跳过拟合的不同分支。此结论来自源码，不是已运行的越界复现；五张及更多也不因此自动获得数值/稳定性验收保证，现有 CUDA 正常样例主要覆盖六张 BGR、七张灰度。

## 模式、返回值与资源所有权

| 窗口模式 | 实际调用 | 关键区别 |
| --- | --- | --- |
| Auto | `ImageCompute.Fusion` 根据 `UseCuda` 选择以下 CPU 或 GPU 调用 | 只选一次路径；GPU 返回失败或抛异常后没有 CPU 重试 |
| CPU | `OpenCVMediaHelper.M_Fusion` → `opencv_helper.dll` | 读取文件后调用 `fusion(imgs, 2)` |
| GPU | `OpenCVCuda.CM_Fusion` → `opencv_cuda.dll` | 读取文件后调用 CUDA `Fusion(imgs, 2)` |
| GPUAsync | `OpenCVCuda.CM_Fusion_Async` → `opencv_cuda.dll` | 内部异步加载队列收齐结果后调用 CUDA 融合；导出仍同步返回最终 `HImage`，不是提交后台任务后返回句柄 |

四种模式在窗口中都通过 `Task.Run` 调用。`ImageCompute.UseCuda` 初始值来自 CUDA 驱动初始化和设备数量检查，也是可设置的运行时开关；它不证明 `opencv_cuda.dll` 及其依赖可加载、输入适合 GPU 或本次调用必定成功。窗口只在构造时更新 CUDA 状态文字，手选 GPU/GPUAsync 也不会因为该状态为 false 而禁用。

两个 DLL 的成功约定都是返回 `0` 并提供输出图像，不是其它 JSON 导出的“正数表示生成 JSON”。负数的具体含义须按各自导出解读：helper 的参数、JSON、算法、分配和异常错误码，与 CUDA 的解析/加载/异常码并非同一张表。CUDA DLL 还导出名为 `M_Fusion` 的 `CM_Fusion` 别名；同名导出不能单凭函数名判断使用了 CPU。

窗口遇到非零返回会释放输出 `HImage` 并显示错误码；成功后在 UI 续体中调用 `ToWriteableBitmap`，用 `finally` 释放本次 native 输出。`DllNotFoundException` 有专门提示，其它托管异常走通用错误提示。所有权的一般规则见 [native 集成](../../02-developer-guide/engine-development/opencv-integration.md)；这些分支不构成对进程级 native 故障或所有异常路径的隔离保证。

## 取消、并发与计时

“取消”按钮的 `Cancel_Click` 只调用 `Close()`。执行没有传入 `CancellationToken`，窗口关闭也没有等待/终止 native 任务的协议；在途调用仍可能完成并进入显示结果的续体。不能用关窗作为计算已停止或资源已全部释放的证据。

执行时只把执行按钮设为禁用，并未禁用列表编辑，也没有独立的忙碌标志。`FilePaths.CollectionChanged` 会按 `Count >= 2` 重新设置按钮，所以修改列表可能在旧计算未结束时重新启用执行。当前代码不能承诺单窗口单任务互斥；旧调用使用先前序列化的快照，不随列表后续修改而改变。

`TimingRecord` 的字段是窗口侧测量，不是完整性能分解：

| 字段 | 当前实际覆盖 |
| --- | --- |
| `LoadMs` | 开始后立即停止的空计时段，没有执行读文件；不能作为磁盘加载耗时 |
| `FusionMs` | 整个同步 native 调用，包括其内部文件读取、计算和输出缓冲生成，不是纯算法时间 |
| `ConvertMs` | `ToWriteableBitmap` 转换及 `finally` 中释放输出的时间 |
| `TotalMs` | 从排入 `Task.Run` 前到位图转换完成，包含任务调度/续体等待；不包含后续结果窗口显示 |

计时记录在结果交给 `ImageView` 之前追加，所以有记录不证明最终标签已成功显示。Auto 的模式文字也在记录时再次读取 `UseCuda`，不是从 native 返回的实际设备标识；不能单凭该文字进行严格的 CPU/GPU 基准归因。

## 结果显示与保存边界

`ShowResultInImageEditor` 有工作区停靠面板时，直接创建随机 `ContentId` 的 `LayoutDocument` 和 `ImageView`；没有面板时打开独立窗口。这里没有通过 `EditorDocumentService` 注册路径文档，也没有写出图像文件或建立结果数据库记录。不能把该标签视为可按原路径保存、自动恢复或统一脏文档保护的持久文件会话。

结果标签/窗口在 `Closing` 而非 `Closed` 中先清理 `ImageView`，再由 `async void` 处理器等待 10ms 后释放；关闭方不会等待这个释放过程，处理器也不检查取消。关闭尝试就可能清空内容，若其它处理器取消关闭，没有撤销这次清理的逻辑。图像另存或快照属于[图像编辑器输出](./ColorVision.ImageEditor.md)的后续操作，需单独确认输出文件及其像素/叠加含义。

## 验证入口与缺口

- `Test/opencv_helper_test/test_find_luminous_area.cpp` 中的 `smokeFusionReturnsOwnedHImage` 使用单张合成图，检查 helper 返回图像的尺寸、布局和释放；同文件还检查无效 JSON 的输出清空。它不证明多图融合质量、GPU 一致性或窗口行为。
- `Test/opencv_helper_test/test_cuda_fusion.cpp` 提供 CUDA DLL 比较、验证与基准入口：包括无效输入、失败输出清空、尺寸不匹配，以及合成灰度/BGR 样例中 `M_Fusion`、`CM_Fusion`、`CM_Fusion_Async` 的一致性。比较的是指定 CUDA DLL/入口，不是自动证明 helper CPU 与 GPU 输出相等。
- 尚未登记覆盖窗口取消、列表改动期间重复执行、计时定义或结果标签恢复的专门 UI 测试。真实驱动/native 依赖、不同图片数量/布局和结果质量仍需独立验收；本页记录源码契约，不宣称已经运行这些验收。

native 测试会创建/清理临时图像并实际加载 DLL，CUDA 场景还使用 GPU；应先检查 fixture 路径和环境，再在隔离数据上执行。不要为验证文档而启动主程序、直接调用真实图片集合，或把计时表当成已完成的性能对比结论。
