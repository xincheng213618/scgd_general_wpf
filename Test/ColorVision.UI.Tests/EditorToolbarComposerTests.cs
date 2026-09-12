using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.ImageEditor.Tooling;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class EditorToolbarComposerTests
{
    [Fact]
    public void DisposingShaderToolsPreservesDisplayCapabilityAndReplacementToolState()
    {
        IConfigService? previousConfigService = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler());
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureImageViewResources();
                using ImageView view = new();
                view.IEditorToolFactory.Dispose();
                view.IEditorToolFactory.IEditorTools.Clear();
                var capability = view.EditorContext.ProcessingContext.DisplayEffects.Shader;
                DisplayShaderFilterState state = capability.State;
                // Enable the capability before attaching a tool, so environmental notices
                // remain an interactive UI concern rather than part of this lifecycle test.
                state.IsEnabled = true;
                state.Brightness = 0.25;
                var effect = view.Presentation.SceneEffect;
                if (DisplayShaderFilterEnvironment.Current.CanUseShaderFilter)
                    Assert.IsType<DisplayShaderFilterEffect>(effect);

                using DisplayShaderFilterEditorTool original = new(view.EditorContext);
                int originalNotifications = 0;
                original.StateChanged += (_, _) => originalNotifications++;
                Assert.Same(state, original.State);
                state.Brightness = 0.4;
                Assert.Equal(1, originalNotifications);
                original.Dispose();

                state.Brightness = 0.6;
                Assert.Equal(1, originalNotifications);
                Assert.True(state.IsEnabled);
                Assert.Same(effect, view.Presentation.SceneEffect);
                if (effect is DisplayShaderFilterEffect filter)
                    Assert.Equal(0.6, filter.Brightness);

                using DisplayShaderFilterEditorTool replacement = new(view.EditorContext);
                Assert.Same(state, replacement.State);
                Assert.Equal(0.6, replacement.State.Brightness);
                replacement.Dispose();
                state.IsEnabled = false;
                Assert.Null(view.Presentation.SceneEffect);
            });
        }
        finally { ConfigService.SetInstance(previousConfigService!); }
    }

    [Fact]
    public void RefreshPreservesHostItemsAndCustomContentWithoutOwningToolDisposal()
    {
        WpfTestHost.Invoke(() =>
        {
            ToolBar toolbar = new();
            Button hostItem = new() { Content = "Host action" };
            toolbar.Items.Add(hostItem);
            IconTool iconTool = new();
            CustomTool customTool = new();
            EditorToolbarComposer composer = new(region => region == ToolBarLocal.Top ? toolbar : null);

            composer.Refresh([iconTool, customTool]);
            Button firstIconHost = Assert.Single(toolbar.Items.OfType<Button>(), button => ReferenceEquals(button.Content, iconTool.Icon));
            composer.Refresh([iconTool, customTool]);

            Assert.Null(firstIconHost.Content);
            Assert.Equal(3, toolbar.Items.Count);
            Assert.Same(hostItem, toolbar.Items[0]);
            Assert.Same(customTool.Control, toolbar.Items[2]);
            Assert.Same(customTool.InnerContent, customTool.Control.Content);
            Assert.False(customTool.IsDisposed);

            composer.Clear();

            Assert.Same(hostItem, Assert.Single(toolbar.Items.Cast<object>()));
            Assert.Null(LogicalTreeHelper.GetParent((DependencyObject)iconTool.Icon));
            Assert.Null(LogicalTreeHelper.GetParent(customTool.Control));
            Assert.Same(customTool.InnerContent, customTool.Control.Content);
            Assert.False(customTool.IsDisposed);
        });
    }

    private sealed class IconTool : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string GuidId => "icon";
        public int Order => 1;
        public object Icon { get; } = new Border();
        public ICommand? Command => null;
    }

    private sealed class CustomTool : IEditorCustomControlTool, IDisposable
    {
        internal Border InnerContent { get; } = new();
        internal Button Control { get; }
        internal bool IsDisposed { get; private set; }

        internal CustomTool() => Control = new Button { Content = InnerContent };

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string GuidId => "custom";
        public int Order => 2;
        public object? Icon => null;
        public ICommand? Command => null;
        public FrameworkElement CreateToolControl() => Control;
        public void Dispose() => IsDisposed = true;
    }

    private static void EnsureImageViewResources()
    {
        Application application = Application.Current!;
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
