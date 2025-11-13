using System.ComponentModel;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

/// <summary>
/// Tests for PropertyEditorWindow sorting functionality
/// </summary>
public class PropertyEditorWindowTests
{
    // Test class with properties to display in PropertyEditorWindow
    private class TestConfig
    {
        [Category("General")]
        [DisplayName("Name")]
        [Description("The name of the item")]
        public string Name { get; set; } = "Test";

        [Category("General")]
        [DisplayName("Age")]
        [Description("The age of the item")]
        public int Age { get; set; } = 25;

        [Category("Settings")]
        [DisplayName("Enabled")]
        [Description("Whether the item is enabled")]
        public bool Enabled { get; set; } = true;

        [Category("Settings")]
        [DisplayName("Count")]
        [Description("The count value")]
        public int Count { get; set; } = 10;

        [Category("Advanced")]
        [DisplayName("Value")]
        [Description("An advanced value")]
        public double Value { get; set; } = 3.14;
    }

    [Fact]
    public void PropertySortMode_Enum_HasExpectedValues()
    {
        // Assert that the PropertySortMode enum has all expected values
        Assert.True(Enum.IsDefined(typeof(PropertySortMode), PropertySortMode.Default));
        Assert.True(Enum.IsDefined(typeof(PropertySortMode), PropertySortMode.NameAscending));
        Assert.True(Enum.IsDefined(typeof(PropertySortMode), PropertySortMode.NameDescending));
        Assert.True(Enum.IsDefined(typeof(PropertySortMode), PropertySortMode.CategoryAscending));
        Assert.True(Enum.IsDefined(typeof(PropertySortMode), PropertySortMode.CategoryDescending));
    }

    [Fact]
    public void PropertyEditorWindow_Constructor_WithConfig_DoesNotThrow()
    {
        // Arrange
        var config = new TestConfig();

        // Act & Assert - Should not throw when creating window
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.Equal(config, window.Config);
        Assert.True(window.IsEdit);
    }

    [Fact]
    public void PropertyEditorWindow_Constructor_WithNonEditMode_DoesNotThrow()
    {
        // Arrange
        var config = new TestConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: false);
        Assert.NotNull(window);
        Assert.Equal(config, window.Config);
        Assert.False(window.IsEdit);
    }

    [Fact]
    public void PropertyEditorWindow_CategoryGroups_IsInitializedEmpty()
    {
        // Arrange
        var config = new TestConfig();
        var window = new PropertyEditorWindow(config, isEdit: true);

        // Assert - Before initialization, categoryGroups should be empty
        Assert.NotNull(window.categoryGroups);
        Assert.Empty(window.categoryGroups);
    }
}
