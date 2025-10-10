# ColorVision.UI Sort 迁移指南

## 📋 迁移概述

本指南帮助您从基于接口的排序方式迁移到更灵活的反射排序方式。

## 🎯 为什么要迁移？

### 旧方案的局限性

1. **需要手动实现接口** - 每个需要排序的类都必须实现特定接口
2. **灵活性不足** - 只能按预定义的属性排序
3. **代码重复** - 多个类实现相同的接口导致代码重复
4. **维护困难** - 添加新的排序属性需要修改接口定义

### 新方案的优势

1. **无需接口** - 任意类都可以排序，无需实现特定接口
2. **动态灵活** - 可以按任意属性排序
3. **类型安全** - Lambda 表达式提供编译时类型检查
4. **易于维护** - 集中管理排序逻辑

## 🔄 迁移步骤

### 步骤 1: 评估现有代码

首先，找出所有实现了排序接口的类：

```bash
# 查找实现 ISortID 的类
grep -r "ISortID" --include="*.cs"

# 查找实现 ISortKey 的类
grep -r "ISortKey" --include="*.cs"

# 查找实现 ISortBatch 的类
grep -r "ISortBatch" --include="*.cs"
```

### 步骤 2: 逐个迁移类

#### 示例 1: 从 ISortID 迁移

**迁移前:**
```csharp
public class ProductModel : ISortID
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// 使用
var products = new ObservableCollection<ProductModel>();
products.SortByID(descending: false);
```

**迁移后:**
```csharp
// 1. 移除接口实现
public class ProductModel  // ← 移除 : ISortID
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// 2. 更新排序调用
var products = new ObservableCollection<ProductModel>();

// 方式 A: 使用属性名
products.SortBy("Id", descending: false);

// 方式 B: 使用 Lambda（推荐，类型安全）
products.SortBy(x => x.Id, descending: false);

// 方式 C: 智能排序
products.SmartSort(descending: false);
```

#### 示例 2: 从 ISortKey 迁移

**迁移前:**
```csharp
public class FileItem : ISortKey
{
    public string Key { get; set; }
    public long Size { get; set; }
}

// 使用
fileItems.SortByKey(descending: false);
```

**迁移后:**
```csharp
public class FileItem
{
    public string Key { get; set; }
    public long Size { get; set; }
}

// 使用
fileItems.SortBy("Key", descending: false);
// 或
fileItems.SortBy(x => x.Key, descending: false);
```

#### 示例 3: 从 ISortBatch 迁移

**迁移前:**
```csharp
public class BatchData : ISortBatch
{
    public string? Batch { get; set; }
    public int Value { get; set; }
}

// 使用
batchData.SortByBatch(descending: false);
```

**迁移后:**
```csharp
public class BatchData
{
    public string? Batch { get; set; }
    public int Value { get; set; }
}

// 使用
batchData.SortBy("Batch", descending: false);
// 或
batchData.SortBy(x => x.Batch, descending: false);
```

#### 示例 4: 从 ISortBatchID 迁移

**迁移前:**
```csharp
public class BatchItem : ISortBatchID
{
    public int? BatchID { get; set; }
    public string Name { get; set; }
}

// 使用
batchItems.SortByBatchID(descending: false);
```

**迁移后:**
```csharp
public class BatchItem
{
    public int? BatchID { get; set; }
    public string Name { get; set; }
}

// 使用（支持可空类型）
batchItems.SortBy("BatchID", descending: false);
// 或
batchItems.SortBy(x => x.BatchID, descending: false);
```

### 步骤 3: 更新排序逻辑

#### 替换单一属性排序

**迁移前:**
```csharp
collection.SortByID(descending: false);
collection.SortByKey(descending: false);
collection.SortByBatch(descending: false);
collection.SortByBatchID(descending: false);
```

**迁移后:**
```csharp
// 统一使用 SortBy
collection.SortBy("Id", descending: false);
collection.SortBy("Key", descending: false);
collection.SortBy("Batch", descending: false);
collection.SortBy("BatchID", descending: false);

// 或使用 Lambda（推荐）
collection.SortBy(x => x.Id, descending: false);
collection.SortBy(x => x.Key, descending: false);
collection.SortBy(x => x.Batch, descending: false);
collection.SortBy(x => x.BatchID, descending: false);
```

#### 添加多级排序能力

新方案支持多级排序，这是旧方案无法实现的：

```csharp
// 多级排序 - 先按 Batch，再按 Id
collection.SortByMultiple(
    ("Batch", false),
    ("Id", false)
);
```

### 步骤 4: 利用新功能

#### 1. 使用 SortManager

```csharp
// 创建排序管理器
var manager = new SortManager<ProductModel>(products);

// 应用排序
manager.ApplySort("Price", descending: false);

// 保存常用排序配置
manager.SaveSort("PriceLowToHigh");
manager.SaveSort("PriceHighToLow");

// 快速切换
manager.LoadSort("PriceLowToHigh");

// 切换排序方向
manager.ToggleSortDirection();
```

#### 2. 智能排序

```csharp
// 自动检测并使用 Id、Key、Name 等常用属性
collection.SmartSort(descending: false);
```

#### 3. 动态排序

```csharp
// 根据用户选择动态排序
string selectedProperty = userComboBox.SelectedValue.ToString();
bool isDescending = descendingCheckBox.IsChecked == true;

collection.SortBy(selectedProperty, isDescending);
```

## 📊 迁移对照表

### 方法对照

| 旧方法 | 新方法（属性名） | 新方法（Lambda） | 新方法（智能） |
|-------|----------------|-----------------|--------------|
| `SortByID()` | `SortBy("Id")` | `SortBy(x => x.Id)` | `SmartSort()` |
| `SortByKey()` | `SortBy("Key")` | `SortBy(x => x.Key)` | `SmartSort()` |
| `SortByBatch()` | `SortBy("Batch")` | `SortBy(x => x.Batch)` | - |
| `SortByBatchID()` | `SortBy("BatchID")` | `SortBy(x => x.BatchID)` | - |
| `AddUnique(item)` | - | `AddUniqueBy(item, x => x.Id)` | - |

### 接口对照

| 旧接口 | 新方案 |
|-------|--------|
| `ISortID` | 无需接口，使用 `SortBy("Id")` |
| `ISortKey` | 无需接口，使用 `SortBy("Key")` |
| `ISortBatch` | 无需接口，使用 `SortBy("Batch")` |
| `ISortBatchID` | 无需接口，使用 `SortBy("BatchID")` |

## 🎯 ListView 集成迁移

### 迁移前（接口方式）

```csharp
private void ColumnHeader_Click(object sender, RoutedEventArgs e)
{
    if (sender is GridViewColumnHeader header)
    {
        var sortBy = header.Tag?.ToString();
        if (sortBy == "Id" && listView.ItemsSource is ObservableCollection<MyItem> items)
        {
            items.SortByID(_isDescending);
            _isDescending = !_isDescending;
        }
    }
}
```

### 迁移后（反射方式）

```csharp
private void ColumnHeader_Click(object sender, RoutedEventArgs e)
{
    if (sender is GridViewColumnHeader header)
    {
        var propertyName = header.Tag?.ToString();
        if (!string.IsNullOrEmpty(propertyName) && 
            listView.ItemsSource is ObservableCollection<MyItem> items)
        {
            // 方法 1: 直接排序
            items.SortBy(propertyName, _isDescending);
            
            // 方法 2: 使用扩展方法
            GridViewColumnVisibilityCollection.SortListViewData(
                listView, 
                propertyName, 
                _isDescending
            );
            
            _isDescending = !_isDescending;
        }
    }
}
```

## ⚠️ 迁移注意事项

### 1. 向后兼容性

如果需要保持向后兼容，可以同时保留接口和新方法：

```csharp
// 保留接口实现
public class MyItem : ISortID
{
    public int Id { get; set; }
}

// 两种方式都可用
collection.SortByID();           // 旧方式
collection.SortBy("Id");         // 新方式
```

### 2. 性能影响

- 反射方法在首次调用时有轻微性能开销
- Lambda 方法性能最好
- 对于频繁排序的大型集合，推荐使用 Lambda

```csharp
// 性能最佳（编译时绑定）
collection.SortBy(x => x.Id, descending);

// 性能良好（运行时反射）
collection.SortBy("Id", descending);

// 性能一般（需要检测多个属性）
collection.SmartSort(descending);
```

### 3. 错误处理

添加错误处理以应对属性不存在的情况：

```csharp
try
{
    collection.SortBy(propertyName, descending);
}
catch (ArgumentException ex)
{
    // 属性不存在
    MessageBox.Show($"无法排序: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // 属性不可比较
    MessageBox.Show($"该属性不支持排序: {ex.Message}");
}
```

### 4. 类型安全

优先使用 Lambda 表达式获得编译时类型检查：

```csharp
// ✅ 推荐：编译时检查
collection.SortBy(x => x.Id, descending);

// ⚠️ 可用：运行时检查
collection.SortBy("Id", descending);

// ❌ 错误示例：属性名拼写错误在运行时才会发现
collection.SortBy("Idd", descending);  // 运行时错误
```

## 🧪 测试迁移

### 更新单元测试

**迁移前:**
```csharp
[Test]
public void TestSortById()
{
    var collection = new ObservableCollection<MyItem>
    {
        new MyItem { Id = 3 },
        new MyItem { Id = 1 },
        new MyItem { Id = 2 }
    };
    
    collection.SortByID(descending: false);
    
    Assert.AreEqual(1, collection[0].Id);
}
```

**迁移后:**
```csharp
[Test]
public void TestSortById()
{
    var collection = new ObservableCollection<MyItem>
    {
        new MyItem { Id = 3 },
        new MyItem { Id = 1 },
        new MyItem { Id = 2 }
    };
    
    // 测试三种方式
    collection.SortBy("Id", descending: false);
    Assert.AreEqual(1, collection[0].Id);
    
    collection.SortBy(x => x.Id, descending: false);
    Assert.AreEqual(1, collection[0].Id);
    
    collection.SmartSort(descending: false);
    Assert.AreEqual(1, collection[0].Id);
}
```

## 📝 迁移清单

- [ ] 识别所有实现排序接口的类
- [ ] 为每个类创建迁移计划
- [ ] 更新排序调用代码
- [ ] 移除不必要的接口实现
- [ ] 添加错误处理
- [ ] 更新单元测试
- [ ] 利用新功能（SortManager、多级排序等）
- [ ] 性能测试
- [ ] 文档更新
- [ ] 代码审查

## 🚀 渐进式迁移策略

### 阶段 1: 并行运行（1-2周）
- 保留旧代码
- 添加新方法
- 两种方式并存

### 阶段 2: 新功能使用新方法（2-4周）
- 所有新代码使用新方法
- 逐步替换旧代码
- 监控问题

### 阶段 3: 完全迁移（4-6周）
- 移除旧接口
- 清理遗留代码
- 更新文档

## 📚 相关资源

- [ColorVision.UI Sort 功能文档](./ColorVision.UI.Sort.md)
- [测试项目](../Test/ColorVision.UI.Tests/README.md)
- [UniversalSortExtensions 源码](../UI/ColorVision.UI/Sort/UniversalSortExtensions.cs)

## 💡 获取帮助

如果在迁移过程中遇到问题：
1. 查看测试项目中的示例代码
2. 参考完整文档
3. 提交 Issue
4. 联系开发团队

## ✅ 迁移成功标志

- [ ] 所有类都不再依赖排序接口
- [ ] 排序功能正常工作
- [ ] 单元测试全部通过
- [ ] 性能满足要求
- [ ] 代码更加简洁灵活
