# ColorVision.UI.Tests

ColorVision.UI 组件库的单元测试项目，用于测试发布到 NuGet 的 ColorVision.UI DLL 功能。

Unit test project for the ColorVision.UI component library, used to test the ColorVision.UI DLL published to NuGet.

## 📋 项目概述 (Project Overview)

该测试项目专注于测试 ColorVision.UI 的核心功能，特别是排序（Sort）相关功能。

This test project focuses on testing the core functionality of ColorVision.UI, especially Sort-related features.

## 🎯 测试范围 (Test Coverage)

### 1. 接口定义的排序测试 (Interface-Based Sorting Tests)
- **ISortID** - 基于 ID 属性的排序
- **ISortKey** - 基于 Key 属性的逻辑排序
- **ISortBatch** - 基于 Batch 属性的排序
- **ISortBatchID** - 基于 BatchID 属性的排序（支持可空类型）

测试文件: `InterfaceBasedSortTests.cs`

### 2. 通用反射排序测试 (Universal/Reflection-Based Sorting Tests)
- **SortBy(propertyName)** - 按属性名排序（字符串）
- **SortBy(keySelector)** - 使用 Lambda 表达式排序
- **SortByMultiple** - 多级排序
- **SmartSort** - 智能排序（自动检测合适的排序属性）
- **AddUniqueBy** - 按键添加唯一元素

测试文件: `UniversalSortTests.cs`

### 3. 排序管理器测试 (Sort Manager Tests)
- **ApplySort** - 应用排序
- **SaveSort/LoadSort** - 保存和加载排序配置
- **ToggleSortDirection** - 切换排序方向

测试文件: `SortManagerTests.cs`

## 🚀 运行测试 (Running Tests)

### 前提条件 (Prerequisites)
- .NET 8.0 SDK 或更高版本
- Windows 操作系统（因为 WPF 依赖 Windows Desktop Runtime）

### 构建项目 (Build Project)
```bash
cd Test/ColorVision.UI.Tests
dotnet build
```

### 运行测试 (Run Tests)
```bash
dotnet test --verbosity normal
```

### 运行特定测试 (Run Specific Tests)
```bash
# 运行接口定义的排序测试
dotnet test --filter "FullyQualifiedName~InterfaceBasedSortTests"

# 运行通用排序测试
dotnet test --filter "FullyQualifiedName~UniversalSortTests"

# 运行排序管理器测试
dotnet test --filter "FullyQualifiedName~SortManagerTests"
```

## 📚 测试示例 (Test Examples)

### 接口定义排序示例 (Interface-Based Sorting Example)

```csharp
// 1. 定义实现接口的类
public class MyItem : ISortID
{
    public int Id { get; set; }
    public string Name { get; set; }
}

// 2. 使用排序扩展方法
var collection = new ObservableCollection<MyItem>
{
    new MyItem { Id = 3, Name = "Third" },
    new MyItem { Id = 1, Name = "First" },
    new MyItem { Id = 2, Name = "Second" }
};

// 升序排序
collection.SortByID(descending: false);
// 结果: [1, 2, 3]

// 降序排序
collection.SortByID(descending: true);
// 结果: [3, 2, 1]
```

### 通用排序示例 (Universal Sorting Example)

```csharp
// 1. 定义普通类（无需实现特定接口）
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// 2. 使用属性名排序
var products = new ObservableCollection<Product>
{
    new Product { Id = 3, Name = "C", Price = 30 },
    new Product { Id = 1, Name = "A", Price = 10 },
    new Product { Id = 2, Name = "B", Price = 20 }
};

// 按 Id 排序
products.SortBy("Id", descending: false);

// 按 Price 排序
products.SortBy("Price", descending: true);

// 3. 使用 Lambda 表达式排序
products.SortBy(x => x.Price, descending: false);

// 4. 多级排序
products.SortByMultiple(
    ("Price", false),    // 首先按价格升序
    ("Name", false)      // 然后按名称升序
);

// 5. 智能排序（自动检测 Id、Key、Name 等属性）
products.SmartSort(descending: false);
```

### 排序管理器示例 (Sort Manager Example)

```csharp
var collection = new ObservableCollection<Product> { /* ... */ };
var manager = new SortManager<Product>(collection);

// 应用排序
manager.ApplySort("Price", descending: false);

// 保存排序配置
manager.SaveSort("PriceSort");

// 切换到其他排序
manager.ApplySort("Name", descending: true);

// 恢复之前保存的排序
manager.LoadSort("PriceSort");

// 切换排序方向
manager.ToggleSortDirection();
```

## 🔧 项目配置 (Project Configuration)

测试项目引用:
- **ColorVision.UI** - 被测试的主要库
- **xUnit** - 测试框架
- **Microsoft.NET.Test.Sdk** - .NET 测试 SDK

## 📖 相关文档 (Related Documentation)

- [ColorVision.UI Sort 功能文档](../../docs/ColorVision.UI.Sort.md)
- [排序功能迁移指南](../../docs/Sort-Migration-Guide.md)

## ⚠️ 注意事项 (Notes)

1. **Windows Only**: 由于 ColorVision.UI 使用 WPF，测试必须在 Windows 环境中运行
2. **ObservableCollection**: 所有排序方法都是 ObservableCollection 的扩展方法
3. **线程安全**: 排序操作应在 UI 线程上执行
4. **性能**: 对于大型集合，建议使用 UniversalSortExtensions 的反射方法以获得更好的灵活性

## 🤝 贡献 (Contributing)

欢迎添加更多测试用例来提高测试覆盖率。请确保:
- 每个测试方法只测试一个功能点
- 使用有意义的测试方法名
- 添加必要的注释说明测试意图
