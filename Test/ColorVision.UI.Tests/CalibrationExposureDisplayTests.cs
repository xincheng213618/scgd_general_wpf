using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Tif;
using ColorVision.Themes;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

/// <summary>Protects file exposure provenance, template visibility and manual fallback.</summary>
public sealed class CalibrationExposureDisplayTests
{
    [Theory]
    [InlineData("png")]
    [InlineData("tif")]
    [InlineData("tiff")]
    [InlineData("cvraw")]
    [InlineData("cvcie")]
    public void RecordedRgbExposureIsVisibleAndUsedWithoutClampingOrChangingManualMode(string extension)
    {
        WpfTestHost.Invoke(() =>
        {
            using Scope scope = new();
            string path = scope.CreateFile(extension, new() { Exposure = [0.25f, 22.5f, 9000] });
            scope.View.ImageFile.Text = path;
            Assert.Equal(Visibility.Visible, scope.View.ExposurePanel.Visibility);
            Assert.Equal(Visibility.Visible, scope.View.RgbExposurePanel.Visibility);
            Assert.True(scope.View.TextBoxExpR.IsReadOnly);
            Assert.False(scope.View.SliderExpR.IsEnabled);
            Assert.Equal(0.25, scope.Device.DisplayConfig.ExpTimeR);
            Assert.Equal(22.5, scope.Device.DisplayConfig.ExpTimeG);
            Assert.Equal(9000, scope.Device.DisplayConfig.ExpTimeB);
            Assert.False(scope.Device.DisplayConfig.IsAdvancedExposure);
            Assert.Equal(new float[] { 0.25f, 22.5f, 9000 }, Values(scope.View));
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });
    }

    [Fact]
    public void BasicTemplatesHideExposureAndColorSlotChangesRefreshWithoutChangingTheFile()
    {
        WpfTestHost.Invoke(() =>
        {
            using Scope scope = new();
            Assert.Equal(Visibility.Collapsed, scope.View.ExposurePanel.Visibility);
            scope.View.ImageFile.Text = scope.CreateFile("png", new() { Exposure = [12, 24, 36] });
            Assert.Equal(Visibility.Visible, scope.View.ExposurePanel.Visibility);
            scope.View.ComboxCalibrationTemplate.SelectedIndex = 1;
            Assert.Equal(Visibility.Collapsed, scope.View.ExposurePanel.Visibility);
            scope.Color.Color.LumFourColor.IsSelected = false;
            Assert.Equal(Visibility.Collapsed, scope.View.ExposurePanel.Visibility);
            foreach (var slot in new[] { scope.Basic.Color.Luminance, scope.Basic.Color.LumOneColor, scope.Basic.Color.LumFourColor, scope.Basic.Color.LumMultiColor })
            {
                slot.IsSelected = true;
                Assert.Equal(Visibility.Visible, scope.View.ExposurePanel.Visibility);
                slot.IsSelected = false;
                Assert.Equal(Visibility.Collapsed, scope.View.ExposurePanel.Visibility);
            }
            scope.Basic.Normal.ColorShift.IsSelected = true;
            Assert.Equal(Visibility.Collapsed, scope.View.ExposurePanel.Visibility);
            scope.View.ComboxCalibrationTemplate.SelectedIndex = -1;
            Assert.Equal(Visibility.Collapsed, scope.View.ExposurePanel.Visibility);
        });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("future")]
    [InlineData("corrupt")]
    [InlineData("unknown-unit")]
    public void MissingOrInvalidExposureReturnsToEditableManualValues(string reason)
    {
        WpfTestHost.Invoke(() =>
        {
            using Scope scope = new();
            scope.View.ImageFile.Text = scope.CreateFile("png", new() { Exposure = [12, 24, 36] });
            ColorVisionTiffParameters? record = reason switch
            {
                "invalid" => new() { Exposure = [10, 0, 30] },
                "future" => new() { Schema = "ColorVision.CVImage/999", Exposure = [10, 20, 30] },
                "unknown-unit" => new() { Acquisition = new() { Exposure = [10], ExposureUnit = "unknown" } },
                _ => null
            };
            string path = scope.CreateFile("png", record);
            if (reason == "corrupt") File.WriteAllText(path, "not a PNG");
            scope.View.ImageFile.Text = path;
            Assert.Equal(Visibility.Visible, scope.View.ExposurePanel.Visibility);
            Assert.Equal(Visibility.Visible, scope.View.SingleExposurePanel.Visibility);
            Assert.False(scope.View.TextBoxExp.IsReadOnly);
            Assert.True(scope.View.SliderExp.IsEnabled);
            scope.Device.DisplayConfig.ExpTimeR = 77;
            Assert.Equal(new float[] { 77, 77, 77 }, Values(scope.View));
            scope.Device.DisplayConfig.IsAdvancedExposure = true;
            Assert.Equal(Visibility.Visible, scope.View.RgbExposurePanel.Visibility);
            scope.View.ImageFile.Text = "";
            Assert.Equal(Visibility.Collapsed, scope.View.ExposurePanel.Visibility);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleExposureAndAcquisitionFallbackRemainInformational(bool acquisitionOnly)
    {
        WpfTestHost.Invoke(() =>
        {
            using Scope scope = new();
            ColorVisionTiffParameters record = acquisitionOnly
                ? new() { Acquisition = new() { Exposure = [2.5f], ExposureUnit = "ms" } }
                : new() { Schema = "ColorVision.CVImage/1", Exposure = [2.5f] };
            record.PixelRole = "display";
            string path = scope.CreateFile("tiff", record);
            scope.View.ImageFile.Text = path;
            Assert.Equal(Visibility.Visible, scope.View.SingleExposurePanel.Visibility);
            Assert.True(scope.View.TextBoxExp.IsReadOnly);
            Assert.Equal(new float[] { 2.5f, 2.5f, 2.5f }, Values(scope.View));
            Assert.True(ColorVisionTiffParameters.TryReadFile(path, out var read));
            Assert.Equal("display", read!.PixelRole);
            Assert.Null(read.Calibration);
        });
    }

    private static float[] Values(DisplayCalibration view) => (float[])typeof(DisplayCalibration)
        .GetMethod("GetExposureValues", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null)!;

    private sealed class Scope : IDisposable
    {
        private readonly IConfigService _previousConfig = ConfigService.Instance;
        private readonly List<ResourceDictionary> _dictionaries = [];
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"cv-calibration-exposure-{Guid.NewGuid():N}");
        public DeviceCalibration Device { get; }
        public DisplayCalibration View { get; }
        public CalibrationParam Color { get; } = new();
        public CalibrationParam Basic { get; } = new();

        public Scope()
        {
            Directory.CreateDirectory(_root);
            ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                ResourceDictionary dictionary = new() { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                _dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
            Device = new DeviceCalibration(new SysResourceModel { Id = -2, Code = $"exposure-{Guid.NewGuid():N}", Name = "Exposure test", Value = "{}" });
            View = (DisplayCalibration)Device.GetDisplayControl();
            Device.DisplayConfig.IsAdvancedExposure = false;
            Color.Color.LumFourColor.IsSelected = true;
            Basic.Normal.DarkNoise.IsSelected = true;
            View.ComboxCalibrationTemplate.ItemsSource = new[]
            {
                new TemplateModel<CalibrationParam>("Basic name, color selected", Color),
                new TemplateModel<CalibrationParam>("LumFourColor name, basic selected", Basic)
            };
            View.ComboxCalibrationTemplate.SelectedIndex = 0;
        }

        public string CreateFile(string extension, ColorVisionTiffParameters? record)
        {
            string path = Path.Combine(_root, $"input-{Guid.NewGuid():N}.{extension}");
            if (extension is "cvraw" or "cvcie")
            {
                using CVCIEFile raw = new() { Version = 1, Cols = 1, Rows = 1, Bpp = 16, Channels = 3, FileExtType = extension == "cvraw" ? CVType.Raw : CVType.CIE, Exp = record?.Exposure ?? [], Data = [1, 0, 2, 0, 3, 0] };
                Assert.True(CVFileUtil.WriteCIEFile(path, raw));
            }
            else
            {
                BitmapSource pixels = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray16, null, new ushort[] { 123 }, 2);
                CalibratedImageExport.Save(pixels, record, path);
            }
            return path;
        }

        public void Dispose()
        {
            View.Dispose(); Device.Dispose();
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            ConfigService.SetInstance(_previousConfig);
            foreach (string path in Directory.EnumerateFiles(_root)) File.Delete(path);
            Directory.Delete(_root);
        }
    }
}
