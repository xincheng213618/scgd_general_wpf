using ColorVision.Core;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Themes;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class FovImageViewSettingsTests
{
    [Fact]
    public void FovImageViewRetainsConfirmedOptionsAndOnlyAppliesConfirmedDefaults()
    {
        WithConfig((config, reload) =>
        {
            var custom = (9123.5, 62.5, FovMeasurementMode.CurvedBoundary, 0.4);
            var defaults = (9410d, 74.2, FovMeasurementMode.FourCorners, 0.25);
            SaveOptions(config, custom);
            Assert.Equal(custom, Values(config.GetRequiredService<FovImageViewOptions>()));

            ConfigHandler reloaded = reload();
            Edit(reloaded, (window, draft) =>
            {
                Assert.Equal(custom, Values(draft));
                draft.CameraDegrees = 90;
                Click(window, "ResetButton");
                Assert.Equal(custom, Values(draft));
                Click(window, "ResetDefaultsButton");
                Assert.Equal(defaults, Values(draft));
                Click(window, "CancelButton");
            });
            Assert.Equal(custom, Values(reloaded.GetRequiredService<FovImageViewOptions>()));
            Assert.Equal(custom, Values(reload().GetRequiredService<FovImageViewOptions>()));

            Edit(reloaded, (window, draft) =>
            {
                draft.FovDist = 0;
                Click(window, "ConfirmButton");
                Assert.Equal(Visibility.Visible, Assert.IsType<TextBlock>(window.FindName("SubmissionError")).Visibility);
                Assert.Equal(custom, Values(reloaded.GetRequiredService<FovImageViewOptions>()));
                Assert.Equal(custom, Values(reload().GetRequiredService<FovImageViewOptions>()));

                Click(window, "ResetDefaultsButton");
                Click(window, "ConfirmButton");
            });
            Assert.Equal(defaults, Values(reloaded.GetRequiredService<FovImageViewOptions>()));
            Assert.Equal(defaults, Values(reload().GetRequiredService<FovImageViewOptions>()));
        });
    }

    [Fact]
    public void CameraOverridesAreIsolatedAndConfirmedResetResumesInheritingGlobalSettings()
    {
        WithConfig((config, reload) =>
        {
            FovImageViewCamera left = new("camera-left", "Camera");
            FovImageViewCamera right = new("camera-right", "Camera");
            var original = (9123.5, 62.5, FovMeasurementMode.CurvedBoundary, 0.4);
            var leftOverride = (8500d, 71.3, FovMeasurementMode.FourCorners, 0.31);
            var rightOverride = (9600d, 81.7, FovMeasurementMode.CurvedBoundary, 0.56);
            var updated = (8300d, 54.6, FovMeasurementMode.FourCorners, 0.7);
            var later = (9400d, 64.6, FovMeasurementMode.CurvedBoundary, 0.3);

            SaveOptions(config, original);
            AssertOptions(config, original, left);
            SaveOptions(config, leftOverride, left);
            AssertOptions(config, original, right);
            SaveOptions(config, rightOverride, right);

            ConfigHandler reloaded = reload();
            AssertOptions(reloaded, original);
            AssertOptions(reloaded, leftOverride, left);
            AssertOptions(reloaded, rightOverride, right);
            SaveOptions(reloaded, updated);

            Edit(reloaded, (window, draft) =>
            {
                Assert.Equal(leftOverride, Values(draft));
                Click(window, "ResetDefaultsButton");
                Assert.Equal(updated, Values(draft));
                Click(window, "CancelButton");
            }, left);
            reloaded = reload();
            AssertOptions(reloaded, leftOverride, left);

            Edit(reloaded, (window, draft) =>
            {
                Click(window, "ResetDefaultsButton");
                Assert.Equal(updated, Values(draft));
                Click(window, "ConfirmButton");
            }, left);
            reloaded = reload();
            AssertOptions(reloaded, updated, left);
            SaveOptions(reloaded, later);

            reloaded = reload();
            AssertOptions(reloaded, later);
            AssertOptions(reloaded, later, left);
            AssertOptions(reloaded, rightOverride, right);
        });
    }

    private static void WithConfig(Action<ConfigHandler, Func<ConfigHandler>> test)
    {
        WpfTestHost.Invoke(() =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-FovSettings-");
            string path = Path.Combine(directory.FullName, "settings.json");
            ResourceDictionary[] dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
            foreach (ResourceDictionary dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
            try
            {
                var config = new ConfigHandler { ConfigFilePath = path, IsAutoSave = false };
                test(config, ReloadConfig);
            }
            finally
            {
                foreach (ResourceDictionary dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
                directory.Delete();
            }

            ConfigHandler ReloadConfig()
            {
                var handler = new ConfigHandler { ConfigFilePath = path, IsAutoSave = false };
                handler.LoadConfigs();
                return handler;
            }
        });
    }

    private static void SaveOptions(ConfigHandler config, (double, double, FovMeasurementMode, double) values, FovImageViewCamera? camera = null)
        => Edit(config, (window, draft) =>
        {
            (draft.FovDist, draft.CameraDegrees, draft.MeasurementMode, draft.MinimumConfidence) = values;
            Click(window, "ConfirmButton");
        }, camera);

    private static void AssertOptions(ConfigHandler config, (double, double, FovMeasurementMode, double) expected, FovImageViewCamera? camera = null)
        => Edit(config, (_, draft) => Assert.Equal(expected, Values(draft)), camera);

    private static void Edit(ConfigHandler config, Action<PropertyEditorWindow, FovImageViewOptions> edit, FovImageViewCamera? camera = null)
    {
        PropertyEditorWindow window = FovImageViewRunner.CreateOptionsEditor(config, camera);
        try
        {
            edit(window, Assert.IsType<FovImageViewOptions>(window.EditConfig));
        }
        finally
        {
            window.Close();
        }
    }

    private static void Click(PropertyEditorWindow window, string name) =>
        Assert.IsType<Button>(window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static (double, double, FovMeasurementMode, double) Values(FovImageViewOptions options) =>
        (options.FovDist, options.CameraDegrees, options.MeasurementMode, options.MinimumConfidence);
}
