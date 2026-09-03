---
knowledge_id: "ui.core"
knowledge_type: "reference"
status: "current"
summary: "定位 HImage 所有权、OpenCV/CUDA PInvoke、ImageCompute 融合分流、位图桥接与默认关闭的原生日志。"
aliases: ["原生图像调用缺少DLL","ColorVision.Core","HImage","OpenCVMediaHelper","ImageCompute","NativeLogBridge","原生日志初始化"]
code_paths: ["UI/ColorVision.Core/HImage.cs","UI/ColorVision.Core/HImageExtension.cs","UI/ColorVision.Core/OpenCVMediaHelper.cs","UI/ColorVision.Core/OpenCVCuda.cs","UI/ColorVision.Core/ImageCompute.cs","UI/ColorVision.Core/NativeLogBridge.cs","UI/ColorVision.Core/ColorVision.Core.csproj","UI/ColorVision.Core/README.md"]
test_paths: ["Test/ColorVision.UI.Tests/HImageAbiTests.cs","Test/ColorVision.UI.Tests/HImageExtensionCopyTests.cs","Test/ColorVision.UI.Tests/NativeLogBridgeTests.cs","Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs","Test/ColorVision.UI.Tests/VideoFrameCopyTests.cs"]
related: ["ui.index","engine.native-integration","ui.image-editor","ui.image-fusion","ui.image-frames"]
---

# ColorVision.Core

`UI/ColorVision.Core/` 提供原生图像/视频互操作、图像缓冲结构和 WPF 位图桥接。上层编辑工具、文档状态与结果叠加由 [ImageEditor](./ColorVision.ImageEditor.md) 等调用方负责；Core 的方法签名和返回值应按具体 native 函数核对。

## 能力与责任位置

| 能力 | 入口 | 使用边界 |
| --- | --- | --- |
| 原生图像结构 | `HImage.cs` | `HImage` 是含非托管指针的值类型；复制结构体不会复制像素或转移释放责任 |
| 位图转换与复制 | `HImageExtension.cs` | `ToPixelFormat`、`ToWriteableBitmap`、`ToHImage`、`UpdateWriteableBitmap` 等入口；复制、借用和释放语义需区分 |
| helper 导出 | `OpenCVMediaHelper.cs` | 伪彩、增强、滤波、阈值、SFR、聚焦评价、视频等 P/Invoke，参数与成功条件因函数而异 |
| CUDA 导出包装 | `OpenCVCuda.cs` | Fusion 系列、批量调用和输出释放；托管包装在调用前尝试准备 CUDA 日志 |
| Fusion 选择 | `ImageCompute.cs` | `UseCuda` 与 `Fusion()` 选路，不提供通用直方图、统计或滤波托管 API |
| 源帧与读者租约 | `SourceImageFrame.cs` | 引用计数、revision 与最后读者退出后的释放，完整规则见[源图像帧](./image-frame-lifetime.md) |
| 伪彩类型与日志 | `ColormapTypes.cs`、`NativeLogBridge.cs` | 颜色映射枚举与 native 日志桥接；日志默认关闭 |

`HImage` 的 ABI 布局、`isDispose` 含义及不同 native 函数的返回/释放约定由 [native 集成](../../02-developer-guide/engine-development/opencv-integration.md)维护。缓冲区不能仅因包装在 struct 中就按托管对象生命周期处理，也不能把不同函数族的整数返回值解释成统一成功码。

## 位图与调用结果

调用方先确认输入的尺寸、通道、位深、stride 和缓冲区有效期，再进入对应 native 接口。接口可能返回结构体、JSON 指针或写入输出参数，并非所有调用都经过同一条“返回 HImage → 显示位图”流程。

需要显示时，使用具体的位图桥接入口并遵守 WPF 线程和资源所有权要求。`ToPixelFormat` 的格式选择本身不是对任意像素布局的完整验证；异步复制与 native 调用返回也不能代替上层文档提交、叠加和渲染完成信号。

`HImageExtension` 中普通转换、带 Dispose 的转换、复制和借用入口的生命周期不同。`ToWriteableBitmapAndDispose` 会在 finally 中 Dispose 参数副本；若该副本负责释放缓冲，原副本仍保留的指针也会失效。完整转换契约及测试见[源图像帧与内存生命周期](./image-frame-lifetime.md)，不要为同一指针建立多个不协调的释放者。

## CUDA 选择与 Fusion

`ImageCompute.UseCuda` 的初始值来自 CUDA 驱动初始化与设备数量检查，上层配置可以覆盖它。这不是纯常量读取，也不校验 `opencv_cuda.dll` 的所有算法入口或本次输入。`Fusion` 根据该值直接选择 `OpenCVCuda.CM_Fusion` 或 `OpenCVMediaHelper.M_Fusion`；GPU 调用失败后没有自动 CPU 重试。

Auto/CPU/GPU 的窗口入口、输入数量限制、取消、计时、显示与保存统一见[景深融合](./image-fusion.md)。需要 CPU 模式时在调用前明确选择，不能把异常后的回退当作已有保障。

## 原生日志初始化

`InitializeWithResult()` 的 `enableLogs` 和 `enableNativeSink` 默认均为 false。仅订阅 `LogReceived` 不会初始化来源或启用捕获。初始化会尝试加载 helper，并接入已加载的 CUDA 来源；捕获已启用但 CUDA 尚未接入时，后续托管 CUDA 调用可尝试接入。

| 状态 | 能说明什么 |
| --- | --- |
| `IsInitialized` | 已执行初始化流程，不保证任一 DLL/日志 ABI 可用 |
| `IsEnabled` | 捕获是否启用，不能代替各来源状态 |
| `LastInitializationResult.HelperAvailable` / `CudaAvailable` | 本次记录的对应来源是否可用；一个来源可用不代表另一个可用 |
| `LastInitializationResult.Diagnostics` | 初始化/接入诊断，用于定位缺 DLL、导出或配置问题 |

CUDA 包装准备日志时隔离诊断异常，但实际算法调用仍有自己的失败路径。初始化日志可能加载 native DLL，不能把它当作纯元数据查询。

## 构建与运行依赖

Core 当前目标框架为 `net8.0-windows7.0;net10.0-windows7.0`，native 资产面向 Windows x64。`ColorVision.Core.csproj` 将 helper、CUDA 与列出的 OpenCV runtime 放入 `runtimes/win-x64/native` 并复制到输出；实际加载还取决于宿主输出和 DLL 依赖是否完整。

仓库构建优先复用符合路径选择条件的 `opencv_helper.dll`，缺失时可加入 C++ 项目引用，因此首次构建不能只假定需要 .NET SDK。当前 csproj 无条件声明 `opencv_cuda.dll` 打包输入：运行时不用 CUDA，不等于构建时可以缺少该文件。DLL 选择和工具链前提见 [native 集成](../../02-developer-guide/engine-development/opencv-integration.md)，NuGet 检查见[包构建与发布](./publishing.md)。

## 常见问题

| 现象 | 检查顺序 |
| --- | --- |
| `DllNotFoundException` | 对应 helper/CUDA DLL、它的运行依赖、宿主输出及 native 资产路径 |
| `EntryPointNotFoundException` | 托管声明的导出名、调用约定与实际 DLL 版本 |
| `BadImageFormatException` | 托管宿主、插件和 native DLL 的位数与文件有效性 |
| 黑屏、错色或行错位 | `HImage` 尺寸/通道/位深/stride、位图格式和复制入口，不先归因于算法 |
| 批处理后内存持续上涨 | 谁分配和释放输出、是否仍有帧租约，以及借用数据是否超出有效期 |
| CUDA Fusion 失败 | 部署依赖、驱动/设备和输入限制；`UseCuda=true` 不证明本次算法可用 |
| 原生日志未出现 | 捕获开关、来源可用性、日志等级及初始化诊断，不能只看 `IsInitialized` |

## 验证范围

相关测试位于 `Test/ColorVision.UI.Tests/`：

| 测试 | 覆盖范围 |
| --- | --- |
| `HImageAbiTests` | 托管声明的布局、大小和字段偏移，不能独自证明实际 native DLL 的 ABI |
| `HImageExtensionCopyTests`、`VideoFrameCopyTests` | 位图复制、行填充和格式等边界；后者不等于真实视频采集或解码验收 |
| `NativeLogBridgeTests` | 默认参数、导出前缀、回调解码与隔离；另含真实 helper 日志回传调用 |
| `LuminousAreaNativeInteropTests` | 声明检查及亮区 V2 集成；真实导出用例有 `COLORVISION_RUN_LUMINOUS_NATIVE_V2_TESTS=1` 门禁 |

运行前区分纯托管检查、加载真实 DLL 和实际设备验证。交付时还需核对包资产及上层实际打开、显示和释放结果；测试文件存在或托管构建成功，都不能证明相机、CUDA 设备和全部 native 导出已验证。
