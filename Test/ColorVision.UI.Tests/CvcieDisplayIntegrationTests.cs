using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Layers;
using ColorVision.UI;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CvcieDisplayIntegrationTests
{
    private static readonly byte[] RawPixels = [3, 17, 231, 220, 9, 25];
    private static readonly byte[] RedGreenPixels = [0, 0, 255, 0, 255, 0];

    [Fact]
    public async Task SourceDefaultPreservesAssociatedRawAndCompletesWithFinalState()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(true, Red(), Green());

        DisplayState completed = await fixture.OpenAsync();

        AssertOpened(fixture, completed, "composite");
        Assert.Equal(PixelFormats.Bgr24, completed.Format);
        Assert.Equal(RawPixels, completed.Pixels);
    }

    [Theory]
    [InlineData(CvcieBrightnessMode.Auto, 137, 255)]
    [InlineData(CvcieBrightnessMode.ReferenceWhite, 118, 221)]
    public async Task SrgbDefaultUsesEmbeddedXyzAndConfiguredBrightnessDespiteAssociatedRaw(
        CvcieBrightnessMode brightnessMode,
        byte firstPixel,
        byte secondPixel)
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Srgb, brightnessMode);
        fixture.WriteCie(true, White(0.18f), White(0.72f));

        DisplayState completed = await fixture.OpenAsync();

        AssertOpened(fixture, completed, "cie-srgb");
        Assert.Equal(PixelFormats.Bgr24, completed.Format);
        Assert.Equal(new[] { firstPixel, firstPixel, firstPixel, secondPixel, secondPixel, secondPixel }, completed.Pixels);
    }

    [Theory]
    [InlineData(CvcieDisplayMode.Source)]
    [InlineData(CvcieDisplayMode.Srgb)]
    public async Task TemporaryLayerSelectionSwitchesPixelsWithoutChangingGlobalDefaults(CvcieDisplayMode defaultMode)
    {
        using DisplayFixture fixture = new(defaultMode, CvcieBrightnessMode.ReferenceWhite);
        fixture.WriteCie(true, Red(), Green());
        DisplayState completed = await fixture.OpenAsync();
        AssertOpened(fixture, completed, defaultMode == CvcieDisplayMode.Srgb ? "cie-srgb" : "composite");

        string firstLayer = defaultMode == CvcieDisplayMode.Srgb ? "composite" : "cie-srgb";
        DisplayState first = await fixture.SelectLayerAsync(firstLayer);
        Assert.Equal(firstLayer, first.LayerId);
        Assert.Equal(firstLayer == "cie-srgb" ? RedGreenPixels : RawPixels, first.Pixels);

        string secondLayer = defaultMode == CvcieDisplayMode.Srgb ? "cie-srgb" : "composite";
        DisplayState second = await fixture.SelectLayerAsync(secondLayer);
        Assert.Equal(secondLayer, second.LayerId);
        Assert.Equal(secondLayer == "cie-srgb" ? RedGreenPixels : RawPixels, second.Pixels);

        WpfTestHost.Invoke(() =>
        {
            Assert.Equal(defaultMode, CvcieDisplayConfig.Current.DisplayMode);
            Assert.Equal(CvcieBrightnessMode.ReferenceWhite, CvcieDisplayConfig.Current.BrightnessMode);
            Assert.Equal(1, CvcieDisplayConfig.Current.ReferenceWhiteLuminance);
        });
    }

    [Fact]
    public async Task SourceWithoutAssociatedRawDisplaysEmbeddedYInsteadOfX()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(false, (0.8f, 0.2f, 0.5f), (0.1f, 0.8f, 0.2f));

        DisplayState completed = await fixture.OpenAsync();

        AssertOpened(fixture, completed, "cie-y");
        AssertLuminanceOrder(completed);
    }

    [Fact]
    public async Task CorruptAssociatedRawFallsBackToEmbeddedY()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(true, (0.8f, 0.2f, 0.5f), (0.1f, 0.8f, 0.2f));
        File.WriteAllBytes(Path.ChangeExtension(fixture.CiePath, ".cvraw"), [1, 2, 3]);

        DisplayState completed = await fixture.OpenAsync();

        AssertOpened(fixture, completed, "cie-y");
        AssertLuminanceOrder(completed);
    }

    [Fact]
    public async Task SourceReferenceThroughParentDirectoryToSameCieFallsBackToY()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(false, (0.8f, 0.2f, 0.5f), (0.1f, 0.8f, 0.2f));
        bool read = CVFileUtil.Read(fixture.CiePath, out CVCIEFile cie);
        using (cie)
        {
            Assert.True(read);
            string directory = Path.GetDirectoryName(fixture.CiePath)!;
            cie.SrcFileName = Path.Combine("..", Path.GetFileName(directory), Path.GetFileName(fixture.CiePath));
            Assert.Equal(fixture.CiePath, Path.GetFullPath(Path.Combine(directory, cie.SrcFileName)));
            Assert.True(CVFileUtil.WriteCIEFile(fixture.CiePath, cie));
        }

        DisplayState completed = await fixture.OpenAsync();

        AssertOpened(fixture, completed, "cie-y");
        AssertLuminanceOrder(completed);
    }

    [Fact]
    public async Task DoublePrecisionCieWithoutRawDisplaysYAsGray8()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(false, (0.8f, 0.2f, 0.5f), (0.1f, 0.8f, 0.2f));
        bool read = CVFileUtil.Read(fixture.CiePath, out CVCIEFile cie);
        using (cie)
        {
            Assert.True(read);
            double[] planes = [0.8, 0.1, 0.2, 0.8, 0.5, 0.2];
            cie.Bpp = 64;
            cie.Data = new byte[planes.Length * sizeof(double)];
            Buffer.BlockCopy(planes, 0, cie.Data, 0, cie.Data.Length);
            Assert.True(CVFileUtil.WriteCIEFile(fixture.CiePath, cie));
        }

        DisplayState completed = await fixture.OpenAsync();

        AssertOpened(fixture, completed, "cie-y");
        AssertLuminanceOrder(completed);
    }

    [Fact]
    public async Task SrgbWithoutAssociatedRawStillDisplaysEmbeddedXyz()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Srgb, CvcieBrightnessMode.ReferenceWhite);
        fixture.WriteCie(false, Red(), Green());

        DisplayState completed = await fixture.OpenAsync();

        AssertOpened(fixture, completed, "cie-srgb");
        Assert.Equal(PixelFormats.Bgr24, completed.Format);
        Assert.Equal(RedGreenPixels, completed.Pixels);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidXyzOnOpenFallsBackToRawOrEmbeddedY(bool hasRaw)
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Srgb);
        fixture.WriteCie(hasRaw, (float.NaN, 0.2f, 0.5f), (0.1f, 0.8f, 0.2f));

        DisplayState completed = await fixture.OpenAsync();

        AssertOpened(fixture, completed, hasRaw ? "composite" : "cie-y");
        if (hasRaw)
        {
            Assert.Equal(PixelFormats.Bgr24, completed.Format);
            Assert.Equal(RawPixels, completed.Pixels);
        }
        else
        {
            AssertLuminanceOrder(completed);
        }
        Assert.Equal(CvcieDisplayMode.Srgb, CvcieDisplayConfig.Current.DisplayMode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidXyzDuringTemporarySelectionFallsBackWithoutChangingGlobalDefault(bool hasRaw)
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(hasRaw, (float.NaN, 0.2f, 0.5f), (0.1f, 0.8f, 0.2f));
        await fixture.OpenAsync();

        DisplayState fallback = await fixture.SelectLayerAsync("cie-srgb");

        Assert.Equal(hasRaw ? "composite" : "cie-y", fallback.LayerId);
        if (hasRaw)
            Assert.Equal(RawPixels, fallback.Pixels);
        else
            AssertLuminanceOrder(fallback);
        Assert.Equal(CvcieDisplayMode.Source, CvcieDisplayConfig.Current.DisplayMode);
    }

    [Theory]
    [InlineData("cie-x", 255, 0)]
    [InlineData("cie-y", 0, 255)]
    [InlineData("cie-z", 0, 0)]
    public async Task XyzLayerSelectionDisplaysTheRequestedEmbeddedPlane(string layerId, byte firstPixel, byte secondPixel)
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(true, (0.8f, 0.2f, 0.5f), (0.1f, 0.8f, 0.5f));
        await fixture.OpenAsync();

        DisplayState selected = await fixture.SelectLayerAsync(layerId);

        Assert.Equal(layerId, selected.LayerId);
        Assert.Equal(PixelFormats.Gray8, selected.Format);
        Assert.Equal(new[] { firstPixel, secondPixel }, selected.Pixels);
    }

    [Fact]
    public async Task RapidLayerSelectionsOnlyPublishTheLastRequestedPlane()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(true, (0.8f, 0.2f, 0.5f), (0.1f, 0.8f, 0.5f));
        await fixture.OpenAsync();

        DisplayState selected = await fixture.SelectLayersAsync("cie-x", "cie-srgb", "cie-z", "cie-y");

        Assert.Equal("cie-y", selected.LayerId);
        Assert.Equal(PixelFormats.Gray8, selected.Format);
        Assert.Equal(new byte[] { 0, 255 }, selected.Pixels);
        var (current, completionCount) = await fixture.CaptureAfterPendingDispatchesAsync();
        Assert.Equal(2, completionCount);
        Assert.Equal(selected.Pixels, current.Pixels);
    }

    [Fact]
    public async Task OpeningAnotherImagePreventsPendingLayerFromReplacingIt()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source);
        fixture.WriteCie(true, Red(), Green());
        string replacementPath = fixture.WriteReplacementRaw();
        await fixture.OpenAsync();

        DisplayState replaced = await fixture.SelectLayerThenOpenAsync("cie-srgb", replacementPath);

        Assert.Equal(replacementPath, replaced.FilePath);
        Assert.Equal("composite", replaced.LayerId);
        Assert.Equal(PixelFormats.Gray8, replaced.Format);
        Assert.Equal(new byte[] { 35, 211 }, replaced.Pixels);
        Assert.False(replaced.IsCvcie);
        var (current, completionCount) = await fixture.CaptureAfterPendingDispatchesAsync();
        Assert.Equal(replacementPath, current.FilePath);
        Assert.Equal(replaced.Pixels, current.Pixels);
        Assert.Equal(2, completionCount);
    }

    [Fact]
    public async Task ChangingReferenceWhiteRecomputesSrgbWhenSelectedAgain()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Source, CvcieBrightnessMode.ReferenceWhite);
        fixture.WriteCie(true, White(1), White(0.25f));
        await fixture.OpenAsync();
        DisplayState first = await fixture.SelectLayerAsync("cie-srgb");
        Assert.Equal(new byte[] { 255, 255, 255, 137, 137, 137 }, first.Pixels);

        await fixture.SelectLayerAsync("composite");
        WpfTestHost.Invoke(() => CvcieDisplayConfig.Current.ReferenceWhiteLuminance = 4);
        DisplayState adjusted = await fixture.SelectLayerAsync("cie-srgb");

        Assert.Equal("cie-srgb", adjusted.LayerId);
        Assert.Equal(new byte[] { 137, 137, 137, 71, 71, 71 }, adjusted.Pixels);
        Assert.Equal(CvcieDisplayMode.Source, CvcieDisplayConfig.Current.DisplayMode);
    }

    [Fact]
    public async Task ReopeningSamePathAfterAnotherImageOnlyPublishesTheLatestBrightness()
    {
        using DisplayFixture fixture = new(CvcieDisplayMode.Srgb, CvcieBrightnessMode.ReferenceWhite);
        fixture.WriteCie(true, White(1), White(0.25f));
        string replacementPath = fixture.WriteReplacementRaw();

        DisplayState reopened = await fixture.OpenAwayThenReopenAsync(replacementPath, 4);

        AssertOpened(fixture, reopened, "cie-srgb");
        Assert.Equal(new byte[] { 137, 137, 137, 71, 71, 71 }, reopened.Pixels);
        var (current, completionCount) = await fixture.CaptureAfterPendingDispatchesAsync();
        Assert.Equal(fixture.CiePath, current.FilePath);
        Assert.Equal(reopened.Pixels, current.Pixels);
        Assert.Equal(1, completionCount);
    }

    [Fact]
    public void ConfigurationRoundTripPreservesSelectionsAndMissingValuesKeepSourceDefault()
    {
        CvcieDisplayConfig previousVersion = Assert.IsType<CvcieDisplayConfig>(JsonConvert.DeserializeObject<CvcieDisplayConfig>("{}"));
        Assert.Equal(CvcieDisplayMode.Source, previousVersion.DisplayMode);
        Assert.False(previousVersion.EnableTrueColor);
        Assert.Equal(CvcieBrightnessMode.Auto, previousVersion.BrightnessMode);
        Assert.Equal(65535, previousVersion.ReferenceWhiteLuminance);

        CvcieDisplayConfig configured = new()
        {
            EnableTrueColor = true,
            BrightnessMode = CvcieBrightnessMode.ReferenceWhite,
            ReferenceWhiteLuminance = 203.5,
        };
        string serialized = JsonConvert.SerializeObject(configured);
        CvcieDisplayConfig restored = Assert.IsType<CvcieDisplayConfig>(JsonConvert.DeserializeObject<CvcieDisplayConfig>(serialized));

        Assert.Equal(CvcieDisplayMode.Srgb, restored.DisplayMode);
        Assert.True(restored.EnableTrueColor);
        Assert.True(JsonConvert.DeserializeObject<CvcieDisplayConfig>("{\"DisplayMode\":1}")!.EnableTrueColor);
        Assert.Equal(CvcieBrightnessMode.ReferenceWhite, restored.BrightnessMode);
        Assert.Equal(203.5, restored.ReferenceWhiteLuminance);
    }

    private static void AssertOpened(DisplayFixture fixture, DisplayState completed, string layerId)
    {
        // Capture at ImageSourceLoaded, so a later metadata/layer correction cannot hide an early completion.
        Assert.Equal(layerId, completed.LayerId);
        Assert.Equal(fixture.CiePath, completed.FilePath);
        Assert.Equal(fixture.CiePath, completed.FileSource);
        Assert.Equal(Path.GetFileName(fixture.CiePath), completed.FileName);
        Assert.Equal(2, completed.Width);
        Assert.Equal(1, completed.Height);
        Assert.Equal(2, completed.MetadataWidth);
        Assert.Equal(1, completed.MetadataHeight);
        Assert.Equal(typeof(CVRawOpen), completed.OpenerType);
        Assert.True(completed.IsCvcie);
        Assert.Equal(1, WpfTestHost.Invoke(() => fixture.CompletionCount));
    }

    private static void AssertLuminanceOrder(DisplayState state)
    {
        Assert.Equal(PixelFormats.Gray8, state.Format);
        Assert.Equal(2, state.Pixels.Length);
        Assert.True(state.Pixels[0] < state.Pixels[1], "The first Y sample is darker; X is deliberately in the opposite order.");
    }

    private static (float X, float Y, float Z) Red() => (0.4124564f, 0.2126729f, 0.0193339f);
    private static (float X, float Y, float Z) Green() => (0.3575761f, 0.7151522f, 0.1191920f);
    private static (float X, float Y, float Z) White(float luminance) => (0.95047f * luminance, luminance, 1.08883f * luminance);

    private sealed class DisplayFixture : IDisposable
    {
        private readonly CvcieDisplayConfig _config;
        private readonly IConfigService? _previousConfigService;
        private readonly Hierarchy _logHierarchy;
        private readonly MemoryAppender _logAppender;
        private readonly Level? _previousRootLogLevel;
        private readonly bool _previousLogConfigured;
        private TaskCompletionSource<DisplayState>? _completion;
        private readonly ImageView _imageView;

        public DisplayFixture(CvcieDisplayMode displayMode, CvcieBrightnessMode brightnessMode = CvcieBrightnessMode.Auto)
        {
            string filePrefix = Path.Combine(Path.GetTempPath(), $"{nameof(CvcieDisplayIntegrationTests)}-{Guid.NewGuid():N}");
            CiePath = filePrefix + ".cvcie";
            RawPath = filePrefix + ".cvraw";
            ReplacementRawPath = filePrefix + "-replacement.cvraw";
            _previousConfigService = ConfigService.Instance;
            _logHierarchy = (Hierarchy)LogManager.GetRepository(typeof(CVRawOpen).Assembly);
            _previousRootLogLevel = _logHierarchy.Root.Level;
            _previousLogConfigured = _logHierarchy.Configured;
            _logAppender = new MemoryAppender { Name = $"CvcieDisplayTest-{Guid.NewGuid():N}", Threshold = Level.Warn };
            _logAppender.ActivateOptions();
            _logHierarchy.Root.AddAppender(_logAppender);
            _logHierarchy.Root.Level = Level.Warn;
            _logHierarchy.Configured = true;

            try
            {
                // A fresh handler supplies all opener settings without loading or saving user configuration.
                ConfigService.SetInstance(new ConfigHandler());
                _config = CvcieDisplayConfig.Current;
                _imageView = WpfTestHost.Invoke(() =>
                {
                    EnsureImageViewTestResources();
                    ImageView imageView = new();
                    // Toolbar regeneration needs a loaded visual tree; the file and layer routes do not.
                    imageView.IEditorToolFactory.IEditorTools.Clear();
                    _config.EnableTrueColor = displayMode == CvcieDisplayMode.Srgb;
                    _config.BrightnessMode = brightnessMode;
                    _config.ReferenceWhiteLuminance = 1;
                    imageView.ImageSourceLoaded += OnImageSourceLoaded;
                    return imageView;
                });
            }
            catch
            {
                RestoreGlobalState();
                throw;
            }
        }

        public string CiePath { get; }
        private string RawPath { get; }
        private string ReplacementRawPath { get; }
        public int CompletionCount { get; private set; }

        public void WriteCie(bool hasRaw, params (float X, float Y, float Z)[] pixels)
        {
            Assert.Equal(2, pixels.Length);
            float[] planes = new float[pixels.Length * 3];
            for (int index = 0; index < pixels.Length; index++)
            {
                planes[index] = pixels[index].X;
                planes[pixels.Length + index] = pixels[index].Y;
                planes[pixels.Length * 2 + index] = pixels[index].Z;
            }
            byte[] data = new byte[planes.Length * sizeof(float)];
            Buffer.BlockCopy(planes, 0, data, 0, data.Length);
            using CVCIEFile cie = new()
            {
                Version = 1,
                FileExtType = CVType.CIE,
                SrcFileName = Path.GetFileName(RawPath),
                Rows = 1,
                Cols = pixels.Length,
                Bpp = 32,
                Channels = 3,
                Gain = 3,
                Exp = [2f, 3f, 4f],
                Data = data,
            };
            Assert.True(CVFileUtil.WriteCIEFile(CiePath, cie));
            if (!hasRaw) return;

            using CVCIEFile raw = new()
            {
                Version = 1,
                FileExtType = CVType.Raw,
                Rows = 1,
                Cols = 2,
                Bpp = 8,
                Channels = 3,
                Gain = 1,
                Exp = [1f, 1f, 1f],
                Data = RawPixels.ToArray(),
            };
            Assert.True(CVFileUtil.WriteCIEFile(RawPath, raw));
        }

        public string WriteReplacementRaw()
        {
            using CVCIEFile raw = new()
            {
                Version = 1,
                FileExtType = CVType.Raw,
                Rows = 1,
                Cols = 2,
                Bpp = 8,
                Channels = 1,
                Gain = 1,
                Exp = [1f],
                Data = [35, 211],
            };
            Assert.True(CVFileUtil.WriteCIEFile(ReplacementRawPath, raw));
            return ReplacementRawPath;
        }

        public Task<DisplayState> OpenAsync()
            => WaitForDisplayAsync(() => _imageView.OpenImage(CiePath), $"open {CiePath}");

        public Task<DisplayState> SelectLayerAsync(string layerId)
            => WaitForDisplayAsync(() => SelectLayer(layerId), $"select {layerId}");

        public Task<DisplayState> SelectLayersAsync(params string[] layerIds)
            => WaitForDisplayAsync(() =>
            {
                // Keep the dispatcher occupied until every request is issued, so earlier workers cannot display in between.
                foreach (string layerId in layerIds) SelectLayer(layerId);
            }, $"select {string.Join(", ", layerIds)}");

        public Task<DisplayState> SelectLayerThenOpenAsync(string layerId, string filePath)
            => WaitForDisplayAsync(() =>
            {
                SelectLayer(layerId);
                _imageView.OpenImage(filePath);
            }, $"select {layerId} and open {filePath}");

        public Task<DisplayState> OpenAwayThenReopenAsync(string replacementPath, double referenceWhite)
            => WaitForDisplayAsync(() =>
            {
                _imageView.OpenImage(CiePath);
                _imageView.OpenImage(replacementPath);
                CvcieDisplayConfig.Current.ReferenceWhiteLuminance = referenceWhite;
                _imageView.OpenImage(CiePath);
            }, $"open {CiePath}, then {replacementPath}, then reopen with reference white {referenceWhite}");

        public async Task<(DisplayState State, int CompletionCount)> CaptureAfterPendingDispatchesAsync()
        {
            // Idle priorities can starve while ImageView schedules UI work. A normal-priority snapshot
            // checks the state after already queued normal continuations without requiring an idle app.
            DispatcherOperation<(DisplayState, int)> operation = _imageView.Dispatcher.InvokeAsync(
                () => (CaptureState(), CompletionCount), DispatcherPriority.Normal);
            try
            {
                return await operation.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                operation.Abort();
                throw CreateFailure("capture state after queued dispatcher work", ex);
            }
        }

        private async Task<DisplayState> WaitForDisplayAsync(Action action, string operation)
        {
            TaskCompletionSource<DisplayState> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            DispatcherOperation request = _imageView.Dispatcher.InvokeAsync(() =>
            {
                _completion = completion;
                action();
            }, DispatcherPriority.Normal);
            try
            {
                await request.Task.WaitAsync(TimeSpan.FromSeconds(10));
                return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                request.Abort();
                throw CreateFailure(operation, ex);
            }
            finally
            {
                Interlocked.CompareExchange(ref _completion, null, completion);
            }
        }

        private InvalidOperationException CreateFailure(string operation, Exception error)
        {
            string diagnostics = string.Join(Environment.NewLine, _logAppender.GetEvents().Select(entry =>
                $"{entry.Level} {entry.LoggerName}: {entry.RenderedMessage}{Environment.NewLine}{entry.GetExceptionString()}"));
            if (string.IsNullOrWhiteSpace(diagnostics)) diagnostics = "No warnings or errors were captured.";
            return new InvalidOperationException($"CVCIE display did not complete successfully ({operation}, completed frames: {CompletionCount}): {CiePath}{Environment.NewLine}{diagnostics}", error);
        }

        private void SelectLayer(string layerId)
        {
            ImageLayerDescriptor layer = Assert.Single(_imageView.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>(), item => item.Id == layerId);
            _imageView.ComboBoxLayers.SelectedItem = layer;
        }

        public DisplayState CaptureState()
        {
            BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(_imageView.ViewBitmapSource);
            int stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            return new DisplayState(
                source.Format, pixels, source.PixelWidth, source.PixelHeight, _imageView.SelectedLayer?.Id,
                _imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath),
                _imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FileSource),
                _imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FileName),
                _imageView.Config.GetProperties<int>(ImageViewPropertyKeys.ImageWidth),
                _imageView.Config.GetProperties<int>(ImageViewPropertyKeys.ImageHeight),
                _imageView.EditorContext.IImageOpen?.GetType(),
                _imageView.Config.GetProperties<bool>("IsCVCIE"));
        }

        private void OnImageSourceLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args)
        {
            CompletionCount++;
            try
            {
                _completion?.TrySetResult(CaptureState());
            }
            catch (Exception ex)
            {
                _completion?.TrySetException(ex);
            }
        }

        public void Dispose()
        {
            DispatcherOperation cleanup = _imageView.Dispatcher.InvokeAsync(() =>
            {
                _imageView.ImageSourceLoaded -= OnImageSourceLoaded;
                _imageView.Dispose();
            }, DispatcherPriority.Normal);
            try
            {
                // A failed UI wait must not hang again while unwinding the fixture.
                cleanup.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                cleanup.Abort();
                throw CreateFailure("dispose image view", ex);
            }
            finally
            {
                RestoreGlobalState();
                if (File.Exists(CiePath)) File.Delete(CiePath);
                if (File.Exists(RawPath)) File.Delete(RawPath);
                if (File.Exists(ReplacementRawPath)) File.Delete(ReplacementRawPath);
            }
        }

        private void RestoreGlobalState()
        {
            ConfigService.SetInstance(_previousConfigService!);
            _logHierarchy.Root.RemoveAppender(_logAppender);
            _logHierarchy.Root.Level = _previousRootLogLevel;
            _logHierarchy.Configured = _previousLogConfigured;
            _logAppender.Close();
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

    private sealed record DisplayState(
        PixelFormat Format,
        byte[] Pixels,
        int Width,
        int Height,
        string? LayerId,
        string? FilePath,
        string? FileSource,
        string? FileName,
        int MetadataWidth,
        int MetadataHeight,
        Type? OpenerType,
        bool IsCvcie);
}
