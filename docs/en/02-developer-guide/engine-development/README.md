# Engine Development Guide

Introduces how to develop and extend ColorVision Engine layer functionality.

## Overview

ColorVision.Engine is the system's core engine layer, responsible for:

- 🔧 Device service management
- 🔄 Workflow engine
- 📐 Algorithm template system
- 📡 MQTT message processing
- 🖼️ OpenCV image processing

## Engine Architecture

```
ColorVision.Engine
├── Services/          # Devices and services
├── Templates/         # Template system
├── MQTT/              # MQTT message processing
├── Algorithms/        # Algorithm implementations
└── Utilities/         # Utility classes
```

## Main Components

### 1. Service System

See: [Service Development Guide](./services.md)

### 2. Template System

See: [Template System Development](./templates.md)

### 3. MQTT Message Processing

See: [MQTT Message Processing](./mqtt.md)

### 4. OpenCV Integration

See: [OpenCV Integration Development](./opencv-integration.md)

## Development Workflow

### 1. Create Service

```csharp
public class MyDeviceService : DeviceService
{
    public override string ServiceName => "My Device";
    
    protected override Task OnStartAsync()
    {
        // Initialize device
        return Task.CompletedTask;
    }
    
    protected override Task OnStopAsync()
    {
        // Stop device
        return Task.CompletedTask;
    }
}
```

### 2. Register Service

```csharp
ServiceManager.GetInstance().Add\<IMyDeviceService, MyDeviceService>();
```

### 3. Use Service

```csharp
var service = ServiceManager.GetInstance().GetService\<IMyDeviceService>();
await service.StartAsync();
```

## Best Practices

1. **Interface Definition**: Define an interface for each service
2. **Dependency Injection**: Use ServiceManager to manage dependencies
3. **Async Operations**: Use async/await for time-consuming operations
4. **Exception Handling**: Properly handle exceptions and log them
5. **Resource Management**: Implement IDisposable to release resources

## Related Documents

- [Service Development Guide](./services.md)
- [Template System Development](./templates.md)
- [MQTT Message Processing](./mqtt.md)
- [OpenCV Integration Development](./opencv-integration.md)
- [Engine API Reference](/en/04-api-reference/engine-components/README.md)

## Example Code

References:

- `Engine/ColorVision.Engine/Services/` - Service implementations
- `Engine/ColorVision.Engine/Templates/` - Template system
- `Engine/ColorVision.Engine/MQTT/` - MQTT implementation

---

*For more technical details, refer to each sub-topic document.*