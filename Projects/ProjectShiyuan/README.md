# ProjectShiyuan

## 🎯 功能定位

世源科技客户定制项目 - 针对特定业务需求的完整测试解决方案

## 作用范围

为世源科技客户提供定制化的测试系统，满足特定业务流程和质量控制要求。

## 主要功能点

- **定制化界面** - 针对客户需求的专用界面设计
- **特定工作流** - 客户业务流程的软件化实现  
- **专用算法** - 针对特定检测需求的算法优化
- **数据接口** - 与客户现有系统的数据对接
- **报告生成** - 客户要求的专用报告格式
- **流程管理** - 基于流程引擎的灵活测试流程
- **结果追溯** - 完整的测试数据记录和追溯功能

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 核心引擎功能
- ColorVision.UI - 基础UI组件
- ColorVision.Common - 通用接口和工具
- ColorVision.Engine.Templates - 模板管理

**被引用方式**:
- 作为独立项目启动
- 或通过插件方式集成到主程序

## 使用方式

### 引用方式
作为独立项目编译和部署

```xml
<ProjectReference Include="..\..\Engine\ColorVision.Engine\ColorVision.Engine.csproj" />
<ProjectReference Include="..\..\UI\ColorVision.UI\ColorVision.UI.csproj" />
```

### 在主程序中的启用
- 可直接运行项目执行文件
- 或通过主菜单插件方式加载

### 主要功能模块
- **测试流程配置** - 自定义测试流程和步骤
- **数据采集** - 从设备和传感器采集测试数据
- **结果分析** - 测试数据的处理和分析
- **报告输出** - 生成符合客户要求的测试报告

## 开发调试

```bash
dotnet build Projects/ProjectShiyuan/ProjectShiyuan.csproj
```

## 目录说明

- `Views/` - 用户界面和窗口
- `Models/` - 数据模型和配置
- `Services/` - 业务服务和逻辑
- `Config/` - 配置文件和参数
- `Resources/` - 资源文件

## 配置说明

### 客户定制配置
- 根据客户需求配置测试参数
- 定制化的界面布局和功能
- 特定的数据格式和通信协议

### 测试流程配置
- 基于模板的流程配置
- 灵活的测试步骤组合
- 自动化的测试执行

## 相关文档链接

- [项目开发指南](../../docs/extensibility/README.md)
- [客户定制说明](../../docs/getting-started/入门指南.md)
- [流程引擎文档](../../docs/engine-components/README.md)

## 维护者

ColorVision 项目团队
