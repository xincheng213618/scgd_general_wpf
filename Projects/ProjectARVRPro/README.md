# ProjectARVRPro

## 🎯 功能定位

ARVR Pro 专业版测试系统 - 针对AR/VR显示设备的全面光学性能测试解决方案

## 作用范围

专为AR/VR显示设备提供专业级光学测试，涵盖显示质量、光学性能、缺陷检测等全方位测试需求。

## 运行环境

- 目标框架：.NET 8.0-windows（x64）
- 插件输出：PostBuild 会将生成物复制到 `ColorVision/bin/x64/{Configuration}/net8.0-windows/Plugins/ProjectARVRPro/`

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
  - **ProcessMeta管理** - 支持多流程配置和选择性执行
  - **IsEnabled属性** - 可选择性启用/禁用特定测试步骤
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

### 打包与部署
- 构建命令：`dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -p:Platform=x64`
- 构建后会自动复制 DLL、manifest 以及 README/CHANGELOG 到 ColorVision 插件目录，便于直接运行主程序验证

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

## 数据与结果输出

- 客观测试数据支持自动 CSV 导出（`ObjectiveTestResultCsvExporter`），包含测试项、上下限、单位与判定结果
- 结果视图由 `ViewResultManager` 汇总展示，可结合模板/流程的启用状态生成同步的产出
- 配套日志窗口与配置窗口可从主界面打开，便于调试与问题追踪

## 优化计划概览

- 持续压缩单次流程的处理耗时，减少 UI 线程阻塞
- 提升 ProcessMeta/Recipe 的复用能力，完善禁用步骤的动态校验
- 强化结果导出链路（CSV/PDF 命名规范、异常重试），细节见 [优化计划](OPTIMIZATION.md)

## 相关文档链接

- [ARVR测试规范](../../docs/testing/ARVR-testing.md)
- [光学测试算法](../../docs/algorithms/optical-testing.md)
- [流程引擎文档](../../docs/04-api-reference/engine-components/README.md)
- [优化计划](OPTIMIZATION.md)

## 维护者

ColorVision 项目团队
