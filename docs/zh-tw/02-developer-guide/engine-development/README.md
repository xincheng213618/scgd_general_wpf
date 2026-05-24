# Engine 開發指南

介紹如何開發和擴充套件 ColorVision Engine 層的功能。

## 概述

ColorVision.Engine 是系統的核心引擎層，負責：

- 🔧 裝置服務管理
- 🔄 流程引擎
- 📐 演算法模板系統
- 📡 MQTT 訊息處理
- 🖼️ OpenCV 影像處理

## Engine 架構

```
ColorVision.Engine
├── Services/          # 裝置和服務
├── Templates/         # 模板系統
├── MQTT/              # MQTT 訊息處理
├── Algorithms/        # 演算法實現
└── Utilities/         # 工具類
```

## 主要元件

### 1. 服務系統

詳見：[服務開發指南](./services.md)

### 2. 模板系統

詳見：[模板系統開發](./templates.md)

### 3. MQTT 訊息處理

詳見：[MQTT 訊息處理](./mqtt.md)

### 4. OpenCV 整合

詳見：[OpenCV 整合開發](./opencv-integration.md)

## 開發流程

### 1. 建立服務

```csharp
public class MyDeviceService : DeviceService
{
    public override string ServiceName => "My Device";
    
    protected override Task OnStartAsync()
    {
        // 初始化裝置
        return Task.CompletedTask;
    }
    
    protected override Task OnStopAsync()
    {
        // 停止裝置
        return Task.CompletedTask;
    }
}
```

### 2. 註冊服務

```csharp
ServiceManager.GetInstance().Add\<IMyDeviceService, MyDeviceService>();
```

### 3. 使用服務

```csharp
var service = ServiceManager.GetInstance().GetService\<IMyDeviceService>();
await service.StartAsync();
```

## 最佳實踐

1. **介面定義**: 為每個服務定義介面
2. **依賴注入**: 使用ServiceManager管理依賴
3. **非同步操作**: 耗時操作使用async/await
4. **異常處理**: 妥善處理異常並記錄日誌
5. **資源管理**: 實現IDisposable釋放資源

## 相關文件

- [服務開發指南](./services.md)
- [模板系統開發](./templates.md)
- [MQTT 訊息處理](./mqtt.md)
- [OpenCV 整合開發](./opencv-integration.md)
- [Engine API 參考](/zh-tw/04-api-reference/engine-components/README.md)

## 示例程式碼

參考：

- `Engine/ColorVision.Engine/Services/` - 服務實現
- `Engine/ColorVision.Engine/Templates/` - 模板系統
- `Engine/ColorVision.Engine/MQTT/` - MQTT實現

---

*更多技術細節請參考各子主題文件。*
