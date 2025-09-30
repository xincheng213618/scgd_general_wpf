# ProjectLUX

## 🎯 功能定位

LUX 亮度测试系统 - 专为显示设备亮度和照度测试提供的专业测试解决方案

## 作用范围

专注于显示设备的亮度测试、照度测量和相关光学参数检测，适用于各种显示面板和光学设备的质量控制。

## 主要功能点

- **亮度测试** - 精确的显示设备亮度测量
- **照度检测** - 环境照度和设备照度测试
- **Recipe管理** - 亮度测试配方管理和配置
- **结果分析** - 客观测试结果分析和数据处理
- **流程控制** - 基于流程引擎的测试执行
- **数据修正** - 测试结果的修正和校准功能
- **大流程支持** - 支持复杂的测试流程配置
- **实时监控** - 测试过程的实时数据监控

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 使用核心引擎功能
- ColorVision.Engine.Templates - 模板管理支持
- ColorVision.Engine.Templates.Jsons.LargeFlow - 大流程模板支持
- ColorVision.UI - 基础UI组件

**被引用方式**:
- 作为插件集成到主程序
- 支持独立窗口和流程模式

## 使用方式

### 引用方式
作为独立插件项目，通过插件系统自动加载

### 测试流程
1. **流程执行** - 通过ProjectLUX事件触发测试流程
2. **参数配置** - 配置亮度测试相关参数
3. **数据采集** - 执行亮度和照度数据采集
4. **结果分析** - 处理和分析测试结果

### API调用示例
**流程执行**:
```json
{
  "Version": "1.0",
  "MsgID": "12345",
  "EventName": "ProjectLUX",
  "SerialNumber": "SN001",
  "Params": "Flow"
}
```

**菜单操作**:
```json
{
  "Version": "1.0",
  "MsgID": "12345", 
  "EventName": "Menu",
  "SerialNumber": "SN001",
  "Code": 0,
  "Msg": "RunTemplate",
  "Data": null
}
```

## 测试特性

### 亮度测试功能
- 支持多点亮度测量
- 亮度均匀性分析
- 亮度稳定性测试
- 白平衡检测

### 流程管理
- 可配置的测试流程
- 模板化的测试参数
- 自动化的测试执行
- 结果数据的自动分析

## 开发调试

```bash
dotnet build Projects/ProjectLUX/ProjectLUX.csproj
```

## 目录说明

- `ProjectLUXConfig.cs` - 项目配置类
- `LUXWindow.xaml/.cs` - 主测试窗口
- `LUXRecipeConfig.cs` - Recipe配置管理
- `ViewResultManager.cs` - 结果视图管理
- `EditRecipeWindow.xaml/.cs` - Recipe编辑窗口
- `EditFixWindow.xaml/.cs` - 修正参数编辑窗口
- `Services/` - 服务模块目录
- `PluginConfig/` - 插件配置目录

## 配置说明

### Recipe管理
- 亮度测试配方的创建和编辑
- 测试参数的配置和管理
- 结果修正参数的设定

### 大流程支持
- 基于LargeFlow模板的复杂测试流程
- 支持流程可视化配置和编辑

## 与其他ARVR项目的区别

- **专注亮度测试** - 相比ARVR项目，专门针对亮度和照度测试优化
- **简化配置** - 去除了不必要的光学测试功能，专注于亮度相关测试
- **专业算法** - 采用专门的亮度测试算法和分析方法

## 相关文档链接

- [亮度测试规范](../../docs/testing/brightness-testing.md)
- [光学测试算法](../../docs/algorithms/optical-testing.md)
- [流程引擎文档](../../docs/engine-components/README.md)

## 维护者

ColorVision 项目团队