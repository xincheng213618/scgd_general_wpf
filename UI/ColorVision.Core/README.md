# ColorVision.Core

> 版本: 1.5.5.1 | 目标框架: .NET 10.0 Windows (仅 x64)

## 功能定位

OpenCV 4.13 的 .NET 互操作层，提供高性能图像处理算法调用接口。通过 P/Invoke 调用原生 C++ DLL，封装为易用的 C# API。

## 主要功能

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

## 依赖关系

- **无项目依赖**，直接引用原生 OpenCV DLL
- **被引用**: ColorVision.ImageEditor

## 构建

```bash
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj
```

> 注意: 仅支持 x64 平台，需要 OpenCV 4.13 原生 DLL
