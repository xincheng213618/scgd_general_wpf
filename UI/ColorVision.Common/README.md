# ColorVision.Common

> 版本: 1.5.1.2 | 目标框架: .NET 8.0 / .NET 10.0 Windows

## 🎯 功能定位

ColorVision 系统的通用基础框架库，提供整个系统的基础接口定义、MVVM架构支持、命令模式实现以及各种通用工具类。作为最底层的通用库，为所有上层模块提供统一的接口规范和基础功能实现。

## 作用范围

基础框架层，为 ColorVision.UI、ColorVision.Engine 以及所有插件和项目提供通用的接口定义、MVVM支持、工具类和基础服务。

## 主要功能点

### MVVM 架构支持
- **ViewModelBase** - 视图模型基类，实现 INotifyPropertyChanged
- **ViewModelBaseExtensions** - ViewModelBase 扩展方法
- **ActionCommand** - 支持 Action 委托的命令实现
- **RelayCommand** - 带参数的命令实现
- **RelayCommand<T>** - 泛型命令支持

### 配置管理接口
- **IConfig** - 配置对象的基础标记接口
- **IConfigSettingProvider** - 配置设置提供者接口
- **ConfigSettingMetadata** - 配置设置元数据
- **ConfigService** - 配置服务

### 菜单系统接口
- **IMenuItemProvider** - 菜单项提供者接口
- **IRightMenuItemProvider** - 右键菜单项提供者接口
- **MenuItemMetadata** - 菜单项元数据
- **MenuService** - 菜单服务

### 视图接口
- **IView** - 视图接口
- **IViewManager** - 视图管理器接口
- **View** - 视图基类
- **ViewGridManager** - 视图网格管理器

### 状态栏接口
- **IStatusBarProvider** - 状态栏提供者接口
- **StatusBarMeta** - 状态栏元数据
- **StatusBarAlignment** - 状态栏对齐方式

### 搜索接口
- **ISearch** - 搜索功能接口
- **ISearchProvider** - 搜索提供者接口
- **SearchBase** - 搜索基类
- **SearchMeta** - 搜索元数据
- **SearchType** - 搜索类型

### 初始化接口
- **IInitializer** - 初始化器接口
- **InitializerBase** - 初始化器基类

### 插件接口
- **IPlugin** - 插件基础接口
- **IPluginBase** - 插件基类

### 文件处理接口
- **IFileProcessor** - 文件处理器接口
- **IThumbnailProvider** - 缩略图提供者接口
- **ThumbnailProviderFactory** - 缩略图提供者工厂

### 向导接口
- **IWizardStep** - 向导步骤接口

### 权限管理
- **AccessControl** - 访问控制类
- **RequiresPermissionAttribute** - 权限要求特性
- **PermissionMode** - 权限模式

### 通用工具类
- **FileUtils** - 文件工具类
- **ImageUtils** - 图像工具类
- **Cryptography** - 加密工具类
- **RegexUtils** - 正则表达式工具类
- **RegUtils** - 注册表工具类
- **WindowHelpers** - 窗口帮助类
- **WindowUtils** - 窗口工具类
- **CollectionUtils** - 集合工具类
- **DictionaryUtils** - 字典工具类
- **StringUtils** - 字符串工具类
- **EnumUtils** - 枚举工具类
- **FileIcon** - 文件图标工具
- **FileAssociation** - 文件关联工具

### 原生方法封装
- **User32** - Windows User32 API 封装
- **Dwmapi** - Windows DWM API 封装
- **Clipboard** - 剪贴板操作
- **Keyboard** - 键盘操作
- **IniFiles** - INI 文件操作
- **FileProperties** - 文件属性操作
- **PerformanceInfo** - 性能信息获取
- **CheckAppRunning** - 检查应用程序运行状态
- **DumpHelper** - 崩溃转储帮助

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                    ColorVision.Common                         │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │    MVVM     │    │   接口定义   │    │   命令系统   │      │
│  │             │    │             │    │             │      │
│  │ • ViewModel │    │ • IConfig   │    │ • ActionCmd │      │
│  │ • RelayCmd  │    │ • IPlugin   │    │ • RelayCmd  │      │
│  │ • Extensions│    │ • IMenuItem │    │             │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │   工具类    │    │   权限管理   │    │  原生方法   │      │
│  │             │    │             │    │             │      │
│  │ • FileUtils │    │ • AccessCtrl│    │ • User32    │      │
│  │ • ImageUtils│    │ • Permission│    │ • Dwmapi    │      │
│  │ • Crypto    │    │             │    │ • Clipboard │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.UI - 使用基础接口和MVVM支持
- ColorVision.Engine - 使用配置接口和通用工具
- ColorVision.Themes - 使用基础接口
- ColorVision.ImageEditor - 使用MVVM和工具类
- ColorVision.Database - 使用配置接口
- ColorVision.Solution - 使用接口和工具类
- ColorVision.Scheduler - 使用基础接口
- ColorVision.SocketProtocol - 使用基础接口
- 所有插件和项目 - 依赖通用接口定义

**引用的外部依赖**:
- 仅依赖 .NET 基础库，无第三方依赖

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Common\ColorVision.Common.csproj" />
```

### MVVM 示例
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
    private string _theme = "Light";
    private string _language = "zh-CN";
    
    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }
    
    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }
}

// 配置设置提供者
public class MyConfigSettingProvider : IConfigSettingProvider
{
    public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
    {
        return new List<ConfigSettingMetadata>
        {
            new ConfigSettingMetadata
            {
                Type = ConfigSettingType.Property,
                Name = "主题",
                Group = "通用",
                Order = 1,
                Source = MyConfig.Instance,
                BindingName = nameof(MyConfig.Theme)
            }
        };
    }
}
```

### 菜单提供者实现
```csharp
public class MyMenuProvider : IMenuItemProvider
{
    public IEnumerable<MenuItemMetadata> GetMenuItems()
    {
        return new List<MenuItemMetadata>
        {
            new MenuItemMetadata
            {
                Id = "menuMyFeature",
                Header = "我的功能",
                Icon = "/Icons/myfeature.png",
                Command = new ActionCommand(ExecuteMyFeature),
                Order = 100
            }
        };
    }
}
```

### 初始化器实现
```csharp
public class MyInitializer : InitializerBase
{
    public override int Order => 10;
    
    public override async Task InitializeAsync()
    {
        // 异步初始化逻辑
        await Task.Delay(100);
        Console.WriteLine("MyInitializer 已执行");
    }
}
```

### 文件工具使用
```csharp
// 确保目录存在
FileUtils.EnsureDirectory(@"C:\MyApp\Data");

// 安全删除文件
FileUtils.SafeDelete(@"C:\MyApp\Temp\file.tmp");

// 获取文件大小文本
var sizeText = FileUtils.GetFileSizeText(1024 * 1024); // "1 MB"
```

### 加密工具使用
```csharp
// MD5 哈希
var md5Hash = Cryptography.MD5Hash("password");

// SHA256 哈希
var sha256Hash = Cryptography.SHA256Hash("password");

// AES 加密
var encrypted = Cryptography.AESEncrypt(data, key, iv);
var decrypted = Cryptography.AESDecrypt(encrypted, key, iv);
```

## 主要组件

### ViewModelBase
提供 INotifyPropertyChanged 的基础实现：

```csharp
[Serializable]
public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
    {
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

### ActionCommand
简单的命令实现：

```csharp
public class ActionCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public ActionCommand(Action execute, Func<bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object parameter) => _execute();
    
    // 支持撤销/重做
    public Action UndoAction { get; set; }
    public Action RedoAction { get; set; }
    public void Undo() => UndoAction?.Invoke();
    public void Redo() => RedoAction?.Invoke();
}
```

### RelayCommand<T>
支持参数的命令实现：

```csharp
public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Predicate<T> _canExecute;

    public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
    public void Execute(object parameter) => _execute((T)parameter);

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}
```

### ConfigService
配置服务，提供配置实例管理：

```csharp
public class ConfigService
{
    public static ConfigService Instance => _instance ??= new ConfigService();
    
    // 获取配置实例
    public T GetRequiredService<T>() where T : class, IConfig, new();
    
    // 注册配置实例
    public void Register<T>(T instance) where T : class, IConfig;
}
```

## 目录说明

- `MVVM/` - MVVM 基础设施
  - `ViewModelBase.cs` - 视图模型基类
  - `ViewModelBaseExtensions.cs` - 扩展方法
  - `RelayCommand.cs` - 中继命令
  - `ActionCommand.cs` - 动作命令
- `Interfaces/` - 接口定义
  - `Config/` - 配置接口
  - `Menus/` - 菜单接口
  - `Views/` - 视图接口
  - `StatusBar/` - 状态栏接口
  - `Serach/` - 搜索接口
  - `IPlugin.cs` - 插件接口
  - `IInitializer.cs` - 初始化器接口
  - `IWizardStep.cs` - 向导接口
  - `IFileProcessor.cs` - 文件处理器接口
  - `IThumbnailProvider.cs` - 缩略图接口
  - `IDisPlayControl.cs` - 显示控制接口
  - `IFeatureLauncher.cs` - 功能启动器接口
- `Authorizations/` - 权限管理
  - `AccessControl.cs` - 访问控制
  - `RequiresPermissionAttribute.cs` - 权限特性
  - `PermissionMode.cs` - 权限模式
- `Utilities/` - 工具类
  - `FileUtils.cs` - 文件工具
  - `ImageUtils.cs` - 图像工具
  - `Cryptography.cs` - 加密工具
  - `RegexUtils.cs` - 正则工具
  - `RegUtils.cs` - 注册表工具
  - `WindowHelpers.cs` - 窗口帮助
  - `WindowUtils.cs` - 窗口工具
  - `CollectionUtils.cs` - 集合工具
  - `DictionaryUtils.cs` - 字典工具
  - `StringUtils.cs` - 字符串工具
  - `EnumUtils.cs` - 枚举工具
- `NativeMethods/` - 原生方法
  - `User32.cs` - User32 API
  - `Dwmapi.cs` - DWM API
  - `Clipboard.cs` - 剪贴板
  - `Keyboard.cs` - 键盘
  - `IniFiles.cs` - INI文件
  - `FileProperties.cs` - 文件属性
  - `FileIcon.cs` - 文件图标
  - `FileAssociation.cs` - 文件关联
  - `PerformanceInfo.cs` - 性能信息
  - `CheckAppRunning.cs` - 应用运行检查
  - `DumpHelper.cs` - 崩溃转储
  - `Shlwapi.cs` - Shell API
  - `ShellFileOperations.cs` - Shell文件操作
- `Input/` - 输入处理
  - `Cursors.cs` - 光标
- `Algorithms/` - 算法
  - `GrahamScan.cs` - Graham扫描算法

## 开发调试

```bash
# 构建项目
dotnet build UI/ColorVision.Common/ColorVision.Common.csproj

# 运行测试
dotnet test
```

## 最佳实践

1. **MVVM 模式**: 使用 ViewModelBase 基类确保属性变更通知
2. **命令绑定**: 使用 ActionCommand 和 RelayCommand 实现命令绑定
3. **接口设计**: 通过接口定义组件契约，提高系统的可测试性和可扩展性
4. **配置管理**: 实现 IConfigSettingProvider 接口统一配置管理方式
5. **插件架构**: 通过 IPlugin 接口实现插件化架构
6. **初始化顺序**: 使用 Order 属性控制初始化器的执行顺序
7. **权限控制**: 使用 RequiresPermissionAttribute 标记需要权限的方法

## 依赖关系

ColorVision.Common 作为基础库，被其他所有组件依赖：

- ColorVision.UI 依赖 Common
- ColorVision.ImageEditor 依赖 Common
- ColorVision.Themes 依赖 Common
- ColorVision.Database 依赖 Common
- ColorVision.Solution 依赖 Common
- ColorVision.Scheduler 依赖 Common
- ColorVision.SocketProtocol 依赖 Common
- 所有业务模块都依赖 Common

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/ui-components/ColorVision.Common.md)
- [MVVM 模式最佳实践](../../docs/02-developer-guide/ui-development/xaml-mvvm.md)
- [插件开发指南](../../docs/02-developer-guide/plugin-development/overview.md)
- [配置管理指南](../../docs/00-getting-started/README.md)

## 更新日志

### v1.5.1.2（2026-02）
- 升级目标框架至 .NET 8.0 / .NET 10.0
- 移除 .NET 6.0 支持
- 新增 NuGet 符号包发布支持
- 改进 ViewModelBase 属性通知性能
- 新增 IStatusBarProvider 接口
- 新增 IView 和 IViewManager 接口
- 优化文件工具类性能

### v1.3.8.1 及更早
- 基础 MVVM 架构支持
- 接口定义系统（IConfig、IPlugin、IMenuItem 等）
- ActionCommand / RelayCommand 命令模式
- 权限管理系统

## 维护者

ColorVision 核心团队
