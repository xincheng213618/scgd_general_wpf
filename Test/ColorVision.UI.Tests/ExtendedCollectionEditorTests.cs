using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ColorVision.UI;
using ColorVision.UI.PropertyEditor.Editor.Dictionary;

namespace ColorVision.UI.Tests;

/// <summary>
/// Tests for extended collection support in PropertyEditor
/// Tests for ObservableCollection, Collection, IEnumerable, and Dictionary support
/// </summary>
public class ExtendedCollectionEditorTests
{
    // Test enum
    public enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    // Test class with various collection properties
    private sealed class TestCollectionConfig
    {
        [Category("Collections")]
        [DisplayName("ObservableCollection of Int")]
        public ObservableCollection<int> ObservableInts { get; set; } = new ObservableCollection<int> { 1, 2, 3 };

        [Category("Collections")]
        [DisplayName("Collection of String")]
        public System.Collections.ObjectModel.Collection<string> StringCollection { get; set; } = 
            new System.Collections.ObjectModel.Collection<string> { "A", "B", "C" };

        [Category("Collections")]
        [DisplayName("IList of Double")]
        public IList<double> DoubleList { get; set; } = new List<double> { 1.1, 2.2, 3.3 };

        [Category("Collections")]
        [DisplayName("ICollection of Int")]
        public ICollection<int> IntCollection { get; set; } = new List<int> { 10, 20, 30 };

        [Category("Collections")]
        [DisplayName("IEnumerable of String")]
        public IEnumerable<string> StringEnumerable { get; set; } = new List<string> { "X", "Y", "Z" };

        [Category("Dictionaries")]
        [DisplayName("String to Int Dictionary")]
        public Dictionary<string, int> StringIntDict { get; set; } = new Dictionary<string, int> 
        { 
            { "one", 1 }, 
            { "two", 2 }, 
            { "three", 3 } 
        };

        [Category("Dictionaries")]
        [DisplayName("Int to String Dictionary")]
        public Dictionary<int, string> IntStringDict { get; set; } = new Dictionary<int, string> 
        { 
            { 1, "one" }, 
            { 2, "two" }, 
            { 3, "three" } 
        };

        [Category("Dictionaries")]
        [DisplayName("IDictionary of String to Double")]
        public IDictionary<string, double> StringDoubleDict { get; set; } = new Dictionary<string, double> 
        { 
            { "pi", 3.14 }, 
            { "e", 2.72 } 
        };

        [Category("Dictionaries")]
        [DisplayName("Enum to String Dictionary")]
        public Dictionary<TestEnum, string> EnumStringDict { get; set; } = new Dictionary<TestEnum, string> 
        { 
            { TestEnum.Value1, "First" }, 
            { TestEnum.Value2, "Second" } 
        };
    }

    [Fact]
    public void PropertyEditorWindow_WithObservableCollection_DoesNotThrow()
    {
        // Arrange
        var config = new TestCollectionConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.Equal(config, window.Config);
    }

    [Fact]
    public void PropertyEditorWindow_WithCollection_DoesNotThrow()
    {
        // Arrange
        var config = new TestCollectionConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.NotNull(config.StringCollection);
        Assert.Equal(3, config.StringCollection.Count);
    }

    [Fact]
    public void PropertyEditorWindow_WithIListInterface_DoesNotThrow()
    {
        // Arrange
        var config = new TestCollectionConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.NotNull(config.DoubleList);
        Assert.Equal(3, config.DoubleList.Count);
    }

    [Fact]
    public void PropertyEditorWindow_WithICollectionInterface_DoesNotThrow()
    {
        // Arrange
        var config = new TestCollectionConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.NotNull(config.IntCollection);
        Assert.Equal(3, config.IntCollection.Count);
    }

    [Fact]
    public void PropertyEditorWindow_WithIEnumerableInterface_DoesNotThrow()
    {
        // Arrange
        var config = new TestCollectionConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.NotNull(config.StringEnumerable);
    }

    [Fact]
    public void PropertyEditorWindow_WithDictionaries_DoesNotThrow()
    {
        // Arrange
        var config = new TestCollectionConfig();

        // Act & Assert
        var window = new PropertyEditorWindow(config, isEdit: true);
        Assert.NotNull(window);
        Assert.Equal(config, window.Config);
    }

    [Fact]
    public void DictionaryEditorWindow_Constructor_WithStringIntDict_DoesNotThrow()
    {
        // Arrange
        var dict = new Dictionary<string, int> { { "test", 123 } };

        // Act & Assert
        var window = new DictionaryEditorWindow(dict, typeof(string), typeof(int));
        Assert.NotNull(window);
    }

    [Fact]
    public void DictionaryEditorWindow_Constructor_WithIntStringDict_DoesNotThrow()
    {
        // Arrange
        var dict = new Dictionary<int, string> { { 1, "one" }, { 2, "two" } };

        // Act & Assert
        var window = new DictionaryEditorWindow(dict, typeof(int), typeof(string));
        Assert.NotNull(window);
    }

    [Fact]
    public void DictionaryEditorWindow_Constructor_WithEnumDict_DoesNotThrow()
    {
        // Arrange
        var dict = new Dictionary<TestEnum, string> 
        { 
            { TestEnum.Value1, "First" },
            { TestEnum.Value2, "Second" }
        };

        // Act & Assert
        var window = new DictionaryEditorWindow(dict, typeof(TestEnum), typeof(string));
        Assert.NotNull(window);
    }

    [Fact]
    public void DictionaryItemEditorWindow_Constructor_DoesNotThrow()
    {
        // Arrange
        var existingKeys = new List<object> { "key1", "key2" };

        // Act & Assert
        var window = new DictionaryItemEditorWindow(
            typeof(string), 
            typeof(int), 
            "newKey", 
            42, 
            existingKeys);
        Assert.NotNull(window);
        Assert.Equal("newKey", window.EditedKey);
        Assert.Equal(42, window.EditedValue);
    }

    [Fact]
    public void JsonNumericListConverter_SupportsObservableCollection()
    {
        // Arrange
        var converter = new JsonNumericListConverter();
        var obsCollection = new ObservableCollection<int> { 1, 2, 3 };

        // Act
        var json = converter.Convert(obsCollection, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("1", json.ToString());
        Assert.Contains("2", json.ToString());
        Assert.Contains("3", json.ToString());
    }

    [Fact]
    public void JsonNumericListConverter_SupportsCollection()
    {
        // Arrange
        var converter = new JsonNumericListConverter();
        var collection = new System.Collections.ObjectModel.Collection<string> { "A", "B", "C" };

        // Act
        var json = converter.Convert(collection, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"A\"", json.ToString());
        Assert.Contains("\"B\"", json.ToString());
        Assert.Contains("\"C\"", json.ToString());
    }

    [Fact]
    public void JsonDictionaryConverter_ConvertsDictionaryToJson()
    {
        // Arrange
        var converter = new JsonDictionaryConverter();
        var dict = new Dictionary<string, int> { { "one", 1 }, { "two", 2 } };

        // Act
        var json = converter.Convert(dict, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);

        // Assert
        Assert.NotNull(json);
        var jsonString = json.ToString();
        Assert.Contains("\"one\"", jsonString);
        Assert.Contains("\"two\"", jsonString);
    }

    [Fact]
    public void JsonDictionaryConverter_ConvertsJsonToDictionary()
    {
        // Arrange
        var converter = new JsonDictionaryConverter();
        var json = "{\"one\":1,\"two\":2}";

        // Act
        var result = converter.ConvertBack(json, typeof(Dictionary<string, int>), null, System.Globalization.CultureInfo.CurrentCulture);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, int>>(result);
        var dict = (Dictionary<string, int>)result;
        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["one"]);
        Assert.Equal(2, dict["two"]);
    }

    [Fact]
    public void JsonDictionaryConverter_EmptyStringCreatesEmptyDictionary()
    {
        // Arrange
        var converter = new JsonDictionaryConverter();

        // Act
        var result = converter.ConvertBack("", typeof(Dictionary<string, int>), null, System.Globalization.CultureInfo.CurrentCulture);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Dictionary<string, int>>(result);
        var dict = (Dictionary<string, int>)result;
        Assert.Empty(dict);
    }
}
