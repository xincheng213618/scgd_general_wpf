using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageRegistration;
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

public sealed class ImageRegistrationV1Tests
{
    public static TheoryData<AlgorithmImageFormat> CanonicalFormats => new()
    {
        AlgorithmImageFormat.Gray8,
        AlgorithmImageFormat.Gray16,
        AlgorithmImageFormat.Gray32Float,
        AlgorithmImageFormat.Bgr24,
        AlgorithmImageFormat.Bgr48,
        AlgorithmImageFormat.Bgr96Float,
        AlgorithmImageFormat.Bgra32,
        AlgorithmImageFormat.Bgra64,
        AlgorithmImageFormat.Bgra128Float,
    };

    [Fact]
    public void CatalogDefaultsSchemaAliasesAndPresetRoundTripAreStable()
    {
        AlgorithmDescriptor descriptor = StandardAlgorithmCatalog.Create().Descriptors.Single(value => value.Id == StandardAlgorithmIds.ImageRegistration);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.Equal(2, descriptor.MinimumInputCount);
        Assert.Equal(2, descriptor.MaximumInputCount);
        Assert.Equal("primary=same-as-moving; canvas=reference; validity-mask=gray8", descriptor.OutputFormatPolicy);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.MultiInput));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(StandardAlgorithmCatalog.Create().TryResolveAlias("OrbHomographyRegistration", out AlgorithmDescriptor? alias));
        Assert.Equal(descriptor.Id, alias!.Id);
        ImageRegistrationParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<ImageRegistrationParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        Assert.Contains(descriptor.ParameterSchema.Fields, field => field.Name == nameof(ImageRegistrationParameters.ConsensusReprojectionThresholdPixels)
            && field.Unit == "px" && field.Minimum == 0.01);

        ImageRegistrationParameters parameters = new() { Method = ImageRegistrationMethod.OrbHomography, MaximumFeatures = 1_500 };
        string json = ImageRegistrationPresetSerializer.Serialize("registration-fixture", parameters);
        (string presetId, ImageRegistrationParameters restored) = ImageRegistrationPresetSerializer.Deserialize(json);
        Assert.Equal("registration-fixture", presetId);
        Assert.Equal(ImageRegistrationMethod.OrbHomography, restored.Method);
        Assert.Equal(1_500, restored.MaximumFeatures);
    }

    [Fact]
    public void ValidationAndPresetVersionBoundaryAreStrict()
    {
        ImageRegistrationParameters invalid = new()
        {
            MinimumPhaseResponse = -1,
            MaximumFeatures = 10,
            MinimumMatchCount = 12,
            MinimumInlierCount = 13,
            MaximumConsensusMatches = 8,
            BorderChannel0 = 2,
        };
        AlgorithmValidationResult validation = invalid.Validate();
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(ImageRegistrationParameters.MinimumPhaseResponse));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(ImageRegistrationParameters.MaximumFeatures));
        Assert.Contains(validation.Issues, issue => issue.Code == "inlier_count_exceeds_match_count");
        Assert.Contains(validation.Issues, issue => issue.Code == "consensus_limit_below_match_count");
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(ImageRegistrationParameters.BorderChannel0));

        AlgorithmParameterPreset missingVersion = new()
        {
            PresetId = "missing-version",
            AlgorithmId = StandardAlgorithmIds.ImageRegistration,
            Parameters = AlgorithmJson.ToElement(new ImageRegistrationParameters()),
        };
        Assert.Throws<InvalidOperationException>(() => ImageRegistrationPresetSerializer.Deserialize(JsonSerializer.Serialize(missingVersion, AlgorithmJson.Options)));
    }

    [Theory]
    [MemberData(nameof(CanonicalFormats))]
    public async Task PhaseIdentityGoldenPreservesEveryCanonicalFormatInputAndDpi(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer reference = Pattern(64, 48, format, 121, 119);
        using AlgorithmImageBuffer moving = reference.Clone();
        byte[] beforeReference = reference.Data.ToArray();
        byte[] beforeMoving = moving.Data.ToArray();
        using AlgorithmResult result = await RunAsync(reference, moving, new ImageRegistrationParameters
        {
            Interpolation = GeometricTransformInterpolation.Nearest,
            MinimumPhaseResponse = 0,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Image(result, "registered-image");
        Assert.Equal(format, output.Format);
        Assert.Equal(reference.Width, output.Width);
        Assert.Equal(reference.Height, output.Height);
        Assert.Equal(121, output.DpiX);
        Assert.Equal(119, output.DpiY);
        Assert.Equal(beforeMoving, output.Data.ToArray());
        Assert.Equal(beforeReference, reference.Data.ToArray());
        Assert.Equal(beforeMoving, moving.Data.ToArray());
        Assert.InRange(Math.Abs(Measurement(result, "registration.phase_shift_x")), 0, 1e-5);
        Assert.InRange(Math.Abs(Measurement(result, "registration.phase_shift_y")), 0, 1e-5);
        Assert.Equal(1, Measurement(result, "registration.valid_fraction"));
    }

    [Fact]
    public async Task PhaseCorrelationRecoversSubpixelDirectionAndMovingToReferenceMatrix()
    {
        const int width = 96;
        const int height = 80;
        using AlgorithmImageBuffer reference = Pattern(width, height, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer moving = CircularShift(reference, 7, -4);
        using AlgorithmResult result = await RunAsync(reference, moving, new ImageRegistrationParameters
        {
            UseHannWindow = false,
            MinimumPhaseResponse = 0.2,
            Interpolation = GeometricTransformInterpolation.Nearest,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.InRange(Measurement(result, "registration.phase_shift_x"), 6.99, 7.01);
        Assert.InRange(Measurement(result, "registration.phase_shift_y"), -4.01, -3.99);
        double[] matrix = ReadMatrix(result.GetArtifact<AlgorithmTableArtifact>("image-registration-matrix")!, "M");
        Assert.InRange(matrix[2], -7.01, -6.99);
        Assert.InRange(matrix[5], 3.99, 4.01);
        Assert.InRange(Measurement(result, "registration.photometric_rmse"), 0, 1e-8);
        Assert.Equal("colorvision.geometry.registration/v1", result.GetArtifact<AlgorithmStructuredDataArtifact>("image-registration")!.Schema);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)127)]
    [InlineData((byte)255)]
    public async Task PhaseCorrelationRejectsIdenticalConstantInputsAsInsufficientTexture(byte value)
    {
        using AlgorithmImageBuffer reference = new(64, 48, 64, AlgorithmImageFormat.Gray8, Enumerable.Repeat(value, 64 * 48).ToArray());
        using AlgorithmImageBuffer moving = reference.Clone();
        using AlgorithmResult result = await RunAsync(reference, moving, new ImageRegistrationParameters { MinimumPhaseResponse = 0 });

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "phase_insufficient_texture");
        Assert.Empty(result.Artifacts);
    }

    [Fact]
    public async Task PhaseCorrelationRejectsPeriodicTextureWithNonUniqueCorrelationPeak()
    {
        byte[] checkerboard = new byte[96 * 80];
        for (int y = 0; y < 80; y++)
        for (int x = 0; x < 96; x++)
            checkerboard[y * 96 + x] = (byte)(((x / 4 + y / 4) & 1) == 0 ? 24 : 232);
        using AlgorithmImageBuffer reference = new(96, 80, 96, AlgorithmImageFormat.Gray8, checkerboard);
        using AlgorithmImageBuffer moving = CircularShift(reference, 8, -4);
        using AlgorithmResult result = await RunAsync(reference, moving, new ImageRegistrationParameters
        {
            UseHannWindow = false,
            MinimumPhaseResponse = 0,
        });

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "phase_ambiguous_texture");
        Assert.Empty(result.Artifacts);
    }

    [Fact]
    public async Task PhaseResultReportsCorrelationLossWithoutMislabelingItAsPixelRmse()
    {
        using AlgorithmImageBuffer reference = Pattern(96, 80, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer moving = CircularShift(reference, 7, -4);
        using AlgorithmResult result = await RunAsync(reference, moving, new ImageRegistrationParameters
        {
            UseHannWindow = false,
            MinimumPhaseResponse = 0.2,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmMeasurementArtifact summary = result.GetArtifact<AlgorithmMeasurementArtifact>("image-registration-summary")!;
        Assert.DoesNotContain(summary.Measurements, measurement => measurement.Name == "registration.geometric_rmse");
        AlgorithmMeasurement loss = Assert.Single(summary.Measurements, measurement => measurement.Name == "registration.correlation_loss");
        Assert.Equal("ratio", loss.Unit);
        Assert.InRange(loss.Value, 0, 1);
        AlgorithmGeometry transform = Assert.Single(
            result.GetArtifact<AlgorithmGeometryArtifact>("image-registration")!.Geometries,
            geometry => geometry.Kind == AlgorithmGeometryKind.Transform);
        Assert.Null(transform.Residual);
    }

    [Fact]
    public async Task PhaseCorrelationRecoversNoisySubpixelCircularShift()
    {
        using AlgorithmImageBuffer reference = FeaturePattern(128, 96);
        using AlgorithmImageBuffer moving = SubpixelCircularShift(reference, 2.5, -1.75, noiseSeed: 4471);
        using AlgorithmResult result = await RunAsync(reference, moving, new ImageRegistrationParameters
        {
            UseHannWindow = true,
            MinimumPhaseResponse = 0.1,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.InRange(Measurement(result, "registration.phase_shift_x"), 2.1, 2.9);
        Assert.InRange(Measurement(result, "registration.phase_shift_y"), -2.15, -1.35);
        Assert.InRange(Measurement(result, "registration.phase_peak_uniqueness"), 0.05, 1);
        Assert.InRange(Measurement(result, "registration.confidence"), 0, 1);
    }

    [Fact]
    public async Task OrbFeatureConsensusRecoversKnownProjectiveTransformDeterministically()
    {
        using AlgorithmImageBuffer reference = FeaturePattern(320, 240);
        double[] referenceToMoving = [1, 0.012, 9, -0.008, 1, 6, 0.00008, -0.00006, 1];
        using AlgorithmImageBuffer moving = Warp(reference, referenceToMoving, 340, 255);
        ImageRegistrationParameters parameters = new()
        {
            Method = ImageRegistrationMethod.OrbHomography,
            MaximumFeatures = 3_000,
            MinimumMatchCount = 8,
            MinimumInlierCount = 6,
            MinimumInlierRatio = 0.2,
            MaximumConsensusMatches = 50,
            MaximumConsensusEvaluations = 10_000,
            ConsensusReprojectionThresholdPixels = 3,
            Interpolation = GeometricTransformInterpolation.Linear,
        };
        using AlgorithmResult first = await RunAsync(reference, moving, parameters);
        using AlgorithmResult second = await RunAsync(reference, moving, parameters);

        Assert.Equal(AlgorithmResultStatus.Succeeded, first.Status);
        Assert.Equal(AlgorithmResultStatus.Succeeded, second.Status);
        double[] firstMatrix = ReadMatrix(first.GetArtifact<AlgorithmTableArtifact>("image-registration-matrix")!, "M");
        double[] secondMatrix = ReadMatrix(second.GetArtifact<AlgorithmTableArtifact>("image-registration-matrix")!, "M");
        Assert.Equal(firstMatrix, secondMatrix);
        double[] expected = Invert(referenceToMoving);
        foreach (AlgorithmPoint point in new[] { new AlgorithmPoint(40, 40), new AlgorithmPoint(150, 100), new AlgorithmPoint(260, 190) })
        {
            AlgorithmPoint expectedPoint = Transform(expected, point);
            AlgorithmPoint actualPoint = Transform(firstMatrix, point);
            Assert.InRange(actualPoint.X, expectedPoint.X - 1.5, expectedPoint.X + 1.5);
            Assert.InRange(actualPoint.Y, expectedPoint.Y - 1.5, expectedPoint.Y + 1.5);
        }
        Assert.True(Measurement(first, "registration.inlier_count") >= 6);
        Assert.InRange(Measurement(first, "registration.geometric_rmse"), 0, 3);
        Assert.InRange(Measurement(first, "registration.confidence"), 0, 1);
        Assert.Equal(reference.Width, Image(first, "registered-image").Width);
        Assert.Equal(reference.Height, Image(first, "registered-image").Height);
    }

    [Theory]
    [InlineData(100_000, 40, 5_000)]
    [InlineData(2_000, 200, 1_000_000)]
    public async Task OrbRejectsUnboundedFeatureOrConsensusWorkBeforeNativeExecution(
        int maximumFeatures,
        int maximumConsensusMatches,
        int maximumConsensusEvaluations)
    {
        using AlgorithmImageBuffer reference = FeaturePattern(96, 80);
        using AlgorithmImageBuffer moving = reference.Clone();
        List<string> stages = [];
        using AlgorithmResult result = await RunAsync(reference, moving, new ImageRegistrationParameters
        {
            Method = ImageRegistrationMethod.OrbHomography,
            MaximumFeatures = maximumFeatures,
            MaximumConsensusMatches = maximumConsensusMatches,
            MaximumConsensusEvaluations = maximumConsensusEvaluations,
            MinimumMatchCount = 4,
            MinimumInlierCount = 4,
        }, progress: new InlineProgress(value => stages.Add(value.Stage)));

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "registration_work_budget_exceeded");
        Assert.DoesNotContain("registration.features", stages);
        Assert.Empty(result.Artifacts);
    }

    [Fact]
    public async Task OrbMatchingCanCancelBetweenBoundedDescriptorBatches()
    {
        using AlgorithmImageBuffer reference = FeaturePattern(640, 480);
        using AlgorithmImageBuffer moving = CircularShift(reference, 3, -2);
        using CancellationTokenSource cancellation = new();
        List<string> stages = [];
        using AlgorithmResult result = await RunAsync(reference, moving, new ImageRegistrationParameters
        {
            Method = ImageRegistrationMethod.OrbHomography,
            MaximumFeatures = 4_000,
            MinimumMatchCount = 4,
            MinimumInlierCount = 4,
        }, cancellationToken: cancellation.Token, progress: new InlineProgress(value =>
        {
            stages.Add(value.Stage);
            if (value.Stage == "registration.match.forward") cancellation.Cancel();
        }));

        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.Contains("registration.match.forward", stages);
        Assert.DoesNotContain("registration.consensus", stages);
    }

    [Fact]
    public void OrbConsensusDoesNotForceTheFirstDistanceRankedOutlierIntoEveryCandidate()
    {
        const double dx = 7;
        const double dy = -4;
        List<ImageRegistrationAlgorithmProvider.RegistrationMatch> matches =
        [
            new(0, new Point2d(12, 12), new Point2d(200, 5), 0, false, double.NaN),
        ];
        int index = 1;
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
        {
            Point2d moving = new(20 + x * 30, 20 + y * 25);
            matches.Add(new(index++, moving, new Point2d(moving.X + dx, moving.Y + dy), index, false, double.NaN));
        }
        ImageRegistrationParameters parameters = new()
        {
            Method = ImageRegistrationMethod.OrbHomography,
            MaximumConsensusMatches = matches.Count,
            MaximumConsensusEvaluations = 128,
            ConsensusReprojectionThresholdPixels = 0.01,
            MinimumMatchCount = 4,
            MinimumInlierCount = 4,
        };

        ImageRegistrationAlgorithmProvider.RegistrationConsensus? consensus =
            ImageRegistrationAlgorithmProvider.FindConsensus(matches.ToArray(), parameters, CancellationToken.None, null);

        Assert.NotNull(consensus);
        Assert.Equal(16, consensus.InlierCount);
        Assert.InRange(consensus.Matrix[2], dx - 1e-6, dx + 1e-6);
        Assert.InRange(consensus.Matrix[5], dy - 1e-6, dy + 1e-6);

        ImageRegistrationAlgorithmProvider.RegistrationConsensus? reordered =
            ImageRegistrationAlgorithmProvider.FindConsensus(matches.AsEnumerable().Reverse().ToArray(), parameters, CancellationToken.None, null);
        Assert.NotNull(reordered);
        Assert.Equal(16, reordered.InlierCount);
        Assert.InRange(reordered.Matrix[2], dx - 1e-6, dx + 1e-6);
        Assert.InRange(reordered.Matrix[5], dy - 1e-6, dy + 1e-6);
    }

    [Fact]
    public void OrbConsensusSamplingIsDeterministicUniqueAndCoversEveryRank()
    {
        IReadOnlyList<(int A, int B, int C, int D)> first = ImageRegistrationAlgorithmProvider.BuildConsensusSamples(40, 5_000);
        IReadOnlyList<(int A, int B, int C, int D)> second = ImageRegistrationAlgorithmProvider.BuildConsensusSamples(40, 5_000);

        Assert.Equal(5_000, first.Count);
        Assert.Equal(first, second);
        Assert.Equal(first.Count, first.Distinct().Count());
        Assert.Contains(first, sample => sample.A != 0 && sample.B != 0 && sample.C != 0 && sample.D != 0);
        Assert.All(Enumerable.Range(0, 40), rank => Assert.Contains(first, sample =>
            sample.A == rank || sample.B == rank || sample.C == rank || sample.D == rank));
    }

    [Fact]
    public async Task UnsupportedNamesFormatsColorSpaceDimensionsRoiAndNonfiniteInputAreStructuredFailures()
    {
        using AlgorithmImageBuffer gray = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer gray2 = gray.Clone();
        using AlgorithmResult names = await RunAsync(gray, gray2, inputNames: ("left", "right"));
        Assert.Contains(names.Failures, failure => failure.Code == "invalid_input_names");

        using AlgorithmImageBuffer gray3 = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer color = Pattern(32, 24, AlgorithmImageFormat.Bgr24);
        using AlgorithmResult format = await RunAsync(gray3, color);
        Assert.Contains(format.Failures, failure => failure.Code == "format_mismatch");

        using AlgorithmImageBuffer gray4 = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer gray5 = gray4.Clone();
        using AlgorithmResult colorSpace = await RunAsync(gray4, gray5, colorSpaces: ("linear", "encoded"));
        Assert.Contains(colorSpace.Failures, failure => failure.Code == "color_space_mismatch");

        using AlgorithmImageBuffer gray6 = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer different = Pattern(31, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmResult dimensions = await RunAsync(gray6, different);
        Assert.Contains(dimensions.Failures, failure => failure.Code == "phase_dimension_mismatch");

        using AlgorithmImageBuffer gray7 = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer gray8 = gray7.Clone();
        using AlgorithmResult roi = await RunAsync(gray7, gray8, roi: new RectangleAlgorithmRoi(0, 0, 10, 10));
        Assert.Contains(roi.Failures, failure => failure.Code == "roi_kind_unsupported");

        using AlgorithmImageBuffer finite = Pattern(32, 24, AlgorithmImageFormat.Gray32Float);
        byte[] nonfiniteBytes = finite.Data.ToArray();
        BinaryPrimitives.WriteSingleLittleEndian(nonfiniteBytes.AsSpan(0, 4), float.NaN);
        using AlgorithmImageBuffer nonfinite = new(32, 24, 32 * sizeof(float), AlgorithmImageFormat.Gray32Float, nonfiniteBytes);
        using AlgorithmResult nan = await RunAsync(finite, nonfinite);
        Assert.Contains(nan.Failures, failure => failure.Code == "registration_nonfinite_input");
    }

    [Fact]
    public async Task CancellationAndTransferredOwnershipReleaseInputsAndResultArtifacts()
    {
        using AlgorithmImageBuffer cancelledReference = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer cancelledMoving = cancelledReference.Clone();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using AlgorithmResult cancelled = await RunAsync(cancelledReference, cancelledMoving, cancellationToken: cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);

        AlgorithmImageBuffer reference = Pattern(64, 48, AlgorithmImageFormat.Gray8);
        AlgorithmImageBuffer moving = reference.Clone();
        AlgorithmResult result = await RunAsync(reference, moving, transferred: true);
        Assert.True(reference.IsDisposed);
        Assert.True(moving.IsDisposed);
        AlgorithmImageBuffer[] outputs = result.Artifacts.OfType<AlgorithmImageArtifact>().Select(artifact => artifact.Image).ToArray();
        Assert.All(outputs, output => Assert.False(output.IsDisposed));
        result.Dispose();
        Assert.All(outputs, output => Assert.True(output.IsDisposed));

        AlgorithmImageBuffer interruptedReference = Pattern(192, 128, AlgorithmImageFormat.Gray8);
        AlgorithmImageBuffer interruptedMoving = interruptedReference.Clone();
        using CancellationTokenSource interruptedCancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "registration.warp") interruptedCancellation.Cancel();
        });
        using AlgorithmResult interrupted = await RunAsync(
            interruptedReference,
            interruptedMoving,
            transferred: true,
            cancellationToken: interruptedCancellation.Token,
            progress: progress);
        Assert.Equal(AlgorithmResultStatus.Cancelled, interrupted.Status);
        Assert.True(interruptedReference.IsDisposed);
        Assert.True(interruptedMoving.IsDisposed);

        AlgorithmImageBuffer failedReference = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        AlgorithmImageBuffer failedMoving = Pattern(32, 24, AlgorithmImageFormat.Bgr24);
        using AlgorithmResult failed = await RunAsync(failedReference, failedMoving, transferred: true);
        Assert.Contains(failed.Failures, failure => failure.Code == "format_mismatch");
        Assert.True(failedReference.IsDisposed);
        Assert.True(failedMoving.IsDisposed);
    }

    [Fact]
    public async Task LocalFlowPairAdapterUsesTheSameMultiInputInvocation()
    {
        LocalFrameMetadata metadata = new() { Width = 32, Height = 24, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
        using LocalFlowFrame referenceFrame = LocalFlowFrame.Allocate(metadata, 32 * 24, 0);
        using LocalFlowFrame movingFrame = LocalFlowFrame.Allocate(metadata, 32 * 24, 0);
        using LocalFlowFrameLease reference = referenceFrame.Acquire();
        using LocalFlowFrameLease moving = movingFrame.Acquire();
        byte[] pixels = Enumerable.Range(0, 32 * 24).Select(index => (byte)((index * 29 + index / 32 * 17) & 0xff)).ToArray();
        Marshal.Copy(pixels, 0, reference.RawPointer, pixels.Length);
        Marshal.Copy(pixels, 0, moving.RawPointer, pixels.Length);
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageRegistration, new ImageRegistrationParameters { MinimumPhaseResponse = 0 });
        using AlgorithmResult result = await LocalFlowImageAlgorithmAdapter.ExecuteRawPairAsync(reference, moving, invocation);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(32, Image(result, "registered-image").Width);
        Assert.Equal(AlgorithmImageFormat.Gray8, Image(result, "valid-region-mask").Format);
    }

    [Fact]
    public async Task ImageViewCatalogEntryAndResultWindowExposeRegistrationArtifactsAndReleaseOwnership()
    {
        using AlgorithmImageBuffer reference = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmImageBuffer moving = reference.Clone();
        AlgorithmResult result = await RunAsync(reference, moving);
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView view = new();
            WriteableBitmap bitmap = new(32, 24, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 32, 24), reference.Data.ToArray(), 32, 0);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                string?[] ids = new AlgorithmsContextMenu(imageView.EditorContext.ProcessingContext)
                    .GetContextMenuItems().Select(value => value.GuidId).ToArray();
                Assert.Single(ids, value => value == "ImageRegistration");

                ImageProcessingContext context = imageView.EditorContext.ProcessingContext;
                ImageRegistrationResultWindow window = new(result, "moving.png", context, imageView.EditorContext.DrawEditorContext);
                window.Show();
                System.Windows.Controls.Image registered = Assert.IsType<System.Windows.Controls.Image>(window.FindName("RegisteredPreview"));
                System.Windows.Controls.Image mask = Assert.IsType<System.Windows.Controls.Image>(window.FindName("MaskPreview"));
                DataGrid matrix = Assert.IsType<DataGrid>(window.FindName("MatrixGrid"));
                DataGrid matches = Assert.IsType<DataGrid>(window.FindName("MatchesGrid"));
                Assert.Equal(32, Assert.IsAssignableFrom<BitmapSource>(registered.Source).PixelWidth);
                Assert.Equal(PixelFormats.Gray8, Assert.IsAssignableFrom<BitmapSource>(mask.Source).Format);
                Assert.NotNull(matrix.ItemsSource);
                Assert.NotNull(matches.ItemsSource);
                Assert.Single(context.SnapshotAlgorithmOverlayRegistrations());
                window.Close();
                Assert.True(result.IsDisposed);
                Assert.Empty(context.SnapshotAlgorithmOverlayRegistrations());
            });
        }
        finally
        {
            result.Dispose();
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public async Task StructuredRegistrationResultExportsAsVersionedJsonWithoutOverwriting()
    {
        using AlgorithmImageBuffer reference = Pattern(24, 16, AlgorithmImageFormat.Gray16);
        using AlgorithmImageBuffer moving = reference.Clone();
        using AlgorithmResult result = await RunAsync(reference, moving);
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-registration-{Guid.NewGuid():N}.json");
        try
        {
            Assert.Equal(path, AlgorithmResultExporter.ExportJson(result, path));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(StandardAlgorithmIds.ImageRegistration.ToString(), document.RootElement.GetProperty("algorithmId").GetString());
            Assert.Contains("image-registration", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Throws<IOException>(() => AlgorithmResultExporter.ExportJson(result, path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<AlgorithmResult> RunAsync(
        AlgorithmImageBuffer reference,
        AlgorithmImageBuffer moving,
        ImageRegistrationParameters? parameters = null,
        (string Reference, string Moving)? inputNames = null,
        (string Reference, string Moving)? colorSpaces = null,
        AlgorithmRoi? roi = null,
        bool transferred = false,
        CancellationToken cancellationToken = default,
        IProgress<AlgorithmProgress>? progress = null)
    {
        parameters ??= new ImageRegistrationParameters { MinimumPhaseResponse = 0 };
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageRegistration, parameters, roi);
        return await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs =
            [
                new AlgorithmInput
                {
                    Name = inputNames?.Reference ?? "reference",
                    Image = reference,
                    Ownership = transferred ? AlgorithmInputOwnership.Transferred : AlgorithmInputOwnership.Borrowed,
                    ColorSpace = colorSpaces?.Reference ?? "encoded-device-values",
                },
                new AlgorithmInput
                {
                    Name = inputNames?.Moving ?? "moving",
                    Image = moving,
                    Ownership = transferred ? AlgorithmInputOwnership.Transferred : AlgorithmInputOwnership.Borrowed,
                    ColorSpace = colorSpaces?.Moving ?? "encoded-device-values",
                },
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
            Progress = progress,
        }, cancellationToken);
    }

    private static AlgorithmImageBuffer Pattern(int width, int height, AlgorithmImageFormat format, double dpiX = 96, double dpiY = 96)
    {
        int stride = width * format.BytesPerPixel();
        byte[] data = new byte[stride * height];
        int channels = format.Channels();
        int bytes = format.BitsPerChannel() / 8;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        for (int channel = 0; channel < channels; channel++)
        {
            uint code = unchecked((uint)(x * 73856093 ^ y * 19349663 ^ channel * 83492791));
            int offset = y * stride + (x * channels + channel) * bytes;
            if (bytes == 1) data[offset] = (byte)(code % 251);
            else if (bytes == 2) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), (ushort)(code % 65521));
            else BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset, 4), (code % 10000) / 9999f);
        }
        return new AlgorithmImageBuffer(width, height, stride, format, data, dpiX, dpiY);
    }

    private static AlgorithmImageBuffer CircularShift(AlgorithmImageBuffer source, int dx, int dy)
    {
        byte[] result = new byte[source.Data.Length];
        int pixelBytes = source.Format.BytesPerPixel();
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < source.Width; x++)
        {
            int targetX = (x + dx + source.Width) % source.Width;
            int targetY = (y + dy + source.Height) % source.Height;
            source.Data.Span.Slice(y * source.Stride + x * pixelBytes, pixelBytes)
                .CopyTo(result.AsSpan(targetY * source.Stride + targetX * pixelBytes, pixelBytes));
        }
        return new AlgorithmImageBuffer(source.Width, source.Height, source.Stride, source.Format, result, source.DpiX, source.DpiY);
    }

    private static AlgorithmImageBuffer SubpixelCircularShift(AlgorithmImageBuffer source, double dx, double dy, int noiseSeed)
    {
        using Mat input = Mat.FromPixelData(source.Height, source.Width, MatType.CV_8UC1, source.Data.ToArray());
        using Mat transform = Mat.FromArray(new double[,] { { 1, 0, dx }, { 0, 1, dy } });
        using Mat output = new();
        Cv2.WarpAffine(input, output, transform, input.Size(), InterpolationFlags.Linear, BorderTypes.Wrap);
        byte[] bytes = new byte[source.Width * source.Height];
        Marshal.Copy(output.Data, bytes, 0, bytes.Length);
        Random random = new(noiseSeed);
        for (int index = 0; index < bytes.Length; index++)
            bytes[index] = (byte)Math.Clamp(bytes[index] + random.Next(-2, 3), byte.MinValue, byte.MaxValue);
        return new AlgorithmImageBuffer(source.Width, source.Height, source.Width, AlgorithmImageFormat.Gray8, bytes, source.DpiX, source.DpiY);
    }

    private static AlgorithmImageBuffer FeaturePattern(int width, int height)
    {
        byte[] data = new byte[width * height];
        Random random = new(123456);
        random.NextBytes(data);
        using Mat mat = Mat.FromPixelData(height, width, MatType.CV_8UC1, data);
        for (int index = 0; index < 40; index++)
        {
            OpenCvSharp.Point center = new(15 + index * 47 % (width - 30), 15 + index * 71 % (height - 30));
            Cv2.Circle(mat, center, 4 + index % 7, Scalar.All(index % 2 == 0 ? 255 : 0), 2);
            Cv2.PutText(mat, (index % 10).ToString(), center, HersheyFonts.HersheySimplex, 0.35, Scalar.All(255), 1);
        }
        return new AlgorithmImageBuffer(width, height, width, AlgorithmImageFormat.Gray8, data);
    }

    private static AlgorithmImageBuffer Warp(AlgorithmImageBuffer source, double[] matrix, int width, int height)
    {
        using Mat input = Mat.FromPixelData(source.Height, source.Width, MatType.CV_8UC1, source.Data.ToArray());
        using Mat transform = new(3, 3, MatType.CV_64FC1);
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
                transform.Set(row, column, matrix[row * 3 + column]);
        using Mat output = new();
        Cv2.WarpPerspective(input, output, transform, new OpenCvSharp.Size(width, height), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        byte[] bytes = new byte[width * height];
        System.Runtime.InteropServices.Marshal.Copy(output.Data, bytes, 0, bytes.Length);
        return new AlgorithmImageBuffer(width, height, width, AlgorithmImageFormat.Gray8, bytes);
    }

    private static AlgorithmImageBuffer Image(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmImageArtifact>(name)!.Image;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("image-registration-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static double[] ReadMatrix(AlgorithmTableArtifact table, string prefix)
        => table.Rows.SelectMany(row => Enumerable.Range(1, 3).Select(column => row[$"{prefix}{column}"].GetDouble())).ToArray();

    private static AlgorithmPoint Transform(double[] matrix, AlgorithmPoint point)
    {
        double denominator = matrix[6] * point.X + matrix[7] * point.Y + matrix[8];
        return new AlgorithmPoint(
            (matrix[0] * point.X + matrix[1] * point.Y + matrix[2]) / denominator,
            (matrix[3] * point.X + matrix[4] * point.Y + matrix[5]) / denominator);
    }

    private static double[] Invert(double[] matrix)
    {
        double determinant = matrix[0] * (matrix[4] * matrix[8] - matrix[5] * matrix[7])
            - matrix[1] * (matrix[3] * matrix[8] - matrix[5] * matrix[6])
            + matrix[2] * (matrix[3] * matrix[7] - matrix[4] * matrix[6]);
        return
        [
            (matrix[4] * matrix[8] - matrix[5] * matrix[7]) / determinant,
            (matrix[2] * matrix[7] - matrix[1] * matrix[8]) / determinant,
            (matrix[1] * matrix[5] - matrix[2] * matrix[4]) / determinant,
            (matrix[5] * matrix[6] - matrix[3] * matrix[8]) / determinant,
            (matrix[0] * matrix[8] - matrix[2] * matrix[6]) / determinant,
            (matrix[2] * matrix[3] - matrix[0] * matrix[5]) / determinant,
            (matrix[3] * matrix[7] - matrix[4] * matrix[6]) / determinant,
            (matrix[1] * matrix[6] - matrix[0] * matrix[7]) / determinant,
            (matrix[0] * matrix[4] - matrix[1] * matrix[3]) / determinant,
        ];
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
