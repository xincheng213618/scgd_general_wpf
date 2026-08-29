using ColorVision.Core;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.UI.Tests;

[Collection(LuminousAreaNativeInteropCollection.CollectionName)]
[Trait("Category", "NativeIntegration")]
public sealed class FindCrossLocalTests
{
    private const string SiteResultJson = """
        {
          "result": [
            {
              "center": { "x": 4712, "y": 3199 },
              "h": 2655,
              "name": "Point_1",
              "rotationAngle": -0.17305171489715576,
              "tilt": {
                "tilt_x": -0.60909968614578247,
                "tilt_y": -0.076140284538269043
              },
              "w": 3751,
              "x": 2888,
              "y": 1920
            }
          ]
        }
        """;

    [Fact]
    public void ProductionOptionsSerializeOnlyGeometryOpticsAndCalibration()
    {
        FindCrossLocalOptions options = CreateSiteOptions();
        options.ExpectedAngleDegrees = -0.25;
        options.AngleToleranceDegrees = 8;
        options.Name = "Point_1";
        options.CalibrationOffset = new FindCrossLocalPoint(-1.5, 2.25);

        using JsonDocument document = JsonDocument.Parse(options.ToJson());
        JsonElement root = document.RootElement;

        Assert.Equal(
            ["opticsParams", "ExpectedAngleDegrees", "AngleToleranceDegrees", "Name", "CalibrationOffset"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(4784, root.GetProperty("opticsParams").GetProperty("stdCenter").GetProperty("x").GetDouble());
        Assert.Equal(25.4, root.GetProperty("opticsParams").GetProperty("focusLength").GetDouble(), 8);
        Assert.Equal(-0.25, root.GetProperty("ExpectedAngleDegrees").GetDouble(), 8);
        Assert.Equal(8, root.GetProperty("AngleToleranceDegrees").GetDouble(), 8);
        Assert.Equal("Point_1", root.GetProperty("Name").GetString());
        Assert.Equal(-1.5, root.GetProperty("CalibrationOffset").GetProperty("x").GetDouble(), 8);
        Assert.Equal(2.25, root.GetProperty("CalibrationOffset").GetProperty("y").GetDouble(), 8);

        string[] internalOrLegacyKeys =
        [
            "DetectionMode", "PatternPolarity", "RotationMethod", "CenterMethod",
            "MinPatternContrast", "MinArmLengthPixels", "MinArmCoverage", "MinConfidence",
            "MaxProcessingSize", "MinAreaRatio", "MaxAreaRatio", "SearchWidthRatio",
            "MinEdgeContrast", "CaliperCount", "AllowBorder",
            "caclWay", "debugCfg", "CheckLine", "threshold", "blurKernel", "maxLineGap",
            "mathMaskRect", "erodeAndDiate", "minLineLength", "findEndPointWay",
            "binaryByContours", "singleErodeKernel", "binaryRateInContours"
        ];
        Assert.All(internalOrLegacyKeys, key => Assert.False(root.TryGetProperty(key, out _), key));
    }

    [Fact]
    public void DefaultOptionsUsePatternCrossSiteOpticsAndOmitAutoStandardCenter()
    {
        FindCrossLocalOptions options = new();
        using JsonDocument document = JsonDocument.Parse(options.ToJson());
        JsonElement optics = document.RootElement.GetProperty("opticsParams");

        Assert.Equal(
            ["opticsParams", "ExpectedAngleDegrees", "AngleToleranceDegrees", "Name"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(0, document.RootElement.GetProperty("ExpectedAngleDegrees").GetDouble(), 8);
        Assert.Equal(10, document.RootElement.GetProperty("AngleToleranceDegrees").GetDouble(), 8);
        Assert.Equal("Point_1", document.RootElement.GetProperty("Name").GetString());
        Assert.Null(options.Optics.StandardCenter);
        Assert.Null(options.Optics.Distortion);
        Assert.False(optics.TryGetProperty("stdCenter", out _));
        Assert.False(optics.TryGetProperty("distortion", out _));
        Assert.Equal(25.4, optics.GetProperty("focusLength").GetDouble(), 8);
        Assert.Equal(3.76, optics.GetProperty("sensorPixSize").GetDouble(), 8);
        Assert.False(document.RootElement.TryGetProperty("CalibrationOffset", out _));
    }

    [Fact]
    public void ProductionOptionsSerializeCompleteCalibratedDistortionContract()
    {
        FindCrossLocalOptions options = new()
        {
            ExpectedAngleDegrees = -12.5,
            AngleToleranceDegrees = 7.5,
            Optics = new FindCrossLocalOpticsOptions
            {
                Distortion = new FindCrossLocalDistortionOptions
                {
                    Enabled = true,
                    K1 = -0.12,
                    K2 = 0.03,
                    P1 = 0.001,
                    P2 = -0.002,
                    K3 = 0.004,
                    FxPixels = 6800.5,
                    FyPixels = 6795.25,
                    PrincipalPointX = 4781.2,
                    PrincipalPointY = 3188.8
                }
            }
        };

        using JsonDocument document = JsonDocument.Parse(options.ToJson());
        JsonElement root = document.RootElement;
        JsonElement distortion = root.GetProperty("opticsParams").GetProperty("distortion");

        Assert.Equal(-12.5, root.GetProperty("ExpectedAngleDegrees").GetDouble(), 8);
        Assert.Equal(7.5, root.GetProperty("AngleToleranceDegrees").GetDouble(), 8);
        Assert.False(root.TryGetProperty("DetectionMode", out _));
        Assert.False(root.TryGetProperty("PatternPolarity", out _));
        Assert.False(root.TryGetProperty("RotationMethod", out _));
        Assert.True(distortion.GetProperty("Enabled").GetBoolean());
        Assert.Equal(-0.12, distortion.GetProperty("K1").GetDouble(), 8);
        Assert.Equal(0.03, distortion.GetProperty("K2").GetDouble(), 8);
        Assert.Equal(0.001, distortion.GetProperty("P1").GetDouble(), 8);
        Assert.Equal(-0.002, distortion.GetProperty("P2").GetDouble(), 8);
        Assert.Equal(0.004, distortion.GetProperty("K3").GetDouble(), 8);
        Assert.Equal(6800.5, distortion.GetProperty("Fx").GetDouble(), 8);
        Assert.Equal(6795.25, distortion.GetProperty("Fy").GetDouble(), 8);
        Assert.Equal(4781.2, distortion.GetProperty("Cx").GetDouble(), 8);
        Assert.Equal(3188.8, distortion.GetProperty("Cy").GetDouble(), 8);
    }

    [Fact]
    public void ParserAcceptsTheExactSiteLegacyResult()
    {
        Assert.True(FindCrossLocalResultParser.TryParse(
            SiteResultJson,
            out FindCrossLocalResult result,
            out string error), error);

        Assert.True(result.Success);
        Assert.True(result.HasSingleItem);
        Assert.Equal("LegacyFindCross", result.Algorithm);
        FindCrossLocalItem item = Assert.Single(result.Items);
        Assert.Equal("Point_1", item.Name);
        Assert.Equal(new FindCrossLocalPoint(4712, 3199), item.Center);
        Assert.Equal(2888, item.X);
        Assert.Equal(1920, item.Y);
        Assert.Equal(3751, item.W);
        Assert.Equal(2655, item.H);
        Assert.Equal(-0.17305171489715576, item.RotationAngle, 12);
        Assert.Equal(-0.60909968614578247, item.TiltX, 12);
        Assert.Equal(-0.076140284538269043, item.TiltY, 12);
        Assert.True(item.ContainsCenter);
    }

    [Fact]
    public void ParserReadsRobustDiagnosticsCaseInsensitively()
    {
        const string json = """
            {
              "SUCCESS": true,
              "algorithm": "RobustOuterEdgesV2",
              "warnings": ["RootWarning"],
              "RESULT": [{
                "CENTER": {"X": 4712, "Y": 3199},
                "H": 2655, "NAME": "Point_1", "ROTATIONANGLE": -0.173,
                "TILT": {"TILT_X": -0.61, "TILT_Y": -0.076},
                "W": 3751, "X": 2888, "Y": 1920
              }],
              "DIAGNOSTICS": {
                "success": true,
                "algorithm": "RobustOuterEdgesV2",
                "centerMethod": "DiagonalIntersection",
                "rotationMethod": "AllEdges",
                "subpixelCenter": {"x": 4711.75, "y": 3199.2},
                "confidence": 0.91,
                "corners": [
                  {"x": 2888.2, "y": 1920.1}, {"x": 6638.7, "y": 1908.0},
                  {"x": 6639.0, "y": 4575.1}, {"x": 2889.0, "y": 4574.8}
                ],
                "sideQuality": [{
                  "name": "Top", "coverage": 0.94, "inlierRatio": 0.91,
                  "contrastP10": 18.5, "fitRms": 0.7, "maxGap": 4,
                  "confidence": 0.92, "sampleCount": 400, "inlierCount": 364
                }],
                "rotationCandidates": {"topEdge": -0.16, "allEdges": -0.173},
                "rawGeometricCenter": {"x": 4712.4, "y": 3199.1},
                "appliedOffset": {"x": -0.65, "y": 0.1},
                "effectiveRoi": {"x": 2888, "y": 1920, "w": 3751, "h": 2655},
                "ignoredParameters": ["threshold", "debugCfg.Debug"],
                "effectiveOptics": {
                  "standardCenter": {"x": 4784, "y": 3190},
                  "focusLengthMm": 25.4,
                  "sensorPixelSizeUm": 3.76,
                  "standardCenterSource": "Configuration"
                },
                "warnings": ["DiagnosticWarning"]
              }
            }
            """;

        Assert.True(FindCrossLocalResultParser.TryParse(json, out FindCrossLocalResult result, out string error), error);

        Assert.True(result.Success);
        Assert.Equal("RobustOuterEdgesV2", result.Diagnostics.Algorithm);
        Assert.Equal("DiagonalIntersection", result.Diagnostics.CenterMethod);
        Assert.Equal("AllEdges", result.Diagnostics.RotationMethod);
        Assert.Equal(new FindCrossLocalPoint(4711.75, 3199.2), result.Diagnostics.CenterSubpixel);
        Assert.Equal(0.91, result.Diagnostics.Confidence!.Value, 8);
        Assert.Equal(4, result.Diagnostics.Corners.Count);
        FindCrossLocalSideQuality side = Assert.Single(result.Diagnostics.SideQuality);
        Assert.Equal("Top", side.Name);
        Assert.Equal(0.94, side.Coverage!.Value, 8);
        Assert.Equal(400, side.SampleCount);
        Assert.Equal(-0.16, result.Diagnostics.TopEdgeAngle!.Value, 8);
        Assert.Equal(-0.173, result.Diagnostics.AllEdgesAngle!.Value, 8);
        Assert.Equal(new FindCrossLocalPoint(4712.4, 3199.1), result.Diagnostics.RawGeometricCenter);
        Assert.Equal(new FindCrossLocalPoint(-0.65, 0.1), result.Diagnostics.AppliedOffset);
        Assert.Equal(new FindCrossLocalRectangle(2888, 1920, 3751, 2655), result.Diagnostics.EffectiveRoi);
        Assert.Equal(["threshold", "debugCfg.Debug"], result.Diagnostics.IgnoredParameters);
        Assert.NotNull(result.Diagnostics.EffectiveOptics);
        Assert.Equal(new FindCrossLocalPoint(4784, 3190), result.Diagnostics.EffectiveOptics.StandardCenter);
        Assert.Equal(25.4, result.Diagnostics.EffectiveOptics.FocusLengthMillimeters, 8);
        Assert.Equal(3.76, result.Diagnostics.EffectiveOptics.SensorPixelSizeMicrometers, 8);
        Assert.Equal("Configuration", result.Diagnostics.EffectiveOptics.StandardCenterSource);
        Assert.Null(result.Diagnostics.PatternPolarity);
        Assert.Empty(result.Diagnostics.ArmEndpoints);
        Assert.Empty(result.Diagnostics.RawArmEndpoints);
        Assert.Null(result.Diagnostics.OrthogonalityError);
        Assert.Null(result.Diagnostics.PatternContrast);
        Assert.Null(result.Diagnostics.DistortionApplied);
        Assert.Equal(["RootWarning", "DiagnosticWarning"], result.Diagnostics.Warnings);
    }

    [Fact]
    public void ParserReadsPatternCrossDiagnosticsWithoutBreakingTheLegacyResultShape()
    {
        const string json = """
            {
              "Success": true,
              "Algorithm": "PatternCrossV1",
              "result": [{
                "center": {"x": 320, "y": 240},
                "h": 180,
                "name": "Point_1",
                "rotationAngle": -1.25,
                "tilt": {"tilt_x": 0.1, "tilt_y": -0.2},
                "w": 200,
                "x": 220,
                "y": 150
              }],
              "diagnostics": {
                "Success": true,
                "PatternPolarity": "Dark",
                "ArmEndpoints": [
                  {"x": 240.5, "y": 240.1}, {"x": 399.5, "y": 239.9},
                  {"x": 320.2, "y": 165.5}, {"x": 319.8, "y": 314.5}
                ],
                "RawArmEndpoints": [
                  {"x": -0.5, "y": 240}, {"x": 640.5, "y": 240},
                  {"x": 320, "y": -0.25}, {"x": 320, "y": 480.25}
                ],
                "OrthogonalityError": 0.35,
                "PatternContrast": 0.42,
                "DistortionApplied": true
              }
            }
            """;

        Assert.True(FindCrossLocalResultParser.TryParse(json, out FindCrossLocalResult result, out string error), error);

        Assert.True(result.Success);
        Assert.Equal("Dark", result.Diagnostics.PatternPolarity);
        Assert.Equal(4, result.Diagnostics.ArmEndpoints.Count);
        Assert.Equal(new FindCrossLocalPoint(240.5, 240.1), result.Diagnostics.ArmEndpoints[0]);
        Assert.Equal(4, result.Diagnostics.RawArmEndpoints.Count);
        Assert.Equal(new FindCrossLocalPoint(-0.5, 240), result.Diagnostics.RawArmEndpoints[0]);
        Assert.Equal(0.35, result.Diagnostics.OrthogonalityError!.Value, 8);
        Assert.Equal(0.42, result.Diagnostics.PatternContrast!.Value, 8);
        Assert.True(result.Diagnostics.DistortionApplied);
        Assert.Single(result.Items);
    }

    [Fact]
    public void ParserAllowsFiniteDiagnosticGeometrySlightlyOutsideTheImage()
    {
        const string json = """
            {
              "Success": true,
              "result": [{
                "center": {"x": 5, "y": 5},
                "h": 10,
                "name": "Point_1",
                "rotationAngle": 0,
                "tilt": {"tilt_x": 0, "tilt_y": 0},
                "w": 10,
                "x": 0,
                "y": 0
              }],
              "diagnostics": {
                "Success": true,
                "CenterSubpixel": {"x": -0.25, "y": 5.1},
                "RawGeometricCenter": {"x": -1.75, "y": 5.2},
                "Corners": [
                  {"x": -2, "y": -1}, {"x": 9.5, "y": -0.5},
                  {"x": 10.25, "y": 10.5}, {"x": -0.75, "y": 9.75}
                ]
              }
            }
            """;

        Assert.True(FindCrossLocalResultParser.TryParse(json, out FindCrossLocalResult result, out string error), error);
        Assert.True(result.Success);
        Assert.Equal(new FindCrossLocalPoint(-0.25, 5.1), result.Diagnostics.CenterSubpixel);
        Assert.Equal(new FindCrossLocalPoint(-1.75, 5.2), result.Diagnostics.RawGeometricCenter);
        Assert.Equal(new FindCrossLocalPoint(-2, -1), result.Diagnostics.Corners[0]);
        Assert.True(Assert.Single(result.Items).ContainsCenter);
    }

    [Fact]
    public void ParserPreservesAnAlgorithmRejectionWithNoLegacyItem()
    {
        const string json = """
            {
              "result": [],
              "diagnostics": {
                "Success": false,
                "Algorithm": "RobustOuterEdgesV2",
                "CenterSubpixel": null,
                "Confidence": 0.18,
                "RotationCandidates": null,
                "FailureReason": "InsufficientSideSupport",
                "Warnings": ["WeakTopSide"]
              }
            }
            """;

        Assert.True(FindCrossLocalResultParser.TryParse(json, out FindCrossLocalResult result, out string error), error);

        Assert.False(result.Success);
        Assert.Empty(result.Items);
        Assert.Equal("InsufficientSideSupport", result.FailureReason);
        Assert.Equal(0.18, result.Diagnostics.Confidence!.Value, 8);
        Assert.Equal("WeakTopSide", Assert.Single(result.Diagnostics.Warnings));
    }

    [Theory]
    [InlineData("{\"result\":[{\"name\":\"Point_1\",\"x\":0,\"y\":0,\"w\":10,\"h\":10,\"center\":{\"x\":5,\"y\":5},\"rotationAngle\":0}]}")]
    [InlineData("{\"result\":[{\"name\":\"Point_1\",\"x\":0,\"y\":0,\"w\":0,\"h\":10,\"center\":{\"x\":5,\"y\":5},\"rotationAngle\":0,\"tilt\":{\"tilt_x\":0,\"tilt_y\":0}}]}")]
    [InlineData("{\"result\":[{\"name\":\"Point_1\",\"x\":0,\"y\":0,\"w\":10,\"h\":10,\"center\":{\"x\":50,\"y\":5},\"rotationAngle\":0,\"tilt\":{\"tilt_x\":0,\"tilt_y\":0}}]}")]
    [InlineData("{\"result\":[{\"name\":\"Point_1\",\"x\":0,\"y\":0,\"w\":10,\"h\":10,\"center\":{\"x\":10,\"y\":5},\"rotationAngle\":0,\"tilt\":{\"tilt_x\":0,\"tilt_y\":0}}]}")]
    [InlineData("{\"result\":[{\"name\":\"Point_1\",\"x\":0,\"y\":0,\"w\":10,\"h\":10,\"center\":{\"x\":5,\"y\":10},\"rotationAngle\":0,\"tilt\":{\"tilt_x\":0,\"tilt_y\":0}}]}")]
    [InlineData("{\"result\":[{\"name\":\"Point_1\",\"x\":0,\"y\":0,\"w\":10,\"h\":10,\"center\":{\"x\":-0.25,\"y\":5},\"rotationAngle\":0,\"tilt\":{\"tilt_x\":0,\"tilt_y\":0}}]}")]
    [InlineData("{\"result\":[{\"name\":\"Point_1\",\"x\":-1,\"y\":0,\"w\":10,\"h\":10,\"center\":{\"x\":5,\"y\":5},\"rotationAngle\":0,\"tilt\":{\"tilt_x\":0,\"tilt_y\":0}}]}")]
    [InlineData("{\"result\":[],\"Success\":true}")]
    public void ParserRejectsIncompleteOrContradictoryLegacyContracts(string json)
    {
        Assert.False(FindCrossLocalResultParser.TryParse(json, out FindCrossLocalResult result, out string error));
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ManagedTiltCalculationMatchesTheSiteOpticalGeometry()
    {
        FindCrossLocalOpticsOptions optics = CreateSiteOptions().Optics;

        FindCrossLocalTilt tilt = FindCrossLocal.CalculateTilt(new FindCrossLocalPoint(4712, 3199), optics);

        Assert.Equal(-0.610650634744981, tilt.X, 12);
        Assert.Equal(-0.0763341744709155, tilt.Y, 12);
    }

    [Theory]
    [InlineData("expected-not-finite")]
    [InlineData("expected-out-of-range")]
    [InlineData("angle-zero")]
    [InlineData("angle-out-of-range")]
    [InlineData("distortion-not-finite")]
    [InlineData("distortion-intrinsics-incomplete")]
    [InlineData("name-blank")]
    [InlineData("offset-not-finite")]
    [InlineData("focus-non-positive")]
    public void ProductionOptionsRejectInvalidParametersBeforeNativeInterop(string invalidField)
    {
        FindCrossLocalOptions options = CreateSiteOptions();
        switch (invalidField)
        {
            case "expected-not-finite": options.ExpectedAngleDegrees = double.NaN; break;
            case "expected-out-of-range": options.ExpectedAngleDegrees = 181; break;
            case "angle-zero": options.AngleToleranceDegrees = 0; break;
            case "angle-out-of-range": options.AngleToleranceDegrees = 46; break;
            case "distortion-not-finite":
                options.Optics.Distortion = CreateCompleteDistortion();
                options.Optics.Distortion.K2 = double.PositiveInfinity;
                break;
            case "distortion-intrinsics-incomplete":
                options.Optics.Distortion = new FindCrossLocalDistortionOptions { Enabled = true, FxPixels = 6800 };
                break;
            case "name-blank": options.Name = " "; break;
            case "offset-not-finite": options.CalibrationOffset = new FindCrossLocalPoint(double.NaN, 0); break;
            case "focus-non-positive": options.Optics.FocusLengthMillimeters = 0; break;
        }

        FindCrossLocalResult result = FindCrossLocal.Run(default, default, options);

        Assert.False(result.Success);
        Assert.Equal("InvalidConfiguration", result.FailureReason);
        Assert.False(string.IsNullOrWhiteSpace(result.InteropDiagnostic));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{not-json")]
    public void RawJsonOverloadRejectsMalformedConfigurationBeforeLoadingTheDll(string json)
    {
        FindCrossLocalResult result = FindCrossLocal.RunJson(default, default, json);

        Assert.False(result.Success);
        Assert.Equal("InvalidConfigurationJson", result.FailureReason);
        Assert.False(string.IsNullOrWhiteSpace(result.InteropDiagnostic));
    }

    [Fact]
    public void NativeFailureStillReleasesANonNullResultPointer()
    {
        int releases = 0;
        FindCrossLocalResult result = FindCrossLocal.InvokeForTest(
            (out IntPtr pointer) =>
            {
                pointer = new IntPtr(123);
                return -4;
            },
            _ => throw new InvalidOperationException("Reader must not run for a failed native call."),
            _ =>
            {
                releases++;
                return 0;
            });

        Assert.False(result.Success);
        Assert.Equal("NativeConfigurationInvalid", result.FailureReason);
        Assert.Equal(-4, result.NativeReturnCode);
        Assert.Equal(1, releases);
    }

    [Fact]
    public void NativeConfigurationFailureIncludesTheFieldLevelLastError()
    {
        FindCrossLocalResult result = FindCrossLocal.InvokeForTest(
            (out IntPtr pointer) =>
            {
                pointer = IntPtr.Zero;
                return -4;
            },
            _ => throw new InvalidOperationException("Reader must not run for a failed native call."),
            _ => 0,
            () => "opticsParams.focusLength must be finite and greater than zero");

        Assert.False(result.Success);
        Assert.Equal("NativeConfigurationInvalid", result.FailureReason);
        Assert.Contains("opticsParams.focusLength", result.InteropDiagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseFailureStillReleasesTheNativeResultPointer()
    {
        int releases = 0;
        FindCrossLocalResult result = FindCrossLocal.InvokeForTest(
            (out IntPtr pointer) =>
            {
                pointer = new IntPtr(456);
                return 12;
            },
            _ => "{bad-json",
            _ =>
            {
                releases++;
                return 0;
            });

        Assert.False(result.Success);
        Assert.Equal("ResultParseFailed", result.FailureReason);
        Assert.Equal(12, result.NativeReturnCode);
        Assert.Equal(1, releases);
    }

    [Fact]
    public void ReaderExceptionStillReleasesTheNativeResultPointer()
    {
        int releases = 0;
        FindCrossLocalResult result = FindCrossLocal.InvokeForTest(
            (out IntPtr pointer) =>
            {
                pointer = new IntPtr(789);
                return 12;
            },
            _ => throw new InvalidOperationException("UTF-8 decode failed."),
            _ =>
            {
                releases++;
                return 0;
            });

        Assert.False(result.Success);
        Assert.Equal("ManagedInteropFailed", result.FailureReason);
        Assert.Equal(1, releases);
    }

    [Fact]
    public void FreeResultFailureClearsItemsAndMakesDiagnosticsConsistentlyFailed()
    {
        FindCrossLocalResult result = FindCrossLocal.InvokeForTest(
            (out IntPtr pointer) =>
            {
                pointer = new IntPtr(901);
                return 12;
            },
            _ => SiteResultJson,
            _ => -1);

        Assert.False(result.Success);
        Assert.False(result.Diagnostics.Success);
        Assert.False(result.HasSingleItem);
        Assert.Empty(result.Items);
        Assert.Equal("NativeResultReleaseFailed", result.FailureReason);
        Assert.Equal(result.FailureReason, result.Diagnostics.FailureReason);
        Assert.Contains("-1", result.InteropDiagnostic, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("dll", "NativeLibraryUnavailable")]
    [InlineData("entry", "NativeEntryPointUnavailable")]
    [InlineData("image", "NativeLibraryIncompatible")]
    [InlineData("abi", "NativeAbiMismatch")]
    public void NativeLoadAndAbiExceptionsBecomeStructuredFailures(string kind, string expectedReason)
    {
        FindCrossLocalResult result = FindCrossLocal.InvokeForTest(
            (out IntPtr pointer) =>
            {
                pointer = IntPtr.Zero;
                throw kind switch
                {
                    "dll" => new DllNotFoundException("missing"),
                    "entry" => new EntryPointNotFoundException("missing export"),
                    "image" => new BadImageFormatException("wrong architecture"),
                    _ => new MarshalDirectiveException("bad ABI")
                };
            },
            _ => string.Empty,
            _ => 0);

        Assert.False(result.Success);
        Assert.Equal(expectedReason, result.FailureReason);
        Assert.False(string.IsNullOrWhiteSpace(result.InteropDiagnostic));
    }

    [Fact]
    public void BindingUsesCdeclUtf8AndTheExpectedSignature()
    {
        MethodInfo method = typeof(OpenCVMediaHelper).GetMethod(
            nameof(OpenCVMediaHelper.M_FindCrossLocal),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(OpenCVMediaHelper), nameof(OpenCVMediaHelper.M_FindCrossLocal));
        DllImportAttribute import = method.GetCustomAttribute<DllImportAttribute>()
            ?? throw new InvalidOperationException("M_FindCrossLocal must remain a DllImport binding.");
        ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal(CallingConvention.Cdecl, import.CallingConvention);
        Assert.True(import.ExactSpelling);
        Assert.EndsWith("opencv_helper.dll", import.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(typeof(HImage), parameters[0].ParameterType);
        Assert.Equal(typeof(RoiRect), parameters[1].ParameterType);
        Assert.Equal(UnmanagedType.LPUTF8Str, parameters[2].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.Equal(typeof(IntPtr).MakeByRefType(), parameters[3].ParameterType);
        Assert.True(parameters[3].IsOut);

        MethodInfo errorMethod = typeof(OpenCVMediaHelper).GetMethod(
            nameof(OpenCVMediaHelper.M_FindCrossLocalGetLastError),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(OpenCVMediaHelper), nameof(OpenCVMediaHelper.M_FindCrossLocalGetLastError));
        DllImportAttribute errorImport = errorMethod.GetCustomAttribute<DllImportAttribute>()
            ?? throw new InvalidOperationException("M_FindCrossLocalGetLastError must remain a DllImport binding.");
        Assert.Equal(CallingConvention.Cdecl, errorImport.CallingConvention);
        Assert.True(errorImport.ExactSpelling);
    }

    [NativeV2Fact]
    public void RawJsonCanStillRunTheLegacyOuterPanelDiagnosticMode()
    {
        using FindCrossFixture fixture = FindCrossFixture.Create();
        FindCrossLocalResult result = FindCrossLocal.RunJson(
            fixture.Image,
            fixture.Region,
            """{"DetectionMode":"OuterPanel","MinConfidence":0.2}""");

        Assert.True(result.Success,
            $"Failure={result.FailureReason}; Interop={result.InteropDiagnostic}; JSON={result.RawJson}");
        FindCrossLocalItem item = Assert.Single(result.Items);
        Assert.InRange(item.Center.X, fixture.ExpectedCenter.X - 14, fixture.ExpectedCenter.X + 14);
        Assert.InRange(item.Center.Y, fixture.ExpectedCenter.Y - 14, fixture.ExpectedCenter.Y + 14);
        Assert.True(item.ContainsCenter);
        Assert.True(result.Diagnostics.Confidence >= 0.2);
        Assert.Equal(new FindCrossLocalRectangle(
            fixture.Region.X, fixture.Region.Y, fixture.Region.Width, fixture.Region.Height),
            result.Diagnostics.EffectiveRoi);
        Assert.Empty(result.Diagnostics.IgnoredParameters);
        Assert.NotNull(result.Diagnostics.EffectiveOptics);
        Assert.Equal("ImageCenterDefault", result.Diagnostics.EffectiveOptics.StandardCenterSource);
        Assert.Equal(new FindCrossLocalPoint(560, 410), result.Diagnostics.EffectiveOptics.StandardCenter);
        Assert.Equal(25.4, result.Diagnostics.EffectiveOptics.FocusLengthMillimeters, 8);
        Assert.Equal(3.76, result.Diagnostics.EffectiveOptics.SensorPixelSizeMicrometers, 8);
    }

    [NativeV2Fact]
    public void RealFindCrossExportReturnsFieldLevelConfigurationError()
    {
        using FindCrossFixture fixture = FindCrossFixture.Create();
        FindCrossLocalResult result = FindCrossLocal.RunJson(
            fixture.Image,
            fixture.Region,
            """{"DetectionMode":"PatternCross","opticsParams":{"focusLength":0,"sensorPixSize":3.76}}""");

        Assert.False(result.Success);
        Assert.Equal("NativeConfigurationInvalid", result.FailureReason);
        Assert.Equal(-4, result.NativeReturnCode);
        Assert.Contains("opticsParams.focusLength", result.InteropDiagnostic, StringComparison.Ordinal);
        Assert.Contains("greater than zero", result.InteropDiagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FindCrossFixture : IDisposable
    {
        private const int OffsetX = 87;
        private const int OffsetY = 61;
        private const int LocalWidth = 920;
        private const int LocalHeight = 680;
        private readonly GCHandle handle;

        private FindCrossFixture(ushort[] pixels)
        {
            handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Image = new HImage
            {
                rows = 820,
                cols = 1120,
                channels = 1,
                depth = 16,
                stride = 1120 * sizeof(ushort),
                isDispose = true,
                pData = handle.AddrOfPinnedObject()
            };
        }

        public HImage Image { get; }

        public RoiRect Region { get; } = new(OffsetX, OffsetY, LocalWidth, LocalHeight);

        public FindCrossLocalPoint ExpectedCenter { get; } = AddOffset(IntersectDiagonals(
            new FindCrossLocalPoint(153, 126), new FindCrossLocalPoint(746, 96),
            new FindCrossLocalPoint(779, 526), new FindCrossLocalPoint(119, 558)));

        public static FindCrossFixture Create()
        {
            FindCrossLocalPoint[] corners =
            [
                new(153, 126), new(746, 96), new(779, 526), new(119, 558)
            ];
            ushort[] pixels = new ushort[1120 * 820];
            Array.Fill(pixels, (ushort)700);
            for (int y = 0; y < LocalHeight; y++)
            {
                for (int x = 0; x < LocalWidth; x++)
                {
                    double value = 900 + 0.18 * x + 0.11 * y;
                    if (IsInsideConvexPolygon(x + 0.5, y + 0.5, corners))
                    {
                        value = 39000 + 4500 * Math.Cos((y - LocalHeight * 0.5) / LocalHeight * Math.PI);
                    }
                    uint noise = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
                    value += (int)(noise & 0xff) - 128;
                    pixels[(y + OffsetY) * 1120 + x + OffsetX] =
                        (ushort)Math.Clamp((int)Math.Round(value), ushort.MinValue, ushort.MaxValue);
                }
            }
            return new FindCrossFixture(pixels);
        }

        public void Dispose()
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }

        private static bool IsInsideConvexPolygon(double x, double y, IReadOnlyList<FindCrossLocalPoint> corners)
        {
            for (int index = 0; index < corners.Count; index++)
            {
                FindCrossLocalPoint start = corners[index];
                FindCrossLocalPoint end = corners[(index + 1) % corners.Count];
                if ((end.X - start.X) * (y - start.Y) - (end.Y - start.Y) * (x - start.X) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static FindCrossLocalPoint IntersectDiagonals(
            FindCrossLocalPoint topLeft,
            FindCrossLocalPoint topRight,
            FindCrossLocalPoint bottomRight,
            FindCrossLocalPoint bottomLeft)
        {
            double firstX = bottomRight.X - topLeft.X;
            double firstY = bottomRight.Y - topLeft.Y;
            double secondX = bottomLeft.X - topRight.X;
            double secondY = bottomLeft.Y - topRight.Y;
            double denominator = firstX * secondY - firstY * secondX;
            double deltaX = topRight.X - topLeft.X;
            double deltaY = topRight.Y - topLeft.Y;
            double parameter = (deltaX * secondY - deltaY * secondX) / denominator;
            return new FindCrossLocalPoint(topLeft.X + parameter * firstX, topLeft.Y + parameter * firstY);
        }

        private static FindCrossLocalPoint AddOffset(FindCrossLocalPoint point) =>
            new(point.X + OffsetX, point.Y + OffsetY);
    }

    private static FindCrossLocalOptions CreateSiteOptions() => new()
    {
        Optics = new FindCrossLocalOpticsOptions
        {
            StandardCenter = new FindCrossLocalPoint(4784, 3190),
            FocusLengthMillimeters = 25.4,
            SensorPixelSizeMicrometers = 3.76
        },
        Name = "Point_1"
    };

    private static FindCrossLocalDistortionOptions CreateCompleteDistortion() => new()
    {
        Enabled = true,
        K1 = -0.12,
        K2 = 0.03,
        P1 = 0.001,
        P2 = -0.002,
        K3 = 0.004,
        FxPixels = 6800.5,
        FyPixels = 6795.25,
        PrincipalPointX = 4781.2,
        PrincipalPointY = 3188.8
    };
}
