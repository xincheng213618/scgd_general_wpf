# PropertyGrid 动态属性系统

详细的 PropertyGrid 开发指南。

## 概述

ColorVision PropertyGrid 是一个功能强大的动态属性编辑系统，基于 Attribute 元数据驱动，支持多种数据类型和集合类型的可视化编辑。

## 支持的数据类型

### 基础类型
- **数值类型**: `int`, `float`, `double`, `decimal`, `long`, `short`, `byte` 等
- **字符串**: `string`
- **布尔值**: `bool`
- **枚举**: 任何 `enum` 类型

### 集合类型（完整支持）
- **List<T>** - 标准列表
- **ObservableCollection<T>** - 可观察集合，支持 WPF 数据绑定
- **Collection<T>** - 通用集合类
- **IList<T>** - 列表接口
- **ICollection<T>** - 集合接口
- **IEnumerable<T>** - 可枚举接口

### 字典类型
- **Dictionary<TKey, TValue>** - 标准字典
- **IDictionary<TKey, TValue>** - 字典接口

### 特殊类型
- **文件路径选择** - 通过 `TextSelectFilePropertiesEditor`
- **文件夹路径选择** - 通过 `TextSelectFolderPropertiesEditor`
- **Cron 表达式** - 通过 `CronExpressionPropertiesEditor`
- **串口选择** - 通过 `TextSerialPortPropertiesEditor`
- **波特率选择** - 通过 `TextBaudRatePropertiesEditor`

## 使用示例

### 基本用法

```csharp
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;

public class MyConfig
{
    [Category("基本属性")]
    [DisplayName("名称")]
    [Description("配置项的名称")]
    public string Name { get; set; } = "默认名称";

    [Category("基本属性")]
    [DisplayName("启用")]
    [Description("是否启用此配置")]
    public bool IsEnabled { get; set; } = true;

    [Category("基本属性")]
    [DisplayName("优先级")]
    [Description("配置的优先级")]
    public int Priority { get; set; } = 0;

    [Category("基本属性")]
    [DisplayName("类型")]
    [Description("配置的类型")]
    public MyEnum Type { get; set; } = MyEnum.Type1;
}

public enum MyEnum
{
    Type1,
    Type2,
    Type3
}
```

### 集合类型示例

```csharp
public class CollectionConfig
{
    [Category("集合")]
    [DisplayName("整数列表")]
    [Description("整数列表示例")]
    public List<int> Numbers { get; set; } = new List<int> { 1, 2, 3 };

    [Category("集合")]
    [DisplayName("可观察集合")]
    [Description("ObservableCollection 示例")]
    public ObservableCollection<string> Items { get; set; } = 
        new ObservableCollection<string> { "Item1", "Item2" };

    [Category("集合")]
    [DisplayName("IList 接口")]
    [Description("使用 IList 接口")]
    public IList<double> Values { get; set; } = new List<double> { 1.1, 2.2 };
}
```

### 字典类型示例

```csharp
public class DictionaryConfig
{
    [Category("字典")]
    [DisplayName("字符串映射")]
    [Description("字符串到整数的映射")]
    public Dictionary<string, int> StringToInt { get; set; } = 
        new Dictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 },
            { "three", 3 }
        };

    [Category("字典")]
    [DisplayName("枚举映射")]
    [Description("枚举到字符串的映射")]
    public Dictionary<MyEnum, string> EnumToString { get; set; } = 
        new Dictionary<MyEnum, string>
        {
            { MyEnum.Type1, "第一类型" },
            { MyEnum.Type2, "第二类型" }
        };

    [Category("字典")]
    [DisplayName("通用字典")]
    [Description("使用 IDictionary 接口")]
    public IDictionary<string, double> GenericDict { get; set; } = 
        new Dictionary<string, double>
        {
            { "pi", 3.14159 },
            { "e", 2.71828 }
        };
}
```

### 自定义编辑器

```csharp
public class CustomEditorConfig
{
    [Category("文件")]
    [DisplayName("配置文件")]
    [Description("选择配置文件")]
    [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
    public string ConfigFilePath { get; set; } = "";

    [Category("文件")]
    [DisplayName("输出目录")]
    [Description("选择输出目录")]
    [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
    public string OutputFolder { get; set; } = "";

    [Category("定时任务")]
    [DisplayName("Cron 表达式")]
    [Description("定时任务的 Cron 表达式")]
    [PropertyEditorType(typeof(CronExpressionPropertiesEditor))]
    public string CronExpression { get; set; } = "0 0 * * * ?";
}
```

## 属性特性（Attributes）

### CategoryAttribute
分组显示属性：
```csharp
[Category("基本设置")]
public string Name { get; set; }
```

### DisplayNameAttribute
设置显示名称：
```csharp
[DisplayName("用户名")]
public string UserName { get; set; }
```

### DescriptionAttribute
添加属性描述（显示为工具提示）：
```csharp
[Description("用户的登录名称")]
public string UserName { get; set; }
```

### BrowsableAttribute
控制属性是否显示：
```csharp
[Browsable(false)]
public string InternalProperty { get; set; }
```

### PropertyEditorTypeAttribute
指定自定义编辑器：
```csharp
[PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
public string FilePath { get; set; }
```

## 集合编辑器功能

### JSON 文本编辑
所有集合和字典类型都支持直接输入 JSON 格式：

**列表示例**：
```json
[1, 2, 3, 4, 5]
["apple", "banana", "cherry"]
```

**字典示例**：
```json
{"key1": "value1", "key2": "value2"}
{"one": 1, "two": 2, "three": 3}
```

### 可视化编辑器

#### 列表/集合编辑器
点击"编辑"按钮打开可视化编辑窗口，支持：
- **添加** - 添加新项
- **编辑** - 修改现有项（双击或点击编辑按钮）
- **删除** - 删除选中项
- **上移/下移** - 调整项的顺序

#### 字典编辑器
专用的键值对编辑窗口，支持：
- **添加** - 添加新的键值对
- **编辑** - 修改键或值
- **删除** - 删除选中的键值对
- **验证** - 自动检查键的唯一性

## 显示 PropertyEditor

### 在代码中使用

```csharp
using ColorVision.UI;

// 创建配置对象
var config = new MyConfig();

// 打开属性编辑器窗口
var editor = new PropertyEditorWindow(config, isEdit: true);
editor.ShowDialog();

// 获取修改后的配置
if (editor.DialogResult == true)
{
    // config 对象已被修改
}
```

### 嵌套对象

PropertyEditor 支持嵌套的 `INotifyPropertyChanged` 对象：

```csharp
public class ParentConfig : INotifyPropertyChanged
{
    [Category("子配置")]
    [DisplayName("数据库设置")]
    public DatabaseConfig Database { get; set; } = new DatabaseConfig();

    // INotifyPropertyChanged 实现...
}

public class DatabaseConfig : INotifyPropertyChanged
{
    [Category("连接")]
    [DisplayName("服务器地址")]
    public string Server { get; set; } = "localhost";

    [Category("连接")]
    [DisplayName("端口")]
    public int Port { get; set; } = 3306;

    // INotifyPropertyChanged 实现...
}
```

## 高级功能

### 条件显示属性

使用 `PropertyVisibilityAttribute` 控制属性的显示条件：

```csharp
public class ConditionalConfig
{
    [Category("设置")]
    [DisplayName("启用高级选项")]
    public bool EnableAdvanced { get; set; } = false;

    [Category("设置")]
    [DisplayName("高级参数")]
    [PropertyVisibility(nameof(EnableAdvanced))]
    public int AdvancedParameter { get; set; } = 0;
}
```

### 多语言支持

PropertyEditor 支持通过资源文件实现多语言：

```csharp
// 在资源文件 Properties/Resources.resx 中定义
// UserName = "用户名"
// Password = "密码"

public class LocalizedConfig
{
    [DisplayName("UserName")]  // 将从资源文件中查找
    public string UserName { get; set; }

    [DisplayName("Password")]
    public string Password { get; set; }
}
```

## 注意事项

1. **元素类型限制**
   - 集合的元素类型应该是基本类型、字符串、枚举或已注册的 PropertyEditor 类型
   - 复杂对象类型可能需要自定义编辑器

2. **字典键类型**
   - 建议使用字符串、整数或枚举作为键
   - 自定义类型需要正确实现 `Equals` 和 `GetHashCode`

3. **只读集合**
   - `IEnumerable<T>` 类型的属性在编辑时会创建临时列表
   - 编辑完成后会将结果转换回原类型

4. **线程安全**
   - 如果使用 `ObservableCollection`，注意在 UI 线程上修改

## 扩展开发

### 创建自定义编辑器

实现 `IPropertyEditor` 接口：

```csharp
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;

public class MyCustomEditor : IPropertyEditor
{
    static MyCustomEditor()
    {
        // 注册编辑器
        PropertyEditorHelper.RegisterEditor<MyCustomEditor>(t => t == typeof(MyCustomType));
    }

    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var dockPanel = new DockPanel();
        
        // 创建标签
        var label = PropertyEditorHelper.CreateLabel(property, 
            PropertyEditorHelper.GetResourceManager(obj));
        dockPanel.Children.Add(label);

        // 创建自定义控件
        var customControl = new MyCustomControl();
        var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
        customControl.SetBinding(MyCustomControl.ValueProperty, binding);
        
        dockPanel.Children.Add(customControl);
        return dockPanel;
    }
}
```

## 相关资源

- 源代码: `UI/ColorVision.UI/PropertyEditor/`
- 集合编辑器: `UI/ColorVision.UI/PropertyEditor/Editor/List/`
- 字典编辑器: `UI/ColorVision.UI/PropertyEditor/Editor/Dictionary/`
- 测试代码: `Test/ColorVision.UI.Tests/ExtendedCollectionEditorTests.cs`
- 详细文档: [PropertyEditor 集合支持文档](../../../PropertyEditor-Collection-Support.md)

## 参考文档

- [UI 开发指南](./README.md)
- [UI 组件 API](/04-api-reference/ui-components/README.md)
- [XAML 和 MVVM 模式](./xaml-mvvm.md)
