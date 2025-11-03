# ProjectARVRLite

## 🎯 功能定位

ARVR Lite 轻量版测试系统 - 为AR/VR显示设备提供简化的快速测试解决方案

## 作用范围

针对AR/VR显示设备的基础测试需求，提供轻量化、高效的测试流程，适用于快速检测和批量测试场景。

## 主要功能点

- **快速初始化** - 通过ProjectARVRInit快速初始化测试参数信息
- **简化测试流程** - 精简的测试步骤和快速执行
- **PG切换控制** - 图案生成器(Pattern Generator)的切换管理
- **异步测试支持** - 支持异步测试执行和结果处理
- **Recipe管理** - 轻量化的测试配方管理
- **结果分析** - 基础的客观测试结果分析
- **视场角计算** - Bosight视场角计算功能
- **流程模板支持** - 基于模板的测试流程配置

## 与主程序的依赖关系

**引用的程序集**:
- ColorVision.Engine - 使用核心引擎功能
- ColorVision.Engine.Templates - 模板管理支持
- ColorVision.Engine.Templates.Jsons.LargeFlow - 大流程模板支持
- ColorVision.UI - 基础UI组件

**被引用方式**:
- 作为插件集成到主程序
- 支持独立窗口模式运行

## 使用方式

### 引用方式
作为独立插件项目，通过插件系统自动加载

### 测试流程
1. **初始化** - 传入ProjectARVRInit参数初始化测试系统
2. **切图响应** - 成功后返回切图指令，系统自动切换测试图案
3. **拍照执行** - 切图拍照完成后自动执行ProjectARVRResult
4. **结果处理** - 处理测试结果和数据分析

### API调用示例
```json
{
  "Version": "1.0",
  "MsgID": "12345", 
  "EventName": "ProjectARVRInit",
  "SerialNumber": "",
  "Params": ""
}
```

**响应格式**:
```json
{
  "Code": 0,
  "Msg": null,
  "Data": {"ARVRTestType": 1},
  "Version": null,
  "MsgID": null,
  "EventName": "SwitchPG",
  "SerialNumber": null
}
```

**完成通知**:
```json
{
  "Version": "1.0",
  "MsgID": "12345",
  "EventName": "SwitchPGCompleted", 
  "SerialNumber": "",
  "Params": ""
}
```

## 算法支持

### 视场角计算
支持Bosight视场角计算：
```
Bosight = ATAN(((TAN(BQ3*3.1415926/180))^2 + (TAN(BS3*3.1415926/180))^2)^(1/2)) * 180/3.1415926
```
其中BQ3和BS3分别是tiltx和tilty参数。

## 开发调试

```bash
dotnet build Projects/ProjectARVRLite/ProjectARVRLite.csproj
```

## 目录说明

- `ProjectARVRLiteConfig.cs` - 项目配置类
- `ARVRWindow.xaml/.cs` - 主测试窗口
- `ARVRRecipeConfig.cs` - Recipe配置管理
- `ViewResultManager.cs` - 结果视图管理
- `EditRecipeWindow.xaml/.cs` - Recipe编辑窗口
- `EditFixWindow.xaml/.cs` - 修正参数编辑窗口
- `Services/` - 服务模块目录
- `PluginConfig/` - 插件配置目录

## 配置说明

### 轻量化配置
- 简化的参数配置界面
- 快速的测试流程设定
- 基础的结果分析功能

### Recipe管理
- 支持测试配方的创建和编辑
- 参数修正和数据分析功能

## 与ProjectARVRPro的区别

- **简化流程** - 相比Pro版本，去除了复杂的处理模块
- **快速执行** - 针对效率优化的测试流程
- **轻量配置** - 简化的配置界面和参数管理
- **基础功能** - 保留核心测试功能，去除高级特性

## 相关文档链接

- [ARVR测试规范](../../docs/testing/ARVR-testing.md)
- [流程引擎文档](../../docs/04-api-reference/engine-components/README.md)
- [算法引擎文档](../../docs/04-api-reference/algorithms/README.md)

## 维护者

ColorVision 项目团队