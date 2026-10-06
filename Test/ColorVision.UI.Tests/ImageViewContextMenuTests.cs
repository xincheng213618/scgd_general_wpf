using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.AppCommand;
using ColorVision.ImageEditor.EditorTools;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class ImageViewContextMenuTests
{
    [Theory]
    [InlineData("zh-Hans", "设置…", "问 AI 分析当前图像…", "适应窗口", "双线性（快速）", "批量处理图像…")]
    [InlineData("en-US", "Settings…", "Analyze Image with AI…", "Fit to Window", "Bilinear (Fast)", "Batch Image Processing…")]
    [InlineData("zh-Hant", "設定…", "請 AI 分析目前影像…", "符合視窗", "雙線性（快速）", "批次處理影像…")]
    public void StandardMenuAndSubmenusUseTheSelectedLanguage(
        string culture, string settings, string ai, string fit, string bilinear, string batch)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousCulture = CultureInfo.CurrentUICulture;
            var previousResourceCulture = ColorVision.ImageEditor.Properties.Resources.Culture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                ColorVision.ImageEditor.Properties.Resources.Culture = null;
                EnsureImageViewTestResources();
                using ImageView view = new();
                var providers = view.IEditorToolFactory.IIEditorToolContextMenus;
                // The standard menu must not depend on host or customer extension configuration.
                foreach (var provider in providers.Where(provider => provider.GetType().Assembly != typeof(ImageView).Assembly).ToArray())
                    providers.Remove(provider);
                var commands = Assert.Single(providers.OfType<ZoomEditorToolContextMenu>()).GetContextMenuItems();
                Assert.Equal(ai, Assert.Single(commands, item => item.GuidId == "AskCopilotAboutImage").Header);
                Assert.Same(ApplicationCommands.Open, Assert.Single(commands, item => item.GuidId == "OpenImage").Command);
                var settingsItems = Assert.Single(providers.OfType<ImageViewSettingsEditorToolContextMenu>()).GetContextMenuItems();
                Assert.Equal(settings, Assert.Single(settingsItems, item => item.GuidId == "ImageViewSettings").Header);
                var zoomItems = new ColorVision.ImageEditor.EditorTools.Zoom.ZoomEditorToolContextMenu(view.EditorContext.DrawEditorContext).GetContextMenuItems();
                Assert.Equal(fit, Assert.Single(zoomItems, item => item.GuidId == "ZoomUniform").Header);

                RenderOptions.SetBitmapScalingMode(view.ImageShow, BitmapScalingMode.Fant);
                var scaling = new BitmapScalingEditorToolContextMenu(view.EditorContext.DrawEditorContext).GetContextMenuItems()
                    .Where(item => item.OwnerGuid == "BitmapScalingMode").ToArray();
                Assert.Equal(4, scaling.Length);
                Assert.Single(scaling, item => item.IsChecked == true);
                var linear = Assert.Single(scaling, item => item.Order == (int)BitmapScalingMode.Linear);
                Assert.Equal(bilinear, linear.Header);
                linear.Command!.Execute(null);
                Assert.Equal(BitmapScalingMode.Linear, RenderOptions.GetBitmapScalingMode(view.ImageShow));

                var algorithmMenu = new AlgorithmsContextMenu(view.EditorContext.ProcessingContext).GetContextMenuItems();
                Assert.Equal(batch, Assert.Single(algorithmMenu, item => item.GuidId == "BatchImageProcessing").Header);
                var opening = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs),
                    BindingFlags.Instance | BindingFlags.NonPublic, binder: null, args: [view.Zoombox1, true], culture: null)!;
                view.Zoombox1.RaiseEvent(opening);
                var roots = view.EditorContext.ContextMenu.Items.OfType<MenuItem>().ToArray();
                Assert.Contains(roots, item => Equals(item.Header, settings));
                Assert.Contains(roots, item => Equals(item.Header, ai));
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
                ColorVision.ImageEditor.Properties.Resources.Culture = previousResourceCulture;
            }
        });
    }

    [Theory]
    [InlineData("zh-Hans")]
    [InlineData("en-US")]
    [InlineData("zh-Hant")]
    public void StandardCatalogMenusDeclareLocalizedCaptions(string culture)
    {
        var catalog = StandardAlgorithmCatalog.Create();
        var captions = ColorVision.Algorithms.AlgorithmCatalogProjection.ForInteractiveMenu(catalog);
        var language = CultureInfo.GetCultureInfo(culture);
        foreach (var entry in captions)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Presentation.ResourceKey));
            var header = ColorVision.ImageEditor.Properties.Resources.ResourceManager.GetString(entry.Presentation.ResourceKey, language);
            Assert.False(string.IsNullOrWhiteSpace(header));
            if (culture == "en-US")
                Assert.DoesNotContain(header!, character => character is >= '\u4e00' and <= '\u9fff');
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyCanvasInEditModeBuildsStandardMenuFromBothContextMenuSurfaces(bool raiseFromImageShow)
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            IIEditorToolContextMenu standardMenu = Assert.Single(
                view.IEditorToolFactory.IIEditorToolContextMenus,
                item => item is ZoomEditorToolContextMenu);
            view.IEditorToolFactory.IIEditorToolContextMenus.Clear();
            view.IEditorToolFactory.IIEditorToolContextMenus.Add(standardMenu);
            view.ImageEditMode = true;
            view.EditorContext.ContextMenu.Items.Clear();

            Assert.Null(view.ViewBitmapSource);
            UIElement eventSource = raiseFromImageShow ? view.ImageShow : view.Zoombox1;

            ContextMenuEventArgs opening = (ContextMenuEventArgs)Activator.CreateInstance(
                typeof(ContextMenuEventArgs),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: [eventSource, true],
                culture: null)!;

            eventSource.RaiseEvent(opening);

            Assert.False(opening.Handled);
            Assert.NotEmpty(view.EditorContext.ContextMenu.Items);
            Assert.Contains(
                view.EditorContext.ContextMenu.Items.OfType<MenuItem>(),
                item => ReferenceEquals(item.Command, ApplicationCommands.Open));
        });
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
}
