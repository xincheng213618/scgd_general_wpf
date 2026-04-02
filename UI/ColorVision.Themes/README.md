# ColorVision.Themes

> 版本: 1.5.1.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

## 🎯 功能定位

ColorVision 系统的主题管理和样式系统，提供多种预设主题方案和自定义UI控件。封装了基于系统、黑色、白色、粉色、青色等几种主题方案，支持通过 ApplyTheme 方法直接切换主题。

## 作用范围

UI视觉层，为整个应用程序提供统一的主题风格和自定义控件外观，支持深色、浅色、粉色、青色等多种主题，以及系统主题跟随。

## 主要功能点

### 主题管理 (ThemeManager)
- **五种预设主题** - 系统主题、浅色、深色、粉色、青色
- **系统主题跟随** - 自动适配 Windows 系统主题设置
- **动态切换** - 运行时动态切换主题，无需重启应用
- **标题栏颜色** - 支持设置窗口标题栏颜色以匹配主题
- **配置持久化** - 主题选择自动保存和恢复

### 主题资源
- **基础资源** - 所有主题共享的基础样式（Base.xaml、Menu.xaml、GroupBox.xaml、Icons.xaml）
- **深色主题** - 护眼的深色背景主题（Dark.xaml）
- **浅色主题** - 明亮清晰的浅色主题（White.xaml）
- **粉色主题** - 温暖的粉色调主题（Pink.xaml，标题栏颜色 #E8A6C1）
- **青色主题** - 清新的青色调主题（Cyan.xaml，标题栏颜色 #00796B）
- **HandyControl集成** - 集成 HandyControl 3.5.1 主题资源
- **WPF-UI集成** - 集成 WPF-UI 4.2.0 主题资源

### 自定义控件
- **MessageBox** - 主题化的消息对话框
- **ProgressRing** - 进度环控件
- **LoadingOverlay** - 加载遮罩层
- **UploadControl** - 文件上传控件，支持拖拽和文件选择
- **UploadMsg** - 上传消息控件
- **UploadWindow** - 上传窗口

### 转换器 (Converters)
- **BooleanToVisibilityReConverter** - 布尔到可见性反向转换
- **InverseBooleanConverter** - 布尔反向转换
- **MemorySizeConverter** - 内存大小转换
- **EnumToVisibilityConverter** - 枚举到可见性转换
- **IntToVisibilityConverter** - 整数到可见性转换
- **DescriptioConverter** - 描述转换
- **WidthToBooleanConverter** - 宽度到布尔转换

### 窗口样式
- **BaseWindow** - 基础窗口样式
- **WindowHelper** - 窗口帮助类
- **WindowBlur** - 窗口模糊效果

## 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                    ColorVision.Themes                         │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │ThemeManager │    │  主题资源   │    │  自定义控件  │      │
│  │             │    │             │    │             │      │
│  │ • 主题切换  │    │ • Base.xaml │    │ • MessageBox│      │
│  │ • 系统监听  │    │ • Dark.xaml │    │ • Progress  │      │
│  │ • 标题栏   │    │ • White.xaml│    │ • Upload    │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐                          │
│  │  转换器    │    │  窗口样式   │                          │
│  │             │    │             │                          │
│  │ • Bool2Vis  │    │ • BaseWindow│                          │
│  │ • Inverse   │    │ • WindowBlur│                          │
│  │ • Memory    │    │ • Helper    │                          │
│  └─────────────┘    └─────────────┘                          │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.UI - 引用主题资源和样式
- ColorVision.UI.Desktop - 主程序应用主题
- 所有插件和项目 - 继承主题样式

**引用的外部依赖**:
- WPF基础库
- ColorVision.Common - 配置接口
- HandyControl 3.5.1
- WPF-UI 4.2.0

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Themes\ColorVision.Themes.csproj" />
```

### 应用主题
```csharp
// 设置主题
ThemeConfig.Instance.Theme = ThemeType.Dark;
ThemeConfig.Instance.Save();

// 应用主题
Application.Current.ApplyTheme(ThemeConfig.Instance.Theme);

// 更新窗口标题栏颜色
foreach (Window window in Application.Current.Windows)
{
    var hwnd = new WindowInteropHelper(window).Handle;
    ThemeManager.SetWindowTitleBarColor(hwnd, ThemeConfig.Instance.Theme);
}
```

### 在XAML中使用主题资源
```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Light" />
                <ui:ControlsDictionary />
                <ResourceDictionary Source="/HandyControl;component/Themes/basic/colors/colors.xaml"/>
                <ResourceDictionary Source="/HandyControl;component/Themes/Theme.xaml"/>
                <ResourceDictionary Source="/ColorVision.Themes;component/Themes/White.xaml"/>
                <ResourceDictionary Source="/ColorVision.Themes;component/Themes/Base.xaml"/>
                <ResourceDictionary Source="/ColorVision.Themes;component/Themes/Menu.xaml"/>
                <ResourceDictionary Source="/ColorVision.Themes;component/Themes/GroupBox.xaml"/>
                <ResourceDictionary Source="/ColorVision.Themes;component/Themes/Icons.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Window>
```

### 使用上传控件
```xml
<cv:UploadControl x:Name="uploadControl"
                  Filter="图像文件|*.jpg;*.png;*.bmp"
                  SelectChaned="OnFileSelected"/>
```

```csharp
private void OnFileSelected(object sender, EventArgs e)
{
    var control = sender as UploadControl;
    string filePath = control.UploadFilePath;
    string fileName = control.UploadFileName;
    // 处理文件
}
```

### 显示加载遮罩
```xml
<Grid>
    <DataGrid ItemsSource="{Binding Data}"/>
    <cv:LoadingOverlay IsLoading="{Binding IsLoading}" 
                       Message="正在加载数据..."/>
</Grid>
```

## 主要组件

### ThemeManager
主题管理器是主题系统的核心类，负责主题切换、系统主题监听和窗口标题栏颜色设置。

```csharp
public class ThemeManager
{
    public static ThemeManager Current { get; set; } = new ThemeManager();
    
    // 当前选择的主题
    public Theme? CurrentTheme { get; private set; }
    
    // 当前应用的主题
    public Theme CurrentUITheme { get; private set; }
    
    // Windows应用的主题
    public Theme AppsTheme { get; set; }
    
    // 任务栏的主题
    public Theme SystemTheme { get; set; }
    
    // 应用主题
    public void ApplyTheme(Application app, Theme theme);
    
    // 设置窗口标题栏颜色
    public static void SetWindowTitleBarColor(IntPtr hwnd, Theme theme);
    
    // 事件
    public event ThemeChangedHandler? CurrentThemeChanged;
    public event ThemeChangedHandler? CurrentUIThemeChanged;
    public event ThemeChangedHandler? AppsThemeChanged;
    public event ThemeChangedHandler? SystemThemeChanged;
}

public enum Theme
{
    UseSystem,
    Light,
    Dark,
    Pink,
    Cyan
}
```

### UploadControl
文件上传控件，支持拖拽和文件选择：

```csharp
public partial class UploadControl : UserControl
{
    // 上传文件名
    public string UploadFileName { get; set; }
    
    // 上传文件路径
    public string UploadFilePath { get; set; }
    
    // 文件过滤器
    public string Filter { get; set; }
    
    // 选择变更事件
    public event EventHandler SelectChaned;
    
    // 选择文件
    public void ChoiceFile();
}
```

### ThemeConfig
主题配置类，管理应用程序主题设置：

```csharp
public class ThemeConfig : IConfig
{
    public static ThemeConfig Instance => 
        ConfigService.Instance.GetRequiredService<ThemeConfig>();
    
    // 当前主题
    public ThemeType Theme { get; set; }
    
    // 是否跟随系统主题
    public bool FollowSystem { get; set; }
}
```

## 目录说明

- `Themes/` - 主题资源文件目录
  - `Base.xaml` - 基础共享资源
  - `Dark.xaml` - 深色主题
  - `White.xaml` - 浅色主题
  - `Pink.xaml` - 粉色主题
  - `Cyan.xaml` - 青色主题
  - `HPink.xaml` - HandyControl粉色主题
  - `HCyan.xaml` - HandyControl青色主题
  - `Window/` - 窗口样式
    - `BaseWindow.cs` - 基础窗口
    - `WindowHelper.cs` - 窗口帮助
    - `WindowBlur.cs` - 窗口模糊
- `Controls/` - 自定义控件
  - `MessageBox.cs` - 消息框
  - `MessageBoxWindow.xaml` - 消息框窗口
  - `ProgressRing.xaml` - 进度环
  - `LoadingOverlay.xaml` - 加载遮罩
  - `Uploads/` - 上传控件
    - `Upload.xaml` - 上传控件
    - `UploadMsg.xaml` - 上传消息
    - `UploadWindow.xaml` - 上传窗口
    - `FileUploadInfo.cs` - 文件上传信息
    - `UploadStatus.cs` - 上传状态
- `Converter/` - 值转换器
  - `BooleanToVisibilityReConverter.cs`
  - `InverseBooleanConverter.cs`
  - `MemorySizeConverter.cs`
  - `EnumToVisibilityConverter.cs`
  - `IntToVisibilityConverter.cs`
  - `DescriptioConverter.cs`
  - `WidthToBooleanConverter.cs`
- `NativeMethods/` - 原生方法
  - `Dwmapi.cs` - DWM API
  - `Keyboard.cs` - 键盘
  - `User32.cs` - User32 API
- `Utilities/` - 工具类
  - `EnumUtils.cs` - 枚举工具
  - `MemorySize.cs` - 内存大小
  - `ImageUtils.cs` - 图像工具

## 开发调试

```bash
# 构建项目
dotnet build UI/ColorVision.Themes/ColorVision.Themes.csproj

# 运行测试
dotnet test
```

## 最佳实践

### 1. 主题一致性
- 保持整个应用程序使用统一的主题资源
- 避免在不同模块中定义重复的颜色和样式
- 使用共享的资源字典

### 2. 颜色对比度
- 文本与背景的对比度至少 4.5:1 (WCAG AA 标准)
- 大字体 (18pt+) 对比度至少 3:1

### 3. 避免主题切换卡顿
- 避免在主题切换时执行重操作
- 使用异步主题切换

### 4. 主题配置持久化
```csharp
public class ThemeConfig : IConfig
{
    public Theme Theme { get; set; } = Theme.Dark;
    public bool FollowSystem { get; set; } = false;
    
    public void Save()
    {
        ConfigHandler.SaveConfig(this);
    }
}
```

## 相关文档链接

- [详细技术文档](../../docs/04-api-reference/ui-components/ColorVision.Themes.md)
- [ColorVision.UI README](./../ColorVision.UI/README.md)
- [WPF 主题和样式官方文档](https://docs.microsoft.com/zh-cn/dotnet/desktop/wpf/controls/styling-and-templating)
- [HandyControl 主题库](https://github.com/HandyOrg/HandyControl)

## 更新日志

### v1.5.1.1（2026-02）
- 升级目标框架至 .NET 8.0 / .NET 10.0
- 集成 HandyControl 3.5.1 和 WPF-UI 4.2.0
- 新增 Windows 11 风格系统主题适配
- 支持 Windows Forms 控件主题一致性
- 优化主题切换性能

### v1.4.x 及更早
- 基础五主题支持（系统、浅色、深色、粉色、青色）
- ApplyTheme 动态切换机制
- 自定义控件样式库

## 维护者

ColorVision UI团队
