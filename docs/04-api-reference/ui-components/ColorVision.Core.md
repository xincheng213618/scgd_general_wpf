# ColorVision.Core

本页只描述 UI/ColorVision.Core 当前已经落地的原生互操作层，不再延续旧文档里那种“高层图像 API 手册”和并不存在的托管方法示例。

## 模块定位

ColorVision.Core 当前更接近一个原生图像和视频能力桥接层，主要负责：

- 定义 `HImage` 这类跨托管/非托管边界的数据结构
- 通过 P/Invoke 调用 `opencv_helper.dll`、`opencv_cuda.dll`
- 提供 WPF 侧的位图转换与更新辅助
- 暴露伪彩色、图像增强、聚焦评价、视频相关等原生入口

它不是一个已经封装好的高层图像处理框架。很多能力当前仍然是 `extern` 方法级别的原生导出包装。

## 当前最关键的文件

从项目目录看，最值得优先阅读的是：

- `HImage.cs`：图像数据结构
- `HImageExtension.cs`：`HImage` 与 WPF 图像对象之间的桥接
- `OpenCVMediaHelper.cs`：主要的原生导出包装集合
- `OpenCVCuda.cs`：CUDA 相关原生入口
- `ColormapTypes.cs`：伪彩色枚举
- `NativeLogBridge.cs`：原生日志桥接
- `nvcuda.cs`：CUDA 相关 P/Invoke 定义

## 关键入口类型

### HImage

`HImage` 当前不是旧文档里那种带大量实例方法的托管类，而是一个承载原生图像缓冲区的结构体。它的核心字段包括：

- `rows`
- `cols`
- `channels`
- `depth`
- `stride`
- `pData`

同时它实现了 `Dispose()`，负责释放 `Marshal.AllocHGlobal` 分配的图像内存。

这意味着当前模块最重要的职责之一，就是安全地在原生和托管边界上传递图像缓冲区。

### HImageExtension

`HImageExtension` 提供的是桥接辅助，而不是完整处理算法库。它当前主要负责：

- 根据通道数和位深推导 `PixelFormat`
- 把 `HImage` 内容拷贝到 `WriteableBitmap`
- 提供异步位图更新路径
- 协助把原生图像数据转换成 WPF 可显示对象

因此它的价值主要在显示链，而不是算法链。

### OpenCVMediaHelper

虽然名字叫 `OpenCVMediaHelper`，当前它其实承载了大量 `opencv_helper.dll` 的导出包装，不只是视频相关接口，还包括：

- 伪彩色与自动范围伪彩色
- 最小值/最大值提取
- 自动亮度、自动颜色、自动色调
- 通道提取
- 亮度对比度、Gamma、反相、阈值、锐化、滤波、边缘检测
- SFR 与聚焦评价
- 若干识别或检测类入口
- 视频相关结构和函数

所以当前更准确的理解是：它是主要的原生图像能力导出面，而不只是“视频帮助类”。

### OpenCVCuda

`OpenCVCuda` 当前并不是旧文档里声称的通用 CUDA 设备管理层。它现在公开的是少量 `opencv_cuda.dll` 导出，重点在融合相关入口，例如：

- `CM_Fusion`
- `CM_Fusion_Async`
- `CM_Fusion_Batch`

因此描述 CUDA 能力时，应按当前实际导出写，不要再扩写成完整 GPU 能力总入口。

### ColormapTypes 与 NativeLogBridge

- `ColormapTypes` 负责统一伪彩色映射枚举。
- `NativeLogBridge` 负责把原生侧日志桥接到托管日志系统。

这两个文件都很小，但它们分别是伪彩色链和调试链的重要边界点。

## 当前运行时主链

这套模块当前更像下面这条链：

1. 上层模块通过 P/Invoke 调用 `OpenCVMediaHelper` 或 `OpenCVCuda`。
2. 原生 DLL 返回 `HImage` 或写入 `HImage` 输出参数。
3. WPF 显示链通过 `HImageExtension` 把图像数据更新到 `WriteableBitmap`。
4. 像 `ColorVision.ImageEditor` 这类上层模块继续围绕这些位图做交互、绘制和显示。

## 作为 DLL 使用时

### 应该引用它的场景

- 上层图像模块需要和 `opencv_helper.dll` 交换图像缓冲区。
- 需要将 native 图像数据转换为 WPF `WriteableBitmap`。
- 需要调用伪彩色、图像增强、滤波、阈值、聚焦评价、SFR 或 fusion 相关 native 方法。
- 需要把 native 日志桥接到托管日志体系。

### 使用注意

| 风险点 | 说明 |
| --- | --- |
| x64 native DLL | 必须确认 `opencv_helper.dll`、OpenCV runtime DLL 位于输出目录或 NuGet runtime 目录 |
| 内存所有权 | `HImage` 包含非托管指针，释放边界必须清楚 |
| 线程与 UI | native 计算结果回到 WPF 显示时，要注意 UI 线程和 bitmap 更新方式 |
| CUDA 可选性 | `opencv_cuda.dll` 不是所有环境都有，调用前要按部署包和设备能力判断 |

### 发布注意

NuGet 包必须包含 `runtimes/win-x64/native` 下的 native DLL。只发布托管 DLL 会导致上层功能编译通过但运行时报错。

### DLL 发布验收表

| 验收项 | 要查什么 | 通过标准 |
| --- | --- | --- |
| 目标框架产物 | `net8.0-windows7.0`、`net10.0-windows7.0` | 两个 TFM 都能生成 DLL、`.nupkg`、`.snupkg` |
| native runtime | NuGet 包与宿主输出目录 | `opencv_helper.dll`、OpenCV 4130 系列 DLL 位于 `runtimes/win-x64/native` 或最终输出目录 |
| CUDA 可选包 | `opencv_cuda.dll` | 源文件存在时进入包；缺失时非 CUDA 路径不应因为它失败 |
| P/Invoke 入口 | `OpenCVMediaHelper`、`OpenCVCuda`、`NativeLogBridge` | 不出现 `DllNotFoundException`、`EntryPointNotFoundException`、x86/x64 混用 |
| 图像内存 | `HImage.Dispose()`、`Marshal.FreeHGlobal` | 批量转换后内存能释放，重复打开图像不持续增长 |
| WPF 显示桥接 | `HImageExtension` | `HImage` 到 `WriteableBitmap` 的尺寸、通道、位深和 stride 对齐 |
| 上层回归 | `ColorVision.ImageEditor` | 至少验证一张图像能打开、显示、伪彩或增强后仍可刷新 |

### 现场故障首查

| 现象 | 第一检查点 |
| --- | --- |
| 启动或调用时报 `DllNotFoundException` | 先查 `runtimes/win-x64/native` 与主程序输出目录是否有 native DLL |
| 报 `BadImageFormatException` | 先查宿主、插件、native DLL 是否统一 x64 |
| 图像显示黑屏或颜色错位 | 检查 `HImage` 的 rows、cols、channels、depth、stride 和 WPF `PixelFormat` 推导 |
| 批量处理后内存上涨 | 检查 `HImage.Dispose()` 是否被调用，以及 native 输出缓冲区所有权是否清楚 |
| CUDA fusion 不可用 | 先确认 `opencv_cuda.dll`、NVIDIA 驱动和 CUDA 运行时；再确认是否走了非 CUDA 兜底路径 |
| 原生日志没有进入托管日志 | 检查 `NativeLogBridge` 初始化顺序和 helper/cuda DLL 是否已成功加载 |

## 当前实现有哪些边界

### 不要把它写成高层 OO API

当前代码里并没有旧文档写的这些典型高层接口：

- `HImage.Load(...)`
- `HImage.ToBitmapSource()`
- `OpenCVCuda.GetCudaDeviceCount()`
- `OpenCVCuda.IsCudaAvailable()`

这些写法会误导读者去寻找并不存在的托管封装。

### HImage 的资源语义很重要

`HImage` 不是普通托管对象，包含非托管指针和显式释放逻辑。讨论这个模块时，内存和所有权边界比“类设计”更重要。

### 上层业务语义不在这里

Core 只负责桥接原生能力，不负责像 ImageEditor 那样的工具栏、交互或文档状态编排。阅读时应明确它只是下层能力底座。

## 当前更适合怎样读这个模块

### 想看图像数据结构和显示桥接

先看：

- `HImage.cs`
- `HImageExtension.cs`

### 想看原生导出面

先看：

- `OpenCVMediaHelper.cs`
- `OpenCVCuda.cs`

### 想看伪彩色和日志边界

先看：

- `ColormapTypes.cs`
- `NativeLogBridge.cs`

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 不存在的托管高层方法示例
- 把 `OpenCVCuda` 写成完整设备管理层
- 大段更新日志和版本清单
- 把 Core 说成完整上层图像处理框架

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)
