# ColorVision.Core

`UI/ColorVision.Core/` 是原生图像和视频能力桥接层，负责 `HImage` 数据结构、P/Invoke、WPF 位图转换、伪彩色/增强/聚焦评价/fusion 等 native 入口。它不是高层图像处理框架。

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
| CUDA 可选包 | `opencv_cuda.dll` 存在时进入包；缺失时非 CUDA 路径不失败 |
| P/Invoke | 不出现 `DllNotFoundException`、`EntryPointNotFoundException`、x86/x64 混用 |
| 图像内存 | 批量转换后内存可释放，重复打开图像不持续增长 |
| WPF 显示 | 尺寸、通道、位深、stride 和 `PixelFormat` 对齐 |
| 上层回归 | 至少用 `ColorVision.ImageEditor` 打开、显示、伪彩或增强一张图像 |

## 不要再这样写

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
