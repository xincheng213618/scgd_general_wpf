using ColorVision.Engine.Services;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.RoiStatistics;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using ColorVision.ImageEditor.EditorTools.AppCommand;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Tooling;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EditorText = ColorVision.ImageEditor.DisplayText;
using Resources = ColorVision.ImageEditor.Properties.Resources;
using ZoomContextMenu = ColorVision.ImageEditor.EditorTools.Zoom.ZoomEditorToolContextMenu;

namespace ColorVision.UI.Tests;

public sealed class ImageFileContextMenuTests
{
    [Fact]
    public void CanvasMenuRoutesOpenToItsImageViewAndFollowsImageAvailability()
    {
        WpfTestHost.Invoke(() =>
        {
            ResourceDictionary theme = AddTestTheme();
            try
            {
                using ImageView view = new();
                EditorContext context = view.EditorContext;
                var providers = view.IEditorToolFactory.IIEditorToolContextMenus;
                providers.Clear();
                providers.Add(new ZoomEditorToolContextMenu(view.Config, context.DrawEditorContext));
                providers.Add(new ImageViewSettingsEditorToolContextMenu(context));
                providers.Add(new ZoomContextMenu(context.DrawEditorContext));
                providers.Add(new AlgorithmsContextMenu(context.ProcessingContext, context.DrawEditorContext));

                ToolBar left = view.GetRegionToolBar(ToolBarLocal.Left)!;
                ToolBar top = view.GetRegionToolBar(ToolBarLocal.Top)!;
                ToolBar right = view.GetRegionToolBar(ToolBarLocal.Right)!;
                ButtonBase FindTool(ToolBar bar, string id)
                {
                    var tool = Assert.IsAssignableFrom<IEditorTool>(view.IEditorToolFactory.GetIEditorTool(id));
                    return Assert.Single(bar.Items.OfType<ButtonBase>(), item => item.Command == tool.Command);
                }

                ButtonBase settings = FindTool(left, "ImageViewSettings");
                Assert.True(left.Items.IndexOf(settings) > left.Items.IndexOf(FindTool(left, "CloseImageEditorTool")));
                Assert.True(FindTool(left, "OpenImage").IsEnabled);
                Assert.True(settings.IsEnabled);
                Assert.False(FindTool(left, "SaveAs").IsEnabled);
                Assert.False(FindTool(top, "ZoomUniform").IsEnabled);
                Assert.False(FindTool(right, "Histogram").IsEnabled);

                int openRequests = 0;
                CommandManager.AddPreviewExecutedHandler(view, (_, args) =>
                {
                    if (args.Command != ApplicationCommands.Open) return;
                    openRequests++;
                    args.Handled = true;
                });

                MenuItem[] empty = OpenCanvasMenu(view);
                AssertEmptyCanvasMenu(empty);
                MenuItem open = Assert.Single(empty, item => item.Command == ApplicationCommands.Open);
                Assert.Same(view, open.CommandTarget);
                Assert.True(open.IsEnabled);
                var invoke = Assert.IsAssignableFrom<IInvokeProvider>(new MenuItemAutomationPeer(open).GetPattern(PatternInterface.Invoke));
                invoke.Invoke();
                view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Assert.Equal(1, openRequests);

                view.SetImageSource(new WriteableBitmap(16, 8, 96, 96, PixelFormats.Gray8, null));
                Assert.True(FindTool(left, "SaveAs").IsEnabled);
                Assert.True(FindTool(top, "ZoomUniform").IsEnabled);
                Assert.True(FindTool(right, "Histogram").IsEnabled);
                MenuItem[] loaded = OpenCanvasMenu(view);
                Assert.Contains(loaded, item => item.Command == ApplicationCommands.Open);
                Assert.Contains(loaded, item => item.Command == ApplicationCommands.Close);
                Assert.Contains(loaded, item => item.Command == ApplicationCommands.SaveAs);
                Assert.Contains(loaded, item => item.Command == ApplicationCommands.Print);
                Assert.Contains(loaded, item => Equals(item.Header, Resources.Zoom));
                Assert.DoesNotContain(loaded, item => item.Tag is MenuItemMetadata { GuidId: "SelectImageFileInFolder" });
                Assert.Single(loaded, item => item.Tag is MenuItemMetadata { GuidId: "OpenImageInNewWindow" });

                view.SetImageSource(new ResultImagePlaceholderCache().GetOrCreate(16, 8),
                    enableEditorImageServices: false, configureDefaultLayerController: false);
                Assert.DoesNotContain(OpenCanvasMenu(view), item => item.Tag is MenuItemMetadata { GuidId: "OpenImageInNewWindow" });

                view.Clear();
                AssertEmptyCanvasMenu(OpenCanvasMenu(view));
                Assert.False(FindTool(left, "SaveAs").IsEnabled);
                Assert.False(FindTool(top, "ZoomUniform").IsEnabled);
                Assert.True(settings.IsEnabled);
            }
            finally { Application.Current.Resources.MergedDictionaries.Remove(theme); }
        });
    }

    [Fact]
    public void RectangleMenuKeepsEditingAndExtensionActionsAtRootAndGroupsPixelOperations()
    {
        WpfTestHost.Invoke(() =>
        {
            ResourceDictionary theme = AddTestTheme();
            try
            {
                using ImageView view = new();
                view.SetImageSource(new WriteableBitmap(16, 8, 96, 96, PixelFormats.Gray8, null));
                EditorContext context = view.EditorContext;
                DrawEditorContext drawing = context.DrawEditorContext;
                MenuItem extensionAction = new() { Header = "Extension action" };
                var providers = view.IEditorToolFactory.ContextMenuProviders;
                providers.Clear();
                providers.Add(new RoiStatisticsRectangleDrawingContextMenu(context.ProcessingContext, drawing));
                providers.Add(new DVRectangleDVContextMenu(view.ImageShow, context.ProcessingContext));
                providers.Add(new ExtensionRectangleMenu(extensionAction));
                providers.Add(new BmwSfrRectangleContextMenu(context.ProcessingContext, drawing));
                providers.Add(new DrawingVisualBaseDVContextMenu(drawing));
                providers.Add(new SelectEditorVisualVContextMenu());

                DVRectangle rectangle = new() { Rect = new Rect(2, 2, 8, 4) };
                new ImageContextMenuComposer(context).AddDrawingItems(rectangle, drawing.SelectionVisual);

                MenuItem[] root = context.ContextMenu.Items.OfType<MenuItem>().ToArray();
                Assert.Equal(Resources.Draw_Edit, root[0].Header);
                Assert.Contains(extensionAction, root);
                Assert.Contains(root, item => Equals(item.Header, Resources.Draw_Delete));
                Assert.Contains(root, item => Equals(item.Header, Resources.Draw_Rasterize));
                Assert.DoesNotContain(root, item => Equals(item.Header, Resources.Algorithm_FourEdgeSfr)
                    || Equals(item.Header, EditorText.Get("ROI 统计...")));
                MenuItem crop = Assert.Single(root, item => Equals(item.Header, EditorText.Get("裁剪")));
                Assert.Equal(3, crop.Items.Count);

                MenuItem analysis = Assert.Single(root, item => Equals(item.Header, Resources.Algorithm_AlgorithmCalls));
                Assert.Collection(analysis.Items.OfType<MenuItem>(),
                    category =>
                    {
                        Assert.Equal(Resources.Algorithm_GroupImageQuality, category.Header);
                        Assert.Equal(Resources.Algorithm_FourEdgeSfr, Assert.Single(category.Items.OfType<MenuItem>()).Header);
                    },
                    category =>
                    {
                        Assert.Equal(Resources.Algorithm_GroupStatistics, category.Header);
                        Assert.Equal(EditorText.Get("ROI 统计..."), Assert.Single(category.Items.OfType<MenuItem>()).Header);
                    });
            }
            finally { Application.Current.Resources.MergedDictionaries.Remove(theme); }
        });
    }

    [Fact]
    public void FileLocationFollowsCurrentPathAndIsUnavailableForMemoryOnlyOrRemovedFiles()
    {
        string path = Path.GetTempFileName();
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ImageViewConfig config = new();
                DrawEditorContext drawing = new(new DrawCanvas(), new Zoombox());
                var menu = new ZoomEditorToolContextMenu(config, drawing).GetContextMenuItems();
                ICommand select = Assert.Single(menu, item => item.GuidId == "SelectImageFileInFolder").Command!;
                Assert.False(select.CanExecute(null));
                config.FilePath = path;
                Assert.True(select.CanExecute(null));
                config.FilePath = path + ".missing";
                Assert.False(select.CanExecute(null));
                Assert.Same(ApplicationCommands.Open, Assert.Single(menu, item => item.GuidId == "OpenImage").Command);
            });
        }
        finally { File.Delete(path); }
    }

    private static MenuItem[] OpenCanvasMenu(ImageView view)
    {
        var opening = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [view.Zoombox1, true], culture: null)!;
        view.Zoombox1.RaiseEvent(opening);
        Assert.False(opening.Handled);
        var items = view.EditorContext.ContextMenu.Items;
        Assert.IsType<MenuItem>(items[0]);
        Assert.IsType<MenuItem>(items[items.Count - 1]);
        return items.OfType<MenuItem>().ToArray();
    }

    private static ResourceDictionary AddTestTheme()
    {
        ResourceDictionary theme = new();
        foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            theme.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
        Application.Current.Resources.MergedDictionaries.Add(theme);
        return theme;
    }

    private static void AssertEmptyCanvasMenu(MenuItem[] items)
    {
        Assert.Single(items, item => item.Command == ApplicationCommands.Open);
        Assert.Single(items, item => Equals(item.Header, Resources.ImageView_ContextSettings));
        MenuItem algorithms = Assert.Single(items, item => Equals(item.Header, Resources.ImageAlgorithm));
        Assert.Equal(Resources.ImageView_BatchProcessing, Assert.Single(algorithms.Items.OfType<MenuItem>()).Header);
        Assert.DoesNotContain(items, item => item.Command == ApplicationCommands.Close || item.Command == ApplicationCommands.SaveAs
            || item.Command == ApplicationCommands.Print || Equals(item.Header, Resources.Zoom) || Equals(item.Header, Resources.ImageView_AskAiAboutImage)
            || item.Tag is MenuItemMetadata { GuidId: "OpenImageInNewWindow" });
    }

    private sealed class ExtensionRectangleMenu(MenuItem action) : IDVContextMenu
    {
        public Type ContextType => typeof(DVRectangle);
        public IEnumerable<MenuItem> GetContextMenuItems(object obj) => [action];
    }
}
