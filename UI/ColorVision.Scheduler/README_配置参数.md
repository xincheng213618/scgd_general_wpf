# 定时任务配置参数使用指南

## 概述

ColorVision.Scheduler 现在支持为定时任务添加配置参数。这使得任务可以保存和使用特定的配置信息，例如选择特定的设备、设置参数等。

## 为现有任务添加配置支持

### 1. 创建配置类

创建一个继承自 `JobConfigBase` 的配置类，并使用 PropertyEditor 特性来定义 UI：

```csharp
using ColorVision.Scheduler;
using System.ComponentModel;

/// <summary>
/// 光谱仪数据采集任务配置
/// </summary>
public class SpectrumGetDataJobConfig : JobConfigBase
{
    [Category("光谱仪设置")]
    [DisplayName("光谱仪设备名称")]
    [Description("输入要使用的光谱仪设备名称")]
    public string DeviceSpectrumName 
    { 
        get => _DeviceSpectrumName; 
        set { _DeviceSpectrumName = value; OnPropertyChanged(); } 
    }
    private string _DeviceSpectrumName;
}
```

### 2. 实现 IConfigurableJob 接口

更新您的任务类以实现 `IConfigurableJob` 接口：

```csharp
using ColorVision.Scheduler;
using Quartz;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

[DisplayName("光谱仪单次测试")]
public class SpectrumGetDataJob : IJob, IConfigurableJob
{
    // 指定配置类型
    public Type ConfigType => typeof(SpectrumGetDataJobConfig);

    // 创建默认配置
    public IJobConfig CreateDefaultConfig()
    {
        var config = new SpectrumGetDataJobConfig();
        // 设置默认值为第一个可用设备
        var firstDevice = ServiceManager.GetInstance().DeviceServices
            .OfType<DeviceSpectrum>().FirstOrDefault();
        if (firstDevice != null)
        {
            config.DeviceSpectrumName = firstDevice.Config.Name;
        }
        return config;
    }

    public Task Execute(IJobExecutionContext context)
    {
        var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos
            .First(x => x.JobName == context.JobDetail.Key.Name && 
                       x.GroupName == context.JobDetail.Key.Group);
        
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            schedulerInfo.Status = SchedulerStatus.Running;
        });

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            DeviceSpectrum deviceSpectrum = null;
            
            // 从配置中获取设备
            if (schedulerInfo.Config is SpectrumGetDataJobConfig config && 
                !string.IsNullOrEmpty(config.DeviceSpectrumName))
            {
                deviceSpectrum = ServiceManager.GetInstance().DeviceServices
                    .OfType<DeviceSpectrum>()
                    .FirstOrDefault(d => d.Config.Name == config.DeviceSpectrumName);
            }
            
            // 如果配置中的设备未找到，回退到最后一个设备
            if (deviceSpectrum == null)
            {
                deviceSpectrum = ServiceManager.GetInstance().DeviceServices
                    .OfType<DeviceSpectrum>().LastOrDefault();
            }
            
            deviceSpectrum?.DService.GetData();
            schedulerInfo.Status = SchedulerStatus.Ready;
        });
        
        return Task.CompletedTask;
    }
}
```

## 使用配置

### 在 UI 中配置任务

1. 打开"创建任务"对话框
2. 选择支持配置的任务类型
3. 在"任务"下拉框下方会自动显示配置参数区域
4. 填写配置参数（例如设备名称）
5. 保存任务

### 配置属性特性

可以使用以下特性来自定义配置 UI：

```csharp
using System.ComponentModel;
using ColorVision.UI;

public class MyJobConfig : JobConfigBase
{
    // 基本文本输入
    [Category("基本设置")]
    [DisplayName("设备名称")]
    [Description("要使用的设备名称")]
    public string DeviceName { get; set; }

    // 数值输入
    [Category("参数")]
    [DisplayName("采样次数")]
    [Description("执行采样的次数")]
    public int SampleCount { get; set; } = 1;

    // 枚举选择
    [Category("模式")]
    [DisplayName("测量模式")]
    public MeasurementMode Mode { get; set; }

    // 布尔值
    [Category("选项")]
    [DisplayName("启用自动校准")]
    public bool AutoCalibration { get; set; } = true;

    // 自定义编辑器
    [Category("高级")]
    [DisplayName("配置文件")]
    [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
    public string ConfigFilePath { get; set; }
}
```

## 配置持久化

配置会自动保存到以下位置：
```
%APPDATA%\ColorVision\scheduler_tasks.json
```

配置使用 JSON 序列化，支持多态类型（`TypeNameHandling.All`）。

## 最佳实践

1. **提供合理的默认值**：在 `CreateDefaultConfig()` 中设置有意义的默认值
2. **添加清晰的描述**：使用 `Description` 特性帮助用户理解每个参数
3. **使用分类**：使用 `Category` 特性将相关参数分组
4. **验证配置**：在 `Execute()` 方法中验证配置值的有效性
5. **提供回退逻辑**：如果配置的资源不可用，提供合理的回退行为

## 已支持配置的任务

- **光谱仪单次测试** (`SpectrumGetDataJob`)
  - 可配置：光谱仪设备名称
  
- **相机拍摄任务** (`CameraCaptureJob`)
  - 可配置：相机设备名称

## 扩展说明

### 创建自定义属性编辑器

如果需要特殊的 UI 控件，可以创建自定义属性编辑器：

```csharp
using ColorVision.UI;
using System.Reflection;
using System.Windows.Controls;

public class MyCustomPropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var rm = PropertyEditorHelper.GetResourceManager(obj);
        var dockPanel = new DockPanel { LastChildFill = true };

        var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
        dockPanel.Children.Add(textBlock);

        // 创建自定义控件
        var myControl = new MyCustomControl();
        var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
        myControl.SetBinding(MyCustomControl.ValueProperty, binding);

        dockPanel.Children.Add(myControl);
        return dockPanel;
    }
}
```

然后在配置类中使用：

```csharp
[PropertyEditorType(typeof(MyCustomPropertiesEditor))]
public string MyProperty { get; set; }
```

## 疑难解答

### 配置 UI 没有显示

确保：
1. 任务类实现了 `IConfigurableJob` 接口
2. 配置类继承自 `JobConfigBase`
3. 属性有公开的 getter 和 setter
4. 使用了正确的特性（`Category`, `DisplayName` 等）

### 配置值没有保存

检查：
1. 属性的 setter 调用了 `OnPropertyChanged()`
2. 任务信息已正确保存（查看日志）
3. JSON 文件权限正常

### 任务执行时配置为 null

确保：
1. `CreateDefaultConfig()` 返回非 null 对象
2. 任务在创建时正确调用了 `CreateDefaultConfig()`
3. JSON 序列化配置包含类型信息（`TypeNameHandling.All`）

## 参考资料

- PropertyEditor 文档：`docs/02-developer-guide/core-concepts/property-editor.md`
- Scheduler 文档：`docs/04-api-reference/ui-components/ColorVision.Scheduler.md`
- 示例实现：
  - `Engine/ColorVision.Engine/Services/Devices/Spectrum/DisplaySpectrum.xaml.cs`
  - `Engine/ColorVision.Engine/Services/Devices/Camera/CameraCaptureJob.cs`
