# 预处理和文件清理模块

## 概述

本功能为流程执行添加了两个新模块：

1. **预处理模块 (PreProcess)**: 在流程运行之前执行的检测逻辑
2. **后处理模块 (BatchProcess)**: 在流程完成后执行的清理逻辑（已有框架，新增文件清理实现）

## 功能说明

### 预处理模块 (PreProcess)

预处理模块在流程开始执行前运行。如果预处理失败，流程将不会执行。

#### 内置预处理器

**文件夹大小检测 (FolderSizePreProcess)**
- 监控指定文件夹的大小
- 当超过配置的大小限制时，自动删除最早的文件
- 支持配置：
  - 监控文件夹路径
  - 大小限制（MB）
  - 文件扩展名过滤（逗号分隔，如: .jpg,.png,.tiff）
  - 是否包含子文件夹
  - 启用/禁用开关

#### 使用方法

1. 在流程界面点击"预处理管理"按钮
2. 选择流程模板和预处理类
3. 配置预处理参数
4. 点击"添加"将预处理添加到流程

### 后处理模块 (BatchProcess)

后处理模块在流程完成后运行，用于清理流程产生的文件。

#### 内置后处理器

**文件清理 (FileCleanupProcess)**
- 删除流程产生的临时文件或指定类型的文件
- 支持配置：
  - 清理文件夹路径
  - 文件扩展名过滤（逗号分隔，如: .tmp,.log,.cache）
  - 文件名模式（支持通配符*和?）
  - 是否包含子文件夹
  - 保留最近N个文件
  - 仅删除旧于N天的文件
  - 是否删除空文件夹
  - 启用/禁用开关

#### 使用方法

1. 在流程界面点击"流程处理配置"按钮（原有功能）
2. 选择流程模板和处理类
3. 配置处理参数
4. 点击"添加"将处理添加到流程

## 配置示例

### 示例1：监控图像文件夹大小

**场景**: 限制相机采集图像文件夹不超过10GB，超过时删除最旧的图像

**预处理配置**:
- 监控文件夹: `C:\ImageData\Camera`
- 大小限制(MB): `10240`
- 文件扩展名: `.jpg,.png,.tiff,.bmp`
- 包含子文件夹: `否`
- 启用: `是`

### 示例2：清理流程临时文件

**场景**: 流程完成后删除临时文件，保留最近100个文件

**后处理配置**:
- 清理文件夹: `C:\Temp\FlowTemp`
- 文件扩展名: `.tmp,.cache,.log`
- 文件名模式: 留空
- 包含子文件夹: `是`
- 保留最近N个文件: `100`
- 仅删除旧于N天的文件: `0` (不限制)
- 删除空文件夹: `是`
- 启用: `是`

### 示例3：定期清理旧数据

**场景**: 删除30天前的分析结果文件

**后处理配置**:
- 清理文件夹: `C:\AnalysisResults`
- 文件扩展名: `.csv,.xlsx,.pdf`
- 文件名模式: 留空
- 包含子文件夹: `是`
- 保留最近N个文件: `0` (不保留)
- 仅删除旧于N天的文件: `30`
- 删除空文件夹: `是`
- 启用: `是`

## 扩展开发

### 自定义预处理器

实现自定义预处理器：

```csharp
using ColorVision.Engine.Batch;
using ColorVision.Common.MVVM;
using log4net;

// 1. 定义配置类
public class MyPreProcessConfig : ViewModelBase
{
    [DisplayName("配置项名称")]
    [Description("配置项说明")]
    public string MyProperty { get; set; }
}

// 2. 实现预处理器
[PreProcess("我的预处理器", "预处理器功能描述")]
public class MyPreProcess : PreProcessBase<MyPreProcessConfig>
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MyPreProcess));
    
    public override bool PreProcess(IPreProcessContext ctx)
    {
        try
        {
            // 执行预处理逻辑
            log.Info($"执行预处理: {ctx.FlowName}");
            
            // 返回 true 表示成功，流程继续
            // 返回 false 表示失败，流程取消
            return true;
        }
        catch (Exception ex)
        {
            log.Error("预处理失败", ex);
            return false;
        }
    }
}
```

### 自定义后处理器

实现自定义后处理器：

```csharp
using ColorVision.Engine.Batch;
using ColorVision.Common.MVVM;
using log4net;

// 1. 定义配置类
public class MyBatchProcessConfig : ViewModelBase
{
    [DisplayName("配置项名称")]
    [Description("配置项说明")]
    public string MyProperty { get; set; }
}

// 2. 实现后处理器
[BatchProcess("我的后处理器", "后处理器功能描述")]
public class MyBatchProcess : BatchProcessBase<MyBatchProcessConfig>
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MyBatchProcess));
    
    public override bool Process(IBatchContext ctx)
    {
        try
        {
            // 执行后处理逻辑
            log.Info($"执行后处理: {ctx.FlowName}");
            log.Info($"批次ID: {ctx.Batch?.Id}");
            
            // 返回 true 表示成功
            // 返回 false 表示失败（不会影响流程，仅记录）
            return true;
        }
        catch (Exception ex)
        {
            log.Error("后处理失败", ex);
            return false;
        }
    }
}
```

## 架构说明

### 预处理执行流程

```
用户点击运行流程
    ↓
检查预处理配置
    ↓
按顺序执行所有预处理器 ← 如果任一失败，终止流程
    ↓
开始执行流程
```

### 后处理执行流程

```
流程执行完成
    ↓
检查后处理配置
    ↓
按顺序执行所有后处理器 ← 失败不影响流程状态
    ↓
记录结果
```

### 配置持久化

- 预处理配置保存在: `%AppData%\ColorVision\Config\PreProcessConfig.json`
- 后处理配置保存在: `%AppData%\ColorVision\Config\BatchConfig.json`

## 注意事项

1. **预处理器失败**: 会阻止流程执行，请谨慎配置
2. **后处理器失败**: 不会影响流程状态，仅记录错误
3. **文件删除**: 删除操作不可恢复，建议先在测试环境验证配置
4. **执行顺序**: 同一流程模板的多个处理器按列表顺序执行
5. **性能影响**: 大量文件操作可能影响性能，建议合理配置

## 日志

所有预处理和后处理的执行情况都会记录到日志中：
- 执行开始/结束
- 配置参数
- 删除的文件列表
- 错误信息

可以通过查看日志了解详细的执行情况。

## 支持

如有问题或建议，请联系开发团队。
