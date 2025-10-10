using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

namespace ColorVision.UI.Tests;

/// <summary>
/// 测试通用排序功能（基于反射，不需要实现特定接口）
/// Tests for universal/reflection-based sorting functionality (no specific interface required)
/// </summary>
public class UniversalSortTests
{
    // 普通测试类，不实现任何特定接口
    // Regular test class without implementing any specific interface
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public int? OptionalValue { get; set; }
    }

    private class ProductItem
    {
        public string Code { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }

    [Fact]
    public void SortBy_IntProperty_AscendingOrder_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 3, Name = "Third" },
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" }
        };

        // Act
        collection.SortBy("Id", descending: false);

        // Assert
        Assert.Equal(1, collection[0].Id);
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(3, collection[2].Id);
    }

    [Fact]
    public void SortBy_IntProperty_DescendingOrder_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 3, Name = "Third" },
            new TestItem { Id = 2, Name = "Second" }
        };

        // Act
        collection.SortBy("Id", descending: true);

        // Assert
        Assert.Equal(3, collection[0].Id);
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(1, collection[2].Id);
    }

    [Fact]
    public void SortBy_StringProperty_UsesLogicalSorting()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "Item10" },
            new TestItem { Id = 2, Name = "Item2" },
            new TestItem { Id = 3, Name = "Item1" }
        };

        // Act
        collection.SortBy("Name", descending: false);

        // Assert - Logical sorting: Item1, Item2, Item10
        Assert.Equal("Item1", collection[0].Name);
        Assert.Equal("Item2", collection[1].Name);
        Assert.Equal("Item10", collection[2].Name);
    }

    [Fact]
    public void SortBy_DateTimeProperty_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, CreatedDate = new DateTime(2024, 3, 1) },
            new TestItem { Id = 2, CreatedDate = new DateTime(2024, 1, 1) },
            new TestItem { Id = 3, CreatedDate = new DateTime(2024, 2, 1) }
        };

        // Act
        collection.SortBy("CreatedDate", descending: false);

        // Assert
        Assert.Equal(new DateTime(2024, 1, 1), collection[0].CreatedDate);
        Assert.Equal(new DateTime(2024, 2, 1), collection[1].CreatedDate);
        Assert.Equal(new DateTime(2024, 3, 1), collection[2].CreatedDate);
    }

    [Fact]
    public void SortBy_WithKeySelector_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 3, Name = "Third" },
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" }
        };

        // Act
        collection.SortBy(x => x.Id, descending: false);

        // Assert
        Assert.Equal(1, collection[0].Id);
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(3, collection[2].Id);
    }

    [Fact]
    public void SortByMultiple_TwoProperties_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<ProductItem>
        {
            new ProductItem { Code = "A", Price = 20, Stock = 100 },
            new ProductItem { Code = "B", Price = 10, Stock = 50 },
            new ProductItem { Code = "C", Price = 10, Stock = 75 }
        };

        // Act - Sort by Price (ascending), then by Stock (descending)
        collection.SortByMultiple(
            ("Price", false),
            ("Stock", true)
        );

        // Assert
        // Price=10: Stock 75 should come before Stock 50 (descending)
        Assert.Equal("C", collection[0].Code); // Price=10, Stock=75
        Assert.Equal("B", collection[1].Code); // Price=10, Stock=50
        Assert.Equal("A", collection[2].Code); // Price=20, Stock=100
    }

    [Fact]
    public void SmartSort_WithIdProperty_SortsById()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 3, Name = "Third" },
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" }
        };

        // Act - SmartSort should automatically detect "Id" property
        collection.SmartSort(descending: false);

        // Assert
        Assert.Equal(1, collection[0].Id);
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(3, collection[2].Id);
    }

    [Fact]
    public void AddUniqueBy_WithDuplicateKey_DoesNotAddItem()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" }
        };

        // Act
        collection.AddUniqueBy(
            new TestItem { Id = 1, Name = "Duplicate" },
            x => x.Id
        );

        // Assert
        Assert.Equal(2, collection.Count);
        Assert.Equal("First", collection[0].Name); // Original unchanged
    }

    [Fact]
    public void AddUniqueBy_WithUniqueKey_AddsItem()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "First" }
        };

        // Act
        collection.AddUniqueBy(
            new TestItem { Id = 2, Name = "Second" },
            x => x.Id
        );

        // Assert
        Assert.Equal(2, collection.Count);
        Assert.Equal(2, collection[1].Id);
    }

    [Fact]
    public void AddUniqueBy_WithInsertAtBeginning_InsertsAtStart()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "First" }
        };

        // Act
        collection.AddUniqueBy(
            new TestItem { Id = 2, Name = "Second" },
            x => x.Id,
            insertAtBeginning: true
        );

        // Assert
        Assert.Equal(2, collection.Count);
        Assert.Equal(2, collection[0].Id); // New item at beginning
        Assert.Equal(1, collection[1].Id); // Original item moved down
    }

    [Fact]
    public void SortBy_NonExistentProperty_ThrowsArgumentException()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "First" }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            collection.SortBy("NonExistentProperty", descending: false)
        );
    }

    [Fact]
    public void SortBy_EmptyCollection_DoesNotThrow()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>();

        // Act & Assert - Should not throw
        collection.SortBy("Id", descending: false);
        Assert.Empty(collection);
    }

    [Fact]
    public void SortBy_SingleItem_DoesNotModify()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "Only" }
        };

        // Act
        collection.SortBy("Id", descending: false);

        // Assert
        Assert.Single(collection);
        Assert.Equal(1, collection[0].Id);
    }
}
