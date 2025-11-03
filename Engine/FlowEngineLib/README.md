# FlowEngineLib

> 可视化流程引擎核心库 - ColorVision 系统的流程编排与执行框架

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%204.7.2-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-Proprietary-blue)](../../LICENSE)
[![Version](https://img.shields.io/badge/version-1.6.1-green)](CHANGELOG.md)

## 📋 概述

FlowEngineLib 是 ColorVision 系统的流程引擎核心库，提供可视化流程节点编辑和执行框架。该库实现了基于节点的流程编排系统，支持多种设备集成、MQTT通信、算法处理和流程控制功能。

### ✨ 核心特性

- 🎨 **可视化流程编辑** - 基于 ST.Library.UI 的节点编辑器，支持拖拽连线
- 📦 **流程节点管理** - 支持各种类型的流程节点：相机、算法、传感器、逻辑控制等
- 🔌 **MQTT 通信集成** - 流程执行中的设备通信和消息传递
- 📝 **模板参数化** - 支持流程模板的创建、保存和参数配置
- 🔄 **循环控制** - 支持循环节点和条件判断逻辑
- 🔧 **设备抽象** - 统一的设备接口，支持相机、光谱仪、源表等设备
- ⚡ **异步执行** - 支持流程的异步执行和状态监控
- 🧩 **插件化扩展** - 支持自定义节点开发和动态加载

### 🎯 适用场景

- 自动化测试流程编排
- 图像采集与处理
- 设备联动控制
- 数据采集与分析
- 质量检测流程
- 生产线自动化

## 🏗️ 架构设计

### 核心组件

```
FlowEngineLib/
├── Base/                    # 节点基类和核心抽象
│   ├── CVCommonNode        # 通用节点基类
│   ├── CVBaseServerNode    # 服务节点基类
│   └── CVBaseLoopServerNode # 循环服务节点基类
├── Start/                   # 启动节点
│   ├── BaseStartNode       # 启动节点基类
│   └── MQTTStartNode       # MQTT启动节点
├── End/                     # 结束节点
│   └── CVEndNode           # 流程结束节点
├── MQTT/                    # MQTT通信
│   ├── MQTTHelper          # MQTT辅助类
│   ├── MQTTPublishHub      # 发布Hub
│   └── MQTTSubscribeHub    # 订阅Hub
├── Algorithm/               # 算法节点
│   ├── AlgorithmNode       # 通用算法节点
│   └── AlgorithmARVRNode   # ARVR算法节点
├── Camera/                  # 相机节点
│   ├── CVCameraNode        # 标准相机节点
│   └── CVCameraLoopNode    # 相机循环节点
├── Control/                 # 控制节点
│   ├── LoopNode            # 循环控制节点
│   └── ManualConfirmNode   # 手动确认节点
└── Node/                    # 其他节点实现
    ├── Algorithm/          # 算法节点
    ├── Camera/             # 相机节点
    ├── Spectrum/           # 光谱节点
    └── POI/                # POI节点
```

### 技术栈

- **目标框架**: .NET 8.0 Windows / .NET Framework 4.7.2
- **UI框架**: Windows Forms (节点编辑器)
- **通信协议**: MQTT (MQTTnet 4.3.4)
- **序列化**: Newtonsoft.Json 13.0.x
- **日志**: log4net 3.2.0
- **代码规模**: 271个C#文件，约20,000+行代码

## 🚀 快速开始

### 安装引用

在项目中添加引用：

```xml
<ProjectReference Include="..\FlowEngineLib\FlowEngineLib.csproj" />
```

### 基本使用

```csharp
using FlowEngineLib;
using ST.Library.UI.NodeEditor;

// 1. 创建节点编辑器
var nodeEditor = new STNodeEditor();

// 2. 创建流程引擎控制器
var flowEngine = new FlowEngineControl(nodeEditor, isAutoStartName: true);

// 3. 监听流程完成事件
flowEngine.Finished += (sender, args) => {
    Console.WriteLine($"Flow {args.FlowName} completed");
};

// 4. 运行流程
flowEngine.RunFlow("MainFlow");
```

### 创建自定义节点

```csharp
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

[STNode("/Custom/MyNode")]  // 节点分类
public class MyCustomNode : CVBaseServerNode
{
    public MyCustomNode()
        : base("MyNode", "CustomNode", "CN1", "DEV01")
    {
    }
    
    protected override void OnCreate()
    {
        base.OnCreate();
        // 添加输入输出选项
        InputOptions.Add("Input", typeof(double), false);
        OutputOptions.Add("Output", typeof(double), false);
    }
    
    protected override void DoServerWork(CVStartCFC cfc)
    {
        // 实现业务逻辑
        var input = GetInputData<double>("Input");
        var result = ProcessData(input);
        SetOutputData("Output", result);
        
        // 传递给下一节点
        DoTransferData(m_op_data_out, cfc);
    }
}
```

## 📚 核心概念

### 节点类型

| 节点类型 | 说明 | 主要用途 |
|---------|------|---------|
| **启动节点** | 流程入口 | 流程开始、参数接收 |
| **服务节点** | 业务逻辑执行 | 设备控制、数据处理 |
| **循环节点** | 循环控制 | 批量处理、重复操作 |
| **算法节点** | 算法处理 | 图像处理、数据分析 |
| **控制节点** | 流程控制 | 条件判断、流程跳转 |
| **结束节点** | 流程结束 | 结果收集、清理工作 |

### 数据流

```
StartNode → ServerNode → AlgorithmNode → EndNode
     ↓           ↓              ↓            ↓
  CVStartCFC  CVTransAction  CVTransAction  Result
```

### 通信模型

- **MQTT 发布/订阅** - 设备通信和状态同步
- **节点间数据传递** - 通过连接线传递数据对象
- **事件驱动** - 基于事件的异步处理

## 🔧 开发调试

### 构建项目

```bash
# 构建项目
dotnet build Engine/FlowEngineLib/FlowEngineLib.csproj

# 清理项目
dotnet clean Engine/FlowEngineLib/FlowEngineLib.csproj

# 发布项目
dotnet publish Engine/FlowEngineLib/FlowEngineLib.csproj -c Release
```

### 运行测试

```bash
# 运行单元测试
dotnet test Test/FlowEngineLib.Tests/

# 运行集成测试
dotnet test Test/FlowEngineLib.Integration.Tests/
```

### 调试技巧

1. **启用详细日志**
   ```csharp
   LogHelper.SetLogLevel(LogLevel.Debug);
   ```

2. **断点调试**
   - 在 `DoServerWork` 方法设置断点
   - 检查 `CVStartCFC` 对象内容
   - 监视节点状态变化

3. **MQTT消息监控**
   - 使用 MQTT.fx 或 MQTTX 工具
   - 监听主题: `{ServiceType}/{DeviceCode}/#`

## 📖 目录说明

| 目录 | 说明 |
|-----|------|
| `Base/` | 节点基类和核心抽象 |
| `Start/` | 流程启动节点 |
| `End/` | 流程结束节点 |
| `MQTT/` | MQTT通信组件 |
| `Algorithm/` | 算法处理节点 |
| `Camera/` | 相机控制节点 |
| `Control/` | 流程控制节点 |
| `Node/` | 各类功能节点实现 |
| `Logical/` | 逻辑处理组件 |
| `SMU/` | 源表设备节点 |
| `PG/` | PG设备节点 |
| `Spectum/` | 光谱仪节点 |
| `simulator/` | 模拟器节点 |
| `Properties/` | 程序集信息 |

## 📊 统计信息

- **C# 文件数**: 271个
- **代码行数**: 约20,000+行
- **节点类型**: 50+种
- **支持设备**: 10+类
- **算法类型**: 20+种

## 🔗 相关文档

### 核心文档

- 📘 [FlowEngineLib 详细文档](../../docs/04-api-reference/engine-components/FlowEngineLib.md) - 完整API文档和使用指南
- 📗 [流程引擎概述](../../docs/flow-engine/flow-engine-overview.md) - 架构设计和原理说明
- 📕 [节点开发指南](../../docs/02-developer-guide/core-concepts/extensibility.md) - 自定义节点开发教程
- 📙 [流程引擎中文文档](../../docs/algorithm-engine-templates/flow-engine/流程引擎.md) - 详细的中文技术文档

### 相关组件

- [ST.Library.UI](../../docs/engine-components/ST.Library.UI.md) - 节点编辑器UI库
- [ColorVision.Engine](../../docs/04-api-reference/engine-components/ColorVision.Engine.md) - 引擎核心库
- [Engine组件概览](../../docs/engine-components/Engine组件概览.md) - 整体架构说明

## 🤝 贡献指南

欢迎提交问题和改进建议！

1. Fork 本项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 提交 Pull Request

## 📄 许可证

版权所有 © ColorVision 开发团队

本软件为专有软件，未经授权不得使用、复制或分发。

## 👥 维护团队

**ColorVision 开发团队**

- 项目负责人: ColorVision Team
- 技术支持: 通过 Issue 系统
- 文档维护: 持续更新中

## 🔖 版本历史

- **v1.6.1.25093** (当前版本)
  - 优化MQTT连接稳定性
  - 增加新的算法节点
  - 性能优化和bug修复

详细的版本历史请查看 [CHANGELOG.md](CHANGELOG.md)

## 📞 技术支持

如有问题或建议，请通过以下方式联系：

- 📧 提交 Issue
- 💬 参与讨论
- 📝 查阅文档

---

**最后更新**: 2024年
**文档版本**: 1.0

> 💡 提示：本README提供快速入门指南，详细文档请查看 [FlowEngineLib完整文档](../../docs/04-api-reference/engine-components/FlowEngineLib.md)