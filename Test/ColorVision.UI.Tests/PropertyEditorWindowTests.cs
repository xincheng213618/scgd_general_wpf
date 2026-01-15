using System.ComponentModel;

namespace ColorVision.UI.Tests;

/// <summary>
/// Tests for PropertyEditorWindow sorting functionality
/// </summary>
public class PropertyEditorWindowTests
{
    // Test enum for visibility testing
    public enum TestMode
    {
        ModeA,
        ModeB,
        ModeC
    }

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

    // Test class with enum visibility attributes
    private class TestConfigWithEnumVisibility
    {
        [Category("General")]
        [DisplayName("Mode")]
        [Description("The current mode")]
        public TestMode Mode { get; set; } = TestMode.ModeA;

        [Category("Settings")]
        [DisplayName("Setting for Mode A")]
        [Description("This should only be visible when Mode is ModeA")]
        [PropertyVisibility(nameof(Mode), TestMode.ModeA)]
        public string SettingForModeA { get; set; } = "A Setting";

        [Category("Settings")]
        [DisplayName("Setting for Mode B")]
        [Description("This should only be visible when Mode is ModeB")]
        [PropertyVisibility(nameof(Mode), TestMode.ModeB)]
        public string SettingForModeB { get; set; } = "B Setting";

        [Category("Settings")]
        [DisplayName("Setting for Not Mode A")]
        [Description("This should be hidden when Mode is ModeA")]
        [PropertyVisibility(nameof(Mode), TestMode.ModeA, isInverted: true)]
        public string SettingForNotModeA { get; set; } = "Not A Setting";

        [Category("Advanced")]
        [DisplayName("Boolean Visible Setting")]
        [Description("This should be visible when BoolFlag is true")]
        [PropertyVisibility(nameof(BoolFlag))]
        public string BoolVisibleSetting { get; set; } = "Bool Setting";

        [Category("Advanced")]
        [DisplayName("Boolean Flag")]
        [Description("A boolean flag for testing")]
        public bool BoolFlag { get; set; } = true;
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

    [Fact]
    public void PropertyVisibilityAttribute_WithBooleanProperty_CreatesCorrectAttribute()
    {
        // Arrange & Act
        var attr = new PropertyVisibilityAttribute("IsEnabled");

        // Assert
        Assert.Equal("IsEnabled", attr.PropertyName);
        Assert.False(attr.IsInverted);
        Assert.Null(attr.ExpectedValue);
    }

    [Fact]
    public void PropertyVisibilityAttribute_WithBooleanPropertyInverted_CreatesCorrectAttribute()
    {
        // Arrange & Act
        var attr = new PropertyVisibilityAttribute("IsEnabled", isInverted: true);

        // Assert
        Assert.Equal("IsEnabled", attr.PropertyName);
        Assert.True(attr.IsInverted);
        Assert.Null(attr.ExpectedValue);
    }

    [Fact]
    public void PropertyVisibilityAttribute_WithEnumValue_CreatesCorrectAttribute()
    {
        // Arrange & Act
        var attr = new PropertyVisibilityAttribute("Mode", TestMode.ModeA);

        // Assert
        Assert.Equal("Mode", attr.PropertyName);
        Assert.False(attr.IsInverted);
        Assert.Equal(TestMode.ModeA, attr.ExpectedValue);
    }

    [Fact]
    public void PropertyVisibilityAttribute_WithEnumValueInverted_CreatesCorrectAttribute()
    {
        // Arrange & Act
        var attr = new PropertyVisibilityAttribute("Mode", TestMode.ModeB, isInverted: true);

        // Assert
        Assert.Equal("Mode", attr.PropertyName);
        Assert.True(attr.IsInverted);
        Assert.Equal(TestMode.ModeB, attr.ExpectedValue);
    }

    [Fact]
    public void PropertyEditorWindow_WithEnumVisibilityConfig_DoesNotThrow()
    {
        // Arrange
        var config = new TestConfigWithEnumVisibility();

        // Act & Assert - Should not throw when creating window with enum visibility attributes
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.Equal(config, window.Config);
    }
}
