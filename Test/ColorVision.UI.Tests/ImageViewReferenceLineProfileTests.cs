using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageViewReferenceLineProfileTests
{
    private const string DeviceCode = "legacy-reference-camera";
    private const string CameraProfile = "Camera:" + DeviceCode;
    private const string LegacyPropertyName = "ReferenceLineParam";

    [Fact]
    public void CameraUsesIndependentProfileAndKeepsWindowReferenceLineState()
    {
        WithConfig(handler =>
        {
            SeedLegacyCamera(handler);
            handler = ReloadConfig(handler.ConfigFilePath);
            JObject legacy = ReadLegacyCameraOptions(handler.ConfigFilePath);

            using (var camera = CreateCamera())
            using (var otherHost = new ImageView { ConfigurationCode = "Calibration:" + DeviceCode })
            {
                ImageView imageView = camera.View.ImageView;
                Assert.Equal(CameraProfile, imageView.ConfigurationCode);
                Assert.False(camera.DisplayConfig.UseLocalCamera);
                var referenceLineEntry = Assert.Single(imageView.GetRegisteredSettings(),
                    entry => entry.Id == ToolReferenceLine.ProfileToolId);
                Assert.Same(Options(imageView), referenceLineEntry.Source);
                var defaults = (ReferenceLineMode.SimpleCross, 0d, 0d, 0d, false);
                Assert.Equal(defaults, Values(Options(imageView)));
                Assert.Equal(defaults, Values(Options(otherHost)));
                Assert.Null(handler.ReadOwnedSection("ImageEditor.ViewProfiles"));

                var firstImage = new WriteableBitmap(160, 120, 96, 96, PixelFormats.Gray8, null);
                imageView.SetImageSource(firstImage);
                Assert.Same(firstImage, imageView.ViewBitmapSource);
                ToolReferenceLine tool = Tool(imageView);
                tool.IsChecked = true;
                AssertReferenceLineVisible(imageView, true);
                var updated = (ReferenceLineMode.DoubleCross, 34d, 78d, 16d, true);
                Edit(imageView, (window, draft) =>
                {
                    (draft.Mode, draft.PointX, draft.PointY, draft.Angle, draft.IsMapping) = updated;
                    draft.Brush = Brushes.DodgerBlue;
                    Click(window, "ConfirmButton");
                });
                Assert.Same(referenceLineEntry.Source, Options(imageView));
                Assert.Equal(updated, Values(Options(imageView)));
                Assert.Equal(Colors.DodgerBlue, LineColor(imageView));

                imageView.SetDisplayLayerSource(new WriteableBitmap(firstImage));
                AssertReferenceLineVisible(imageView, true);
                imageView.CommitSourcePixels(new WriteableBitmap(firstImage));
                AssertReferenceLineVisible(imageView, true);

                var nextImage = new WriteableBitmap(170, 130, 96, 96, PixelFormats.Gray8, null);
                imageView.SetImageSource(nextImage);
                Assert.Same(nextImage, imageView.ViewBitmapSource);
                AssertReferenceLineVisible(imageView, true);
                Assert.Equal(CameraProfile, imageView.ConfigurationCode);
                Assert.Equal(updated, Values(Options(imageView)));
                Assert.Equal(Colors.DodgerBlue, LineColor(imageView));
                imageView.Clear();
                Assert.True(tool.IsChecked);
                tool.IsChecked = false;
                imageView.SetImageSource(firstImage);
                AssertReferenceLineVisible(imageView, false);
                imageView.SetImageSource(nextImage);
                AssertReferenceLineVisible(imageView, false);
                Assert.Equal(defaults, Values(Options(otherHost)));
                SaveOptions(otherHost, ReferenceLineMode.DiagonalCross, 30d);
                Assert.Equal(updated, Values(Options(imageView)));
            }

            Assert.True(JToken.DeepEquals(legacy, ReadLegacyCameraOptions(handler.ConfigFilePath)));
            handler.GetRequiredService<DisplayConfigManager>().GetDisplayConfig<DisplayCameraConfig>(DeviceCode).TakePictureDelay = 45;
            handler.SaveConfigs();
            ReloadConfig(handler.ConfigFilePath);
            using var reopened = CreateCamera();
            Assert.Equal((ReferenceLineMode.DoubleCross, 34d, 78d, 16d, true), Values(Options(reopened.View.ImageView)));
            Assert.Equal(Colors.DodgerBlue, LineColor(reopened.View.ImageView));
            Assert.False(reopened.DisplayConfig.UseLocalCamera);
            Assert.Equal(45d, reopened.DisplayConfig.TakePictureDelay);
            using var reopenedOtherHost = new ImageView { ConfigurationCode = "Calibration:" + DeviceCode };
            Assert.Equal((ReferenceLineMode.DiagonalCross, 30d, 0d, 0d, false), Values(Options(reopenedOtherHost)));
        });
    }

    [Fact]
    public void ResetProfileInheritsFutureGlobalChanges()
    {
        WithConfig(handler =>
        {
            using (var globalView = new ImageView())
            using (var camera = CreateCamera())
            {
                SaveOptions(globalView, ReferenceLineMode.DiagonalCross, 41d);
                ImageView imageView = camera.View.ImageView;
                Assert.Equal((ReferenceLineMode.DiagonalCross, 41d, 0d, 0d, false), Values(Options(imageView)));
                SaveOptions(imageView, ReferenceLineMode.DoubleCross, 55d);
                Assert.True(imageView.TryResetProfileOptions(ToolReferenceLine.ProfileToolId, out string error), error);
            }

            handler = ReloadConfig(handler.ConfigFilePath);
            using (var camera = CreateCamera())
            {
                Assert.Equal((ReferenceLineMode.DiagonalCross, 41d, 0d, 0d, false), Values(Options(camera.View.ImageView)));
            }

            using (var globalView = new ImageView())
                SaveOptions(globalView, ReferenceLineMode.ConcentricCircles, 62d);

            ReloadConfig(handler.ConfigFilePath);
            using var reopened = CreateCamera();
            Assert.Equal((ReferenceLineMode.ConcentricCircles, 62d, 0d, 0d, false), Values(Options(reopened.View.ImageView)));
        });
    }

    [Fact]
    public void CanceledAndFailedEditsLeaveTheAppliedAndSavedOptionsUnchanged()
    {
        WithConfig(handler =>
        {
            using var imageView = new ImageView { ConfigurationCode = CameraProfile };
            SaveOptions(imageView, ReferenceLineMode.DoubleCross, 81d);
            var original = Values(Options(imageView));
            string path = handler.ConfigFilePath;
            string savedJson = File.ReadAllText(path);

            Edit(imageView, (window, draft) =>
            {
                draft.PointX = 10;
                Click(window, "CancelButton");
            });
            Assert.Equal(original, Values(Options(imageView)));
            Assert.Equal(savedJson, File.ReadAllText(path));

            // The existing configuration file cannot be used as a directory.
            handler.ConfigFilePath = Path.Combine(path, "blocked", "settings.json");
            try
            {
                Edit(imageView, (window, draft) =>
                {
                    draft.Mode = ReferenceLineMode.DiagonalCross;
                    draft.PointX = 16;
                    Click(window, "ConfirmButton");
                    Assert.Equal(Visibility.Visible, Assert.IsType<TextBlock>(window.FindName("SubmissionError")).Visibility);
                    Assert.Equal(original, Values(Options(imageView)));
                });
            }
            finally
            {
                handler.ConfigFilePath = path;
            }

            Assert.Equal(savedJson, File.ReadAllText(path));
            ReloadConfig(path);
            using var reopened = new ImageView { ConfigurationCode = CameraProfile };
            Assert.Equal(original, Values(Options(reopened)));
        });
    }

    private static DeviceCamera CreateCamera() =>
        new(new SysResourceModel { Id = -2, Code = DeviceCode, Value = "{}" });

    private static void SeedLegacyCamera(ConfigHandler handler)
    {
        var manager = handler.GetRequiredService<DisplayConfigManager>();
        manager.Object = new JObject
        {
            [DeviceCode] = new JObject
            {
                [nameof(DisplayCameraConfig.UseLocalCamera)] = false,
                [LegacyPropertyName] = new JObject
                {
                    [nameof(ReferenceLineParam.Mode)] = "GridLines",
                    [nameof(ReferenceLineParam.PointX)] = 123.5,
                    [nameof(ReferenceLineParam.PointY)] = 78,
                    [nameof(ReferenceLineParam.Angle)] = 13,
                    [nameof(ReferenceLineParam.IsMapping)] = true,
                    ["MaskSize"] = 75,
                },
            },
        };
        Assert.True(handler.TrySave(manager, out string error), error);
    }

    private static JObject ReadLegacyCameraOptions(string path)
    {
        var document = JObject.Parse(File.ReadAllText(path));
        string section = ConfigSectionNames.GetSectionName(typeof(DisplayConfigManager));
        return Assert.IsType<JObject>(document[section]?[nameof(DisplayConfigManager.Object)]?[DeviceCode]?[LegacyPropertyName]);
    }

    private static void SaveOptions(ImageView imageView, ReferenceLineMode mode, double pointX)
    {
        Edit(imageView, (window, draft) =>
        {
            draft.Mode = mode;
            draft.PointX = pointX;
            Click(window, "ConfirmButton");
        });
        Assert.Equal(mode, Options(imageView).Mode);
        Assert.Equal(pointX, Options(imageView).PointX);
    }

    private static void Edit(ImageView imageView, Action<PropertyEditorWindow, ReferenceLineParam> edit)
    {
        PropertyEditorWindow window = Tool(imageView).CreateOptionsEditor();
        try
        {
            edit(window, Assert.IsType<ReferenceLineParam>(window.EditConfig));
        }
        finally
        {
            window.Close();
        }
    }

    private static void Click(PropertyEditorWindow window, string name) =>
        Assert.IsType<Button>(window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static ToolReferenceLine Tool(ImageView imageView) =>
        Assert.IsType<ToolReferenceLine>(imageView.IEditorToolFactory.GetIEditorTool<ToolReferenceLine>());

    private static void AssertReferenceLineVisible(ImageView imageView, bool expected)
    {
        ToolReferenceLine tool = Tool(imageView);
        Assert.Equal(expected, tool.IsChecked);
        Assert.Equal(expected, imageView.EditorContext.DrawCanvas.ContainsVisual(tool.ReferenceLine));
    }

    private static ReferenceLineParam Options(ImageView imageView) => Tool(imageView).ReferenceLine.Attribute;

    private static Color LineColor(ImageView imageView) =>
        Assert.IsType<SolidColorBrush>(Options(imageView).Brush).Color;

    private static (ReferenceLineMode, double, double, double, bool) Values(ReferenceLineParam options) =>
        (options.Mode, options.PointX, options.PointY, options.Angle, options.IsMapping);

    private static ConfigHandler ReloadConfig(string path)
    {
        var handler = new ConfigHandler { ConfigFilePath = path, IsAutoSave = false };
        handler.LoadConfigs();
        TestEnvironment.SetConfig(handler);
        return handler;
    }

    private static void WithConfig(Action<ConfigHandler> test)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-ReferenceLineProfiles-");
            ResourceDictionary[] dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
            try
            {
                foreach (ResourceDictionary dictionary in dictionaries)
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                var handler = new ConfigHandler { ConfigFilePath = Path.Combine(directory.FullName, "settings.json"), IsAutoSave = false };
                TestEnvironment.SetConfig(handler);
                test(handler);
            }
            finally
            {
                TestEnvironment.SetConfig(previousConfig);
                foreach (ResourceDictionary dictionary in dictionaries)
                    Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                foreach (FileInfo file in directory.EnumerateFiles())
                    file.Delete();
                directory.Delete();
            }
        });
    }
}
