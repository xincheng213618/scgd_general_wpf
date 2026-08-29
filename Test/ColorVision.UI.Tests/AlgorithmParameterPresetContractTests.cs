using ColorVision.Algorithms;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmParameterPresetContractTests
{
    [Fact]
    public void MissingVersionAndNullMetadataAreRejectedBeforeToInvocation()
    {
        AlgorithmParameterPreset preset = Deserialize("""
            {
              "schema": "colorvision.algorithm-parameter-preset/v1",
              "presetId": "malicious",
              "algorithmId": "test.invert",
              "parameterSchemaVersion": 1,
              "parameters": {},
              "metadata": null
            }
            """);

        AlgorithmValidationResult validation = preset.Validate();

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(AlgorithmParameterPreset.AlgorithmVersion));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(AlgorithmParameterPreset.Metadata));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => preset.ToInvocation());
        Assert.Contains(nameof(AlgorithmParameterPreset.AlgorithmVersion), exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ArgumentNullException), exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ParametersMustBeABoundedFiniteJsonObject()
    {
        AlgorithmParameterPreset stringParameters = ValidPreset(JsonSerializer.SerializeToElement("not-an-object"));
        AlgorithmParameterPreset nonFinite = ValidPreset(JsonSerializer.SerializeToElement(new { gain = "NaN" }));
        AlgorithmParameterPreset tooDeep = ValidPreset(Parse("{" + string.Concat(Enumerable.Repeat("\"child\":{", 33))
            + "\"value\":1" + new string('}', 33) + "}"));
        AlgorithmParameterPreset tooLarge = ValidPreset(JsonSerializer.SerializeToElement(new { value = new string('x', 1_048_577) }));

        Assert.Contains(stringParameters.Validate().Issues, issue => issue.Code == "parameters_not_object");
        Assert.Contains(nonFinite.Validate().Issues, issue => issue.Code == "non_finite_number");
        Assert.Contains(tooDeep.Validate().Issues, issue => issue.Code == "json_depth_exceeded");
        Assert.Contains(tooLarge.Validate().Issues, issue => issue.Code == "json_size_exceeded");
        Assert.Throws<InvalidOperationException>(() => stringParameters.ToInvocation());
    }

    [Fact]
    public void MetadataKeysValuesCountsAndAggregateSizeAreValidated()
    {
        Dictionary<string, string> invalid = new()
        {
            [""] = "blank-key",
            [new string('k', 129)] = "long-key",
            ["null-value"] = null!,
            ["long-value"] = new string('v', 4097),
        };
        AlgorithmParameterPreset preset = ValidPreset(JsonSerializer.SerializeToElement(new { }), invalid);

        AlgorithmValidationResult validation = preset.Validate();

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Code == "metadata_key_invalid");
        Assert.Contains(validation.Issues, issue => issue.Code == "metadata_key_too_long");
        Assert.Contains(validation.Issues, issue => issue.Code == "metadata_value_invalid");
        Assert.Contains(validation.Issues, issue => issue.Code == "metadata_value_too_long");

        AlgorithmParameterPreset tooMany = ValidPreset(
            JsonSerializer.SerializeToElement(new { }),
            Enumerable.Range(0, AlgorithmParameterPreset.MaximumMetadataEntries + 1)
                .ToDictionary(index => $"key-{index}", _ => "value"));
        AlgorithmParameterPreset aggregateTooLarge = ValidPreset(
            JsonSerializer.SerializeToElement(new { }),
            Enumerable.Range(0, 17).ToDictionary(index => $"key-{index}", _ => new string('v', 4_096)));

        Assert.Contains(tooMany.Validate().Issues, issue => issue.Code == "metadata_count_exceeded");
        Assert.Contains(aggregateTooLarge.Validate().Issues, issue => issue.Code == "metadata_size_exceeded");
    }

    [Fact]
    public void DuplicateParameterPropertiesAndOversizedPresetIdsAreRejected()
    {
        AlgorithmParameterPreset duplicate = ValidPreset(Parse("{\"value\":1,\"value\":2}"));
        AlgorithmParameterPreset oversizedId = new()
        {
            PresetId = new string('p', AlgorithmParameterPreset.MaximumPresetIdLength + 1),
            AlgorithmId = new AlgorithmId("test.invert"),
            AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
            ParameterSchemaVersion = 1,
            Parameters = JsonSerializer.SerializeToElement(new { }),
        };

        Assert.Contains(duplicate.Validate().Issues, issue => issue.Code == "duplicate_parameter_property");
        Assert.Contains(oversizedId.Validate().Issues, issue => issue.Code == "preset_id_too_long");
    }

    [Fact]
    public void ValidPresetRoundTripsAndCreatesAnEquivalentInvocation()
    {
        AlgorithmParameterPreset source = ValidPreset(
            JsonSerializer.SerializeToElement(new { amount = 12.5, enabled = true }),
            new Dictionary<string, string> { ["source"] = "fixture" });
        string json = JsonSerializer.Serialize(source, AlgorithmJson.Options);
        AlgorithmParameterPreset restored = Deserialize(json);

        Assert.True(restored.Validate().IsValid);
        AlgorithmInvocation invocation = restored.ToInvocation();
        Assert.Equal(source.AlgorithmId, invocation.AlgorithmId);
        Assert.Equal(source.AlgorithmVersion, invocation.AlgorithmVersion);
        Assert.Equal("fixture", invocation.Metadata["source"]);
        Assert.Equal(JsonValueKind.Object, invocation.Parameters.ValueKind);
        Assert.Equal(12.5, invocation.Parameters.GetProperty("amount").GetDouble());
    }

    private static AlgorithmParameterPreset ValidPreset(
        JsonElement parameters,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new()
        {
            PresetId = "fixture",
            AlgorithmId = new AlgorithmId("test.invert"),
            AlgorithmVersion = new AlgorithmVersion(1, 2, 3),
            ParameterSchemaVersion = 1,
            Parameters = parameters,
            Metadata = metadata ?? new Dictionary<string, string>(),
        };

    private static AlgorithmParameterPreset Deserialize(string json)
        => JsonSerializer.Deserialize<AlgorithmParameterPreset>(json, AlgorithmJson.Options)!;

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 128 });
        return document.RootElement.Clone();
    }
}
