using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

namespace ColorVision.UI.Tests;

/// <summary>
/// 测试接口定义的排序功能
/// Tests for interface-based sorting functionality
/// </summary>
public class InterfaceBasedSortTests
{
    // 测试类实现 ISortID 接口
    private class TestItemWithId : ISortID
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // 测试类实现 ISortKey 接口
    private class TestItemWithKey : ISortKey
    {
        public string Key { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    // 测试类实现 ISortBatch 接口
    private class TestItemWithBatch : ISortBatch
    {
        public string? Batch { get; set; }
        public int Value { get; set; }
    }

    // 测试类实现 ISortBatchID 接口
    private class TestItemWithBatchID : ISortBatchID
    {
        public int? BatchID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void SortByID_AscendingOrder_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItemWithId>
        {
            new TestItemWithId { Id = 3, Name = "Third" },
            new TestItemWithId { Id = 1, Name = "First" },
            new TestItemWithId { Id = 2, Name = "Second" }
        };

        // Act
        collection.SortByID(descending: false);

        // Assert
        Assert.Equal(1, collection[0].Id);
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(3, collection[2].Id);
    }

    [Fact]
    public void SortByID_DescendingOrder_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItemWithId>
        {
            new TestItemWithId { Id = 1, Name = "First" },
            new TestItemWithId { Id = 3, Name = "Third" },
            new TestItemWithId { Id = 2, Name = "Second" }
        };

        // Act
        collection.SortByID(descending: true);

        // Assert
        Assert.Equal(3, collection[0].Id);
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(1, collection[2].Id);
    }

    [Fact]
    public void SortByKey_WithLogicalSorting_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItemWithKey>
        {
            new TestItemWithKey { Key = "Item10", Value = 1 },
            new TestItemWithKey { Key = "Item2", Value = 2 },
            new TestItemWithKey { Key = "Item1", Value = 3 }
        };

        // Act
        collection.SortByKey(descending: false);

        // Assert - Logical sorting should order as: Item1, Item2, Item10
        Assert.Equal("Item1", collection[0].Key);
        Assert.Equal("Item2", collection[1].Key);
        Assert.Equal("Item10", collection[2].Key);
    }

    [Fact]
    public void SortByBatch_AscendingOrder_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItemWithBatch>
        {
            new TestItemWithBatch { Batch = "Batch3", Value = 1 },
            new TestItemWithBatch { Batch = "Batch1", Value = 2 },
            new TestItemWithBatch { Batch = "Batch2", Value = 3 }
        };

        // Act
        collection.SortByBatch(descending: false);

        // Assert
        Assert.Equal("Batch1", collection[0].Batch);
        Assert.Equal("Batch2", collection[1].Batch);
        Assert.Equal("Batch3", collection[2].Batch);
    }

    [Fact]
    public void SortByBatchID_WithNullableValues_SortsCorrectly()
    {
        // Arrange
        var collection = new ObservableCollection<TestItemWithBatchID>
        {
            new TestItemWithBatchID { BatchID = 3, Name = "Third" },
            new TestItemWithBatchID { BatchID = null, Name = "Null" },
            new TestItemWithBatchID { BatchID = 1, Name = "First" },
            new TestItemWithBatchID { BatchID = 2, Name = "Second" }
        };

        // Act
        collection.SortByBatchID(descending: false);

        // Assert
        Assert.Equal(1, collection[0].BatchID);
        Assert.Equal(2, collection[1].BatchID);
        Assert.Equal(3, collection[2].BatchID);
    }

    [Fact]
    public void AddUnique_WithDuplicateId_DoesNotAddItem()
    {
        // Arrange
        var collection = new ObservableCollection<TestItemWithId>
        {
            new TestItemWithId { Id = 1, Name = "First" },
            new TestItemWithId { Id = 2, Name = "Second" }
        };

        // Act
        collection.AddUnique(new TestItemWithId { Id = 1, Name = "Duplicate" });

        // Assert
        Assert.Equal(2, collection.Count);
        Assert.Equal("First", collection[0].Name); // Original item unchanged
    }

    [Fact]
    public void AddUnique_WithUniqueId_AddsItem()
    {
        // Arrange
        var collection = new ObservableCollection<TestItemWithId>
        {
            new TestItemWithId { Id = 1, Name = "First" }
        };

        // Act
        collection.AddUnique(new TestItemWithId { Id = 2, Name = "Second" });

        // Assert
        Assert.Equal(2, collection.Count);
        Assert.Equal(2, collection[1].Id);
    }
}