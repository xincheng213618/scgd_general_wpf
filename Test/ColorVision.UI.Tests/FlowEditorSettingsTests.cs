using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Themes;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
            IConfigService previousConfig = ConfigService.Instance;
            ResourceDictionary[] previousResources = Application.Current.Resources.MergedDictionaries.ToArray();
            using var manager = new ThemeManager();
            var directory = Directory.CreateTempSubdirectory("FlowEditorTheme-");
            string configPath = Path.Combine(directory.FullName, "settings.json");
            ThemeManager.Current = manager;
            try
            {
                var handler = new ConfigHandler { ConfigFilePath = configPath, IsAutoSave = false };
                TestEnvironment.SetConfig(handler);
                manager.ApplyTheme(Application.Current, Theme.Light);
                var settings = handler.GetRequiredService<FlowEditorSettings>();
                using var view = new FlowEngineManager().View;
                FlowEditorCanvas first = view.EditorCanvas;
                using var second = new FlowEditorCanvas(settings);
                var themeButton = (Button)first.FindName("ThemeButton");
                var node = new PresentationNode();
                node.Create();
                first.NodeEditor.Nodes.Add(node);
                first.NodeEditor.ClearHistory();
                byte[] document = first.NodeEditor.GetCanvasData();

                Assert.Same(STNodeVisualTheme.Light, first.NodeEditor.VisualTheme);
                manager.ApplyTheme(Application.Current, Theme.Dark);
                Assert.Same(STNodeVisualTheme.Dark, first.NodeEditor.VisualTheme);
                themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(FlowEditorThemeMode.Light, settings.ThemeMode);
                Assert.Same(STNodeVisualTheme.Light, first.NodeEditor.VisualTheme);
                Assert.Same(STNodeVisualTheme.Light, second.NodeEditor.VisualTheme);
                AssertExecutionPalette(view);
                Assert.Equal(Theme.Dark, manager.CurrentUITheme);
                manager.ApplyTheme(Application.Current, Theme.Light);
                manager.ApplyTheme(Application.Current, Theme.Dark);
                Assert.Same(STNodeVisualTheme.Light, first.NodeEditor.VisualTheme);

                themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(FlowEditorThemeMode.Dark, settings.ThemeMode);
                Assert.Same(STNodeVisualTheme.Dark, first.NodeEditor.VisualTheme);
                AssertExecutionPalette(view);

                settings.CustomIsDark = false;
                settings.CanvasColor = Colors.MidnightBlue;
                settings.AccentColor = Colors.Coral;
                settings.ShowGrid = false;
                settings.Curvature = 0.6;
                themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(FlowEditorThemeMode.Custom, settings.ThemeMode);
                Assert.Equal(System.Drawing.Color.MidnightBlue.ToArgb(), first.NodeEditor.BackColor.ToArgb());
                Assert.Equal(first.NodeEditor.BackColor, second.NodeEditor.BackColor);
                Assert.Equal(System.Drawing.Color.Coral.ToArgb(), first.NodeEditor.VisualTheme.TitleSurface(System.Drawing.Color.DodgerBlue).ToArgb());
                AssertExecutionPalette(view);
                Assert.False(first.NodeEditor.ShowGrid);
                Assert.Equal(0.6f, first.NodeEditor.Curvature);
                Assert.Equal(document, first.NodeEditor.GetCanvasData());
                Assert.False(first.NodeEditor.IsModified);
                Assert.False(first.NodeEditor.CanUndo);

                themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(FlowEditorThemeMode.FollowApplication, settings.ThemeMode);
                Assert.Same(STNodeVisualTheme.Dark, first.NodeEditor.VisualTheme);
                AssertExecutionPalette(view);
                Assert.Equal(Colors.MidnightBlue, settings.CanvasColor);
                var reloaded = new ConfigHandler { ConfigFilePath = configPath };
                reloaded.LoadConfigs();
                Assert.Equal(FlowEditorThemeMode.FollowApplication, reloaded.GetRequiredService<FlowEditorSettings>().ThemeMode);

                manager.ApplyTheme(Application.Current, Theme.Light);
                Assert.Same(STNodeVisualTheme.Light, first.NodeEditor.VisualTheme);
                AssertExecutionPalette(view);
                first.Dispose();
                var disposedTheme = first.NodeEditor.VisualTheme;
                settings.ThemeMode = FlowEditorThemeMode.Dark;
                Assert.Same(disposedTheme, first.NodeEditor.VisualTheme);
                Assert.Same(STNodeVisualTheme.Dark, second.NodeEditor.VisualTheme);
            }
            finally
            {
                ThemeManager.Current = previousManager;
                TestEnvironment.SetConfig(previousConfig);
                Application.Current.Resources.MergedDictionaries.Clear();
                foreach (ResourceDictionary resource in previousResources) Application.Current.Resources.MergedDictionaries.Add(resource);
                foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
                directory.Delete();
            }
        });
    }

    private static void AssertExecutionPalette(ViewFlow view)
    {
        Assert.Same(view.EditorCanvas.FindResource("ButtonBackground"), view.RunButton.Background);
        Assert.Same(view.RunButton.Background, view.StopButton.Background);
        Assert.Same(view.RunButton.Background, view.FlowResultManagementButton.Background);
        Assert.Same(view.RunButton.Background, view.ExecutionAnalysisButton.Background);
        Assert.Same(view.EditorCanvas.FindResource("GlobalTextBrush"), view.logTextBox.Foreground);
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
