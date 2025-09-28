# ColorVision.Engine

## 目录
1. [概述](#概述)
2. [核心功能](#核心功能)
3. [架构设计](#架构设计)
4. [主要组件](#主要组件)
5. [流程引擎](#流程引擎)
6. [设备服务管理](#设备服务管理)
7. [模板系统](#模板系统)
8. [数据库操作](#数据库操作)
9. [使用示例](#使用示例)
10. [API参考](#api参考)

## 概述

**ColorVision.Engine** 是 ColorVision 系统的核心引擎，负责流程管理、设备服务协调、模板系统和数据处理。它是整个系统的控制中心，通过可视化的流程设计器，用户可以创建复杂的测试和分析流程。

### 基本信息

- **主要功能**: 流程引擎、设备管理、模板系统、数据库访问
- **架构模式**: 模块化、服务化、事件驱动
- **通信协议**: MQTT、TCP/IP、串行通信
- **数据存储**: MySQL、SQLite、文件系统
- **特色功能**: 可视化流程设计、设备自动发现、实时监控

## 核心功能

### 1. 流程引擎系统
- **可视化流程设计**: 通过拖拽节点创建复杂流程
- **流程执行管理**: 支持串行、并行、条件分支执行
- **状态监控**: 实时监控流程执行状态和进度
- **异常处理**: 完善的错误处理和恢复机制

### 2. 设备服务管理
- **设备自动发现**: 自动扫描和识别网络设备
- **多协议支持**: MQTT、TCP、UDP、串行通信
- **服务注册**: 动态注册和管理设备服务
- **状态同步**: 实时同步设备状态和参数

### 3. 模板系统
- **参数化模板**: 支持灵活的参数配置
- **模板继承**: 模板间的继承和组合关系
- **版本管理**: 模板版本控制和历史记录
- **导入导出**: 模板的备份和迁移功能

### 4. 数据管理
- **多数据源支持**: MySQL、SQLite、文件存储
- **ORM映射**: Entity Framework数据访问层
- **事务管理**: 保证数据操作的一致性
- **数据同步**: 支持数据的同步和复制

## 架构设计

```mermaid
graph TB
    subgraph "ColorVision.Engine 架构"
        A[FlowEngine<br/>流程引擎] --> B[NodeManager<br/>节点管理器]
        A --> C[ExecutionEngine<br/>执行引擎]
        
        D[DeviceManager<br/>设备管理器] --> E[DeviceService<br/>设备服务]
        D --> F[CommunicationLayer<br/>通信层]
        
        G[TemplateManager<br/>模板管理器] --> H[TemplateLoader<br/>模板加载器]
        G --> I[ParameterManager<br/>参数管理器]
        
        J[DatabaseManager<br/>数据库管理器] --> K[EntityModels<br/>实体模型]
        J --> L[DataAccess<br/>数据访问层]
        
        M[ServiceBus<br/>服务总线] --> A
        M --> D
        M --> G
        M --> J
    end
    
    subgraph "外部接口"
        N[MQTT Broker]
        O[MySQL Database]
        P[Device Hardware]
        Q[File System]
    end
    
    F --> N
    F --> P
    L --> O
    H --> Q
```

## 主要组件

### FlowEngineManager 流程引擎管理器

负责流程的创建、加载、执行和监控管理。

```csharp
public class FlowEngineManager
{
    // 加载流程模板
    public async Task<FlowTemplate> LoadFlowTemplateAsync(string templateName)
    
    // 执行流程
    public async Task<FlowResult> ExecuteFlowAsync(FlowTemplate template, Dictionary<string, object> parameters)
    
    // 监控流程状态
    public event EventHandler<FlowStatusChangedEventArgs> FlowStatusChanged;
}
```

### DeviceServiceManager 设备服务管理器

管理所有设备服务的生命周期和通信。

```csharp
public class DeviceServiceManager
{
    // 注册设备服务
    public void RegisterService<T>() where T : IDeviceService
    
    // 获取设备服务
    public T GetService<T>() where T : IDeviceService
    
    // 设备状态变更事件
    public event EventHandler<DeviceStatusChangedEventArgs> DeviceStatusChanged;
}
```

### TemplateManager 模板管理器

处理模板的加载、保存、参数管理等操作。

```csharp
public class TemplateManager
{
    // 创建新模板
    public async Task<Template> CreateTemplateAsync(string name, TemplateType type)
    
    // 保存模板
    public async Task SaveTemplateAsync(Template template)
    
    // 加载模板参数
    public async Task<Dictionary<string, object>> LoadTemplateParametersAsync(int templateId)
}
```

## 流程引擎

### 流程设计器

基于 STNodeEditor 的可视化流程设计器，支持：

- **节点拖拽**: 从工具箱拖拽节点到画布
- **连接管理**: 节点间的输入输出连接
- **属性编辑**: 实时编辑节点属性
- **布局管理**: 自动布局和手动调整

### 节点类型

#### 1. 输入节点
- **数据输入**: 从文件、数据库、设备读取数据
- **参数设置**: 流程参数和常量定义
- **触发条件**: 定时、事件、手动触发

#### 2. 处理节点
- **图像处理**: 滤波、变换、增强、分析
- **数据处理**: 统计、计算、格式转换
- **算法执行**: 调用自定义算法模块

#### 3. 控制节点
- **条件判断**: if-else逻辑分支
- **循环控制**: for、while循环处理
- **并行执行**: 多线程并行处理

#### 4. 输出节点
- **结果保存**: 保存到文件、数据库
- **报告生成**: 自动生成分析报告
- **设备控制**: 控制外部设备动作

### 执行机制

```csharp
// 流程执行示例
var flowEngine = new FlowEngine();
var flow = await flowEngine.LoadFlowAsync("quality_check_flow");

// 设置参数
flow.SetParameter("input_image", imagePath);
flow.SetParameter("threshold", 0.8);

// 执行流程
var result = await flowEngine.ExecuteAsync(flow);

// 处理结果
if (result.Success)
{
    var outputData = result.GetOutput<ProcessingResult>();
    Console.WriteLine($"处理完成：{outputData.Message}");
}
```

## 设备服务管理

### 设备服务架构

每个设备类型都有对应的服务类，实现 IDeviceService 接口：

```csharp
public interface IDeviceService
{
    string ServiceType { get; }
    DeviceStatus Status { get; }
    
    Task<bool> ConnectAsync();
    Task<bool> DisconnectAsync();
    Task<object> ExecuteCommandAsync(string command, object parameters);
    
    event EventHandler<DeviceStatusChangedEventArgs> StatusChanged;
}
```

### 支持的设备类型

#### 1. 相机设备服务
- **LV相机**: 亮度图像采集
- **BV相机**: 生物视觉图像
- **CV相机**: 色彩视觉图像
- **通用相机**: 标准工业相机

#### 2. 光谱设备服务
- **光谱仪**: 光谱数据采集
- **色度计**: 色度参数测量
- **辐射计**: 辐射量测量

#### 3. 运动控制服务
- **电机控制**: 步进电机、伺服电机
- **位移台**: XYZ精密位移控制
- **旋转台**: 角度控制和定位

#### 4. 通信服务
- **MQTT服务**: 消息发布订阅
- **TCP服务**: 网络通信服务
- **串口服务**: RS232/RS485通信

### 设备配置管理

```csharp
// 设备服务配置示例
public class CameraServiceConfig
{
    public string IpAddress { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public CameraType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
}

// 注册和配置设备服务
var deviceManager = ServiceContainer.GetService<DeviceServiceManager>();
var cameraService = deviceManager.GetService<CameraService>();
await cameraService.ConfigureAsync(config);
```

## 模板系统

### 模板类型

#### 1. 算法模板
- **图像处理算法**: 各种图像算法的参数模板
- **测量算法**: 尺寸、角度、面积测量
- **检测算法**: 缺陷检测、特征识别

#### 2. 流程模板
- **标准流程**: 预定义的标准检测流程
- **自定义流程**: 用户创建的自定义流程
- **组合流程**: 多个子流程的组合

#### 3. 设备模板
- **设备配置**: 设备连接和参数配置
- **校准模板**: 设备校准参数
- **操作模板**: 常用操作的参数预设

### 模板参数管理

```csharp
// 模板参数定义
public class TemplateParameter
{
    public string Name { get; set; }
    public Type ParameterType { get; set; }
    public object DefaultValue { get; set; }
    public object MinValue { get; set; }
    public object MaxValue { get; set; }
    public string Description { get; set; }
    public bool Required { get; set; }
}

// 使用模板参数
var template = await templateManager.LoadTemplateAsync("image_enhance");
var parameters = template.GetParameters();
parameters["brightness"].Value = 1.2;
parameters["contrast"].Value = 0.8;
await template.ApplyParametersAsync(parameters);
```

## 数据库操作

### 数据模型

主要数据实体包括：

```csharp
// 模板主表
public class ModMaster
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Type { get; set; }
    public string CreateDate { get; set; }
    public int SysResourceId { get; set; }
    public int Pid { get; set; }
    public string Remark { get; set; }
    public int TenantId { get; set; }
    public bool IsDelete { get; set; }
}

// 流程详情表  
public class ModFlowDetail
{
    public int Id { get; set; }
    public int MasterId { get; set; }
    public string FlowData { get; set; }  // Base64编码的流程数据
    public string CreateDate { get; set; }
    public bool IsEnable { get; set; }
    public string Remark { get; set; }
}
```

### DAO 数据访问层

```csharp
public class ModMasterDao : BaseDao<ModMaster>
{
    // 按类型查询模板
    public async Task<List<ModMaster>> GetByTypeAsync(int type)
    
    // 按名称搜索
    public async Task<List<ModMaster>> SearchByNameAsync(string keyword)
    
    // 获取模板树结构
    public async Task<List<ModMaster>> GetTreeStructureAsync(int parentId)
}

public class ModFlowDetailDao : BaseDao<ModFlowDetail>
{
    // 获取流程详情
    public async Task<ModFlowDetail> GetFlowDetailAsync(int masterId)
    
    // 保存流程数据
    public async Task SaveFlowDataAsync(int masterId, string flowData)
}
```

## 使用示例

### 完整的流程执行示例

```csharp
public class QualityCheckFlow
{
    private readonly FlowEngineManager _flowEngine;
    private readonly DeviceServiceManager _deviceManager;
    private readonly TemplateManager _templateManager;
    
    public async Task<QualityCheckResult> ExecuteQualityCheckAsync(string productId)
    {
        try
        {
            // 1. 加载质量检测流程模板
            var flowTemplate = await _templateManager.LoadTemplateAsync("quality_check_flow");
            
            // 2. 获取相机服务
            var cameraService = _deviceManager.GetService<CameraService>();
            await cameraService.ConnectAsync();
            
            // 3. 设置流程参数
            var parameters = new Dictionary<string, object>
            {
                ["product_id"] = productId,
                ["camera_service"] = cameraService,
                ["quality_threshold"] = 0.95,
                ["save_images"] = true
            };
            
            // 4. 执行流程
            var result = await _flowEngine.ExecuteFlowAsync(flowTemplate, parameters);
            
            // 5. 处理结果
            if (result.Success)
            {
                var qualityResult = result.GetOutput<QualityCheckResult>();
                await SaveResultAsync(qualityResult);
                return qualityResult;
            }
            else
            {
                LogError($"质量检测失败: {result.ErrorMessage}");
                throw new Exception(result.ErrorMessage);
            }
        }
        finally
        {
            // 6. 清理资源
            await _deviceManager.DisconnectAllAsync();
        }
    }
}
```

### 自定义算法节点示例

```csharp
[NodeInfo("图像增强", "对图像进行亮度和对比度调整")]
public class ImageEnhanceNode : BaseFlowNode
{
    [NodeParameter("亮度", 1.0, 0.1, 3.0)]
    public double Brightness { get; set; } = 1.0;
    
    [NodeParameter("对比度", 1.0, 0.1, 3.0)]
    public double Contrast { get; set; } = 1.0;
    
    public override async Task<NodeResult> ExecuteAsync(NodeContext context)
    {
        var inputImage = context.GetInput<Mat>("image");
        if (inputImage == null)
            return NodeResult.Error("输入图像为空");
            
        var outputImage = new Mat();
        inputImage.ConvertTo(outputImage, -1, Contrast, Brightness * 255 - 255);
        
        return NodeResult.Success(new { image = outputImage });
    }
}
```

## API参考

### 核心接口

#### IFlowEngine
```csharp
public interface IFlowEngine
{
    Task<FlowResult> ExecuteAsync(FlowDefinition flow);
    Task<FlowDefinition> LoadFlowAsync(string flowName);
    Task SaveFlowAsync(FlowDefinition flow);
    event EventHandler<FlowExecutionEventArgs> FlowExecutionChanged;
}
```

#### IDeviceService
```csharp
public interface IDeviceService
{
    string ServiceType { get; }
    DeviceStatus Status { get; }
    Task<bool> ConnectAsync();
    Task<object> ExecuteCommandAsync(string command, object parameters);
    event EventHandler<DeviceStatusChangedEventArgs> StatusChanged;
}
```

#### ITemplate
```csharp
public interface ITemplate
{
    string Name { get; }
    TemplateType Type { get; }
    Dictionary<string, TemplateParameter> Parameters { get; }
    Task ApplyAsync(Dictionary<string, object> parameters);
    Task<object> ExecuteAsync();
}
```

### 配置和初始化

```csharp
// 服务容器配置
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<FlowEngineManager>();
    services.AddSingleton<DeviceServiceManager>();
    services.AddSingleton<TemplateManager>();
    services.AddSingleton<DatabaseManager>();
    
    // 注册设备服务
    services.AddTransient<CameraService>();
    services.AddTransient<SpectrometerService>();
    services.AddTransient<MotorService>();
}

// 初始化引擎
public async Task InitializeEngineAsync()
{
    var serviceProvider = BuildServiceProvider();
    
    var deviceManager = serviceProvider.GetService<DeviceServiceManager>();
    await deviceManager.InitializeAsync();
    
    var templateManager = serviceProvider.GetService<TemplateManager>();
    await templateManager.LoadTemplatesAsync();
    
    var flowEngine = serviceProvider.GetService<FlowEngineManager>();
    await flowEngine.InitializeAsync();
}
```

## 相关资源

- [流程引擎详细文档](../algorithm-engine-templates/flow-engine/流程引擎.md)
- [设备管理指南](../device-management/)
- [模板系统说明](../algorithm-engine-templates/template-management/模板管理.md)
- [数据库设计文档](../developer-guide/database-schema/)
- [API完整参考](../developer-guide/api-reference/)