# FlowEngineLib

> 版本: 1.6.1 | 目标框架: .NET 8.0 Windows / .NET Framework 4.7.2

## 🎯 功能定位

FlowEngineLib 是 ColorVision 系统的可视化流程引擎核心库，提供可视化流程节点编辑和执行框架。该库实现了基于节点的流程编排系统，支持多种设备集成、MQTT通信、算法处理和流程控制功能。

## 主要功能点

### 可视化流程编辑
- **节点编辑器** - 基于 ST.Library.UI 的节点编辑器，支持拖拽连线
- **节点管理** - 支持各种类型的流程节点：相机、算法、传感器、逻辑控制等
- **动态加载** - 支持程序集动态加载和节点发现

### 流程执行
- **执行引擎** - 支持流程的异步执行和状态监控
- **循环控制** - 支持循环节点和条件判断逻辑
- **异常处理** - 完善的错误处理和恢复机制

### 设备集成
- **MQTT通信** - 流程执行中的设备通信和消息传递
- **设备抽象** - 统一的设备接口，支持相机、光谱仪、源表等设备
- **多协议支持** - TCP、UDP、串行通信

### 模板参数化
- **模板创建** - 支持流程模板的创建和保存
- **参数配置** - 支持流程参数的动态配置
- **模板继承** - 模板间的继承和组合关系

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                      FlowEngineLib                            │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │    Base     │    │    Start    │    │     End     │      │
│  │             │    │             │    │             │      │
│  │ • 节点基类  │    │ • 启动节点  │    │ • 结束节点  │      │
│  │ • 服务节点  │    │ • MQTT启动  │    │ • 流程结束  │      │
│  │ • 循环节点  │    │ • 参数接收  │    │ • 结果收集  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │    MQTT     │    │  Algorithm  │    │    Camera   │      │
│  │             │    │             │    │             │      │
│  │ • 发布/订阅 │    │ • 算法节点  │    │ • 相机节点  │      │
│  │ • 消息传递  │    │ • ARVR算法  │    │ • 循环采集  │      │
│  │ • 设备通信  │    │ • 图像处理  │    │ • 触发控制  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │   Control   │    │ Spectrum/   │    │    SMU/     │      │
│  │             │    │     PG      │    │     POI     │      │
│  │ • 循环控制  │    │             │    │             │      │
│  │ • 条件判断  │    │ • 光谱采集  │    │ • 源表控制  │      │
│  │ • 手动确认  │    │ • 图案生成  │    │ • POI处理   │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 使用方式

### 引用方式

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

## 主要组件

### CVCommonNode
通用节点基类。

```csharp
public abstract class CVCommonNode : STNode
{
    // 节点属性
    public string NodeType { get; set; }
    public string DeviceCode { get; set; }
    
    // 输入输出选项
    public List<STNodeOption> InputOptions { get; }
    public List<STNodeOption> OutputOptions { get; }
    
    // 数据操作
    protected T GetInputData<T>(string optionName);
    protected void SetOutputData<T>(string optionName, T data);
}
```

### CVBaseServerNode
服务节点基类。

```csharp
public abstract class CVBaseServerNode : CVCommonNode
{
    // 服务类型和设备代码
    public string ServiceType { get; set; }
    public string DeviceCode { get; set; }
    
    // 执行工作
    protected abstract void DoServerWork(CVStartCFC cfc);
    
    // 数据传输
    protected void DoTransferData(STNodeOption output, CVStartCFC cfc);
}
```

### FlowEngineControl
流程引擎控制器。

```csharp
public class FlowEngineControl
{
    // 构造函数
    public FlowEngineControl(STNodeEditor editor, bool isAutoStartName = true);
    
    // 流程控制
    public void RunFlow(string flowName);
    public void StopFlow();
    public void PauseFlow();
    public void ResumeFlow();
    
    // 事件
    public event EventHandler<FlowFinishedEventArgs> Finished;
    public event EventHandler<FlowStatusChangedEventArgs> StatusChanged;
}
```

## 目录说明

- `Base/` - 节点基类和核心抽象
  - `CVCommonNode.cs` - 通用节点基类
  - `CVBaseServerNode.cs` - 服务节点基类
  - `CVBaseLoopServerNode.cs` - 循环服务节点基类
- `Start/` - 启动节点
- `End/` - 结束节点
- `MQTT/` - MQTT通信组件
- `Algorithm/` - 算法处理节点
- `Camera/` - 相机控制节点
- `Control/` - 流程控制节点
- `Node/` - 各类功能节点实现
- `Logical/` - 逻辑处理组件
- `SMU/` - 源表设备节点
- `PG/` - PG设备节点
- `Spectum/` - 光谱仪节点
- `simulator/` - 模拟器节点

## 节点类型

| 节点类型 | 说明 | 主要用途 |
|---------|------|---------|
| **启动节点** | 流程入口 | 流程开始、参数接收 |
| **服务节点** | 业务逻辑执行 | 设备控制、数据处理 |
| **循环节点** | 循环控制 | 批量处理、重复操作 |
| **算法节点** | 算法处理 | 图像处理、数据分析 |
| **控制节点** | 流程控制 | 条件判断、流程跳转 |
| **结束节点** | 流程结束 | 结果收集、清理工作 |

## 开发调试

```bash
# 构建项目
dotnet build Engine/FlowEngineLib/FlowEngineLib.csproj

# 清理项目
dotnet clean Engine/FlowEngineLib/FlowEngineLib.csproj

# 发布项目
dotnet publish Engine/FlowEngineLib/FlowEngineLib.csproj -c Release
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

## 最佳实践

### 1. 节点开发规范

1. **使用 STNodeAttribute 标记节点**
   ```csharp
   [STNode("分类/节点名", "描述")]
   ```

2. **使用 STNodePropertyAttribute 标记属性**
   ```csharp
   [STNodeProperty("显示名", "描述")]
   ```

3. **在 OnCreate 中初始化**
   - 添加输入/输出选项
   - 设置节点样式
   - 注册事件处理

4. **实现数据处理逻辑**
   - 监听 DataTransfer 事件
   - 处理输入数据
   - 更新输出数据

### 2. 推荐做法

✅ **推荐做法**:
- 保持节点功能单一
- 提供清晰的属性描述
- 实现适当的数据验证
- 处理异常情况
- 使用有意义的命名

❌ **避免**:
- 在绘制方法中创建对象
- 循环依赖的连接
- 未验证的数据处理
- 过于复杂的节点逻辑

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/engine-components/FlowEngineLib.md)
- [流程引擎概述](../../docs/02-developer-guide/algorithm-engine-templates/flow-engine/流程引擎.md)
- [节点开发指南](../../docs/02-developer-guide/core-concepts/extensibility.md)
- [ST.Library.UI](../ST.Library.UI/README.md)

## 版本历史

### v1.6.1 (当前版本)
- 优化MQTT连接稳定性
- 增加新的算法节点
- 性能优化和bug修复

## 维护者

ColorVision 开发团队
