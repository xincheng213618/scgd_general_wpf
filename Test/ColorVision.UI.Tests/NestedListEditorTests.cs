using System.ComponentModel;
using ColorVision.UI.PropertyEditor.Editor.List;

namespace ColorVision.UI.Tests;

/// <summary>
/// Tests for nested list editing functionality (e.g., List&lt;List&lt;int&gt;&gt;)
/// </summary>
public class NestedListEditorTests
{
    // Test class with nested list properties
    private class TestNestedListConfig
    {
        [Category("Nested Lists")]
        [DisplayName("Integer Matrix")]
        [Description("A list of integer lists (2D array)")]
        public List<List<int>> IntegerMatrix { get; set; } = new List<List<int>>
        {
            new List<int> { 1, 2, 3 },
            new List<int> { 4, 5, 6 },
            new List<int> { 7, 8, 9 }
        };

        [Category("Nested Lists")]
        [DisplayName("String Groups")]
        [Description("A list of string lists")]
        public List<List<string>> StringGroups { get; set; } = new List<List<string>>
        {
            new List<string> { "Group1-A", "Group1-B" },
            new List<string> { "Group2-A", "Group2-B", "Group2-C" }
        };

        [Category("Nested Lists")]
        [DisplayName("Double Matrix")]
        [Description("A list of double lists")]
        public List<List<double>> DoubleMatrix { get; set; } = new List<List<double>>
        {
            new List<double> { 1.1, 2.2 },
            new List<double> { 3.3, 4.4, 5.5 }
        };
    }

    [Fact]
    public void ListEditorWindow_Constructor_WithNestedIntList_DoesNotThrow()
    {
        // Arrange
        var nestedList = new List<List<int>>
        {
            new List<int> { 1, 2, 3 },
            new List<int> { 4, 5, 6 }
        };

        // Act & Assert
        var window = new ListEditorWindow(nestedList, typeof(List<int>));
        Assert.NotNull(window);
    }

    [Fact]
    public void ListEditorWindow_Constructor_WithNestedStringList_DoesNotThrow()
    {
        // Arrange
        var nestedList = new List<List<string>>
        {
            new List<string> { "A", "B" },
            new List<string> { "C", "D", "E" }
        };

        // Act & Assert
        var window = new ListEditorWindow(nestedList, typeof(List<string>));
        Assert.NotNull(window);
    }

    [Fact]
    public void ListItemEditorWindow_Constructor_WithListType_DoesNotThrow()
    {
        // Arrange
        var innerList = new List<int> { 1, 2, 3 };

        // Act & Assert - This should handle nested list editing
        var window = new ListItemEditorWindow(typeof(List<int>), innerList);
        Assert.NotNull(window);
        Assert.NotNull(window.EditedValue);
    }

    [Fact]
    public void ListItemEditorWindow_Constructor_WithEmptyListType_DoesNotThrow()
    {
        // Arrange & Act & Assert
        var window = new ListItemEditorWindow(typeof(List<int>), null);
        Assert.NotNull(window);
        // Should create a new empty list
        Assert.NotNull(window.EditedValue);
        Assert.IsAssignableFrom<List<int>>(window.EditedValue);
    }

    [Fact]
    public void PropertyEditorWindow_WithNestedListProperties_DoesNotThrow()
    {
        // Arrange
        var config = new TestNestedListConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.Equal(config, window.Config);
    }

    [Fact]
    public void ListEditorWindow_DisplayValue_ShowsListCount()
    {
        // Arrange
        var innerList = new List<int> { 1, 2, 3, 4, 5 };
        var viewModel = new ListEditorWindow.ListItemViewModel
        {
            Index = 0,
            Value = innerList
        };

        // Act
        var displayValue = viewModel.DisplayValue;

        // Assert
        Assert.Contains("5", displayValue); // Should show count
        Assert.Contains("列表", displayValue); // Should indicate it's a list
    }

    [Fact]
    public void ListEditorWindow_DisplayValue_WithNullValue_ShowsNull()
    {
        // Arrange
        var viewModel = new ListEditorWindow.ListItemViewModel
        {
            Index = 0,
            Value = null
        };

        // Act
        var displayValue = viewModel.DisplayValue;

        // Assert
        Assert.Equal("(null)", displayValue);
    }

    [Fact]
    public void ListEditorWindow_DisplayValue_WithNonListValue_ShowsToString()
    {
        // Arrange
        var viewModel = new ListEditorWindow.ListItemViewModel
        {
            Index = 0,
            Value = 42
        };

        // Act
        var displayValue = viewModel.DisplayValue;

        // Assert
        Assert.Equal("42", displayValue);
    }
}
