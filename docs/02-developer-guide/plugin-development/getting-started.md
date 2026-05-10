# 插件开发入门

本页提供当前仓库可执行的最短插件开发路径，不再沿用旧版通用宿主、异步生命周期和 `plugin.json` 示例。

## 先准备什么

- Windows 开发环境
- .NET 8.0 SDK
- WPF 开发工具链
- 当前仓库源码和主程序可运行输出

## 最小开发路径

### 1. 新建插件项目

建议把插件项目直接建在 `Plugins/<PluginId>/` 下，目标框架保持为 `net8.0-windows`。如果插件带界面，启用 WPF。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <OutputType>Library</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\UI\ColorVision.Common\ColorVision.Common.csproj" Private="false" />
    <ProjectReference Include="..\..\UI\ColorVision.UI\ColorVision.UI.csproj" Private="false" />
  </ItemGroup>
</Project>
```

## 2. 实现最小插件入口

当前仓库里最直接的入口是实现 `IPluginBase`：

```csharp
using ColorVision.UI;

namespace ColorVision.Plugins.MyPlugin;

public class MyPluginEntry : IPluginBase
{
    public override string Header => "我的插件";
    public override string Description => "一个最小可加载插件";

    public override void Execute()
    {
        // 最简单的入口逻辑
    }
}
```

如果插件需要挂到菜单或设置页，通常还会实现平台扫描的 provider 接口，例如 `IMenuItemProvider`。这类接口的具体用法建议直接参考现有插件实现。

## 3. 添加 manifest.json

插件目录至少需要一个 `manifest.json`：

```json
{
  "id": "MyPlugin",
  "manifest_version": 1,
  "name": "我的插件",
  "version": "1.0.0",
  "description": "插件功能描述",
  "dllpath": "MyPlugin.dll",
  "requires": "1.3.12.0",
  "author": "Your Name"
}
```

如果需要显式指定入口类型，可以继续补 `entry_point`。

## 4. 把产物复制到主程序插件目录

主程序运行时会从自己的输出目录扫描 `Plugins/`，所以调试时需要把插件产物复制进去。

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /E /I $(TargetDir)* $(SolutionDir)ColorVision\bin\$(ConfigurationName)\net8.0-windows\Plugins\MyPlugin\" />
</Target>
```

如果你本地输出目录不同，应按实际主程序输出路径调整。

## 5. 运行和调试

1. 构建主程序。
2. 构建插件项目，确认 DLL 和 `manifest.json` 已复制到插件目录。
3. 启动 `ColorVision/ColorVision.csproj`。
4. 在对应菜单、工具页或插件管理界面验证插件是否被加载。

## 推荐参考实现

- `Plugins/EventVWR/EventVWRPlugins.cs`
- `Plugins/EventVWR/Dump/MenuDump.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
- `Plugins/README.md`

这些示例已经覆盖了基础插件入口和菜单扩展两类常见模式。

## 常见问题

### 插件没有被发现

- 检查 `manifest.json` 是否存在
- 检查 `dllpath` 指向的 DLL 是否真实存在
- 检查插件目录是否已经复制到主程序输出目录下的 `Plugins/<PluginId>/`

### 插件被发现但功能没出现

- 检查是否只实现了基础插件类，但没有实现需要的 provider 接口
- 检查入口类型是否有公开无参构造
- 检查类型是否为非抽象、非泛型开放类型

### 依赖冲突

- 不要重复打包平台自带的 `ColorVision.*.dll`
- 若插件带 `.deps.json`，确认依赖版本不高于目标平台

## 下一步

- 想理解平台如何扫描和装载插件：看 [插件生命周期](./lifecycle.md)
- 想先了解整体结构：看 [插件开发概览](./overview.md)
$packagePath = "bin\$Configuration\MyAwesomePlugin-$Version.zip"
Upload-PluginPackage -Path $packagePath -Version $Version

Write-Host "Plugin deployed successfully: $Version"
```

## 最佳实践

### 错误处理

```csharp
public class PluginErrorHandler
{
    private readonly ILogger _logger;
    
    public async Task\<T\> ExecuteWithErrorHandlingAsync\<T\>(Func<Task\<T\>> operation, string operationName)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Operation cancelled: {OperationName}", operationName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Operation failed: {OperationName}", operationName);
            
            // 根据异常类型决定是否重试
            if (IsRetriableException(ex))
            {
                return await RetryOperation(operation, operationName);
            }
            
            throw;
        }
    }
}
```

### 性能优化

```csharp
public class PerformanceOptimizer
{
    // 使用对象池减少内存分配
    private readonly ObjectPool\<StringBuilder\> _stringBuilderPool;
    
    // 缓存昂贵的计算结果
    private readonly MemoryCache _computationCache;
    
    // 异步操作避免阻塞UI线程
    public async Task\<ProcessingResult\> ProcessImageAsync(string imagePath)
    {
        return await Task.Run(() => ProcessImageCore(imagePath));
    }
    
    // 合理使用ConfigureAwait
    private async Task\<string\> LoadConfigurationAsync()
    {
        var content = await File.ReadAllTextAsync(ConfigPath).ConfigureAwait(false);
        return content;
    }
}
```

### 资源管理

```csharp
public class ResourceManager : IDisposable
{
    private readonly List\\<IDisposable\> _disposables = new();
    private bool _disposed;
    
    public void RegisterDisposable(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch (Exception ex)
            {
                // 记录但不抛出异常
                _logger.Warning(ex, "Error disposing resource");
            }
        }
        
        _disposed = true;
    }
}
```

### 配置管理

```csharp
public class PluginConfiguration
{
    public double DefaultThreshold { get; set; } = 0.5;
    public bool EnableGpuAcceleration { get; set; } = true;
    public string OutputDirectory { get; set; } = "./Output";
    
    public static async Task\<PluginConfiguration\> LoadAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            var defaultConfig = new PluginConfiguration();
            await SaveAsync(defaultConfig, configPath);
            return defaultConfig;
        }
        
        var json = await File.ReadAllTextAsync(configPath);
        return JsonSerializer.Deserialize\<PluginConfiguration\>(json);
    }
    
    public static async Task SaveAsync(PluginConfiguration config, string configPath)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await File.WriteAllTextAsync(configPath, json);
    }
}
```

### 本地化支持

```csharp
// Resources/Strings.resx (默认语言)
// Resources/Strings.zh-CN.resx (中文)
// Resources/Strings.en-US.resx (英文)

public static class Strings
{
    private static readonly ResourceManager ResourceManager = 
        new ResourceManager("ColorVision.Plugins.MyAwesome.Resources.Strings", 
                           typeof(Strings).Assembly);
    
    public static string GetString(string name)
    {
        return ResourceManager.GetString(name) ?? $"[Missing: {name}]";
    }
    
    public static string ProcessingCompleted => GetString(nameof(ProcessingCompleted));
    public static string ProcessingFailed => GetString(nameof(ProcessingFailed));
}
```

---

*最后更新: 2024-09-28 | 状态: draft*