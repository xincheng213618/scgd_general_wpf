# ColorVision.Core

## 目录
1. [概述](#概述)
2. [核心功能](#核心功能)
3. [主要组件](#主要组件)
4. [使用示例](#使用示例)
5. [性能考虑](#性能考虑)

## 概述

**ColorVision.Core** 是 OpenCV 4.13 的 .NET 互操作层，提供高性能图像处理算法调用接口。通过 P/Invoke 调用原生 C++ DLL，封装为易用的 C# API。

### 基本信息

- **版本**: 1.5.5.1
- **目标框架**: .NET 10.0 Windows（仅 x64）
- **主要功能**: OpenCV 封装、图像处理、视频解码、CUDA 加速
- **技术栈**: P/Invoke, OpenCV 4.13, CUDA
- **允许不安全代码**: 是（AllowUnsafeBlocks）

### 原生依赖

ColorVision.Core 集成了以下 OpenCV 原生库（win-x64）：
- `opencv_core` — 核心功能
- `opencv_imgproc` — 图像处理
- `opencv_videoio` — 视频输入输出
- `opencv_imgcodecs` — 图像编解码

## 核心功能

### 图像处理核心
- **HImage** — 基于 OpenCV Mat 的图像封装类，支持高位深（RGB48）图像
- **HImageExtension** — HImage 扩展方法（格式转换、缩放、裁剪等）
- **ImageCompute** — 图像计算（直方图、统计、滤波等）

### 视频媒体
- **OpenCVMediaHelper** — C++/C# 视频解码桥接（FFmpeg + OpenCV）
- **NativeLogBridge** — C++ 原生日志桥接到 .NET log4net

### CUDA 加速
- **OpenCVCuda** — CUDA 设备检测和 GPU 加速接口
- **nvcuda** — NVIDIA CUDA P/Invoke 定义

### 色彩映射
- **ColormapTypes** — OpenCV 伪彩色映射类型定义

## 主要组件

### HImage

基于 OpenCV Mat 的图像封装类，支持高位深图像。

```csharp
public class HImage : IDisposable
{
    public int Width { get; }
    public int Height { get; }
    public int Channels { get; }
    public IntPtr Data { get; }

    // 从文件加载
    public static HImage Load(string path);

    // 从 byte[] 创建
    public static HImage FromBytes(byte[] data, int width, int height, int channels);

    // 转换为 BitmapSource
    public BitmapSource ToBitmapSource();

    public void Dispose();
}
```

### HImageExtension

HImage 的扩展方法集。

```csharp
public static class HImageExtension
{
    // 格式转换
    public static HImage ConvertTo(this HImage src, PixelFormat format);

    // 缩放
    public static HImage Resize(this HImage src, int width, int height);

    // 裁剪
    public static HImage Crop(this HImage src, Rect roi);

    // 保存
    public static bool Save(this HImage src, string path);
}
```

### ImageCompute

图像计算功能。

```csharp
public static class ImageCompute
{
    // 直方图计算
    public static int[] CalcHistogram(HImage image, int channel = 0);

    // 统计信息
    public static ImageStats GetStats(HImage image);

    // 滤波
    public static HImage GaussianBlur(HImage image, int kernelSize);
}
```

### OpenCVMediaHelper

视频解码桥接，通过 P/Invoke 调用 C++ FFmpeg/OpenCV 解码器。

```csharp
public static class OpenCVMediaHelper
{
    // 打开视频
    public static int OpenVideo(string path);

    // 读取帧
    public static bool ReadFrame(int handle, ref HImage frame);

    // 获取视频信息
    public static VideoInfo GetVideoInfo(int handle);

    // 释放
    public static void Release(int handle);
}
```

### OpenCVCuda

CUDA 设备检测和 GPU 加速接口。

```csharp
public static class OpenCVCuda
{
    // 检测 CUDA 设备
    public static int GetCudaDeviceCount();

    // 检查是否支持 CUDA
    public static bool IsCudaAvailable();

    // GPU 加速图像处理
    public static HImage GpuProcess(HImage image);
}
```

## 文件清单

| 文件 | 说明 |
|------|------|
| `HImage.cs` | 图像封装类 |
| `HImageExtension.cs` | 图像扩展方法 |
| `ImageCompute.cs` | 图像计算 |
| `OpenCVMediaHelper.cs` | 视频解码桥接 |
| `OpenCVCuda.cs` | CUDA 接口 |
| `nvcuda.cs` | CUDA P/Invoke |
| `ColormapTypes.cs` | 色彩映射类型 |
| `NativeLogBridge.cs` | 原生日志桥接 |

## 使用示例

### 1. 加载和显示图像

```csharp
// 加载图像
using var image = HImage.Load("test.png");

// 转换为 WPF BitmapSource
var bitmap = image.ToBitmapSource();
imageView.Source = bitmap;
```

### 2. 图像处理

```csharp
using var image = HImage.Load("input.png");

// 缩放
using var resized = image.Resize(800, 600);

// 高斯模糊
using var blurred = ImageCompute.GaussianBlur(resized, 5);

// 保存
blurred.Save("output.png");
```

### 3. 视频解码

```csharp
int handle = OpenCVMediaHelper.OpenVideo("video.mp4");
var info = OpenCVMediaHelper.GetVideoInfo(handle);
Console.WriteLine($"分辨率: {info.Width}x{info.Height}, 帧率: {info.Fps}");

HImage frame = null;
while (OpenCVMediaHelper.ReadFrame(handle, ref frame))
{
    // 处理帧
    var bitmap = frame.ToBitmapSource();
}

OpenCVMediaHelper.Release(handle);
```

### 4. CUDA 加速

```csharp
if (OpenCVCuda.IsCudaAvailable())
{
    Console.WriteLine($"CUDA 设备数: {OpenCVCuda.GetCudaDeviceCount()}");

    using var image = HImage.Load("large_image.png");
    using var result = OpenCVCuda.GpuProcess(image);
    result.Save("processed.png");
}
```

## 性能考虑

### 1. 内存管理
- HImage 实现 IDisposable，使用后及时释放
- 大图像使用 `using` 语句确保释放
- 避免频繁的 HImage ↔ BitmapSource 转换

### 2. CUDA 加速
- 检测 CUDA 可用性后再使用
- 大图像（>4K）适合 GPU 加速
- 小图像 CPU 处理可能更快（避免数据传输开销）

### 3. 视频解码
- 使用完成后及时调用 `Release` 释放资源
- 高分辨率视频考虑降采样预览

## 依赖关系

- **无项目依赖**，直接引用原生 OpenCV DLL
- **被引用**: ColorVision.ImageEditor

## 更新日志

### v1.5.5.1 (2026-04)
- ✅ 版本号统一升级

### v1.5.2.1 (2026-02)
- ✅ 升级目标框架至 .NET 10.0
- ✅ 更新 OpenCV 原生库至 4.13
- ✅ 新增视频解码支持（OpenCVMediaHelper）
- ✅ 新增 HImage 内存共享机制
- ✅ 优化 C++/C# 跨语言数据传输

## 构建

```bash
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj
```

> 注意: 仅支持 x64 平台，需要 OpenCV 4.13 原生 DLL

## 相关资源

- [ColorVision.ImageEditor](ColorVision.ImageEditor.md) - 图像编辑器
- [性能优化指南](../../02-developer-guide/core-optimization/overview.md)
