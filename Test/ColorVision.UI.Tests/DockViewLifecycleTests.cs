using AvalonDock.Layout;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.Core;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Documents;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using FlowEngineLib.Algorithm;
using System.Collections.ObjectModel;
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

public sealed class DockViewLifecycleTests
{
    [Fact]
    public void RegisteringFactoryDoesNotCreateItsView()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new Scope();
            int created = 0;
            using var registration = new DockViewRegistration(() => { created++; return new UserControl(); }, "Lazy view");
            new UserControl().AddViewConfig(registration, "Lazy view");
            Assert.Null(registration.Current);
            Assert.Equal(0, created);
            DockViewManager.GetInstance().ShowAllViews();
            Assert.NotNull(registration.Current);
            Assert.Equal(1, created);
        });
    }

    [Fact]
    public void BackgroundCameraPreviewDoesNotCreateAView()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new Scope();
            using var camera = new DeviceCamera(Resource("background"));
            using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 2, Height = 1, SourceBpp = 8, Channels = 1
            }, 2, 0);
            camera.PublishLocalPreview(frame, null, forceDisplay: true);
            Pump();
            Assert.Null(camera.ExistingView);
            Assert.Null(camera.GetType().GetField("pendingPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(camera));
        });
    }

    [Theory]
    [InlineData("camera")]
    [InlineData("algorithm")]
    [InlineData("calibration")]
    public void DevicePanelDoesNotCreateAnImageView(string kind)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new Scope();
            using DeviceService device = CreateDevice(kind);
            Assert.NotNull(device.GetDisplayControl());
            Assert.Null(Registration(device).Current);
            Assert.Empty(scope.Pane.Children);
        });
    }

    [Theory]
    [InlineData("camera")]
    [InlineData("algorithm")]
    [InlineData("calibration")]
    public void DirectDeviceViewAccessStillReleasesTheViewOnDocumentClose(string kind)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new Scope();
            using DeviceService device = CreateDevice(kind);
            ImageView image = Image(device);
            var registration = Registration(device);
            DockViewManager.GetInstance().ActiveView(registration.Current!);
            Assert.Single(scope.Pane.Children).Close();
            Assert.Null(registration.Current);
            Assert.Throws<ObjectDisposedException>(() => image.RegisterSettingsProvider(() => []));
        });
    }

    [Theory]
    [InlineData("camera")]
    [InlineData("algorithm")]
    [InlineData("calibration")]
    public void CancelledCloseAndTabSwitchKeepTheSameUsableImage(string kind)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new Scope();
            using DeviceService device = CreateDevice(kind);
            DockViewRegistration registration = Registration(device);
            var manager = DockViewManager.GetInstance();
            manager.OpenView(registration);
            Control control = registration.Current!;
            ImageView image = Image(device);
            image.SetImageSource(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null));
            var source = image.ViewBitmapSource;
            var document = Assert.Single(scope.Pane.Children);
            var other = new UserControl();
            manager.ActiveView(other);
            manager.SelectView(control);
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Same(source, image.ViewBitmapSource);

            EventHandler<CancelEventArgs> cancel = (_, e) => e.Cancel = true;
            document.Closing += cancel;
            document.Close();
            manager.OpenView(registration);
            Assert.Same(control, registration.Current);
            Assert.Same(source, image.ViewBitmapSource);
            Assert.Contains(document, scope.Pane.Children);
            Assert.Equal(2, scope.Pane.Children.Count);
            document.Closing -= cancel;
            document.Close();
            Assert.Null(registration.Current);
            Assert.Null(document.Content);
            Assert.Null(image.ViewBitmapSource);
            Assert.Throws<ObjectDisposedException>(() => image.RegisterSettingsProvider(() => []));
            manager.OpenView(registration);
            Assert.NotSame(control, registration.Current);
            Image(device).SetImageSource(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null));
            Assert.NotNull(Image(device).ViewBitmapSource);
            manager.RemoveView(other);
        });
    }

    [Fact]
    public void QueuedPreviewUsesTheNewViewAfterCloseAndReopen()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new Scope();
            using var camera = new DeviceCamera(Resource("queued"));
            var manager = DockViewManager.GetInstance();
            manager.OpenView(camera.ViewRegistration);
            var previous = camera.View;
            using var first = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 2, Height = 1, SourceBpp = 8, Channels = 1 }, 2, 0);
            camera.PublishLocalPreview(first, null, forceDisplay: true);
            Assert.Single(scope.Pane.Children).Close();
            manager.OpenView(camera.ViewRegistration);
            var current = camera.View;
            using var second = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 4, Height = 1, SourceBpp = 8, Channels = 1 }, 4, 0);
            camera.PublishLocalPreview(second, null, forceDisplay: true);
            Pump();
            Assert.NotSame(previous, current);
            Assert.Null(previous.ImageView.ViewBitmapSource);
            Assert.Equal(4, Assert.IsAssignableFrom<BitmapSource>(current.ImageView.ViewBitmapSource).PixelWidth);
        });
    }

    [Fact]
    public void RealtimePreviewDetachesAndReconnectsWithoutChangingVideoState()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new Scope();
            using var camera = new DeviceCamera(Resource("realtime"));
            var display = Assert.IsType<DisplayCamera>(camera.GetDisplayControl());
            camera.DisplayConfig.IsLocalVideoOpen = true;
            var pipeline = typeof(DisplayCamera).GetField("_localRealtimePipeline", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(display)!;
            var imageField = pipeline.GetType().GetField("_imageView", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var manager = DockViewManager.GetInstance();
            manager.OpenView(camera.ViewRegistration);
            var previous = camera.View;
            Assert.Same(previous.ImageView, imageField.GetValue(pipeline));
            Assert.Single(scope.Pane.Children).Close();
            Assert.True(camera.DisplayConfig.IsLocalVideoOpen);
            Assert.Null(camera.ExistingView);
            Assert.Null(imageField.GetValue(pipeline));
            manager.OpenView(camera.ViewRegistration);
            var current = camera.View;
            Assert.NotSame(previous, current);
            Assert.Same(current.ImageView, imageField.GetValue(pipeline));
        });
    }

    [Theory]
    [InlineData("camera")]
    [InlineData("algorithm")]
    [InlineData("calibration")]
    public async Task ClosedViewsAndRawBuffersCanBeCollectedWhileDeviceStaysAlive(string kind)
    {
        using var sample = new RawFile();
        Scope? scope = null;
        DeviceService? device = null;
        try
        {
            WpfTestHost.Invoke(() => { scope = new Scope(); device = CreateDevice(kind); device.GetDisplayControl(); });
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Task loading = WpfTestHost.Invoke(() => OpenRaw(device!, sample.Path));
                await loading.WaitAsync(TimeSpan.FromSeconds(15));
                // Publication runs before the opener releases its load gate. Await that completion,
                // so the close assertions observe cleanup rather than an in-flight load continuation.
                SemaphoreSlim gate = WpfTestHost.Invoke(() => (SemaphoreSlim)typeof(CVRawOpen)
                    .GetField("_rawOpenGate", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(Image(device!).EditorContext.IImageOpen)!);
                Assert.True(await gate.WaitAsync(TimeSpan.FromSeconds(15)));
                gate.Release();
                WeakReference[] references = WpfTestHost.Invoke(() => CloseAndCapture(device!));
                Collect(references);
                Assert.All(references, reference => Assert.False(reference.IsAlive,
                    "A live device must not retain its closed view, bitmap or pinned RAW pixels."));
                WpfTestHost.Invoke(() => Assert.Null(Registration(device!).Current));
            }
        }
        finally
        {
            WpfTestHost.Invoke(() => { device?.Dispose(); scope?.Dispose(); });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeImageClearAndCloseReclaimWithoutTestInitiatedCollection(bool closeView)
    {
        using var sample = new RawFile(4096, 3072);
        Scope? scope = null;
        DeviceService? device = null;
        Window? window = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                scope = new Scope();
                device = CreateDevice("camera");
                var docking = new AvalonDock.DockingManager { Layout = Assert.IsType<LayoutRoot>(scope.Pane.Root) };
                window = new Window { Content = docking, Width = 800, Height = 600, ShowActivated = false };
                window.Show();
            });
            Task loading = WpfTestHost.Invoke(() => OpenRaw(device!, sample.Path));
            await loading.WaitAsync(TimeSpan.FromSeconds(30));
            SemaphoreSlim gate = WpfTestHost.Invoke(() => (SemaphoreSlim)typeof(CVRawOpen)
                .GetField("_rawOpenGate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Image(device!).EditorContext.IImageOpen)!);
            Assert.True(await gate.WaitAsync(TimeSpan.FromSeconds(30)));
            gate.Release();

            (WeakReference[] references, Task reclamation) = WpfTestHost.Invoke(() => RetireLargeImage(device!, closeView));
            // Observe the product's task; do not trigger GC or request another collection here.
            Assert.NotSame(Task.CompletedTask, reclamation);
            await reclamation.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.All(references, reference => Assert.False(reference.IsAlive));
        }
        finally
        {
            WpfTestHost.Invoke(() => { window?.Close(); device?.Dispose(); scope?.Dispose(); });
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference[], Task) RetireLargeImage(DeviceService device, bool closeView)
    {
        ImageView image = Image(device);
        var opener = Assert.IsType<CVRawOpen>(image.EditorContext.IImageOpen);
        object raw = typeof(CVRawOpen).GetField("_rawPixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(opener)!;
        object pixels = raw.GetType().GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(raw)!;
        var references = new[] { new WeakReference(image.ViewBitmapSource), new WeakReference(pixels) };
        if (closeView) Assert.Single(WorkspaceManager.LayoutDocumentPane.Children).Close();
        else image.Clear();
        Assert.Null(image.ViewBitmapSource);
        return (references, ImageMemoryReclaimer.PendingCollection);
    }

    [Fact]
    public void ClearingAnEmptyOrSmallImageDoesNotScheduleCollection()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new Scope();
            using var device = CreateDevice("camera");
            ImageView image = Image(device);
            image.Clear();
            Assert.Same(Task.CompletedTask, ImageMemoryReclaimer.PendingCollection);
            image.SetImageSource(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null));
            image.Clear();
            Assert.Same(Task.CompletedTask, ImageMemoryReclaimer.PendingCollection);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeavingRawReleasesItsPixelsButToolbarRefreshPreservesMeasurement(bool calibrated)
    {
        using var sample = new RawFile(4, 3);
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(4, 3, 16, 3);
        Assert.True(CVFileUtil.WriteCIEFile(sample.Path, raw));
        var transform = RawColorTransformV1.Create();
        transform.Kind = 0; transform.Channels = 3; transform.InterleavedBgr = 1;
        transform.Coefficients = [0.1, 0.02, 0.03, 0.04, 0.2, 0.06, 0.07, 0.08, 0.3];
        var snapshot = ColorCalibrationSnapshot.Create(transform, 4, 3, 16, raw.Exp, "lifetime-test");
        if (calibrated) snapshot.Save(sample.Path, true);
        using PoiMeasurementBuffer reference = new(raw, snapshot);
        PoiMeasurementPoint[] points = [new(1, 1, 1, 1, PoiMeasurementShape.Point)];
        var expected = PoiMeasurementService.CalculateRaw(reference, points);
        string png = System.IO.Path.ChangeExtension(sample.Path, ".png");
        Scope? scope = null;
        DeviceService? device = null;
        CVRawOpen? opener = null;
        CvRawPixelBuffer? buffer = null;
        byte[]? pixels = null;
        PoiMeasurementBuffer? measurement = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                scope = new Scope();
                device = CreateDevice("camera");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[] { 1, 2, 3, 4 }, 2)));
                using var stream = File.Create(png);
                encoder.Save(stream);
            });
            await WpfTestHost.Invoke(() => OpenRaw(device!, sample.Path)).WaitAsync(TimeSpan.FromSeconds(15));
            WpfTestHost.Invoke(() =>
            {
                ImageView image = Image(device!);
                opener = Assert.IsType<CVRawOpen>(image.EditorContext.IImageOpen);
                buffer = GetOpenerField<CvRawPixelBuffer>(opener, "_rawPixels")!;
                pixels = GetRawPixels(buffer);
                Assert.Equal(raw.Data, pixels);
                measurement = GetOpenerField<PoiMeasurementBuffer>(opener, "_measurementBuffer");
                image.IEditorToolFactory.ApplyImageOpenTools(opener);
                image.IEditorToolFactory.ApplyImageOpenTools(opener);
                Assert.Same(pixels, GetRawPixels(buffer));
                if (calibrated)
                {
                    Assert.NotNull(measurement);
                    Assert.Same(pixels, measurement.RawSource!.BorrowRaw(file => file.Data));
                    Assert.Equal(expected, PoiMeasurementService.CalculateRaw(measurement, points));
                }
                else Assert.Null(measurement);
            });

            await WpfTestHost.Invoke(() => BeginOpen(Image(device!), png)).WaitAsync(TimeSpan.FromSeconds(15));
            await WpfTestHost.Invoke(() => Image(device!).PendingContentRelease).WaitAsync(TimeSpan.FromSeconds(15));
            WpfTestHost.Invoke(() =>
            {
                Assert.Null(GetRawPixels(buffer!));
                Assert.Null(GetOpenerField<PoiMeasurementBuffer>(opener!, "_measurementBuffer"));
                if (calibrated) Assert.Throws<ObjectDisposedException>(() => measurement!.RawSource!.BorrowRaw(file => file.Data));
                Assert.Equal(2, ((BitmapSource)Image(device!).ViewBitmapSource).PixelWidth);
            });

            await WpfTestHost.Invoke(() => BeginOpen(Image(device!), sample.Path)).WaitAsync(TimeSpan.FromSeconds(15));
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(opener, Image(device!).EditorContext.IImageOpen);
                var reopened = GetRawPixels(GetOpenerField<CvRawPixelBuffer>(opener!, "_rawPixels")!);
                Assert.NotSame(pixels, reopened);
                Assert.Equal(raw.Data, reopened);
                if (calibrated) Assert.Equal(expected, PoiMeasurementService.CalculateRaw(GetOpenerField<PoiMeasurementBuffer>(opener!, "_measurementBuffer")!, points));
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => { device?.Dispose(); scope?.Dispose(); });
            File.Delete(png);
        }
    }

    [Fact]
    public async Task RetiringQueuedRawLoadCannotPublishOrClearTheNextImage()
    {
        using var first = new RawFile(4, 3);
        using var second = new RawFile(3, 2);
        Scope? scope = null;
        DeviceService? device = null;
        SemaphoreSlim? gate = null;
        bool held = false;
        try
        {
            WpfTestHost.Invoke(() => { scope = new Scope(); device = CreateDevice("camera"); });
            await WpfTestHost.Invoke(() => OpenRaw(device!, first.Path)).WaitAsync(TimeSpan.FromSeconds(15));
            CVRawOpen opener = WpfTestHost.Invoke(() => Assert.IsType<CVRawOpen>(Image(device!).EditorContext.IImageOpen));
            gate = GetOpenerField<SemaphoreSlim>(opener, "_rawOpenGate")!;
            Assert.True(await gate.WaitAsync(TimeSpan.FromSeconds(15)));
            held = true;
            CvRawPixelBuffer retired = GetOpenerField<CvRawPixelBuffer>(opener, "_rawPixels")!;
            byte[] originalPixels = GetRawPixels(retired)!;
            Task load = WpfTestHost.Invoke(() =>
            {
                ImageView image = Image(device!);
                // The old request and its release both wait behind a controlled reader.
                image.OpenImage(second.Path);
                Assert.Same(originalPixels, GetRawPixels(GetOpenerField<CvRawPixelBuffer>(opener, "_rawPixels")!));
                image.Clear();
                Assert.False(image.PendingContentRelease.IsCompleted);
                return BeginOpen(image, first.Path);
            });
            gate.Release(); held = false;
            await load.WaitAsync(TimeSpan.FromSeconds(15));
            await WpfTestHost.Invoke(() => Image(device!).PendingContentRelease).WaitAsync(TimeSpan.FromSeconds(15));
            WpfTestHost.Invoke(() =>
            {
                Assert.Null(GetRawPixels(retired));
                Assert.NotNull(GetRawPixels(GetOpenerField<CvRawPixelBuffer>(opener, "_rawPixels")!));
                Assert.Equal(first.Path, Image(device!).Config.FilePath);
                Assert.Equal(4, ((BitmapSource)Image(device!).ViewBitmapSource).PixelWidth);
            });
        }
        finally
        {
            if (held) gate!.Release();
            WpfTestHost.Invoke(() => { device?.Dispose(); scope?.Dispose(); });
        }
    }

    private static T? GetOpenerField<T>(CVRawOpen opener, string name) where T : class
        => (T?)typeof(CVRawOpen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(opener);

    private static byte[]? GetRawPixels(CvRawPixelBuffer buffer)
        => (byte[]?)typeof(CvRawPixelBuffer).GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(buffer);

    private static Task BeginOpen(ImageView image, string path)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ImageViewImageSourceLoadedEventArgs>? handler = null;
        handler = (_, _) => { image.ImageSourceLoaded -= handler; completion.TrySetResult(); };
        image.ImageSourceLoaded += handler;
        image.OpenImage(path);
        return completion.Task;
    }

    private static Task OpenRaw(DeviceService device, string path)
    {
        DockViewManager.GetInstance().OpenView(Registration(device));
        ImageView image = Image(device);
        image.IEditorToolFactory.IEditorTools.Clear();
        image.IEditorToolFactory.IImageOpens[".cvraw"] = new CVRawOpen(image.EditorContext);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ImageViewImageSourceLoadedEventArgs>? handler = null;
        handler = (_, _) => { image.ImageSourceLoaded -= handler; completion.TrySetResult(); };
        image.ImageSourceLoaded += handler;
        image.OpenImage(path);
        return completion.Task;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CloseAndCapture(DeviceService device)
    {
        Control control = Registration(device).Current!;
        ImageView image = Image(device);
        using (var lease = image.AcquireImageFrame()) Assert.NotNull(lease);
        var opener = Assert.IsType<CVRawOpen>(image.EditorContext.IImageOpen);
        object raw = typeof(CVRawOpen).GetField("_rawPixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(opener)!;
        object pixels = raw.GetType().GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(raw)!;
        var references = new[] { new WeakReference(control), new WeakReference(image), new WeakReference(image.ViewBitmapSource), new WeakReference(pixels) };

        // Exercise menu closures as well as the columns retained by the global preferences.
        var list = Assert.IsType<ListView>(control.FindName("listView1"));
        var columns = Assert.IsType<ObservableCollection<GridViewColumnVisibility>>(
            control.GetType().GetProperty("GridViewColumnVisibilitys")!.GetValue(control));
        columns.First().IsVisible = false;
        control.GetType().GetMethod("ContextMenu_Opened", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, [list.ContextMenu, new RoutedEventArgs()]);

        DockViewManager.GetInstance().CloseView(control);
        Assert.Null(image.ViewBitmapSource);
        Assert.Null(raw.GetType().GetField("pixels", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(raw));
        Assert.DoesNotContain(control, DockViewManager.GetInstance().Views);
        Pump();
        return references;
    }

    private static void Collect(WeakReference[] references)
    {
        for (int i = 0; i < 3 && references.Any(reference => reference.IsAlive); i++)
        {
            WpfTestHost.Invoke(Pump);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static SysResourceModel Resource(string kind) => new()
    {
        Id = -2, Code = $"dock-lifetime-{kind}-{Guid.NewGuid():N}", Name = "Offline view lifecycle", Value = "{}"
    };

    private static DeviceService CreateDevice(string kind) => kind switch
    {
        "camera" => new DeviceCamera(Resource(kind)),
        "algorithm" => new DeviceAlgorithm(Resource(kind)),
        "calibration" => new DeviceCalibration(Resource(kind)),
        _ => throw new ArgumentException(kind)
    };

    private static DockViewRegistration Registration(DeviceService device) => device switch
    {
        DeviceCamera camera => camera.ViewRegistration,
        DeviceAlgorithm algorithm => algorithm.ViewRegistration,
        DeviceCalibration calibration => calibration.ViewRegistration,
        _ => throw new ArgumentException("Unsupported device.", nameof(device))
    };

    private static ImageView Image(DeviceService device) => device switch
    {
        DeviceCamera camera => camera.View.ImageView,
        DeviceAlgorithm algorithm => algorithm.View.ImageView,
        DeviceCalibration calibration => calibration.View.ImageView,
        _ => throw new ArgumentException("Unsupported device.", nameof(device))
    };

    private sealed class Scope : IDisposable
    {
        private readonly IConfigService? _previousConfig = ConfigService.Instance;
        private readonly LayoutDocumentPane _previousPane = WorkspaceManager.LayoutDocumentPane;
        private readonly List<ResourceDictionary> _dictionaries = [];
        private readonly DockViewManager _manager = DockViewManager.GetInstance();
        private readonly Action<Control>? _active;
        private readonly Action<Control>? _select;
        private readonly Action<Control>? _added;
        private readonly Action<Control>? _removed;
        private readonly Action<Control, string>? _title;
        private readonly Action? _showAll;
        private readonly Control? _lastActive;
        public LayoutDocumentPane Pane { get; } = new();

        public Scope()
        {
            _active = _manager.ActiveViewHandler;
            _select = _manager.SelectViewHandler;
            _added = _manager.ViewAddedHandler;
            _removed = _manager.ViewRemovedHandler;
            _title = _manager.ViewTitleChangedHandler;
            _showAll = _manager.ShowAllViewsHandler;
            _lastActive = _manager.LastActiveView;
            ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                _dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
            var root = new LayoutRoot { RootPanel = new LayoutPanel() };
            root.RootPanel.Children.Add(Pane);
            WorkspaceManager.LayoutDocumentPane = Pane;
            DockViewManagerHost.Initialize();
        }

        public void Dispose()
        {
            _manager.ActiveViewHandler = _active;
            _manager.SelectViewHandler = _select;
            _manager.ViewAddedHandler = _added;
            _manager.ViewRemovedHandler = _removed;
            _manager.ViewTitleChangedHandler = _title;
            _manager.ShowAllViewsHandler = _showAll;
            _manager.LastActiveView = _lastActive;
            WorkspaceManager.LayoutDocumentPane = _previousPane;
            ConfigService.SetInstance(_previousConfig!);
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    }

    private sealed class RawFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dock-lifetime-{Guid.NewGuid():N}.cvraw");
        public RawFile(int width = 1024, int height = 1024)
        {
            using var raw = new CVCIEFile
            {
                Version = 1, FileExtType = CVType.Raw, Cols = width, Rows = height, Bpp = 16, Channels = 3,
                Exp = [1, 1, 1], Data = new byte[width * height * 6]
            };
            Assert.True(CVFileUtil.WriteCIEFile(Path, raw));
        }
        public void Dispose() { CVFileReadCache.Release(); File.Delete(Path); }
    }
}
