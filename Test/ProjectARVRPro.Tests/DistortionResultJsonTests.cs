using ColorVision.Engine.Templates.Jsons.Distortion2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process.Distortion;

namespace ProjectARVRPro.Tests;

public sealed class DistortionResultJsonTests
{
    [Fact]
    public void ProjectOutput_UsesStandardOpticalFieldWhileAlgorithmInputKeepsSourceMapping()
    {
        var algorithm = JsonConvert.DeserializeObject<DistortionReslut>(
            """{"Optic_Distortion":{"opticRatio":-0.36}}""")!;
        var result = new ObjectiveTestResult
        {
            DistortionTestResult = new DistortionTestResult
            {
                OpticDistortion = new ObjectiveTestItem
                {
                    Name = nameof(DistortionTestResult.OpticDistortion),
                    Value = algorithm.OpticDistortion.OpticRatio
                }
            }
        };

        var json = JObject.Parse(JsonConvert.SerializeObject(result));
        var distortion = (JObject)json[nameof(ObjectiveTestResult.DistortionTestResult)]!;
        Assert.Equal(-0.36, distortion["OpticDistortion"]!["Value"]!.Value<double>());
        Assert.Equal("OpticDistortion", distortion["OpticDistortion"]!["Name"]!.Value<string>());
        Assert.Null(distortion.Property("Optic_Distortion"));
        Assert.Null(distortion.Property("LegacyOpticDistortion"));
    }

    [Theory]
    [InlineData("""{"Optic_Distortion":{"Value":1}}""", 1)]
    [InlineData("""{"OpticDistortion":{"Value":2}}""", 2)]
    [InlineData("""{"Optic_Distortion":{"Value":1},"OpticDistortion":{"Value":2}}""", 2)]
    [InlineData("""{"OpticDistortion":{"Value":2},"Optic_Distortion":{"Value":1}}""", 2)]
    public void HistoricalResults_ReadBothFieldNamesAndWriteOnlyStandardField(string input, double expected)
    {
        var result = JsonConvert.DeserializeObject<DistortionViewTestResult>(input)!;

        Assert.NotNull(result.OpticDistortion);
        Assert.Equal(expected, result.OpticDistortion.Value);
        var output = JObject.Parse(JsonConvert.SerializeObject(result));
        Assert.Equal(expected, output["OpticDistortion"]!["Value"]!.Value<double>());
        Assert.Null(output.Property("Optic_Distortion"));
    }
}
