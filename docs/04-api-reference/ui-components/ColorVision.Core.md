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

`UI/ColorVision.Core/` 是原生图像和视频能力桥接层，负责 `HImage` 数据结构、P/Invoke、WPF 位图转换、伪彩色/增强/聚焦评价/fusion 等 native 入口。它不是高层图像处理框架。

`HImage` 是声明了 ABI 布局的 `struct`，不是托管 OpenCV `Mat` 类；复制结构体不会转移其非托管缓冲区所有权。`ImageCompute` 当前只提供 `UseCuda` 与 `Fusion()` 分流，不提供独立的直方图、统计或滤波托管 API；其它 native 能力应核对具体 P/Invoke 声明。

`SourceImageFrame` / `ImageFrameStore` / `ImageFrameLease` 的引用计数、延迟释放、位图借用与复制、revision 失效及对应测试统一见[源图像帧契约](./image-frame-lifetime.md)。这里由托管代码管理非托管像素缓冲的生命周期，不等于 native ABI 验收或完整算法会话仲裁。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| `DllNotFoundException` | `opencv_helper.dll`、OpenCV runtime、宿主输出目录或 `runtimes/win-x64/native` |
| `EntryPointNotFoundException` | `OpenCVMediaHelper` / `OpenCVCuda` 声明和 native 导出是否一致 |
| `BadImageFormatException` | 宿主、插件、native DLL 是否统一 x64 |
| 图像黑屏或颜色错位 | `HImage.rows/cols/channels/depth/stride` 与 WPF `PixelFormat` |
| 批量处理后内存上涨 | `HImage.Dispose()`、native 输出缓冲区所有权 |
| CUDA fusion 不可用 | `opencv_cuda.dll`、驱动、CUDA runtime、是否需要非 CUDA 兜底 |
| 原生日志没进托管日志 | `NativeLogBridge` 初始化顺序和 native DLL 是否成功加载 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 图像缓冲区 | `HImage.cs` | 承载 rows、cols、channels、depth、stride、`pData`，包含非托管内存释放 |
| 源帧与读者租约 | `SourceImageFrame.cs` | 当前帧缓存、revision 与最后读者退出后的释放；具体契约见源图像帧主题 |
| WPF 显示桥接 | `HImageExtension.cs` | 推导 `PixelFormat`，把 `HImage` 拷贝到 `WriteableBitmap` |
| native 导出面 | `OpenCVMediaHelper.cs` | 包装 `opencv_helper.dll` 的伪彩、增强、滤波、阈值、SFR、聚焦评价、视频等入口 |
| CUDA 入口 | `OpenCVCuda.cs` | 主要是 `CM_Fusion`、`CM_Fusion_Async`、`CM_Fusion_Batch` |
| fusion 选择 | `ImageCompute.cs` | 根据配置在 CUDA 和普通 native fusion 之间选择 |
| 伪彩枚举 | `ColormapTypes.cs` | 统一 colormap 类型 |
| 原生日志 | `NativeLogBridge.cs` | native 日志桥接到托管日志体系 |

## 运行链路

1. 上层模块调用 `OpenCVMediaHelper`、`OpenCVCuda` 或 `ImageCompute`。
2. native DLL 返回 `HImage` 或写入输出参数。
3. `HImageExtension` 把图像缓冲区转为 WPF 可显示的 `WriteableBitmap`。
4. `ColorVision.ImageEditor` 等上层模块继续做交互、绘制和显示。

## 原生日志初始化边界

`NativeLogBridge` 的日志捕获默认关闭，`InitializeWithResult()` 的 `enableLogs` 和 `enableNativeSink` 默认都是 `false`；仅订阅 `LogReceived` 不会初始化来源或启用捕获。初始化会尝试加载 helper，并尝试接入已加载的 CUDA 来源；未接入的 CUDA 在捕获已启用时可由后续托管 CUDA 调用尝试接入。

`IsInitialized` 表示已走过初始化，不证明 DLL/日志 ABI 可用，也不等于 `IsEnabled`。诊断时分别核对 `LastInitializationResult` 的 `HelperAvailable`、`CudaAvailable`、`Diagnostics` 及捕获开关；有一个来源可用不表示两者都可用。初始化可能加载 native DLL，不能仅为文档验证运行它。

## 使用边界

| 边界 | 说明 |
| --- | --- |
| x64 native DLL | native DLL 必须随宿主输出或 NuGet runtime 发布 |
| 内存所有权 | `HImage` 含非托管指针，释放责任必须明确 |
| UI 线程 | native 计算结果回到 WPF 显示时，要处理 bitmap 更新线程 |
| CUDA 可选 | `opencv_cuda.dll` 不是所有环境都有，调用前要按部署和设备能力判断 |
| 高层交互 | 工具栏、绘制、文档状态在 `ColorVision.ImageEditor`，不在 Core |

## 发布验收

| 验收项 | 要查什么 |
| --- | --- |
| 目标框架 | `ColorVision.Core.csproj` 的 `net8.0-windows7.0;net10.0-windows7.0` |
| native runtime | `opencv_helper.dll`、OpenCV runtime 是否进入 `runtimes/win-x64/native` 或最终输出目录 |
| CUDA 发布输入 | 当前 csproj 无条件包含 `x64/Release/opencv_cuda.dll`；运行时不选择 CUDA 与构建时可缺少 DLL 是两件事 |
| P/Invoke | 不出现 `DllNotFoundException`、`EntryPointNotFoundException`、x86/x64 混用 |
| 图像内存 | 批量转换后内存可释放，重复打开图像不持续增长 |
| WPF 显示 | 尺寸、通道、位深、stride 和 `PixelFormat` 对齐 |
| 上层回归 | 至少用 `ColorVision.ImageEditor` 打开、显示、伪彩或增强一张图像 |

## 不要再这样写

首次拉仓若没有可复用的 `opencv_helper.dll`，Core 会加入 native C++ 项目引用；构建前提和 DLL 选择顺序见 [native 集成](../../02-developer-guide/engine-development/opencv-integration.md)。不能只因目标是 C# 项目就承诺安装 .NET SDK 后一定可构建。

- 不要写 `HImage.Load(...)`、`HImage.ToBitmapSource()` 这类当前不存在的托管高层 API。
- 不要把 `OpenCVCuda` 写成完整 CUDA 设备管理层；当前公开入口很少。
- 不要把 Core 写成完整图像处理框架；它主要是 native 桥接和显示底座。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 图像数据结构 | `HImage.cs` |
| WPF 显示桥接 | `HImageExtension.cs` |
| native 导出包装 | `OpenCVMediaHelper.cs`、`OpenCVCuda.cs` |
| fusion 调用选择 | `ImageCompute.cs` |
| 伪彩和日志边界 | `ColormapTypes.cs`、`NativeLogBridge.cs` |

景深融合的 Auto/CPU/GPU 分流、窗口取消与计时、结果显示和保存边界见[景深融合契约](./image-fusion.md)。`UseCuda` 是选路开关，不是 native 部署健康检查或 GPU 失败后的 CPU 回退机制。

## 验证入口与缺口

关联测试：`HImageAbiTests.cs` 与 `HImageExtensionCopyTests.cs` 检查结构布局和位图拷贝；`NativeLogBridgeTests.cs` 检查默认参数、回调隔离及 helper 日志回传，后者包含真实 DLL 调用。另有 `LuminousAreaNativeInteropTests.cs`、`VideoFrameCopyTests.cs`，均位于 `Test/ColorVision.UI.Tests/`。

native 测试依赖真实 DLL；测试入口存在不表示相机、CUDA 设备或全部导出 ABI 已在当前机器验证。
