# ColorVision.Core

> 目标框架：.NET 8 / .NET 10 Windows；运行时资产仅提供 x64（以 `ColorVision.Core.csproj` 为准）

## 功能定位

OpenCV 4.14 的 .NET 互操作层，提供高性能图像处理算法调用接口。通过 P/Invoke 调用原生 C++ DLL，封装为易用的 C# API。

## 主要功能

### 图像处理核心
- **HImage** — 基于 OpenCV Mat 的图像封装类，支持高位深（RGB48）图像
- **HImageExtension** — HImage 扩展方法（格式转换、缩放、裁剪等）
- **ImageCompute** — 图像计算（直方图、统计、滤波等）

### 视频媒体
- **OpenCVMediaHelper** — C++/C# 视频解码桥接（FFmpeg + OpenCV）
- **NativeLogBridge** — 可选的 C++ 原生日志回调/事件桥（默认关闭，按需启用）

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
| `NativeLogBridge.cs` | 原生日志回调、级别与动态开关桥接 |

## 依赖关系

- **无项目依赖**，直接引用原生 OpenCV DLL
- **被引用**: ColorVision.ImageEditor

## 构建

```powershell
dotnet build .\UI\ColorVision.Core\ColorVision.Core.csproj -p:Platform=x64
```

> 注意: 仅支持 x64 平台，需要 OpenCV 4.14 原生 DLL
