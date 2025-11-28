# cvColorVision


## 目录
1. [功能定位](#功能定位)
2. [技术架构](#技术架构)
3. [核心模块](#核心模块)
4. [文件结构](#文件结构)
5. [依赖关系](#依赖关系)
6. [使用方式](#使用方式)
7. [开发调试](#开发调试)
8. [相关文档](#相关文档)

## 功能定位

cvColorVision 是 ColorVision 系统的**视觉处理核心模块**，提供图像采集、色彩分析和视觉算法的底层实现。它是一个 C# 封装库，通过 P/Invoke 技术调用底层 C++ DLL（cvCamera.dll、cvOled.dll）实现高性能的图像处理功能。

### 作用范围
- 核心算法引擎，为上层应用提供专业的视觉检测和图像分析功能
- 硬件设备接口层，统一管理相机、传感器、光谱仪等设备
- 图像数据处理层，提供色彩空间转换、图像增强等功能

## 技术架构

```
cvColorVision (C# 封装层)
    ├── P/Invoke 调用
    │   ├── cvCamera.dll (C++ 相机和算法库)
    │   └── cvOled.dll (C++ OLED 专用算法)
    └── 对外接口
        └── ColorVision.Engine (上层应用)
```

### 实现语言
- **封装层**: C# (.NET)
- **底层库**: C++ (OpenCV)
- **接口方式**: P/Invoke (DllImport)

## 核心模块

### 1. 相机控制模块
**文件**: `cvCameraCSLib.cs` (1061 行)
- 相机初始化与配置
- 图像采集与实时预览
- 相机参数控制（曝光、增益、白平衡等）
- 多种相机型号支持（QHY、HK、MIL、TOUP 等）

### 2. 色彩空间转换模块
**文件**: `ConvertXYZ.cs` (98 行)
- XYZ 色彩空间转换
- xy/uv 色度坐标计算
- 色差计算（ΔE）
- 色温（CCT）和主波长计算
- 区域（Circle/Rect）色彩测量

### 3. 传感器通信模块
**文件**: `SensorComm.cs` (115 行)
- TCP/IP 网络通信
- 串口（Serial）通信
- 传感器指令收发
- TCP Server 创建与管理
- 客户端连接管理

### 4. 光谱仪支持模块
**文件**: `Spectrometer.cs` (138 行)
- 光谱数据结构定义（COLOR_PARA）
- 光谱参数：色坐标、色度、色温、显色性
- 主波长、峰值波长、半波宽
- 显色性指数 Ra 和 R1-R15

### 5. OLED 算法模块
**文件**: 
- `CvOledDLL.cs` (26 行) - OLED 算法接口
- `CVLED_COLOR.cs` (9 行) - LED 颜色枚举
- `CVOLED_ERROR.cs` (15 行) - 错误码定义

**功能**:
- OLED 像素检测
- 像素重建
- 摩尔纹滤波

### 6. 图案生成器模块
**文件**: `PG.cs` (50 行)
- Pattern Generator 控制
- 支持 GX09C_LCM 和 SKYCODE
- 图案切换（上/下/指定帧）
- TCP/串口通信支持

### 7. 电源控制模块
**文件**: `PassSx.cs` (73 行)
- 电源设备控制
- 电压/电流测量
- 单步测量和序列扫描
- 源表（Source Meter）操作

### 8. AOI 缺陷检测模块
**文件**: `AoiParam.cs` (44 行)
- 缺陷检测参数配置
- 面积、对比度过滤
- 轮廓检测
- Blob 分析

### 9. 键盘光晕检测模块
**文件**: `KeyBoard.cs` (24 行)
- 键盘亮度检测
- 光晕（Halo）计算
- 键盘区域分析

### 10. 基础算法模块
**文件**: `Algorithms.cs` (13 行)
- 点检测算法（forPoint）
- 图像二值化
- 点位提取

### 11. 数据结构定义
**文件**: `CMStruct.cs` (155 行)
- 相机模式和型号枚举
- 图像通道类型
- 标定类型
- 采集模式
- FOV 类型

### 12. 工具类
**文件**: `Util/CfgFile.cs` (50 行)
- JSON 配置文件读写
- 配置序列化/反序列化

## 文件结构

```
cvColorVision/
├── README.md                    # 本文档
├── cvColorVision.csproj         # 项目配置文件
│
├── 相机控制
│   ├── cvCameraCSLib.cs         # 相机控制主模块 (1061 行)
│   └── CMStruct.cs              # 相机数据结构 (155 行)
│
├── 色彩处理
│   ├── ConvertXYZ.cs            # 色彩空间转换 (98 行)
│   └── Spectrometer.cs          # 光谱仪支持 (138 行)
│
├── 设备通信
│   ├── SensorComm.cs            # 传感器通信 (115 行)
│   ├── PG.cs                    # 图案生成器 (50 行)
│   └── PassSx.cs                # 电源控制 (73 行)
│
├── 图像算法
│   ├── Algorithms.cs            # 基础算法 (13 行)
│   ├── AoiParam.cs              # AOI 检测 (44 行)
│   ├── KeyBoard.cs              # 键盘检测 (24 行)
│   ├── CvOledDLL.cs             # OLED 算法 (26 行)
│   ├── CVLED_COLOR.cs           # LED 颜色 (9 行)
│   └── CVOLED_ERROR.cs          # 错误码 (15 行)
│
└── 工具类
    └── Util/
        └── CfgFile.cs           # 配置文件 (50 行)
```

## 依赖关系

### 对外依赖（被引用）
- **ColorVision.Engine** - 直接引用此项目
- **算法节点** - 通过 ColorVision.Engine 调用算法接口

### 对内依赖（引用其他）
- **cvCamera.dll** - C++ 相机控制和图像处理库
- **cvOled.dll** - C++ OLED 专用算法库
- **Newtonsoft.Json** - JSON 序列化库
- **.NET Framework/Core** - 基础运行时

### 底层 C++ 库依赖
- **OpenCV 4.x** - 图像处理基础库
- **CUDA** (可选) - GPU 加速
- **相机 SDK** - 各厂商相机驱动

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\cvColorVision\cvColorVision.csproj" />
```

### 基本使用示例

#### 1. 相机控制
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

#### 2. 色彩空间转换
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

#### 3. 传感器通信
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

### 在主程序中的启用
- 通过流程节点自动调用
- 支持算法模板配置参数
- 与设备服务集成

## 开发调试

### 构建项目
```bash
# 构建 cvColorVision 项目
dotnet build Engine/cvColorVision/cvColorVision.csproj

# 构建整个解决方案
dotnet build ColorVision.sln
```

### 调试注意事项
1. **确保 DLL 可访问**: cvCamera.dll 和 cvOled.dll 需要在输出目录或系统 PATH 中
2. **平台匹配**: 注意 x86/x64 平台匹配
3. **相机权限**: 相机访问可能需要管理员权限
4. **资源释放**: 使用后务必释放 IntPtr 句柄，避免内存泄漏

### 调试工具
- Visual Studio 调试器
- Dependency Walker (检查 DLL 依赖)
- Process Monitor (监控文件和注册表访问)

## 相关文档

### 内部文档
- [算法组件文档](../../docs/04-api-reference/algorithms/README.md)
- [流程引擎使用指南](../../docs/04-api-reference/engine-components/ColorVision.Engine.md)
- [cvColorVision 详细文档](../../docs/04-api-reference/engine-components/cvColorVision.md)

### 外部资源
- [OpenCV 文档](https://docs.opencv.org/)
- [P/Invoke 教程](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)

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

### Q: 图像数据异常
**A**: 验证：
1. 图像缓冲区大小是否正确 (width × height × channels × bpp / 8)
2. 像素格式是否匹配
3. 字节序是否正确

## 维护者

ColorVision 算法团队

---
**版本**: 2025.8.9.0  
**最后更新**: 2025-01-XX