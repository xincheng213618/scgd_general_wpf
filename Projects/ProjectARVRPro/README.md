# ProjectARVRPro

## 🎯 功能定位

ARVR Pro 专业版测试系统 - 针对AR/VR显示设备的全面光学性能测试解决方案

## 作用范围

专为AR/VR显示设备提供专业级光学测试，涵盖显示质量、光学性能、缺陷检测等全方位测试需求。支持多种测试模式的灵活配置和选择性执行，提供完整的测试数据分析和结果管理功能。

## 主要功能点

### 核心功能

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
  - **Red/Green/Blue Process** - RGB通道独立测试处理

### 高级功能

- **Recipe管理** - 完整的测试配方管理系统
  - 支持多种测试配方的创建和编辑
  - 参数化的测试流程配置
  - 配方导入导出功能
  
- **数据分析** - 客观测试结果分析和修正功能
  - 实时数据采集和分析
  - 自动修正算法
  - 统计数据汇总
  
- **流程管理** - 基于大流程模板的测试执行
  - **ProcessMeta管理** - 支持多流程配置和选择性执行
  - **IsEnabled属性** - 可选择性启用/禁用特定测试步骤
  - **动态流程调整** - 运行时可调整测试流程
  - **流程可视化** - 基于StepBar的进度展示
  
- **结果管理** - 测试结果的存储、查询和展示
  - 结果数据持久化
  - CSV/PDF导出功能
  - 历史数据查询
  - 测试报告生成

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
- **配置持久化**：ProcessMeta配置自动保存到ProcessMetas.json
- **示例**：如果配置了8个步骤(0-7)，仅启用第0和第7步，执行流程将是：0→7（跳过1-6）

### ProcessMeta IsEnabled特性详解

**功能概述**：
- IsEnabled属性允许用户在不删除ProcessMeta的情况下，临时禁用某些测试步骤
- 适用于快速调试、部分测试、或跳过不需要的检测项

**使用场景**：
1. **快速调试** - 仅启用需要调试的测试步骤
2. **部分测试** - 产线上只执行关键测试项
3. **阶段性测试** - 分阶段执行不同的测试组合
4. **测试优化** - 通过禁用非必要步骤提升测试效率

**操作方式**：
- 打开ProcessManagerWindow（流程管理窗口）
- 在"是否启用"列中勾选/取消勾选复选框
- 更改会自动保存到配置文件
- 下次运行测试时自动应用新配置

**技术实现**：
- 初始化时查找第一个启用的ProcessMeta
- 执行过程中自动跳过禁用的步骤
- 完成判定仅考虑启用的步骤
- 详细技术文档：[IsEnabled特性说明](Process/README_IsEnabled_Feature.md)

## 相关文档链接

### 核心功能文档
- [ProcessMeta IsEnabled特性详解](Process/README_IsEnabled_Feature.md)
- [ARVR算法模板文档](../../docs/04-api-reference/algorithms/templates/arvr-template.md)
- [流程引擎文档](../../docs/04-api-reference/engine-components/README.md)

### 开发参考
- [插件开发指南](../../docs/02-developer-guide/plugin-development/overview.md)
- [模板系统设计](../../docs/02-developer-guide/engine-development/templates.md)
- [MVVM架构指南](../../docs/02-developer-guide/ui-development/xaml-mvvm.md)

### 用户指南
- [快速开始指南](#快速开始指南)
- [常见问题解答](#常见问题)
- [性能优化建议](#性能优化建议)

## 维护者

ColorVision 项目团队

---

## 快速开始指南

### 1. 基本配置

```csharp
// 初始化ProjectARVRPro
var init = new ProjectARVRProInit();
// 系统会自动加载配置文件和初始化参数
```

### 2. 配置测试流程

1. 打开ARVRWindow主窗口
2. 点击"流程管理"按钮打开ProcessManagerWindow
3. 配置ProcessMeta：
   - 添加测试步骤：输入名称、选择流程模板和Process类型
   - 调整顺序：使用上移/下移按钮
   - 启用/禁用：勾选"是否启用"复选框
4. 配置会自动保存到`ProcessMetas.json`

### 3. 编辑Recipe配置

```csharp
// 获取Recipe管理器
var recipeManager = RecipeManager.GetInstance();
var recipeConfig = recipeManager.RecipeConfig;

// 编辑Recipe参数（通过UI）
// 在ProcessManagerWindow中点击"编辑Recipe"按钮
```

### 4. 执行测试

```csharp
// 启动测试流程
// 1. 确保设备连接正常
// 2. 加载测试配方
// 3. 点击"开始测试"按钮
// 4. 系统将按照启用的ProcessMeta顺序执行
```

### 5. 查看结果

- 测试完成后，结果会显示在TestResultViewWindow
- 支持导出为CSV或PDF格式
- 可查询历史测试记录

## 常见问题

### Q1: 如何跳过某些测试步骤？

**答**：使用IsEnabled功能：
1. 打开ProcessManagerWindow
2. 取消勾选不需要的步骤的"是否启用"复选框
3. 配置自动保存，下次测试会跳过这些步骤

### Q2: ProcessMetas.json文件在哪里？

**答**：配置文件保存在：
```
%AppData%/ColorVision/ProjectARVRPro/ProcessMetas.json
```

### Q3: 如何添加自定义Process？

**答**：
1. 创建类实现`IProcess`接口
2. 实现必要的配置接口（`IRecipeConfig`, `IFixConfig`, `IProcessConfig`）
3. 编译后会自动被发现并添加到可用Process列表

### Q4: 测试结果保存在哪里？

**答**：测试结果保存在数据库中，同时可以导出为：
- CSV文件：包含详细测试数据
- PDF报告：包含图表和汇总信息

### Q5: 如何优化测试速度？

**答**：参考[性能优化建议](#性能优化建议)章节

## 性能优化建议

### 1. 流程配置优化

- **精简测试步骤**：使用IsEnabled功能禁用非必要步骤
- **合理排序**：将耗时较短的测试放在前面，快速发现问题
- **批量处理**：对同类型测试进行批量配置

### 2. Recipe参数优化

```csharp
// 示例：优化图像处理参数
public class OptimizedRecipeConfig : IRecipeConfig
{
    // 减少ROI区域可提升处理速度
    [DisplayName("ROI宽度")]
    public int RoiWidth { get; set; } = 1920;  // 根据实际需要调整
    
    [DisplayName("ROI高度")]
    public int RoiHeight { get; set; } = 1080;  // 根据实际需要调整
    
    // 降低采样率可加快处理
    [DisplayName("采样率")]
    public double SamplingRate { get; set; } = 0.5;  // 0.1-1.0
}
```

### 3. 硬件优化建议

- **CPU**：推荐使用多核处理器（8核以上）
- **内存**：建议16GB以上
- **存储**：使用SSD提升数据读写速度
- **GPU**：支持CUDA的显卡可加速图像处理

### 4. 系统配置优化

- 关闭不必要的后台程序
- 确保设备驱动为最新版本
- 配置合适的日志级别（避免过多日志影响性能）
- 定期清理历史测试数据

### 5. 代码级优化

```csharp
// 使用异步处理提升响应速度
public async Task<TestResult> ExecuteTestAsync(ProcessMeta meta)
{
    if (!meta.IsEnabled) return null;
    
    // 异步执行测试
    return await Task.Run(() => 
    {
        return meta.Process.Execute();
    });
}
```

### 6. 数据库优化

- 定期清理过期数据
- 建立必要的索引
- 使用批量插入代替逐条插入
- 合理配置数据库连接池

## 最佳实践

### 流程设计

1. **模块化设计**：将复杂测试拆分为多个独立ProcessMeta
2. **可重用性**：通过Recipe配置实现参数复用
3. **版本控制**：为不同产品线维护独立的ProcessMetas配置

### 配置管理

1. **备份配置**：定期备份`ProcessMetas.json`和Recipe配置
2. **文档化**：记录每个ProcessMeta的用途和参数说明
3. **版本管理**：使用Git等工具管理配置文件版本

### 测试执行

1. **预检查**：测试前确认设备状态和连接
2. **监控日志**：实时关注日志输出，及时发现问题
3. **结果验证**：测试后验证关键指标是否在预期范围内

## 更新历史

详细更新历史请查看 [CHANGELOG.md](CHANGELOG.md)

**最新版本**：v1.0.4.1 (2025.12.01)
- 优化选项，统一结构
- 增强ProcessMeta管理功能
- 改进IsEnabled特性实现

**规划中的功能**：
- 流程模板导入导出
- 批量测试任务调度
- 更多测试模式支持
- AI辅助结果分析