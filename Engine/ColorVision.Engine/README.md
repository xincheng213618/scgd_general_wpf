# ColorVision.Engine

ColorVision.Engine 是 ColorVision 系统的核心引擎，负责流程管理、设备服务协调、模板系统和数据处理。它是整个系统的控制中心，通过可视化的流程设计器，用户可以创建复杂的测试和分析流程。

## 🚀 核心功能

### 流程引擎系统
- **可视化流程设计**: 通过拖拽节点创建复杂流程
- **流程执行管理**: 支持串行、并行、条件分支执行
- **状态监控**: 实时监控流程执行状态和进度
- **异常处理**: 完善的错误处理和恢复机制

### 设备服务管理
- **设备自动发现**: 自动扫描和识别网络设备
- **多协议支持**: MQTT、TCP、UDP、串行通信
- **服务注册**: 动态注册和管理设备服务
- **状态同步**: 实时同步设备状态和参数

### 模板系统
- **参数化模板**: 支持灵活的参数配置
- **模板继承**: 模板间的继承和组合关系
- **版本管理**: 模板版本控制和历史记录
- **导入导出**: 模板的备份和迁移功能

### 数据管理
- **多数据源支持**: MySQL、SQLite、文件存储
- **ORM映射**: Entity Framework数据访问层
- **事务管理**: 保证数据操作的一致性
- **数据同步**: 支持数据的同步和复制

## 🏗️ 架构特点

- **模块化设计**: 每个组件职责单一，接口清晰
- **服务化架构**: 基于服务总线的松耦合设计
- **事件驱动**: 异步事件处理机制
- **插件化扩展**: 支持自定义算法节点和设备驱动

## 📦 主要组件

- **FlowEngineManager**: 流程引擎管理器
- **DeviceServiceManager**: 设备服务管理器
- **TemplateManager**: 模板管理器
- **DatabaseManager**: 数据库管理器

## 🔧 支持的设备类型

- **相机设备**: LV相机、BV相机、CV相机、通用相机
- **光谱设备**: 光谱仪、色度计、辐射计
- **运动控制**: 电机控制、位移台、旋转台
- **通信服务**: MQTT服务、TCP服务、串口服务

## 💡 快速开始

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

## 📚 文档资源

- [详细技术文档](../../docs/engine-components/ColorVision.Engine.md)
- [流程引擎文档](../../docs/algorithm-engine-templates/flow-engine/流程引擎.md)
- [设备管理指南](../../docs/device-management/)
- [API参考文档](../../docs/developer-guide/api-reference/)

## 🔗 相关组件

- [cvColorVision](../cvColorVision/) - 底层算法库
- [ColorVision.FileIO](../ColorVision.FileIO/) - 文件IO处理
- [FlowEngineLib](../FlowEngineLib/) - 流程引擎核心库
