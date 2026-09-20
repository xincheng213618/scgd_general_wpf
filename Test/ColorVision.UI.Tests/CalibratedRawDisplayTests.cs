using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Layers;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class CalibratedRawDisplayTests
{
    [Fact]
    public async Task CalibratedRawKeepsIdentitySupportsChannelsAndNeverAddsSrgbOrWritesOnOpen()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-raw-display-{Guid.NewGuid():N}.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(9, 7, 16, 3);
        Assert.True(CVFileUtil.WriteCIEFile(path, raw));
        RawColorTransformV1 transform = RawColorTransformV1.Create();
        transform.Coefficients = [1, 0, 0, 0, 1, 0, 0, 0, 1];
        ColorCalibrationSnapshot.Create(transform, 9, 7, 16, raw.Exp, "display").Save(path, true);
        byte[] original = File.ReadAllBytes(path);
        IConfigService previous = ConfigService.Instance;
        ImageView? view = null;
        try
        {
            ConfigService.SetInstance(new ConfigHandler());
            view = WpfTestHost.Invoke(() =>
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
                CvcieDisplayConfig.Current.EnableTrueColor = true;
                return created;
            });
            await DisplayAsync(view, () => view.OpenImage(path));
            WpfTestHost.Invoke(() =>
            {
                Assert.IsType<CVRawOpen>(view.EditorContext.IImageOpen);
                Assert.False(view.Config.GetProperties<bool>("IsCVCIE"));
                Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
                Assert.NotEmpty(view.EditorContext.ProcessingContext.ProfileMeasurementSources!);
                Assert.Equal(CVType.Raw, view.Config.GetProperties<CVCIEFile>("meta").FileExtType);
                Assert.DoesNotContain(view.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>(), layer => layer.Id == "cie-srgb");
            });
            foreach (string id in new[] { "cie-x", "cie-y", "cie-z", "composite", "cie-y", "red" })
            {
                await DisplayAsync(view, () => view.ComboBoxLayers.SelectedItem = Assert.Single(view.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>(), layer => layer.Id == id));
                WpfTestHost.Invoke(() =>
                {
                    Assert.Equal(id, view.SelectedLayer!.Id);
                    Assert.Equal(path, view.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath));
                    Assert.False(view.Config.GetProperties<bool>("IsCVCIE"));
                    Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
                    Assert.NotEmpty(view.EditorContext.ProcessingContext.ProfileMeasurementSources!);
                });
            }
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            ConfigService.SetInstance(previous);
            File.Delete(path);
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
