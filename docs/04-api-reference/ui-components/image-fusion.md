---
knowledge_id: "ui.image-fusion"
knowledge_type: "topic"
status: "current"
summary: "景深融合窗口与本地流程节点共用文件执行器；输入顺序、CPU/CUDA门禁、取消、结果保存及下游图像交接。"
aliases: ["景深融合", "执行融合", "融合结果", "GPU 异步 (CUDA Async)", "焦点堆栈", "focus stacking", "FusionWindow", "FusionMode", "FusionFolderMenuContribution", "ImageCompute", "M_Fusion", "CM_Fusion", "CM_Fusion_Async", "融合耗时", "FileFusion", "LocalFileFusionNode", "流程景深融合", "有序文件列表"]
code_paths: ["UI/ColorVision.Core/FileFusion.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFileFusionNode.cs", "Engine/ColorVision.Engine/Services/Images/FileFusion/FileFusionServices.cs", "UI/ColorVision.ImageTools/Fusion/FusionWindow.xaml", "UI/ColorVision.ImageTools/Fusion/FusionWindow.xaml.cs", "UI/ColorVision.ImageTools/Fusion/FusionFolderMenuContribution.cs", "UI/ColorVision.ImageTools/ImageResourceFileTypes.cs", "UI/ColorVision.Core/ImageCompute.cs", "UI/ColorVision.Core/OpenCVMediaHelper.cs", "UI/ColorVision.Core/OpenCVCuda.cs", "Native/include/opencv_media_export.h", "Native/include/cuda_export.h", "Native/opencv_helper/opencv_media_export.cpp", "Native/opencv_helper/fusion.cpp", "Native/opencv_cuda/cuda_export.cpp", "Native/opencv_cuda/Fusion.h", "Native/opencv_cuda/cudamath.h"]
test_paths: ["Test/ColorVision.UI.Tests/FileFusionTests.cs", "Test/ColorVision.UI.Tests/LocalFileFusionNodeTests.cs", "Test/opencv_helper_test/test_find_luminous_area.cpp", "Test/opencv_helper_test/test_cuda_fusion.cpp"]
related: ["ui.image-tools", "ui.core", "ui.image-editor", "ui.documents", "engine.native-integration"]
---

# 景深融合：输入、执行与结果生命周期

景深融合将一组不同焦点的本地图片融合为一张图像。独立窗口和本地流程节点共同调用 `ColorVision.Core.FileFusion`，读取磁盘文件，不使用编辑器未保存的修改或流程连续采集帧。算法不依赖远端服务；流程图像结果登记仍依赖已有流程批次和 MySQL。

## 执行融合

1. 在 Solution 文件树中右键单个现有文件夹，选择 **景深融合**。
2. 使用 **添加文件** 或拖入文件补充列表，通过上移、下移、移除和清空调整焦点顺序。
3. 选择计算模式并点击 **执行融合**。运行期间列表、模式和执行按钮禁用。
4. 成功后显示“融合结果”标签；没有停靠面板时打开独立窗口。窗口不会自动保存文件，需要保存时使用图像编辑器的输出操作。

流程中添加 **自定义节点 → 景深融合**（`LocalFileFusionNode`）。它有一个 `IN` 和一个 `OUT`，输入连接负责触发执行，图片来源取自节点属性：

| 属性 | 含义 |
| --- | --- |
| 输入方式 | `Folder` 读取文件夹，`FileList` 使用有序文件列表 |
| 输入文件夹 | 只读取直接子文件，按文件名自然排序，不递归 |
| 有序文件列表 | 通过通用集合编辑器添加文件并调整顺序；STN 使用 JSON 保存，文件名中的逗号不会拆分路径 |
| 计算模式 | Auto、CPU、GPU、GPUAsync |
| 结果目录 | 留空使用当前用户 `LocalAppData/ColorVision/Results/Fusion`；不得等于任一输入文件的父目录 |

例如：开始 → 景深融合 → 十字定位 → 结束。每次执行保存一张带唯一名称的 PNG，以现有图像结果类型 `100` 登记，并通过 `SetCurrentFrame` 交给后续本地节点。`MasterId` 指向图像记录，`MasterValue` 和记录 `FileUrl` 指向保存后的图片。远端节点仍按自己的文件访问及结果引用契约工作，不因本地保存而自动获得远端文件可见性。

## 文件列表与输入约束

文件夹入口统一使用 `FileFusion.GetFolderFiles`，过滤 `.bmp/.jpeg/.jpg/.png/.tif/.tiff`。显式文件列表保留顺序；窗口添加文件保留对话框顺序，拖入文件仍按字符串排序，可在执行前手动调整。每次执行捕获独立路径快照，不随运行期间参数编辑改变。

公共执行器要求至少两张不同路径的图片，拒绝重复完整路径、缺失文件、不可识别文件、尺寸或通道不一致。预检使用 WPF 解码器，仅接受 `Gray8`、`Bgr24`、`Rgb24`，对应 8-bit 单通道或三通道图像；Indexed、Alpha、高位深和浮点布局明确拒绝，不隐式降位深。文件后缀不能替代像素布局校验，也不包含 `.cvraw`、`.cvcie`。

预检后的只读文件句柄保持到 native 调用结束，禁止这段时间覆盖或替换源文件。native 仍使用 `IMREAD_UNCHANGED` 自行解码；不自动对齐、缩放、校色或跳过坏图。预检通过不代表 native 必定解码成功。

## 模式、返回值与资源所有权

| 模式 | 选择与调用 |
| --- | --- |
| Auto | 2–4 张固定选 CPU；至少五张且 `ImageCompute.UseCuda` 为真才选 GPU；否则选 CPU |
| CPU | `OpenCVMediaHelper.M_Fusion` → `opencv_helper.dll` |
| GPU | 至少五张且 CUDA 开关可用，调用 `OpenCVCuda.CM_Fusion` |
| GPUAsync | 同样的门禁，调用 `OpenCVCuda.CM_Fusion_Async`；native 内部并行加载，导出仍同步返回最终图像 |

Auto 只在执行前选路，不在 GPU 失败后重试 CPU。CUDA 驱动检查不证明 DLL、依赖和输入能够成功执行。结果记录保存本次实际模式，不在结束时重新读取全局开关推测模式。

底层 CUDA 固定 `STEP=2`，仍存在少于五个焦点平面时的越界读取路径。本次由公共执行器阻止窗口和流程进入该路径，没有修改 native ABI 或 CUDA 算法。直接使用 `ImageCompute.Fusion`、`OpenCVCuda` 的旧低层调用不会获得新的文件预检和数量门禁；它们继续保留兼容语义。五张及以上也不等于融合质量已验收。

两个 native DLL 均以返回 `0` 表示成功。公共执行器在所有返回和异常路径通过 `finally` 释放输出 `HImage`，成功时复制成冻结的托管位图。`FileFusionResult` 包含位图、输入快照、实际模式和耗时，不拥有待调用方释放的 native 缓冲，不引用窗口、工作区或 Engine DAO。

## 取消、并发与计时

公共执行器在进程内串行执行窗口和流程的融合任务，避免多个图组同时占用 GPU 和大量 native 内存。排队支持取消；已进入同步 native 的调用不能强制中断，取消后等待返回并释放输出，不再交付成功结果。

窗口关闭或点击 **取消** 会取消该次调用；迟到结果不会打开结果窗口或弹出错误框。流程执行把取消资源挂到本次 `RuntimeResources`，流程结束/停止释放资源后取消排队任务或丢弃在途结果。节点在保存、登记和交接之间检查停止状态；已经完成的文件或数据库写入不会回滚，也不保证与并发停止操作构成跨文件/数据库事务。

| 字段 | 实际覆盖 |
| --- | --- |
| `ValidationMs`，窗口“校验(ms)” | 打开并锁定输入、读取格式及尺寸预检 |
| `NativeMs`，窗口“读图及融合(ms)” | 整个 native 调用，包含 native 文件读取、融合和输出缓冲生成 |
| `ConvertMs` | native 输出复制为冻结托管位图 |
| `TotalMs` | 公共同步执行入口开始至结果完成，包含执行门禁排队；不含 UI 显示、流程保存和数据库登记 |

流程额外通过 `FlowNodeTiming` 记录 `ResolveBatch`、`Fusion`、`SaveImage`、`OpenImage`、`PersistResult`、`PublishResult`，不能将公共执行器耗时冒充流程整体耗时。

## 结果显示与保存边界

窗口结果仍使用临时 `LayoutDocument` 或独立窗口，不通过 `EditorDocumentService` 注册路径文档。关闭完成（`Closed`）后释放 ImageView；取消关闭不会提前清空图像。另存与像素/叠加含义见[图像编辑器](./ColorVision.ImageEditor.md)。

流程结果由 Engine 保存和登记，计算模块不接触 MySQL。输出 PNG 使用唯一名称和 `CreateNew`，不覆盖已有文件；默认目录与源文件分离，配置输出为输入子目录时因不递归枚举也不会卷入下一次图组。输入顺序、请求/实际模式和计算耗时保存到图像记录 `Params` 中。

只有图像记录成功登记、结果交接和发布完成后才作为节点成功结果继续执行。计算/写文件/数据库/发布任一失败均按节点失败处理；已经产生的 PNG 或记录保留用于诊断，不作为成功执行的证据。融合图是普通像素图，不生成 CIE 标定数据或承诺亮度、色度测量有效。

## 验证入口与缺口

- `FileFusionTests`：文件夹自然顺序、显式顺序、输入锁、格式/尺寸/重复拒绝、Auto 小图组选路、强制 GPU 拒绝、取消后丢弃、native 失败不重试，以及两张合成灰度图的真实 CPU 调用。
- `LocalFileFusionNodeTests`：节点端口、包含逗号和中文路径的 STN 往返、文件夹/列表输入、真实 PNG 保存和帧交接、登记失败及停止后丢弃。数据库与消息发布使用替身，不访问真实数据库。
- `Test/opencv_helper_test/test_find_luminous_area.cpp` 的 `smokeFusionReturnsOwnedHImage` 检查 CPU 单图输出和释放；`test_cuda_fusion.cpp` 提供 CUDA DLL 验证和比较入口。CUDA DLL 内的 `M_Fusion` 是 GPU 别名，该入口比较不等于 CPU/GPU 数值一致性。

测试入口不是本次已通过声明；窗口人工操作、真实数据库历史显示、CUDA 驱动/native 依赖、大图资源和真实焦点堆栈融合质量需分别验收。Native/CUDA 测试会创建隔离图像并加载真实 DLL，不能用窗口响应或托管替身测试代替它们。
