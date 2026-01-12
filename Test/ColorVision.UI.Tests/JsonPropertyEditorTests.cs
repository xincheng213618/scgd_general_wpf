using Newtonsoft.Json.Linq;
using ColorVision.UI.PropertyEditor.Json;
using System.Linq;

namespace ColorVision.UI.Tests;

/// <summary>
/// Tests for JSON PropertyEditor functionality
/// </summary>
public class JsonPropertyEditorTests
{
    [Fact]
    public void JsonPropertyEditorConverter_SimpleObject_ConvertsCorrectly()
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
        var result = JsonPropertyEditorConverter.ToObject(jObject);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<JsonObjectWrapper>(result);
        
        var wrapper = result as JsonObjectWrapper;
        Assert.NotNull(wrapper);
        
        // Verify properties exist
        var properties = wrapper!.GetProperties().ToList();
        Assert.Equal(4, properties.Count);
        
        // Verify property values
        Assert.Equal("Test", wrapper.GetValue("name"));
        Assert.Equal(25L, wrapper.GetValue("age")); // JSON integers are parsed as long
        Assert.Equal(true, wrapper.GetValue("enabled"));
        Assert.Equal(3.14, wrapper.GetValue("value"));
    }

    [Fact]
    public void JsonPropertyEditorConverter_RoundTrip_PreservesData()
    {
        // Arrange
        var originalJson = @"{
            ""name"": ""Test"",
            ""count"": 42,
            ""active"": true
        }";
        var jObject = JObject.Parse(originalJson);

        // Act - Convert to object and back
        var obj = JsonPropertyEditorConverter.ToObject(jObject);
        var resultJObject = JsonPropertyEditorConverter.ToJObject(obj);

        // Assert - Values should match
        Assert.Equal(jObject["name"]?.ToString(), resultJObject["name"]?.ToString());
        Assert.Equal(jObject["count"]?.Value<long>(), resultJObject["count"]?.Value<long>());
        Assert.Equal(jObject["active"]?.Value<bool>(), resultJObject["active"]?.Value<bool>());
    }

    [Fact]
    public void JsonPropertyEditorConverter_NestedObject_ConvertsRecursively()
    {
        // Arrange
        var json = @"{
            ""user"": {
                ""name"": ""John"",
                ""age"": 30
            },
            ""enabled"": true
        }";
        var jObject = JObject.Parse(json);

        // Act
        var result = JsonPropertyEditorConverter.ToObject(jObject);

        // Assert
        Assert.NotNull(result);
        var wrapper = result as JsonObjectWrapper;
        Assert.NotNull(wrapper);
        
        // Verify nested object
        var nestedUser = wrapper!.GetValue("user");
        Assert.NotNull(nestedUser);
        Assert.IsType<JsonObjectWrapper>(nestedUser);
        
        var nestedWrapper = nestedUser as JsonObjectWrapper;
        Assert.Equal("John", nestedWrapper!.GetValue("name"));
        Assert.Equal(30L, nestedWrapper.GetValue("age"));
    }

    [Fact]
    public void JsonPropertyEditorConverter_Array_ConvertsList()
    {
        // Arrange
        var json = @"{
            ""numbers"": [1, 2, 3, 4, 5],
            ""names"": [""Alice"", ""Bob"", ""Charlie""]
        }";
        var jObject = JObject.Parse(json);

        // Act
        var result = JsonPropertyEditorConverter.ToObject(jObject);

        // Assert
        var wrapper = result as JsonObjectWrapper;
        Assert.NotNull(wrapper);
        
        // Verify array of numbers
        var numbers = wrapper!.GetValue("numbers") as System.Collections.IList;
        Assert.NotNull(numbers);
        Assert.Equal(5, numbers!.Count);
        
        // Verify array of strings
        var names = wrapper.GetValue("names") as System.Collections.IList;
        Assert.NotNull(names);
        Assert.Equal(3, names!.Count);
    }

    [Fact]
    public void JsonObjectWrapper_PropertyChanged_FiresEvent()
    {
        // Arrange
        var wrapper = new JsonObjectWrapper();
        wrapper.AddProperty("testProp", typeof(string), "initial", "Test", "Test Property");
        
        bool eventFired = false;
        string? changedPropertyName = null;
        
        wrapper.PropertyChanged += (sender, args) =>
        {
            eventFired = true;
            changedPropertyName = args.PropertyName;
        };

        // Act
        wrapper.SetValue("testProp", "updated");

        // Assert
        Assert.True(eventFired);
        Assert.Equal("testProp", changedPropertyName);
        Assert.Equal("updated", wrapper.GetValue("testProp"));
    }

    [Fact]
    public void JsonObjectWrapper_GetProperties_ReturnsAllProperties()
    {
        // Arrange
        var wrapper = new JsonObjectWrapper();
        wrapper.AddProperty("prop1", typeof(string), "value1");
        wrapper.AddProperty("prop2", typeof(int), 42);
        wrapper.AddProperty("prop3", typeof(bool), true);

        // Act
        var properties = wrapper.GetProperties().ToList();

        // Assert
        Assert.Equal(3, properties.Count);
        Assert.Contains(properties, p => p.Name == "prop1");
        Assert.Contains(properties, p => p.Name == "prop2");
        Assert.Contains(properties, p => p.Name == "prop3");
    }

    [Fact]
    public void JsonPropertyEditorConverter_EmptyObject_HandlesGracefully()
    {
        // Arrange
        var json = "{}";
        var jObject = JObject.Parse(json);

        // Act
        var result = JsonPropertyEditorConverter.ToObject(jObject);

        // Assert
        Assert.NotNull(result);
        var wrapper = result as JsonObjectWrapper;
        Assert.NotNull(wrapper);
        Assert.Empty(wrapper!.GetProperties());
    }

    [Fact]
    public void JsonPropertyEditorConverter_NullValue_HandlesCorrectly()
    {
        // Arrange
        var json = @"{
            ""name"": ""Test"",
            ""value"": null
        }";
        var jObject = JObject.Parse(json);

        // Act
        var result = JsonPropertyEditorConverter.ToObject(jObject);

        // Assert
        var wrapper = result as JsonObjectWrapper;
        Assert.NotNull(wrapper);
        
        // Null should be converted to default value for string (empty string or null)
        var value = wrapper!.GetValue("value");
        Assert.True(value == null || value is string);
    }
}
