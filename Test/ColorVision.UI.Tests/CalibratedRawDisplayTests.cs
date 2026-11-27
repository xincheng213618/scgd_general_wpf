using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Layers;
using ColorVision.ImageEditor.Settings;
using System.Buffers.Binary;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CalibratedRawDisplayTests
{
    [Theory]
    [InlineData(8, true, true)]
    [InlineData(8, false, false)]
    [InlineData(16, true, false)]
    [InlineData(16, false, true)]
    public async Task CalibratedRawShowsTrueColorAndKeepsRawIdentity(int bpp, bool interleavedBgr, bool enableTrueColor)
    {
        const int width = 9, height = 7;
        const byte full = 100;
        string path = Path.Combine(Path.GetTempPath(), $"cv-raw-display-{Guid.NewGuid():N}.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(width, height, bpp, 3);
        Array.Clear(raw.Data);
        // Put red, green, and blue samples in three successive pixels, in the file's native layout.
        for (int channel = 0; channel < 3; channel++)
        {
            int sampleIndex = interleavedBgr ? channel * 3 + 2 - channel : channel * width * height + channel;
            if (bpp == 8) raw.Data[sampleIndex] = full;
            else BinaryPrimitives.WriteUInt16LittleEndian(raw.Data.AsSpan(sampleIndex * sizeof(ushort)), full);
        }
        Assert.True(CVFileUtil.WriteCIEFile(path, raw));
        RawColorTransformV1 transform = RawColorTransformV1.Create();
        transform.InterleavedBgr = interleavedBgr ? 1 : 0;
        transform.Coefficients = [0.4124564, 0.3575761, 0.1804375, 0.2126729, 0.7151522, 0.0721750, 0.0193339, 0.1191920, 0.9503041];
        ColorCalibrationSnapshot.Create(transform, width, height, bpp, raw.Exp, "display").Save(path, true);
        byte[] original = File.ReadAllBytes(path);
        IConfigService previous = ConfigService.Instance;
        ImageView? view = null;
        try
        {
            TestEnvironment.SetConfig(new ConfigHandler());
            view = WpfTestHost.Invoke(CreateView);
            WpfTestHost.Invoke(() =>
            {
                CvcieDisplayConfig.Current.EnableTrueColor = enableTrueColor;
                CvcieDisplayConfig.Current.BrightnessMode = CvcieBrightnessMode.ReferenceWhite;
                CvcieDisplayConfig.Current.ReferenceWhiteLuminance = full;
            });
            await DisplayAsync(view, () => view.OpenImage(path));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(enableTrueColor ? "cie-srgb" : "composite", view.SelectedLayer!.Id);
                if (enableTrueColor) AssertTrueColor(view);
                Assert.IsType<CVRawOpen>(view.EditorContext.IImageOpen);
                Assert.False(view.Config.GetProperties<bool>("IsCVCIE"));
                Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
                Assert.NotEmpty(view.EditorContext.ProcessingContext.ProfileMeasurementSources!);
                Assert.Equal(CVType.Raw, Assert.IsType<CVCIEFile>(view.Config.GetProperties<CVCIEFile>("meta")).FileExtType);
                ImageViewSettingsEntry information = CalibrationInformation(view);
                Assert.True(information.IsReadOnly);
                Assert.Null(information.Save);
                Assert.Equal(ImageSettingsCategories.Information, information.CategoryId);
                var rows = RawCalibrationInformationProvider.GetRows(Assert.IsType<ColorCalibrationSnapshot>(information.Source)).ToArray();
                Assert.Contains(rows, row => row.Value.StartsWith("[ 0.4124564, 0.3575761, 0.1804375 ]", StringComparison.Ordinal));
            });
            foreach (string id in new[] { "cie-x", "cie-y", "cie-z", "composite", "cie-srgb", "red", "cie-srgb" })
            {
                await DisplayAsync(view, () => view.ComboBoxLayers.SelectedItem = Assert.Single(view.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>(), layer => layer.Id == id));
                WpfTestHost.Invoke(() =>
                {
                    Assert.Equal(id, view.SelectedLayer!.Id);
                    if (id == "cie-srgb") AssertTrueColor(view);
                    Assert.Equal(path, view.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath));
                    Assert.False(view.Config.GetProperties<bool>("IsCVCIE"));
                    Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
                    Assert.NotEmpty(view.EditorContext.ProcessingContext.ProfileMeasurementSources!);
                    Assert.Equal("display", Assert.IsType<ColorCalibrationSnapshot>(CalibrationInformation(view).Source).Template);
                    Assert.Equal(enableTrueColor, CvcieDisplayConfig.Current.EnableTrueColor);
                });
            }
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            TestEnvironment.SetConfig(previous);
            File.Delete(path);
        }

        static void AssertTrueColor(ImageView view)
        {
            var bitmap = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
            Assert.Equal(PixelFormats.Bgr24, bitmap.Format);
            Assert.Equal(width, bitmap.PixelWidth);
            Assert.Equal(height, bitmap.PixelHeight);
            byte[] pixels = new byte[12];
            bitmap.CopyPixels(new Int32Rect(0, 0, 4, 1), pixels, pixels.Length, 0);
            Assert.Equal(new byte[] { 0, 0, 255, 0, 255, 0, 255, 0, 0, 0, 0, 0 }, pixels);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task DefaultTrueColorKeepsRawDisplayWhenXyzIsIncompleteOrCannotRender(int channels)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-raw-display-fallback-{Guid.NewGuid():N}.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(9, 7, 16, channels);
        RawColorTransformV1 transform = RawColorTransformV1.Create();
        transform.Channels = channels;
        transform.Kind = channels == 1 ? 2 : 0;
        transform.Coefficients[0] = channels == 1 ? 1 : double.MaxValue;
        Assert.True(CVFileUtil.WriteCIEFile(path, raw));
        ColorCalibrationSnapshot.Create(transform, raw.Cols, raw.Rows, raw.Bpp, raw.Exp, "display-fallback").Save(path, true);
        IConfigService previous = ConfigService.Instance;
        ImageView? view = null;
        try
        {
            TestEnvironment.SetConfig(new ConfigHandler());
            view = WpfTestHost.Invoke(CreateView);
            WpfTestHost.Invoke(() => CvcieDisplayConfig.Current.EnableTrueColor = true);
            await DisplayAsync(view, () => view.OpenImage(path));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal("composite", view.SelectedLayer!.Id);
                Assert.Equal(16 * channels, Assert.IsType<WriteableBitmap>(view.ViewBitmapSource).Format.BitsPerPixel);
                Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
                Assert.Equal(channels == 3, view.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>().Any(layer => layer.Id == "cie-srgb"));
            });
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            TestEnvironment.SetConfig(previous);
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0, 3, "[ 1, -0.25, 0.12345678901234566 ]\r\n[ 4, 5, 6 ]\r\n[ 7, 8, 9 ]")]
    [InlineData(1, 3, "a = 1\nb = -0.25\nc = 0.12345678901234566\nd = 4")]
    [InlineData(2, 1, "1")]
    public async Task NonReplayableCalibrationIsVisibleReadOnlyAndDoesNotSurviveAnotherFile(int kind, int channels, string coefficients)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-raw-information-{Guid.NewGuid():N}.cvraw");
        string plainPath = Path.Combine(Path.GetTempPath(), $"cv-raw-plain-{Guid.NewGuid():N}.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(9, 7, 16, channels);
        Assert.True(CVFileUtil.WriteCIEFile(path, raw));
        Assert.True(CVFileUtil.WriteCIEFile(plainPath, raw));
        RawColorTransformV1 transform = RawColorTransformV1.Create();
        transform.Kind = kind;
        transform.Channels = channels;
        transform.CalibrationType = kind == 2 ? 6 : kind == 1 ? 7 : 8;
        transform.Coefficients = [1, -0.25, 0.12345678901234566, 4, 5, 6, 7, 8, 9];
        ColorCalibrationSnapshot.Create(transform, 9, 7, 16, raw.Exp, "recorded-only").Save(path, false);
        byte[] original = File.ReadAllBytes(path);
        IConfigService previous = ConfigService.Instance;
        ImageView? view = null;
        try
        {
            TestEnvironment.SetConfig(new ConfigHandler());
            view = WpfTestHost.Invoke(CreateView);
            WpfTestHost.Invoke(() => CvcieDisplayConfig.Current.EnableTrueColor = true);
            await DisplayAsync(view, () => view.OpenImage(path));
            WpfTestHost.Invoke(() =>
            {
                Assert.False(view.Config.GetProperties<bool>("HasCieMeasurements"));
                Assert.Equal("composite", view.SelectedLayer!.Id);
                Assert.DoesNotContain(view.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>(), layer => layer.Id == "cie-srgb");
                ImageViewSettingsWindow window = new(view, ImageSettingsCategories.Information);
                try
                {
                    var content = Assert.IsType<ContentControl>(window.FindName("SettingsContent"));
                    TextBox[] values = Descendants(content).OfType<TextBox>().ToArray();
                    Assert.Contains(values, value => value.Text == coefficients);
                    Assert.Contains(values, value => value.Text == "recorded-only");
                    Assert.All(values, value => { Assert.True(value.IsReadOnly); Assert.True(value.IsEnabled); });
                }
                finally { window.Close(); }
            });
            await DisplayAsync(view, () => view.OpenImage(plainPath));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal("composite", view.SelectedLayer!.Id);
                Assert.DoesNotContain(view.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>(), layer => layer.Id == "cie-srgb");
                Assert.DoesNotContain(view.GetRegisteredSettings(), entry => entry.Id == "cvraw-color-calibration");
            });
            // A valid container with an invalid color record must keep opening as ordinary RAW.
            CVFileMetadata.SetProperty(plainPath, ColorCalibrationSnapshot.PropertyKind, 1, System.Text.Encoding.UTF8.GetBytes("{invalid}"));
            await DisplayAsync(view, () => view.RestoreOriginalImage());
            WpfTestHost.Invoke(() => Assert.DoesNotContain(view.GetRegisteredSettings(), entry => entry.Id == "cvraw-color-calibration"));
            await DisplayAsync(view, () => view.OpenImage(path));
            WpfTestHost.Invoke(() =>
            {
                Assert.NotNull(CalibrationInformation(view));
                view.Clear();
                Assert.DoesNotContain(view.GetRegisteredSettings(), entry => entry.Id == "cvraw-color-calibration");
            });
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            TestEnvironment.SetConfig(previous);
            File.Delete(path);
            File.Delete(plainPath);
        }
    }

    private static ImageViewSettingsEntry CalibrationInformation(ImageView view)
        => Assert.Single(view.GetRegisteredSettings(), entry => entry.Id == "cvraw-color-calibration");

    private static ImageView CreateView()
    {
        Application app = Application.Current;
        app.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        app.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        app.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        app.Resources["ToolBarImage"] = new Style(typeof(Image));
        app.Resources["BaseStyle"] = new Style(typeof(Control));
        app.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        app.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
        ImageView created = new();
        created.IEditorToolFactory.IEditorTools.Clear();
        return created;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (DependencyObject child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static async Task DisplayAsync(ImageView view, Action action)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args) => completion.TrySetResult();
        WpfTestHost.Invoke(() => { view.ImageSourceLoaded += OnLoaded; action(); });
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { WpfTestHost.Invoke(() => view.ImageSourceLoaded -= OnLoaded); }
    }
}
