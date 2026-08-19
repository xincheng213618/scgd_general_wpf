using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.KeyedResults;
using Xunit;

namespace ProjectARVRPro.Tests;

public class ChessboardKeyedResultTests
{
    [Fact]
    public void Write_DefaultKey_UpdatesKeyedAndCompatibilityResults()
    {
        var destination = new ObjectiveTestResult();
        var first = CreateResult(10);
        var replacement = CreateResult(20);

        KeyedTestResultWriter.Write(destination, "Chessboard", first);
        KeyedTestResultWriter.Write(destination, "chessboard", replacement);

        Assert.Single(destination.ChessboardTestResults);
        Assert.Same(replacement, destination.ChessboardTestResults["Chessboard"]);
        Assert.Same(replacement, destination.ChessboardTestResult);
    }

    [Fact]
    public void Write_CustomKey_DoesNotReplaceCompatibilityResult()
    {
        var compatibility = CreateResult(10);
        var destination = new ObjectiveTestResult { ChessboardTestResult = compatibility };
        var keyed = CreateResult(20);

        KeyedTestResultWriter.Write(destination, "ChessboardFar", keyed);

        Assert.Same(keyed, destination.ChessboardTestResults["ChessboardFar"]);
        Assert.Same(compatibility, destination.ChessboardTestResult);
    }

    [Fact]
    public void DynamicConfig_NormalizesKeyAndReadsLegacyName()
    {
        var defaultConfig = new ChessboardDynamicProcessConfig { Key = "  " };
        var legacyConfig = JsonConvert.DeserializeObject<ChessboardDynamicProcessConfig>("{\"Name\":\" ChessboardNear \"}");

        Assert.Equal("Chessboard", defaultConfig.GetOutputKey());
        Assert.Equal("ChessboardNear", legacyConfig!.GetOutputKey());
        Assert.Contains("\"Key\":\" ChessboardNear \"", JsonConvert.SerializeObject(legacyConfig));
        Assert.DoesNotContain("\"Name\":", JsonConvert.SerializeObject(legacyConfig));
    }

    [Fact]
    public void ObjectiveResultJson_ExposesTypedKeyedShape()
    {
        var destination = new ObjectiveTestResult();
        KeyedTestResultWriter.Write(destination, "ChessboardFar", CreateResult(20));

        var json = JObject.Parse(JsonConvert.SerializeObject(destination));

        Assert.Equal(20, json["ChessboardTestResults"]?["ChessboardFar"]?["ChessboardContrast"]?["Value"]?.Value<double>());
    }

    private static ChessboardTestResult CreateResult(double value) => new()
    {
        ChessboardContrast = new ObjectiveTestItem
        {
            Name = "Chessboard_Contrast",
            Value = value
        }
    };
}
