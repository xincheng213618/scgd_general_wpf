# ProjectARVRPro

## 🎯 功能定位

ARVR Pro 专业版测试系统 - 针对AR/VR显示设备的全面光学性能测试解决方案

## 作用范围

专为AR/VR显示设备提供专业级光学测试，涵盖显示质量、光学性能、缺陷检测等全方位测试需求。

## 主要功能点

- **初始化管理** - 通过 ProjectARVRProInit 进行完整的测试参数初始化
- **多模式处理** - 覆盖核心测试/修正流程：
  - **White255Process / W25Process** - 白场/灰阶亮度测试
  - **RedProcess / GreenProcess / BlueProcess** - 单色通道测试与修正
  - **BlackProcess** - 黑场表现与噪声测试
  - **ChessboardProcess** - 棋盘格测试与几何精度
  - **DistortionProcess** - 畸变测试与校正
  - **MTFHVProcess** - 水平/垂直 MTF 测试
  - **OpticCenterProcess** - 光轴中心定位与偏移分析
- **Recipe管理** - 完整的测试配方管理系统
- **数据分析** - 客观测试结果分析和修正功能
- **流程管理** - 基于大流程模板的测试执行
  - **ProcessMeta管理** - 支持多流程配置和选择性执行
  - **IsEnabled属性** - 按需启用/禁用测试步骤，自动跳过禁用项
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

### 配置与流程裁剪
- 在 ProcessManager 中可通过 `IsEnabled` 控制具体流程是否执行
- `ProcessMetas.json` 持久化流程开关状态，执行时自动跳过禁用步骤
- 详见 `Process/README_IsEnabled_Feature.md`

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
  - 支持多流程配置管理
  - IsEnabled属性控制步骤执行
  - 可选择性跳过禁用的测试步骤
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
- ProcessMeta选择性执行：通过IsEnabled属性控制步骤是否执行，系统自动跳过禁用步骤

### ProcessMeta管理
- **流程配置**：支持创建、更新、删除和排序多个测试流程
- **选择性执行**：每个ProcessMeta都有IsEnabled属性（默认启用）
- **智能跳过**：执行时自动跳过已禁用的步骤
- **完成检测**：仅基于启用的步骤判断测试是否完成
- **示例**：如果配置了8个步骤(0-7)，仅启用第0和第7步，执行流程将是：0→7（跳过1-6）

## 相关文档链接

- [ProjectARVRPro 项目说明](../../docs/05-resources/project-structure/project-arvrpro.md)
- [ProjectARVRPro 优化计划](../../docs/02-developer-guide/performance/arvrpro-optimization.md)
- [流程引擎文档](../../docs/01-user-guide/workflow/README.md)

## 维护者

ColorVision 项目团队
