# UI 开发指南

本指南介绍如何使用 ColorVision 的 UI 框架进行界面开发。

## 概述

ColorVision UI 层基于 WPF 和 MVVM 模式构建，提供了丰富的自定义控件、主题系统和动态属性编辑器。

## UI 架构

### 主要组件

- **ColorVision.UI** - UI 基础框架和控件库
- **ColorVision.Themes** - 多主题支持系统
- **ColorVision.Common** - 通用 UI 组件
- **ColorVision.ImageEditor** - 专业图像编辑器
- **ColorVision.Solution** - 解决方案管理界面

### MVVM 模式

ColorVision 严格遵循 MVVM (Model-View-ViewModel) 模式：

- **Model**: 数据模型和业务逻辑（位于 Engine 层）
- **View**: XAML 界面定义
- **ViewModel**: 视图逻辑和数据绑定（实现 `INotifyPropertyChanged`）

## 核心功能

### 1. 主题系统

ColorVision 支持多主题切换：

- 深色主题（Dark）
- 浅色主题（Light）
- 粉色主题（Pink）
- 青色主题（Cyan）
- 跟随系统主题

详见：[主题与样式系统](./themes.md)

### 2. PropertyGrid 动态属性编辑器

ColorVision 提供元数据驱动的动态属性编辑系统：

- 基于 Attribute 的UI生成
- 支持各种编辑器类型（文本框、下拉框、滑块等）
- 属性分组和显示控制
- 数据验证

详见：[PropertyGrid 动态属性系统](./property-grid.md)

### 3. 多语言支持

- English、简体中文、繁体中文、日本語、한국어
- 跟随系统语言
- 运行时语言切换

### 4. 自定义控件

ColorVision 提供了丰富的自定义控件：

- 图像查看器
- 树形列表
- 数据网格
- 工具栏和菜单
- 对话框和窗口

详见：[自定义控件开发](./custom-controls.md)

## 开发流程

### 1. 创建 View (XAML)

```xml
<UserControl x:Class="YourNamespace.YourView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <!-- UI 元素 -->
    </Grid>
</UserControl>
```

### 2. 创建 ViewModel

```csharp
public class YourViewModel : INotifyPropertyChanged
{
    private string _text;
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            OnPropertyChanged();
        }
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

### 3. 绑定 DataContext

```xml
<UserControl.DataContext>
    <local:YourViewModel />
</UserControl.DataContext>
```

## 集成 ImageEditor

ColorVision 提供专业的图像编辑器组件，支持：

- 图像显示和缩放
- ROI 绘制
- 测量和标注
- 图层管理

详见：[ImageEditor 集成开发](./image-editor-integration.md)

## 最佳实践

### 1. 遵循 MVVM 模式

- View 不包含业务逻辑
- ViewModel 不直接操作 UI 元素
- 使用 Command 处理用户交互
- 使用数据绑定更新 UI

### 2. 使用依赖注入

```csharp
// 注册服务
ServiceManager.GetInstance().Add<IYourService, YourService>();

// 获取服务
var service = ServiceManager.GetInstance().GetService<IYourService>();
```

### 3. 资源管理

- 及时释放订阅和事件处理器
- 使用 `using` 语句管理可释放资源
- 避免内存泄漏

### 4. 性能优化

- 使用虚拟化控件（VirtualizingStackPanel）
- 延迟加载大数据
- 避免频繁的 UI 更新

## 相关文档

- [XAML 与 MVVM 模式](./xaml-mvvm.md)
- [PropertyGrid 动态属性系统](./property-grid.md)
- [自定义控件开发](./custom-controls.md)
- [ImageEditor 集成开发](./image-editor-integration.md)
- [主题与样式系统](./themes.md)
- [UI 组件 API 参考](/04-api-reference/ui-components/README.md)

## 示例项目

参考 ColorVision 主程序中的界面实现：

- `ColorVision/MainWindow.xaml` - 主窗口
- `ColorVision.UI/` - UI 控件库
- `ColorVision.Solution/` - 解决方案管理界面
- `ColorVision.ImageEditor/` - 图像编辑器

---

*更多技术细节请参考各子主题文档。*
