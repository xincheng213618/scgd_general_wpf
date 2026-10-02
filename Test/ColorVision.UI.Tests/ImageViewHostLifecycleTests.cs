using ColorVision.Engine;
using ColorVision.Engine.Impl.SolutionImpl;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.Solution.MultiImageViewer;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ImageViewHostLifecycleTests
{
    [Theory]
    [InlineData("roi", false)]
    [InlineData("roi", true)]
    [InlineData("poi", false)]
    [InlineData("poi", true)]
    [InlineData("keyboard", false)]
    [InlineData("keyboard", true)]
    public void EditorsReleaseImagesEvenWhenNeverShown(string kind, bool show)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new PresentationScope();
            Window window = CreateEditor(kind);
            ImageView view = Preview(window);
            try
            {
                if (show)
                {
                    PlaceOffscreen(window);
                    window.Show();
                    PumpDispatcher();
                }
                SetPixels(view);
                window.Close();
                PumpDispatcher();
                AssertDisposed(view);
            }
            finally
            {
                window.Close();
                view.Dispose();
            }
        });
    }

    [Theory]
    [InlineData("roi")]
    [InlineData("poi")]
    [InlineData("keyboard")]
    [InlineData("camera")]
    public void CancelledCloseKeepsPreviewUsableUntilActualClose(string kind)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new PresentationScope();
            using DeviceCamera? device = kind == "camera" ? new DeviceCamera(Resource("camera")) : null;
            Window window = device == null ? CreateEditor(kind) : new CameraLocalWindow(device);
            if (device != null)
            {
                // Exercise WPF lifetime without initializing the SDK or saving camera preferences.
                RemoveHandler(window, "Window_Loaded", typeof(RoutedEventHandler));
                RemoveHandler(window, "Window_Closing", typeof(CancelEventHandler));
            }
            ImageView view = Preview(window);
            CancelEventHandler cancel = (_, e) => e.Cancel = true;
            try
            {
                PlaceOffscreen(window);
                window.Show();
                PumpDispatcher();
                SetPixels(view);
                if (window is CameraLocalWindow camera)
                {
                    camera.rawArray = new byte[2048];
                    camera.srcrawArray = new byte[1024];
                }
                window.Closing += cancel;
                window.Close();
                Assert.NotNull(view.ViewBitmapSource);
                using (view.RegisterSettingsProvider(() => [])) { }
                window.Closing -= cancel;
                window.Close();
                PumpDispatcher();
                AssertDisposed(view);
                if (window is CameraLocalWindow closedCamera)
                {
                    Assert.Null(closedCamera.rawArray);
                    Assert.Null(closedCamera.srcrawArray);
                    closedCamera.Dispose();
                }
            }
            finally
            {
                window.Closing -= cancel;
                window.Close();
                view.Dispose();
            }
        });
    }

    [Theory]
    [InlineData("file")]
    [InlineData("result")]
    [InlineData("keyboard")]
    [InlineData("multi")]
    public void StandalonePreviewOwnersDisposeTheirImageView(string kind)
    {
        using var file = new ImageFile();
        WpfTestHost.Invoke(() =>
        {
            using var scope = new PresentationScope();
            var owner = CreateEditor("keyboard");
            ImageView ownerView = Preview(owner);
            PlaceOffscreen(owner);
            owner.Show();
            PumpDispatcher();
            Application.Current.MainWindow = owner;
            SetPixels(ownerView);
            Window[] before = Application.Current.Windows.Cast<Window>().ToArray();
            Window? popup = null;
            try
            {
                switch (kind)
                {
                    case "file":
                        var result = new CVRawStandaloneFileProcessor().OpenFile(file.Path);
                        Assert.True(result.Succeeded);
                        break;
                    case "result":
                        new ViewResultImage { FileUrl = file.Path }.Open();
                        break;
                    case "keyboard":
                        typeof(EditPoiParam1).GetMethod("ShowKeyboardResultPreview", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(owner, [new JArray()]);
                        break;
                    case "multi":
                        MultiImageViewer.Config.ShowThumbnail = false;
                        ownerView.Config.FilePath = file.Path;
                        new ZoomEditorToolContextMenu(ownerView.EditorContext).GetContextMenuItems()
                            .Single(item => item.GuidId == "MultiImageViewerEditor").Command!.Execute(null);
                        break;
                }
                popup = Assert.Single(Application.Current.Windows.Cast<Window>().Except(before));
                PlaceOffscreen(popup);
                PumpDispatcher();
                ImageView preview = popup.Content is MultiImageViewer viewer
                    ? Assert.IsType<ImageView>(viewer.FindName("ImageView"))
                    : Assert.IsType<ImageView>(popup.Content);
                Assert.NotNull(preview.ViewBitmapSource);
                using (preview.AcquireImageFrame()) { }
                popup.Close();
                PumpDispatcher();
                AssertDisposed(preview);
                if (popup.Content is MultiImageViewer closedViewer) Assert.Empty(closedViewer.ImageFiles);
            }
            finally
            {
                foreach (Window window in Application.Current.Windows.Cast<Window>().Except(before).ToArray())
                {
                    window.Close();
                    (window.Content as IDisposable)?.Dispose();
                }
                owner.Close();
                ownerView.Dispose();
                PumpDispatcher();
            }
        });
    }

    [Fact]
    public async Task FinishingThumbnailLoadDoesNotReviveDisposedViewer()
    {
        using var file = new ImageFile();
        PresentationScope? scope = null;
        MultiImageViewer? viewer = null;
        try
        {
            Task loading = WpfTestHost.Invoke(() =>
            {
                scope = new PresentationScope();
                MultiImageViewer.Config.ShowThumbnail = true;
                MultiImageViewer.Config.EnableThumbnailCache = false;
                viewer = new MultiImageViewer();
                Task pending = viewer.LoadFromFilesAsync([file.Path]);
                Assert.False(pending.IsCompleted); // Thumbnail decoding is queued on this dispatcher.
                viewer.Dispose();
                return pending;
            });
            await loading;
            WpfTestHost.Invoke(() =>
            {
                Assert.Empty(viewer!.ImageFiles);
                AssertDisposed(Assert.IsType<ImageView>(viewer.FindName("ImageView")));
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => { viewer?.Dispose(); scope?.Dispose(); });
        }
    }

    [Fact]
    public void ClosedEditorsAndPreviewsCanBeCollected()
    {
        var scope = WpfTestHost.Invoke(() => new PresentationScope());
        try
        {
            WeakReference[] references = WpfTestHost.Invoke(CreateClosedEditorReferences);
            Collect(references);
        }
        finally { WpfTestHost.Invoke(scope.Dispose); }
    }

    [Theory]
    [InlineData("poi")]
    [InlineData("keyboard")]
    public void ClosingPoiEditorKeepsColumnPreferencesWithoutRetainingUiObjects(string kind)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new PresentationScope();
            Window editor = CreateEditor(kind);
            try
            {
                var columns = ColumnPreferences(kind);
                GridViewColumnVisibility column = columns.First();
                column.IsVisible = false;
                column.IsSortD = true;
                // A generated menu subscribes closures capturing the live GridView columns.
                var menu = Assert.IsType<ListView>(editor.FindName("ListView1")).ContextMenu;
                editor.GetType().GetMethod("ContextMenu_Opened", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(editor, [menu, new RoutedEventArgs()]);
                editor.Close();
                var saved = ColumnPreferences(kind);
                Assert.All(saved, item => Assert.Null(item.GridViewColumn));
                Assert.False(saved.First().IsVisible);
                Assert.True(saved.First().IsSortD);
                Window reopened = CreateEditor(kind);
                try
                {
                    Assert.False(ColumnPreferences(kind).First().IsVisible);
                    Assert.True(ColumnPreferences(kind).First().IsSortD);
                }
                finally { reopened.Close(); }
            }
            finally { editor.Close(); }
        });
    }

    private static IReadOnlyList<GridViewColumnVisibility> ColumnPreferences(string kind) => kind == "poi"
        ? EditPoiParamConfig.Instance.GridViewColumnVisibilitys
        : EditPoiParam1Config.Instance.GridViewColumnVisibilitys;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateClosedEditorReferences()
    {
        var references = new List<WeakReference>();
        foreach (string kind in new[] { "roi", "poi", "keyboard" })
        {
            Window window = CreateEditor(kind);
            ImageView view = Preview(window);
            PlaceOffscreen(window);
            window.Show();
            PumpDispatcher();
            SetPixels(view);
            if (kind != "roi")
            {
                var menu = Assert.IsType<ListView>(window.FindName("ListView1")).ContextMenu;
                window.GetType().GetMethod("ContextMenu_Opened", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [menu, new RoutedEventArgs()]);
            }
            references.Add(new WeakReference(window));
            references.Add(new WeakReference(view));
            window.Close();
            PumpDispatcher();
        }
        Application.Current.MainWindow = null;
        return references.ToArray();
    }

    private static void Collect(WeakReference[] references)
    {
        try
        {
            for (int i = 0; i < 3 && references.Any(reference => reference.IsAlive); i++)
            {
                WpfTestHost.Invoke(PumpDispatcher);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.All(references, reference => Assert.False(reference.IsAlive,
                "Disposed image owners and their previews/services must be collectible."));
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                foreach (var reference in references)
                    if (reference.Target is IDisposable disposable) disposable.Dispose();
            });
        }
    }

    private static SysResourceModel Resource(string name) => new()
    {
        Id = -2, Code = $"image-lifetime-{name}", Name = "Offline lifecycle fixture", Value = "{}"
    };

    private static Window CreateEditor(string kind) => kind switch
    {
        "roi" => new RoiEditorWindow(new PhyCameraCfg { SensorWidth = 64, SensorHeight = 32, Width = 32, Height = 16 }),
        "poi" => new EditPoiParam(new PoiParam { Id = -1, Width = 64, Height = 32, Name = "Lifecycle" }),
        "keyboard" => new EditPoiParam1(new TemplateJsonKBParam { KBJson = new KBJson { Width = 64, Height = 32 } }),
        _ => throw new ArgumentException(kind)
    };

    private static ImageView Preview(Window window) => Assert.IsType<ImageView>(
        window.FindName(window is RoiEditorWindow ? "PreviewImageView" : "ImageView"));

    private static void SetPixels(ImageView view)
    {
        view.SetImageSource(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null));
        using var frame = view.AcquireImageFrame();
        Assert.NotNull(frame);
    }

    private static void AssertDisposed(ImageView view)
    {
        Assert.Null(view.ViewBitmapSource);
        Assert.Throws<ObjectDisposedException>(() => view.RegisterSettingsProvider(() => []));
    }

    private static void RemoveHandler(Window window, string methodName, Type delegateType)
    {
        Delegate handler = window.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(delegateType, window);
        if (handler is RoutedEventHandler loaded) window.Loaded -= loaded;
        if (handler is CancelEventHandler closing) window.Closing -= closing;
    }

    private static void PlaceOffscreen(Window window)
    {
        window.WindowState = WindowState.Normal;
        window.Width = 900;
        window.Height = 600;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000;
        window.Top = -10000;
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class PresentationScope : IDisposable
    {
        private readonly List<ResourceDictionary> _dictionaries = [];
        private readonly IConfigService? _previousConfig = ConfigService.Instance;
        private readonly Window? _previousMainWindow = Application.Current.MainWindow;

        public PresentationScope()
        {
            ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                _dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
        }

        public void Dispose()
        {
            Application.Current.MainWindow = _previousMainWindow;
            ConfigService.SetInstance(_previousConfig!);
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    }

    private sealed class ImageFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ColorVision-image-lifetime-{Guid.NewGuid():N}", "sample.png");

        public ImageFile()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            WpfTestHost.Invoke(() =>
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null)));
                using var stream = File.Create(Path);
                encoder.Save(stream);
            });
        }

        public void Dispose()
        {
            File.Delete(Path);
            Directory.Delete(System.IO.Path.GetDirectoryName(Path)!);
        }
    }
}
