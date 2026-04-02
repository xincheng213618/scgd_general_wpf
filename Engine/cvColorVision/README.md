# cvColorVision

> 版本: 2025.8.9.0 | 目标框架: .NET 8.0 / .NET 10.0 Windows

## 🎯 功能定位

cvColorVision 是 ColorVision 系统的视觉处理核心模块，提供图像采集、色彩分析和视觉算法的底层实现。它是一个 C# 封装库，通过 P/Invoke 技术调用底层 C++ DLL（cvCamera.dll、cvOled.dll）实现高性能的图像处理功能。

## 主要功能点

### 相机控制模块
- **相机初始化与配置** - 支持多种相机型号
- **图像采集** - 实时预览和图像采集
- **参数控制** - 曝光、增益、白平衡等参数设置
- **多型号支持** - QHY、HK、MIL、TOUP 等相机

### 色彩空间转换
- **XYZ色彩空间** - XYZ色彩空间转换
- **色度坐标** - xy/uv色度坐标计算
- **色差计算** - ΔE色差计算
- **色温计算** - CCT色温和主波长计算

### 传感器通信
- **TCP通信** - TCP/IP网络通信
- **串口通信** - 串口（Serial）通信
- **指令收发** - 传感器指令收发

### 光谱仪支持
- **光谱数据** - COLOR_PARA数据结构
- **色度参数** - 色坐标、色度、色温、显色性
- **显色指数** - Ra和R1-R15显色指数

### OLED算法
- **像素检测** - OLED像素检测
- **像素重建** - 像素重建算法
- **摩尔纹滤波** - 摩尔纹滤波处理

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                    cvColorVision (C#)                         │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │    Camera   │    │   Color     │    │   Sensor    │      │
│  │   Control   │    │   Space     │    │   Comm      │      │
│  │             │    │             │    │             │      │
│  │ • 初始化    │    │ • XYZ转换   │    │ • TCP通信   │      │
│  │ • 图像采集  │    │ • xy/uv计算 │    │ • 串口通信  │      │
│  │ • 参数控制  │    │ • ΔE计算    │    │ • 指令收发  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │ Spectrometer│    │    OLED     │    │     PG      │      │
│  │             │    │  Algorithm  │    │             │      │
│  │             │    │             │    │             │      │
│  │ • 光谱数据  │    │ • 像素检测  │    │ • 图案切换  │      │
│  │ • 色度参数  │    │ • 像素重建  │    │ • TCP/串口  │      │
│  │ • 显色指数  │    │ • 摩尔纹滤波│    │ • 帧控制    │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
└────────────────────────┬─────────────────────────────────────┘
                         │ P/Invoke
┌────────────────────────┴─────────────────────────────────────┐
│              cvCamera.dll / cvOled.dll (C++)                  │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │   OpenCV    │    │    CUDA     │    │  Camera SDK │      │
│  │             │    │             │    │             │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 使用方式

### 引用方式

```xml
<ProjectReference Include="..\cvColorVision\cvColorVision.csproj" />
```

### 相机控制

```csharp
using cvColorVision;

// 初始化相机
IntPtr handle = cvCameraCSLib.CM_Init(CameraType.CV_Q);

// 打开相机
cvCameraCSLib.CM_Open(handle, 0);

// 设置曝光时间
cvCameraCSLib.CM_SetExposureTime(handle, 100000); // 100ms

// 采集图像
byte[] imageData = new byte[width * height * channels * bpp / 8];
cvCameraCSLib.CM_GetImage(handle, imageData);

// 关闭相机
cvCameraCSLib.CM_Close(handle);
cvCameraCSLib.CM_Uninit(handle);
```

### 色彩空间转换

```csharp
using cvColorVision;

// 初始化 XYZ 转换
IntPtr handle = ConvertXYZ.CM_InitXYZ(cameraHandle);

// 设置图像缓冲区
ConvertXYZ.CM_SetBufferXYZ(handle, width, height, bpp, channels, imageData);

// 获取 XYZ 值（圆形区域）
float X = 0, Y = 0, Z = 0, x = 0, y = 0, u = 0, v = 0;
ConvertXYZ.CM_GetXYZxyuvCircle(handle, posX, posY, 
    ref X, ref Y, ref Z, ref x, ref y, ref u, ref v, radius);

// 释放资源
ConvertXYZ.CM_UnInitXYZ(handle);
```

### 传感器通信

```csharp
using cvColorVision;

// 初始化 TCP 传感器
IntPtr handle = SensorComm.CM_InitSensorComm(Communicate_Type.Communicate_Tcp);

// 连接到传感器
bool connected = SensorComm.CM_ConnectToSensor(handle, "192.168.1.100", 5000);

// 发送指令
byte[] response = new byte[1024];
SensorComm.CM_SendCmdToSensorEx(handle, "MEAS?", response, 5000);

// 断开连接
SensorComm.CM_DestroySensor(handle);
SensorComm.CM_UnInitSensorComm(handle);
```

## 主要组件

### cvCameraCSLib
相机控制主模块。

```csharp
public static class cvCameraCSLib
{
    // 初始化和释放
    public static IntPtr CM_Init(CameraType type);
    public static int CM_Uninit(IntPtr handle);
    
    // 相机开关
    public static int CM_Open(IntPtr handle, int index);
    public static int CM_Close(IntPtr handle);
    
    // 图像采集
    public static int CM_GetImage(IntPtr handle, byte[] buffer);
    
    // 参数设置
    public static int CM_SetExposureTime(IntPtr handle, int expTime);
    public static int CM_SetGain(IntPtr handle, int gain);
    public static int CM_SetWhiteBalance(IntPtr handle, int r, int g, int b);
}
```

### ConvertXYZ
色彩空间转换模块。

```csharp
public static class ConvertXYZ
{
    // 初始化和释放
    public static IntPtr CM_InitXYZ(IntPtr cameraHandle);
    public static int CM_UnInitXYZ(IntPtr handle);
    
    // 设置缓冲区
    public static int CM_SetBufferXYZ(IntPtr handle, int width, int height, 
        int bpp, int channels, byte[] data);
    
    // 获取 XYZ 值
    public static int CM_GetXYZxyuvCircle(IntPtr handle, int x, int y,
        ref float X, ref float Y, ref float Z,
        ref float x, ref float y, ref float u, ref float v, int radius);
    
    public static int CM_GetXYZxyuvRect(IntPtr handle, int x, int y, int w, int h,
        ref float X, ref float Y, ref float Z,
        ref float x, ref float y, ref float u, ref float v);
}
```

### Spectrometer
光谱仪支持模块。

```csharp
public static class Spectrometer
{
    // 初始化和释放
    public static IntPtr CM_InitSpectrum();
    public static int CM_UnInitSpectrum(IntPtr handle);
    
    // 连接设备
    public static int CM_Connect(IntPtr handle, string port, int baudRate);
    public static int CM_Disconnect(IntPtr handle);
    
    // 测量
    public static int CM_Measure(IntPtr handle, ref COLOR_PARA result);
    
    // 参数设置
    public static int CM_SetIntegrationTime(IntPtr handle, int time);
}
```

## 目录说明

- `cvCameraCSLib.cs` - 相机控制主模块
- `ConvertXYZ.cs` - 色彩空间转换
- `SensorComm.cs` - 传感器通信
- `Spectrometer.cs` - 光谱仪支持
- `CvOledDLL.cs` - OLED算法接口
- `PG.cs` - 图案生成器控制
- `PassSx.cs` - 电源控制
- `AoiParam.cs` - AOI缺陷检测参数
- `KeyBoard.cs` - 键盘光晕检测
- `Algorithms.cs` - 基础算法
- `CMStruct.cs` - 数据结构定义
- `Util/` - 工具类

## 开发调试

```bash
# 构建项目
dotnet build Engine/cvColorVision/cvColorVision.csproj

# 构建整个解决方案
dotnet build ColorVision.sln
```

### 调试注意事项

1. **确保 DLL 可访问**: cvCamera.dll 和 cvOled.dll 需要在输出目录或系统 PATH 中
2. **平台匹配**: 注意 x86/x64 平台匹配
3. **相机权限**: 相机访问可能需要管理员权限
4. **资源释放**: 使用后务必释放 IntPtr 句柄，避免内存泄漏

## 最佳实践

### 1. 资源管理
```csharp
// 使用 try-finally 确保资源释放
IntPtr handle = IntPtr.Zero;
try
{
    handle = cvCameraCSLib.CM_Init(CameraType.CV_Q);
    // 使用相机
}
finally
{
    if (handle != IntPtr.Zero)
    {
        cvCameraCSLib.CM_Close(handle);
        cvCameraCSLib.CM_Uninit(handle);
    }
}
```

### 2. 错误处理
```csharp
int result = cvCameraCSLib.CM_GetImage(handle, buffer);
if (result != 0)
{
    string error = cvCameraCSLib.CM_GetErrorString(result);
    Log.Error($"图像采集失败: {error}");
}
```

### 3. 缓冲区大小计算
```csharp
// 正确计算缓冲区大小
int bufferSize = width * height * channels * bpp / 8;
byte[] buffer = new byte[bufferSize];
```

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/engine-components/cvColorVision.md)
- [算法组件文档](../../docs/04-api-reference/algorithms/README.md)
- [ColorVision.Engine README](../ColorVision.Engine/README.md)

## 依赖关系

### 对外依赖（被引用）
- **ColorVision.Engine** - 直接引用此项目
- **算法节点** - 通过 ColorVision.Engine 调用算法接口

### 对内依赖（引用其他）
- **cvCamera.dll** - C++ 相机控制和图像处理库
- **cvOled.dll** - C++ OLED专用算法库
- **Newtonsoft.Json** - JSON序列化库

## 常见问题

### Q: DllNotFoundException 异常
**A**: 确保 cvCamera.dll 在以下位置之一：
- 与 exe 相同目录
- 系统 PATH 环境变量
- 项目输出目录 (bin/Debug 或 bin/Release)

### Q: 相机打开失败
**A**: 检查：
1. 相机是否连接并正常识别
2. 相机驱动是否安装
3. 是否被其他程序占用
4. 权限是否足够

## 维护者

ColorVision 算法团队
