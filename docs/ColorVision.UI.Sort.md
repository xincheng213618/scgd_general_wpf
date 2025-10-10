# ColorVision.UI Sort 功能文档

## 📋 概述 (Overview)

ColorVision.UI Sort 模块提供了强大的集合排序功能，支持两种方式：
1. **接口定义排序** - 通过实现特定接口（ISortID, ISortKey, ISortBatch, ISortBatchID）
2. **通用反射排序** - 使用反射机制，无需实现特定接口（推荐）

ColorVision.UI Sort module provides powerful collection sorting capabilities with two approaches:
1. **Interface-Based Sorting** - By implementing specific interfaces (ISortID, ISortKey, ISortBatch, ISortBatchID)
2. **Universal Reflection-Based Sorting** - Using reflection, no specific interface required (Recommended)

## 🎯 核心功能 (Core Features)

### 1. 接口定义排序 (Interface-Based Sorting)

#### ISortID - ID 排序接口
```csharp
public interface ISortID
{
    public int Id { get; }
}

// 使用示例
public class MyItem : ISortID
{
    public int Id { get; set; }
    public string Name { get; set; }
}

var collection = new ObservableCollection<MyItem>();
collection.SortByID(descending: false);  // 升序
collection.SortByID(descending: true);   // 降序
```

#### ISortKey - Key 排序接口（逻辑排序）
```csharp
public interface ISortKey
{
    public string Key { get; }
}

// 使用示例 - 支持自然排序（Item1, Item2, Item10）
public class MyItem : ISortKey
{
    public string Key { get; set; }
}

var collection = new ObservableCollection<MyItem>();
collection.SortByKey(descending: false);  // 使用逻辑比较
```

#### ISortBatch - Batch 排序接口
```csharp
public interface ISortBatch
{
    string? Batch { get; set; }
}

// 使用示例
var collection = new ObservableCollection<MyItem>();
collection.SortByBatch(descending: false);
```

#### ISortBatchID - BatchID 排序接口（支持可空）
```csharp
public interface ISortBatchID
{
    int? BatchID { get; set; }
}

// 使用示例
var collection = new ObservableCollection<MyItem>();
collection.SortByBatchID(descending: false);
```

### 2. 通用反射排序 (Universal Reflection-Based Sorting) ⭐推荐

通用排序扩展不需要实现任何特定接口，提供更大的灵活性。

Universal sorting extensions don't require implementing any specific interface, providing greater flexibility.

#### 基本用法 (Basic Usage)

```csharp
// 定义任意类，无需实现特定接口
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedDate { get; set; }
}

var products = new ObservableCollection<Product>();

// 1. 按属性名排序
products.SortBy("Id", descending: false);
products.SortBy("Price", descending: true);
products.SortBy("Name", descending: false);

// 2. 使用 Lambda 表达式
products.SortBy(x => x.Price, descending: false);
products.SortBy(x => x.CreatedDate, descending: true);
```

#### 多级排序 (Multi-Level Sorting)

```csharp
// 按多个属性排序
products.SortByMultiple(
    ("Price", false),      // 首先按价格升序
    ("CreatedDate", true)  // 然后按创建日期降序
);
```

#### 智能排序 (Smart Sorting)

智能排序会自动检测并使用以下优先级的属性：
1. Id
2. Key
3. Name
4. Title
5. Order
6. Index

```csharp
// 自动检测并使用合适的排序属性
products.SmartSort(descending: false);
```

#### 添加唯一元素 (Add Unique Items)

```csharp
// 基于指定键添加唯一元素
var newProduct = new Product { Id = 1, Name = "New" };
products.AddUniqueBy(newProduct, x => x.Id);

// 在开头插入
products.AddUniqueBy(newProduct, x => x.Id, insertAtBeginning: true);
```

### 3. 排序管理器 (Sort Manager)

SortManager 提供了高级排序管理功能，包括保存、加载和切换排序配置。

```csharp
var collection = new ObservableCollection<Product>();
var manager = new SortManager<Product>(collection);

// 应用排序
manager.ApplySort("Price", descending: false);

// 获取当前排序配置
var currentSort = manager.CurrentSort;
// currentSort.PropertyName = "Price"
// currentSort.Descending = false

// 保存排序配置
manager.SaveSort("MyFavoriteSort");

// 应用其他排序
manager.ApplySort("Name", descending: true);

// 加载之前保存的排序
bool loaded = manager.LoadSort("MyFavoriteSort");

// 切换排序方向（升序 ⟷ 降序）
manager.ToggleSortDirection();

// 在同一属性上第二次调用 ApplySort 会自动切换方向
manager.ApplySort("Price");  // 第一次：升序
manager.ApplySort("Price");  // 第二次：降序
manager.ApplySort("Price");  // 第三次：升序
```

## 📊 排序类型支持 (Supported Sorting Types)

### 字符串排序 (String Sorting)
使用 Windows Shell 逻辑比较，支持自然排序：
- "Item1" < "Item2" < "Item10" （而不是字典序）

```csharp
// 逻辑排序结果
["Item1", "Item2", "Item10", "Item20"]

// 而不是字典序
["Item1", "Item10", "Item2", "Item20"]
```

### 数值类型排序 (Numeric Type Sorting)
支持所有实现 IComparable 的数值类型：
- int, long, decimal, double, float
- 可空类型: int?, decimal?, DateTime?

### 日期时间排序 (DateTime Sorting)
```csharp
collection.SortBy("CreatedDate", descending: false);
collection.SortBy(x => x.ModifiedDate, descending: true);
```

### 自定义类型排序 (Custom Type Sorting)
任何实现 IComparable 或 IComparable<T> 的类型都可以排序

## 🔄 迁移指南 (Migration Guide)

### 从接口定义排序迁移到通用排序

**旧方式（需要实现接口）：**
```csharp
// 必须实现 ISortID 接口
public class MyItem : ISortID
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// 排序
collection.SortByID(descending: false);
```

**新方式（推荐，无需接口）：**
```csharp
// 无需实现任何接口
public class MyItem
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// 方式 1: 使用属性名
collection.SortBy("Id", descending: false);

// 方式 2: 使用 Lambda
collection.SortBy(x => x.Id, descending: false);

// 方式 3: 智能排序（自动检测 Id 属性）
collection.SmartSort(descending: false);
```

### 迁移对照表

| 旧方法 | 新方法（推荐） |
|-------|--------------|
| `collection.SortByID()` | `collection.SortBy("Id")` 或 `collection.SmartSort()` |
| `collection.SortByKey()` | `collection.SortBy("Key")` |
| `collection.SortByBatch()` | `collection.SortBy("Batch")` |
| `collection.SortByBatchID()` | `collection.SortBy("BatchID")` |
| `collection.AddUnique(item)` | `collection.AddUniqueBy(item, x => x.Id)` |

## 🎨 在 ListView 中使用 (Usage with ListView)

### 配合 GridViewColumnVisibility 使用

```csharp
// 在 ListView 列点击事件中排序
private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
{
    if (sender is GridViewColumnHeader header)
    {
        string? propertyName = header.Tag?.ToString();
        if (!string.IsNullOrEmpty(propertyName))
        {
            bool descending = !_isAscending;
            
            // 方法 1: 直接排序
            if (listView.ItemsSource is ObservableCollection<MyItem> collection)
            {
                collection.SortBy(propertyName, descending);
            }
            
            // 方法 2: 使用 GridViewColumnVisibility 扩展
            GridViewColumnVisibilityCollection.SortListViewData(
                listView, 
                propertyName, 
                descending
            );
            
            _isAscending = !_isAscending;
        }
    }
}
```

### 智能排序

```csharp
// 自动检测合适的排序属性
GridViewColumnVisibilityCollection.SmartSort(listView, descending: false);
```

## ⚡ 性能考虑 (Performance Considerations)

### 1. 集合大小
- 小型集合（< 100 项）：所有方法性能相似
- 中型集合（100-1000 项）：推荐使用反射方法
- 大型集合（> 1000 项）：考虑使用 LINQ 的 OrderBy

### 2. 排序频率
- 频繁排序：使用 SortManager 缓存排序配置
- 偶尔排序：直接使用扩展方法

### 3. 反射开销
反射排序在首次使用时会有轻微性能开销，但提供了更大的灵活性：
```csharp
// 反射方法 - 灵活但有轻微开销
collection.SortBy("PropertyName", descending);

// Lambda 方法 - 性能更好
collection.SortBy(x => x.PropertyName, descending);
```

## 🔍 异常处理 (Exception Handling)

### 属性不存在
```csharp
try
{
    collection.SortBy("NonExistentProperty", false);
}
catch (ArgumentException ex)
{
    // 处理属性不存在的情况
    Console.WriteLine($"Property not found: {ex.Message}");
}
```

### 不可比较的类型
```csharp
try
{
    collection.SortBy("ComplexProperty", false);
}
catch (InvalidOperationException ex)
{
    // 处理不可比较类型的情况
    Console.WriteLine($"Property is not comparable: {ex.Message}");
}
```

## 📝 最佳实践 (Best Practices)

### ✅ 推荐做法

1. **优先使用通用排序方法**
   ```csharp
   // ✅ 好
   collection.SortBy("Id", descending: false);
   
   // ❌ 不推荐（除非已经实现了接口）
   collection.SortByID(descending: false);
   ```

2. **使用 Lambda 表达式获得类型安全**
   ```csharp
   // ✅ 类型安全，编译时检查
   collection.SortBy(x => x.Price, descending: false);
   
   // ⚠️ 运行时检查
   collection.SortBy("Price", descending: false);
   ```

3. **使用 SortManager 管理复杂排序**
   ```csharp
   // ✅ 好 - 可保存和恢复
   var manager = new SortManager<T>(collection);
   manager.ApplySort("Price");
   manager.SaveSort("PriceSort");
   
   // ❌ 不好 - 无法保存状态
   collection.SortBy("Price");
   ```

4. **多级排序使用 SortByMultiple**
   ```csharp
   // ✅ 好
   collection.SortByMultiple(
       ("Category", false),
       ("Price", true)
   );
   
   // ❌ 不好 - 只会保留最后一次排序
   collection.SortBy("Category");
   collection.SortBy("Price");
   ```

5. **智能排序用于默认排序**
   ```csharp
   // ✅ 好 - 自动检测合适的属性
   collection.SmartSort(descending: false);
   ```

## 🧪 单元测试 (Unit Testing)

查看 `Test/ColorVision.UI.Tests` 项目获取完整的测试示例：
- `InterfaceBasedSortTests.cs` - 接口定义排序测试
- `UniversalSortTests.cs` - 通用排序测试
- `SortManagerTests.cs` - 排序管理器测试

## 📚 相关资源 (Related Resources)

- [ColorVision.UI README](../../UI/ColorVision.UI/README.md)
- [测试项目 README](../Test/ColorVision.UI.Tests/README.md)
- [API 文档](../../docs/api/ColorVision.UI.Sorts.md)

## 🤝 贡献 (Contributing)

欢迎提交 Issue 和 Pull Request 来改进排序功能！

## 📄 更新日志 (Changelog)

### v1.3.8.7
- ✨ 新增通用反射排序方法
- ✨ 新增 SortManager 排序管理器
- ✨ 新增多级排序支持
- ✨ 新增智能排序功能
- 📝 完善文档和测试用例
- ⚡ 优化排序性能

### 之前版本
- 基础的接口定义排序功能（ISortID, ISortKey, ISortBatch, ISortBatchID）
