# ProjectARVRPro

## 功能定位

ARVR Pro 专业版测试系统 - 针对AR/VR显示设备的全面光学性能测试解决方案

## 作用范围

专为AR/VR显示设备提供专业级光学测试，涵盖显示质量、光学性能、缺陷检测等全方位测试需求。

## 主要功能点

- **初始化管理** - 通过ProjectARVRProInit进行完整的测试参数初始化
- **多模式处理** - 支持多种测试模式和图像处理算法：
  - **White255Process** - 白色255灰度测试处理
  - **White51Process** - 白色51灰度测试处理  
  - **W25Process** - W25测试模式处理
  - **BlackProcess** - 黑色画面测试处理
  - **ChessboardProcess** - 棋盘格测试处理
  - **DistortionProcess** - 畸变测试处理
  - **MTFHVProcess** - 水平垂直MTF测试处理
  - **OpticCenterProcess** - 光轴中心测试处理
- **Recipe管理** - 完整的测试配方管理系统
- **数据分析** - 客观测试结果分析和修正功能
- **流程管理** - 基于大流程模板的测试执行
- **结果管理** - 测试结果的存储、查询和展示

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 使用核心引擎功能
- ColorVision.Engine.Templates - 模板管理支持
- ColorVision.Engine.Templates.Jsons.LargeFlow - 大流程模板支持
- ColorVision.UI - 基础UI组件

**被引用方式**:
- 作为插件集成到主程序
- 支持独立窗口和大流程模式

## 使用方式

### 引用方式
作为独立插件项目，通过插件系统加载

### 在主程序中的启用
- 插件自动加载和注册
- 支持模板编辑器和流程工具集成

### 初始化流程
传入`ProjectARVRProInit`参数，系统会自动初始化整个测试参数信息，包括：
- 测试配方配置
- 算法参数设定
- 硬件设备初始化
- 流程模板加载

## 开发调试

```bash
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj
```

## 目录说明

- `ProjectARVRProConfig.cs` - 项目主配置类
- `ARVRWindow.xaml/.cs` - 主测试窗口
- `ARVRRecipeConfig.cs` - Recipe配置管理
- `ViewResultManager.cs` - 结果视图管理
- `ProcessManagerWindow.xaml/.cs` - 流程管理窗口
- `EditRecipeWindow.xaml/.cs` - Recipe编辑窗口
- `EditFixWindow.xaml/.cs` - 修正参数编辑窗口
- `Services/` - 服务模块目录
- `PluginConfig/` - 插件配置目录

### 核心处理模块
- `White255Process.cs` - 白255处理模块
- `White51Process.cs` - 白51处理模块
- `W25Process.cs` - W25处理模块
- `BlackProcess.cs` - 黑屏处理模块
- `ChessboardProcess.cs` - 棋盘格处理模块
- `DistortionProcess.cs` - 畸变处理模块
- `MTFHVProcess.cs` - MTF处理模块
- `OpticCenterProcess.cs` - 光轴中心处理模块

## 配置说明

### 测试配方管理
- 支持多种测试配方的创建和编辑
- 参数化的测试流程配置
- 结果数据的自动分析和修正

### 大流程支持
- 基于LargeFlow模板的复杂测试流程
- 支持模板编辑和流程可视化配置

## 相关文档链接

- [ARVR测试规范](../../docs/testing/ARVR-testing.md)
- [光学测试算法](../../docs/algorithms/optical-testing.md)
- [流程引擎文档](../../docs/engine-components/README.md)

## 维护者

ColorVision 项目团队