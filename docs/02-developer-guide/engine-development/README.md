# Engine 开发指南

介绍如何开发和扩展 ColorVision Engine 层的功能。

## 概述

ColorVision.Engine 是系统的核心引擎层，负责：

- 🔧 设备服务管理
- 🔄 流程引擎
- 📐 算法模板系统
- 📡 MQTT 消息处理
- 🖼️ OpenCV 图像处理

## Engine 架构

```
ColorVision.Engine
├── Services/          # 设备和服务
├── Templates/         # 模板系统
├── MQTT/              # MQTT 消息处理
├── Algorithms/        # 算法实现
└── Utilities/         # 工具类
```

## 主要组件

### 1. 服务系统

详见：[服务开发指南](./services.md)

### 2. 模板系统

详见：[模板系统开发](./templates.md)

### 3. MQTT 消息处理

详见：[MQTT 消息处理](./mqtt.md)

### 4. OpenCV 集成

详见：[OpenCV 集成开发](./opencv-integration.md)

## 开发流程

### 1. 创建服务

```csharp
public class MyDeviceService : DeviceService
{
    public override string ServiceName => "My Device";
    
    protected override Task OnStartAsync()
    {
        // 初始化设备
        return Task.CompletedTask;
    }
    
    protected override Task OnStopAsync()
    {
        // 停止设备
        return Task.CompletedTask;
    }
}
```

### 2. 注册服务

```csharp
ServiceManager.GetInstance().Add<IMyDeviceService, MyDeviceService>();
```

### 3. 使用服务

```csharp
var service = ServiceManager.GetInstance().GetService<IMyDeviceService>();
await service.StartAsync();
```

## 最佳实践

1. **接口定义**: 为每个服务定义接口
2. **依赖注入**: 使用ServiceManager管理依赖
3. **异步操作**: 耗时操作使用async/await
4. **异常处理**: 妥善处理异常并记录日志
5. **资源管理**: 实现IDisposable释放资源

## 相关文档

- [服务开发指南](./services.md)
- [模板系统开发](./templates.md)
- [MQTT 消息处理](./mqtt.md)
- [OpenCV 集成开发](./opencv-integration.md)
- [Engine API 参考](/04-api-reference/engine-components/README.md)

## 示例代码

参考：

- `Engine/ColorVision.Engine/Services/` - 服务实现
- `Engine/ColorVision.Engine/Templates/` - 模板系统
- `Engine/ColorVision.Engine/MQTT/` - MQTT实现

---

*更多技术细节请参考各子主题文档。*
