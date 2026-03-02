# ColorVision.UI.Desktop

> 版本: 1.5.1.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

## 🎯 功能定位

ColorVision 系统的桌面应用程序入口模块，提供主窗口、WebView 服务和配置管理窗口。这是整个应用程序的启动点和桌面端特定功能的实现层。

## 作用范围

桌面应用程序层，负责应用程序的启动、主窗口管理、Web 内容显示和桌面端配置管理。

## 主要功能点

### 主窗口 (MainWindow)
- **应用程序主界面** - 提供主窗口容器
- **模块集成** - 承载各功能模块的 UI
- **生命周期管理** - 管理应用程序的启动和关闭流程

### WebView 服务 (WebViewService)
- **Web 内容显示** - 基于 WebView2 的 Web 内容渲染
- **脚本交互** - 支持 JavaScript 与 C# 的双向调用
- **导航控制** - URL 导航、前进、后退、刷新
- **下载管理** - 文件下载处理

### 配置管理窗口 (ConfigManagerWindow)
- **可视化配置** - 图形化配置管理界面
- **配置项编辑** - 支持各类配置项的编辑
- **配置验证** - 配置项的合法性验证
- **配置导入导出** - 配置的备份和恢复

### 应用程序生命周期
- **启动初始化** - 应用程序启动时的初始化流程
- **配置加载** - 自动加载应用程序配置
- **模块初始化** - 初始化各功能模块
- **异常处理** - 全局异常捕获和处理
- **优雅退出** - 应用程序关闭时的资源释放

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                   ColorVision.UI.Desktop                      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │ MainWindow  │    │WebViewService│   │ConfigManager│      │
│  │             │    │             │    │   Window    │      │
│  │ • 主界面    │    │ • Web显示   │    │ • 配置编辑  │      │
│  │ • 模块容器  │    │ • 脚本交互  │    │ • 配置验证  │      │
│  │ • 生命周期  │    │ • 下载管理  │    │ • 导入导出  │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│         │                   │                   │            │
│         └───────────────────┼───────────────────┘            │
│                             ▼                                │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              ColorVision.UI / ColorVision.Solution       │ │
│  │                    上层功能模块                          │ │
│  └────────────────────────────────────────────────────────┘ │
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
- `Microsoft.Web.WebView2` - WebView2 控件

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
        
        // 初始化配置
        ConfigHandler.GetInstance();
        
        // 加载主题
        this.ApplyTheme(ThemeConfig.Instance.Theme);
        
        // 设置语言
        Thread.CurrentThread.CurrentUICulture = 
            new CultureInfo(LanguageConfig.Instance.UICulture);
        
        // 显示主窗口
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
```

### WebView 使用
```csharp
// 获取 WebView 服务
var webViewService = WebViewService.Instance;

// 导航到 URL
webViewService.Navigate("https://example.com");

// 执行 JavaScript
var result = await webViewService.ExecuteScriptAsync("document.title");

// 注册脚本回调
webViewService.RegisterScriptCallback("CSharpMethod", (args) =>
{
    Console.WriteLine($"收到 JS 调用: {args}");
    return "返回值";
});
```

### 配置管理窗口
```csharp
// 显示配置管理窗口
var configWindow = new ConfigManagerWindow();
configWindow.ShowDialog();
```

## 主要组件

### MainWindow
应用程序主窗口，提供模块容器和生命周期管理。

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // 窗口拖拽支持
        this.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        };
    }
}
```

### WebViewService
WebView2 封装服务，提供 Web 内容显示和交互功能。

```csharp
public class WebViewService
{
    public static WebViewService Instance { get; } = new WebViewService();
    
    public WebView2 WebView { get; private set; }
    
    public void Initialize(WebView2 webView);
    public void Navigate(string url);
    public void NavigateToString(string html);
    public Task<string> ExecuteScriptAsync(string script);
    public void RegisterScriptCallback(string name, Func<string, string> callback);
    public void GoBack();
    public void GoForward();
    public void Reload();
    
    public event EventHandler<NavigationStartingEventArgs> NavigationStarting;
    public event EventHandler<NavigationCompletedEventArgs> NavigationCompleted;
}
```

### ConfigManagerWindow
配置管理窗口，提供可视化的配置编辑功能。

```csharp
public partial class ConfigManagerWindow : Window
{
    public ConfigManagerWindow()
    {
        InitializeComponent();
        
        // 加载配置项
        LoadConfigurations();
    }
    
    private void LoadConfigurations()
    {
        // 显示所有可配置项
    }
    
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // 保存配置
        ConfigHandler.Instance.Save();
        DialogResult = true;
    }
}
```

### App
应用程序类，管理应用程序生命周期。

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 注册全局异常处理
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        
        // 初始化各模块
        InitializeModules();
        
        // 显示主窗口
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        // 保存配置
        ConfigHandler.Instance.Save();
        
        // 清理资源
        Cleanup();
        
        base.OnExit(e);
    }
}
```

## 目录说明

- `App.xaml/cs` - 应用程序定义和启动逻辑
- `MainWindow.xaml/cs` - 主窗口
- `ConfigManagerWindow.xaml/cs` - 配置管理窗口
- `WebViewService.cs` - WebView2 服务
- `AssemblyInfo.cs` - 程序集信息

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

### 1. 启动优化
- 延迟加载非核心模块
- 使用 SplashScreen 显示启动进度
- 异步初始化避免 UI 卡顿

### 2. 异常处理
- 注册全局异常处理器
- 记录异常日志
- 提供用户友好的错误提示

### 3. 资源管理
- 及时释放 WebView 资源
- 关闭时取消所有异步操作
- 保存用户配置和状态

### 4. WebView 使用
- 预加载常用页面
- 缓存优化
- 处理脚本注入安全

## 相关文档链接

- [WPF 应用程序开发指南](https://docs.microsoft.com/zh-cn/dotnet/desktop/wpf/)
- [WebView2 文档](https://docs.microsoft.com/zh-cn/microsoft-edge/webview2/)
- [解决方案管理](../../UI/ColorVision.Solution/README.md)

## 更新日志

### v1.5.1.1 (2025-02)
- 支持 .NET 10.0
- 优化 WebView2 初始化流程

### v1.4.1.1 (2025-02)
- 改进配置管理窗口
- 增加全局异常处理

### v1.3.18.1 (2025-02)
- 增加 WebView 服务
- 优化启动性能

## 维护者

ColorVision UI团队
