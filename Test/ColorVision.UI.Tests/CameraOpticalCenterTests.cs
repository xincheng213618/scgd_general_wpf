using ColorVision.Core;
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Controls;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Types;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Tif;
using ColorVision.Themes;
using ColorVision.UI;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

/// <summary>Protects camera-level principal-point ownership, import precedence and reference coordinates.</summary>
public sealed class CameraOpticalCenterTests
{
    [Fact]
    public void LastValidDistortionWinsAndSurvivesConfigurationReload()
    {
        var config = new ConfigPhyCamera();
        Assert.Null(config.CameraCfg.GetOpticalCenter());
        foreach (string json in new[]
        {
            "\uFEFF" + """{"cameraMatrix":[3917,0,4798.1293219167246,0,3919,3169.9010318641335,0,0,1]}""",
            """{"cameraMatrix":[500,0,3110.25,0,500,2074.5,0,0,1]}"""
        }) Assert.True(config.CameraCfg.TryImportOpticalCenter(json));
        var reloaded = JsonConvert.DeserializeObject<ConfigPhyCamera>(JsonConvert.SerializeObject(config))!;
        Assert.Equal(new ImageOpticalCenter(3110.25, 2074.5), reloaded.CameraCfg.GetOpticalCenter());
        ImageOpticalCenter snapshot = reloaded.CameraCfg.GetOpticalCenter()!;
        reloaded.CameraCfg.OpticalCenterX = 12;
        Assert.Equal(3110.25, snapshot.X);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{}")]
    [InlineData("{\"cameraMatrix\":[1,2]}")]
    [InlineData("{\"cameraMatrix\":[1,0,-1,0,1,2,0,0,1]}")]
    [InlineData("{\"cameraMatrix\":[1,0,\"3\",0,1,2,0,0,1]}")]
    [InlineData("{\"cameraMatrix\":[1,0,3,0,1,NaN,0,0,1]}")]
    public void InvalidDistortionPreservesBothExistingCoordinates(string json)
    {
        var config = new PhyCameraCfg { OpticalCenterX = 100.25, OpticalCenterY = 200.5 };
        Assert.False(config.TryImportOpticalCenter(json));
        Assert.Equal(new ImageOpticalCenter(100.25, 200.5), config.GetOpticalCenter());
    }

    [Fact]
    public void OldDisplayCenterOverridesAreNoLongerSettings()
    {
        var display = JsonConvert.DeserializeObject<DisplayCameraConfig>("""{"CrossGuideStandardCenterX":123,"CrossGuideStandardCenterY":456}""")!;
        Assert.Null(typeof(DisplayCameraConfig).GetProperty("CrossGuideStandardCenterX"));
        Assert.Null(typeof(DisplayCameraConfig).GetProperty("CrossGuideStandardCenterY"));
        Assert.DoesNotContain("CrossGuideStandardCenterX", JsonConvert.SerializeObject(display));
    }

    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"OpticalCenterX\":12,\"OpticalCenterY\":34}", false)]
    [InlineData("{\"OpticalCenterX\":null,\"OpticalCenterY\":null}", true)]
    [InlineData("{\"OpticalCenterX\":12}", true)]
    public void PhysicalCameraResolvesMissingCenterAndRefreshesImportedFilesWithoutAView(string json, bool shouldRead)
    {
        using var scope = new PhysicalCameraScope(JsonConvert.DeserializeObject<PhyCameraCfg>(json)!);
        string first = scope.AddDistortion("first.dat", 111, 222);
        string last = scope.AddDistortion("last.dat", 333.25, 444.5);
        int syncCalls = 0, notifications = 0;
        scope.Camera.ConfigChanged += (_, _) => syncCalls++;
        scope.Camera.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PhyCamera.OpticalCenter)) notifications++; };

        ImageOpticalCenter expected = shouldRead ? new(333.25, 444.5) : new(12, 34);
        Assert.Equal(expected, scope.Camera.OpticalCenter);
        Assert.Equal(expected, scope.ReadSavedCenter());
        // An explicit upload refresh follows the imported order and overrides an existing manual value.
        scope.Camera.RefreshOpticalCenter([last, first]);
        Assert.Equal(new ImageOpticalCenter(111, 222), scope.Camera.OpticalCenter);
        Assert.Equal(scope.Camera.OpticalCenter, scope.ReadSavedCenter());
        File.WriteAllText(last, "{invalid");
        scope.Camera.RefreshOpticalCenter([last, Path.Combine(scope.Root, "missing.dat")]);
        Assert.Equal(new ImageOpticalCenter(111, 222), scope.Camera.OpticalCenter);
        Assert.Equal(3, notifications);
        Assert.Equal(0, syncCalls);
    }

    [Fact]
    public void MissingDistortionKeepsCenterAbsentUntilFilesAreImportedAndRefreshed()
    {
        using var scope = new PhysicalCameraScope(new());
        Assert.Null(scope.Camera.OpticalCenter);
        string file = scope.AddDistortion("new.dat", 0, 15.5);
        scope.Camera.RefreshOpticalCenter([file]);
        Assert.Equal(new ImageOpticalCenter(0, 15.5), scope.Camera.OpticalCenter);
        Assert.Equal(scope.Camera.OpticalCenter, scope.ReadSavedCenter());
    }

    [Fact]
    public void OpticalCenterFieldsUseStandardEditorAndBindDirectlyToPhysicalCamera()
    {
        WpfTestHost.Invoke(() =>
        {
            List<ResourceDictionary> dictionaries = [];
            try
            {
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                var config = new PhyCameraCfg { OpticalCenterX = 123.25, OpticalCenterY = 456.5 };
                var panel = PropertyEditorHelper.GenProperties(config, nameof(PhyCameraCfg.OpticalCenterX));
                var field = Assert.Single(panel.Children.OfType<TextBox>());
                field.SetCurrentValue(TextBox.TextProperty, "789.125");
                field.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal(789.125, config.OpticalCenterX);
                field.SetCurrentValue(TextBox.TextProperty, string.Empty);
                field.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Null(config.OpticalCenterX);
                Assert.Null(config.GetOpticalCenter());

                using var scope = new PhysicalCameraScope(new());
                var editCommand = new RelayCommand(_ => { });
                var preview = new CameraPreviewControl
                {
                    DataContext = new { PhyCamera = scope.Camera, DisplayConfig = new DisplayCameraConfig(), EditPhyCameraConfigCommand = editCommand }
                };
                preview.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                var x = (TextBox)preview.FindName("OpticalCenterX");
                var y = (TextBox)preview.FindName("OpticalCenterY");
                x.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
                y.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
                Assert.False(string.IsNullOrWhiteSpace(x.Text));
                Assert.Equal("PhyCamera.OpticalCenter.X", x.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
                var editButton = (Button)preview.FindName("EditCameraConfigButton");
                editButton.GetBindingExpression(Button.CommandProperty)!.UpdateTarget();
                Assert.Same(editCommand, editButton.Command);
                scope.Camera.RefreshOpticalCenter([scope.AddDistortion("ui.dat", 123.25, 456.5)]);
                preview.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                x.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
                y.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
                Assert.Equal(123.25, double.Parse(x.Text, System.Globalization.CultureInfo.CurrentCulture));
                Assert.Equal(456.5, double.Parse(y.Text, System.Globalization.CultureInfo.CurrentCulture));
                Assert.True(x.IsReadOnly);
            }
            finally
            {
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RealtimeFramePresenter.TransformFlipX)]
    [InlineData(RealtimeFramePresenter.TransformFlipY)]
    [InlineData(RealtimeFramePresenter.TransformFlipX | RealtimeFramePresenter.TransformFlipY)]
    public void CrossDetectionRetainsZeroPrincipalPointAndTransformsItWithTheImage(int transform)
    {
        const int width = 64, height = 48;
        byte[] pixels = new byte[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (x == 20 || y == 25) pixels[y * width + x] = 255;
        IntPtr data = Marshal.AllocCoTaskMem(pixels.Length);
        try
        {
            Marshal.Copy(pixels, 0, data, pixels.Length);
            HImage frame = new() { rows = height, cols = width, channels = 1, depth = 8, stride = width, pData = data };
            Point expected = VideoCrossGuideDetector.TransformPointToDisplay(new Point(0, 10.25), width, height, transform);
            var roi = new RoiRect(0, 0, width, height);
            var request = new VideoCrossGuideRequest(roi, expected, transform, 300, 0.45, 0.02, 3);
            var result = VideoCrossGuideDetector.ProcessFrame(frame, request, width, height, roi);
            Assert.True(result.Found);
            Assert.Equal(expected, result.StandardCenter);
            Point cross = VideoCrossGuideDetector.TransformPointToDisplay(new Point(20, 25), width, height, transform);
            Assert.Equal(cross.X - expected.X, result.OffsetX, 8);
            Assert.Equal(cross.Y - expected.Y, result.OffsetY, 8);
        }
        finally { Marshal.FreeCoTaskMem(data); }
    }

    private sealed class PhysicalCameraScope : IDisposable
    {
        private readonly SysResourceDao previous = SysResourceDao.Instance;
        public string Root { get; } = Directory.CreateTempSubdirectory("cv-optical-").FullName;
        public PhyCamera Camera { get; }

        public PhysicalCameraScope(PhyCameraCfg config)
        {
            SysResourceDao.Instance = new SysResourceDao(new LocalTemplateStore(Path.Combine(Root, "config.db")), () => false);
            var physical = new ConfigPhyCamera { CameraID = "test-camera", Code = "CAM-OPTICAL", CameraCfg = config };
            physical.FileServerCfg.FileBasePath = Root;
            var row = new SysResourceModel { Code = physical.Code, Type = (int)ServiceTypes.PhyCamera, Value = JsonConvert.SerializeObject(physical) };
            Assert.Equal(1, SysResourceDao.Instance.Save(row));
            // Refresh requires only the physical model and storage, with no UI, services, SDK or View initialization.
            Camera = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
            Camera.SysResourceModel = row;
            Camera.Config = physical;
            Camera.VisualChildren = [];
            Directory.CreateDirectory(Path.Combine(Root, Camera.Code, "cfg"));
        }

        public string AddDistortion(string name, double x, double y)
        {
            string path = Path.Combine(Root, Camera.Code, "cfg", name);
            File.WriteAllText(path, JsonConvert.SerializeObject(new { cameraMatrix = new[] { 500, 0, x, 0, 500, y, 0, 0, 1 } }));
            var resource = (CalibrationResource)RuntimeHelpers.GetUninitializedObject(typeof(CalibrationResource));
            resource.SysResourceModel = new SysResourceModel { Id = Camera.VisualChildren.Count + 1, Type = (int)ServiceTypes.Distortion, Value = name };
            resource.Parent = Camera;
            Camera.VisualChildren.Add(resource);
            return path;
        }

        public ImageOpticalCenter? ReadSavedCenter()
            => JsonConvert.DeserializeObject<ConfigPhyCamera>(SysResourceDao.Instance.GetById(Camera.Id)!.Value!)!.CameraCfg.GetOpticalCenter();

        public void Dispose()
        {
            SysResourceDao.Instance = previous;
            Directory.Delete(Root, true);
        }
    }
}
