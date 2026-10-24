using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Themes;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class FlowEditorSettingsTests
{
    [Fact]
    public void SharedPresentationFollowsApplicationUnlessOverriddenAndDoesNotEditDocuments()
    {
        WpfTestHost.Invoke(() =>
        {
            ThemeManager previousManager = ThemeManager.Current;
            ResourceDictionary[] previousResources = Application.Current.Resources.MergedDictionaries.ToArray();
            using var manager = new ThemeManager();
            ThemeManager.Current = manager;
            try
            {
                manager.ApplyTheme(Application.Current, Theme.Light);
                var settings = new FlowEditorSettings();
                using var first = new FlowEditorCanvas(settings);
                using var second = new FlowEditorCanvas(settings);
                var node = new PresentationNode();
                node.Create();
                first.NodeEditor.Nodes.Add(node);
                first.NodeEditor.ClearHistory();
                byte[] document = first.NodeEditor.GetCanvasData();

                Assert.Same(STNodeVisualTheme.Light, first.NodeEditor.VisualTheme);
                manager.ApplyTheme(Application.Current, Theme.Dark);
                Assert.Same(STNodeVisualTheme.Dark, first.NodeEditor.VisualTheme);
                settings.ThemeMode = FlowEditorThemeMode.Light;
                Assert.Same(STNodeVisualTheme.Light, first.NodeEditor.VisualTheme);
                Assert.Same(STNodeVisualTheme.Light, second.NodeEditor.VisualTheme);
                Assert.Equal(Theme.Dark, manager.CurrentUITheme);
                manager.ApplyTheme(Application.Current, Theme.Light);
                manager.ApplyTheme(Application.Current, Theme.Dark);
                Assert.Same(STNodeVisualTheme.Light, first.NodeEditor.VisualTheme);

                settings.ThemeMode = FlowEditorThemeMode.Custom;
                settings.CanvasColor = Colors.MidnightBlue;
                settings.AccentColor = Colors.Coral;
                settings.ShowGrid = false;
                settings.Curvature = 0.6;
                Assert.Equal(System.Drawing.Color.MidnightBlue.ToArgb(), first.NodeEditor.BackColor.ToArgb());
                Assert.Equal(first.NodeEditor.BackColor, second.NodeEditor.BackColor);
                Assert.Equal(System.Drawing.Color.Coral.ToArgb(), first.NodeEditor.VisualTheme.TitleSurface(System.Drawing.Color.DodgerBlue).ToArgb());
                Assert.False(first.NodeEditor.ShowGrid);
                Assert.Equal(0.6f, first.NodeEditor.Curvature);
                Assert.Equal(document, first.NodeEditor.GetCanvasData());
                Assert.False(first.NodeEditor.IsModified);
                Assert.False(first.NodeEditor.CanUndo);

                settings.ThemeMode = FlowEditorThemeMode.FollowApplication;
                Assert.Same(STNodeVisualTheme.Dark, first.NodeEditor.VisualTheme);
                first.Dispose();
                var disposedTheme = first.NodeEditor.VisualTheme;
                settings.ThemeMode = FlowEditorThemeMode.Light;
                Assert.Same(disposedTheme, first.NodeEditor.VisualTheme);
                Assert.Same(STNodeVisualTheme.Light, second.NodeEditor.VisualTheme);
            }
            finally
            {
                ThemeManager.Current = previousManager;
                Application.Current.Resources.MergedDictionaries.Clear();
                foreach (ResourceDictionary resource in previousResources) Application.Current.Resources.MergedDictionaries.Add(resource);
            }
        });
    }

    [Fact]
    public void SavedCustomPaletteAndCanvasPreferencesSurviveColdReloadAndReset()
    {
        string directory = Directory.CreateTempSubdirectory("FlowEditorSettings-").FullName;
        try
        {
            string path = Path.Combine(directory, "settings.json");
            var handler = new ConfigHandler { ConfigFilePath = path };
            var settings = handler.GetRequiredService<FlowEditorSettings>();
            settings.ThemeMode = FlowEditorThemeMode.Custom;
            settings.CanvasColor = Colors.AliceBlue;
            settings.CustomIsDark = false;
            settings.ShowGrid = false;
            settings.NodeCornerRadius = 18;
            Assert.True(handler.TrySave(settings, out string error), error);

            var reloaded = new ConfigHandler { ConfigFilePath = path };
            reloaded.LoadConfigs();
            var restored = reloaded.GetRequiredService<FlowEditorSettings>();
            Assert.Equal(FlowEditorThemeMode.Custom, restored.ThemeMode);
            Assert.Equal(Colors.AliceBlue, restored.CanvasColor);
            Assert.False(restored.CustomIsDark);
            Assert.False(restored.ShowGrid);
            Assert.Equal(18, restored.NodeCornerRadius);
            var session = PropertyEditSession.Create(restored, PropertyEditorEditMode.Immediate);
            session.ResetToDefaults();
            Assert.Equal(FlowEditorThemeMode.FollowApplication, restored.ThemeMode);
            Assert.True(restored.ShowGrid);
            session.Reset();
            Assert.Equal(FlowEditorThemeMode.Custom, restored.ThemeMode);
            Assert.Equal(Colors.AliceBlue, restored.CanvasColor);
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private sealed class PresentationNode : STNode
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            Title = "Theme sample";
            TitleColor = System.Drawing.Color.DodgerBlue;
        }
    }
}
