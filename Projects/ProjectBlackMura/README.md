# ProjectBlackMura

## 🎯 功能定位

BlackMura 缺陷检测测试插件 - 专用于显示面板黑色缺陷（Black Mura）检测的测试解决方案

## 作用范围

针对显示面板制造过程中的黑色缺陷检测，提供完整的测试流程、结果分析和报告生成功能。

## 主要功能点

- **Black Mura检测** - 专业的黑色缺陷检测算法
- **单体测试支持** - 支持单个面板的独立测试
- **流程模板管理** - 基于流程引擎的测试模板配置
- **结果数据管理** - 测试结果的存储、查询和分析
- **Excel报告生成** - 自动生成测试报告和数据导出
- **MES系统集成** - 与制造执行系统的数据交互
- **判定配置管理** - 灵活的判定标准配置和管理

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 使用核心引擎功能
- ColorVision.UI - 使用基础UI组件
- EPPlus - Excel文件处理
- CVCommCore - 通信核心组件

**被引用方式**:
- 作为插件集成到主程序
- 支持独立窗口模式运行

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\..\Engine\ColorVision.Engine\ColorVision.Engine.csproj" />
<ProjectReference Include="..\..\UI\ColorVision.UI\ColorVision.UI.csproj" />
```

### 在主程序中的启用
- 通过插件系统自动加载
- 支持流程模板编辑和配置

### 主要功能模块
- **测试流程配置** - 支持模板编辑器和流程工具
- **结果数据展示** - 实时显示测试结果和历史数据
- **判定标准管理** - 可配置的判定参数和阈值
- **报告生成** - Excel格式的详细测试报告

## 开发调试

```bash
dotnet build Projects/ProjectBlackMura/ProjectBlackMura.csproj
```

## 目录说明

- `MainWindow.xaml/.cs` - 主窗口界面和逻辑
- `ProjectBlackMuraConfig.cs` - 项目配置和管理
- `ExcelReportGenerator.cs` - Excel报告生成器
- `HYMesManager.cs` - MES系统管理
- `Config/` - 配置文件目录
- `PluginConfig/` - 插件配置目录

## 配置说明

### 判定配置
- 支持多种判定标准的配置
- 灵活的阈值设定和参数管理

### 流程配置
- 基于模板的测试流程配置
- 支持步骤化的测试执行

## 相关文档链接

- [算法引擎文档](../../docs/algorithms/README.md)
- [插件开发指南](../../docs/plugins/README.md)
- [测试流程配置](../../docs/engine-components/README.md)

## 维护者

ColorVision 项目团队



