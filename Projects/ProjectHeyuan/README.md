# ProjectHeyuan

## 🎯 功能定位

河源精电客户定制项目 - 基于ColorVision平台的专用测试解决方案

## 作用范围

专为河源精电客户定制的测试系统，提供完整的测试流程管理和MES系统集成功能。

## 主要功能点

- **MES系统集成** - 与制造执行系统的数据交互和流程控制
- **串口通信管理** - 支持设备串口连接和数据传输
- **测试流程管理** - 基于流程引擎的测试模板配置和执行
- **实时监控界面** - 提供测试状态监控和数据展示
- **数据记录和追溯** - 完整的测试数据记录和查询功能
- **设备控制** - 集成设备控制和参数管理

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 使用核心引擎功能
- ColorVision.Engine.MQTT - MQTT通信支持
- ColorVision.Database - 数据库访问
- ColorVision.UI - 基础UI组件
- ColorVision.Themes - 主题样式
- FlowEngineLib - 流程引擎核心库

**被引用方式**:
- 作为插件集成到主程序
- 通过菜单项"河源精电"启动

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\..\Engine\ColorVision.Engine\ColorVision.Engine.csproj" />
```

### 在主程序中的启用
- 通过主菜单"工具"下的"河源精电"选项启动
- 支持独立窗口模式运行

### 主要界面功能
- **设定参数配置** - 测试参数的设定和管理
- **串口连接管理** - 串口设备的连接状态监控
- **流程模板管理** - 测试流程的配置和编辑
- **实时数据显示** - 测试过程中的数据实时展示

## 开发调试

```bash
dotnet build Projects/ProjectHeyuan/ProjectHeyuan.csproj
```

## 目录说明

- `ProjectHeyuanWindow.xaml/.cs` - 主窗口界面和逻辑
- `HYMesManager.cs` - MES系统管理和配置
- `MenuItemHeyuan.cs` - 菜单集成和插件启动
- `SerialMsg.cs` - 串口通信消息处理
- `ConnectConverter.cs` - 连接状态转换器
- `NumSet.cs` - 数值设定管理
- `TempResult.cs` - 临时结果处理
- `manifest.json` - 插件配置清单

## 配置说明

### MES配置
- 支持流程模板选择和配置
- 设备ID和测试名称设定
- 串口通信参数配置

### 流程引擎集成
- 基于FlowEngineLib的流程管理
- 支持模板编辑器和流程工具
- 灵活的测试流程定制

## 相关文档链接

- [项目开发指南](../../docs/02-developer-guide/core-concepts/extensibility.md)
- [流程引擎文档](../../docs/04-api-reference/engine-components/README.md)
- [客户定制说明](../../docs/00-getting-started/README.md)

## 维护者

ColorVision 项目团队