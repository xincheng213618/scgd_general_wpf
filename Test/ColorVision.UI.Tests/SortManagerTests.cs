using System.Collections.ObjectModel;
using ColorVision.UI.Sorts;

namespace ColorVision.UI.Tests;

/// <summary>
/// 测试 SortManager 类的功能
/// Tests for SortManager class functionality
/// </summary>
public class SortManagerTests
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    [Fact]
    public void ApplySort_SortsCollectionAndRecordsConfiguration()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 3, Name = "Third" },
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" }
        };
        var manager = new SortManager<TestItem>(collection);

        // Act
        manager.ApplySort("Id", descending: false);

        // Assert
        Assert.Equal(1, collection[0].Id);
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(3, collection[2].Id);
        Assert.NotNull(manager.CurrentSort);
        Assert.Equal("Id", manager.CurrentSort?.PropertyName);
        Assert.False(manager.CurrentSort?.Descending);
    }

    [Fact]
    public void ApplySort_SamePropertyTwice_TogglesDirection()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 3, Name = "Third" },
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" }
        };
        var manager = new SortManager<TestItem>(collection);

        // Act - Apply sort twice on same property
        manager.ApplySort("Id"); // First time: ascending (default behavior)
        manager.ApplySort("Id"); // Second time: should toggle to descending

        // Assert
        Assert.Equal(3, collection[0].Id); // Descending order
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(1, collection[2].Id);
        Assert.True(manager.CurrentSort?.Descending);
    }

    [Fact]
    public void SaveSort_AndLoadSort_RestoresSortConfiguration()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 3, Name = "Third" },
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" }
        };
        var manager = new SortManager<TestItem>(collection);

        // Act
        manager.ApplySort("Id", descending: true);
        manager.SaveSort("MySort");

        // Change to a different sort
        manager.ApplySort("Name", descending: false);

        // Load saved sort
        var loaded = manager.LoadSort("MySort");

        // Assert
        Assert.True(loaded);
        Assert.Equal("Id", manager.CurrentSort?.PropertyName);
        Assert.True(manager.CurrentSort?.Descending);
        Assert.Equal(3, collection[0].Id); // Back to Id descending
    }

    [Fact]
    public void LoadSort_NonExistentSort_ReturnsFalse()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "First" }
        };
        var manager = new SortManager<TestItem>(collection);

        // Act
        var loaded = manager.LoadSort("NonExistent");

        // Assert
        Assert.False(loaded);
    }

    [Fact]
    public void ToggleSortDirection_ReversesSortOrder()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 3, Name = "Third" },
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" }
        };
        var manager = new SortManager<TestItem>(collection);
        manager.ApplySort("Id", descending: false);

        // Act
        manager.ToggleSortDirection();

        // Assert
        Assert.True(manager.CurrentSort?.Descending);
        Assert.Equal(3, collection[0].Id); // Now descending
        Assert.Equal(2, collection[1].Id);
        Assert.Equal(1, collection[2].Id);
    }

    [Fact]
    public void ToggleSortDirection_WithoutCurrentSort_DoesNotThrow()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "First" }
        };
        var manager = new SortManager<TestItem>(collection);

        // Act & Assert - Should not throw
        manager.ToggleSortDirection();
    }

    [Fact]
    public void SaveSort_OverwritesExistingSort()
    {
        // Arrange
        var collection = new ObservableCollection<TestItem>
        {
            new TestItem { Id = 1, Name = "First" }
        };
        var manager = new SortManager<TestItem>(collection);

        // Act
        manager.ApplySort("Id", descending: false);
        manager.SaveSort("MySort");

        manager.ApplySort("Name", descending: true);
        manager.SaveSort("MySort"); // Overwrite

        manager.LoadSort("MySort");

        // Assert
        Assert.Equal("Name", manager.CurrentSort?.PropertyName);
        Assert.True(manager.CurrentSort?.Descending);
    }
}
