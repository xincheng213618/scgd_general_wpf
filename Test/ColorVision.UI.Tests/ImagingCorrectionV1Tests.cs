using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImagingCorrection;
using OpenCvSharp;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImagingCorrectionV1Tests
{
    public static TheoryData<AlgorithmImageFormat> CanonicalFormats => new()
    {
        AlgorithmImageFormat.Gray8, AlgorithmImageFormat.Gray16, AlgorithmImageFormat.Gray32Float,
        AlgorithmImageFormat.Bgr24, AlgorithmImageFormat.Bgr48, AlgorithmImageFormat.Bgr96Float,
        AlgorithmImageFormat.Bgra32, AlgorithmImageFormat.Bgra64, AlgorithmImageFormat.Bgra128Float,
    };

    [Fact]
    public void CatalogPresetDefaultsAliasesAndHostPolicyAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, value => value.Id == StandardAlgorithmIds.ImagingCorrection);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.Equal(1, descriptor.MinimumInputCount);
        Assert.Equal(5, descriptor.MaximumInputCount);
        Assert.Equal("primary=same-as-source; validity-mask=gray8", descriptor.OutputFormatPolicy);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Interactive));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.MultiInput));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(catalog.TryResolveAlias("FlatFieldCorrection", out AlgorithmDescriptor? alias));
        Assert.Equal(descriptor.Id, alias!.Id);
        ImagingCorrectionParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<ImagingCorrectionParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        Assert.Contains(descriptor.ParameterSchema.Fields, value => value.Name == nameof(ImagingCorrectionParameters.BadPixelRadius) && value.Minimum == 1 && value.Maximum == 7);
        Assert.Contains(BatchImageAlgorithms.CreateAll(catalog), value => value.Descriptor?.Id == descriptor.Id);

        ImagingCorrectionParameters parameters = new()
        {
            EnableDarkFrame = true,
            DarkFramePath = "references/dark.tif",
            CalibrationSource = "camera-17",
            CalibrationVersion = "2026-08-28",
            CalibrationChecksum = "sha256:fixture",
        };
        string json = ImagingCorrectionPresetSerializer.Serialize("camera-17-v3", parameters);
        (string presetId, ImagingCorrectionParameters restored) = ImagingCorrectionPresetSerializer.Deserialize(json);
        Assert.Equal("camera-17-v3", presetId);
        Assert.True(restored.EnableDarkFrame);
        Assert.Equal(parameters.DarkFramePath, restored.DarkFramePath);
        Assert.Equal(parameters.CalibrationChecksum, restored.CalibrationChecksum);
    }

    [Fact]
    public void ValidationRejectsThresholdGainEnumRadiusAndProvenanceDrift()
    {
        ImagingCorrectionParameters parameters = new()
        {
            ReferenceZeroThresholdNormalized = 0.8,
            ReferenceSaturationThresholdNormalized = 0.7,
            MinimumValidReferenceFraction = 1.1,
            MinimumGain = 5,
            MaximumGain = 4,
            BadPixelRadius = 8,
            InvalidReferencePolicy = (InvalidReferencePixelPolicy)999,
            OutputRangePolicy = (ImagingCorrectionOutputRangePolicy)999,
            CalibrationSource = string.Empty,
            CalibrationVersion = string.Empty,
            CalibrationChecksum = null!,
        };
        AlgorithmValidationResult validation = parameters.Validate();
        Assert.Contains(validation.Issues, value => value.Code == "reference_threshold_order");
        Assert.Contains(validation.Issues, value => value.Code == "gain_order");
        Assert.Contains(validation.Issues, value => value.Path == nameof(ImagingCorrectionParameters.BadPixelRadius));
        Assert.Contains(validation.Issues, value => value.Path == nameof(ImagingCorrectionParameters.InvalidReferencePolicy));
        Assert.Contains(validation.Issues, value => value.Path == nameof(ImagingCorrectionParameters.OutputRangePolicy));
        Assert.Contains(validation.Issues, value => value.Path == nameof(ImagingCorrectionParameters.CalibrationSource));
        Assert.Contains(validation.Issues, value => value.Path == nameof(ImagingCorrectionParameters.CalibrationChecksum));
    }

    [Theory]
    [MemberData(nameof(CanonicalFormats))]
    public async Task DisabledPipelineIsPixelExactAndInputReadonlyForEveryCanonicalFormat(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer source = Pattern(7, 5, format, 123, 117);
        byte[] before = source.Data.ToArray();
        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters());
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Image(result, "corrected-image");
        Assert.Equal(format, output.Format);
        Assert.Equal(123, output.DpiX);
        Assert.Equal(117, output.DpiY);
        Assert.Equal(before, output.Data.ToArray());
        Assert.Equal(before, source.Data.ToArray());
        Assert.All(Image(result, "correction-validity-mask").Data.ToArray(), value => Assert.Equal(byte.MaxValue, value));
        Assert.Contains(result.Diagnostics.Messages, value => value.Code == "imaging_correction_identity");
    }

    [Fact]
    public async Task DarkFlatAndResidualShadingHaveDeterministicFloatGoldenAndProvenance()
    {
        using AlgorithmImageBuffer source = FloatBuffer(2, 1, [0.5f, 0.5f]);
        using AlgorithmImageBuffer dark = FloatBuffer(2, 1, [0.1f, 0.1f]);
        using AlgorithmImageBuffer flat = FloatBuffer(2, 1, [0.5f, 0.5f]);
        using AlgorithmImageBuffer shading = FloatBuffer(2, 1, [0.3f, 0.5f]);
        ImagingCorrectionParameters parameters = new()
        {
            EnableDarkFrame = true,
            EnableFlatField = true,
            EnableShading = true,
            OutputRangePolicy = ImagingCorrectionOutputRangePolicy.PreserveFloatingPoint,
            CalibrationSource = "numeric-fixture",
            CalibrationVersion = "v4",
            CalibrationChecksum = "sha256:set",
        };
        using AlgorithmResult result = await RunAsync(source, parameters,
        [
            Input("dark-frame", dark, "dark.tif", "sha256:dark"),
            Input("flat-field", flat, "flat.tif", "sha256:flat"),
            Input("shading-reference", shading, "shading.tif", "sha256:shading"),
        ], presetId: "fixture-preset");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        ReadOnlySpan<byte> bytes = Image(result, "corrected-image").Data.Span;
        Assert.Equal(0.6f, BinaryPrimitives.ReadSingleLittleEndian(bytes[..4]), 5);
        Assert.Equal(0.3f, BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(4, 4)), 5);
        AlgorithmStructuredDataArtifact structured = result.GetArtifact<AlgorithmStructuredDataArtifact>("imaging-correction")!;
        Assert.Equal(ImagingCorrectionAlgorithmProvider.ResultSchema, structured.Schema);
        Assert.Equal("fixture-preset", structured.Data.GetProperty("presetId").GetString());
        Assert.Equal("numeric-fixture", structured.Data.GetProperty("calibration").GetProperty("source").GetString());
        Assert.Equal(4, result.GetArtifact<AlgorithmTableArtifact>("imaging-correction-provenance")!.Rows.Count);
        Assert.Equal(new[] { 0.5f, 0.5f }, ReadFloats(source));
    }

    [Fact]
    public async Task Gray16DarkGoldenAndBgraAlphaPolicyPreserveBitDepthAndChannelSemantics()
    {
        using AlgorithmImageBuffer graySource = UShortBuffer(2, 1, [32768, 50000]);
        using AlgorithmImageBuffer grayDark = UShortBuffer(2, 1, [8192, 1000]);
        using AlgorithmResult gray = await RunAsync(graySource, new ImagingCorrectionParameters { EnableDarkFrame = true }, [Input("dark-frame", grayDark)]);
        Assert.Equal(AlgorithmImageFormat.Gray16, Image(gray, "corrected-image").Format);
        Assert.Equal(new ushort[] { 24576, 49000 }, ReadUShorts(Image(gray, "corrected-image")));

        using AlgorithmImageBuffer colorSource = new(1, 1, 4, AlgorithmImageFormat.Bgra32, [100, 110, 120, 77]);
        using AlgorithmImageBuffer colorDark = new(1, 1, 4, AlgorithmImageFormat.Bgra32, [10, 10, 10, 255]);
        using AlgorithmResult color = await RunAsync(colorSource, new ImagingCorrectionParameters { EnableDarkFrame = true }, [Input("dark-frame", colorDark)]);
        Assert.Equal(new byte[] { 90, 100, 110, 77 }, Image(color, "corrected-image").Data.ToArray());
        Assert.Equal(new byte[] { 100, 110, 120, 77 }, colorSource.Data.ToArray());

        using AlgorithmImageBuffer floatingSource = FloatBuffer(1, 1, [0.1f]);
        using AlgorithmImageBuffer floatingDark = FloatBuffer(1, 1, [0.2f]);
        using AlgorithmResult preserved = await RunAsync(floatingSource, new ImagingCorrectionParameters
        {
            EnableDarkFrame = true,
            OutputRangePolicy = ImagingCorrectionOutputRangePolicy.PreserveFloatingPoint,
        }, [Input("dark-frame", floatingDark)]);
        Assert.Equal(-0.1f, ReadFloats(Image(preserved, "corrected-image"))[0], 5);
    }

    [Fact]
    public async Task ZeroAndSaturatedReferencesFollowStructuredPoliciesAndRangeRules()
    {
        using AlgorithmImageBuffer source = ByteBuffer(2, 1, [100, 100]);
        using AlgorithmImageBuffer flat = ByteBuffer(2, 1, [0, 128]);
        ImagingCorrectionParameters preserve = new()
        {
            EnableFlatField = true,
            MinimumValidReferenceFraction = 0.5,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
        };
        using AlgorithmResult result = await RunAsync(source, preserve, [Input("flat-field", flat)]);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(new byte[] { 100, 100 }, Image(result, "corrected-image").Data.ToArray());
        Assert.Equal(new byte[] { 0, 255 }, Image(result, "correction-validity-mask").Data.ToArray());

        using AlgorithmImageBuffer rejectSource = ByteBuffer(2, 1, [100, 100]);
        using AlgorithmImageBuffer rejectFlat = ByteBuffer(2, 1, [0, 255]);
        using AlgorithmResult rejected = await RunAsync(rejectSource, WithPolicy(InvalidReferencePixelPolicy.RejectInvocation), [Input("flat-field", rejectFlat)]);
        Assert.Equal(AlgorithmResultStatus.Failed, rejected.Status);
        Assert.Contains(rejected.Failures, value => value.Code == "reference_valid_fraction_too_low" || value.Code == "invalid_reference_pixel");

        using AlgorithmImageBuffer darkSource = ByteBuffer(2, 1, [100, 100]);
        using AlgorithmImageBuffer saturatedDark = ByteBuffer(2, 1, [255, 10]);
        using AlgorithmResult darkResult = await RunAsync(darkSource, new ImagingCorrectionParameters
        {
            EnableDarkFrame = true,
            MinimumValidReferenceFraction = 0.5,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
        }, [Input("dark-frame", saturatedDark)]);
        Assert.Equal(new byte[] { 100, 90 }, Image(darkResult, "corrected-image").Data.ToArray());
        Assert.Equal(new byte[] { 0, 255 }, Image(darkResult, "correction-validity-mask").Data.ToArray());

        static ImagingCorrectionParameters WithPolicy(InvalidReferencePixelPolicy policy) => new()
        {
            EnableFlatField = true,
            MinimumValidReferenceFraction = 0,
            InvalidReferencePolicy = policy,
        };
    }

    [Fact]
    public async Task ColorReferenceValidityRequiresEveryCorrectedChannelAtTheSamePixel()
    {
        using AlgorithmImageBuffer source = new(1, 1, 3, AlgorithmImageFormat.Bgr24, [100, 100, 100]);
        using AlgorithmImageBuffer flat = new(1, 1, 3, AlgorithmImageFormat.Bgr24, [0, 128, 128]);

        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            MinimumValidReferenceFraction = 0.5,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
        }, [Input("flat-field", flat)]);

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "reference_valid_fraction_too_low");
        Assert.Empty(result.Artifacts);
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Gray8)]
    [InlineData(AlgorithmImageFormat.Gray16)]
    [InlineData(AlgorithmImageFormat.Gray32Float)]
    public async Task SingleChannelAllInvalidReferenceFailsEvenWhenMinimumFractionIsZero(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer source = format switch
        {
            AlgorithmImageFormat.Gray8 => ByteBuffer(1, 1, [100]),
            AlgorithmImageFormat.Gray16 => UShortBuffer(1, 1, [30_000]),
            _ => FloatBuffer(1, 1, [0.5f]),
        };
        using AlgorithmImageBuffer flat = format switch
        {
            AlgorithmImageFormat.Gray8 => ByteBuffer(1, 1, [0]),
            AlgorithmImageFormat.Gray16 => UShortBuffer(1, 1, [0]),
            _ => FloatBuffer(1, 1, [0]),
        };

        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            MinimumValidReferenceFraction = 0,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
        }, [Input("flat-field", flat)]);

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "reference_valid_fraction_too_low");
    }

    [Fact]
    public async Task BgraAlphaParticipatesInPixelValidityOnlyWhenCorrectionIsEnabled()
    {
        using AlgorithmImageBuffer source = new(1, 1, 4, AlgorithmImageFormat.Bgra32, [100, 100, 100, 200]);
        using AlgorithmImageBuffer flat = new(1, 1, 4, AlgorithmImageFormat.Bgra32, [128, 128, 128, 0]);
        using AlgorithmResult alphaPreserved = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            CorrectAlpha = false,
            MinimumValidReferenceFraction = 1,
        }, [Input("flat-field", flat)]);
        Assert.Equal(AlgorithmResultStatus.Succeeded, alphaPreserved.Status);
        Assert.Equal(byte.MaxValue, Assert.Single(Image(alphaPreserved, "correction-validity-mask").Data.ToArray()));

        using AlgorithmImageBuffer correctedSource = new(1, 1, 4, AlgorithmImageFormat.Bgra32, [100, 100, 100, 200]);
        using AlgorithmImageBuffer correctedFlat = new(1, 1, 4, AlgorithmImageFormat.Bgra32, [128, 128, 128, 0]);
        using AlgorithmResult alphaCorrected = await RunAsync(correctedSource, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            CorrectAlpha = true,
            MinimumValidReferenceFraction = 0.5,
        }, [Input("flat-field", correctedFlat)]);
        Assert.Equal(AlgorithmResultStatus.Failed, alphaCorrected.Status);
        Assert.Contains(alphaCorrected.Failures, failure => failure.Code == "reference_valid_fraction_too_low");
    }

    [Fact]
    public async Task StageValidityMetricsAndMaskUseTheSameAllChannelsPixelRuleAtTheBoundary()
    {
        using AlgorithmImageBuffer source = new(2, 1, 6, AlgorithmImageFormat.Bgr24, [100, 100, 100, 100, 100, 100]);
        using AlgorithmImageBuffer flat = new(2, 1, 6, AlgorithmImageFormat.Bgr24, [0, 128, 128, 128, 128, 128]);
        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            MinimumValidReferenceFraction = 0.5,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
        }, [Input("flat-field", flat)]);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(new byte[] { 0, 255 }, Image(result, "correction-validity-mask").Data.ToArray());
        Assert.Equal(0.5, Measurement(result, "imaging-correction.valid_fraction"), 12);
        Assert.Equal(0.5, Measurement(result, "imaging-correction.flat_valid_fraction"), 12);
        IReadOnlyDictionary<string, JsonElement> flatStage = Assert.Single(
            result.GetArtifact<AlgorithmTableArtifact>("imaging-correction-stages")!.Rows,
            row => row["Stage"].GetString() == "flat-field");
        Assert.Equal(1, flatStage["ValidPixels"].GetInt64());
        Assert.Equal(1, flatStage["InvalidPixels"].GetInt64());
        Assert.Equal(0.5, flatStage["ValidFraction"].GetDouble(), 12);
    }

    [Fact]
    public async Task PreserveFloatingPointRejectsGainOverflowBeforePublishingInfinityAsValid()
    {
        using AlgorithmImageBuffer source = FloatBuffer(2, 1, [float.MaxValue, 0.5f]);
        using AlgorithmImageBuffer flat = FloatBuffer(2, 1, [0.00001f, 0.5f]);
        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            MaximumGain = 1_000_000,
            MinimumValidReferenceFraction = 0.5,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
            OutputRangePolicy = ImagingCorrectionOutputRangePolicy.PreserveFloatingPoint,
        }, [Input("flat-field", flat)]);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        float[] output = ReadFloats(Image(result, "corrected-image"));
        Assert.Equal(float.MaxValue, output[0]);
        Assert.True(float.IsFinite(output[1]));
        Assert.Equal(new byte[] { 0, 255 }, Image(result, "correction-validity-mask").Data.ToArray());
        Assert.Equal(1, Measurement(result, "imaging-correction.non_finite_output_sample_count"));
    }

    [Fact]
    public async Task NonFiniteFloatInputsAreInvalidAndNeverCopiedIntoSuccessfulOutput()
    {
        using AlgorithmImageBuffer source = FloatBuffer(4, 1, [float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0.5f]);
        using AlgorithmImageBuffer flat = FloatBuffer(4, 1, [0.5f, 0.5f, 0.5f, 0.5f]);
        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            MinimumValidReferenceFraction = 0.25,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
            OutputRangePolicy = ImagingCorrectionOutputRangePolicy.PreserveFloatingPoint,
        }, [Input("flat-field", flat)]);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.All(ReadFloats(Image(result, "corrected-image")), value => Assert.True(float.IsFinite(value)));
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, Image(result, "correction-validity-mask").Data.ToArray());
        Assert.Equal(3, Measurement(result, "imaging-correction.non_finite_output_sample_count"));
    }

    [Fact]
    public async Task AllFloatOutputsOutsideTheTargetRangeFailAndReleaseArtifacts()
    {
        using AlgorithmImageBuffer source = FloatBuffer(2, 1, [float.MaxValue, float.MaxValue]);
        using AlgorithmImageBuffer flat = FloatBuffer(2, 1, [0.25f, 0.75f]);
        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            MinimumGain = 2,
            MaximumGain = 2,
            MinimumValidReferenceFraction = 0,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
            OutputRangePolicy = ImagingCorrectionOutputRangePolicy.PreserveFloatingPoint,
        }, [Input("flat-field", flat)]);

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "correction_no_valid_pixels");
        Assert.Empty(result.Artifacts);
    }

    [Fact]
    public async Task FloatAlphaRemainsFiniteAndUnchangedWhenColorGainOverflows()
    {
        using AlgorithmImageBuffer source = FloatBuffer(2, 1,
        [
            float.MaxValue, 0.25f, 0.5f, 0.75f,
            0.25f, 0.25f, 0.5f, 0.5f,
        ], AlgorithmImageFormat.Bgra128Float);
        using AlgorithmImageBuffer flat = FloatBuffer(2, 1,
        [
            0.00001f, 0.5f, 0.5f, float.NaN,
            0.5f, 0.5f, 0.5f, float.NaN,
        ], AlgorithmImageFormat.Bgra128Float);
        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableFlatField = true,
            CorrectAlpha = false,
            MaximumGain = 1_000_000,
            MinimumValidReferenceFraction = 0.5,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.PreserveSource,
            OutputRangePolicy = ImagingCorrectionOutputRangePolicy.PreserveFloatingPoint,
        }, [Input("flat-field", flat)]);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        float[] output = ReadFloats(Image(result, "corrected-image"));
        Assert.Equal(0.75f, output[3]);
        Assert.Equal(0.5f, output[7]);
        Assert.All(output, value => Assert.True(float.IsFinite(value)));
        Assert.Equal(new byte[] { 0, 255 }, Image(result, "correction-validity-mask").Data.ToArray());
    }

    [Fact]
    public async Task BadPixelMapUsesGoodNeighborMedianAndReportsUnresolvedPixels()
    {
        using AlgorithmImageBuffer source = ByteBuffer(5, 1, [10, 20, 255, 40, 50]);
        using AlgorithmImageBuffer map = ByteBuffer(5, 1, [0, 0, 255, 0, 0]);
        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            EnableBadPixelCorrection = true,
            BadPixelRadius = 1,
        }, [Input("bad-pixel-map", map)]);
        Assert.Equal(new byte[] { 10, 20, 30, 40, 50 }, Image(result, "corrected-image").Data.ToArray());
        Assert.Equal(1, Measurement(result, "imaging-correction.bad_pixels_marked"));
        Assert.Equal(1, Measurement(result, "imaging-correction.bad_pixels_corrected"));

        using AlgorithmImageBuffer unresolvedSource = ByteBuffer(3, 1, [1, 2, 3]);
        using AlgorithmImageBuffer unresolvedMap = ByteBuffer(3, 1, [255, 255, 255]);
        using AlgorithmResult unresolved = await RunAsync(unresolvedSource, new ImagingCorrectionParameters
        {
            EnableBadPixelCorrection = true,
            InvalidReferencePolicy = InvalidReferencePixelPolicy.FillConstant,
            InvalidReferenceFillNormalized = 0.25,
        }, [Input("bad-pixel-map", unresolvedMap)]);
        Assert.Equal(new byte[] { 64, 64, 64 }, Image(unresolved, "corrected-image").Data.ToArray());
        Assert.Equal(3, Measurement(unresolved, "imaging-correction.bad_pixels_unresolved"));
    }

    [Fact]
    public async Task NamedInputShapeFormatColorSpaceAndRoiFailuresAreStructured()
    {
        using AlgorithmImageBuffer source = ByteBuffer(2, 2, [1, 2, 3, 4]);
        using AlgorithmResult missing = await RunAsync(source, new ImagingCorrectionParameters { EnableDarkFrame = true });
        Assert.Contains(missing.Failures, value => value.Code == "reference_input_missing");

        using AlgorithmImageBuffer wrongSizeSource = ByteBuffer(2, 2, [1, 2, 3, 4]);
        using AlgorithmImageBuffer wrongSize = ByteBuffer(1, 1, [1]);
        using AlgorithmResult size = await RunAsync(wrongSizeSource, new ImagingCorrectionParameters { EnableDarkFrame = true }, [Input("dark-frame", wrongSize)]);
        Assert.Contains(size.Failures, value => value.Code == "reference_size_mismatch");

        using AlgorithmImageBuffer colorSource = ByteBuffer(2, 2, [1, 2, 3, 4]);
        using AlgorithmImageBuffer colorDark = ByteBuffer(2, 2, [1, 1, 1, 1]);
        using AlgorithmResult color = await RunAsync(colorSource, new ImagingCorrectionParameters { EnableDarkFrame = true },
            [new AlgorithmInput { Name = "dark-frame", Image = colorDark, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "linear" }]);
        Assert.Contains(color.Failures, value => value.Code == "reference_color_space_mismatch");

        using AlgorithmImageBuffer formatSource = ByteBuffer(2, 2, [1, 2, 3, 4]);
        using AlgorithmImageBuffer wrongFormat = UShortBuffer(2, 2, [1, 1, 1, 1]);
        using AlgorithmResult format = await RunAsync(formatSource, new ImagingCorrectionParameters { EnableDarkFrame = true }, [Input("dark-frame", wrongFormat)]);
        Assert.Contains(format.Failures, value => value.Code == "reference_format_mismatch");

        using AlgorithmImageBuffer mapSource = ByteBuffer(2, 2, [1, 2, 3, 4]);
        using AlgorithmImageBuffer wrongMap = UShortBuffer(2, 2, [0, 0, 0, 0]);
        using AlgorithmResult mapFormat = await RunAsync(mapSource, new ImagingCorrectionParameters { EnableBadPixelCorrection = true }, [Input("bad-pixel-map", wrongMap)]);
        Assert.Contains(mapFormat.Failures, value => value.Code == "bad_pixel_map_format_mismatch");

        using AlgorithmImageBuffer roiSource = ByteBuffer(2, 2, [1, 2, 3, 4]);
        using AlgorithmResult roi = await RunAsync(roiSource, new ImagingCorrectionParameters(), roi: new RectangleAlgorithmRoi(0, 0, 1, 1));
        Assert.Contains(roi.Failures, value => value.Code == "roi_kind_unsupported");
    }

    [Fact]
    public async Task SuccessFailureCancellationAndResultDisposalReleaseTransferredOwnership()
    {
        AlgorithmImageBuffer successSource = Pattern(64, 48, AlgorithmImageFormat.Gray16);
        AlgorithmResult success = await RunTransferredAsync(successSource, new ImagingCorrectionParameters(), []);
        AlgorithmImageBuffer[] outputs = success.Artifacts.OfType<AlgorithmImageArtifact>().Select(value => value.Image).ToArray();
        Assert.True(successSource.IsDisposed);
        Assert.All(outputs, value => Assert.False(value.IsDisposed));
        success.Dispose();
        Assert.All(outputs, value => Assert.True(value.IsDisposed));

        AlgorithmImageBuffer failedSource = ByteBuffer(2, 2, [1, 2, 3, 4]);
        AlgorithmImageBuffer failedReference = ByteBuffer(1, 1, [1]);
        using AlgorithmResult failed = await RunTransferredAsync(failedSource, new ImagingCorrectionParameters { EnableDarkFrame = true },
            [new AlgorithmInput { Name = "dark-frame", Image = failedReference, Ownership = AlgorithmInputOwnership.Transferred, ColorSpace = "encoded-device-values" }]);
        Assert.True(failedSource.IsDisposed);
        Assert.True(failedReference.IsDisposed);

        AlgorithmImageBuffer cancelledSource = Pattern(1024, 512, AlgorithmImageFormat.Bgra32);
        AlgorithmImageBuffer cancelledDark = Pattern(1024, 512, AlgorithmImageFormat.Bgra32);
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value => { if (value.Stage == "imaging-correction.apply") cancellation.Cancel(); });
        using AlgorithmResult cancelled = await RunTransferredAsync(cancelledSource, new ImagingCorrectionParameters { EnableDarkFrame = true },
            [new AlgorithmInput { Name = "dark-frame", Image = cancelledDark, Ownership = AlgorithmInputOwnership.Transferred, ColorSpace = "encoded-device-values" }], progress, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledSource.IsDisposed);
        Assert.True(cancelledDark.IsDisposed);
    }

    [Fact]
    public async Task BatchAndNamedFlowFramesUseTheSameParametersAndArtifacts()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-imaging-correction-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string darkPath = Path.Combine(directory, "dark.png");
        try
        {
            WpfTestHost.Invoke(() => SaveGray8Png(darkPath, 4, 3, Enumerable.Repeat((byte)10, 12).ToArray()));
            BatchImageAlgorithmDefinition batch = BatchImageAlgorithms.CreateAll().Single(value => value.Descriptor?.Id == StandardAlgorithmIds.ImagingCorrection);
            ImagingCorrectionParameters parameters = Assert.IsType<ImagingCorrectionParameters>(batch.Options);
            parameters.EnableDarkFrame = true;
            parameters.DarkFramePath = darkPath;
            using Mat source = new(3, 4, MatType.CV_8UC1, Scalar.All(100));
            using Mat output = batch.Apply(source);
            Assert.Equal(90, output.At<byte>(0, 0));

            LocalFrameMetadata metadata = new() { Width = 4, Height = 3, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame sourceFrame = LocalFlowFrame.Allocate(metadata, 12, 0);
            using LocalFlowFrame darkFrame = LocalFlowFrame.Allocate(metadata, 12, 0);
            using LocalFlowFrameLease sourceLease = sourceFrame.Acquire();
            using LocalFlowFrameLease darkLease = darkFrame.Acquire();
            Marshal.Copy(Enumerable.Repeat((byte)100, 12).ToArray(), 0, sourceLease.RawPointer, 12);
            Marshal.Copy(Enumerable.Repeat((byte)10, 12).ToArray(), 0, darkLease.RawPointer, 12);
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawSetAsync(
                new Dictionary<string, LocalFlowFrameLease> { ["source"] = sourceLease, ["dark-frame"] = darkLease },
                AlgorithmInvocation.Create(StandardAlgorithmIds.ImagingCorrection, parameters));
            Assert.Equal(AlgorithmResultStatus.Succeeded, flow.Status);
            Assert.Equal(90, Image(flow, "corrected-image").Data.Span[0]);
            Assert.NotNull(flow.GetArtifact<AlgorithmStructuredDataArtifact>("imaging-correction"));
        }
        finally
        {
            if (File.Exists(darkPath)) File.Delete(darkPath);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task ImageViewMultiInputSessionCommitsAndResultWindowOwnsArtifacts()
    {
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView view = new();
            WriteableBitmap bitmap = new(4, 3, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 4, 3), Enumerable.Repeat((byte)100, 12).ToArray(), 4, 0);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        AlgorithmResult? result = null;
        try
        {
            ImageProcessingContext context = imageView.EditorContext.ProcessingContext;
            WpfTestHost.Invoke(() => Assert.Single(new AlgorithmsContextMenu(context).GetContextMenuItems(), value => value.GuidId == "ImagingCorrection"));
            long revision = context.ImageRevision;
            AlgorithmImageBuffer dark = ByteBuffer(4, 3, Enumerable.Repeat((byte)10, 12).ToArray());
            result = await WpfTestHost.Invoke(() => ImageAlgorithmApplier.ApplyAsync(
                context,
                AlgorithmInvocation.Create(StandardAlgorithmIds.ImagingCorrection, new ImagingCorrectionParameters { EnableDarkFrame = true }),
                [new AlgorithmInput { Name = "dark-frame", Image = dark, Ownership = AlgorithmInputOwnership.Transferred, ColorSpace = "encoded-device-values" }]));
            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.Equal(revision + 1, context.ImageRevision);
            Assert.True(dark.IsDisposed);
            WpfTestHost.Invoke(() =>
            {
                ImagingCorrectionResultWindow window = new(result);
                window.Show();
                Assert.Equal(4, Assert.IsAssignableFrom<BitmapSource>(Assert.IsType<System.Windows.Controls.Image>(window.FindName("CorrectedPreview")).Source).PixelWidth);
                Assert.NotNull(Assert.IsType<DataGrid>(window.FindName("StageGrid")).ItemsSource);
                Assert.NotNull(Assert.IsType<DataGrid>(window.FindName("ProvenanceGrid")).ItemsSource);
                window.Close();
                Assert.True(result.IsDisposed);
            });
        }
        finally
        {
            result?.Dispose();
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public async Task StalePreviewConsumesAndReleasesTransferredReferenceBeforeRunnerStarts()
    {
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView view = new();
            WriteableBitmap bitmap = new(4, 3, 96, 96, PixelFormats.Gray8, null);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        ImageAlgorithmPreviewSession? first = null;
        try
        {
            ImageProcessingContext context = imageView.EditorContext.ProcessingContext;
            first = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            WpfTestHost.Invoke(context.NotifySourcePixelsChanged);
            AlgorithmImageBuffer transferred = ByteBuffer(4, 3, new byte[12]);
            using AlgorithmResult superseded = await first.PreviewWithInputsAsync(
                AlgorithmInvocation.Create(StandardAlgorithmIds.ImagingCorrection, new ImagingCorrectionParameters { EnableDarkFrame = true }),
                [new AlgorithmInput { Name = "dark-frame", Image = transferred, Ownership = AlgorithmInputOwnership.Transferred, ColorSpace = "encoded-device-values" }],
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.MultiInput);
            Assert.Equal(AlgorithmResultStatus.Superseded, superseded.Status);
            Assert.True(transferred.IsDisposed);
        }
        finally
        {
            WpfTestHost.Invoke(() => { first?.Dispose(); imageView.Dispose(); });
        }
    }

    [Fact]
    public async Task StructuredArtifactsExportWithoutOverwriting()
    {
        using AlgorithmImageBuffer source = ByteBuffer(3, 2, [10, 20, 30, 40, 50, 60]);
        using AlgorithmResult result = await RunAsync(source, new ImagingCorrectionParameters
        {
            CalibrationSource = "export-fixture",
            CalibrationVersion = "v2",
            CalibrationChecksum = "sha256:export",
        });
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-imaging-correction-{Guid.NewGuid():N}.json");
        try
        {
            Assert.Equal(path, AlgorithmResultExporter.ExportJson(result, path));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(StandardAlgorithmIds.ImagingCorrection.ToString(), document.RootElement.GetProperty("algorithmId").GetString());
            Assert.Contains("export-fixture", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Throws<IOException>(() => AlgorithmResultExporter.ExportJson(result, path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<AlgorithmResult> RunAsync(
        AlgorithmImageBuffer source,
        ImagingCorrectionParameters parameters,
        IReadOnlyList<AlgorithmInput>? references = null,
        string? presetId = null,
        AlgorithmRoi? roi = null)
    {
        AlgorithmInvocation basic = AlgorithmInvocation.Create(StandardAlgorithmIds.ImagingCorrection, parameters, roi);
        AlgorithmInvocation invocation = new()
        {
            InvocationId = basic.InvocationId,
            AlgorithmId = basic.AlgorithmId,
            ParameterSchemaVersion = basic.ParameterSchemaVersion,
            Parameters = basic.Parameters,
            Roi = roi,
            PresetId = presetId,
        };
        List<AlgorithmInput> inputs =
        [
            new AlgorithmInput { Name = "source", Image = source, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "encoded-device-values" },
        ];
        if (references != null) inputs.AddRange(references);
        return await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs = inputs,
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local
                | (inputs.Count > 1 ? AlgorithmHostCapabilities.MultiInput : AlgorithmHostCapabilities.None),
        });
    }

    private static async Task<AlgorithmResult> RunTransferredAsync(
        AlgorithmImageBuffer source,
        ImagingCorrectionParameters parameters,
        IReadOnlyList<AlgorithmInput> references,
        IProgress<AlgorithmProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<AlgorithmInput> inputs =
        [
            new AlgorithmInput { Name = "source", Image = source, Ownership = AlgorithmInputOwnership.Transferred, ColorSpace = "encoded-device-values" },
        ];
        inputs.AddRange(references);
        return await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImagingCorrection, parameters),
            Inputs = inputs,
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local
                | (inputs.Count > 1 ? AlgorithmHostCapabilities.MultiInput : AlgorithmHostCapabilities.None),
            Progress = progress,
        }, cancellationToken);
    }

    private static AlgorithmInput Input(string name, AlgorithmImageBuffer image, string? uri = null, string? checksum = null)
        => new() { Name = name, Image = image, Ownership = AlgorithmInputOwnership.Borrowed, SourceUri = uri, Checksum = checksum, ColorSpace = "encoded-device-values" };

    private static AlgorithmImageBuffer ByteBuffer(int width, int height, byte[] values)
        => new(width, height, width, AlgorithmImageFormat.Gray8, values);

    private static AlgorithmImageBuffer FloatBuffer(
        int width,
        int height,
        float[] values,
        AlgorithmImageFormat format = AlgorithmImageFormat.Gray32Float)
    {
        Assert.True(format.IsFloatingPoint());
        Assert.Equal(width * height * format.Channels(), values.Length);
        byte[] bytes = new byte[values.Length * 4];
        for (int index = 0; index < values.Length; index++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * 4, 4), values[index]);
        return new AlgorithmImageBuffer(width, height, width * format.BytesPerPixel(), format, bytes);
    }

    private static AlgorithmImageBuffer UShortBuffer(int width, int height, ushort[] values)
    {
        byte[] bytes = new byte[values.Length * 2];
        for (int index = 0; index < values.Length; index++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2, 2), values[index]);
        return new AlgorithmImageBuffer(width, height, width * 2, AlgorithmImageFormat.Gray16, bytes);
    }

    private static float[] ReadFloats(AlgorithmImageBuffer image)
    {
        float[] values = new float[image.Data.Length / 4];
        for (int index = 0; index < values.Length; index++) values[index] = BinaryPrimitives.ReadSingleLittleEndian(image.Data.Span.Slice(index * 4, 4));
        return values;
    }

    private static ushort[] ReadUShorts(AlgorithmImageBuffer image)
    {
        ushort[] values = new ushort[image.Data.Length / 2];
        for (int index = 0; index < values.Length; index++) values[index] = BinaryPrimitives.ReadUInt16LittleEndian(image.Data.Span.Slice(index * 2, 2));
        return values;
    }

    private static AlgorithmImageBuffer Pattern(int width, int height, AlgorithmImageFormat format, double dpiX = 96, double dpiY = 96)
    {
        int stride = width * format.BytesPerPixel();
        byte[] data = new byte[stride * height];
        int samples = width * height * format.Channels();
        for (int index = 0; index < samples; index++)
        {
            int offset = index * format.BitsPerChannel() / 8;
            if (format.BitsPerChannel() == 8) data[offset] = (byte)(index * 17 % 251);
            else if (format.BitsPerChannel() == 16) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), (ushort)(index * 1009 % 65521));
            else BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset, 4), index / 37f);
        }
        return new AlgorithmImageBuffer(width, height, stride, format, data, dpiX, dpiY);
    }

    private static AlgorithmImageBuffer Image(AlgorithmResult result, string name) => result.GetArtifact<AlgorithmImageArtifact>(name)!.Image;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("imaging-correction-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static void SaveGray8Png(string path, int width, int height, byte[] pixels)
    {
        BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static void EnsureResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(System.Windows.Controls.Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
