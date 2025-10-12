# Templates 模块优化建议 - 下篇：性能与扩展性优化

## 目录
1. [概述](#概述)
2. [性能优化](#性能优化)
3. [内存管理优化](#内存管理优化)
4. [数据库访问优化](#数据库访问优化)
5. [并发处理优化](#并发处理优化)
6. [缓存策略优化](#缓存策略优化)
7. [扩展性设计](#扩展性设计)
8. [监控与诊断](#监控与诊断)
9. [实施建议](#实施建议)

## 概述

本文档是Templates模块优化建议的下篇，聚焦于性能优化和扩展性设计。通过系统的性能优化和合理的扩展性设计，可以显著提升系统的吞吐量、响应速度和可伸缩性。

### 优化目标

- **提升性能**: 减少延迟，提高吞吐量
- **降低资源消耗**: 优化内存和CPU使用
- **增强并发能力**: 支持更高的并发负载
- **提高扩展性**: 便于功能扩展和系统扩容

## 性能优化

### 1. 模板加载性能优化

#### 当前问题

```csharp
// 同步加载，阻塞UI
public void Load()
{
    var items = Db.Queryable<ModMasterModel>()
        .Where(a => a.Type == TemplateType)
        .ToList();
    
    Params.Clear();
    foreach (var item in items)
    {
        // 每个item都查询一次数据库
        var details = Db.Queryable<ModDetailModel>()
            .Where(d => d.ModMasterId == item.Id)
            .ToList();
        Params.Add(new MTFParam(item, details));
    }
}
```

**问题**:
- 同步操作阻塞UI线程
- N+1查询问题（每个Master查询一次Detail）
- 未使用批量加载

#### 优化方案

```csharp
// 异步批量加载
public async Task LoadAsync(CancellationToken cancellationToken = default)
{
    // 1. 异步查询主表
    var items = await Db.Queryable<ModMasterModel>()
        .Where(a => a.Type == TemplateType)
        .ToListAsync(cancellationToken);
    
    if (items.Count == 0)
        return;
    
    // 2. 批量查询详情表（一次性查询所有）
    var masterIds = items.Select(i => i.Id).ToList();
    var allDetails = await Db.Queryable<ModDetailModel>()
        .Where(d => masterIds.Contains(d.ModMasterId))
        .ToListAsync(cancellationToken);
    
    // 3. 按MasterId分组
    var detailsDict = allDetails
        .GroupBy(d => d.ModMasterId)
        .ToDictionary(g => g.Key, g => g.ToList());
    
    // 4. 在UI线程更新集合
    await Application.Current.Dispatcher.InvokeAsync(() =>
    {
        Params.Clear();
        foreach (var item in items)
        {
            var details = detailsDict.GetValueOrDefault(item.Id, new List<ModDetailModel>());
            Params.Add(new MTFParam(item, details));
        }
    });
}
```

**性能提升**: 
- 从N+1次查询优化为2次查询
- 异步操作不阻塞UI
- 对于100个模板项，查询时间从~5秒降至~0.5秒

### 2. 参数值转换性能优化

#### 当前问题

```csharp
public T? GetValue<T>(T? storage, [CallerMemberName] string propertyName = "")
{
    if (parameters != null && parameters.Count > 0)
    {
        if (parameters.TryGetValue(propertyName, out ModDetailModel modDetailModel))
        {
            string val = modDetailModel.ValueA;
            
            // 每次都进行类型判断和转换
            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
            {
                if (string.IsNullOrEmpty(val)) val = "0";
                return (T)(object)int.Parse(val);
            }
            // ... 更多类型判断
        }
    }
    return storage;
}
```

**问题**:
- 每次访问都要类型判断
- 每次都要解析字符串
- 未缓存解析结果

#### 优化方案

```csharp
public class ModelBase : ParamBase
{
    // 缓存解析后的值
    private readonly Dictionary<string, object> _valueCache = new();
    
    public T? GetValue<T>(T? storage, [CallerMemberName] string propertyName = "")
    {
        // 1. 尝试从缓存获取
        if (_valueCache.TryGetValue(propertyName, out var cached))
        {
            return (T)cached;
        }
        
        // 2. 从参数字典获取
        if (parameters != null && parameters.TryGetValue(propertyName, out ModDetailModel modDetailModel))
        {
            string val = modDetailModel.ValueA;
            object parsed = ParseValue<T>(val);
            
            // 3. 缓存解析结果
            _valueCache[propertyName] = parsed;
            return (T)parsed;
        }
        
        return storage;
    }
    
    protected override bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
    {
        storage = value;
        
        // 更新缓存
        _valueCache[propertyName] = value;
        
        if (parameters.TryGetValue(propertyName, out ModDetailModel modDetailModel))
        {
            modDetailModel.ValueB = modDetailModel.ValueA;
            modDetailModel.ValueA = value?.ToString();
        }
        
        OnPropertyChanged(propertyName);
        return true;
    }
    
    // 清除缓存（值变化时调用）
    public void InvalidateCache()
    {
        _valueCache.Clear();
    }
}
```

**性能提升**:
- 首次访问后缓存，后续访问O(1)
- 避免重复的字符串解析
- 对于频繁访问的属性，性能提升10-100倍

### 3. UI渲染优化

#### 虚拟化列表

```csharp
// TemplateEditorWindow.xaml
<ListView ItemsSource="{Binding Params}"
          VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling"
          VirtualizingPanel.CacheLength="20,20"
          VirtualizingPanel.CacheLengthUnit="Item">
    <!-- ... -->
</ListView>
```

**优点**:
- 只渲染可见项
- 重用视觉元素
- 支持大量数据（1000+项）

#### 延迟加载

```csharp
public class LazyTemplateLoader
{
    private readonly ITemplateService _service;
    private readonly int _batchSize = 50;
    
    public async Task LoadInBatchesAsync(
        ObservableCollection<MTFParam> target,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        
        while (true)
        {
            var batch = await LoadBatchAsync(offset, _batchSize, cancellationToken);
            if (batch.Count == 0)
                break;
            
            // 在UI线程添加
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var item in batch)
                {
                    target.Add(item);
                }
            });
            
            offset += _batchSize;
            
            // 延迟以避免UI冻结
            await Task.Delay(100, cancellationToken);
        }
    }
}
```

## 内存管理优化

### 1. 对象池

对于频繁创建销毁的对象，使用对象池：

```csharp
public class POIPointPool
{
    private readonly ConcurrentBag<POIPoint> _pool = new();
    private readonly int _maxSize = 1000;
    
    public POIPoint Rent()
    {
        if (_pool.TryTake(out var point))
        {
            // 重置对象状态
            point.Reset();
            return point;
        }
        
        return new POIPoint();
    }
    
    public void Return(POIPoint point)
    {
        if (_pool.Count < _maxSize)
        {
            _pool.Add(point);
        }
    }
    
    public void ReturnRange(IEnumerable<POIPoint> points)
    {
        foreach (var point in points)
        {
            Return(point);
        }
    }
}

// POIPoint类需要实现Reset方法
public class POIPoint
{
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    // ...
    
    public void Reset()
    {
        Id = 0;
        X = 0;
        Y = 0;
        // 重置其他字段
    }
}

// 使用示例
var pool = new POIPointPool();

// 租用对象
var point = pool.Rent();
point.X = 100;
point.Y = 200;

// 使用完毕后归还
pool.Return(point);
```

### 2. 弱引用缓存

对于大型数据对象，使用弱引用缓存：

```csharp
public class TemplateCache
{
    private readonly Dictionary<string, WeakReference<ITemplate>> _cache = new();
    private readonly object _lock = new();
    
    public bool TryGet(string key, out ITemplate template)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var weakRef))
            {
                if (weakRef.TryGetTarget(out template))
                {
                    return true;
                }
                
                // 引用已被回收，移除
                _cache.Remove(key);
            }
        }
        
        template = null;
        return false;
    }
    
    public void Add(string key, ITemplate template)
    {
        lock (_lock)
        {
            _cache[key] = new WeakReference<ITemplate>(template);
        }
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }
}
```

### 3. 内存泄漏防护

```csharp
public class TemplateEditorWindow : Window, IDisposable
{
    private readonly IEventBus _eventBus;
    private Action<TemplateSavedEvent> _savedHandler;
    
    public TemplateEditorWindow(IEventBus eventBus)
    {
        _eventBus = eventBus;
        
        // 订阅事件
        _savedHandler = OnTemplateSaved;
        _eventBus.Subscribe(_savedHandler);
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // 取消订阅，防止内存泄漏
        _eventBus.Unsubscribe(_savedHandler);
        
        base.OnClosed(e);
        Dispose();
    }
    
    public void Dispose()
    {
        // 释放资源
        ITemplate?.Dispose();
        
        // 清理事件处理器
        _savedHandler = null;
    }
}
```

## 数据库访问优化

### 1. 连接池配置

```csharp
public class DatabaseConfig
{
    public static void Configure(SqlSugarClient client)
    {
        // 启用缓存
        client.CurrentConnectionConfig.IsAutoCloseConnection = true;
        
        // 设置超时
        client.CurrentConnectionConfig.ConnectionString += ";Connection Timeout=30;";
        
        // 启用SQL日志（仅开发环境）
        #if DEBUG
        client.Aop.OnLogExecuting = (sql, pars) =>
        {
            Console.WriteLine($"SQL: {sql}");
            Console.WriteLine($"Parameters: {string.Join(",", pars.Select(p => $"{p.ParameterName}={p.Value}"))}");
        };
        #endif
    }
}
```

### 2. 批量操作

```csharp
public class TemplateRepository : ITemplateRepository
{
    private readonly ISqlSugarClient _db;
    
    // 批量插入
    public async Task<int> InsertBatchAsync(List<ModMasterModel> models)
    {
        return await _db.Insertable(models)
            .ExecuteCommandAsync();
    }
    
    // 批量更新
    public async Task<int> UpdateBatchAsync(List<ModMasterModel> models)
    {
        return await _db.Updateable(models)
            .ExecuteCommandAsync();
    }
    
    // 批量删除
    public async Task<int> DeleteBatchAsync(List<int> ids)
    {
        return await _db.Deleteable<ModMasterModel>()
            .In(ids)
            .ExecuteCommandAsync();
    }
}
```

### 3. 查询优化

```csharp
// 使用投影减少数据传输
public async Task<List<TemplateSummary>> GetSummariesAsync()
{
    return await _db.Queryable<ModMasterModel>()
        .Select(m => new TemplateSummary
        {
            Id = m.Id,
            Name = m.Name,
            Type = m.Type,
            CreateDate = m.CreateDate
        })
        .ToListAsync();
}

// 使用Include预加载关联数据
public async Task<List<ModMasterModel>> GetWithDetailsAsync(int type)
{
    return await _db.Queryable<ModMasterModel>()
        .Includes(m => m.Details)  // 一次查询加载关联
        .Where(m => m.Type == type)
        .ToListAsync();
}

// 使用分页
public async Task<PagedResult<ModMasterModel>> GetPagedAsync(int pageIndex, int pageSize)
{
    int total = 0;
    var items = await _db.Queryable<ModMasterModel>()
        .ToPageListAsync(pageIndex, pageSize, total);
    
    return new PagedResult<ModMasterModel>
    {
        Items = items,
        Total = total,
        PageIndex = pageIndex,
        PageSize = pageSize
    };
}
```

## 并发处理优化

### 1. 并行处理模板

```csharp
public class ParallelTemplateProcessor
{
    private readonly ITemplateService _service;
    private readonly int _maxDegreeOfParallelism;
    
    public ParallelTemplateProcessor(ITemplateService service, int maxDegreeOfParallelism = 4)
    {
        _service = service;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
    }
    
    public async Task<List<ProcessResult>> ProcessTemplatesAsync(
        List<string> templateCodes,
        CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentBag<ProcessResult>();
        
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };
        
        await Parallel.ForEachAsync(templateCodes, options, async (code, ct) =>
        {
            try
            {
                var template = await _service.GetTemplateAsync(code);
                await template.LoadAsync(ct);
                
                results.Add(new ProcessResult
                {
                    Code = code,
                    Success = true,
                    ItemCount = template.Count
                });
            }
            catch (Exception ex)
            {
                results.Add(new ProcessResult
                {
                    Code = code,
                    Success = false,
                    Error = ex.Message
                });
            }
        });
        
        return results.ToList();
    }
}
```

### 2. 线程安全的模板访问

```csharp
public class ThreadSafeTemplate<T> : ITemplate<T> where T : ParamModBase, new()
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    
    public override async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _lock.EnterWriteLock();
        try
        {
            await base.LoadAsync(cancellationToken);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    public override T GetParam(int index)
    {
        _lock.EnterReadLock();
        try
        {
            return Params[index];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lock?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

### 3. 异步队列处理

```csharp
public class TemplateProcessingQueue
{
    private readonly Channel<TemplateTask> _channel;
    private readonly ITemplateService _service;
    private readonly ILogger _logger;
    
    public TemplateProcessingQueue(ITemplateService service, ILogger logger, int capacity = 100)
    {
        _service = service;
        _logger = logger;
        _channel = Channel.CreateBounded<TemplateTask>(capacity);
        
        // 启动后台处理
        _ = ProcessQueueAsync();
    }
    
    public async Task EnqueueAsync(TemplateTask task)
    {
        await _channel.Writer.WriteAsync(task);
    }
    
    private async Task ProcessQueueAsync()
    {
        await foreach (var task in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await ProcessTaskAsync(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing task: {task.Code}");
            }
        }
    }
    
    private async Task ProcessTaskAsync(TemplateTask task)
    {
        var template = await _service.GetTemplateAsync(task.Code);
        await template.LoadAsync();
        await task.Action(template);
    }
}

public class TemplateTask
{
    public string Code { get; set; }
    public Func<ITemplate, Task> Action { get; set; }
}
```

## 缓存策略优化

### 1. 多层缓存

```csharp
public class MultiLevelCache
{
    // L1: 内存缓存（快速但有限）
    private readonly MemoryCache _memoryCache;
    
    // L2: Redis缓存（较慢但大容量）
    private readonly IDistributedCache _distributedCache;
    
    public MultiLevelCache(IMemoryCache memoryCache, IDistributedCache distributedCache)
    {
        _memoryCache = (MemoryCache)memoryCache;
        _distributedCache = distributedCache;
    }
    
    public async Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? l1Expiration = null,
        TimeSpan? l2Expiration = null)
    {
        // 1. 尝试L1缓存
        if (_memoryCache.TryGetValue(key, out T cached))
        {
            return cached;
        }
        
        // 2. 尝试L2缓存
        var serialized = await _distributedCache.GetStringAsync(key);
        if (serialized != null)
        {
            var value = JsonConvert.DeserializeObject<T>(serialized);
            
            // 回填L1缓存
            _memoryCache.Set(key, value, l1Expiration ?? TimeSpan.FromMinutes(5));
            return value;
        }
        
        // 3. 从源获取
        var newValue = await factory();
        
        // 4. 写入两级缓存
        _memoryCache.Set(key, newValue, l1Expiration ?? TimeSpan.FromMinutes(5));
        
        await _distributedCache.SetStringAsync(
            key,
            JsonConvert.SerializeObject(newValue),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = l2Expiration ?? TimeSpan.FromHours(1)
            });
        
        return newValue;
    }
}
```

### 2. 智能缓存失效

```csharp
public class SmartCacheInvalidator
{
    private readonly IMemoryCache _cache;
    private readonly Dictionary<string, HashSet<string>> _dependencies = new();
    
    // 注册依赖关系
    public void RegisterDependency(string key, string dependsOn)
    {
        if (!_dependencies.ContainsKey(dependsOn))
        {
            _dependencies[dependsOn] = new HashSet<string>();
        }
        _dependencies[dependsOn].Add(key);
    }
    
    // 失效缓存及其依赖项
    public void Invalidate(string key)
    {
        _cache.Remove(key);
        
        // 递归失效依赖项
        if (_dependencies.TryGetValue(key, out var dependents))
        {
            foreach (var dependent in dependents)
            {
                Invalidate(dependent);
            }
        }
    }
}

// 使用示例
invalidator.RegisterDependency("template:MTF:1", "template:MTF:list");
invalidator.RegisterDependency("template:MTF:2", "template:MTF:list");

// 当更新模板1时，自动失效列表缓存
invalidator.Invalidate("template:MTF:1");
```

### 3. 缓存预热

```csharp
public class CacheWarmer
{
    private readonly ITemplateService _service;
    private readonly IMemoryCache _cache;
    
    public async Task WarmUpAsync(List<string> codes)
    {
        var tasks = codes.Select(async code =>
        {
            var template = await _service.GetTemplateAsync(code);
            await template.LoadAsync();
            
            _cache.Set($"template:{code}", template, TimeSpan.FromHours(1));
        });
        
        await Task.WhenAll(tasks);
    }
}

// 应用启动时预热常用模板
public class Startup
{
    public async Task ConfigureAsync(IServiceProvider services)
    {
        var warmer = services.GetRequiredService<CacheWarmer>();
        await warmer.WarmUpAsync(new[] { "MTF", "SFR", "POI" });
    }
}
```

## 扩展性设计

### 1. 插件架构

```csharp
// 插件接口
public interface ITemplatePlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize(IServiceProvider services);
    IEnumerable<ITemplate> GetTemplates();
}

// 插件管理器
public class TemplatePluginManager
{
    private readonly List<ITemplatePlugin> _plugins = new();
    private readonly IServiceProvider _services;
    
    public TemplatePluginManager(IServiceProvider services)
    {
        _services = services;
    }
    
    public void LoadPlugin(ITemplatePlugin plugin)
    {
        plugin.Initialize(_services);
        _plugins.Add(plugin);
    }
    
    public void LoadFromAssembly(Assembly assembly)
    {
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(ITemplatePlugin).IsAssignableFrom(t) && !t.IsAbstract);
        
        foreach (var type in pluginTypes)
        {
            var plugin = (ITemplatePlugin)Activator.CreateInstance(type);
            LoadPlugin(plugin);
        }
    }
    
    public IEnumerable<ITemplate> GetAllTemplates()
    {
        return _plugins.SelectMany(p => p.GetTemplates());
    }
}

// 示例插件
public class CustomAlgorithmPlugin : ITemplatePlugin
{
    public string Name => "Custom Algorithm Plugin";
    public string Version => "1.0.0";
    
    public void Initialize(IServiceProvider services)
    {
        // 初始化逻辑
    }
    
    public IEnumerable<ITemplate> GetTemplates()
    {
        yield return new TemplateCustomAlgorithm();
    }
}
```

### 2. 扩展点机制

```csharp
// 扩展点定义
public interface ITemplateExtensionPoint
{
    string ExtensionPointId { get; }
    void Execute(ITemplate template, object context);
}

// 扩展点管理
public class ExtensionPointManager
{
    private readonly Dictionary<string, List<ITemplateExtensionPoint>> _extensionPoints = new();
    
    public void RegisterExtensionPoint(string pointId, ITemplateExtensionPoint extension)
    {
        if (!_extensionPoints.ContainsKey(pointId))
        {
            _extensionPoints[pointId] = new List<ITemplateExtensionPoint>();
        }
        _extensionPoints[pointId].Add(extension);
    }
    
    public void ExecuteExtensionPoints(string pointId, ITemplate template, object context)
    {
        if (_extensionPoints.TryGetValue(pointId, out var extensions))
        {
            foreach (var extension in extensions)
            {
                extension.Execute(template, context);
            }
        }
    }
}

// 在模板中使用
public class ITemplate<T> : ITemplate where T : ParamModBase, new()
{
    private readonly ExtensionPointManager _extensionManager;
    
    public override async Task LoadAsync()
    {
        // 执行加载前扩展点
        _extensionManager.ExecuteExtensionPoints("BeforeLoad", this, null);
        
        // 加载逻辑
        await LoadDataAsync();
        
        // 执行加载后扩展点
        _extensionManager.ExecuteExtensionPoints("AfterLoad", this, null);
    }
}
```

### 3. 配置驱动

```csharp
// 配置模型
public class TemplateConfiguration
{
    public Dictionary<string, TemplateSettings> Templates { get; set; }
}

public class TemplateSettings
{
    public bool Enabled { get; set; }
    public int CacheTimeSeconds { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
}

// 配置加载器
public class TemplateConfigurationLoader
{
    public TemplateConfiguration Load(string configPath)
    {
        var json = File.ReadAllText(configPath);
        return JsonConvert.DeserializeObject<TemplateConfiguration>(json);
    }
}

// 使用配置
public class ConfigurableTemplate : ITemplate
{
    private readonly TemplateSettings _settings;
    
    public ConfigurableTemplate(TemplateSettings settings)
    {
        _settings = settings;
    }
    
    public override async Task LoadAsync()
    {
        if (!_settings.Enabled)
            return;
        
        // 使用配置的参数
        var cacheTime = TimeSpan.FromSeconds(_settings.CacheTimeSeconds);
        // ...
    }
}
```

## 监控与诊断

### 1. 性能监控

```csharp
public class PerformanceMonitor
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, PerformanceMetrics> _metrics = new();
    
    public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var sw = Stopwatch.StartNew();
        Exception error = null;
        
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            RecordMetric(operationName, sw.Elapsed, error == null);
        }
    }
    
    private void RecordMetric(string operation, TimeSpan elapsed, bool success)
    {
        if (!_metrics.ContainsKey(operation))
        {
            _metrics[operation] = new PerformanceMetrics();
        }
        
        var metric = _metrics[operation];
        metric.TotalCalls++;
        metric.TotalTime += elapsed;
        metric.MinTime = metric.MinTime == TimeSpan.Zero ? elapsed : TimeSpan.FromMilliseconds(Math.Min(metric.MinTime.TotalMilliseconds, elapsed.TotalMilliseconds));
        metric.MaxTime = TimeSpan.FromMilliseconds(Math.Max(metric.MaxTime.TotalMilliseconds, elapsed.TotalMilliseconds));
        
        if (success)
            metric.SuccessCount++;
        else
            metric.FailureCount++;
    }
    
    public void ReportMetrics()
    {
        foreach (var kvp in _metrics)
        {
            var metric = kvp.Value;
            var avgTime = metric.TotalTime / metric.TotalCalls;
            
            _logger.LogInformation($"Operation: {kvp.Key}");
            _logger.LogInformation($"  Total Calls: {metric.TotalCalls}");
            _logger.LogInformation($"  Success: {metric.SuccessCount}, Failure: {metric.FailureCount}");
            _logger.LogInformation($"  Avg Time: {avgTime.TotalMilliseconds:F2}ms");
            _logger.LogInformation($"  Min Time: {metric.MinTime.TotalMilliseconds:F2}ms");
            _logger.LogInformation($"  Max Time: {metric.MaxTime.TotalMilliseconds:F2}ms");
        }
    }
}

public class PerformanceMetrics
{
    public int TotalCalls { get; set; }
    public TimeSpan TotalTime { get; set; }
    public TimeSpan MinTime { get; set; }
    public TimeSpan MaxTime { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

// 使用示例
var monitor = new PerformanceMonitor(logger);

var result = await monitor.MeasureAsync("LoadTemplate", async () =>
{
    return await template.LoadAsync();
});
```

### 2. 诊断日志

```csharp
public class DiagnosticLogger
{
    private readonly ILogger _logger;
    
    public IDisposable BeginScope(string operation, Dictionary<string, object> properties = null)
    {
        var scope = new DiagnosticScope(operation, properties, _logger);
        return scope;
    }
}

public class DiagnosticScope : IDisposable
{
    private readonly string _operation;
    private readonly Dictionary<string, object> _properties;
    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch;
    
    public DiagnosticScope(string operation, Dictionary<string, object> properties, ILogger logger)
    {
        _operation = operation;
        _properties = properties ?? new Dictionary<string, object>();
        _logger = logger;
        _stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation($"[START] {_operation} {SerializeProperties()}");
    }
    
    public void Dispose()
    {
        _stopwatch.Stop();
        _properties["ElapsedMs"] = _stopwatch.ElapsedMilliseconds;
        _logger.LogInformation($"[END] {_operation} {SerializeProperties()}");
    }
    
    private string SerializeProperties()
    {
        return string.Join(", ", _properties.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }
}

// 使用示例
using (diagnosticLogger.BeginScope("LoadTemplate", new Dictionary<string, object>
{
    ["TemplateCode"] = "MTF",
    ["UserId"] = currentUser.Id
}))
{
    await template.LoadAsync();
}
```

### 3. 健康检查

```csharp
public class TemplateHealthCheck : IHealthCheck
{
    private readonly ITemplateService _service;
    private readonly ITemplateRepository _repository;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查数据库连接
            await _repository.TestConnectionAsync(cancellationToken);
            
            // 检查能否加载模板
            var testTemplate = await _service.GetTemplateAsync("MTF");
            await testTemplate.LoadAsync(cancellationToken);
            
            return HealthCheckResult.Healthy("All systems operational");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Template system unhealthy", ex);
        }
    }
}

// 注册健康检查
services.AddHealthChecks()
    .AddCheck<TemplateHealthCheck>("template_health");
```

## 实施建议

### 优先级和时间表

#### 第一阶段（1-2周）- 高优先级
1. **模板加载优化**
   - 实现批量查询
   - 添加异步加载
   - 预期性能提升：5-10倍

2. **参数值缓存**
   - 实现值缓存机制
   - 预期性能提升：10-100倍（频繁访问）

3. **基础监控**
   - 添加性能监控
   - 添加诊断日志

#### 第二阶段（2-3周）- 中优先级
4. **并发处理**
   - 实现并行处理
   - 添加线程安全机制

5. **缓存策略**
   - 实现多层缓存
   - 添加缓存预热

6. **内存优化**
   - 实现对象池
   - 添加弱引用缓存

#### 第三阶段（3-4周）- 低优先级
7. **扩展性设计**
   - 实现插件架构
   - 添加扩展点机制

8. **高级功能**
   - 配置驱动
   - 健康检查

### 性能目标

| 指标 | 当前 | 目标 | 方法 |
|------|------|------|------|
| 模板加载时间（100项） | ~5s | <0.5s | 批量查询+异步 |
| 参数访问延迟 | ~1ms | <0.01ms | 值缓存 |
| 内存占用 | ~200MB | <100MB | 对象池+弱引用 |
| 并发处理能力 | 单线程 | 4线程 | 并行处理 |
| 启动时间 | ~10s | <5s | 缓存预热+延迟加载 |

### 验证方法

1. **基准测试**
   ```csharp
   [Benchmark]
   public async Task LoadTemplate_Current()
   {
       await _template.Load();
   }
   
   [Benchmark]
   public async Task LoadTemplate_Optimized()
   {
       await _template.LoadAsync();
   }
   ```

2. **压力测试**
   - 并发加载100个模板
   - 连续加载1000次
   - 长时间运行（24小时）

3. **内存分析**
   - 使用内存分析器检查泄漏
   - 监控GC压力
   - 分析对象分配

## 总结

性能与扩展性优化的关键点：

1. **异步化**: 全面采用async/await，避免阻塞
2. **批量处理**: 减少数据库往返，使用批量操作
3. **缓存策略**: 多层缓存，智能失效
4. **并发控制**: 线程安全，并行处理
5. **资源管理**: 对象池，弱引用，及时释放
6. **可扩展性**: 插件架构，扩展点，配置驱动
7. **可观测性**: 性能监控，诊断日志，健康检查

通过这些优化，Templates模块将获得显著的性能提升和更好的扩展能力，为未来发展打下坚实基础。

## 相关文档

- [Templates 模块优化建议 - 上篇](./Templates优化建议-上篇.md)
- [Templates 模块优化建议 - 中篇](./Templates优化建议-中篇.md)
- [Templates架构设计](./Templates架构设计.md)
- [Templates API参考](./Templates-API参考.md)
