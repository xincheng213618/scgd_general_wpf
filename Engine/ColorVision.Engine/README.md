# ColorVision.Engine

> 版本: 1.4.2.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows

## 🎯 功能定位

ColorVision.Engine 是 ColorVision 系统的核心引擎，负责流程管理、设备服务协调、模板系统和数据处理。它是整个系统的控制中心，通过可视化的流程设计器，用户可以创建复杂的测试和分析流程。

## 主要功能点

### 流程引擎系统
- **可视化流程设计** - 通过拖拽节点创建复杂流程
- **流程执行管理** - 支持串行、并行、条件分支执行
- **状态监控** - 实时监控流程执行状态和进度
- **异常处理** - 完善的错误处理和恢复机制

### 设备服务管理
- **设备自动发现** - 自动扫描和识别网络设备
- **多协议支持** - MQTT、TCP、UDP、串行通信
- **服务注册** - 动态注册和管理设备服务
- **状态同步** - 实时同步设备状态和参数

### 模板系统
- **参数化模板** - 支持灵活的参数配置
- **模板继承** - 模板间的继承和组合关系
- **版本管理** - 模板版本控制和历史记录
- **导入导出** - 模板的备份和迁移功能

### 数据管理
- **多数据源支持** - MySQL、SQLite、文件存储
- **ORM映射** - SqlSugar数据访问层
- **事务管理** - 保证数据操作的一致性
- **数据同步** - 支持数据的同步和复制

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                   ColorVision.Engine                          │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │ FlowEngine  │    │   Device    │    │  Template   │      │
│  │   Manager   │    │   Manager   │    │   Manager   │      │
│  │             │    │             │    │             │      │
│  │ • 流程设计  │    │ • 设备发现  │    │ • 模板加载  │      │
│  │ • 流程执行  │    │ • 服务注册  │    │ • 参数管理  │      │
│  │ • 状态监控  │    │ • 状态同步  │    │ • 版本控制  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │   MQTT      │    │   Media     │    │  Database   │      │
│  │   Service   │    │   Export    │    │    DAO      │      │
│  │             │    │             │    │             │      │
│  │ • 消息通信  │    │ • 图像导出  │    │ • 数据访问  │      │
│  │ • 设备控制  │    │ • 报告生成  │    │ • 事务管理  │      │
│  │ • 状态广播  │    │ • 格式转换  │    │ • 查询优化  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 使用方式

### 初始化Engine核心服务

```csharp
// 初始化Engine核心服务
var engineService = new ColorVisionEngineService();
await engineService.InitializeAsync();

// 注册设备服务
engineService.RegisterDeviceService<CameraService>();
engineService.RegisterDeviceService<SpectrometerService>();

// 加载流程模板
var flowTemplate = await engineService.LoadFlowTemplateAsync("template_name");

// 执行流程
var result = await engineService.ExecuteFlowAsync(flowTemplate, parameters);
```

### 设备服务使用

```csharp
// 获取设备服务管理器
var deviceManager = ServiceContainer.GetService<DeviceServiceManager>();

// 获取相机服务
var cameraService = deviceManager.GetService<CameraService>();
await cameraService.ConnectAsync();

// 执行相机命令
var image = await cameraService.ExecuteCommandAsync("capture", parameters);
```

### 模板系统使用

```csharp
// 获取模板管理器
var templateManager = ServiceContainer.GetService<TemplateManager>();

// 加载模板
var template = await templateManager.LoadTemplateAsync("MTF_Template");

// 设置参数
template.SetParameter("threshold", 0.8);

// 执行模板
var result = await template.ExecuteAsync();
```

## 主要组件

### FlowEngineManager
负责流程的创建、加载、执行和监控管理。

```csharp
public class FlowEngineManager
{
    // 加载流程模板
    public async Task<FlowTemplate> LoadFlowTemplateAsync(string templateName);
    
    // 执行流程
    public async Task<FlowResult> ExecuteFlowAsync(FlowTemplate template, Dictionary<string, object> parameters);
    
    // 监控流程状态
    public event EventHandler<FlowStatusChangedEventArgs> FlowStatusChanged;
}
```

### DeviceServiceManager
管理所有设备服务的生命周期和通信。

```csharp
public class DeviceServiceManager
{
    // 注册设备服务
    public void RegisterService<T>() where T : IDeviceService;
    
    // 获取设备服务
    public T GetService<T>() where T : IDeviceService;
    
    // 设备状态变更事件
    public event EventHandler<DeviceStatusChangedEventArgs> DeviceStatusChanged;
}
```

### TemplateManager
处理模板的加载、保存、参数管理等操作。

```csharp
public class TemplateManager
{
    // 创建新模板
    public async Task<Template> CreateTemplateAsync(string name, TemplateType type);
    
    // 保存模板
    public async Task SaveTemplateAsync(Template template);
    
    // 加载模板参数
    public async Task<Dictionary<string, object>> LoadTemplateParametersAsync(int templateId);
}
```

## 目录说明

- `Services/` - 设备服务目录
  - `Devices/` - 设备服务实现（相机、光谱仪、电机等）
  - `PhyCameras/` - 物理相机管理
  - `RC/` - 注册中心服务
  - `Terminal/` - 终端服务
- `Templates/` - 模板系统目录
  - `ARVR/` - ARVR算法模板（MTF、SFR、FOV等）
  - `POI/` - 兴趣点检测模板
  - `Jsons/` - JSON配置模板
  - `Compliance/` - 合规性检测模板
- `MQTT/` - MQTT通信服务
- `Media/` - 媒体处理和导出
- `Messages/` - 消息系统
- `Utilities/` - 工具类

## 支持的设备类型

- **相机设备**: LV相机、BV相机、CV相机、通用相机
- **光谱设备**: 光谱仪、色度计、辐射计
- **运动控制**: 电机控制、位移台、旋转台
- **通信服务**: MQTT服务、TCP服务、串口服务
- **其他设备**: PG图案生成器、SMU源表、传感器

## 开发调试

```bash
# 构建项目
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj

# 运行测试
dotnet test
```

## 最佳实践

### 1. 架构原则
- **模块化设计**: 每个组件职责单一，接口清晰
- **可扩展性**: 支持插件化扩展和自定义组件
- **高性能**: 关键算法采用C++实现，支持多线程
- **容错设计**: 完善的异常处理和错误恢复机制

### 2. 设备服务开发
```csharp
public class CustomDeviceService : BaseDeviceService
{
    public override string ServiceType => "CustomDevice";
    
    public override async Task<bool> ConnectAsync()
    {
        // 实现设备连接逻辑
        return true;
    }
    
    public override async Task<object> ExecuteCommandAsync(string command, object parameters)
    {
        // 实现设备命令执行
        return await ProcessCommandAsync(command, parameters);
    }
}
```

### 3. 模板开发
```csharp
public class CustomTemplate : ITemplate<CustomParam>
{
    public override string Title => "自定义模板";
    public string Code => "Custom";
    
    public void Load()
    {
        // 从数据库加载参数
        var items = Db.Queryable<ModMasterModel>()
            .Where(a => a.Type == TypeCode)
            .ToList();
        // ...
    }
}
```

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/engine-components/ColorVision.Engine.md)
- [流程引擎文档](../../docs/02-developer-guide/algorithm-engine-templates/flow-engine/流程引擎.md)
- [设备管理指南](../../docs/01-user-guide/devices/overview.md)
- [模板系统说明](../../docs/02-developer-guide/algorithm-engine-templates/template-management/模板管理.md)

## 依赖组件

- [cvColorVision](../cvColorVision/) - 底层算法库
- [ColorVision.FileIO](../ColorVision.FileIO/) - 文件IO处理
- [FlowEngineLib](../FlowEngineLib/) - 流程引擎核心库
- [ST.Library.UI](../ST.Library.UI/) - 节点编辑器UI库

## 更新日志

### v1.4.2.1（2026-02）
- 升级目标框架至 .NET 8.0 / .NET 10.0
- 优化设备服务管理
- 增强模板系统功能
- 改进MQTT通信稳定性

## 维护者

ColorVision 引擎团队
