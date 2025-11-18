# 多编辑器选择功能说明

## 概述

ListItemEditorWindow 现在支持在一个类型有多个可用编辑器时，让用户选择使用哪个编辑器进行编辑。

## 功能特性

### 自动检测

系统会自动检测当前类型可用的所有编辑器：
- 如果只有一个编辑器：自动使用，不显示选择界面
- 如果有多个编辑器：显示下拉选择框，让用户选择

### String 类型的多编辑器支持

String 类型现在支持三种编辑器：

1. **文件选择器** (`TextSelectFilePropertiesEditor`)
   - TextBox 输入框
   - "..." 按钮：打开文件选择对话框
   - "🗁" 按钮：打开文件所在文件夹
   - 适用场景：选择文件路径

2. **文件夹选择器** (`TextSelectFolderPropertiesEditor`)
   - TextBox 输入框
   - "..." 按钮：打开文件夹选择对话框
   - "🗁" 按钮：打开文件夹
   - 适用场景：选择文件夹路径

3. **文本框** (`TextboxPropertiesEditor`)
   - 简单的 TextBox 输入框
   - 适用场景：纯文本输入

## 用户界面

### 单编辑器场景

当类型只有一个编辑器时（如 int, enum），界面保持简洁：

```
┌─────────────────────────────────────────────────┐
│ 编辑项                                     [X]   │
├─────────────────────────────────────────────────┤
│ Value                                           │
│ ┌───────────────────────────────────────┐      │
│ │ 42                                    │      │
│ └───────────────────────────────────────┘      │
├─────────────────────────────────────────────────┤
│                              [确定]    [取消]   │
└─────────────────────────────────────────────────┘
```

### 多编辑器场景

当类型有多个编辑器时（如 string），显示选择下拉框：

```
┌─────────────────────────────────────────────────┐
│ 编辑项                                     [X]   │
├─────────────────────────────────────────────────┤
│ 编辑器类型: [文件选择器 ▼]                      │
│             ├ 文件选择器                         │
│             ├ 文件夹选择器                       │
│             └ 文本框                             │
├─────────────────────────────────────────────────┤
│ Value                                           │
│ ┌────────────┬──────┬───┐                      │
│ │ TextBox    │ ... │ 🗁│                      │
│ └────────────┴──────┴───┘                      │
├─────────────────────────────────────────────────┤
│                              [确定]    [取消]   │
└─────────────────────────────────────────────────┘
```

### 切换编辑器

选择不同的编辑器后，下方的编辑界面会自动更新：

**选择"文件夹选择器"**:
```
┌─────────────────────────────────────────────────┐
│ 编辑器类型: [文件夹选择器 ▼]                    │
├─────────────────────────────────────────────────┤
│ Value                                           │
│ ┌────────────┬──────┬───┐                      │
│ │ TextBox    │ ... │ 🗁│  ← 文件夹选择按钮   │
│ └────────────┴──────┴───┘                      │
└─────────────────────────────────────────────────┘
```

**选择"文本框"**:
```
┌─────────────────────────────────────────────────┐
│ 编辑器类型: [文本框 ▼]                          │
├─────────────────────────────────────────────────┤
│ Value                                           │
│ ┌───────────────────────────────────────┐      │
│ │ 简单文本输入                          │      │
│ └───────────────────────────────────────┘      │
└─────────────────────────────────────────────────┘
```

## 技术实现

### 新增 API

在 `PropertyEditorHelper` 中新增方法：

```csharp
public static List<Type> GetAllEditorTypesForPropertyType(Type propertyType)
```

此方法返回指定类型的所有可用编辑器类型列表。

### 编辑器注册

对于 String 类型，手动添加了三个编辑器：

```csharp
if (elementType == typeof(string))
{
    editorTypes.Add(typeof(TextSelectFilePropertiesEditor));
    editorTypes.Add(typeof(TextSelectFolderPropertiesEditor));
    editorTypes.Add(typeof(TextboxPropertiesEditor));
}
```

### 编辑器名称映射

为了用户友好，将编辑器类型映射到中文名称：

```csharp
var nameMap = new Dictionary<Type, string>
{
    { typeof(TextSelectFilePropertiesEditor), "文件选择器" },
    { typeof(TextSelectFolderPropertiesEditor), "文件夹选择器" },
    { typeof(TextboxPropertiesEditor), "文本框" },
    { typeof(EnumPropertiesEditor), "下拉选择" },
    { typeof(BoolPropertiesEditor), "复选框" }
};
```

### 动态 UI 创建

当用户选择不同的编辑器时：

1. 清空当前的编辑器面板 (`EditorPanel.Children.Clear()`)
2. 获取选中的编辑器类型
3. 使用 `PropertyEditorHelper.GetOrCreateEditor()` 创建编辑器实例
4. 调用 `editor.GenProperties()` 生成 UI
5. 将生成的 UI 添加到面板中

## 扩展指南

### 为其他类型添加多编辑器支持

如果想为其他类型（如 int）添加多编辑器支持：

1. 在 `GetAvailableEditorTypes()` 方法中添加逻辑：

```csharp
else if (elementType == typeof(int))
{
    editorTypes.Add(typeof(TextboxPropertiesEditor));
    editorTypes.Add(typeof(SliderPropertiesEditor)); // 假设有这个编辑器
}
```

2. 在 `GetEditorDisplayName()` 中添加名称映射：

```csharp
{ typeof(SliderPropertiesEditor), "滑块" }
```

3. 确保相应的 PropertyEditor 已在系统中注册

### 创建新的 PropertyEditor

要创建一个新的 PropertyEditor 并支持多编辑器选择：

1. 实现 `IPropertyEditor` 接口
2. 在静态构造函数中注册编辑器（可选）
3. 在 `GetAvailableEditorTypes()` 中添加到对应类型的列表
4. 在 `GetEditorDisplayName()` 中添加友好名称

示例：

```csharp
public class MyCustomStringEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        // 创建自定义 UI
    }
}

// 在 GetAvailableEditorTypes() 中添加
if (elementType == typeof(string))
{
    editorTypes.Add(typeof(TextSelectFilePropertiesEditor));
    editorTypes.Add(typeof(TextSelectFolderPropertiesEditor));
    editorTypes.Add(typeof(TextboxPropertiesEditor));
    editorTypes.Add(typeof(MyCustomStringEditor)); // 新增
}

// 在 GetEditorDisplayName() 中添加
{ typeof(MyCustomStringEditor), "自定义编辑器" }
```

## 使用场景

### 场景 1: 文件路径列表

用户有一个文件路径列表，需要编辑：

```csharp
public List<string> FilePaths { get; set; } = new()
{
    @"C:\Temp\file1.txt",
    @"C:\Temp\file2.txt"
};
```

编辑时可以选择"文件选择器"，通过对话框方便地选择文件。

### 场景 2: 文件夹路径列表

用户有一个文件夹路径列表：

```csharp
public List<string> FolderPaths { get; set; } = new()
{
    @"C:\Projects\Project1",
    @"C:\Projects\Project2"
};
```

编辑时可以选择"文件夹选择器"，通过对话框选择文件夹。

### 场景 3: 普通字符串列表

用户有一个普通字符串列表，只需简单输入：

```csharp
public List<string> Tags { get; set; } = new()
{
    "标签1", "标签2", "标签3"
};
```

编辑时可以选择"文本框"，直接输入文本。

## 优势

1. **灵活性**: 用户可以根据实际需求选择最合适的编辑器
2. **易用性**: 中文名称，界面友好
3. **一致性**: 与现有 PropertyEditor 系统完美集成
4. **可扩展**: 容易为新类型添加多编辑器支持
5. **智能**: 只有一个编辑器时自动隐藏选择界面

## 性能

- 编辑器实例缓存在 `PropertyEditorHelper.CustomEditorCache` 中
- 切换编辑器时仅重建 UI，不重新创建编辑器实例
- 轻量级实现，对性能影响最小

## 兼容性

- 完全向后兼容
- 不影响现有的单编辑器类型
- 可选功能，逐步启用

## 总结

多编辑器选择功能为用户提供了更大的灵活性，特别是对于 String 类型，可以根据实际使用场景（文件路径、文件夹路径或纯文本）选择最合适的编辑器。这个功能与现有系统无缝集成，易于扩展，是一个强大的用户体验改进。
