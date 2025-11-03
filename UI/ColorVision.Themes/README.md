# ColorVision.Themes

## 🎯 功能定位

主题管理和样式系统，提供ColorVision应用程序的视觉主题和自定义控件样式。

## 作用范围

UI视觉层，为整个应用程序提供统一的主题风格和自定义控件外观。

## 主要功能点

### 主题支持
- **深色主题** - 适合长时间使用的暗色调主题
- **浅色主题** - 明亮清晰的浅色调主题
- **粉色主题** - 柔和的粉色调主题
- **青色主题** - 清新的青色调主题
- **跟随系统** - 自动适配系统主题设置

### 自定义控件
- **上传控件** - 文件上传界面组件
- **下载控件** - 文件下载进度显示
- **消息弹窗** - 统一样式的消息提示窗口
- **对话框** - 自定义样式的对话框控件
- **按钮样式** - 多种按钮风格和状态
- **输入控件** - 文本框、下拉框等输入控件样式

### 主题切换
- **运行时切换** - 支持应用运行时动态切换主题
- **配置持久化** - 主题选择自动保存和恢复
- **平滑过渡** - 主题切换时的视觉过渡效果

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.UI - 引用主题资源和样式
- ColorVision - 主程序应用主题
- 所有插件和项目 - 继承主题样式

**引用的外部依赖**:
- WPF基础库
- ColorVision.Common - 配置接口

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.Themes\ColorVision.Themes.csproj" />
```

### 应用主题
```csharp
// 设置主题
this.ApplyTheme(ThemeConfig.Instance.Theme);

// 切换主题
ThemeConfig.Instance.Theme = ThemeType.Dark;
this.ApplyTheme(ThemeConfig.Instance.Theme);
```

### 在XAML中使用主题资源
```xaml
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

## 主题配置

### ThemeConfig设置
```csharp
public class ThemeConfig : IConfig
{
    public ThemeType Theme { get; set; } = ThemeType.Dark;
    public bool FollowSystem { get; set; } = false;
}
```

### 支持的主题类型
- `ThemeType.Dark` - 深色主题
- `ThemeType.Light` - 浅色主题
- `ThemeType.Pink` - 粉色主题
- `ThemeType.Cyan` - 青色主题

## 开发调试

```bash
dotnet build UI/ColorVision.Themes/ColorVision.Themes.csproj
```

## 目录说明

- `Themes/` - 主题资源文件目录
- `Controls/` - 自定义控件实现
- `Converters/` - 值转换器
- `Resources/` - 图像和图标资源

## 相关文档链接

- [主题开发指南](../../docs/04-api-reference/ui-components/ColorVision.Themes.md)
- [UI组件概览](../../docs/ui-components/UI组件概览.md)

## 维护者

ColorVision UI团队
