# ColorVision.UI.Desktop

> 版本: 1.5.1.5 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

## 🎯 功能定位

ColorVision 系统的桌面应用程序入口模块，提供主窗口、菜单定制、设置管理、配置向导、插件管理和第三方应用集成等功能。它是整个应用程序的启动点和桌面端特定功能的实现层。

## 作用范围

桌面应用程序层，负责应用程序的启动、主窗口管理、菜单定制、系统设置、配置向导和桌面端特定功能。

## 主要功能点

### 应用程序入口 (App.xaml.cs)
- **应用启动** - 应用程序入口点，初始化全局资源
- **异常处理** - 全局异常捕获和处理
- **单实例** - 确保应用程序单实例运行
- **启动参数** - 支持命令行参数处理

### 主窗口 (MainWindow)
- **主界面容器** - 应用程序主窗口容器
- **停靠布局** - 基于 AvalonDock 的停靠面板系统
- **视图管理** - 文档视图和面板视图管理
- **状态栏** - 应用程序状态信息显示

### 菜单管理 (MenuItemManager)
- **菜单定制** - 自定义菜单项的可见性和排序
- **持久化设置** - 保存和恢复菜单配置
- **树形编辑** - 可视化菜单结构编辑
- **导入导出** - 菜单配置的导入导出

### 设置管理 (Settings)
- **统一设置界面** - SettingWindow 从所有已注册的配置提供者加载设置
- **分类管理** - 按类别分组显示配置项
- **三种类型** - 支持 TabItem、Class、Property 三种设置类型
- **自动发现** - 通过反射自动发现 IConfigSettingProvider 实现

### 配置向导 (Wizards)
- **多步引导** - WizardWindow 提供分步骤的初始化配置向导
- **自动发现** - WizardManager 通过反射自动发现 IWizardStep 实现
- **进度跟踪** - 可视化的完成进度指示
- **完成验证** - 验证所有步骤配置是否完成

### 插件更新 (PluginsUpdate)
- **版本检查** - 检查插件更新
- **DLL版本查看** - ViewDllVersionsWindow 查看DLL版本信息

### 第三方应用 (ThirdPartyApps)
- **应用浏览** - SystemAppProvider 提供 Windows 系统工具集合
- **快速启动** - 一键启动系统工具和外部应用

### 系统初始化 (SystemInitializer)
- **CUDA初始化** - SystemInitializer 初始化 CUDA 环境
- **系统信息** - 启动时记录操作系统、.NET 版本、CPU 和内存信息
- **调试支持** - 记录调试模式状态和应用程序版本

### 原生方法 (NativeMethods)
- **快捷方式创建** - ShortcutCreator 创建 Windows 快捷方式

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                   ColorVision.UI.Desktop                      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │    App      │    │ MainWindow  │    │ MenuItemMgr │      │
│  │             │    │             │    │             │      │
│  │ • 启动流程  │    │ • 停靠布局  │    │ • 菜单定制  │      │
│  │ • 异常处理  │    │ • 视图管理  │    │ • 持久化    │      │
│  │ • 单实例    │    │ • 状态栏    │    │ • 导入导出  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │   Settings  │    │   Wizards   │    │   Plugins   │      │
│  │             │    │             │    │             │      │
│  │ • 统一设置  │    │ • 配置向导  │    │ • 版本检查  │      │
│  │ • 自动发现  │    │ • 自动发现  │    │ • DLL查看   │      │
│  │ • 三种类型  │    │ • 进度跟踪  │    │ • 更新管理  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐                          │
│  │ ThirdParty  │    │  SystemInit │                          │
│  │             │    │             │                          │
│  │ • 系统工具  │    │ • CUDA初始化│                          │
│  │ • 快速启动  │    │ • 系统信息  │                          │
│  │ • 应用浏览  │    │ • 调试支持  │                          │
│  └─────────────┘    └─────────────┘                          │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 与主程序的依赖关系

**被引用方式**:
- 作为桌面应用程序的入口项目
- 被 ColorVision 主程序引用作为 UI 宿主

**引用的程序集**:
- `ColorVision.UI` - 基础UI框架
- `ColorVision.Solution` - 解决方案管理
- `ColorVision.ImageEditor` - 图像编辑器
- `ColorVision.Database` - 数据库模块
- `ColorVision.Themes` - 主题支持
- `ColorVision.Scheduler` - 任务调度
- `ColorVision.SocketProtocol` - 网络通信

## 使用方式

### 项目配置
这是应用程序的入口项目，配置为可执行文件：

```xml
<OutputType>WinExe</OutputType>
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
```

### 应用程序启动
```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 初始化插件
        PluginLoader.LoadPlugins();
        
        // 应用主题
        this.ApplyTheme(ThemeConfig.Instance.Theme);
        
        // 检查是否首次运行
        if (!WizardWindowConfig.Instance.WizardCompletionKey)
        {
            // 显示配置向导
            var wizard = new WizardWindow();
            wizard.ShowDialog();
            
            if (!WizardWindowConfig.Instance.WizardCompletionKey)
            {
                Shutdown();
                return;
            }
        }
        
        // 显示主窗口
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
```

### 菜单项配置
```csharp
// 添加菜单项设置
var config = MenuItemManagerConfig.Instance;
config.Settings.Add(new MenuItemSetting
{
    Id = "menuFileOpen",
    Header = "打开",
    IsVisible = true,
    Order = 1,
    Hotkey = "Ctrl+O"
});

// 保存配置
ConfigHandler.GetInstance().SaveConfigs();
```

### 创建设置提供者
```csharp
public class MySettingProvider : IConfigSettingProvider
{
    public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
    {
        return new List<ConfigSettingMetadata>
        {
            new ConfigSettingMetadata
            {
                Type = ConfigSettingType.TabItem,
                Name = "我的设置",
                Order = 100,
                UserControl = new MySettingsControl()
            },
            new ConfigSettingMetadata
            {
                Type = ConfigSettingType.Class,
                Name = "高级设置",
                Order = 200,
                Source = MyConfig.Instance
            }
        };
    }
}
```

### 创建向导步骤
```csharp
public class DatabaseWizardStep : IWizardStep
{
    public int Order => 1;
    public string Title => "数据库配置";
    public string Description => "配置数据库连接参数";
    public bool ConfigurationStatus => MySQLConfig.Instance.TestConnection();
    public UserControl StepContent => new DatabaseConfigControl();
    
    public bool Validate()
    {
        return MySQLConfig.Instance.TestConnection();
    }
}
```

## 主要组件

### MainWindow
主窗口类，应用程序的主界面容器。

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    // 停靠布局管理
    public DockLayoutManager LayoutManager { get; }
    
    // 文档视图宿主
    public DockViewManagerHost ViewManagerHost { get; }
}
```

### MenuItemManagerConfig
菜单项管理配置类，存储菜单的自定义配置。

```csharp
public class MenuItemManagerConfig : IConfig
{
    public static MenuItemManagerConfig Instance => 
        ConfigService.Instance.GetRequiredService<MenuItemManagerConfig>();
    
    // 菜单项设置集合
    public ObservableCollection<MenuItemSetting> Settings { get; set; } = new();
    
    // 最后选中的树节点
    public string? LastSelectedTreeNode { get; set; }
}
```

### SettingWindow
统一设置窗口，自动发现并加载所有已注册配置提供者的设置项。

```csharp
public partial class SettingWindow : Window
{
    public SettingWindow()
    {
        InitializeComponent();
        this.ApplyCaption();
    }
    
    // 加载配置设置
    public void LoadIConfigSetting()
    {
        // 自动发现所有 IConfigSettingProvider 实现
        // 支持三种设置类型: TabItem, Class, Property
        // 按类别分组、按优先级排序
    }
}
```

### WizardWindow
配置向导窗口，引导用户完成初始化配置。

```csharp
public partial class WizardWindow : Window
{
    public static WizardWindowConfig WindowConfig => WizardWindowConfig.Instance;
    
    public WizardWindow()
    {
        InitializeComponent();
        this.ApplyCaption();
        WindowConfig.SetWindow(this);
    }
}
```

### WizardManager
向导管理器，负责发现和管理所有向导步骤。

```csharp
public class WizardManager : ViewModelBase
{
    public static WizardManager GetInstance()
    
    // 所有向导步骤
    public List<IWizardStep> IWizardSteps { get; private set; } = new();
    
    // 初始化，自动发现所有 IWizardStep 实现
    public void Initialized()
}
```

### SystemInitializer
系统初始化器，负责应用程序启动时的系统初始化和信息记录。

```csharp
public class SystemInitializer : IInitializer
{
    public int Order => 8;
    
    public async Task InitializeAsync()
    {
        // 记录系统信息
        // 初始化 CUDA
        // 记录 .NET 版本
        // 记录 CPU 和内存信息
    }
}
```

### ShortcutCreator
快捷方式创建工具，使用 WScript.Shell COM 对象创建 `.lnk` 文件。

```csharp
public static class ShortcutCreator
{
    // 创建快捷方式
    public static void CreateShortcut(string name, string path, 
        string target, string arguments)
}
```

## 目录说明

- `App.xaml/cs` - 应用程序定义和启动逻辑
- `MainWindow.xaml/cs` - 主窗口
- `MenuItemManager/` - 菜单项管理
  - `MenuItemManagerConfig.cs` - 菜单配置
  - `MenuItemSetting.cs` - 菜单项设置
- `Settings/` - 设置管理
  - `SettingWindow.xaml/cs` - 设置窗口
- `Wizards/` - 配置向导
  - `WizardWindow.xaml/cs` - 向导窗口
  - `WizardWindowConfig.cs` - 向导配置
  - `WizardManager.cs` - 向导管理器
- `Plugins/` - 插件更新
  - `PluginsUpdate.cs` - 插件更新
  - `ViewDllVersionsWindow.xaml/cs` - DLL版本查看
- `ThirdPartyApps/` - 第三方应用
  - `SystemAppProvider.cs` - 系统应用提供者
- `CUDA/` - 系统初始化
  - `SystemInitializer.cs` - 系统初始化器
- `NativeMethods/` - 原生方法
  - `ShortcutCreator.cs` - 快捷方式创建

## 开发调试

```bash
# 构建项目
dotnet build UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj

# 运行应用程序
dotnet run --project UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj

# 发布应用程序
dotnet publish UI/ColorVision.UI.Desktop/ColorVision.UI.Desktop.csproj -c Release
```

## 最佳实践

### 1. 应用程序启动
- 在 `OnStartup` 中按正确顺序初始化各子系统
- 首先加载插件，然后应用主题
- 首次运行时显示配置向导
- 处理启动参数

### 2. 菜单定制
- 使用 `MenuItemManagerConfig` 持久化菜单配置
- 提供菜单配置的导入导出功能
- 支持快捷键绑定

### 3. 设置管理
- 实现 `IConfigSettingProvider` 提供设置项
- 合理使用三种设置类型（TabItem/Class/Property）
- 设置项按逻辑分组
- 提供设置项的排序

### 4. 配置向导
- 实现 `IWizardStep` 接口创建向导步骤
- 设置合适的 `Order` 值控制步骤顺序
- 提供配置验证
- 显示配置进度

### 5. 系统初始化
- 设置合适的 `Order` 值确保初始化顺序
- 记录必要的系统信息
- 处理初始化失败的情况
- 异步初始化避免阻塞UI

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/ui-components/ColorVision.UI.Desktop.md)
- [ColorVision.UI README](./../ColorVision.UI/README.md)
- [WPF 应用程序开发指南](https://docs.microsoft.com/zh-cn/dotnet/desktop/wpf/)

## 更新日志

### v1.5.1.5 (2026-02)
- 支持 .NET 10.0
- 重构菜单项管理系统
- 优化配置向导界面
- 增强设置窗口功能
- 改进系统初始化流程

### v1.4.1.1 (2026-02)
- 停靠布局管理
- 多图像查看器
- 工作区管理

### v1.3.18.1 (2025-02)
- 基础应用程序框架
- 配置向导
- 系统初始化

## 维护者

ColorVision UI团队
