# 配置服务实现模式指南

本文档说明如何在不同的项目场景中选择和使用合适的 `IConfigService` 实现。

## 核心原理

`ConfigSettingManager` 只依赖 `IConfigService` 接口，不关心具体实现：

```csharp
// ConfigSettingManager 中的代码
var instance = ConfigService.Instance.GetRequiredService(configType);
```

这意味着：
- **解耦**：配置扫描与持久化机制完全分离
- **可插拔**：替换 `ConfigService.Instance` 的实现，而不修改 UI 逻辑
- **灵活性**：支持多种配置管理架构

---

## 场景 1：自维护单例（ColorVision 当前方案）

**适用场景**：
- 每个配置类维护自己的 `static Instance`
- 无需集中的 DI 容器
- 配置各自独立保存/加载

**实现**：使用 `SelfManagedConfigServiceAdapter`

### 配置类示例

```csharp
public class UIConfig : IConfig
{
    public static UIConfig Instance { get; } = new();

    [ConfigSetting]
    [DisplayName("Theme")]
    [Category("Appearance")]
    public string Theme { get; set; } = "Dark";

    public void Load() 
    { 
        // 从文件/数据库加载
    }

    public void Save() 
    { 
        // 保存到文件/数据库
    }
}

public class DeviceConfig : IConfig
{
    public static DeviceConfig Instance { get; } = new();

    [ConfigSetting]
    [DisplayName("Device Port")]
    public string Port { get; set; } = "COM1";

    public void Load() { }
    public void Save() { }
}
```

### 初始化

```csharp
// App.xaml.cs 或 Startup
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 使用自维护单例适配器
        ConfigService.Instance = new SelfManagedConfigServiceAdapter();
        
        // ConfigSettingManager 现在可以访问所有配置
        ConfigSettingManager.Initialize();
        
        base.OnStartup(e);
    }
}
```

### 工作流

```
App 启动
  ↓
ConfigSettingManager 扫描后台代码中的 [ConfigSetting] 属性
  ↓
对于每个配置类型 T：
  ConfigService.Instance.GetRequiredService(T)
  → SelfManagedConfigServiceAdapter 查找 T.Instance
  ↓
返回实例到 PropertyGrid
```

---

## 场景 2：混合模式（显式注册 + 反射回退）

**适用场景**：
- 部分配置已注册到 DI
- 其他配置用 static Instance（向后兼容）
- 需要灵活切换实现

**实现**：使用 `HybridConfigServiceAdapter`

### 配置类示例

```csharp
// 使用 static Instance
public class UIConfig : IConfig
{
    public static UIConfig Instance { get; } = new();
    // ...
}

// 使用 DI 注册
public class DatabaseConfig : IConfig
{
    public string ConnectionString { get; set; }
    public void Load() { }
    public void Save() { }
}
```

### 初始化

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var adapter = new HybridConfigServiceAdapter();
        
        // 显式注册某些配置
        var dbConfig = new DatabaseConfig { ConnectionString = GetConnString() };
        adapter.Register(dbConfig);
        
        ConfigService.Instance = adapter;
        ConfigSettingManager.Initialize();
        
        base.OnStartup(e);
    }
}
```

### 工作流

```
ConfigSettingManager 需要 DatabaseConfig
  ↓
ConfigService.GetRequiredService(DatabaseConfig)
  ↓
HybridConfigServiceAdapter：
  1. 先查找已注册的实例 → 找到 dbConfig ✓ 返回
  2. 若无，查找 DatabaseConfig.Instance
  3. 若都无，抛出异常
```

---

## 场景 3：ASP.NET Core / Unity / Prism DI

**适用场景**：
- 应用已使用 ASP.NET Core DI、Unity、Prism 等容器
- 所有配置通过 DI 注册
- 集中管理配置生命周期

**实现**：使用 `AspNetCoreConfigServiceAdapter`

### 配置类示例（与 DI 无关）

```csharp
public class UIConfig : IConfig
{
    [ConfigSetting]
    public string Theme { get; set; }
    
    public void Load() { }
    public void Save() { }
}

public class DeviceConfig : IConfig
{
    [ConfigSetting]
    public string Port { get; set; }
    
    public void Load() { }
    public void Save() { }
}
```

### DI 容器注册

```csharp
// ConfigHost.cs 或 ServiceCollection 配置
var services = new ServiceCollection();

// 注册每个配置实例
services.AddSingleton<IConfig>(sp => new UIConfig());
services.AddSingleton<IConfig>(sp => new DeviceConfig());

var provider = services.BuildServiceProvider();
```

### 初始化

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var serviceProvider = GetServiceProvider(); // 从 DI 容器获取
        
        ConfigService.Instance = new AspNetCoreConfigServiceAdapter(serviceProvider);
        ConfigSettingManager.Initialize();
        
        base.OnStartup(e);
    }
}
```

### 工作流

```
ConfigSettingManager 需要 UIConfig
  ↓
ConfigService.GetRequiredService(UIConfig)
  ↓
AspNetCoreConfigServiceAdapter：
  ServiceProvider.GetService(UIConfig)
  → DI 容器返回已注册的单例 ✓
```

---

## 场景 4：自定义实现

如果以上三种都不适合，实现自定义 `IConfigService`：

```csharp
public class CustomConfigService : IConfigService
{
    public IConfig GetRequiredService(Type type)
    {
        // 自定义逻辑：从数据库、文件、网络等获取
        // 例如：from SQL database by type name
        // 例如：from Redis cache
        // 例如：从配置文件工厂
        
        return /* ... */;
    }

    public T1 GetRequiredService<T1>() where T1 : IConfig
        => (T1)GetRequiredService(typeof(T1));

    public void SaveConfigs() { /* ... */ }
    public void LoadConfigs() { /* ... */ }
    public void Save<T1>() where T1 : IConfig { /* ... */ }
}

// 初始化
ConfigService.Instance = new CustomConfigService();
ConfigSettingManager.Initialize();
```

---

## 对比表

| 维度 | 自维护单例 | 混合模式 | ASP.NET Core DI |
|-----|---------|--------|-----------------|
| **复杂度** | 低 | 中 | 中 |
| **向后兼容** | 最好 | 好 | 需要迁移 |
| **集中管理** | 否 | 部分 | 是 |
| **DI 框架** | 无需 | 可选 | 必需 |
| **扩展性** | 低 | 中 | 高 |
| **推荐场景** | 小项目 | 过渡期 | 大型应用 |

---

## 关键设计原则

### 1. **IConfigService 是抽象契约，不是具体实现**

```csharp
// ✓ 正确：只依赖接口
instance = ConfigService.Instance.GetRequiredService(type);

// ✗ 避免：依赖具体类
instance = ((SelfManagedConfigServiceAdapter)ConfigService.Instance).ResolveInstance(type);
```

### 2. **[ConfigSetting] 属性扫描与持久化完全分离**

```
扫描层（与实现无关）
    ↓
    ConfigSettingManager 收集 [ConfigSetting] 属性
    ↓
    UIPropertyGrid 呈现
    ↓
   
加载层（可插拔）
    ↓
    IConfigService.GetRequiredService(type)
    ↓
    具体实现决定如何获取实例
```

### 3. **不需要修改 ConfigSettingManager 代码**

- 更换实现 → 修改一行：`ConfigService.Instance = new XXXAdapter()`
- 无需改动菜单、PropertyGrid、属性扫描逻辑

---

## 迁移指南

### 从当前方案迁移到 DI

**第 1 步**：保持现有配置类不变

```csharp
// ColorVision 当前代码，无需改动
public class UIConfig : IConfig
{
    public static UIConfig Instance { get; } = new();
    // ...
}
```

**第 2 步**：切换 ConfigService

```csharp
// 方案 A：用混合适配器（渐进式迁移）
var adapter = new HybridConfigServiceAdapter();
ConfigService.Instance = adapter;

// 方案 B：完全切换到 DI（一次性）
var services = new ServiceCollection();
services.AddSingleton<IConfig>(sp => UIConfig.Instance);
var provider = services.BuildServiceProvider();
ConfigService.Instance = new AspNetCoreConfigServiceAdapter(provider);
```

**第 3 步**：ConfigSettingManager 无需改动 ✓

---

## 总结

| 概念 | 说明 |
|-----|------|
| **IConfigService** | 统一的请求接口（解耦点） |
| **ConfigSettingManager** | 属性扫描与 UI 展示（实现无关） |
| **具体适配器** | 实例获取策略（可替换） |
| **[ConfigSetting]** | 配置元数据（架构中立） |

核心价值：**同一套 PropertyGrid UI 代码，可运行在任何配置架构上**。

