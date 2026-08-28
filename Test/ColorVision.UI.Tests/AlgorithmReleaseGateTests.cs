using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmReleaseGateTests
{
    private static readonly AlgorithmId[] ExperimentalIds =
    [
        StandardAlgorithmIds.BlobComponents,
        StandardAlgorithmIds.Contours,
        StandardAlgorithmIds.SubpixelEdge,
        StandardAlgorithmIds.LineFit,
        StandardAlgorithmIds.CircleFit,
        StandardAlgorithmIds.FrequencySpectrum,
        StandardAlgorithmIds.MoireAnalysis,
    ];

    [Fact]
    public void DefaultCatalogRetainsExperimentalDescriptorsButRuntimeCannotExecuteThem()
    {
        AlgorithmRuntime runtime = ImageAlgorithmPlatform.Runtime;
        foreach (AlgorithmId id in ExperimentalIds)
        {
            Assert.True(runtime.Catalog.TryResolve(id, out AlgorithmDescriptor? descriptor));
            Assert.NotNull(descriptor);
            Assert.False(runtime.CanExecuteDescriptor(
                descriptor,
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local));
            Assert.False(runtime.CanAttemptExecution(
                descriptor,
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local));
        }
    }

    [Fact]
    public void DefaultMenusHideExperimentalAlgorithmsAndKeepReleasedAlgorithmsVisible()
    {
        ImageView view = CreateImageView();
        try
        {
            WpfTestHost.Invoke(() =>
            {
                string?[] genericIds = new AlgorithmsContextMenu(view.EditorContext.ProcessingContext)
                    .GetContextMenuItems()
                    .Select(item => item.GuidId)
                    .ToArray();
                Assert.DoesNotContain("FrequencySpectrum", genericIds);
                Assert.DoesNotContain("MoireAnalysis", genericIds);
                Assert.Contains("GeometricTransform", genericIds);
                Assert.Contains("ImageRegistration", genericIds);
                Assert.Contains("LensDistortionCorrection", genericIds);
                Assert.Contains("ImagingCorrection", genericIds);

                AlgorithmId[] specializedIds = view.IEditorToolFactory.IIEditorToolContextMenus
                    .Cast<object>()
                    .Concat(view.IEditorToolFactory.ContextMenuProviders)
                    .OfType<IAlgorithmCatalogBoundMenu>()
                    .Select(menu => menu.AlgorithmId)
                    .ToArray();
                Assert.DoesNotContain(StandardAlgorithmIds.BlobComponents, specializedIds);
                Assert.DoesNotContain(StandardAlgorithmIds.Contours, specializedIds);
                Assert.DoesNotContain(StandardAlgorithmIds.SubpixelEdge, specializedIds);
                Assert.DoesNotContain(StandardAlgorithmIds.LineFit, specializedIds);
                Assert.DoesNotContain(StandardAlgorithmIds.CircleFit, specializedIds);
                Assert.Contains(StandardAlgorithmIds.RoiStatistics, specializedIds);
                Assert.Contains(StandardAlgorithmIds.ImageProfile, specializedIds);
                Assert.Contains(StandardAlgorithmIds.ImageComparison, specializedIds);
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Fact]
    public async Task DirectRunnerCallsReturnStructuredExperimentalRejection()
    {
        AlgorithmRuntime runtime = ImageAlgorithmPlatform.Runtime;
        foreach (AlgorithmId id in ExperimentalIds)
        {
            Assert.True(runtime.Catalog.TryResolve(id, out AlgorithmDescriptor? descriptor));
            using AlgorithmImageBuffer input = new(2, 2, 2, AlgorithmImageFormat.Gray8, [1, 2, 3, 4]);
            using AlgorithmResult result = await runtime.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = new AlgorithmInvocation
                {
                    AlgorithmId = id,
                    ParameterSchemaVersion = descriptor!.ParameterSchema.Version,
                    Parameters = descriptor.ParameterSchema.Defaults.Clone(),
                },
                Inputs = [new AlgorithmInput { Name = "source", Image = input }],
                RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            });

            Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
            AlgorithmFailure failure = Assert.Single(result.Failures);
            Assert.Equal("provider_unavailable", failure.Code);
            string availability = Assert.Contains("provider_dependency_unavailable", failure.Details!);
            Assert.Contains("algorithm_experimental", availability, StringComparison.Ordinal);
            Assert.Contains("release_validation_pending", availability, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ReleasedAlgorithmsRemainAvailableAndExecuteThroughTheDefaultRuntime()
    {
        AlgorithmRuntime runtime = ImageAlgorithmPlatform.Runtime;
        AssertAvailable(runtime, StandardAlgorithmIds.Invert, AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local);
        AssertAvailable(runtime, StandardAlgorithmIds.ImagingCorrection, AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local);
        AssertAvailable(runtime, StandardAlgorithmIds.RoiStatistics,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi);
        AssertAvailable(runtime, StandardAlgorithmIds.ImageProfile,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi);
        AssertAvailable(runtime, StandardAlgorithmIds.ImageComparison,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput);

        using AlgorithmImageBuffer input = new(2, 1, 2, AlgorithmImageFormat.Gray8, [1, 2]);
        using AlgorithmResult result = await runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Invert, new NoAlgorithmParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input }],
            RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageArtifact output = Assert.Single(result.Artifacts.OfType<AlgorithmImageArtifact>());
        Assert.Equal(new byte[] { 254, 253 }, output.Image.Data.ToArray());
    }

    private static void AssertAvailable(
        AlgorithmRuntime runtime,
        AlgorithmId id,
        AlgorithmHostCapabilities capabilities)
    {
        Assert.True(runtime.Catalog.TryResolve(id, out AlgorithmDescriptor? descriptor));
        Assert.True(runtime.CanExecuteDescriptor(descriptor!, capabilities));
    }

    private static ImageView CreateImageView()
        => WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new(ImageAlgorithmPlatform.Runtime);
            WriteableBitmap bitmap = new(1, 1, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 1 }, 1, 0);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });

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
}
