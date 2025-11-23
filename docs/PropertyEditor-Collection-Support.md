# 使用指南：扩展的集合类型支持

## 概述

ColorVision PropertyEditor 现已支持以下集合类型：
- `List<T>` (原有)
- `ObservableCollection<T>` (新增)
- `Collection<T>` (新增)
- `IList<T>` (新增)
- `ICollection<T>` (新增)
- `IEnumerable<T>` (新增)
- `Dictionary<TKey, TValue>` (新增)
- `IDictionary<TKey, TValue>` (新增)

## 使用示例

### 1. ObservableCollection 支持

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;

public class MyConfig
{
    [Category("集合")]
    [DisplayName("整数集合")]
    [Description("使用 ObservableCollection 的整数集合")]
    public ObservableCollection<int> Numbers { get; set; } = new ObservableCollection<int> { 1, 2, 3 };
    
    [Category("集合")]
    [DisplayName("字符串集合")]
    public ObservableCollection<string> Names { get; set; } = new ObservableCollection<string> { "张三", "李四", "王五" };
}
```

### 2. Collection 支持

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;

public class MyConfig
{
    [Category("集合")]
    [DisplayName("数据集合")]
    public Collection<double> Values { get; set; } = new Collection<double> { 1.1, 2.2, 3.3 };
}
```

### 3. 接口类型支持 (IList, ICollection, IEnumerable)

```csharp
using System.Collections.Generic;
using System.ComponentModel;

public class MyConfig
{
    [Category("接口集合")]
    [DisplayName("IList 整数")]
    public IList<int> IntList { get; set; } = new List<int> { 10, 20, 30 };
    
    [Category("接口集合")]
    [DisplayName("ICollection 字符串")]
    public ICollection<string> StringCollection { get; set; } = new List<string> { "A", "B", "C" };
    
    [Category("接口集合")]
    [DisplayName("IEnumerable 双精度")]
    public IEnumerable<double> DoubleEnumerable { get; set; } = new List<double> { 1.1, 2.2 };
}
```

### 4. Dictionary 支持

```csharp
using System.Collections.Generic;
using System.ComponentModel;

public class MyConfig
{
    [Category("字典")]
    [DisplayName("字符串到整数映射")]
    [Description("键为字符串，值为整数的字典")]
    public Dictionary<string, int> StringToInt { get; set; } = new Dictionary<string, int>
    {
        { "one", 1 },
        { "two", 2 },
        { "three", 3 }
    };
    
    [Category("字典")]
    [DisplayName("整数到字符串映射")]
    public Dictionary<int, string> IntToString { get; set; } = new Dictionary<int, string>
    {
        { 1, "一" },
        { 2, "二" },
        { 3, "三" }
    };
    
    [Category("字典")]
    [DisplayName("枚举到字符串映射")]
    public Dictionary<MyEnum, string> EnumToString { get; set; } = new Dictionary<MyEnum, string>
    {
        { MyEnum.Value1, "第一个" },
        { MyEnum.Value2, "第二个" }
    };
}

public enum MyEnum
{
    Value1,
    Value2,
    Value3
}
```

### 5. IDictionary 接口支持

```csharp
using System.Collections.Generic;
using System.ComponentModel;

public class MyConfig
{
    [Category("字典")]
    [DisplayName("通用字典")]
    public IDictionary<string, double> GenericDict { get; set; } = new Dictionary<string, double>
    {
        { "pi", 3.14159 },
        { "e", 2.71828 }
    };
}
```

## 功能特性

### JSON 文本输入
所有支持的集合类型都可以通过 JSON 格式的文本框直接输入：

**列表/集合示例：**
```json
[1, 2, 3, 4, 5]
["apple", "banana", "cherry"]
[1.1, 2.2, 3.3]
```

**字典示例：**
```json
{"key1": "value1", "key2": "value2"}
{"one": 1, "two": 2, "three": 3}
```

### 可视化编辑器

每种类型都有对应的可视化编辑器：

#### 列表/集合编辑器
- **添加**：添加新项
- **编辑**：修改现有项（双击或点击编辑按钮）
- **删除**：删除选中项
- **上移/下移**：调整项的顺序（仅适用于列表）

#### 字典编辑器
- **添加**：添加新的键值对
- **编辑**：修改键或值（双击或点击编辑按钮）
- **删除**：删除选中的键值对
- **键唯一性验证**：自动检查键是否重复

## 注意事项

1. **元素类型限制**：
   - 集合的元素类型应该是数值类型、字符串、枚举或其他支持 PropertyEditor 的类型
   - 复杂对象类型可能需要自定义编辑器

2. **只读集合**：
   - `IEnumerable<T>` 类型的属性在编辑时会创建临时列表
   - 编辑完成后会将结果转换回原类型

3. **键类型**：
   - 字典的键类型建议使用字符串、整数或枚举
   - 自定义类型需要正确实现 `Equals` 和 `GetHashCode`

4. **线程安全**：
   - 如果使用 `ObservableCollection`，注意在 UI 线程上修改

## 完整示例

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MyApp
{
    public class CompleteExample
    {
        [Category("列表集合")]
        [DisplayName("普通列表")]
        public List<int> RegularList { get; set; } = new List<int> { 1, 2, 3 };
        
        [Category("列表集合")]
        [DisplayName("可观察集合")]
        public ObservableCollection<string> ObservableItems { get; set; } = 
            new ObservableCollection<string> { "Item1", "Item2" };
        
        [Category("列表集合")]
        [DisplayName("通用集合")]
        public Collection<double> CollectionItems { get; set; } = 
            new Collection<double> { 1.1, 2.2 };
        
        [Category("接口集合")]
        [DisplayName("IList 接口")]
        public IList<int> InterfaceList { get; set; } = new List<int> { 10, 20 };
        
        [Category("接口集合")]
        [DisplayName("ICollection 接口")]
        public ICollection<string> InterfaceCollection { get; set; } = 
            new List<string> { "A", "B" };
        
        [Category("字典映射")]
        [DisplayName("字符串字典")]
        public Dictionary<string, int> StringDictionary { get; set; } = 
            new Dictionary<string, int>
            {
                { "first", 1 },
                { "second", 2 }
            };
        
        [Category("字典映射")]
        [DisplayName("IDictionary 接口")]
        public IDictionary<int, string> InterfaceDictionary { get; set; } = 
            new Dictionary<int, string>
            {
                { 1, "one" },
                { 2, "two" }
            };
    }
    
    // 使用 PropertyEditorWindow 显示配置
    public void ShowEditor()
    {
        var config = new CompleteExample();
        var window = new ColorVision.UI.PropertyEditorWindow(config, isEdit: true);
        window.ShowDialog();
    }
}
```

## 测试

项目包含完整的单元测试，位于 `Test/ColorVision.UI.Tests/ExtendedCollectionEditorTests.cs`。
测试覆盖了所有新增的集合类型和字典类型。
