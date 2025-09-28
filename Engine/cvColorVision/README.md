# cvColorVision

## 功能定位

视觉处理核心模块，提供图像处理、色彩分析和视觉算法的底层实现。

## 作用范围

核心算法引擎，为上层应用提供专业的视觉检测和图像分析功能。

## 主要功能点

- **图像处理算法** - 基础图像操作、滤波、增强等
- **色彩空间转换** - RGB、HSV、Lab、XYZ 等色彩空间互转
- **视觉检测算法** - MTF、FOV、畸变、SFR、鬼影检测等
- **ROI 区域分析** - 关注点提取和分析
- **相机标定** - 相机内参外参标定和畸变校正
- **图像质量评估** - 各类图像质量指标计算
- **文件格式支持** - CVRaw、CVCIE 等专有格式处理

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.Engine 直接引用
- 各 Algorithm 节点调用其算法接口

**引用的外部依赖**:
- OpenCV 库 - 基础图像处理
- 自定义 C++ 算法库

## 使用方式

### 引用方式
作为项目内引用，通过 ColorVision.Engine 间接使用

### 在主程序中的启用
- 通过流程节点自动调用
- 支持算法模板配置参数

## 开发调试

```bash
dotnet build Engine/cvColorVision/cvColorVision.csproj
```

## 目录说明

- 包含视觉算法的核心实现代码
- 与 C++ 算法库的接口封装

## 相关文档链接

- [算法组件文档](../../docs/algorithms/README.md)
- [流程引擎使用指南](../../docs/engine-components/ColorVision.Engine.md)

## 维护者

ColorVision 算法团队

---
**版本**: 2025.8.9.0