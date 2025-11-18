using System.Collections.Generic;
using System.ComponentModel;
using ColorVision.UI;
using ColorVision.UI.PropertyEditor.Editor.List;

namespace ColorVision.UI.Tests;

/// <summary>
/// Tests for ListNumericJsonEditor and ListEditorWindow functionality
/// </summary>
public class ListEditorTests
{
    // Test enum
    public enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    // Test class with list properties
    private class TestListConfig
    {
        [Category("Lists")]
        [DisplayName("Integer List")]
        [Description("A list of integers")]
        public List<int> IntegerList { get; set; } = new List<int> { 1, 2, 3, 4, 5 };

        [Category("Lists")]
        [DisplayName("String List")]
        [Description("A list of strings")]
        public List<string> StringList { get; set; } = new List<string> { "Apple", "Banana", "Cherry" };

        [Category("Lists")]
        [DisplayName("Double List")]
        [Description("A list of doubles")]
        public List<double> DoubleList { get; set; } = new List<double> { 1.1, 2.2, 3.3 };

        [Category("Lists")]
        [DisplayName("Enum List")]
        [Description("A list of enums")]
        public List<TestEnum> EnumList { get; set; } = new List<TestEnum> { TestEnum.Value1, TestEnum.Value2 };
    }

    [Fact]
    public void ListEditorWindow_Constructor_WithIntList_DoesNotThrow()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };

        // Act & Assert
        var window = new ListEditorWindow(list, typeof(int));
        Assert.NotNull(window);
    }

    [Fact]
    public void ListEditorWindow_Constructor_WithStringList_DoesNotThrow()
    {
        // Arrange
        var list = new List<string> { "Test1", "Test2" };

        // Act & Assert
        var window = new ListEditorWindow(list, typeof(string));
        Assert.NotNull(window);
    }

    [Fact]
    public void ListItemEditorWindow_Constructor_WithIntType_DoesNotThrow()
    {
        // Arrange & Act & Assert
        var window = new ListItemEditorWindow(typeof(int), 42);
        Assert.NotNull(window);
        Assert.Equal(42, window.EditedValue);
    }

    [Fact]
    public void ListItemEditorWindow_Constructor_WithStringType_DoesNotThrow()
    {
        // Arrange & Act & Assert
        var window = new ListItemEditorWindow(typeof(string), "Test");
        Assert.NotNull(window);
        Assert.Equal("Test", window.EditedValue);
    }

    [Fact]
    public void ListItemEditorWindow_Constructor_WithEnumType_DoesNotThrow()
    {
        // Arrange & Act & Assert
        var window = new ListItemEditorWindow(typeof(TestEnum), TestEnum.Value2);
        Assert.NotNull(window);
        Assert.Equal(TestEnum.Value2, window.EditedValue);
    }

    [Fact]
    public void ListEditorWindow_WithEnumList_DoesNotThrow()
    {
        // Arrange
        var list = new List<TestEnum> { TestEnum.Value1, TestEnum.Value3 };

        // Act & Assert
        var window = new ListEditorWindow(list, typeof(TestEnum));
        Assert.NotNull(window);
    }

    [Fact]
    public void PropertyEditorWindow_WithListProperties_DoesNotThrow()
    {
        // Arrange
        var config = new TestListConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.Equal(config, window.Config);
    }

    [Fact]
    public void ListEditorWindow_WorkingCopy_DoesNotModifyOriginal()
    {
        // Arrange
        var originalList = new List<int> { 1, 2, 3 };
        var originalCount = originalList.Count;

        // Act - Create window (which creates working copy)
        var window = new ListEditorWindow(originalList, typeof(int));

        // Assert - Original list should remain unchanged
        Assert.Equal(originalCount, originalList.Count);
    }
}
