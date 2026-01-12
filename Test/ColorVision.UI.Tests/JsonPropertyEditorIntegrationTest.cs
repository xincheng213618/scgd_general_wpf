using Newtonsoft.Json.Linq;
using ColorVision.UI.PropertyEditor.Json;
using Xunit;
using System.Linq;

namespace ColorVision.UI.Tests;

/// <summary>
/// Integration tests for JSON PropertyEditor with WPF binding
/// </summary>
public class JsonPropertyEditorIntegrationTest
{
    [Fact]
    public void JsonObjectWrapper_ICustomTypeDescriptor_ExposesProperties()
    {
        // Arrange
        var json = @"{
            ""name"": ""Test"",
            ""age"": 25,
            ""enabled"": true,
            ""value"": 3.14
        }";
        var jObject = JObject.Parse(json);

        // Act
        var wrapper = JsonPropertyEditorConverter.ToObject(jObject) as JsonObjectWrapper;

        // Assert
        Assert.NotNull(wrapper);
        
        // Test ICustomTypeDescriptor
        var typeDescriptor = wrapper as System.ComponentModel.ICustomTypeDescriptor;
        Assert.NotNull(typeDescriptor);
        
        var properties = typeDescriptor!.GetProperties();
        Assert.NotNull(properties);
        Assert.Equal(4, properties.Count);
        
        // Verify each property descriptor
        var nameDesc = properties["name"];
        Assert.NotNull(nameDesc);
        Assert.Equal(typeof(string), nameDesc!.PropertyType);
        Assert.Equal("Test", nameDesc.GetValue(wrapper));
        
        var ageDesc = properties["age"];
        Assert.NotNull(ageDesc);
        Assert.Equal(typeof(long), ageDesc!.PropertyType);
        Assert.Equal(25L, ageDesc.GetValue(wrapper));
        
        var enabledDesc = properties["enabled"];
        Assert.NotNull(enabledDesc);
        Assert.Equal(typeof(bool), enabledDesc!.PropertyType);
        Assert.Equal(true, enabledDesc.GetValue(wrapper));
        
        var valueDesc = properties["value"];
        Assert.NotNull(valueDesc);
        Assert.Equal(typeof(double), valueDesc!.PropertyType);
        Assert.Equal(3.14, valueDesc.GetValue(wrapper));
    }

    [Fact]
    public void JsonObjectWrapper_SetValue_TriggersPropertyChanged()
    {
        // Arrange
        var json = @"{""name"": ""Test"", ""age"": 25}";
        var jObject = JObject.Parse(json);
        var wrapper = JsonPropertyEditorConverter.ToObject(jObject) as JsonObjectWrapper;
        
        Assert.NotNull(wrapper);
        
        var typeDescriptor = wrapper as System.ComponentModel.ICustomTypeDescriptor;
        var properties = typeDescriptor!.GetProperties();
        var nameDesc = properties["name"];
        
        string? changedPropertyName = null;
        var notifyWrapper = wrapper as System.ComponentModel.INotifyPropertyChanged;
        notifyWrapper!.PropertyChanged += (s, e) => changedPropertyName = e.PropertyName;

        // Act
        nameDesc!.SetValue(wrapper, "Updated");

        // Assert
        Assert.Equal("name", changedPropertyName);
        Assert.Equal("Updated", nameDesc.GetValue(wrapper));
        Assert.Equal("Updated", wrapper!.GetValue("name"));
    }

    [Fact]
    public void JsonObjectWrapper_PropertyTypes_MatchExpected()
    {
        // Arrange
        var json = @"{
            ""intValue"": 42,
            ""floatValue"": 3.14,
            ""boolValue"": true,
            ""stringValue"": ""hello"",
            ""nested"": {
                ""innerValue"": 123
            }
        }";
        var jObject = JObject.Parse(json);

        // Act
        var wrapper = JsonPropertyEditorConverter.ToObject(jObject) as JsonObjectWrapper;
        var typeDescriptor = wrapper as System.ComponentModel.ICustomTypeDescriptor;
        var properties = typeDescriptor!.GetProperties();

        // Assert
        Assert.Equal(typeof(long), properties["intValue"]!.PropertyType);
        Assert.Equal(typeof(double), properties["floatValue"]!.PropertyType);
        Assert.Equal(typeof(bool), properties["boolValue"]!.PropertyType);
        Assert.Equal(typeof(string), properties["stringValue"]!.PropertyType);
        Assert.Equal(typeof(JsonObjectWrapper), properties["nested"]!.PropertyType);
    }

    [Fact]
    public void JsonObjectWrapper_GetProperties_ReturnsPropertyInfoList()
    {
        // Arrange
        var json = @"{""a"": 1, ""b"": 2, ""c"": 3}";
        var jObject = JObject.Parse(json);
        var wrapper = JsonPropertyEditorConverter.ToObject(jObject) as JsonObjectWrapper;

        // Act
        var props = wrapper!.GetProperties().ToList();

        // Assert
        Assert.Equal(3, props.Count);
        Assert.Contains(props, p => p.Name == "a");
        Assert.Contains(props, p => p.Name == "b");
        Assert.Contains(props, p => p.Name == "c");
        
        // Verify all are type long (integer JSON values)
        Assert.All(props, p => Assert.Equal(typeof(long), p.PropertyType));
    }
}
