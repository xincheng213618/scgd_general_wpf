using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms.Evidence;
using OpenCvSharp;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.UI.Tests;

// The legacy provider ABI is mutable and can return partial or incomplete JSON.
// Protect the shared frame and prevent a failed/malformed measurement from becoming a valid overlay.
public sealed class DistortionEvidenceTests
{
    [Fact]
    public void LocalParametersAreCapturedWhenTheAdapterIsCreated()
    {
        GridDistortionOptions options = new() { ExpectedRows = 7, ExpectedCols = 7 };
        ImageEvidenceAlgorithm algorithm = GridDistortionEvidenceAlgorithms.CreateLocal(options);
        options.ExpectedRows = options.ExpectedCols = 3;
        using AlgorithmResult result = algorithm.Evaluate(new HImage());
        JsonElement parameters = Assert.Single(result.Artifacts.OfType<AlgorithmStructuredDataArtifact>(), artifact => artifact.Name == "parameters")
            .Data.GetProperty("Parameters");
        Assert.Equal(7, parameters.GetProperty("expectedRows").GetInt32());
        Assert.Equal(7, parameters.GetProperty("expectedCols").GetInt32());
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
    }

    [Fact]
    public void RejectedLocalGridKeepsDiagnosticsAndCannotDrawDiagnosticPoints()
    {
        GridDistortionResult native = GridDistortionResult.CreateFailure("MissingPoints", "missing") with
        {
            Points = [new() { Id = 0, X = 1, Y = 2 }], RawJson = "native rejection"
        };
        using AlgorithmResult result = GridDistortionEvidenceAlgorithms.ConvertLocal(native, new { LibraryHash = "recorded" });
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Equal("MissingPoints", Assert.Single(result.Failures).Code);
        Assert.Empty(result.Artifacts.OfType<AlgorithmGeometryArtifact>());
        Assert.Empty(result.Artifacts.OfType<AlgorithmMeasurementArtifact>());
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmStructuredDataArtifact>().Count());
    }

    [Fact]
    public void LegacyGroupsKeepTheirOwnMeasuredPointsAndDoNotInventGridConnections()
    {
        JsonObject reply = ValidReply();
        reply["Optic_Distortion"]!["finalPoints"]![0]!["x"] = 1.5;
        using AlgorithmResult result = Convert(1, reply.ToJsonString());
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(3, result.Artifacts.OfType<AlgorithmGeometryArtifact>().Count());
        Assert.Equal(9, result.Artifacts.OfType<AlgorithmMeasurementArtifact>().Single().Measurements.Count);
        AlgorithmGeometryArtifact optic = Assert.Single(result.Artifacts.OfType<AlgorithmGeometryArtifact>(), artifact => artifact.Name == "Optic");
        Assert.Equal(1.5, optic.Geometries[0].Points[0].X);
        Assert.All(optic.Geometries, geometry => Assert.Equal(AlgorithmGeometryKind.Point, geometry.Kind));
        Assert.Equal(3, result.Artifacts.OfType<AlgorithmOverlayArtifact>().Count());
        Assert.Equal("%", result.Artifacts.OfType<AlgorithmMeasurementArtifact>().Single().Measurements[0].Unit);
    }

    [Theory]
    [InlineData("missing_metric")]
    [InlineData("missing_group")]
    [InlineData("duplicate_id")]
    [InlineData("duplicate_point")]
    [InlineData("off_image")]
    [InlineData("rejected_group")]
    [InlineData("empty_points")]
    public void MalformedSuccessfulLegacyReplyIsNotReplacedByZeroMeasurements(string fault)
    {
        JsonObject reply = ValidReply();
        JsonObject tv = reply["TV_distortion"]!.AsObject();
        JsonArray points = tv["finalPoints"]!.AsArray();
        switch (fault)
        {
            case "missing_metric": tv.Remove("horizontalRatio"); break;
            case "missing_group": reply.Remove("Point9_distortion"); break;
            case "duplicate_id": points[1]!["id"] = 0; break;
            case "duplicate_point": points[1]!["x"] = 1; points[1]!["y"] = 1; break;
            case "off_image": points[1]!["x"] = 10; break;
            case "rejected_group": tv["message"] = "Failed"; break;
            case "empty_points": tv["finalPoints"] = new JsonArray(); break;
        }
        using AlgorithmResult result = Convert(1, reply.ToJsonString());
        AssertRejected(result);
        Assert.Equal("InvalidDistortionResult", Assert.Single(result.Failures).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    public void FailedOrPartiallySuccessfulLegacyCallPreservesRawEvidenceWithoutAcceptedOverlay(int code)
    {
        string raw = ValidReply().ToJsonString();
        using AlgorithmResult result = Convert(code, raw);
        AssertRejected(result);
        JsonElement diagnostics = Assert.Single(result.Artifacts.OfType<AlgorithmStructuredDataArtifact>(), artifact => artifact.Name == "diagnostics").Data;
        Assert.Equal(raw, diagnostics.GetProperty("RawJson").GetString());
        Assert.Equal(code, diagnostics.GetProperty("NativeReturnCode").GetInt32());
    }

    [Fact]
    public void ExplicitlyDisabledLegacyGroupsAreNotRequiredOrReportedAsZero()
    {
        JsonObject parameters = Parameters();
        parameters["caclDistorType"]!["Distortion9Point"] = false;
        parameters["caclDistorType"]!["DistortionOptic"] = false;
        JsonObject reply = ValidReply();
        reply.Remove("Point9_distortion");
        reply.Remove("Optic_Distortion");
        using AlgorithmResult result = ServerDistortionEvidenceAlgorithm.ConvertReply(1, reply.ToJsonString(), 10, 10, parameters, new { });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, Assert.Single(result.Artifacts.OfType<AlgorithmMeasurementArtifact>()).Measurements.Count);
        Assert.Single(result.Artifacts.OfType<AlgorithmGeometryArtifact>());
    }

    [Fact]
    public void LegacyParametersDisableFileDebugAndPreserveMeasurementSwitches()
    {
        const string input = """{"debugCfg":{"Debug":true,"debugPath":"keep"},"caclDistorType":{"TvCaclWay":1},"CommonParams":{"PercentageDisplay":false},"vendorExtension":42}""";
        JsonObject parameters = ServerDistortionEvidenceAlgorithm.PrepareParameters(input);
        Assert.False(parameters["debugCfg"]!["Debug"]!.GetValue<bool>());
        Assert.Equal("keep", parameters["debugCfg"]!["debugPath"]!.GetValue<string>());
        Assert.Equal(1, parameters["caclDistorType"]!["TvCaclWay"]!.GetValue<int>());
        Assert.False(parameters["CommonParams"]!["PercentageDisplay"]!.GetValue<bool>());
        Assert.Equal(42, parameters["vendorExtension"]!.GetValue<int>());
    }

    [Fact]
    public void LegacyInputIsPrivatePackedBgrWithoutEightBitRescaling()
    {
        using Mat allocation = new(2, 4, MatType.CV_16UC4, new Scalar(1000, 2000, 3000, 65535));
        using Mat source = allocation.SubMat(new Rect(0, 0, 3, 2));
        using Mat copy = ServerDistortionEvidenceAlgorithm.CopyInput(new HImage
        {
            rows = source.Height, cols = source.Width, channels = 4, depth = 16,
            stride = (int)source.Step(), pData = source.Data, isDispose = true
        });
        Assert.Equal(MatType.CV_16UC3, copy.Type());
        Assert.Equal(18, copy.Step());
        Assert.Equal(new Vec3w(1000, 2000, 3000), copy.At<Vec3w>(1, 2));
        copy.Set(1, 2, new Vec3w(0, 0, 0));
        Assert.Equal(new Vec4w(1000, 2000, 3000, 65535), source.At<Vec4w>(1, 2));
    }

    private static void AssertRejected(AlgorithmResult result)
    {
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Single(result.Failures);
        Assert.Empty(result.Artifacts.OfType<AlgorithmMeasurementArtifact>());
        Assert.Empty(result.Artifacts.OfType<AlgorithmGeometryArtifact>());
        Assert.Empty(result.Artifacts.OfType<AlgorithmOverlayArtifact>());
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmStructuredDataArtifact>().Count());
    }

    private static AlgorithmResult Convert(int code, string json) =>
        ServerDistortionEvidenceAlgorithm.ConvertReply(code, json, 10, 10, Parameters(), new { LibrarySha256 = "recorded" });

    private static JsonObject Parameters() => JsonNode.Parse("""{"CommonParams":{"PercentageDisplay":true},"caclDistorType":{"DistortionTV":true,"Distortion9Point":true,"DistortionOptic":true,"TvCaclWay":0}}""")!.AsObject();

    private static JsonObject ValidReply()
    {
        JsonArray points = new(Enumerable.Range(0, 9).Select(index => JsonSerializer.SerializeToNode(new
        {
            id = index, x = 1 + index % 3 * 3, y = 1 + index / 3 * 3
        })).ToArray());
        return new JsonObject
        {
            ["TV_distortion"] = new JsonObject { ["finalPoints"] = points.DeepClone(), ["horizontalRatio"] = 0, ["verticalRatio"] = 0 },
            ["Point9_distortion"] = new JsonObject
            {
                ["finalPoints"] = points.DeepClone(), ["message"] = "Success", ["topRatio"] = 0, ["bottomRatio"] = 0,
                ["leftRatio"] = 0, ["rightRatio"] = 0, ["keyStoneHoriRatio"] = 0, ["keyStoneVercRatio"] = 0
            },
            ["Optic_Distortion"] = new JsonObject { ["finalPoints"] = points.DeepClone(), ["message"] = "Success", ["opticRatio"] = 0 }
        };
    }
}
