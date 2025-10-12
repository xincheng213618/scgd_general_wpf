# Templates 模块优化建议 (中篇) - 模板实现优化

## 概述

本篇针对 Templates 模块中的具体模板实现提出优化建议，重点关注 ARVR、POI、图像处理等算法模板的代码组织、参数管理和算法结构优化。

**分析范围**: 各类算法模板实现（ARVR, POI, Jsons, 图像处理等）
**优先级**: 中高 - 这些优化将提升代码复用性和算法模块的独立性

## 优化项 1: 算法模板标准化 ⭐⭐⭐⭐⭐

### 现状问题

不同算法模板的实现方式不一致，缺乏统一标准：

```
ARVR/MTF/
├── MTFParam.cs              # 参数类
├── TemplateMTF.cs           # 模板类
├── AlgorithmMTF.cs          # 算法类
├── ViewHandleMTF.cs         # 视图处理
├── ViewResultMTF.cs         # 结果视图
├── AlgResultMTFDao.cs       # 结果DAO
└── DisplayMTF.xaml.cs       # 显示控件

ARVR/Ghost/
├── GhostParam.cs
├── TemplateGhost.cs
├── AlgorithmGhost.cs
├── ViewHandleGhost.cs
└── ... (文件命名和组织方式略有不同)
```

### 优化方案

**建议**: 定义统一的模板模块结构标准

```
{AlgorithmName}/
├── Models/                              # 数据模型
│   ├── {Alg}Param.cs                   # 参数模型
│   ├── {Alg}Result.cs                  # 结果模型
│   └── {Alg}Config.cs                  # 配置模型
│
├── Core/                                # 核心逻辑
│   ├── Template{Alg}.cs                # 模板类
│   ├── Algorithm{Alg}.cs               # 算法实现
│   └── {Alg}Processor.cs               # 处理器
│
├── Data/                                # 数据访问
│   ├── {Alg}ResultDao.cs               # 结果DAO
│   └── {Alg}Repository.cs              # 仓储
│
├── Views/                               # 视图
│   ├── Display{Alg}.xaml               # 显示控件
│   ├── ViewHandle{Alg}.cs              # 视图处理器
│   └── ViewResult{Alg}.cs              # 结果视图
│
└── README.md                            # 模块说明
```

#### 标准模板接口

```csharp
// 1. 算法模板标准接口
public interface IAlgorithmTemplate<TParam, TResult>
    where TParam : ParamModBase
    where TResult : class
{
    // 模板信息
    string AlgorithmName { get; }
    string Version { get; }
    
    // 参数管理
    TParam GetDefaultParams();
    Result ValidateParams(TParam param);
    
    // 算法执行
    Task<Result<TResult>> ExecuteAsync(TParam param, CancellationToken ct = default);
    
    // 结果处理
    Task<Result> SaveResultAsync(TResult result);
    void DisplayResult(TResult result);
}

// 2. 标准基类实现
public abstract class AlgorithmTemplateBase<TParam, TResult> 
    : ITemplate<TParam>, IAlgorithmTemplate<TParam, TResult>
    where TParam : ParamModBase, new()
    where TResult : class
{
    protected ILogger Logger { get; }
    protected ITemplateRepository Repository { get; }
    
    public abstract string AlgorithmName { get; }
    public virtual string Version => "1.0.0";
    
    protected AlgorithmTemplateBase(
        ILogger logger, 
        ITemplateRepository repository)
    {
        Logger = logger;
        Repository = repository;
    }
    
    public abstract TParam GetDefaultParams();
    
    public virtual Result ValidateParams(TParam param)
    {
        // 基础验证
        if (param == null)
            return Result.Fail("参数不能为空");
            
        // 子类可重写添加特定验证
        return ValidateSpecificParams(param);
    }
    
    protected virtual Result ValidateSpecificParams(TParam param)
    {
        return Result.Ok();
    }
    
    public abstract Task<Result<TResult>> ExecuteAsync(
        TParam param, 
        CancellationToken ct = default);
        
    public abstract Task<Result> SaveResultAsync(TResult result);
    
    public abstract void DisplayResult(TResult result);
}

// 3. MTF 示例实现
public class TemplateMTF : AlgorithmTemplateBase<MTFParam, MTFResult>
{
    public override string AlgorithmName => "MTF";
    public override string Version => "2.0.0";
    
    public TemplateMTF(ILogger<TemplateMTF> logger, ITemplateRepository repository)
        : base(logger, repository)
    {
        Title = "MTF 分析";
        Code = "MTF";
        TemplateDicId = 100;
    }
    
    public override MTFParam GetDefaultParams()
    {
        return new MTFParam
        {
            FrequencyThreshold = 0.5,
            SamplingRate = 100
        };
    }
    
    protected override Result ValidateSpecificParams(MTFParam param)
    {
        if (param.FrequencyThreshold <= 0 || param.FrequencyThreshold > 1)
            return Result.Fail("频率阈值必须在 0-1 之间");
            
        if (param.SamplingRate <= 0)
            return Result.Fail("采样率必须大于 0");
            
        return Result.Ok();
    }
    
    public override async Task<Result<MTFResult>> ExecuteAsync(
        MTFParam param, 
        CancellationToken ct = default)
    {
        try
        {
            Logger.LogInformation("开始执行 MTF 分析");
            
            var validation = ValidateParams(param);
            if (!validation.Success)
                return Result<MTFResult>.Fail(validation.Message);
            
            // 执行算法
            var result = await MTFAlgorithm.AnalyzeAsync(param, ct);
            
            Logger.LogInformation("MTF 分析完成");
            return Result<MTFResult>.Ok(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MTF 分析失败");
            return Result<MTFResult>.Fail(ex);
        }
    }
    
    public override async Task<Result> SaveResultAsync(MTFResult result)
    {
        // 保存结果到数据库
        return await Repository.SaveResultAsync(result);
    }
    
    public override void DisplayResult(MTFResult result)
    {
        var window = new DisplayMTF();
        window.DataContext = result;
        window.Show();
    }
}
```

### 优势

1. **统一标准**: 所有算法遵循相同模式
2. **清晰职责**: 参数、算法、视图分离
3. **易于维护**: 标准化降低学习成本
4. **可测试性**: 每个部分可独立测试

---

## 优化项 2: ARVR 模块版本统一 ⭐⭐⭐⭐

### 现状问题

存在两套 ARVR 实现：
- `ARVR/MTF/` - 旧版本
- `Jsons/MTF2/` - 新版本基于 JSON

导致：
- 代码重复
- 维护困难
- 用户困惑

### 优化方案

**建议**: 实现版本管理机制，统一两套实现

```csharp
// 1. 版本化模板接口
public interface IVersionedTemplate
{
    string Version { get; }
    IEnumerable<string> SupportedVersions { get; }
    ITemplate GetVersion(string version);
    ITemplate GetLatestVersion();
}

// 2. 版本管理器
public class TemplateVersionManager
{
    private readonly Dictionary<string, List<ITemplate>> _versions = new();
    
    public void Register(string algorithmName, string version, ITemplate template)
    {
        if (!_versions.ContainsKey(algorithmName))
            _versions[algorithmName] = new List<ITemplate>();
            
        _versions[algorithmName].Add(template);
    }
    
    public ITemplate? GetVersion(string algorithmName, string version)
    {
        if (!_versions.TryGetValue(algorithmName, out var versions))
            return null;
            
        return versions.FirstOrDefault(t => 
            t is IVersionedTemplate vt && vt.Version == version);
    }
    
    public ITemplate? GetLatest(string algorithmName)
    {
        if (!_versions.TryGetValue(algorithmName, out var versions))
            return null;
            
        return versions
            .OrderByDescending(t => (t as IVersionedTemplate)?.Version ?? "0")
            .FirstOrDefault();
    }
    
    public IEnumerable<ITemplate> GetAllVersions(string algorithmName)
    {
        if (!_versions.TryGetValue(algorithmName, out var versions))
            return Enumerable.Empty<ITemplate>();
            
        return versions;
    }
}

// 3. MTF 版本化实现
public class TemplateMTFVersioned : IVersionedTemplate
{
    private readonly TemplateMTF _v1;
    private readonly TemplateMTF2 _v2;
    
    public string Version => "2.0";
    
    public IEnumerable<string> SupportedVersions => new[] { "1.0", "2.0" };
    
    public TemplateMTFVersioned()
    {
        _v1 = new TemplateMTF();  // 基于传统模型
        _v2 = new TemplateMTF2(); // 基于 JSON
    }
    
    public ITemplate GetVersion(string version)
    {
        return version switch
        {
            "1.0" => _v1,
            "2.0" => _v2,
            _ => _v2 // 默认最新版
        };
    }
    
    public ITemplate GetLatestVersion() => _v2;
}

// 4. 版本迁移工具
public class TemplateVersionMigrator
{
    public async Task<Result> MigrateAsync(
        ITemplate source, 
        ITemplate target, 
        int sourceIndex)
    {
        try
        {
            // 导出旧版本
            var tempFile = Path.GetTempFileName();
            source.Export(sourceIndex);
            
            // 转换格式
            var converted = await ConvertAsync(tempFile, 
                source.GetTemplateType, 
                target.GetTemplateType);
            
            // 导入新版本
            var result = target.ImportFile(converted);
            
            return result 
                ? Result.Ok() 
                : Result.Fail("导入失败");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }
    
    private async Task<string> ConvertAsync(
        string sourceFile, 
        Type sourceType, 
        Type targetType)
    {
        // 版本转换逻辑
        // ...
    }
}
```

### 迁移策略

1. **阶段 1**: 保留两套实现，添加版本标识
2. **阶段 2**: 提供迁移工具，鼓励用户升级
3. **阶段 3**: 标记旧版本为 Deprecated
4. **阶段 4**: 移除旧版本（下个主版本）

---

## 优化项 3: POI 模块重组 ⭐⭐⭐⭐

### 现状问题

POI 模块包含 6 个子模块，但职责划分不够清晰：

```
POI/
├── BuildPoi/        # POI 构建
├── POIFilters/      # POI 滤波
├── POIGenCali/      # POI 生成校准
├── POIRevise/       # POI 修正
├── POIOutput/       # POI 输出
└── AlgorithmImp/    # 算法实现
```

### 优化方案

**建议**: 按照 POI 处理管道重新组织

```
POI/
├── Core/                                # 核心定义
│   ├── IPOITemplate.cs                 # POI 模板接口
│   ├── POIPoint.cs                     # POI 点模型
│   └── POICollection.cs                # POI 集合
│
├── Generation/                          # POI 生成
│   ├── Builder/                        # 构建器 (原 BuildPoi)
│   │   ├── AutoBuilder.cs
│   │   ├── ManualBuilder.cs
│   │   └── TemplateBuilder.cs
│   ├── Generator/                      # 生成器 (原 POIGenCali)
│   │   ├── GridGenerator.cs
│   │   └── PatternGenerator.cs
│   └── TemplatePOIGeneration.cs        # 生成模板
│
├── Processing/                          # POI 处理
│   ├── Filters/                        # 滤波器 (原 POIFilters)
│   │   ├── NoiseFilter.cs
│   │   ├── OutlierFilter.cs
│   │   └── SmoothFilter.cs
│   ├── Calibration/                    # 校准 (从 POIGenCali 拆分)
│   │   ├── Calibrator.cs
│   │   └── CalibrationModel.cs
│   ├── Revision/                       # 修正 (原 POIRevise)
│   │   ├── PositionReviser.cs
│   │   └── ValueReviser.cs
│   └── TemplatePOIProcessing.cs        # 处理模板
│
├── Output/                              # POI 输出 (原 POIOutput)
│   ├── Exporters/
│   │   ├── CSVExporter.cs
│   │   ├── XMLExporter.cs
│   │   └── JSONExporter.cs
│   ├── Formatters/
│   │   └── POIFormatter.cs
│   └── TemplatePOIOutput.cs            # 输出模板
│
├── Algorithms/                          # 算法实现 (原 AlgorithmImp)
│   ├── Detection/
│   ├── Analysis/
│   └── Matching/
│
└── Views/                               # 视图
    ├── POIEditor.xaml
    └── POIViewer.xaml
```

#### 管道式处理接口

```csharp
// 1. POI 处理管道
public interface IPOIPipeline
{
    IPOIPipeline AddStep(IPOIProcessor processor);
    Task<Result<POICollection>> ProcessAsync(POICollection input);
}

// 2. POI 处理器接口
public interface IPOIProcessor
{
    string Name { get; }
    Task<Result<POICollection>> ProcessAsync(POICollection input, CancellationToken ct);
}

// 3. 具体处理器实现
public class POINoiseFilter : IPOIProcessor
{
    public string Name => "噪声滤波";
    
    public async Task<Result<POICollection>> ProcessAsync(
        POICollection input, 
        CancellationToken ct)
    {
        // 滤波逻辑
        var filtered = await FilterNoiseAsync(input, ct);
        return Result<POICollection>.Ok(filtered);
    }
}

// 4. 管道使用示例
public class POITemplate
{
    public async Task<Result<POICollection>> ProcessPOIAsync(POICollection input)
    {
        var pipeline = new POIPipeline()
            .AddStep(new POINoiseFilter())
            .AddStep(new POIOutlierFilter())
            .AddStep(new POIPositionReviser())
            .AddStep(new POICalibrator());
            
        return await pipeline.ProcessAsync(input);
    }
}
```

### 优势

1. **清晰的管道**: POI 处理流程一目了然
2. **可组合**: 可以灵活组合不同处理器
3. **可复用**: 处理器可在不同场景复用
4. **易测试**: 每个处理器可独立测试

---

## 优化项 4: JSON 模板统一框架 ⭐⭐⭐⭐

### 现状问题

Jsons 目录下有 16 个子模块，每个都有相似的结构，但实现各不相同。

### 优化方案

**建议**: 建立统一的 JSON 模板框架

```csharp
// 1. JSON 模板基类
public abstract class JsonTemplateBase<TConfig, TParam, TResult> : ITemplate<TParam>
    where TConfig : class
    where TParam : ParamModBase, new()
    where TResult : class
{
    protected string JsonSchemaPath { get; set; }
    protected IJsonValidator Validator { get; }
    
    protected JsonTemplateBase(IJsonValidator validator)
    {
        Validator = validator;
    }
    
    // 从 JSON 配置创建参数
    protected abstract TParam CreateParamFromJson(TConfig config);
    
    // 从参数生成 JSON 配置
    protected abstract TConfig CreateJsonFromParam(TParam param);
    
    // 验证 JSON 配置
    protected virtual Result ValidateJson(string json)
    {
        if (string.IsNullOrEmpty(JsonSchemaPath))
            return Result.Ok();
            
        return Validator.Validate(json, JsonSchemaPath);
    }
    
    // 加载 JSON 配置
    public override bool ImportFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var validation = ValidateJson(json);
            
            if (!validation.Success)
            {
                MessageBox.Show($"JSON 验证失败: {validation.Message}");
                return false;
            }
            
            var config = JsonConvert.DeserializeObject<TConfig>(json);
            var param = CreateParamFromJson(config);
            
            // 添加到模板列表
            var name = Path.GetFileNameWithoutExtension(filePath);
            TemplateParams.Add(new TemplateModel<TParam>(name, param));
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导入 JSON 失败");
            return false;
        }
    }
    
    // 导出 JSON 配置
    public override void Export(int index)
    {
        try
        {
            var param = TemplateParams[index].Value;
            var config = CreateJsonFromParam(param);
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            
            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files|*.json",
                FileName = TemplateParams[index].Key
            };
            
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, json);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导出 JSON 失败");
        }
    }
}

// 2. JSON Schema 验证器
public interface IJsonValidator
{
    Result Validate(string json, string schemaPath);
}

public class JsonSchemaValidator : IJsonValidator
{
    public Result Validate(string json, string schemaPath)
    {
        try
        {
            var schema = JSchema.Parse(File.ReadAllText(schemaPath));
            var obj = JObject.Parse(json);
            
            if (obj.IsValid(schema, out IList<string> errors))
            {
                return Result.Ok();
            }
            else
            {
                var message = string.Join("\n", errors);
                return Result.Fail(message);
            }
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }
}

// 3. MTF2 JSON 模板实现
public class TemplateMTF2 : JsonTemplateBase<MTF2Config, MTFParam, MTFResult>
{
    public TemplateMTF2(IJsonValidator validator) : base(validator)
    {
        Title = "MTF 分析 v2";
        Code = "MTF2";
        JsonSchemaPath = "Schemas/MTF2.schema.json";
    }
    
    protected override MTFParam CreateParamFromJson(MTF2Config config)
    {
        return new MTFParam
        {
            FrequencyThreshold = config.Frequency.Threshold,
            SamplingRate = config.Frequency.SamplingRate,
            // ... 映射其他参数
        };
    }
    
    protected override MTF2Config CreateJsonFromParam(MTFParam param)
    {
        return new MTF2Config
        {
            Frequency = new FrequencyConfig
            {
                Threshold = param.FrequencyThreshold,
                SamplingRate = param.SamplingRate
            }
            // ... 映射其他参数
        };
    }
}
```

### 迁移路径

```csharp
// 统一的 MTF 模板（支持两个版本）
public class TemplateMTF : IVersionedTemplate
{
    private readonly TemplateMTFV1 _v1;
    private readonly TemplateMTF2 _v2;
    
    public string Version => "2.0";
    public IEnumerable<string> SupportedVersions => new[] { "1.0", "2.0" };
    
    public ITemplate GetVersion(string version)
    {
        return version switch
        {
            "1.0" => _v1,
            "2.0" or _ => _v2
        };
    }
    
    public ITemplate GetLatestVersion() => _v2;
}
```

---

## 优化项 5: 参数验证框架 ⭐⭐⭐⭐

### 现状问题

当前缺少统一的参数验证机制，每个模板自行实现验证逻辑。

### 优化方案

**建议**: 使用注解和验证框架

```csharp
// 1. 验证特性
[AttributeUsage(AttributeTargets.Property)]
public class TemplateValidationAttribute : Attribute
{
    public virtual Result Validate(object value)
    {
        return Result.Ok();
    }
}

public class RangeValidationAttribute : TemplateValidationAttribute
{
    public double Min { get; set; }
    public double Max { get; set; }
    
    public RangeValidationAttribute(double min, double max)
    {
        Min = min;
        Max = max;
    }
    
    public override Result Validate(object value)
    {
        if (value is double d)
        {
            if (d < Min || d > Max)
                return Result.Fail($"值必须在 {Min} 到 {Max} 之间");
        }
        return Result.Ok();
    }
}

public class RequiredAttribute : TemplateValidationAttribute
{
    public override Result Validate(object value)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return Result.Fail("该参数为必填项");
        return Result.Ok();
    }
}

// 2. 参数类使用验证
public class MTFParam : ParamModBase
{
    [Required]
    [RangeValidation(0.0, 1.0)]
    [Description("频率阈值")]
    public double FrequencyThreshold { get; set; }
    
    [Required]
    [RangeValidation(1, 1000)]
    [Description("采样率")]
    public int SamplingRate { get; set; }
    
    [Required]
    [Description("处理模式")]
    public ProcessMode Mode { get; set; }
}

// 3. 验证器
public class ParameterValidator
{
    public Result Validate<T>(T param) where T : ParamModBase
    {
        var errors = new List<string>();
        var type = typeof(T);
        
        foreach (var property in type.GetProperties())
        {
            var value = property.GetValue(param);
            var attributes = property.GetCustomAttributes<TemplateValidationAttribute>();
            
            foreach (var attr in attributes)
            {
                var result = attr.Validate(value);
                if (!result.Success)
                {
                    var desc = property.GetCustomAttribute<DescriptionAttribute>();
                    var name = desc?.Description ?? property.Name;
                    errors.Add($"{name}: {result.Message}");
                }
            }
        }
        
        if (errors.Any())
        {
            return Result.Fail(string.Join("\n", errors));
        }
        
        return Result.Ok();
    }
}

// 4. 在模板中使用
public class TemplateMTF : ITemplate<MTFParam>
{
    private readonly ParameterValidator _validator;
    
    public async Task<Result> ExecuteAsync(int index)
    {
        var param = TemplateParams[index].Value;
        
        // 验证参数
        var validation = _validator.Validate(param);
        if (!validation.Success)
        {
            MessageBox.Show($"参数验证失败:\n{validation.Message}");
            return validation;
        }
        
        // 执行算法
        return await ExecuteAlgorithmAsync(param);
    }
}
```

### 优势

1. **声明式验证**: 参数验证逻辑在属性上声明
2. **可复用**: 验证特性可在不同参数类复用
3. **集中管理**: 验证逻辑统一管理
4. **自动化**: 验证器自动处理所有特性

---

## 优化项 6: 模板命名和组织优化 ⭐⭐⭐

### 现状问题

模板文件和类命名不统一：
- 有的用 `TemplateMTF`
- 有的用 `TemplateDistortionParam`
- 有的用 `TemplateSFR`

### 优化方案

**建议**: 统一命名规范

```csharp
// 命名规范
// 1. 参数类: {Algorithm}Param
// 2. 模板类: Template{Algorithm}
// 3. 算法类: {Algorithm}Algorithm
// 4. 结果类: {Algorithm}Result
// 5. DAO类: {Algorithm}ResultDao
// 6. 视图类: {Algorithm}View

// 示例：MTF 模块
namespace ColorVision.Engine.Templates.ARVR.MTF
{
    // 参数
    public class MTFParam : ParamModBase { }
    
    // 模板
    public class TemplateMTF : AlgorithmTemplateBase<MTFParam, MTFResult> { }
    
    // 算法
    public class MTFAlgorithm : IAlgorithm<MTFParam, MTFResult> { }
    
    // 结果
    public class MTFResult { }
    
    // DAO
    public class MTFResultDao : BaseDao<MTFResult> { }
    
    // 视图
    public class MTFView : UserControl { }
}
```

#### 重组建议

```bash
# 重构脚本示例
# 将 TemplateDistortionParam -> TemplateDistortion
# 将 AlgorithmDistortion -> DistortionAlgorithm
# 将 AlgResultDistortionDao -> DistortionResultDao
```

---

## 优化项 7: 算法和模板分离 ⭐⭐⭐⭐⭐

### 现状问题

算法实现和模板管理混在一起，不利于算法的独立测试和复用。

### 优化方案

**建议**: 将算法提取到独立的 Algorithms 命名空间

```csharp
// 1. 算法接口定义
namespace ColorVision.Engine.Algorithms
{
    public interface IAlgorithm<TInput, TOutput>
    {
        string Name { get; }
        string Version { get; }
        Task<Result<TOutput>> ExecuteAsync(TInput input, CancellationToken ct);
    }
    
    public abstract class AlgorithmBase<TInput, TOutput> 
        : IAlgorithm<TInput, TOutput>
    {
        protected ILogger Logger { get; }
        
        public abstract string Name { get; }
        public virtual string Version => "1.0.0";
        
        protected AlgorithmBase(ILogger logger)
        {
            Logger = logger;
        }
        
        public async Task<Result<TOutput>> ExecuteAsync(
            TInput input, 
            CancellationToken ct)
        {
            try
            {
                Logger.LogInformation($"开始执行 {Name} 算法");
                
                var validation = Validate(input);
                if (!validation.Success)
                    return Result<TOutput>.Fail(validation.Message);
                
                var output = await ExecuteCoreAsync(input, ct);
                
                Logger.LogInformation($"{Name} 算法执行完成");
                return Result<TOutput>.Ok(output);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"{Name} 算法执行失败");
                return Result<TOutput>.Fail(ex);
            }
        }
        
        protected abstract Result Validate(TInput input);
        protected abstract Task<TOutput> ExecuteCoreAsync(
            TInput input, 
            CancellationToken ct);
    }
}

// 2. MTF 算法实现
namespace ColorVision.Engine.Algorithms.ARVR
{
    public class MTFAlgorithm : AlgorithmBase<MTFInput, MTFOutput>
    {
        private readonly IImageProcessor _imageProcessor;
        
        public override string Name => "MTF";
        
        public MTFAlgorithm(
            ILogger<MTFAlgorithm> logger, 
            IImageProcessor imageProcessor)
            : base(logger)
        {
            _imageProcessor = imageProcessor;
        }
        
        protected override Result Validate(MTFInput input)
        {
            if (input.Image == null)
                return Result.Fail("输入图像不能为空");
            return Result.Ok();
        }
        
        protected override async Task<MTFOutput> ExecuteCoreAsync(
            MTFInput input, 
            CancellationToken ct)
        {
            // 纯粹的算法逻辑，不涉及模板、UI、数据库
            var processed = await _imageProcessor.ProcessAsync(input.Image);
            var mtf = CalculateMTF(processed, input.Threshold);
            
            return new MTFOutput
            {
                MTFValues = mtf,
                Timestamp = DateTime.Now
            };
        }
        
        private double[] CalculateMTF(Mat image, double threshold)
        {
            // MTF 计算逻辑
            // ...
        }
    }
}

// 3. 模板使用算法
namespace ColorVision.Engine.Templates.ARVR.MTF
{
    public class TemplateMTF : AlgorithmTemplateBase<MTFParam, MTFResult>
    {
        private readonly MTFAlgorithm _algorithm;
        
        public TemplateMTF(MTFAlgorithm algorithm, ITemplateRepository repository)
            : base(repository)
        {
            _algorithm = algorithm;
            Title = "MTF 分析";
            Code = "MTF";
        }
        
        public override async Task<Result<MTFResult>> ExecuteAsync(
            MTFParam param, 
            CancellationToken ct)
        {
            // 将模板参数转换为算法输入
            var input = new MTFInput
            {
                Image = param.Image,
                Threshold = param.FrequencyThreshold
            };
            
            // 执行算法
            var result = await _algorithm.ExecuteAsync(input, ct);
            
            if (!result.Success)
                return Result<MTFResult>.Fail(result.Message);
            
            // 将算法输出转换为模板结果
            var mtfResult = new MTFResult
            {
                MTFValues = result.Value.MTFValues,
                Timestamp = result.Value.Timestamp,
                ParamId = param.Id
            };
            
            return Result<MTFResult>.Ok(mtfResult);
        }
    }
}
```

### 项目结构调整

```
ColorVision.Engine/
├── Algorithms/                          # 纯算法实现
│   ├── Core/
│   │   ├── IAlgorithm.cs
│   │   └── AlgorithmBase.cs
│   ├── ARVR/
│   │   ├── MTFAlgorithm.cs
│   │   ├── SFRAlgorithm.cs
│   │   ├── FOVAlgorithm.cs
│   │   └── ...
│   ├── ImageProcessing/
│   └── Analysis/
│
└── Templates/                           # 模板管理
    ├── Core/
    ├── ARVR/
    │   └── MTF/
    │       ├── MTFParam.cs             # 参数类
    │       ├── TemplateMTF.cs          # 模板类（使用算法）
    │       └── MTFResult.cs            # 结果类
    └── ...
```

### 优势

1. **算法独立**: 算法可独立于模板系统使用
2. **易于测试**: 算法测试不需要模板环境
3. **可复用**: 算法可在其他项目中复用
4. **清晰职责**: 算法负责计算，模板负责管理

---

## 优化项 8: 异步操作全面支持 ⭐⭐⭐⭐

### 现状问题

当前大部分操作是同步的，在处理大量数据时会阻塞 UI：

```csharp
public virtual void Load() { }
public virtual void Save() { }
public virtual void Create(string templateName) { }
```

### 优化方案

**建议**: 将所有 I/O 操作改为异步

```csharp
// 1. 异步接口
public interface ITemplateAsync
{
    Task<Result> LoadAsync(CancellationToken ct = default);
    Task<Result> SaveAsync(CancellationToken ct = default);
    Task<Result> CreateAsync(string templateName, CancellationToken ct = default);
    Task<Result> DeleteAsync(int index, CancellationToken ct = default);
    Task<Result> ExportAsync(int index, string path, CancellationToken ct = default);
    Task<Result> ImportAsync(string path, CancellationToken ct = default);
}

// 2. 异步模板基类
public abstract class AsyncTemplateBase<TParam> : ITemplate<TParam>, ITemplateAsync
    where TParam : ParamModBase, new()
{
    public async Task<Result> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var masters = await Db.Queryable<ModMasterModel>()
                .Where(a => a.Pid == TemplateDicId && !a.IsDelete)
                .ToListAsync(ct);
                
            foreach (var master in masters)
            {
                if (ct.IsCancellationRequested)
                    return Result.Fail("操作已取消");
                    
                var details = await Db.Queryable<ModDetailModel>()
                    .Where(a => a.Pid == master.Id)
                    .ToListAsync(ct);
                    
                var param = CreateParam(master, details);
                TemplateParams.Add(new TemplateModel<TParam>(master.Name, param));
            }
            
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("操作已取消");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }
    
    public async Task<Result> SaveAsync(CancellationToken ct = default)
    {
        try
        {
            foreach (var index in SaveIndex)
            {
                if (ct.IsCancellationRequested)
                    return Result.Fail("操作已取消");
                    
                var item = TemplateParams[index];
                
                await Db.Updateable(item.Value.ModMaster)
                    .ExecuteCommandAsync(ct);
                    
                var details = new List<ModDetailModel>();
                item.Value.GetDetail(details);
                
                await Db.Updateable(details)
                    .ExecuteCommandAsync(ct);
            }
            
            SaveIndex.Clear();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }
}

// 3. UI 使用异步操作
public class TemplateEditorViewModel
{
    private CancellationTokenSource _cts;
    
    public async Task SaveAsync()
    {
        IsBusy = true;
        _cts = new CancellationTokenSource();
        
        try
        {
            var result = await _template.SaveAsync(_cts.Token);
            
            if (result.Success)
            {
                StatusMessage = "保存成功";
            }
            else
            {
                StatusMessage = $"保存失败: {result.Message}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    public void CancelSave()
    {
        _cts?.Cancel();
    }
}
```

### 优势

1. **响应式 UI**: 不阻塞主线程
2. **可取消**: 支持长时间操作取消
3. **进度报告**: 可以报告操作进度
4. **现代化**: 符合异步编程最佳实践

---

## 优化项 9: 模板事件系统 ⭐⭐⭐

### 现状问题

缺少事件通知机制，难以实现模板间的通信和 UI 更新。

### 优化方案

**建议**: 添加完整的事件系统

```csharp
// 1. 模板事件定义
public enum TemplateEventType
{
    Created,
    Deleted,
    Updated,
    Imported,
    Exported,
    Loaded,
    Saved
}

public class TemplateEventArgs : EventArgs
{
    public TemplateEventType EventType { get; set; }
    public ITemplate Template { get; set; }
    public int Index { get; set; }
    public object Data { get; set; }
    public DateTime Timestamp { get; set; }
}

// 2. 支持事件的模板接口
public interface ITemplateEvents
{
    event EventHandler<TemplateEventArgs> TemplateChanged;
    event EventHandler<TemplateEventArgs> BeforeTemplateChange;
}

// 3. 事件模板基类
public abstract class EventTemplateBase<TParam> 
    : AsyncTemplateBase<TParam>, ITemplateEvents
    where TParam : ParamModBase, new()
{
    public event EventHandler<TemplateEventArgs> TemplateChanged;
    public event EventHandler<TemplateEventArgs> BeforeTemplateChange;
    
    protected void OnTemplateChanged(TemplateEventType type, int index, object data = null)
    {
        TemplateChanged?.Invoke(this, new TemplateEventArgs
        {
            EventType = type,
            Template = this,
            Index = index,
            Data = data,
            Timestamp = DateTime.Now
        });
    }
    
    protected bool OnBeforeTemplateChange(TemplateEventType type, int index)
    {
        var args = new TemplateEventArgs
        {
            EventType = type,
            Template = this,
            Index = index
        };
        
        BeforeTemplateChange?.Invoke(this, args);
        
        // 允许订阅者取消操作
        return true;
    }
    
    public override async Task<Result> DeleteAsync(int index, CancellationToken ct)
    {
        if (!OnBeforeTemplateChange(TemplateEventType.Deleted, index))
            return Result.Fail("操作已取消");
            
        var result = await base.DeleteAsync(index, ct);
        
        if (result.Success)
            OnTemplateChanged(TemplateEventType.Deleted, index);
            
        return result;
    }
}

// 4. 事件订阅示例
public class TemplateMonitor
{
    public TemplateMonitor(ITemplateRegistry registry)
    {
        foreach (var template in registry.GetAll())
        {
            if (template is ITemplateEvents eventTemplate)
            {
                eventTemplate.TemplateChanged += OnTemplateChanged;
            }
        }
    }
    
    private void OnTemplateChanged(object sender, TemplateEventArgs e)
    {
        Logger.LogInformation(
            $"模板 {e.Template.Code} 发生变更: {e.EventType} at {e.Timestamp}");
            
        // 可以触发其他操作，如：
        // - 更新缓存
        // - 通知其他模块
        // - 记录审计日志
    }
}
```

### 优势

1. **松耦合**: 模块间通过事件通信
2. **可扩展**: 可以添加新的事件监听器
3. **审计支持**: 记录所有模板变更
4. **UI 更新**: 自动更新相关 UI

---

## 实施路线图

### 第一阶段 (1-2个月)
- ✅ 定义标准模板接口和基类
- ✅ 实施参数验证框架
- ✅ 统一命名规范

### 第二阶段 (2-4个月)
- ✅ 重构 ARVR 模块
- ✅ 实施版本管理
- ✅ POI 模块重组

### 第三阶段 (4-6个月)
- ✅ 算法模板分离
- ✅ JSON 模板框架
- ✅ 异步操作改造

### 第四阶段 (6-8个月)
- ✅ 事件系统集成
- ✅ 全面测试
- ✅ 文档更新

## 总结

本篇提出了 9 个模板实现层面的优化建议，主要关注：
- ✅ 算法模板标准化
- ✅ 版本管理和迁移
- ✅ POI 模块重组
- ✅ JSON 模板框架
- ✅ 参数验证
- ✅ 命名规范
- ✅ 算法模板分离
- ✅ 异步操作
- ✅ 事件系统

这些优化将显著提升模板实现的一致性、可维护性和性能。

**上一篇**: [Templates 模块优化建议 (上篇) - 核心架构优化](./template-optimization-top.md)
**下一篇**: [Templates 模块优化建议 (下篇) - 代码质量和性能优化](./template-optimization-bottom.md)
