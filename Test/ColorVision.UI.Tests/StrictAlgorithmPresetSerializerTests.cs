using ColorVision.ImageEditor.Algorithms;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.UI.Tests;

public sealed class StrictAlgorithmPresetSerializerTests
{
    public static TheoryData<string, Func<string>, Action<string>, string> Serializers => new()
    {
        {
            "geometric",
            () => GeometricTransformPresetSerializer.Serialize("geometric", new GeometricTransformParameters { M11 = 2 }),
            json => GeometricTransformPresetSerializer.Deserialize(json),
            "m11"
        },
        {
            "registration",
            () => ImageRegistrationPresetSerializer.Serialize("registration", new ImageRegistrationParameters { MaximumFeatures = 500 }),
            json => ImageRegistrationPresetSerializer.Deserialize(json),
            "maximumFeatures"
        },
        {
            "imaging-correction",
            () => ImagingCorrectionPresetSerializer.Serialize("imaging", new ImagingCorrectionParameters { MinimumGain = 0.5 }),
            json => ImagingCorrectionPresetSerializer.Deserialize(json),
            "minimumGain"
        },
        {
            "lens",
            () => LensDistortionCorrectionPresetSerializer.Serialize("lens", new LensDistortionCorrectionParameters { FxPixels = 1_500 }),
            json => LensDistortionCorrectionPresetSerializer.Deserialize(json),
            "fxPixels"
        },
    };

    [Theory]
    [MemberData(nameof(Serializers))]
    public void StructuralContractRejectsUnknownMissingDuplicateAndInvalidParameterFields(
        string _,
        Func<string> serialize,
        Action<string> deserialize,
        string requiredParameter)
    {
        string valid = serialize();

        JsonObject unknownRoot = Parse(valid);
        unknownRoot["unexpectedField"] = true;
        Assert.Throws<JsonException>(() => deserialize(unknownRoot.ToJsonString()));

        JsonObject missingRoot = Parse(valid);
        Assert.True(missingRoot.Remove("presetId"));
        Assert.Throws<JsonException>(() => deserialize(missingRoot.ToJsonString()));

        JsonObject unknownParameter = Parse(valid);
        unknownParameter["parameters"]!.AsObject()["unexpectedField"] = true;
        Assert.Throws<JsonException>(() => deserialize(unknownParameter.ToJsonString()));

        JsonObject missingParameter = Parse(valid);
        Assert.True(missingParameter["parameters"]!.AsObject().Remove(requiredParameter));
        Assert.Throws<JsonException>(() => deserialize(missingParameter.ToJsonString()));

        JsonObject wrongType = Parse(valid);
        wrongType["parameters"]!.AsObject()[requiredParameter] = "not-a-number";
        Assert.Throws<JsonException>(() => deserialize(wrongType.ToJsonString()));

        JsonObject nonFinite = Parse(valid);
        nonFinite["parameters"]!.AsObject()[requiredParameter] = "NaN";
        Assert.Throws<JsonException>(() => deserialize(nonFinite.ToJsonString()));

        string duplicate = valid.Replace(
            $"\"{requiredParameter}\":",
            $"\"{requiredParameter}\": 1, \"{requiredParameter}\":",
            StringComparison.Ordinal);
        Assert.NotEqual(valid, duplicate);
        Assert.Throws<JsonException>(() => deserialize(duplicate));
    }

    [Theory]
    [MemberData(nameof(Serializers))]
    public void EnvelopeRejectsUnsupportedPresetAndParameterSchemaVersions(
        string _,
        Func<string> serialize,
        Action<string> deserialize,
        string requiredParameter)
    {
        Assert.False(string.IsNullOrWhiteSpace(requiredParameter));
        JsonObject unsupportedPresetSchema = Parse(serialize());
        unsupportedPresetSchema["schema"] = "colorvision.algorithm-parameter-preset/v99";
        Assert.Throws<InvalidOperationException>(() => deserialize(unsupportedPresetSchema.ToJsonString()));

        JsonObject unsupportedParameterSchema = Parse(serialize());
        unsupportedParameterSchema["parameterSchemaVersion"] = 99;
        Assert.Throws<InvalidOperationException>(() => deserialize(unsupportedParameterSchema.ToJsonString()));
    }

    private static JsonObject Parse(string json)
        => JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Fixture preset was not an object.");
}
