# FlowEngineLib

## 功能定位

流程引擎核心库，提供可视化流程节点编辑和执行框架。

## 作用范围

核心算法引擎，为 ColorVision 主程序提供流程编排和节点执行能力。

## 主要功能点

- **流程节点管理** - 支持各种类型的流程节点：相机、算法、传感器、逻辑控制等
- **可视化流程编辑** - 基于 ST.Library.UI 的节点编辑器，支持拖拽连线
- **MQTT 通信集成** - 流程执行中的设备通信和消息传递
- **模板参数化** - 支持流程模板的创建、保存和参数配置
- **循环控制** - 支持循环节点和条件判断逻辑
- **设备抽象** - 统一的设备接口，支持相机、光谱仪、源表等设备
- **异步执行** - 支持流程的异步执行和状态监控

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.Engine 项目引用本库
- 作为流程引擎的核心依赖

**引用的程序集**:
- ST.Library.UI - 节点编辑器UI库
- ColorVision.Common - 通用接口和基类

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\FlowEngineLib\FlowEngineLib.csproj" />
```

### 在主程序中的启用
- 通过 FlowEngineControl 控件集成到主界面
- 自动注册所有流程节点类型
- 支持插件化节点扩展

## 开发调试

```bash
dotnet build Engine/FlowEngineLib/FlowEngineLib.csproj
```

## 目录说明

- `Node/` - 各类流程节点实现
- `Camera/` - 相机相关节点
- `Algorithm/` - 算法处理节点  
- `MQTT/` - MQTT通信节点
- `Control/` - 逻辑控制节点
- `Start/`, `End/` - 流程起止节点
- `Base/` - 节点基类定义

## 相关文档链接

- [流程引擎使用指南](../../docs/engine-components/ColorVision.Engine.md)
- [节点开发指南](../../docs/extensibility/README.md)

## 维护者

ColorVision 开发团队