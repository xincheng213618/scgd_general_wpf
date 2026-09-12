using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.GridDistortion;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.UI.Tests;

public sealed class GridDistortionTests
{
    [Fact]
    public void OptionsUseCamelCaseAndSupportSevenBySevenChart()
    {
        GridDistortionOptions options = new() { ExpectedRows = 7, ExpectedCols = 7 };
        using JsonDocument document = JsonDocument.Parse(options.ToJson());
        Assert.Equal(7, document.RootElement.GetProperty("expectedRows").GetInt32());
        Assert.Equal(7, document.RootElement.GetProperty("expectedCols").GetInt32());
        Assert.Equal(1600, document.RootElement.GetProperty("maxProcessingSize").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("ExpectedRows", out _));
        Assert.Equal(6, document.RootElement.EnumerateObject().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(17)]
    public void OptionsRejectAmbiguousCentralGridDimensions(int rows)
    {
        GridDistortionOptions options = new() { ExpectedRows = rows };
        Assert.False(options.TryValidate(out _));
        Assert.Throws<ArgumentException>(() => options.ToJson());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void OptionsRejectInvalidContrast(double contrast) =>
        Assert.False(new GridDistortionOptions { MinimumContrast = contrast }.TryValidate(out _));

    [Theory]
    [InlineData(0, 0, 0, 0, true)]
    [InlineData(10, 20, 100, 80, true)]
    [InlineData(1, 0, 0, 0, false)]
    [InlineData(0, 0, -1, 80, false)]
    [InlineData(-1, 0, 20, 20, false)]
    [InlineData(90, 0, 20, 20, true)]
    [InlineData(990, 0, 20, 20, false)]
    [InlineData(int.MaxValue, 0, int.MaxValue, 20, false)]
    public void InputRequiresAnExplicitFullImageOrAnEntirelyContainedRoi(int x, int y, int width, int height, bool valid)
    {
        HImage image = new() { cols = 1000, rows = 800, channels = 1, depth = 16, stride = 2000, pData = new IntPtr(123) };
        Assert.Equal(valid, GridDistortionNative.TryValidateInput(image, new RoiRect(x, y, width, height), out _));
    }

    [Fact]
    public void InvalidImageIsRejectedBeforeCallingNative()
    {
        GridDistortionResult result = GridDistortionNative.Run(new HImage(), new RoiRect(), new GridDistortionOptions());
        Assert.False(result.Success);
        Assert.Equal("InvalidInput", result.StatusCode);
        Assert.Null(result.Metrics);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void ParserAcceptsCompleteGridAndNineReferencesWithoutCoordinateOffsets(int dimension)
    {
        string json = ValidJson(dimension).ToJsonString();
        Assert.True(GridDistortionResultParser.TryParse(json, 1000, 800, out GridDistortionResult result, out string error), error);
        Assert.True(result.Success);
        Assert.Equal(dimension * dimension, result.Points.Count);
        Assert.Equal(9, result.ReferencePointIds.Count);
        Assert.Equal(100.25, result.Points[0].X);
        Assert.Equal(110.75, result.Points[0].Y);
        Assert.Equal(json, result.RawJson);
    }

    [Theory]
    [InlineData("missing_metrics")]
    [InlineData("zero_span")]
    [InlineData("missing_point")]
    [InlineData("duplicate_id")]
    [InlineData("duplicate_grid_position")]
    [InlineData("off_image")]
    [InlineData("missing_coordinate")]
    [InlineData("wrong_reference_order")]
    [InlineData("nonfinite_metric")]
    [InlineData("missing_success")]
    [InlineData("bad_quality")]
    [InlineData("negative_time")]
    public void ParserRejectsContradictoryOrIncompleteSuccessfulResults(string fault)
    {
        JsonObject json = ValidJson();
        JsonArray points = json["points"]!.AsArray();
        switch (fault)
        {
            case "missing_metrics": json["metrics"] = null; break;
            case "zero_span": json["metrics"]!["topWidth"] = 0; break;
            case "missing_point": points.RemoveAt(0); break;
            case "duplicate_id": points[1]!["id"] = 0; break;
            case "duplicate_grid_position": points[1]!["col"] = 0; break;
            case "off_image": points[1]!["x"] = 1000; break;
            case "missing_coordinate": points[1]!.AsObject().Remove("x"); break;
            case "wrong_reference_order": json["referencePointIds"] = JsonSerializer.SerializeToNode(new[] { 2, 1, 0, 3, 4, 5, 6, 7, 8 }); break;
            case "nonfinite_metric": json["metrics"]!["topPercent"] = "NaN"; break;
            case "missing_success": json.Remove("success"); break;
            case "bad_quality": json["quality"]!["score"] = 1.2; break;
            case "negative_time": json["timings"]!["totalMs"] = -1; break;
        }
        Assert.False(GridDistortionResultParser.TryParse(json.ToJsonString(), 1000, 800, out GridDistortionResult result, out _));
        Assert.False(result.Success);
        Assert.Null(result.Metrics);
        Assert.Empty(result.Points);
    }

    [Fact]
    public void PositiveNativeReturnWithAlgorithmRejectionDoesNotProduceZeroMetrics()
    {
        JsonObject json = ValidJson();
        json["success"] = false;
        json["statusCode"] = "missing_points";
        json["metrics"] = null;
        json["points"] = new JsonArray();
        json["referencePointIds"] = new JsonArray();
        json["selectedCount"] = 0;
        int released = 0;
        GridDistortionResult result = GridDistortionNative.Invoke(
            (out IntPtr pointer) => { pointer = new IntPtr(123); return 456; },
            _ => json.ToJsonString(), _ => { released++; return 0; }, 1000, 800);
        Assert.False(result.Success);
        Assert.Equal("missing_points", result.StatusCode);
        Assert.Equal(456, result.NativeReturnCode);
        Assert.Null(result.Metrics);
        Assert.Equal(1, released);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleaseFailureRevokesOtherwiseSuccessfulResult(bool missingEntryPoint)
    {
        int released = 0;
        GridDistortionResult result = GridDistortionNative.Invoke(
            (out IntPtr pointer) => { pointer = new IntPtr(123); return 456; },
            _ => ValidJson().ToJsonString(),
            _ => { released++; if (missingEntryPoint) throw new EntryPointNotFoundException("FreeResult"); return -1; }, 1000, 800);
        Assert.Equal(1, released);
        Assert.False(result.Success);
        Assert.Equal("NativeResultReleaseFailed", result.StatusCode);
        Assert.Null(result.Metrics);
        Assert.Empty(result.Points);
        Assert.NotEmpty(result.RawJson);
    }

    [Theory]
    [InlineData(-1, "{}")]
    [InlineData(1, "not JSON")]
    public void FailedCallOrMalformedResultStillReleasesOwnedPointer(int returnCode, string json)
    {
        int released = 0;
        GridDistortionResult result = GridDistortionNative.Invoke(
            (out IntPtr pointer) => { pointer = new IntPtr(123); return returnCode; },
            _ => json, _ => { released++; return 0; }, 1000, 800);
        Assert.Equal(1, released);
        Assert.False(result.Success);
        Assert.Null(result.Metrics);
    }

    [Fact]
    public void NativeBindingUsesUtf8AndCdecl()
    {
        MethodInfo native = typeof(GridDistortionNative).GetMethod("Detect", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(CallingConvention.Cdecl, native.GetCustomAttribute<DllImportAttribute>()!.CallingConvention);
        Assert.Equal("M_CalDistortionGridV2", native.GetCustomAttribute<DllImportAttribute>()!.EntryPoint);
        Assert.Equal(UnmanagedType.LPUTF8Str, native.GetParameters()[2].GetCustomAttribute<MarshalAsAttribute>()!.Value);
    }

    [Fact]
    public void ImageViewMenusHaveDiscoverableContextConstructors()
    {
        Assert.NotNull(typeof(CMGridDistortion).GetConstructor(new[] { typeof(ImageProcessingContext), typeof(DrawEditorContext) }));
        Assert.NotNull(typeof(DVCMGridDistortion).GetConstructor(new[] { typeof(ImageProcessingContext), typeof(DrawEditorContext), typeof(ImageViewConfig) }));
    }

    [Fact]
    public void ResultWindowUsesAttachmentKeystoneAxesAndDoesNotDisplayFailureAsZero()
    {
        Assert.True(GridDistortionResultParser.TryParse(ValidJson().ToJsonString(), 1000, 800, out GridDistortionResult result, out _));
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        IReadOnlyList<GridDistortionMetricRow> rows = GridDistortionResultWindow.BuildMetricRows(result, analysis);
        Assert.Contains("左高 − 右高", rows.Single(row => row.Method == "对边均值 9 点" && row.Name == "Keystone Horizontal").Description);
        Assert.Contains("上宽 − 下宽", rows.Single(row => row.Method == "对边均值 9 点" && row.Name == "Keystone Vertical").Description);
        Assert.Contains("上宽 − 下宽", rows.Single(row => row.Method == "旧 P9 三跨度" && row.Name == "Keystone Horizontal").Description);
        Assert.Contains("左高 − 右高", rows.Single(row => row.Method == "旧 P9 三跨度" && row.Name == "Keystone Vertical").Description);
        Assert.Equal(2, rows.Count(row => row.Method == "标准 TV"));
        Assert.Equal(2, rows.Count(row => row.Method == "半值 TV"));
        Assert.Contains(rows, row => row.Description.Contains("非已标定镜头畸变"));
        GridDistortionMetricRow failure = Assert.Single(GridDistortionResultWindow.BuildMetricRows(GridDistortionResult.CreateFailure("MissingPoints", "缺点"), null));
        Assert.Equal("无有效指标", failure.Value);
        Assert.DoesNotContain(rows, row => row.Name is "DIFF_H" or "DIFF_V");
    }

    [Fact]
    public void AnalysisExportIncludesAllConventionsAndPreservesNativeEnvelope()
    {
        Assert.True(GridDistortionResultParser.TryParse(ValidJson(7).ToJsonString(), 1000, 800, out GridDistortionResult result, out _));
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        using JsonDocument document = JsonDocument.Parse(GridDistortionResultWindow.CreateAnalysisJson(result, analysis));
        JsonElement root = document.RootElement;
        Assert.Equal("point-grid-metrics/1", root.GetProperty("formulaVersion").GetString());
        Assert.Equal(49, root.GetProperty("nativeResult").GetProperty("selectedCount").GetInt32());
        JsonElement derived = root.GetProperty("analysis");
        Assert.True(derived.TryGetProperty("standardTv", out _));
        Assert.True(derived.TryGetProperty("halfTv", out _));
        Assert.True(derived.TryGetProperty("referencePoint9", out _));
        Assert.True(derived.TryGetProperty("legacyPoint9", out _));
        Assert.False(derived.GetProperty("optical").GetProperty("isCalibrated").GetBoolean());
        Assert.Equal(48, derived.GetProperty("optical").GetProperty("samples").GetArrayLength());
        using JsonDocument nativeDocument = JsonDocument.Parse(result.RawJson);
        Assert.True(JsonElement.DeepEquals(nativeDocument.RootElement, root.GetProperty("nativeResult")));
    }

    [Fact]
    public void FailureAnalysisExportHasNoDefaultZeroAnalysisAndKeepsMalformedNativeText()
    {
        GridDistortionResult failure = GridDistortionResult.CreateFailure("ResultParseFailed", "bad JSON", rawJson: "malformed");
        using JsonDocument document = JsonDocument.Parse(GridDistortionResultWindow.CreateAnalysisJson(failure, null));
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("analysis").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("nativeResult").ValueKind);
        Assert.Equal("malformed", document.RootElement.GetProperty("rawNativeJson").GetString());
        Assert.False(document.RootElement.GetProperty("invocation").GetProperty("success").GetBoolean());
    }

    private static JsonObject ValidJson(int dimension = 3)
    {
        GridDistortionPoint[] points = Enumerable.Range(0, dimension * dimension).Select(index => new GridDistortionPoint
        {
            Id = index, Row = index / dimension, Col = index % dimension, Name = $"P{index}",
            X = 100.25 + index % dimension * 20, Y = 110.75 + index / dimension * 20, Area = 40, Contrast = 0.5
        }).ToArray();
        GridDistortionResult result = new()
        {
            Success = true, StatusCode = "ok", Message = "complete", ExpectedRows = dimension, ExpectedCols = dimension,
            CandidateCount = points.Length, SelectedCount = points.Length, Points = points,
            ReferencePointIds = Enumerable.Range(0, 9).Select(index => index / 3 * (dimension - 1) / 2 * dimension + index % 3 * (dimension - 1) / 2).ToArray(),
            Metrics = new() { TopWidth = 40, MiddleWidth = 40, BottomWidth = 40, LeftHeight = 40, CenterHeight = 40, RightHeight = 40 },
            Quality = new() { Score = 0.9, MinimumContrast = 0.5, GridResidualFraction = 0.01, ProcessingScale = 1 },
            Timings = new() { PreprocessMs = 1, CandidatesMs = 1, GridMs = 1, RefineMs = 1, MetricsMs = 1, TotalMs = 5 }
        };
        return JsonSerializer.SerializeToNode(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsObject();
    }
}
