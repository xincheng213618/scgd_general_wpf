using ColorVision.Core;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class LuminousAreaDetectionUiTests
{
    [Fact]
    public void ExistingConfigurationWithoutAlgorithmUsesRobustV2()
    {
        FindLuminousAreaCorner config = JsonConvert.DeserializeObject<FindLuminousAreaCorner>(
            "{\"Threshold\":37,\"UseRotatedRect\":false}")!;

        Assert.Equal(LuminousAreaDetectionMode.RobustV2, config.Algorithm);
        Assert.Equal(0.25, config.MinConfidence, 8);
        Assert.Equal(37, config.Threshold);
        Assert.False(config.UseRotatedRect);
    }

    [Fact]
    public void RectangleConfigurationSharesTheSameRobustDefault()
    {
        FindLuminousArea config = new();

        Assert.IsAssignableFrom<FindLuminousAreaCorner>(config);
        Assert.Equal(LuminousAreaDetectionMode.RobustV2, config.Algorithm);
        Assert.False(config.UseRotatedRect);
    }

    [Fact]
    public void LegacyRectangleConfigurationWithoutRotatedFlagKeepsAxisAlignedSemantics()
    {
        FindLuminousArea config = JsonConvert.DeserializeObject<FindLuminousArea>(
            "{\"Algorithm\":1,\"Threshold\":37}")!;

        Assert.Equal(LuminousAreaDetectionMode.Legacy, config.Algorithm);
        Assert.Equal(37, config.Threshold);
        Assert.False(config.UseRotatedRect);
    }

    [Fact]
    public void ImageEditorAndPoiConfigurationsRoundTripBothDetectionModes()
    {
        GraphicEditingConfig editor = new();
        editor.FindLuminousArea.Algorithm = LuminousAreaDetectionMode.Legacy;
        editor.FindLuminousArea.Threshold = 31;
        editor.FindLuminousArea.UseRotatedRect = false;
        editor.FindLuminousAreaCorner.Algorithm = LuminousAreaDetectionMode.RobustV2;
        editor.FindLuminousAreaCorner.MinConfidence = 0.63;

        PoiConfig poi = new();
        poi.FindLuminousArea.Algorithm = LuminousAreaDetectionMode.RobustV2;
        poi.FindLuminousArea.MinConfidence = 0.71;
        poi.FindLuminousAreaCorner.Algorithm = LuminousAreaDetectionMode.Legacy;
        poi.FindLuminousAreaCorner.Threshold = 42;
        poi.FindLuminousAreaCorner.UseRotatedRect = true;

        GraphicEditingConfig restoredEditor = JsonConvert.DeserializeObject<GraphicEditingConfig>(
            JsonConvert.SerializeObject(editor))!;
        PoiConfig restoredPoi = JsonConvert.DeserializeObject<PoiConfig>(
            JsonConvert.SerializeObject(poi))!;

        Assert.Equal(LuminousAreaDetectionMode.Legacy, restoredEditor.FindLuminousArea.Algorithm);
        Assert.Equal(31, restoredEditor.FindLuminousArea.Threshold);
        Assert.False(restoredEditor.FindLuminousArea.UseRotatedRect);
        Assert.Equal(LuminousAreaDetectionMode.RobustV2, restoredEditor.FindLuminousAreaCorner.Algorithm);
        Assert.Equal(0.63, restoredEditor.FindLuminousAreaCorner.MinConfidence, 8);
        Assert.Equal(LuminousAreaDetectionMode.RobustV2, restoredPoi.FindLuminousArea.Algorithm);
        Assert.Equal(0.71, restoredPoi.FindLuminousArea.MinConfidence, 8);
        Assert.Equal(LuminousAreaDetectionMode.Legacy, restoredPoi.FindLuminousAreaCorner.Algorithm);
        Assert.Equal(42, restoredPoi.FindLuminousAreaCorner.Threshold);
        Assert.True(restoredPoi.FindLuminousAreaCorner.UseRotatedRect);
    }

    [Fact]
    public void LegacyNestedConfigurationsKeepParametersAndAdoptRobustDefault()
    {
        const string json = """
            {
              "FindLuminousArea": { "Threshold": 27, "UseRotatedRect": false },
              "FindLuminousAreaCorner": { "Threshold": 39, "UseRotatedRect": true }
            }
            """;

        GraphicEditingConfig editor = JsonConvert.DeserializeObject<GraphicEditingConfig>(json)!;
        PoiConfig poi = JsonConvert.DeserializeObject<PoiConfig>(json)!;

        foreach (FindLuminousAreaCorner config in new FindLuminousAreaCorner[]
                 { editor.FindLuminousArea, editor.FindLuminousAreaCorner, poi.FindLuminousArea, poi.FindLuminousAreaCorner })
        {
            Assert.Equal(LuminousAreaDetectionMode.RobustV2, config.Algorithm);
            Assert.Equal(0.25, config.MinConfidence, 8);
        }
        Assert.Equal(27, editor.FindLuminousArea.Threshold);
        Assert.False(editor.FindLuminousArea.UseRotatedRect);
        Assert.Equal(39, editor.FindLuminousAreaCorner.Threshold);
        Assert.True(editor.FindLuminousAreaCorner.UseRotatedRect);
        Assert.Equal(27, poi.FindLuminousArea.Threshold);
        Assert.False(poi.FindLuminousArea.UseRotatedRect);
        Assert.Equal(39, poi.FindLuminousAreaCorner.Threshold);
        Assert.True(poi.FindLuminousAreaCorner.UseRotatedRect);
    }

    [Fact]
    public void UnsupportedConfiguredModeFailsConsistentlyForImageEditorAndPoi()
    {
        const string json = """
            {
              "FindLuminousArea": { "Algorithm": 99 },
              "FindLuminousAreaCorner": { "Algorithm": 99 }
            }
            """;
        GraphicEditingConfig editor = JsonConvert.DeserializeObject<GraphicEditingConfig>(json)!;
        PoiConfig poi = JsonConvert.DeserializeObject<PoiConfig>(json)!;

        LuminousAreaDetectionResult[] results =
        [
            LuminousAreaDetector.Detect(default, default, editor.FindLuminousArea),
            LuminousAreaDetector.Detect(default, default, editor.FindLuminousAreaCorner),
            LuminousAreaDetector.Detect(default, default, poi.FindLuminousArea),
            LuminousAreaDetector.Detect(default, default, poi.FindLuminousAreaCorner)
        ];

        Assert.All(results, result =>
        {
            Assert.False(result.Success);
            Assert.False(result.HasValidCorners);
            Assert.Equal("UnsupportedAlgorithm", result.FailureReason);
            Assert.Equal("不支持所选的发光区定位算法。", LuminousAreaDetector.GetFailureMessage(result));
            Assert.Throws<ArgumentException>(() => LuminousAreaDetector.GetBoundingRect(result));
        });
    }

    [Fact]
    public void LegacyParametersAreOnlyVisibleForLegacyMode()
    {
        PropertyVisibilityAttribute thresholdVisibility = GetVisibility(nameof(FindLuminousAreaCorner.Threshold));
        PropertyVisibilityAttribute rotatedRectVisibility = GetVisibility(nameof(FindLuminousAreaCorner.UseRotatedRect));
        PropertyVisibilityAttribute confidenceVisibility = GetVisibility(nameof(FindLuminousAreaCorner.MinConfidence));

        Assert.Equal(nameof(FindLuminousAreaCorner.Algorithm), thresholdVisibility.PropertyName);
        Assert.Equal(LuminousAreaDetectionMode.Legacy, thresholdVisibility.ExpectedValue);
        Assert.Equal(LuminousAreaDetectionMode.Legacy, rotatedRectVisibility.ExpectedValue);
        Assert.Equal(LuminousAreaDetectionMode.RobustV2, confidenceVisibility.ExpectedValue);
    }

    [Fact]
    public void AlgorithmAndConditionalParametersShareOneCategory()
    {
        string[] propertyNames =
        [
            nameof(FindLuminousAreaCorner.Algorithm),
            nameof(FindLuminousAreaCorner.MinConfidence),
            nameof(FindLuminousAreaCorner.Threshold),
            nameof(FindLuminousAreaCorner.UseRotatedRect)
        ];

        Assert.All(propertyNames, propertyName =>
        {
            CategoryAttribute category = typeof(FindLuminousAreaCorner)
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
                .GetCustomAttribute<CategoryAttribute>()!;
            Assert.Equal("定位", category.Category);
        });
    }

    [Fact]
    public void NativeV2ResultParsesSideMetricsAndKeepsCornerContract()
    {
        const string json = """
            {
              "Success": true,
              "Algorithm": "RobustV2",
              "Corners": [
                { "X": 10.2, "Y": 20.8 },
                { "X": 110.1, "Y": 21.2 },
                { "X": 108.6, "Y": 71.4 },
                { "X": 9.7, "Y": 70.9 }
              ],
              "Confidence": 0.87,
              "SideQuality": [
                { "Name": "Top", "Coverage": 0.91, "Confidence": 0.85 },
                { "Name": "Right", "Coverage": 0.89, "Confidence": 0.82 },
                { "Name": "Bottom", "Coverage": 0.88, "Confidence": 0.81 },
                { "Name": "Left", "Coverage": 0.90, "Confidence": 0.84 }
              ],
              "FailureReason": "",
              "Warnings": ["DarkCorner"]
            }
            """;

        Assert.True(LuminousAreaResultParser.TryParseV2(json, out LuminousAreaDetectionResult result, out string error), error);
        Assert.True(result.HasValidCorners);
        Assert.Equal(new LuminousAreaPoint(10.2, 20.8), result.Corners[0]);
        Assert.Equal(new LuminousAreaPoint(110.1, 21.2), result.Corners[1]);
        Assert.Equal(new LuminousAreaPoint(108.6, 71.4), result.Corners[2]);
        Assert.Equal(new LuminousAreaPoint(9.7, 70.9), result.Corners[3]);
        Assert.Equal(["Top", "Right", "Bottom", "Left"], result.SideQuality.Select(side => side.Side));
        Assert.Equal(0.91, result.SideQuality[0].Metrics["Coverage"], 8);
        Assert.Equal(0.85, result.SideQuality[0].Score!.Value, 8);
        Assert.Equal("DarkCorner", Assert.Single(result.Warnings));
    }

    [Fact]
    public void RectangleConsumerUsesFloorAndCeilingAroundAllCorners()
    {
        const string json = """
            {
              "Success": true,
              "Algorithm": "RobustV2",
              "Corners": [
                { "X": 10.2, "Y": 20.8 },
                { "X": 110.1, "Y": 21.2 },
                { "X": 108.6, "Y": 71.4 },
                { "X": 9.7, "Y": 70.9 }
              ],
              "Confidence": 0.87,
              "FailureReason": "",
              "Warnings": []
            }
            """;
        Assert.True(LuminousAreaResultParser.TryParseV2(json, out LuminousAreaDetectionResult result, out string error), error);

        MRect rect = LuminousAreaDetector.GetBoundingRect(result);

        Assert.Equal(9, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(102, rect.Width);
        Assert.Equal(52, rect.Height);
    }

    [Theory]
    [InlineData(96, 1)]
    [InlineData(192, 2)]
    [InlineData(48, 0.5)]
    [InlineData(0, 1)]
    [InlineData(double.NaN, 1)]
    public void ImageEditorDpiConversionRoundTripsPixelAndCanvasCoordinates(double dpi, double dipToPixel)
    {
        Assert.Equal(dipToPixel, LuminousAreaDetector.GetDipToPixelScale(dpi), 8);
        Assert.Equal(1 / dipToPixel, LuminousAreaDetector.GetPixelToDipScale(dpi), 8);
    }

    [Fact]
    public void AllWpfConsumersCanConvertNativePixelsToHighDpiCanvasCoordinates()
    {
        LuminousAreaDetectionResult result = new(
            true,
            "RobustV2",
            [
                new LuminousAreaPoint(20, 40),
                new LuminousAreaPoint(220, 40),
                new LuminousAreaPoint(220, 140),
                new LuminousAreaPoint(20, 140)
            ],
            0.9,
            null,
            string.Empty,
            null);

        LuminousAreaPoint point = LuminousAreaDetector.ConvertPixelToDip(result.Corners[2], 192, 192);
        MRect rect = LuminousAreaDetector.GetDipBoundingRect(result, 192, 192);

        Assert.Equal(new LuminousAreaPoint(110, 70), point);
        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(100, rect.Width);
        Assert.Equal(50, rect.Height);
    }

    [Theory]
    [InlineData("[{\"X\":100,\"Y\":0},{\"X\":100,\"Y\":50},{\"X\":0,\"Y\":50},{\"X\":0,\"Y\":0}]")]
    [InlineData("[{\"X\":0,\"Y\":0},{\"X\":100,\"Y\":50},{\"X\":100,\"Y\":0},{\"X\":0,\"Y\":50}]")]
    [InlineData("[{\"X\":0,\"Y\":0},{\"X\":50,\"Y\":0},{\"X\":100,\"Y\":0},{\"X\":150,\"Y\":0}]")]
    public void V2ParserRejectsShiftedSelfCrossingAndDegenerateCornerContracts(string corners)
    {
        string json = $"{{\"Success\":true,\"Algorithm\":\"RobustV2\",\"Corners\":{corners},\"Confidence\":0.9,\"FailureReason\":\"\",\"Warnings\":[]}}";

        Assert.False(LuminousAreaResultParser.TryParseV2(json, out LuminousAreaDetectionResult result, out string error));
        Assert.False(result.HasValidCorners);
        Assert.Contains("四角点", error);
    }

    [Fact]
    public void SuccessfulHighRecallWarningsAreExplainedWithoutRejectingCorners()
    {
        LuminousAreaDetectionResult result = new(
            true,
            "RobustV2",
            [
                new LuminousAreaPoint(0, 0),
                new LuminousAreaPoint(100, 0),
                new LuminousAreaPoint(100, 50),
                new LuminousAreaPoint(0, 50)
            ],
            0.777,
            null,
            string.Empty,
            ["PartialBottomSupport", "WeakTopSide"]);

        string message = LuminousAreaDetector.GetWarningMessage(result);

        Assert.True(result.HasValidCorners);
        Assert.Contains("定位已成功", message);
        Assert.Contains("部分有效支持", message);
        Assert.Contains("定位证据偏弱", message);
        Assert.Contains("0.777", message);
    }

    [Fact]
    public void RejectedDiagnosticCornersStillUseFullImageCoordinates()
    {
        LuminousAreaDetectionResult rejected = new(
            false,
            "RobustV2",
            [
                new LuminousAreaPoint(1, 2),
                new LuminousAreaPoint(11, 2),
                new LuminousAreaPoint(11, 8),
                new LuminousAreaPoint(1, 8)
            ],
            0.3,
            null,
            "LowConfidence",
            null);

        LuminousAreaDetectionResult offset = rejected.Offset(100, 200);

        Assert.False(offset.Success);
        Assert.False(offset.HasValidCorners);
        Assert.Equal(new LuminousAreaPoint(101, 202), offset.Corners[0]);
        Assert.Equal(new LuminousAreaPoint(111, 208), offset.Corners[2]);
    }

    [Theory]
    [InlineData("{\"Success\":true,\"Corners\":[{\"X\":0,\"Y\":0},{\"X\":10,\"Y\":0},{\"X\":10,\"Y\":5},{\"X\":0,\"Y\":5}]}")]
    [InlineData("{\"Success\":true,\"Algorithm\":\"Legacy\",\"Corners\":[{\"X\":0,\"Y\":0},{\"X\":10,\"Y\":0},{\"X\":10,\"Y\":5},{\"X\":0,\"Y\":5}]}")]
    public void V2ParserRequiresRobustAlgorithmIdentity(string json)
    {
        Assert.False(LuminousAreaResultParser.TryParseV2(json, out _, out string error));
        Assert.Contains("Algorithm=RobustV2", error);
    }

    [Theory]
    [InlineData("{\"Success\":true,\"Algorithm\":\"RobustV2\",\"Corners\":[{\"X\":0,\"Y\":0},{\"X\":10,\"Y\":0},{\"X\":10,\"Y\":5},{\"X\":0,\"Y\":5}]}")]
    [InlineData("{\"Success\":true,\"Algorithm\":\"RobustV2\",\"Confidence\":1.2,\"Corners\":[{\"X\":0,\"Y\":0},{\"X\":10,\"Y\":0},{\"X\":10,\"Y\":5},{\"X\":0,\"Y\":5}]}")]
    public void V2ParserRequiresBoundedConfidence(string json)
    {
        Assert.False(LuminousAreaResultParser.TryParseV2(json, out _, out string error));
        Assert.Contains("Confidence", error);
    }

    [Fact]
    public void ManagedConfidenceGateRejectsUnexpectedNativeSuccess()
    {
        LuminousAreaDetectionResult nativeSuccess = new(
            true,
            "RobustV2",
            [
                new LuminousAreaPoint(0, 0),
                new LuminousAreaPoint(10, 0),
                new LuminousAreaPoint(10, 5),
                new LuminousAreaPoint(0, 5)
            ],
            0.24,
            null,
            string.Empty,
            null);

        LuminousAreaDetectionResult gated = LuminousAreaNative.EnforceMinimumConfidence(nativeSuccess, 0.25);

        Assert.False(gated.Success);
        Assert.Equal("LowConfidence", gated.FailureReason);
        Assert.Equal(nativeSuccess.Corners, gated.Corners);
    }

    [Theory]
    [InlineData("UnsupportedImage", "图像格式")]
    [InlineData("NoCandidate", "候选")]
    [InlineData("InvalidGeometry", "几何关系")]
    [InlineData("InsufficientSideSupport", "边的有效证据")]
    [InlineData("InsufficientIndependentGeometry", "唯一确定")]
    public void NativeRejectionReasonsHaveUserFacingMessages(string reason, string expectedText)
    {
        LuminousAreaDetectionResult result = LuminousAreaDetectionResult.CreateFailure("RobustV2", reason);

        Assert.Contains(expectedText, LuminousAreaDetector.GetFailureMessage(result));
    }

    private static PropertyVisibilityAttribute GetVisibility(string propertyName) =>
        typeof(FindLuminousAreaCorner)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetCustomAttribute<PropertyVisibilityAttribute>()!;
}
