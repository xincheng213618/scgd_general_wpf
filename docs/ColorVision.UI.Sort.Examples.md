# ColorVision.UI Sort 使用示例 (Usage Examples)

本文件包含 ColorVision.UI Sort 功能的实用示例代码。

This file contains practical example code for ColorVision.UI Sort functionality.

## 基础示例 (Basic Examples)

### 示例 1: 接口定义排序 (Interface-Based Sorting)

```csharp
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

// 定义实现接口的类
public class Product : ISortID, ISortKey
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// 使用排序功能
var products = new ObservableCollection<Product>
{
    new Product { Id = 3, Key = "PROD10", Name = "Product C", Price = 30m },
    new Product { Id = 1, Key = "PROD2", Name = "Product A", Price = 10m },
    new Product { Id = 2, Key = "PROD1", Name = "Product B", Price = 20m }
};

// 按 ID 排序
products.SortByID(descending: false);
// 结果: [Id=1, Id=2, Id=3]

// 按 Key 排序（逻辑排序）
products.SortByKey(descending: false);
// 结果: [PROD1, PROD2, PROD10]
```

### 示例 2: 通用反射排序 (Universal Reflection-Based Sorting)

```csharp
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

// 定义普通类，无需实现任何接口
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? Category { get; set; }
}

var products = new ObservableCollection<Product>
{
    new Product { Id = 3, Name = "Laptop", Price = 1200m, Category = "Electronics" },
    new Product { Id = 1, Name = "Mouse", Price = 20m, Category = "Accessories" },
    new Product { Id = 2, Name = "Keyboard", Price = 80m, Category = "Accessories" }
};

// 方式 1: 使用属性名排序
products.SortBy("Price", descending: false);
// 结果: [Mouse $20, Keyboard $80, Laptop $1200]

// 方式 2: 使用 Lambda 表达式（推荐）
products.SortBy(x => x.Name, descending: false);
// 结果: [Keyboard, Laptop, Mouse]

// 方式 3: 智能排序（自动检测 Id 属性）
products.SmartSort(descending: false);
// 结果: [Id=1, Id=2, Id=3]
```

## WPF ListView 集成示例 (WPF ListView Integration Examples)

### 示例 3: ListView 列头点击排序

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

public partial class ProductListView : Window
{
    private ObservableCollection<Product> Products { get; set; }
    private bool _isDescending = false;
    private string? _lastSortProperty = null;

    public ProductListView()
    {
        InitializeComponent();
        
        Products = new ObservableCollection<Product>
        {
            new Product { Id = 1, Name = "Product A", Price = 10m },
            new Product { Id = 2, Name = "Product B", Price = 20m },
            new Product { Id = 3, Name = "Product C", Price = 30m }
        };
        
        ProductsListView.ItemsSource = Products;
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is GridViewColumnHeader header)
        {
            var propertyName = header.Tag?.ToString();
            if (string.IsNullOrEmpty(propertyName))
                return;

            // 如果点击同一列，切换排序方向
            if (_lastSortProperty == propertyName)
            {
                _isDescending = !_isDescending;
            }
            else
            {
                _isDescending = false;
                _lastSortProperty = propertyName;
            }

            // 执行排序
            Products.SortBy(propertyName, _isDescending);

            // 更新列头显示排序方向
            UpdateColumnHeaders(header, _isDescending);
        }
    }

    private void UpdateColumnHeaders(GridViewColumnHeader currentHeader, bool descending)
    {
        // 清除所有列头的排序指示器
        // 添加当前列的排序指示器（↑ 或 ↓）
        // 具体实现根据您的UI设计
    }
}
```

XAML:
```xml
<ListView x:Name="ProductsListView">
    <ListView.View>
        <GridView>
            <GridViewColumn Header="ID" DisplayMemberBinding="{Binding Id}" Width="80">
                <GridViewColumn.HeaderTemplate>
                    <DataTemplate>
                        <GridViewColumnHeader Content="ID" Tag="Id" 
                                            Click="GridViewColumnHeader_Click"/>
                    </DataTemplate>
                </GridViewColumn.HeaderTemplate>
            </GridViewColumn>
            
            <GridViewColumn Header="名称" DisplayMemberBinding="{Binding Name}" Width="150">
                <GridViewColumn.HeaderTemplate>
                    <DataTemplate>
                        <GridViewColumnHeader Content="名称" Tag="Name" 
                                            Click="GridViewColumnHeader_Click"/>
                    </DataTemplate>
                </GridViewColumn.HeaderTemplate>
            </GridViewColumn>
            
            <GridViewColumn Header="价格" DisplayMemberBinding="{Binding Price}" Width="100">
                <GridViewColumn.HeaderTemplate>
                    <DataTemplate>
                        <GridViewColumnHeader Content="价格" Tag="Price" 
                                            Click="GridViewColumnHeader_Click"/>
                    </DataTemplate>
                </GridViewColumn.HeaderTemplate>
            </GridViewColumn>
        </GridView>
    </ListView.View>
</ListView>
```

## 高级示例 (Advanced Examples)

### 示例 4: 使用 SortManager 管理排序

```csharp
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

public class ProductViewModel
{
    private SortManager<Product> _sortManager;
    public ObservableCollection<Product> Products { get; set; }

    public ProductViewModel()
    {
        Products = new ObservableCollection<Product>();
        _sortManager = new SortManager<Product>(Products);
        
        // 加载示例数据
        LoadSampleData();
        
        // 应用默认排序
        ApplyDefaultSort();
    }

    private void LoadSampleData()
    {
        Products.Add(new Product { Id = 3, Name = "Laptop", Price = 1200m });
        Products.Add(new Product { Id = 1, Name = "Mouse", Price = 20m });
        Products.Add(new Product { Id = 2, Name = "Keyboard", Price = 80m });
    }

    public void ApplyDefaultSort()
    {
        _sortManager.ApplySort("Id", descending: false);
        _sortManager.SaveSort("Default");
    }

    public void SortByPrice()
    {
        _sortManager.ApplySort("Price", descending: false);
    }

    public void SortByName()
    {
        _sortManager.ApplySort("Name", descending: false);
    }

    public void ToggleSortDirection()
    {
        _sortManager.ToggleSortDirection();
    }

    public void RestoreDefaultSort()
    {
        _sortManager.LoadSort("Default");
    }

    public void SaveCurrentSortAs(string name)
    {
        _sortManager.SaveSort(name);
    }
}
```

### 示例 5: 多级排序

```csharp
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

public class OrderItem
{
    public string Category { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime OrderDate { get; set; }
}

var orders = new ObservableCollection<OrderItem>
{
    new OrderItem { Category = "Electronics", Supplier = "A", Price = 100m, OrderDate = new DateTime(2024, 1, 15) },
    new OrderItem { Category = "Electronics", Supplier = "B", Price = 80m, OrderDate = new DateTime(2024, 1, 10) },
    new OrderItem { Category = "Furniture", Supplier = "A", Price = 200m, OrderDate = new DateTime(2024, 1, 20) },
    new OrderItem { Category = "Furniture", Supplier = "C", Price = 150m, OrderDate = new DateTime(2024, 1, 5) }
};

// 多级排序: 先按类别，再按供应商，最后按价格
orders.SortByMultiple(
    ("Category", false),    // 类别升序
    ("Supplier", false),    // 供应商升序
    ("Price", true)         // 价格降序
);

// 结果排序:
// 1. Electronics, A, $100
// 2. Electronics, B, $80
// 3. Furniture, A, $200
// 4. Furniture, C, $150
```

### 示例 6: 动态属性排序

```csharp
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

public class DynamicSortExample
{
    public void SortByUserSelection(
        ObservableCollection<Product> products,
        string selectedProperty,
        bool isDescending)
    {
        try
        {
            products.SortBy(selectedProperty, isDescending);
        }
        catch (ArgumentException ex)
        {
            // 属性不存在
            MessageBox.Show($"无法按 '{selectedProperty}' 排序: 属性不存在");
        }
        catch (InvalidOperationException ex)
        {
            // 属性不可比较
            MessageBox.Show($"属性 '{selectedProperty}' 不支持排序");
        }
    }

    public void Example()
    {
        var products = new ObservableCollection<Product>();
        
        // 用户从下拉框选择排序属性
        string userSelectedProperty = "Price";  // 或从 ComboBox 获取
        bool userSelectedDescending = true;     // 或从 CheckBox 获取
        
        SortByUserSelection(products, userSelectedProperty, userSelectedDescending);
    }
}
```

### 示例 7: 添加唯一元素

```csharp
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

public class UniqueCollectionExample
{
    public void ManageProducts()
    {
        var products = new ObservableCollection<Product>
        {
            new Product { Id = 1, Name = "Existing Product" }
        };

        // 尝试添加重复的产品（按 Id 判断）
        var duplicateProduct = new Product { Id = 1, Name = "Duplicate Attempt" };
        products.AddUniqueBy(duplicateProduct, x => x.Id);
        // 不会添加，因为 Id=1 已存在

        // 添加新产品
        var newProduct = new Product { Id = 2, Name = "New Product" };
        products.AddUniqueBy(newProduct, x => x.Id);
        // 成功添加

        // 在开头插入（如果不存在）
        var topProduct = new Product { Id = 3, Name = "Top Product" };
        products.AddUniqueBy(topProduct, x => x.Id, insertAtBeginning: true);
        // 插入到索引 0

        // 最终集合: [Id=3, Id=1, Id=2]
    }
}
```

## 性能优化示例 (Performance Optimization Examples)

### 示例 8: 选择最佳排序方法

```csharp
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

public class PerformanceExample
{
    // 小型集合 (< 100 items) - 任何方法都可以
    public void SortSmallCollection(ObservableCollection<Product> products)
    {
        products.SortBy("Price", descending: false);
    }

    // 中型集合 (100-1000 items) - 使用 Lambda 获得更好性能
    public void SortMediumCollection(ObservableCollection<Product> products)
    {
        products.SortBy(x => x.Price, descending: false);
    }

    // 大型集合 (> 1000 items) - 考虑使用 LINQ 或预排序
    public void SortLargeCollection(ObservableCollection<Product> products)
    {
        // 方式 1: 仍然使用扩展方法
        products.SortBy(x => x.Price, descending: false);
        
        // 方式 2: 如果需要更复杂的排序逻辑，使用 LINQ
        var sorted = products.OrderBy(x => x.Category)
                            .ThenBy(x => x.Price)
                            .ToList();
        
        // 更新集合
        products.Clear();
        foreach (var item in sorted)
        {
            products.Add(item);
        }
    }

    // 频繁排序 - 使用 SortManager 缓存配置
    public void FrequentSorting(ObservableCollection<Product> products)
    {
        var manager = new SortManager<Product>(products);
        
        // 保存常用排序
        manager.ApplySort("Price", descending: false);
        manager.SaveSort("PriceLowToHigh");
        
        manager.ApplySort("Price", descending: true);
        manager.SaveSort("PriceHighToLow");
        
        manager.ApplySort("Name", descending: false);
        manager.SaveSort("NameAZ");
        
        // 快速切换
        manager.LoadSort("PriceLowToHigh");
        manager.LoadSort("NameAZ");
    }
}
```

## 测试示例 (Testing Examples)

### 示例 9: 单元测试

```csharp
using Xunit;
using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

public class ProductSortTests
{
    [Fact]
    public void SortByPrice_AscendingOrder_ReturnsCorrectOrder()
    {
        // Arrange
        var products = new ObservableCollection<Product>
        {
            new Product { Id = 1, Price = 30m },
            new Product { Id = 2, Price = 10m },
            new Product { Id = 3, Price = 20m }
        };

        // Act
        products.SortBy("Price", descending: false);

        // Assert
        Assert.Equal(10m, products[0].Price);
        Assert.Equal(20m, products[1].Price);
        Assert.Equal(30m, products[2].Price);
    }

    [Fact]
    public void SortByMultiple_SortsCorrectly()
    {
        // Arrange
        var products = new ObservableCollection<Product>
        {
            new Product { Category = "B", Price = 20m },
            new Product { Category = "A", Price = 30m },
            new Product { Category = "A", Price = 10m }
        };

        // Act
        products.SortByMultiple(
            ("Category", false),
            ("Price", false)
        );

        // Assert
        Assert.Equal("A", products[0].Category);
        Assert.Equal(10m, products[0].Price);
        Assert.Equal("A", products[1].Category);
        Assert.Equal(30m, products[1].Price);
        Assert.Equal("B", products[2].Category);
    }
}
```

## 最佳实践总结 (Best Practices Summary)

1. **优先使用通用排序方法** - 提供更大灵活性
2. **使用 Lambda 表达式** - 获得编译时类型检查
3. **使用 SortManager** - 管理复杂排序场景
4. **添加错误处理** - 处理属性不存在或不可比较的情况
5. **性能优化** - 根据集合大小选择合适的方法
6. **编写测试** - 确保排序逻辑正确性

## 相关文档 (Related Documentation)

- [ColorVision.UI Sort 完整文档](./ColorVision.UI.Sort.md)
- [迁移指南](./Sort-Migration-Guide.md)
- [测试项目 README](../Test/ColorVision.UI.Tests/README.md)
