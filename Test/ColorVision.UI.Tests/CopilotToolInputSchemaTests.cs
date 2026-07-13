using ColorVision.Copilot;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolInputSchemaTests
{
    [Fact]
    public void QuerySchema_EmitsStrictRequiredJsonSchema()
    {
        var schema = CopilotToolInputSchema.Query("Focused query.", required: true).JsonSchema;

        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.True(schema.GetProperty("properties").TryGetProperty("query", out var query));
        Assert.Equal("string", query.GetProperty("type").GetString());
        Assert.Equal("query", schema.GetProperty("required")[0].GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void TryBind_RejectsUnknownAndMissingArguments()
    {
        var schema = CopilotToolInputSchema.Query("Focused query.", required: true);

        Assert.False(schema.TryBind(new Dictionary<string, object?> { ["path"] = "file.txt" }, out _, out var unknownError));
        Assert.Contains("Unknown argument", unknownError, StringComparison.Ordinal);

        Assert.False(schema.TryBind(new Dictionary<string, object?>(), out _, out var missingError));
        Assert.Contains("Required argument 'query'", missingError, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBind_ParsesJsonArgumentsAndValidatesLineRange()
    {
        var schema = CopilotToolInputSchema.FileRead();
        var arguments = new Dictionary<string, object?>
        {
            ["path"] = JsonSerializer.SerializeToElement("README.md"),
            ["startLine"] = JsonSerializer.SerializeToElement(20),
            ["endLine"] = JsonSerializer.SerializeToElement(10),
        };

        Assert.False(schema.TryBind(arguments, out _, out var error));
        Assert.Contains("endLine", error, StringComparison.Ordinal);

        arguments["endLine"] = JsonSerializer.SerializeToElement(30);
        Assert.True(schema.TryBind(arguments, out var input, out error), error);
        Assert.Equal("README.md", input.Path);
        Assert.Equal(20, input.StartLine);
        Assert.Equal(30, input.EndLine);
    }
}
