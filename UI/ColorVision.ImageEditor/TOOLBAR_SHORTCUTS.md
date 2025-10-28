# ImageView 工具栏快捷键和可见性控制

## 功能概述

为 ImageView 添加了工具栏快捷键控制和可见性管理功能，允许用户通过键盘快捷键或设置窗口控制各个工具栏和工具的显示/隐藏。

## 快捷键

### 工具栏快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+Shift+A` | 显示所有工具栏 |
| `Ctrl+Shift+H` | 隐藏所有工具栏 |
| `Ctrl+Q` | 打开工具栏设置窗口 |

**注意：** 个别工具栏的单独切换快捷键已移除。请使用工具栏设置窗口来单独控制每个工具栏的显示/隐藏。

## 工具栏设置窗口

### 打开方式

1. 使用快捷键 `Ctrl+Q`
2. 以编程方式调用 `ImageView.OpenToolbarSettingsWindow()`

### 功能

工具栏设置窗口提供以下功能：

1. **工具栏可见性控制**
   - 可以独立控制每个工具栏的显示/隐藏
   - ToolBarDraw (绘制工具栏) 仅在编辑模式 (ImageEditMode) 下显示

2. **编辑器工具可见性控制**
   - 列出所有注册的 IEditorTool
   - 显示每个工具所在的工具栏位置
   - 可以单独控制每个工具的显示/隐藏
   - 工具可见性设置会持久化保存

3. **批量操作**
   - "全部显示" 按钮：显示所有工具栏和工具
   - "全部隐藏" 按钮：隐藏所有工具栏和工具

## 配置持久化

### ImageViewConfig 新增属性

工具栏可见性状态保存在 `ImageViewConfig` 中：

```csharp
public bool IsToolBarAlVisible { get; set; }      // 底部工具栏
public bool IsToolBarDrawVisible { get; set; }    // 绘制工具栏
public bool IsToolBarTopVisible { get; set; }     // 顶部工具栏
public bool IsToolBarLeftVisible { get; set; }    // 左侧工具栏
public bool IsToolBarRightVisible { get; set; }   // 右侧工具栏
```

### EditorToolVisibilityConfig

单个工具的可见性状态保存在 `EditorToolVisibilityConfig` 中：

```csharp
// 获取工具可见性配置
var visibilityConfig = imageView.Config.GetRequiredService<EditorToolVisibilityConfig>();

// 获取特定工具的可见性
bool isVisible = visibilityConfig.GetToolVisibility("ToolGuidId");

// 设置特定工具的可见性
visibilityConfig.SetToolVisibility("ToolGuidId", true);
```

## 实现细节

### 1. XAML 绑定

#### 普通工具栏
工具栏的 Visibility 属性绑定到配置：

```xml
<ToolBarTray x:Name="ToolBarTop" 
             Visibility="{Binding Config.IsToolBarTopVisible,Converter={StaticResource bool2VisibilityConverter}}"
             ...>
```

#### ToolBarDraw (绘制工具栏)
ToolBarDraw 使用 MultiBinding 来同时检查 ImageEditMode 和 IsToolBarDrawVisible：

```xml
<ToolBarTray x:Name="ToolBarDraw" ...>
    <ToolBarTray.Visibility>
        <MultiBinding Converter="{StaticResource BooleanAndToVisibilityConverter}">
            <Binding Path="ImageEditMode"/>
            <Binding Path="Config.IsToolBarDrawVisible"/>
        </MultiBinding>
    </ToolBarTray.Visibility>
    ...
</ToolBarTray>
```

这确保 ToolBarDraw 仅在以下两个条件同时满足时显示：
1. ImageEditMode = true (编辑模式已启用)
2. Config.IsToolBarDrawVisible = true (配置中设置为可见)

### 2. BooleanAndToVisibilityConverter

新增的多值转换器，执行逻辑 AND 操作：

```csharp
public class BooleanAndToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, ...)
    {
        // 所有值都为 true 时返回 Visible，否则返回 Collapsed
        bool allTrue = values.All(v => v is true);
        return allTrue ? Visibility.Visible : Visibility.Collapsed;
    }
}
```

### 3. 键盘快捷键

在 `ImageView.UserControl_Initialized` 中设置快捷键：

```csharp
private void SetupToolbarToggleCommands()
{
    // 显示所有工具栏 (Ctrl+Shift+A)
    var showAllToolbarsCommand = new RoutedCommand();
    CommandBindings.Add(new CommandBinding(showAllToolbarsCommand, ...));
    InputBindings.Add(new KeyBinding(showAllToolbarsCommand, Key.A, ModifierKeys.Control | ModifierKeys.Shift));
    
    // 隐藏所有工具栏 (Ctrl+Shift+H)
    var hideAllToolbarsCommand = new RoutedCommand();
    CommandBindings.Add(new CommandBinding(hideAllToolbarsCommand, ...));
    InputBindings.Add(new KeyBinding(hideAllToolbarsCommand, Key.H, ModifierKeys.Control | ModifierKeys.Shift));
    
    // 打开工具栏设置窗口 (Ctrl+Q)
    var openToolbarSettingsCommand = new RoutedCommand();
    CommandBindings.Add(new CommandBinding(openToolbarSettingsCommand, ...));
    InputBindings.Add(new KeyBinding(openToolbarSettingsCommand, Key.Q, ModifierKeys.Control));
}
```

### 4. 工具UI元素映射

`EditorToolFactory` 维护工具到UI元素的映射：

```csharp
public Dictionary<string, FrameworkElement> ToolUIElements { get; set; }
```

这使得可以在运行时动态控制单个工具的可见性。

## 使用示例

### 以编程方式控制工具栏

```csharp
// 隐藏顶部工具栏
imageView.Config.IsToolBarTopVisible = false;

// 显示绘制工具栏
imageView.Config.IsToolBarDrawVisible = true;
```

### 以编程方式控制工具

```csharp
var visibilityConfig = imageView.Config.GetRequiredService<EditorToolVisibilityConfig>();

// 隐藏特定工具
visibilityConfig.SetToolVisibility("ZoomInEditorTool", false);

// 显示特定工具
visibilityConfig.SetToolVisibility("ZoomOutEditorTool", true);
```

## 注意事项

1. 配置更改会立即生效
2. 工具可见性设置会在 ImageViewConfig 中持久化
3. 隐藏的工具仍然存在于工具集合中，只是UI上不可见
4. 键盘快捷键仅在 ImageView 控件具有焦点时有效
5. **重要：** ToolBarDraw (绘制工具栏) 仅在编辑模式 (ImageEditMode) 启用时显示，这是为了保持原有的设计逻辑

## 变更记录

### 2025-10-28
- 移除了单独工具栏的切换快捷键 (Ctrl+Shift+1~5)
- 添加了 "显示所有" 快捷键 (Ctrl+Shift+A)
- 添加了 "隐藏所有" 快捷键 (Ctrl+Shift+H)
- 修改了打开设置窗口的快捷键为 Ctrl+Q
- 实现了 ToolBarDraw 的条件显示：仅在 ImageEditMode 和 IsToolBarDrawVisible 都为 true 时显示
- 添加了 BooleanAndToVisibilityConverter 用于多条件可见性控制

## 未来改进

- [ ] 添加工具栏位置自定义
- [ ] 支持用户自定义快捷键
- [ ] 添加预设配置（如"最小化视图"、"完整视图"等）
- [ ] 工具栏布局的导入/导出功能
