using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.UI.Menus;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using OpenCvSharp;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmRuntimeIntegrationTests
{
    [Fact]
    public async Task CustomRuntimeMenuProjectionExecutesItsProviderAndPreservesMeasurementResult()
    {
        AlgorithmDescriptor descriptor = Descriptor(
            "test.runtime-menu",
            interactive: true,
            batch: false,
            semantics: AlgorithmResultSemantics.Analysis);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        int executions = 0;
        TestProvider provider = new(descriptor.Id, _ =>
        {
            Interlocked.Increment(ref executions);
            return new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts =
                [
                    new AlgorithmImageArtifact("heatmap", "visualization", Buffer(99)),
                    new AlgorithmMeasurementArtifact("measurements", [new AlgorithmMeasurement("mean", 12.5)]),
                ],
            };
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            double? presentedMean = null;
            AlgorithmsContextMenu menu = WpfTestHost.Invoke(() => new AlgorithmsContextMenu(
                context,
                runtime,
                (result, _) => presentedMean = result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements.Single().Value));

            Assert.Single(WpfTestHost.Invoke(() => menu.GetContextMenuItems()), item => item.GuidId == "RuntimeMenu");
            AlgorithmResultStatus status = await WpfTestHost.Invoke(() => menu.ExecuteCatalogDefaultAsync(descriptor));

            Assert.Equal(AlgorithmResultStatus.Succeeded, status);
            Assert.Equal(1, executions);
            Assert.Equal(12.5, presentedMean);
            Assert.Equal(revision, WpfTestHost.Invoke(() => context.ImageRevision));
            Assert.Equal((byte)3, WpfTestHost.Invoke(() => Pixel(context.ViewBitmapSource)));
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task AnalysisWithPrimaryImageCommitsItAndStillPresentsStructuredArtifacts()
    {
        AlgorithmDescriptor descriptor = Descriptor(
            "test.runtime-analysis-primary",
            interactive: true,
            batch: false,
            semantics: AlgorithmResultSemantics.Analysis);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TestProvider provider = new(descriptor.Id, _ => new AlgorithmResult
        {
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                new AlgorithmImageArtifact("output", "primary", Buffer(61)),
                new AlgorithmMeasurementArtifact("measurements", [new AlgorithmMeasurement("score", 4.25)]),
            ],
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            AlgorithmMeasurementArtifact? presented = null;
            AlgorithmsContextMenu menu = WpfTestHost.Invoke(() => new AlgorithmsContextMenu(
                context,
                runtime,
                (result, _) => presented = result.GetArtifact<AlgorithmMeasurementArtifact>()));

            AlgorithmResultStatus status = await WpfTestHost.Invoke(() => menu.ExecuteCatalogDefaultAsync(descriptor));

            Assert.Equal(AlgorithmResultStatus.Succeeded, status);
            Assert.Equal(revision + 1, WpfTestHost.Invoke(() => context.ImageRevision));
            Assert.Equal((byte)61, WpfTestHost.Invoke(() => Pixel(context.ViewBitmapSource)));
            Assert.NotNull(presented);
            Assert.Equal(4.25, Assert.Single(presented.Measurements).Value);
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void ActualBatchMenuWindowUsesTheImageContextRuntime()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.runtime-batch-window", interactive: false, batch: true);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new TestProvider(descriptor.Id, _ => PrimaryResult(44))], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        BatchImageProcessingWindow? captured = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                AlgorithmsContextMenu menu = new(
                    view.EditorContext.ProcessingContext,
                    runtime,
                    new DelegateAlgorithmAnalysisResultPresenter((_, _) => { }),
                    candidate => new BatchImageProcessingWindow(candidate),
                    window => captured = window);
                ColorVision.UI.Menus.MenuItemMetadata batch = Assert.Single(
                    menu.GetContextMenuItems(), item => item.GuidId == "BatchImageProcessing");
                batch.Command!.Execute(null);

                Assert.NotNull(captured);
                BatchImageAlgorithmDefinition projected = Assert.Single(captured.Algorithms.Skip(1));
                Assert.Equal(descriptor.Id, projected.Descriptor!.Id);
                using Mat source = new(1, 1, MatType.CV_8UC1, Scalar.All(1));
                using Mat output = projected.Apply(source);
                Assert.Equal((byte)44, output.At<byte>(0, 0));
            });
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                captured?.Close();
                view.Dispose();
            });
        }
    }

    [Fact]
    public void ActualEditorToolFactoryOnlyCreatesSpecializedMenusExecutableByItsRuntime()
    {
        AlgorithmDescriptor descriptor = StandardAlgorithmCatalog.Create().Descriptors.Single(
            value => value.Id == StandardAlgorithmIds.RoiStatistics);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new TestProvider(descriptor.Id, _ => new AlgorithmResult
        {
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts = [new AlgorithmMeasurementArtifact("measurements", [new AlgorithmMeasurement("mean", 1)])],
        })], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                object[] specialized = view.IEditorToolFactory.IIEditorToolContextMenus
                    .Cast<object>()
                    .Concat(view.IEditorToolFactory.ContextMenuProviders)
                    .Where(value => value is IAlgorithmCatalogBoundMenu)
                    .ToArray();

                Assert.NotEmpty(specialized);
                Assert.All(specialized, value => Assert.Equal(
                    StandardAlgorithmIds.RoiStatistics,
                    Assert.IsAssignableFrom<IAlgorithmCatalogBoundMenu>(value).AlgorithmId));
                Assert.DoesNotContain(specialized, value =>
                    Assert.IsAssignableFrom<IAlgorithmCatalogBoundMenu>(value).AlgorithmId == StandardAlgorithmIds.ImageProfile);
                Assert.DoesNotContain(specialized, value =>
                    Assert.IsAssignableFrom<IAlgorithmCatalogBoundMenu>(value).AlgorithmId == StandardAlgorithmIds.ImageComparison);
                Assert.DoesNotContain(specialized, value =>
                    Assert.IsAssignableFrom<IAlgorithmCatalogBoundMenu>(value).AlgorithmId == StandardAlgorithmIds.BlobComponents);
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task GenericMenuSelectsTheUniquePrimaryImageRatherThanTheFirstImage()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.runtime-primary", interactive: true, batch: false);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TestProvider provider = new(descriptor.Id, _ => new AlgorithmResult
        {
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                new AlgorithmImageArtifact("visualization", "visualization", Buffer(11)),
                new AlgorithmImageArtifact("output", "primary", Buffer(22)),
            ],
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            AlgorithmsContextMenu menu = WpfTestHost.Invoke(() => new AlgorithmsContextMenu(context, runtime, (_, _) => { }));

            AlgorithmResultStatus status = await WpfTestHost.Invoke(() => menu.ExecuteCatalogDefaultAsync(descriptor));

            Assert.Equal(AlgorithmResultStatus.Succeeded, status);
            Assert.Equal(revision + 1, WpfTestHost.Invoke(() => context.ImageRevision));
            Assert.Equal((byte)22, WpfTestHost.Invoke(() => Pixel(context.ViewBitmapSource)));
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task GenericImageViewPreviewAndApplyRequireLocalAndCannotSelectHigherPriorityRemoteProvider()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.runtime-local-plane", interactive: true, batch: false);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        PlaneProvider remote = new(
            descriptor.Id,
            "remote-high",
            AlgorithmExecutionPlane.RemoteDevice,
            priority: 100,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            output: 91);
        PlaneProvider local = new(
            descriptor.Id,
            "local-low",
            AlgorithmExecutionPlane.Local,
            priority: 1,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            output: 47);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [remote, local], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        ImageAlgorithmPreviewSession? previewSession = null;
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            using AlgorithmResult result = await WpfTestHost.Invoke(() => ImageAlgorithmApplier.ApplyAsync(
                context,
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters())));

            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.Equal("local-low", result.Diagnostics.ProviderId);
            Assert.Equal(0, remote.Executions);
            Assert.Equal(1, local.Executions);
            Assert.Equal((byte)47, WpfTestHost.Invoke(() => Pixel(context.ViewBitmapSource)));

            previewSession = WpfTestHost.Invoke(() => ImageAlgorithmPreviewSession.Start(context));
            using AlgorithmResult preview = await previewSession.PreviewAsync(
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()),
                AlgorithmHostCapabilities.Interactive);
            Assert.Equal(AlgorithmResultStatus.Succeeded, preview.Status);
            Assert.Equal("local-low", preview.Diagnostics.ProviderId);
            Assert.Equal(0, remote.Executions);
            Assert.Equal(2, local.Executions);
        }
        finally
        {
            if (previewSession != null) WpfTestHost.Invoke(previewSession.Dispose);
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task ExplicitRemoteExecutionPlaneRemainsAvailableAndSelectableForARemoteHost()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.runtime-remote-plane", interactive: true, batch: false) with
        {
            Capabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.RemoteDevice,
        };
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        PlaneProvider remote = new(
            descriptor.Id,
            "remote-only",
            AlgorithmExecutionPlane.RemoteDevice,
            priority: 10,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.RemoteDevice,
            output: 89);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [remote], scheduler);

        Assert.True(runtime.CanExecuteDescriptor(
            descriptor,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.RemoteDevice));
        using AlgorithmImageBuffer input = Buffer(3);
        using AlgorithmResult result = await runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input }],
            RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.RemoteDevice,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal("remote-only", result.Diagnostics.ProviderId);
        Assert.Equal(1, remote.Executions);
    }

    [Fact]
    public void LegacyProviderWithoutDescriptorDeclarationIsFailClosedForProjectionWithoutSyntheticInputProbes()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.legacy-descriptor-availability", interactive: true, batch: false);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        MetadataSensitiveLegacyProvider provider = new(descriptor.Id);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);

        Assert.False(runtime.CanExecuteDescriptor(
            descriptor,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local));
        Assert.Equal(0, provider.InputAwareProbes);
    }

    [Fact]
    public async Task LegacyProviderUsesRealInvocationInputsDuringRunnerSelection()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.legacy-descriptor-availability", interactive: true, batch: false);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        MetadataSensitiveLegacyProvider provider = new(descriptor.Id);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);

        using AlgorithmImageBuffer image = new(2, 1, 2, AlgorithmImageFormat.Gray8, [1, 2]);
        using AlgorithmResult result = await runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()),
            Inputs =
            [
                new AlgorithmInput
                {
                    Name = "reference",
                    SourceUri = "file:///fixture.png",
                    ColorSpace = "sRGB",
                    Image = image,
                },
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal("metadata-sensitive-legacy", result.Diagnostics.ProviderId);
        Assert.Equal(1, provider.InputAwareProbes);
    }

    [Fact]
    public void DescriptorAvailabilityAggregatesOnlyProvidersWithExplicitAlgorithmSupport()
    {
        AlgorithmCatalog standard = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor invert = standard.Descriptors.Single(value => value.Id == StandardAlgorithmIds.Invert);
        AlgorithmDescriptor canny = standard.Descriptors.Single(value => value.Id == StandardAlgorithmIds.Canny);
        AlgorithmCatalog catalog = new();
        catalog.Register(invert);
        catalog.Register(canny);
        MetadataSensitiveLegacyProvider legacyInvertOnly = new(invert.Id);
        AlgorithmHostCapabilities capabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local;
        using AlgorithmExecutionScheduler legacyScheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime legacyRuntime = new(catalog, [legacyInvertOnly], legacyScheduler);

        Assert.False(legacyRuntime.CanExecuteDescriptor(invert, capabilities));
        Assert.False(legacyRuntime.CanExecuteDescriptor(canny, capabilities));
        Assert.Equal(0, legacyInvertOnly.InputAwareProbes);

        using AlgorithmExecutionScheduler aggregateScheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime aggregateRuntime = new(
            catalog,
            [legacyInvertOnly, new CapabilityProvider(canny.Id, capabilities)],
            aggregateScheduler);
        Assert.False(aggregateRuntime.CanExecuteDescriptor(invert, capabilities));
        Assert.True(aggregateRuntime.CanExecuteDescriptor(canny, capabilities));
        Assert.Equal(0, legacyInvertOnly.InputAwareProbes);
    }

    [Fact]
    public async Task LegacyProviderIsHiddenFromDescriptorProjectionButRemainsInputAwareExecutionCompatible()
    {
        AlgorithmCatalog standard = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor invert = standard.Descriptors.Single(value => value.Id == StandardAlgorithmIds.Invert);
        AlgorithmDescriptor canny = standard.Descriptors.Single(value => value.Id == StandardAlgorithmIds.Canny);
        AlgorithmCatalog catalog = new();
        catalog.Register(invert);
        catalog.Register(canny);
        MetadataSensitiveLegacyProvider provider = new(invert.Id);
        AlgorithmHostCapabilities capabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local;
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);

        Assert.False(runtime.CanExecuteDescriptor(invert, capabilities));
        Assert.False(runtime.CanExecuteDescriptor(canny, capabilities));

        using AlgorithmImageBuffer invertInput = new(2, 1, 2, AlgorithmImageFormat.Gray8, [7, 9]);
        using AlgorithmResult invertResult = await runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(invert.Id, new NoAlgorithmParameters()),
            Inputs =
            [
                new AlgorithmInput
                {
                    Name = "reference",
                    SourceUri = "file:///fixture.png",
                    ColorSpace = "sRGB",
                    Image = invertInput,
                },
            ],
            RequiredCapabilities = capabilities,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, invertResult.Status);
        Assert.Equal("metadata-sensitive-legacy", invertResult.Diagnostics.ProviderId);
        Assert.Equal(1, provider.InputAwareProbes);

        using AlgorithmImageBuffer cannyInput = new(2, 1, 2, AlgorithmImageFormat.Gray8, [7, 9]);
        using AlgorithmResult cannyResult = await runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(canny.Id, new CannyParameters { LowThreshold = 100, HighThreshold = 200 }),
            Inputs =
            [
                new AlgorithmInput
                {
                    Name = "reference",
                    SourceUri = "file:///fixture.png",
                    ColorSpace = "sRGB",
                    Image = cannyInput,
                },
            ],
            RequiredCapabilities = capabilities,
        });

        Assert.Equal(AlgorithmResultStatus.Failed, cannyResult.Status);
        AlgorithmFailure failure = Assert.Single(cannyResult.Failures);
        Assert.Equal("provider_unavailable", failure.Code);
        Assert.Contains("provider_rejected", failure.Details!.Keys);
        Assert.DoesNotContain("provider_descriptor_contract_mismatch", failure.Details.Keys);
        Assert.Equal(2, provider.InputAwareProbes);
    }

    [Fact]
    public async Task ExplicitDescriptorMismatchFallsThroughToLowerPriorityLegacyInputProvider()
    {
        AlgorithmCatalog standard = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor invert = standard.Descriptors.Single(value => value.Id == StandardAlgorithmIds.Invert);
        AlgorithmCatalog catalog = new();
        catalog.Register(invert);
        AlgorithmHostCapabilities capabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local;
        PlaneProvider incompatibleHighPriority = new(
            StandardAlgorithmIds.Canny,
            "explicit-canny-only",
            AlgorithmExecutionPlane.Local,
            priority: 100,
            capabilities,
            output: 22);
        MetadataSensitiveLegacyProvider compatibleLegacy = new(invert.Id);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [incompatibleHighPriority, compatibleLegacy], scheduler);
        using AlgorithmImageBuffer input = new(2, 1, 2, AlgorithmImageFormat.Gray8, [7, 9]);

        using AlgorithmResult result = await runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(invert.Id, new NoAlgorithmParameters()),
            Inputs =
            [
                new AlgorithmInput
                {
                    Name = "reference",
                    SourceUri = "file:///fixture.png",
                    ColorSpace = "sRGB",
                    Image = input,
                },
            ],
            RequiredCapabilities = capabilities,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal("metadata-sensitive-legacy", result.Diagnostics.ProviderId);
        Assert.Equal(0, incompatibleHighPriority.Executions);
        Assert.Equal(1, compatibleLegacy.InputAwareProbes);
        Assert.Contains(result.Diagnostics.Messages, message =>
            message.Code == "provider_descriptor_contract_mismatch"
            && message.Message.Contains("explicit-canny-only", StringComparison.Ordinal));
    }

    [Fact]
    public void BuiltInProviderDescriptorAvailabilityDoesNotClaimUnknownAlgorithmIds()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.not-implemented-by-opencv", interactive: true, batch: false);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new OpenCvAlgorithmProvider()], scheduler);

        Assert.False(runtime.CanExecuteDescriptor(
            descriptor,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local));
    }

    [Fact]
    public async Task BuiltInProvidersRejectKnownIdsWithIncompatibleExecutionContractsBeforeParameterCasts()
    {
        AlgorithmCatalog canonical = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor canny = canonical.Descriptors.Single(value => value.Id == StandardAlgorithmIds.Canny);
        AlgorithmDescriptor incompatibleCanny = canny with
        {
            ParameterType = typeof(NoAlgorithmParameters),
            ParameterSchema = new AlgorithmParameterSchema(
                canny.ParameterSchema.Version,
                Array.Empty<AlgorithmParameterField>(),
                AlgorithmJson.ToElement(new NoAlgorithmParameters())),
        };
        AlgorithmDescriptor roi = canonical.Descriptors.Single(value => value.Id == StandardAlgorithmIds.RoiStatistics);
        AlgorithmDescriptor incompatibleRoi = roi with
        {
            ResultSemantics = AlgorithmResultSemantics.ImageTransform,
            MinimumInputCount = 2,
            MaximumInputCount = 2,
        };
        OpenCvAlgorithmProvider openCv = new();
        RoiStatisticsAlgorithmProvider roiProvider = new();

        Assert.False(openCv.CanExecuteDescriptor(incompatibleCanny, out string? cannyReason));
        Assert.Equal("descriptor_contract_incompatible", cannyReason);
        Assert.False(roiProvider.CanExecuteDescriptor(incompatibleRoi, out string? roiReason));
        Assert.Equal("descriptor_contract_incompatible", roiReason);

        AlgorithmCatalog mutatedCatalog = new();
        mutatedCatalog.Register(incompatibleCanny);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(mutatedCatalog, [openCv], scheduler);
        using AlgorithmImageBuffer input = Buffer(7);
        using AlgorithmResult result = await runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(incompatibleCanny.Id, new NoAlgorithmParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input }],
            RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "provider_unavailable");
    }

    [Fact]
    public void EveryBuiltInProviderRejectsDifferentMajorAndChangedExecutionShapeForEveryImplementedDescriptor()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        IReadOnlyList<IImageAlgorithmProvider> providers = ImageAlgorithmPlatform.Runtime.ProviderRegistry.Providers;
        Assert.Equal(16, providers.Count);

        foreach (IImageAlgorithmProvider provider in providers)
        {
            AlgorithmDescriptor[] implemented = catalog.Descriptors
                .Where(descriptor => provider.CanExecuteDescriptor(descriptor, out _))
                .ToArray();
            Assert.NotEmpty(implemented);
            foreach (AlgorithmDescriptor descriptor in implemented)
            {
                AlgorithmDescriptor differentMajor = descriptor with
                {
                    Version = new AlgorithmVersion(descriptor.Version.Major + 1, 0, 0),
                };
                AlgorithmDescriptor changedShape = descriptor with
                {
                    ResultSemantics = descriptor.ResultSemantics == AlgorithmResultSemantics.Analysis
                        ? AlgorithmResultSemantics.ImageTransform
                        : AlgorithmResultSemantics.Analysis,
                };

                Assert.False(provider.CanExecuteDescriptor(differentMajor, out string? majorReason),
                    $"{provider.Metadata.ProviderId} accepted {differentMajor.Id} major {differentMajor.Version}.");
                Assert.Equal("descriptor_contract_incompatible", majorReason);
                Assert.False(provider.CanExecuteDescriptor(changedShape, out string? shapeReason),
                    $"{provider.Metadata.ProviderId} accepted a changed execution shape for {changedShape.Id}.");
                Assert.Equal("descriptor_contract_incompatible", shapeReason);
            }
        }
    }

    [Fact]
    public void InteractiveProjectionRequiresDescriptorSpecificMultiInputAndRoiCapabilities()
    {
        AlgorithmCatalog standard = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor registration = standard.Descriptors.Single(value => value.Id == StandardAlgorithmIds.ImageRegistration);
        AlgorithmDescriptor roi = standard.Descriptors.Single(value => value.Id == StandardAlgorithmIds.RoiStatistics);

        Assert.False(IsProjectedMenuVisible(registration, AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local));
        Assert.True(IsProjectedMenuVisible(registration,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput));
        Assert.False(IsCatalogBoundToolVisible(roi, AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local));
        Assert.True(IsCatalogBoundToolVisible(roi,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi));
    }

    [Fact]
    public void DefaultRuntimeProjectsImageComparisonWholeImageAndEveryRoiCommand()
    {
        ImageView view = CreateImageView(ImageAlgorithmPlatform.Runtime, 3);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                IIEditorToolContextMenu menu = Assert.Single(
                    view.IEditorToolFactory.IIEditorToolContextMenus,
                    item => item is IAlgorithmCatalogBoundMenu bound
                        && bound.AlgorithmId == StandardAlgorithmIds.ImageComparison);
                List<MenuItemMetadata> items = menu.GetContextMenuItems();

                Assert.True(Assert.Single(items, item => item.GuidId == "ImageComparisonWhole").Command!.CanExecute(null));
                Assert.True(Assert.Single(items, item => item.GuidId == "ImageComparisonRectangle").Command!.CanExecute(null));
                Assert.True(Assert.Single(items, item => item.GuidId == "ImageComparisonCircle").Command!.CanExecute(null));
                Assert.True(Assert.Single(items, item => item.GuidId == "ImageComparisonPolygon").Command!.CanExecute(null));
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void GenericFallbackDoesNotProjectAMultiInputDescriptorWithoutAnInputCollector()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.generic-multi-input", interactive: true, batch: false) with
        {
            Capabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
            MinimumInputCount = 2,
            MaximumInputCount = 2,
        };

        Assert.False(IsProjectedMenuVisible(
            descriptor,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput));
    }

    [Fact]
    public void WholeImageCommandsRemainVisibleWithoutRoiWhileRoiCommandsAreDisabled()
    {
        AlgorithmCatalog source = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor[] descriptors = source.Descriptors
            .Where(value => value.Id == StandardAlgorithmIds.ImageComparison
                || value.Id == StandardAlgorithmIds.BlobComponents
                || value.Id == StandardAlgorithmIds.Contours)
            .ToArray();
        AlgorithmCatalog catalog = new();
        foreach (AlgorithmDescriptor descriptor in descriptors) catalog.Register(descriptor);
        AlgorithmHostCapabilities capabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local
            | AlgorithmHostCapabilities.MultiInput;
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new MultiIdCapabilityProvider(
            descriptors.Select(value => value.Id).ToHashSet(), "whole-only", 10, capabilities, 7)], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                IAlgorithmCatalogBoundMenu[] menus = view.IEditorToolFactory.IIEditorToolContextMenus
                    .OfType<IAlgorithmCatalogBoundMenu>()
                    .Where(value => descriptors.Any(descriptor => descriptor.Id == value.AlgorithmId))
                    .ToArray();
                Assert.Equal(3, menus.Length);
                foreach (IAlgorithmCatalogBoundMenu menu in menus)
                {
                    List<MenuItemMetadata> items = Assert.IsAssignableFrom<IIEditorToolContextMenu>(menu).GetContextMenuItems();
                    MenuItemMetadata whole = Assert.Single(items, item => item.GuidId.EndsWith("Whole", StringComparison.Ordinal)
                        || item.GuidId.EndsWith("WholeImage", StringComparison.Ordinal));
                    Assert.True(whole.Command!.CanExecute(null));
                    foreach (MenuItemMetadata roi in items.Where(item => item.GuidId.Contains("Rectangle", StringComparison.Ordinal)
                        || item.GuidId.Contains("Circle", StringComparison.Ordinal)
                        || item.GuidId.Contains("Polygon", StringComparison.Ordinal)))
                    {
                        Assert.False(roi.Command!.CanExecute(null));
                    }
                }
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task InvocationShapeSelectsTheSameCapabilityCompatibleProviderUsedByMenuEligibility()
    {
        AlgorithmCatalog source = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor[] descriptors = source.Descriptors
            .Where(value => value.Id == StandardAlgorithmIds.ImageComparison
                || value.Id == StandardAlgorithmIds.BlobComponents
                || value.Id == StandardAlgorithmIds.Contours)
            .ToArray();
        AlgorithmCatalog catalog = new();
        foreach (AlgorithmDescriptor descriptor in descriptors) catalog.Register(descriptor);
        IReadOnlySet<AlgorithmId> ids = descriptors.Select(value => value.Id).ToHashSet();
        AlgorithmHostCapabilities withoutRoi = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local
            | AlgorithmHostCapabilities.MultiInput;
        MultiIdCapabilityProvider high = new(ids, "high-without-roi", 100, withoutRoi, 11);
        MultiIdCapabilityProvider low = new(ids, "low-with-roi", 1, withoutRoi | AlgorithmHostCapabilities.Roi, 22);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [high, low], scheduler);

        await AssertProvider(StandardAlgorithmIds.ImageComparison, new ImageComparisonParameters(), inputCount: 2, roi: null, "high-without-roi");
        await AssertProvider(StandardAlgorithmIds.ImageComparison, new ImageComparisonParameters(), inputCount: 2,
            new RectangleAlgorithmRoi(0, 0, 1, 1), "low-with-roi");
        await AssertProvider(StandardAlgorithmIds.BlobComponents, new BlobAnalysisParameters(), inputCount: 1, roi: null, "high-without-roi");
        await AssertProvider(StandardAlgorithmIds.BlobComponents, new BlobAnalysisParameters(), inputCount: 1,
            new RectangleAlgorithmRoi(0, 0, 1, 1), "low-with-roi");
        await AssertProvider(StandardAlgorithmIds.Contours, new ContourAnalysisParameters(), inputCount: 1, roi: null, "high-without-roi");
        await AssertProvider(StandardAlgorithmIds.Contours, new ContourAnalysisParameters(), inputCount: 1,
            new RectangleAlgorithmRoi(0, 0, 1, 1), "low-with-roi");

        async Task AssertProvider<TParameters>(
            AlgorithmId id,
            TParameters parameters,
            int inputCount,
            AlgorithmRoi? roi,
            string expectedProvider)
            where TParameters : IAlgorithmParameters
        {
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(id, parameters, roi);
            AlgorithmImageBuffer[] buffers = Enumerable.Range(0, inputCount).Select(index => Buffer((byte)(index + 1))).ToArray();
            try
            {
                using AlgorithmResult result = await runtime.Runner.RunAsync(new AlgorithmRunRequest
                {
                    Invocation = invocation,
                    Inputs = buffers.Select((buffer, index) => new AlgorithmInput
                    {
                        Name = inputCount == 1 ? "source" : index == 0 ? "reference" : "candidate",
                        Image = buffer,
                    }).ToArray(),
                    RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local
                        | (inputCount > 1 ? AlgorithmHostCapabilities.MultiInput : AlgorithmHostCapabilities.None),
                });
                Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
                Assert.Equal(expectedProvider, result.Diagnostics.ProviderId);
            }
            finally
            {
                foreach (AlgorithmImageBuffer buffer in buffers) buffer.Dispose();
            }
        }
    }

    [Fact]
    public async Task NativeAvailabilityControlsMenuProjectionAndRunnerRejectionDiagnostics()
    {
        AlgorithmDescriptor descriptor = StandardAlgorithmCatalog.Create().Descriptors.Single(
            value => value.Id == StandardAlgorithmIds.RemoveMoire);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        NativeCompatibilityAlgorithmProvider unavailable = new(() =>
            NativeAlgorithmAvailability.Unavailable("missing controlled export"));
        using AlgorithmExecutionScheduler unavailableScheduler = new(nativeConcurrency: 1);
        AlgorithmRuntime unavailableRuntime = new(catalog, [unavailable], unavailableScheduler);

        Assert.False(IsProjectedMenuVisible(unavailableRuntime, "RemoveMoire"));
        using AlgorithmImageBuffer input = Buffer(9);
        using AlgorithmResult result = await unavailableRuntime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input }],
            RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
        });
        AlgorithmFailure failure = Assert.Single(result.Failures);
        Assert.Equal("provider_unavailable", failure.Code);
        Assert.Contains("missing controlled export", failure.Details!["provider_dependency_unavailable"], StringComparison.Ordinal);

        NativeCompatibilityAlgorithmProvider available = new(() => NativeAlgorithmAvailability.Available);
        using AlgorithmExecutionScheduler availableScheduler = new(nativeConcurrency: 1);
        AlgorithmRuntime availableRuntime = new(catalog, [available], availableScheduler);
        Assert.True(IsProjectedMenuVisible(availableRuntime, "RemoveMoire"));
    }

    [Fact]
    public async Task AnalysisWithMultiplePrimaryImagesFailsWithoutPublishingOrPresenting()
    {
        AlgorithmDescriptor descriptor = Descriptor(
            "test.runtime-analysis-ambiguous",
            interactive: true,
            batch: false,
            semantics: AlgorithmResultSemantics.Analysis);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TestProvider provider = new(descriptor.Id, _ => new AlgorithmResult
        {
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                new AlgorithmImageArtifact("first", "primary", Buffer(11)),
                new AlgorithmImageArtifact("second", "PRIMARY", Buffer(22)),
            ],
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Task<AlgorithmResult> apply = WpfTestHost.Invoke(() => ImageAlgorithmApplier.ApplyAsync(
                context,
                AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters())));
            using AlgorithmResult result = await apply;

            Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
            Assert.Contains(result.Failures, failure => failure.Code == "primary_image_contract_violation");
            Assert.Equal(revision, WpfTestHost.Invoke(() => context.ImageRevision));
            Assert.Equal((byte)3, WpfTestHost.Invoke(() => Pixel(context.ViewBitmapSource)));
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task FrequencyAnalysisKeepsVisualizationImagesWithoutCommittingThem()
    {
        ImageView view = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView created = new(ExperimentalAlgorithmTestRuntime.Runtime);
            WriteableBitmap bitmap = new(16, 16, 96, 96, PixelFormats.Gray8, null);
            byte[] pixels = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
            bitmap.WritePixels(new Int32Rect(0, 0, 16, 16), pixels, 16, 0);
            created.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return created;
        });
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Task<AlgorithmResult> apply = WpfTestHost.Invoke(() => ImageAlgorithmApplier.ApplyAsync(
                context,
                AlgorithmInvocation.Create(StandardAlgorithmIds.FrequencySpectrum, new FrequencySpectrumParameters
                {
                    WindowFunction = FrequencyWindowFunction.Rectangular,
                    RemoveMean = false,
                })));
            using AlgorithmResult result = await apply;

            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.Equal(2, result.Artifacts.OfType<AlgorithmImageArtifact>().Count());
            Assert.All(result.Artifacts.OfType<AlgorithmImageArtifact>(), artifact =>
                Assert.False(string.Equals(artifact.Role, "primary", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(revision, WpfTestHost.Invoke(() => context.ImageRevision));
            Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void CustomRuntimeBatchProjectionExecutesItsProviderAndEnforcesUniquePrimaryRole()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.runtime-batch", interactive: false, batch: true);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        int calls = 0;
        TestProvider provider = new(descriptor.Id, _ =>
        {
            int call = Interlocked.Increment(ref calls);
            return call == 1
                ? new AlgorithmResult
                {
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts =
                    [
                        new AlgorithmImageArtifact("preview", "visualization", Buffer(14)),
                        new AlgorithmImageArtifact("output", "primary", Buffer(27)),
                    ],
                }
                : new AlgorithmResult
                {
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts =
                    [
                        new AlgorithmImageArtifact("first", "primary", Buffer(30)),
                        new AlgorithmImageArtifact("second", "PRIMARY", Buffer(31)),
                    ],
                };
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        BatchImageAlgorithmDefinition definition = Assert.Single(BatchImageAlgorithms.CreateAll(runtime).Skip(1));
        using Mat source = new(1, 1, MatType.CV_8UC1, Scalar.All(1));

        using Mat output = definition.Apply(source);
        Assert.Equal((byte)27, output.At<byte>(0, 0));
        BatchImageAlgorithmContractException failure = Assert.Throws<BatchImageAlgorithmContractException>(() => definition.Apply(source));
        Assert.Contains("primary_image_contract_violation", failure.Message, StringComparison.Ordinal);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InjectedLocalFlowAdapterUsesTheRuntimeProvider()
    {
        AlgorithmDescriptor descriptor = Descriptor(
            "test.runtime-flow",
            interactive: false,
            batch: false,
            extraCapabilities: AlgorithmHostCapabilities.Flow | AlgorithmHostCapabilities.Headless);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        int executions = 0;
        TestProvider provider = new(descriptor.Id, _ =>
        {
            Interlocked.Increment(ref executions);
            return PrimaryResult(73);
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        LocalFrameMetadata metadata = new() { Width = 1, Height = 1, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 1, 0);
        using LocalFlowFrameLease lease = frame.Acquire();
        Marshal.Copy(new byte[] { 7 }, 0, lease.RawPointer, 1);

        using AlgorithmResult result = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(
            runtime,
            lease,
            AlgorithmInvocation.Create(descriptor.Id, new NoAlgorithmParameters()));

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(1, executions);
        Assert.Equal((byte)73, result.GetArtifact<AlgorithmImageArtifact>()!.Image.Data.Span[0]);
        Assert.Equal(new byte[] { 7 }, lease.CopyRawToArray());
    }

    [Fact]
    public void RuntimeCollectionsAreReadOnlySnapshots()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.runtime-immutable", interactive: false, batch: false);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        TestProvider provider = new(descriptor.Id, _ => PrimaryResult(1));
        TestMigrator migrator = new(descriptor.Id);
        IImageAlgorithmProvider[] providerSource = [provider];
        IAlgorithmParameterMigrator[] migratorSource = [migrator];
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, providerSource, scheduler, migratorSource);

        providerSource[0] = new TestProvider(new AlgorithmId("test.replacement"), _ => PrimaryResult(2));
        migratorSource[0] = new TestMigrator(new AlgorithmId("test.replacement"));

        Assert.False(runtime.ProviderRegistry.Providers is IImageAlgorithmProvider[]);
        Assert.False(runtime.ParameterMigrators is IAlgorithmParameterMigrator[]);
        Assert.Same(provider, Assert.Single(runtime.ProviderRegistry.Providers));
        Assert.Same(migrator, Assert.Single(runtime.ParameterMigrators));
        Assert.Throws<NotSupportedException>(() => ((IList<IImageAlgorithmProvider>)runtime.ProviderRegistry.Providers)[0] = providerSource[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<IAlgorithmParameterMigrator>)runtime.ParameterMigrators)[0] = migratorSource[0]);
    }

    [Fact]
    public void DescriptorAndMenuKeepTheirPublishedCompatibilityShapes()
    {
        ConstructorInfo descriptorConstructor = Assert.Single(
            typeof(AlgorithmDescriptor).GetConstructors(), constructor => constructor.GetParameters().Length == 18);
        string[] expectedNames =
        [
            "Id", "Version", "Name", "Category", "Description", "ParameterType", "ParameterSchema",
            "SupportedFormats", "Capabilities", "MinimumInputCount", "MaximumInputCount",
            "SupportsRectangleRoi", "SupportsCircleRoi", "SupportsPolygonRoi", "SupportsPolylineRoi",
            "OutputSuffix", "OutputFormats", "OutputFormatPolicy",
        ];
        Type[] expectedTypes =
        [
            typeof(AlgorithmId), typeof(AlgorithmVersion), typeof(string), typeof(string), typeof(string),
            typeof(Type), typeof(AlgorithmParameterSchema), typeof(IReadOnlySet<AlgorithmImageFormat>),
            typeof(AlgorithmHostCapabilities), typeof(int), typeof(int), typeof(bool), typeof(bool),
            typeof(bool), typeof(bool), typeof(string), typeof(IReadOnlySet<AlgorithmImageFormat>), typeof(string),
        ];
        ParameterInfo[] constructorParameters = descriptorConstructor.GetParameters();
        Assert.Equal(expectedNames, constructorParameters.Select(parameter => parameter.Name));
        Assert.Equal(expectedTypes, constructorParameters.Select(parameter => parameter.ParameterType));
        Assert.All(constructorParameters.Take(9), parameter => Assert.False(parameter.IsOptional));
        Assert.All(constructorParameters.Skip(9), parameter => Assert.True(parameter.IsOptional));
        Assert.Equal(1, constructorParameters[9].DefaultValue);
        Assert.Equal(1, constructorParameters[10].DefaultValue);
        Assert.All(constructorParameters.Skip(11).Take(4), parameter => Assert.Equal(false, parameter.DefaultValue));
        Assert.Equal(string.Empty, constructorParameters[15].DefaultValue);
        Assert.Null(constructorParameters[16].DefaultValue);
        Assert.Equal("same-as-input", constructorParameters[17].DefaultValue);

        MethodInfo descriptorDeconstruct = Assert.Single(
            typeof(AlgorithmDescriptor).GetMethods(), method => method.Name == "Deconstruct" && method.GetParameters().Length == 18);
        ParameterInfo[] deconstructParameters = descriptorDeconstruct.GetParameters();
        Assert.Equal(expectedNames, deconstructParameters.Select(parameter => parameter.Name));
        Assert.Equal(expectedTypes, deconstructParameters.Select(parameter => parameter.ParameterType.GetElementType()));
        Assert.All(deconstructParameters, parameter =>
        {
            Assert.True(parameter.IsOut);
            Assert.False(parameter.IsOptional);
            Assert.False(parameter.HasDefaultValue);
        });
        Assert.NotNull(typeof(AlgorithmDescriptor).GetProperty(nameof(AlgorithmDescriptor.Presentation)));
        Assert.NotNull(typeof(AlgorithmDescriptor).GetProperty(nameof(AlgorithmDescriptor.ResultSemantics)));

        ImageView view = CreateImageView(ImageAlgorithmPlatform.Runtime, 1);
        ImageView secondView = CreateImageView(ImageAlgorithmPlatform.Runtime, 2);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext context = view.EditorContext.ProcessingContext;
                ImageProcessingContext secondContext = secondView.EditorContext.ProcessingContext;
                AlgorithmsContextMenu first = new(context);
                AlgorithmsContextMenu second = new(context);
                Assert.Same(context, first.imageContext);
                first.Deconstruct(out ImageProcessingContext deconstructed);
                Assert.Same(context, deconstructed);
                Assert.Equal(first, second);
                Assert.Equal(first.GetHashCode(), second.GetHashCode());
                AlgorithmsContextMenu rebound = first with { imageContext = secondContext };
                Assert.Same(secondContext, rebound.imageContext);
                Assert.NotEqual(first, rebound);
                Assert.NotEmpty(rebound.GetContextMenuItems());
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
            WpfTestHost.Invoke(secondView.Dispose);
        }
    }

    [Fact]
    public void CatalogOnlyBatchFacadeRejectsChangedExecutionContractsAndExcludesAnalysis()
    {
        AlgorithmDescriptor canonical = ImageAlgorithmPlatform.Catalog.Descriptors.Single(
            value => value.Id == StandardAlgorithmIds.Invert);
        AlgorithmCatalog changed = new();
        changed.Register(canonical with { OutputFormatPolicy = "always-gray8" });
        ArgumentException failure = Assert.Throws<ArgumentException>(() => BatchImageAlgorithms.CreateAll(changed));
        Assert.Contains("AlgorithmRuntime", failure.Message, StringComparison.Ordinal);

        AlgorithmCatalog analysis = new();
        analysis.Register(canonical with { ResultSemantics = AlgorithmResultSemantics.Analysis });
        Assert.Throws<ArgumentException>(() => BatchImageAlgorithms.CreateAll(analysis));
    }

    [Fact]
    public void CatalogOnlyBatchFacadeEnumeratesTheSourceExactlyOnce()
    {
        AlgorithmDescriptor invert = ImageAlgorithmPlatform.Catalog.Descriptors.Single(
            value => value.Id == StandardAlgorithmIds.Invert);
        AlgorithmDescriptor injected = ImageAlgorithmPlatform.Catalog.Descriptors.Single(
            value => value.Id == StandardAlgorithmIds.Canny) with { OutputFormatPolicy = "injected" };
        ChangingCatalog catalog = new([invert], [invert, injected]);

        IReadOnlyList<BatchImageAlgorithmDefinition> algorithms = BatchImageAlgorithms.CreateAll(catalog);

        Assert.Equal(1, catalog.DescriptorReads);
        BatchImageAlgorithmDefinition definition = Assert.Single(algorithms.Skip(1));
        Assert.Equal(StandardAlgorithmIds.Invert, definition.Descriptor!.Id);
    }

    [Fact]
    public void DefaultAnalysisPresentationExportsJsonAndCsvThroughTheSharedExporter()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"algorithm-presenter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using AlgorithmResult result = new()
            {
                InvocationId = Guid.NewGuid(),
                AlgorithmId = new AlgorithmId("test.presentation"),
                AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmMeasurementArtifact("measurements", [new AlgorithmMeasurement("mean", 8.5)])],
            };
            AlgorithmAnalysisResultPresentation presentation = DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "Analysis");

            string json = presentation.ExportJson(Path.Combine(directory, "result.json"));
            IReadOnlyList<string> csv = presentation.ExportCsv(Path.Combine(directory, "result.csv"));

            Assert.True(File.Exists(json));
            Assert.Contains("test.presentation", File.ReadAllText(json), StringComparison.Ordinal);
            string csvPath = Assert.Single(csv);
            Assert.True(File.Exists(csvPath));
            Assert.Contains("mean", File.ReadAllText(csvPath), StringComparison.Ordinal);
            Assert.Contains("Analysis", presentation.Title, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DefaultAnalysisPresentationRendersAndExportsAHeatmapImageAfterResultDisposal()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"algorithm-image-presenter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            AlgorithmResult result = new()
            {
                InvocationId = Guid.NewGuid(),
                AlgorithmId = new AlgorithmId("test.heatmap-presentation"),
                AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts =
                [
                    new AlgorithmImageArtifact(
                        "difference",
                        "heatmap",
                        new AlgorithmImageBuffer(2, 1, 8, AlgorithmImageFormat.Bgra32, [1, 2, 3, 255, 4, 5, 6, 255])),
                ],
            };
            AlgorithmAnalysisResultPresentation presentation = WpfTestHost.Invoke(
                () => DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "Heatmap"));
            result.Dispose();

            AlgorithmAnalysisImagePresentation imagePresentation = Assert.Single(presentation.Images);
            Assert.Equal("heatmap", imagePresentation.Role);
            Assert.Equal(AlgorithmImageFormat.Bgra32, imagePresentation.Format);
            byte[] rendered = new byte[8];
            imagePresentation.Bitmap.CopyPixels(rendered, 8, 0);
            Assert.Equal(new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 }, rendered);

            WpfTestHost.Invoke(() =>
            {
                DockPanel root = Assert.IsType<DockPanel>(DefaultAlgorithmAnalysisResultPresenter.CreateContent(presentation));
                TabControl tabs = Assert.Single(root.Children.OfType<TabControl>());
                TabItem imageTab = Assert.IsType<TabItem>(tabs.Items[0]);
                Assert.Same(imagePresentation, imageTab.DataContext);
                Grid imagePanel = Assert.IsType<Grid>(imageTab.Content);
                Image image = Assert.Single(imagePanel.Children.OfType<Image>());
                Button exportButton = Assert.Single(imagePanel.Children.OfType<Button>());
                Assert.Same(imagePresentation.Bitmap, image.Source);
                Assert.Same(imagePresentation, exportButton.DataContext);
                Assert.Contains("PNG", exportButton.Content?.ToString(), StringComparison.Ordinal);
                Assert.IsType<TextBox>(Assert.IsType<TabItem>(tabs.Items[1]).Content);
            });

            string exported = imagePresentation.Export(Path.Combine(directory, "heatmap.png"));
            Assert.True(File.Exists(exported));
            byte[] exportedPixels = WpfTestHost.Invoke(() =>
            {
                BitmapSource decoded = BitmapDecoder.Create(
                    new Uri(exported),
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad).Frames[0];
                byte[] pixels = new byte[8];
                decoded.CopyPixels(pixels, 8, 0);
                return pixels;
            });
            Assert.Equal(rendered, exportedPixels);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Bgr96Float)]
    [InlineData(AlgorithmImageFormat.Bgra128Float)]
    public void DefaultAnalysisPresentationNormalizesFloatColorImagesAndExportsAfterResultDisposal(AlgorithmImageFormat format)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"algorithm-float-presenter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            float[] values = format == AlgorithmImageFormat.Bgr96Float
                ? [float.NaN, 0f, 0.5f, float.PositiveInfinity, 1f, float.NegativeInfinity]
                : [0f, 0.5f, 1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0.25f, 0.5f];
            int stride = format == AlgorithmImageFormat.Bgr96Float ? 24 : 32;
            AlgorithmResult result = new()
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts =
                [
                    new AlgorithmImageArtifact(
                        "float-heatmap",
                        "heatmap",
                        new AlgorithmImageBuffer(2, 1, stride, format, MemoryMarshal.AsBytes(values.AsSpan()).ToArray())),
                ],
            };
            AlgorithmAnalysisResultPresentation presentation = WpfTestHost.Invoke(
                () => DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "Float heatmap"));
            result.Dispose();

            AlgorithmAnalysisImagePresentation image = Assert.Single(presentation.Images);
            Assert.Equal("finite-global-minmax; nan/-inf=0; +inf=255", image.DisplayRangePolicy);
            Assert.Equal(".png", image.PreferredExtension);
            byte[] expected = format == AlgorithmImageFormat.Bgr96Float
                ? [0, 0, 128, 255, 255, 0]
                : [0, 128, 255, 0, 255, 0, 64, 128];
            Assert.Equal(format == AlgorithmImageFormat.Bgr96Float ? PixelFormats.Bgr24 : PixelFormats.Bgra32, image.Bitmap.Format);
            byte[] rendered = new byte[expected.Length];
            image.Bitmap.CopyPixels(rendered, expected.Length, 0);
            Assert.Equal(expected, rendered);

            foreach (string extension in new[] { ".png", ".tiff" })
            {
                string exported = image.Export(Path.Combine(directory, $"{format}{extension}"));
                byte[] decodedPixels = WpfTestHost.Invoke(() =>
                {
                    BitmapSource decoded = BitmapDecoder.Create(
                        new Uri(exported), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                    Assert.Equal(image.Bitmap.Format, decoded.Format);
                    byte[] pixels = new byte[expected.Length];
                    decoded.CopyPixels(pixels, expected.Length, 0);
                    return pixels;
                });
                Assert.Equal(expected, decodedPixels);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DefaultAnalysisPresentationConstructionIsLazyAndBoundsLargeImageSets()
    {
        const int largeWidth = 4097;
        const int largeHeight = 4097;
        AlgorithmArtifact[] artifacts =
        [
            new AlgorithmImageArtifact(
                "large",
                "heatmap",
                new AlgorithmImageBuffer(
                    largeWidth,
                    largeHeight,
                    largeWidth,
                    AlgorithmImageFormat.Gray8,
                    new byte[checked(largeWidth * largeHeight)])),
            .. Enumerable.Range(0, 9).Select(index => new AlgorithmImageArtifact(
                $"small-{index}",
                "visualization",
                new AlgorithmImageBuffer(1, 1, 1, AlgorithmImageFormat.Gray8, [(byte)index]))),
        ];
        using AlgorithmResult result = new()
        {
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts = artifacts,
        };

        AlgorithmAnalysisResultPresentation presentation = WpfTestHost.Invoke(
            () => DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "Large analysis"));

        Assert.True(presentation.Images.Count <= 8);
        AlgorithmAnalysisImagePresentation image = presentation.Images[0];
        Assert.False(ReadPresentationBoolean(image, "IsBitmapMaterialized"));
        Assert.False(ReadPresentationBoolean(image, "IsPreviewAvailable"));
        WpfTestHost.Invoke(() =>
        {
            DockPanel root = Assert.IsType<DockPanel>(DefaultAlgorithmAnalysisResultPresenter.CreateContent(presentation));
            TabControl tabs = Assert.Single(root.Children.OfType<TabControl>());
            Grid panel = Assert.IsType<Grid>(Assert.IsType<TabItem>(tabs.Items[0]).Content);
            Assert.Contains(panel.Children.OfType<TextBlock>(), text =>
                text.Text.Contains("预算", StringComparison.Ordinal));
            Assert.All(panel.Children.OfType<Image>(), control => Assert.Null(control.Source));
        });
    }

    [Fact]
    public void DefaultAnalysisPresentationUsesABoundedJsonSummaryUntilExplicitExport()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"algorithm-bounded-json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string payload = new('x', 200_000);
            IReadOnlyDictionary<string, JsonElement>[] rows = Enumerable.Range(0, 10_000)
                .Select(index => (IReadOnlyDictionary<string, JsonElement>)new Dictionary<string, JsonElement>
                {
                    ["index"] = JsonSerializer.SerializeToElement(index),
                    ["value"] = JsonSerializer.SerializeToElement($"row-{index}"),
                })
                .ToArray();
            using AlgorithmResult result = new()
            {
                InvocationId = Guid.NewGuid(),
                AlgorithmId = new AlgorithmId("test.large-structured-result"),
                AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts =
                [
                    new AlgorithmStructuredDataArtifact(
                        "large",
                        "test.large.v1",
                        JsonSerializer.SerializeToElement(new { payload })),
                    new AlgorithmTableArtifact(
                        "large-table",
                        [new AlgorithmTableColumn("index", "integer"), new AlgorithmTableColumn("value", "string")],
                        rows),
                ],
            };
            AlgorithmAnalysisResultPresentation presentation = DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "Large JSON");

            Assert.True(presentation.Json.Length <= 32_768);
            WpfTestHost.Invoke(() =>
            {
                DockPanel root = Assert.IsType<DockPanel>(DefaultAlgorithmAnalysisResultPresenter.CreateContent(presentation));
                TabControl tabs = Assert.Single(root.Children.OfType<TabControl>());
                TextBox text = Assert.IsType<TextBox>(Assert.IsType<TabItem>(tabs.Items[^1]).Content);
                Assert.True(text.Text.Length <= 32_768);
            });

            string exported = presentation.ExportJson(Path.Combine(directory, "complete.json"));
            Assert.Contains(payload, File.ReadAllText(exported), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Gray8)]
    [InlineData(AlgorithmImageFormat.Gray16)]
    [InlineData(AlgorithmImageFormat.Gray32Float)]
    [InlineData(AlgorithmImageFormat.Bgr24)]
    [InlineData(AlgorithmImageFormat.Bgr48)]
    [InlineData(AlgorithmImageFormat.Bgr96Float)]
    [InlineData(AlgorithmImageFormat.Bgra32)]
    [InlineData(AlgorithmImageFormat.Bgra64)]
    [InlineData(AlgorithmImageFormat.Bgra128Float)]
    public void DefaultAnalysisPresentationLazilySupportsEveryPublicImageFormatAfterResultDisposal(AlgorithmImageFormat format)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"algorithm-all-formats-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            byte[] pixels = CreatePresentationPixels(format);
            AlgorithmResult result = new()
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts =
                [
                    new AlgorithmImageArtifact(
                        "format",
                        "visualization",
                        new AlgorithmImageBuffer(2, 1, format.BytesPerPixel() * 2, format, pixels)),
                ],
            };
            AlgorithmAnalysisResultPresentation presentation = WpfTestHost.Invoke(
                () => DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, format.ToString()));
            AlgorithmAnalysisImagePresentation image = Assert.Single(presentation.Images);
            Assert.False(ReadPresentationBoolean(image, "IsBitmapMaterialized"));
            result.Dispose();

            BitmapSource bitmap = WpfTestHost.Invoke(() => image.Bitmap);
            Assert.True(ReadPresentationBoolean(image, "IsBitmapMaterialized"));
            Assert.Equal(2, bitmap.PixelWidth);
            Assert.Equal(1, bitmap.PixelHeight);
            if (format.IsFloatingPoint())
                Assert.Contains("finite", image.DisplayRangePolicy, StringComparison.Ordinal);

            string exported = WpfTestHost.Invoke(() => image.Export(Path.Combine(directory, $"image{image.PreferredExtension}")));
            Assert.True(File.Exists(exported));
            Assert.NotEmpty(File.ReadAllBytes(exported));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DefaultAnalysisPresentationMapsGray32FloatFiniteAndExceptionalValuesDeterministically()
    {
        float[] values = [float.NaN, 10f, 20f, float.PositiveInfinity];
        AlgorithmResult result = new()
        {
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                new AlgorithmImageArtifact(
                    "gray32",
                    "heatmap",
                    new AlgorithmImageBuffer(
                        4,
                        1,
                        4 * sizeof(float),
                        AlgorithmImageFormat.Gray32Float,
                        MemoryMarshal.AsBytes(values.AsSpan()).ToArray())),
            ],
        };
        AlgorithmAnalysisResultPresentation presentation = DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, "Gray32Float");
        AlgorithmAnalysisImagePresentation image = Assert.Single(presentation.Images);
        result.Dispose();

        BitmapSource bitmap = WpfTestHost.Invoke(() => image.Bitmap);
        Assert.Equal(PixelFormats.Gray8, bitmap.Format);
        Assert.Equal("finite-global-minmax; nan/-inf=0; +inf=255", image.DisplayRangePolicy);
        byte[] actual = new byte[4];
        bitmap.CopyPixels(actual, 4, 0);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, actual);
    }

    private static bool ReadPresentationBoolean(AlgorithmAnalysisImagePresentation image, string propertyName)
    {
        PropertyInfo? property = typeof(AlgorithmAnalysisImagePresentation).GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<bool>(property.GetValue(image));
    }

    private static byte[] CreatePresentationPixels(AlgorithmImageFormat format)
    {
        if (format.IsFloatingPoint())
        {
            float[] values = format switch
            {
                AlgorithmImageFormat.Gray32Float => [float.NaN, float.PositiveInfinity],
                AlgorithmImageFormat.Bgr96Float => [float.NaN, 0f, 0.5f, float.PositiveInfinity, 1f, float.NegativeInfinity],
                AlgorithmImageFormat.Bgra128Float => [0f, 0.5f, 1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0.25f, 0.5f],
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };
            return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        }

        int length = checked(format.BytesPerPixel() * 2);
        return Enumerable.Range(0, length).Select(index => (byte)(index * 17)).ToArray();
    }

    [Fact]
    public void SpecializedPresetSerializersUseOneInjectedCompatibleCatalogForWriteAndRead()
    {
        AlgorithmCatalog source = StandardAlgorithmCatalog.Create();
        AlgorithmCatalog custom = new();
        foreach (AlgorithmId id in new[]
                 {
                     StandardAlgorithmIds.GeometricTransform,
                     StandardAlgorithmIds.ImageRegistration,
                     StandardAlgorithmIds.ImagingCorrection,
                     StandardAlgorithmIds.LensDistortionCorrection,
                 })
        {
            AlgorithmDescriptor descriptor = source.Descriptors.Single(value => value.Id == id);
            custom.Register(descriptor with
            {
                Version = new AlgorithmVersion(
                    descriptor.Version.Major,
                    descriptor.Version.Minor + 9,
                    descriptor.Version.Patch + 1),
            });
        }

        string geometric = GeometricTransformPresetSerializer.Serialize(custom, "geometric", new GeometricTransformParameters());
        string registration = ImageRegistrationPresetSerializer.Serialize(custom, "registration", new ImageRegistrationParameters());
        string correction = ImagingCorrectionPresetSerializer.Serialize(custom, "correction", new ImagingCorrectionParameters());
        string lens = LensDistortionCorrectionPresetSerializer.Serialize(custom, "lens", new LensDistortionCorrectionParameters());

        Assert.Equal("geometric", GeometricTransformPresetSerializer.Deserialize(custom, geometric).PresetId);
        Assert.Equal("registration", ImageRegistrationPresetSerializer.Deserialize(custom, registration).PresetId);
        Assert.Equal("correction", ImagingCorrectionPresetSerializer.Deserialize(custom, correction).PresetId);
        Assert.Equal("lens", LensDistortionCorrectionPresetSerializer.Deserialize(custom, lens).PresetId);
        Assert.Equal("geometric", GeometricTransformPresetSerializer.Deserialize(geometric).PresetId);
        Assert.Equal("registration", ImageRegistrationPresetSerializer.Deserialize(registration).PresetId);
        Assert.Equal("correction", ImagingCorrectionPresetSerializer.Deserialize(correction).PresetId);
        Assert.Equal("lens", LensDistortionCorrectionPresetSerializer.Deserialize(lens).PresetId);
    }

    [Fact]
    public void SpecializedPresetSerializersAcceptCompatibleMinorPatchCatalogVersionsAndRejectMajorChanges()
    {
        AlgorithmCatalog source = StandardAlgorithmCatalog.Create();
        AlgorithmCatalog compatible = new();
        AlgorithmCatalog incompatibleMajor = new();
        foreach (AlgorithmId id in new[]
                 {
                     StandardAlgorithmIds.GeometricTransform,
                     StandardAlgorithmIds.ImageRegistration,
                     StandardAlgorithmIds.ImagingCorrection,
                     StandardAlgorithmIds.LensDistortionCorrection,
                 })
        {
            AlgorithmDescriptor descriptor = source.Descriptors.Single(value => value.Id == id);
            compatible.Register(descriptor with
            {
                Version = new AlgorithmVersion(descriptor.Version.Major, descriptor.Version.Minor + 1, descriptor.Version.Patch + 1),
            });
            incompatibleMajor.Register(descriptor with
            {
                Version = new AlgorithmVersion(descriptor.Version.Major + 1, 0, 0),
            });
        }

        string geometric = GeometricTransformPresetSerializer.Serialize(source, "geometric", new GeometricTransformParameters());
        string registration = ImageRegistrationPresetSerializer.Serialize(source, "registration", new ImageRegistrationParameters());
        string correction = ImagingCorrectionPresetSerializer.Serialize(source, "correction", new ImagingCorrectionParameters());
        string lens = LensDistortionCorrectionPresetSerializer.Serialize(source, "lens", new LensDistortionCorrectionParameters());

        Assert.Equal("geometric", GeometricTransformPresetSerializer.Deserialize(compatible, geometric).PresetId);
        Assert.Equal("registration", ImageRegistrationPresetSerializer.Deserialize(compatible, registration).PresetId);
        Assert.Equal("correction", ImagingCorrectionPresetSerializer.Deserialize(compatible, correction).PresetId);
        Assert.Equal("lens", LensDistortionCorrectionPresetSerializer.Deserialize(compatible, lens).PresetId);
        Assert.Throws<InvalidOperationException>(() => GeometricTransformPresetSerializer.Deserialize(incompatibleMajor, geometric));
        Assert.Throws<InvalidOperationException>(() => ImageRegistrationPresetSerializer.Deserialize(incompatibleMajor, registration));
        Assert.Throws<InvalidOperationException>(() => ImagingCorrectionPresetSerializer.Deserialize(incompatibleMajor, correction));
        Assert.Throws<InvalidOperationException>(() => LensDistortionCorrectionPresetSerializer.Deserialize(incompatibleMajor, lens));
        Assert.Throws<InvalidOperationException>(() => GeometricTransformPresetSerializer.Serialize(
            incompatibleMajor, "geometric-major", new GeometricTransformParameters()));
        Assert.Throws<InvalidOperationException>(() => ImageRegistrationPresetSerializer.Serialize(
            incompatibleMajor, "registration-major", new ImageRegistrationParameters()));
        Assert.Throws<InvalidOperationException>(() => ImagingCorrectionPresetSerializer.Serialize(
            incompatibleMajor, "correction-major", new ImagingCorrectionParameters()));
        Assert.Throws<InvalidOperationException>(() => LensDistortionCorrectionPresetSerializer.Serialize(
            incompatibleMajor, "lens-major", new LensDistortionCorrectionParameters()));
    }

    [Fact]
    public void SpecializedMenuAdapterRejectsChangedDefaultsAndParameterTypesButAcceptsVersionOnlyChanges()
    {
        AlgorithmDescriptor canonical = StandardAlgorithmCatalog.Create().Descriptors.Single(
            value => value.Id == StandardAlgorithmIds.Canny);
        AlgorithmDescriptor changedDefaults = canonical with
        {
            ParameterSchema = canonical.ParameterSchema with
            {
                Defaults = AlgorithmJson.ToElement(new CannyParameters { LowThreshold = 12, HighThreshold = 34 }),
            },
        };
        AlgorithmDescriptor incompatibleType = canonical with
        {
            ParameterType = typeof(NoAlgorithmParameters),
            ParameterSchema = new AlgorithmParameterSchema(
                canonical.ParameterSchema.Version,
                Array.Empty<AlgorithmParameterField>(),
                AlgorithmJson.ToElement(new NoAlgorithmParameters())),
        };
        AlgorithmDescriptor versionOnly = canonical with
        {
            Version = new AlgorithmVersion(canonical.Version.Major, canonical.Version.Minor + 2, canonical.Version.Patch + 1),
        };
        AlgorithmDescriptor differentMajor = canonical with
        {
            Version = new AlgorithmVersion(canonical.Version.Major + 1, 0, 0),
        };
        AlgorithmDescriptor changedSemantics = canonical with { ResultSemantics = AlgorithmResultSemantics.Analysis };
        AlgorithmDescriptor changedInputs = canonical with { MinimumInputCount = 2, MaximumInputCount = 2 };
        AlgorithmDescriptor changedOutput = canonical with { OutputFormatPolicy = "no-image-output", OutputFormats = new HashSet<AlgorithmImageFormat>() };
        AlgorithmDescriptor changedRoi = canonical with { SupportsRectangleRoi = true };
        AlgorithmDescriptor changedCapabilities = canonical with { Capabilities = canonical.Capabilities | AlgorithmHostCapabilities.MultiInput };

        Assert.False(AlgorithmsContextMenu.UsesSpecializedAdapter(changedDefaults));
        Assert.False(AlgorithmsContextMenu.UsesSpecializedAdapter(incompatibleType));
        Assert.False(AlgorithmsContextMenu.UsesSpecializedAdapter(changedSemantics));
        Assert.False(AlgorithmsContextMenu.UsesSpecializedAdapter(changedInputs));
        Assert.False(AlgorithmsContextMenu.UsesSpecializedAdapter(changedOutput));
        Assert.False(AlgorithmsContextMenu.UsesSpecializedAdapter(changedRoi));
        Assert.False(AlgorithmsContextMenu.UsesSpecializedAdapter(changedCapabilities));
        Assert.True(AlgorithmsContextMenu.UsesSpecializedAdapter(versionOnly));
        Assert.False(AlgorithmsContextMenu.UsesSpecializedAdapter(differentMajor));
        CannyParameters genericDefaults = Assert.IsType<CannyParameters>(
            changedDefaults.ParameterSchema.Defaults.Deserialize(changedDefaults.ParameterType, AlgorithmJson.Options));
        Assert.Equal(12, genericDefaults.LowThreshold);
        Assert.Equal(34, genericDefaults.HighThreshold);
    }

    [Theory]
    [InlineData("colorvision.analysis.roi-statistics")]
    [InlineData("colorvision.analysis.image-profile")]
    [InlineData("colorvision.analysis.image-comparison")]
    [InlineData("colorvision.analysis.blob-components")]
    [InlineData("colorvision.analysis.contours")]
    [InlineData("colorvision.measurement.subpixel-edge")]
    [InlineData("colorvision.measurement.line-fit")]
    [InlineData("colorvision.measurement.circle-fit")]
    public void CatalogBoundSpecializedToolsRejectKnownIdsWithChangedExecutionShape(string algorithmId)
    {
        AlgorithmDescriptor canonical = StandardAlgorithmCatalog.Create().Descriptors.Single(
            value => value.Id == new AlgorithmId(algorithmId));
        AlgorithmDescriptor incompatible = canonical with
        {
            ResultSemantics = canonical.ResultSemantics == AlgorithmResultSemantics.Analysis
                ? AlgorithmResultSemantics.ImageTransform
                : AlgorithmResultSemantics.Analysis,
        };
        AlgorithmCatalog catalog = new();
        catalog.Register(incompatible);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new TestProvider(incompatible.Id, _ => PrimaryResult(1))], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                object[] specialized = view.IEditorToolFactory.IIEditorToolContextMenus
                    .Cast<object>()
                    .Concat(view.IEditorToolFactory.ContextMenuProviders)
                    .Where(value => value is IAlgorithmCatalogBoundMenu)
                    .ToArray();
                Assert.DoesNotContain(specialized, value =>
                    Assert.IsAssignableFrom<IAlgorithmCatalogBoundMenu>(value).AlgorithmId == incompatible.Id);
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public void SpecializedPresetSerializersRejectIncompatibleInjectedParameterContracts()
    {
        AlgorithmCatalog source = StandardAlgorithmCatalog.Create();
        AlgorithmCatalog incompatible = new();
        foreach (AlgorithmId id in new[]
                 {
                     StandardAlgorithmIds.GeometricTransform,
                     StandardAlgorithmIds.ImageRegistration,
                     StandardAlgorithmIds.ImagingCorrection,
                     StandardAlgorithmIds.LensDistortionCorrection,
                 })
        {
            AlgorithmDescriptor descriptor = source.Descriptors.Single(value => value.Id == id);
            incompatible.Register(descriptor with
            {
                ParameterType = typeof(NoAlgorithmParameters),
                ParameterSchema = new AlgorithmParameterSchema(
                    descriptor.ParameterSchema.Version,
                    Array.Empty<AlgorithmParameterField>(),
                    AlgorithmJson.ToElement(new NoAlgorithmParameters())),
            });
        }

        Assert.Throws<InvalidOperationException>(() => GeometricTransformPresetSerializer.Serialize(
            incompatible, "geometric", new GeometricTransformParameters()));
        Assert.Throws<InvalidOperationException>(() => ImageRegistrationPresetSerializer.Serialize(
            incompatible, "registration", new ImageRegistrationParameters()));
        Assert.Throws<InvalidOperationException>(() => ImagingCorrectionPresetSerializer.Serialize(
            incompatible, "correction", new ImagingCorrectionParameters()));
        Assert.Throws<InvalidOperationException>(() => LensDistortionCorrectionPresetSerializer.Serialize(
            incompatible, "lens", new LensDistortionCorrectionParameters()));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void BatchProcessorReportsPrimaryContractFailureAndReleasesTransferredBuffers(bool duplicatePrimary, int resultImageCount)
    {
        AlgorithmDescriptor descriptor = Descriptor("test.runtime-batch-contract", interactive: false, batch: true);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        AlgorithmImageBuffer? transferredInput = null;
        List<AlgorithmImageBuffer> resultImages = [];
        TestProvider provider = new(descriptor.Id, context =>
        {
            transferredInput = context.Inputs.Single().Image;
            if (duplicatePrimary)
            {
                AlgorithmImageBuffer first = Buffer(10);
                AlgorithmImageBuffer second = Buffer(20);
                resultImages.Add(first);
                resultImages.Add(second);
                return new AlgorithmResult
                {
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts =
                    [
                        new AlgorithmImageArtifact("first", "primary", first),
                        new AlgorithmImageArtifact("second", "PRIMARY", second),
                    ],
                };
            }

            AlgorithmImageBuffer visualization = Buffer(30);
            resultImages.Add(visualization);
            return new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmImageArtifact("visualization", "heatmap", visualization)],
            };
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        BatchImageAlgorithmDefinition definition = Assert.Single(BatchImageAlgorithms.CreateAll(runtime).Skip(1));
        string directory = Path.Combine(Path.GetTempPath(), $"batch-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "source.fake");
            File.WriteAllText(sourcePath, "fixture");
            BatchImageProcessor processor = new([new FakeBatchLoader()]);
            BatchImageRunResult run = processor.Process(new BatchImageProcessingRequest
            {
                Items = [new BatchImageItem(sourcePath, directory)],
                Algorithm = definition,
                OutputDirectory = directory,
                Suffix = "_out",
                PreserveFolderStructure = false,
            });

            BatchImageFileResult file = Assert.Single(run.Files);
            Assert.False(file.Success);
            Assert.False(file.Cancelled);
            Assert.True(file.SourceRead);
            Assert.Equal("primary_image_contract_violation", file.ErrorCode);
            Assert.Contains("primary", file.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(transferredInput);
            Assert.True(transferredInput.IsDisposed);
            Assert.Equal(resultImageCount, resultImages.Count);
            Assert.All(resultImages, image => Assert.True(image.IsDisposed));
            Assert.False(File.Exists(file.OutputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BatchProcessorReportsNoImageAndDisposesTransferredInputAndEveryArtifact()
    {
        AlgorithmDescriptor descriptor = Descriptor("test.runtime-batch-no-image", interactive: false, batch: true);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        AlgorithmImageBuffer? transferredInput = null;
        TrackingArtifact artifact = new("tracking");
        TestProvider provider = new(descriptor.Id, context =>
        {
            transferredInput = context.Inputs.Single().Image;
            return new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [artifact],
            };
        });
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [provider], scheduler);
        BatchImageAlgorithmDefinition definition = Assert.Single(BatchImageAlgorithms.CreateAll(runtime).Skip(1));
        string directory = Path.Combine(Path.GetTempPath(), $"batch-no-image-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "source.fake");
            File.WriteAllText(sourcePath, "fixture");
            BatchImageRunResult run = new BatchImageProcessor([new FakeBatchLoader()]).Process(new BatchImageProcessingRequest
            {
                Items = [new BatchImageItem(sourcePath, directory)],
                Algorithm = definition,
                OutputDirectory = directory,
                Suffix = "_out",
                PreserveFolderStructure = false,
            });

            BatchImageFileResult file = Assert.Single(run.Files);
            Assert.False(file.Success);
            Assert.True(file.SourceRead);
            Assert.Equal("primary_image_missing", file.ErrorCode);
            Assert.NotNull(transferredInput);
            Assert.True(transferredInput.IsDisposed);
            Assert.True(artifact.IsDisposed);
            Assert.False(File.Exists(file.OutputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static AlgorithmDescriptor Descriptor(
        string id,
        bool interactive,
        bool batch,
        AlgorithmResultSemantics semantics = AlgorithmResultSemantics.ImageTransform,
        AlgorithmHostCapabilities extraCapabilities = AlgorithmHostCapabilities.None)
    {
        AlgorithmHostCapabilities capabilities = AlgorithmHostCapabilities.Local | extraCapabilities;
        if (interactive) capabilities |= AlgorithmHostCapabilities.Interactive;
        if (batch) capabilities |= AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless;
        AlgorithmPresentationMetadata presentation = new(
            BatchImageProcessingOrder: batch ? 1 : null,
            InteractiveEntries: interactive ? [new AlgorithmInteractivePresentation("RuntimeMenu", 1)] : null);
        return new AlgorithmDescriptor(
            new AlgorithmId(id),
            new AlgorithmVersion(1, 0, 0),
            id,
            "test",
            "runtime integration test",
            typeof(NoAlgorithmParameters),
            new AlgorithmParameterSchema(1, Array.Empty<AlgorithmParameterField>(), AlgorithmJson.ToElement(new NoAlgorithmParameters())),
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
            capabilities,
            OutputFormats: new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 })
        {
            Presentation = presentation,
            ResultSemantics = semantics,
        };
    }

    private static AlgorithmResult PrimaryResult(byte value) => new()
    {
        Status = AlgorithmResultStatus.Succeeded,
        Artifacts = [new AlgorithmImageArtifact("output", "primary", Buffer(value))],
    };

    private static AlgorithmImageBuffer Buffer(byte value) => new(1, 1, 1, AlgorithmImageFormat.Gray8, [value]);

    private static byte Pixel(ImageSource source)
    {
        BitmapSource bitmap = Assert.IsAssignableFrom<BitmapSource>(source);
        byte[] pixel = new byte[1];
        bitmap.CopyPixels(pixel, 1, 0);
        return pixel[0];
    }

    private static ImageView CreateImageView(AlgorithmRuntime runtime, byte value)
        => WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new(runtime);
            WriteableBitmap bitmap = new(1, 1, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new[] { value }, 1, 0);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });

    private static bool IsProjectedMenuVisible(
        AlgorithmDescriptor descriptor,
        AlgorithmHostCapabilities providerCapabilities)
    {
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new CapabilityProvider(descriptor.Id, providerCapabilities)], scheduler);
        string compatibilityId = Assert.Single(descriptor.Presentation!.InteractiveEntries!).CompatibilityId;
        return IsProjectedMenuVisible(runtime, compatibilityId);
    }

    private static bool IsProjectedMenuVisible(AlgorithmRuntime runtime, string compatibilityId)
    {
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            return WpfTestHost.Invoke(() => new AlgorithmsContextMenu(view.EditorContext.ProcessingContext, runtime)
                .GetContextMenuItems()
                .Any(item => item.GuidId == compatibilityId));
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    private static bool IsCatalogBoundToolVisible(
        AlgorithmDescriptor descriptor,
        AlgorithmHostCapabilities providerCapabilities)
    {
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new CapabilityProvider(descriptor.Id, providerCapabilities)], scheduler);
        ImageView view = CreateImageView(runtime, 3);
        try
        {
            return WpfTestHost.Invoke(() => view.IEditorToolFactory.IIEditorToolContextMenus
                .Cast<object>()
                .Concat(view.IEditorToolFactory.ContextMenuProviders)
                .OfType<IAlgorithmCatalogBoundMenu>()
                .Any(menu => menu.AlgorithmId == descriptor.Id));
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed class TestProvider(
        AlgorithmId algorithmId,
        Func<AlgorithmExecutionContext, AlgorithmResult> execute) : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        public AlgorithmProviderMetadata Metadata { get; } = new(
            $"provider-{algorithmId.Value}",
            "test provider",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            1,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless
                | AlgorithmHostCapabilities.Flow | AlgorithmHostCapabilities.Copilot | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi | AlgorithmHostCapabilities.MultiInput,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(execute(context));
        }
    }

    private sealed class PlaneProvider(
        AlgorithmId algorithmId,
        string providerId,
        AlgorithmExecutionPlane executionPlane,
        int priority,
        AlgorithmHostCapabilities capabilities,
        byte output) : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private int _executions;

        public int Executions => Volatile.Read(ref _executions);

        public AlgorithmProviderMetadata Metadata { get; } = new(
            providerId,
            providerId,
            executionPlane == AlgorithmExecutionPlane.Local ? AlgorithmProviderKind.Cpu : AlgorithmProviderKind.Remote,
            executionPlane,
            priority,
            capabilities,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executions);
            return ValueTask.FromResult(PrimaryResult(output));
        }
    }

    private sealed class MultiIdCapabilityProvider(
        IReadOnlySet<AlgorithmId> algorithmIds,
        string providerId,
        int priority,
        AlgorithmHostCapabilities capabilities,
        byte output) : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        public AlgorithmProviderMetadata Metadata { get; } = new(
            providerId,
            providerId,
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            priority,
            capabilities,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = algorithmIds.Contains(descriptor.Id) ? null : "algorithm_not_implemented";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
            => CanExecuteDescriptor(descriptor, out reason);

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(context.Descriptor.ResultSemantics == AlgorithmResultSemantics.Analysis
                ? new AlgorithmResult { Status = AlgorithmResultStatus.Succeeded }
                : PrimaryResult(output));
    }

    private sealed class CapabilityProvider(
        AlgorithmId algorithmId,
        AlgorithmHostCapabilities capabilities) : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        public AlgorithmProviderMetadata Metadata { get; } = new(
            $"capability-{algorithmId.Value}-{(int)capabilities}",
            "capability provider",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            1,
            capabilities,
            Enum.GetValues<AlgorithmImageFormat>().ToHashSet());

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = descriptor.Id == algorithmId ? null : "wrong algorithm";
            return reason == null;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(PrimaryResult(1));
    }

    private sealed class MetadataSensitiveLegacyProvider(AlgorithmId algorithmId) : IImageAlgorithmProvider
    {
        private int _inputAwareProbes;

        public int InputAwareProbes => Volatile.Read(ref _inputAwareProbes);

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "metadata-sensitive-legacy",
            "metadata-sensitive-legacy",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            1,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            Interlocked.Increment(ref _inputAwareProbes);
            bool supported = descriptor.Id == algorithmId
                && inputs.Count == 1
                && inputs[0].Image.Width >= 2
                && inputs[0].Name == "reference"
                && inputs[0].ColorSpace == "sRGB"
                && !string.IsNullOrWhiteSpace(inputs[0].SourceUri);
            reason = supported ? null : "real_input_metadata_required";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(PrimaryResult(88));
    }

    private sealed class ChangingCatalog(
        IReadOnlyCollection<AlgorithmDescriptor> first,
        IReadOnlyCollection<AlgorithmDescriptor> later) : IAlgorithmCatalog
    {
        private int _descriptorReads;

        public int DescriptorReads => Volatile.Read(ref _descriptorReads);

        public IReadOnlyCollection<AlgorithmDescriptor> Descriptors
            => Interlocked.Increment(ref _descriptorReads) == 1 ? first : later;

        public bool TryResolve(AlgorithmId id, out AlgorithmDescriptor? descriptor)
        {
            descriptor = first.FirstOrDefault(value => value.Id == id);
            return descriptor != null;
        }

        public bool TryResolveAlias(string idOrAlias, out AlgorithmDescriptor? descriptor)
        {
            descriptor = first.FirstOrDefault(value => string.Equals(value.Id.Value, idOrAlias, StringComparison.OrdinalIgnoreCase));
            return descriptor != null;
        }
    }

    private sealed record TrackingArtifact(string ArtifactName) : AlgorithmArtifact(ArtifactName), IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class TestMigrator(AlgorithmId algorithmId) : IAlgorithmParameterMigrator
    {
        public AlgorithmId AlgorithmId { get; } = algorithmId;
        public int FromSchemaVersion => 1;
        public int ToSchemaVersion => 2;
        public System.Text.Json.JsonElement Migrate(System.Text.Json.JsonElement parameters) => parameters.Clone();
    }

    private sealed class FakeBatchLoader : IBatchImageLoader
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".fake"];
        public Mat Load(string filePath) => new(1, 1, MatType.CV_8UC1, Scalar.All(7));
    }
}
