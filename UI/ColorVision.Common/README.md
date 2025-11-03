# ColorVision.Common

## 🎯 功能定位

通用基础框架库，提供整个ColorVision系统的基础接口定义、MVVM支持和通用工具类。

## 作用范围

作为最底层的通用库，为所有上层模块提供统一的接口规范和基础功能实现。

## 主要功能点

### MVVM基础设施
- **ViewModelBase** - 视图模型基类，实现INotifyPropertyChanged
- **ActionCommand** - 支持Action委托的命令实现
- **RelayCommand** - 带参数的命令实现
- **RelayCommand<T>** - 泛型命令支持

### 核心接口定义
- **IConfig** - 配置对象统一接口，支持配置持久化
- **IConfigSetting** - 配置设置界面接口
- **IMenuItem** - 菜单项扩展接口
- **IWizardStep** - 向导步骤接口
- **IFileProcessor** - 文件处理器接口
- **ISearch** - 搜索功能接口
- **IInitializer** - 初始化器接口

### 通用工具类
- **观察者模式** - 事件订阅和通知机制
- **扩展方法** - 常用类型的扩展方法集合
- **辅助类** - 各种通用辅助功能

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.UI - 使用基础接口和MVVM支持
- ColorVision.Engine - 使用配置接口和通用工具
- 所有插件和项目 - 依赖通用接口定义

**引用的外部依赖**:
- 仅依赖.NET基础库，无第三方依赖

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Common\ColorVision.Common.csproj" />
```

### MVVM示例
```csharp
public class MyViewModel : ViewModelBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    
    public RelayCommand SaveCommand { get; }
    
    public MyViewModel()
    {
        SaveCommand = new RelayCommand(Save);
    }
    
    private void Save()
    {
        // 保存逻辑
    }
}
```

### 配置接口使用
```csharp
public class MyConfig : IConfig
{
    public string ConfigName => "MyConfig";
    
    public void Save()
    {
        // 保存配置
    }
    
    public void Load()
    {
        // 加载配置
    }
}
```

## 开发调试

```bash
dotnet build UI/ColorVision.Common/ColorVision.Common.csproj
```

## 相关文档链接

- [架构设计文档](../../docs/03-architecture/README.md)
- [扩展开发指南](../../docs/02-developer-guide/core-concepts/extensibility.md)

## 维护者

ColorVision 核心团队