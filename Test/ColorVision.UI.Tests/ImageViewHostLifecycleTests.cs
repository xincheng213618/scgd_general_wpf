using ColorVision.Engine;
using ColorVision.Engine.Impl.SolutionImpl;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.Solution.MultiImageViewer;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using FlowEngineLib.Algorithm;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class ImageViewHostLifecycleTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("roi")]
    [InlineData("poi")]
    [InlineData("keyboard")]
    public void EditorsReleaseImagesEvenWhenNeverShown(string kind)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new PresentationScope();
            Window window = CreateEditor(kind);
            ImageView view = Preview(window);
            try
            {
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
    [InlineData(false)]
    [InlineData(true)]
    public void CameraMeasurementPublishesTheCapturedOrientationAndExposure(bool withCie)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scope = new PresentationScope();
            using var device = new DeviceCamera(Resource("camera-capture"));
            var window = new CameraLocalWindow(device);
            RemoveHandler(window, "Window_Loaded", typeof(RoutedEventHandler));
            RemoveHandler(window, "Window_Closing", typeof(CancelEventHandler));
            ImageView view = Preview(window);
            var opener = new CVRawOpen(view.EditorContext);
            view.IEditorToolFactory.IImageOpens[".cvcie"] = opener;
            try
            {
                byte[] raw = [1, 2, 3, 4, 5, 6], oriented = [3, 2, 1, 6, 5, 4];
                byte[] capturedRaw;
                LocalCameraPreview preview;
                using (LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
                {
                    Width = 3, Height = 2, Channels = 1, SourceBpp = 8, CieBpp = 32, Exposure = [9],
                    FlipMode = CVImageFlipMode.Y, IsMirrorReady = withCie,
                    PrimaryBufferKind = withCie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
                }, raw.Length, withCie ? raw.Length * sizeof(float) : 0))
                {
                    using LocalFlowFrameLease lease = frame.Acquire();
                    Marshal.Copy(raw, 0, lease.RawPointer, raw.Length);
                    if (withCie)
                    {
                        Marshal.Copy(raw.Select(value => (float)value).ToArray(), 0, lease.CiePointer, raw.Length);
                        LocalFrameMirrorService.ApplyPending(frame);
                    }
                    preview = LocalCameraPreview.Create(frame);
                    capturedRaw = lease.CopyRawToArray();
                }

                device.DisplayConfig.ExpTime = 99;
                window.ShowCapturedFrame(preview, capturedRaw);
                Assert.Same(preview.Bitmap, view.ViewBitmapSource);
                Assert.True(preview.Bitmap.IsFrozen);
                byte[] display = new byte[raw.Length];
                preview.Bitmap.CopyPixels(display, 3, 0);
                Assert.Equal(oriented, display);
                Assert.Equal(raw, window.srcrawArray);
                if (withCie)
                {
                    Assert.Same(opener, view.EditorContext.IImageOpen);
                    Assert.Equal(new[] { 9f }, view.Config.GetProperties<float[]>("Exp"));
                    Assert.Equal(oriented.Select(value => (float)value),
                        Enumerable.Range(0, raw.Length).Select(i => BitConverter.ToSingle(window.rawArray, i * sizeof(float))));
                }
                else Assert.Empty(window.rawArray);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("file")]
    [InlineData("result")]
    [InlineData("keyboard")]
    [InlineData("multi")]
    public async Task StandalonePreviewOwnersDisposeTheirImageView(string kind)
    {
        using var file = new ImageFile();
        PresentationScope? scope = null;
        Window? owner = null;
        ImageView? ownerView = null;
        Window[]? before = null;
        try
        {
            var (openedWindow, openedPreview, ready) = WpfTestHost.Invoke(() =>
            {
                scope = new PresentationScope();
                owner = CreateEditor("keyboard");
                ownerView = Preview(owner);
                PlaceOffscreen(owner);
                owner.Show();
                PumpDispatcher();
                Application.Current.MainWindow = owner;
                SetPixels(ownerView);
                before = Application.Current.Windows.Cast<Window>().ToArray();

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
                Window popup = Assert.Single(Application.Current.Windows.Cast<Window>().Except(before));
                PlaceOffscreen(popup);
                ImageView preview = popup.Content is MultiImageViewer viewer
                    ? Assert.IsType<ImageView>(viewer.FindName("ImageView"))
                    : Assert.IsType<ImageView>(popup.Content);
                Task loaded = preview.ViewBitmapSource == null ? ObserveNextImagePublication(preview) : Task.CompletedTask;
                return (popup, preview, loaded);
            });
            await ready.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.NotNull(openedPreview.ViewBitmapSource);
                using (openedPreview.AcquireImageFrame()) { }
                openedWindow.Close();
                PumpDispatcher();
                AssertDisposed(openedPreview);
                if (openedWindow.Content is MultiImageViewer closedViewer) Assert.Empty(closedViewer.ImageFiles);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                try
                {
                    if (before != null)
                    {
                        foreach (Window window in Application.Current.Windows.Cast<Window>().Except(before).ToArray())
                        {
                            window.Close();
                            (window.Content as IDisposable)?.Dispose();
                        }
                    }
                    owner?.Close();
                    ownerView?.Dispose();
                    PumpDispatcher();
                }
                finally { scope?.Dispose(); }
            });
        }
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
                Assert.False(pending.IsCompleted); // Metadata completion still has to return to this dispatcher.
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

    [Theory]
    [InlineData("cached")]
    [InlineData("source changed")]
    [InlineData("thumbnail first")]
    public async Task FolderOpensTheInitialImageWhileCachedDimensionsArePending(string result)
    {
        using var file = new ImageFile();
        using var gate = new ThumbnailMetadataQueryGate(file);
        gate.AddCachedDimensions(file.Path, 1024, 512);
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: false, cache: gate.Cache));
        Task? loading = null;
        try
        {
            Task metadataQueryStarted = gate.HoldQueries();
            string folder = Path.GetDirectoryName(file.Path)!;
            var (request, published) = WpfTestHost.Invoke(() =>
            {
                var publication = ObserveNextImagePublication(scope.View);
                return (scope.Viewer.LoadFromFolderAsync(folder), publication);
            });
            loading = request;
            await metadataQueryStarted.WaitAsync(TimeSpan.FromSeconds(10));
            await published.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(request.IsCompleted);
            ImageFileInfo item = WpfTestHost.Invoke(() =>
            {
                Assert.Equal(folder, scope.Viewer.CurrentFolderPath);
                var current = Assert.Single(scope.Viewer.ImageFiles);
                Assert.Same(current, scope.Viewer.GetSelectedFile());
                Assert.Equal(file.Path, scope.View.Config.FilePath);
                Assert.Equal((0, 0), (current.ImageWidth, current.ImageHeight));
                return current;
            });

            if (result == "source changed") File.SetLastWriteTime(file.Path, item.LastModified.AddMinutes(1));
            if (result == "thumbnail first")
                await WpfTestHost.Invoke(() => item.LoadThumbnailAsync(useCache: false)).WaitAsync(TimeSpan.FromSeconds(10));

            gate.Release();
            await request.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                var expected = result switch
                {
                    "cached" => (1024, 512),
                    "thumbnail first" => (64, 32),
                    _ => (0, 0),
                };
                Assert.Equal(expected, (item.ImageWidth, item.ImageHeight));
                Assert.Same(item, scope.Viewer.GetSelectedFile());
            });
        }
        finally
        {
            gate.Release();
            try
            {
                // The query owns the manager lock; wait for it before restoring or disposing anything.
                if (loading != null) await loading.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally { WpfTestHost.Invoke(scope.Dispose); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearingOrReplacingAListRetiresPendingCachedDimensions(bool replace)
    {
        using var file = new ImageFile();
        using var replacement = new ImageFile();
        using var gate = new ThumbnailMetadataQueryGate(file);
        gate.AddCachedDimensions(file.Path, 1024, 512);
        gate.AddCachedDimensions(replacement.Path, 640, 480);
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: false, cache: gate.Cache));
        var requests = new List<Task>();
        try
        {
            Task metadataQueryStarted = gate.HoldQueries();
            ImageFileInfo first = await OpenListAsync(file.Path);
            await metadataQueryStarted.WaitAsync(TimeSpan.FromSeconds(10));

            ImageFileInfo? queued = null;
            ImageFileInfo? latest = null;
            if (replace)
            {
                queued = await OpenListAsync(replacement.Path);
                latest = await OpenListAsync(file.Path);
                // The superseded list was queued for the same slot. It completes while the first query remains held.
                await requests[1].WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotSame(first, latest);
            }
            else WpfTestHost.Invoke(scope.Viewer.Clear);

            gate.Release();
            await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal((0, 0), (first.ImageWidth, first.ImageHeight));
                if (queued != null) Assert.Equal((0, 0), (queued.ImageWidth, queued.ImageHeight));
                if (latest != null)
                {
                    Assert.Same(latest, Assert.Single(scope.Viewer.ImageFiles));
                    Assert.Same(latest, scope.Viewer.GetSelectedFile());
                    Assert.Equal((1024, 512), (latest.ImageWidth, latest.ImageHeight));
                }
                else
                {
                    Assert.Empty(scope.Viewer.ImageFiles);
                    Assert.Null(scope.Viewer.GetSelectedFile());
                    Assert.Null(scope.View.ViewBitmapSource);
                }
            });
        }
        finally
        {
            gate.Release();
            try { await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30)); }
            finally { WpfTestHost.Invoke(scope.Dispose); }
        }

        async Task<ImageFileInfo> OpenListAsync(string path)
        {
            var (request, published) = WpfTestHost.Invoke(() => LoadObservingImage(scope, [path]));
            requests.Add(request);
            await published.WaitAsync(TimeSpan.FromSeconds(10));
            return WpfTestHost.Invoke(() => Assert.Single(scope.Viewer.ImageFiles));
        }
    }

    [Fact]
    public async Task CachedThumbnailWaitsForMetadataQueryWithoutBlockingDispatcher()
    {
        using var file = new ImageFile();
        using var gate = new ThumbnailMetadataQueryGate(file);
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: false, cache: gate.Cache));
        Task? metadata = null;
        Task<Task>? startThumbnail = null;
        try
        {
            var (item, dispatcher) = WpfTestHost.Invoke(() => (new ImageFileInfo(file.Path), scope.Viewer.Dispatcher));
            Task metadataQueryStarted = gate.HoldQueries();
            metadata = Task.Run(() => gate.Cache.GetCachedInfo(file.Path));
            await metadataQueryStarted.WaitAsync(TimeSpan.FromSeconds(10));

            startThumbnail = dispatcher.InvokeAsync<Task>(() => item.LoadThumbnailAsync(useCache: true)).Task;
            Task dispatcherProgress = dispatcher.InvokeAsync(() => { }).Task;
            await dispatcherProgress.WaitAsync(TimeSpan.FromSeconds(10));
            Task thumbnail = await startThumbnail;
            Assert.False(thumbnail.IsCompleted);

            gate.Release();
            await thumbnail.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                var bitmap = Assert.IsAssignableFrom<BitmapSource>(item.Thumbnail);
                Assert.True(bitmap.IsFrozen);
                Assert.Equal((64, 32), (bitmap.PixelWidth, bitmap.PixelHeight));
                Assert.False(item.IsLoadingThumbnail);
            });
        }
        finally
        {
            gate.Release();
            try
            {
                if (metadata != null) await metadata.WaitAsync(TimeSpan.FromSeconds(30));
                if (startThumbnail != null) await startThumbnail.Unwrap().WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally { WpfTestHost.Invoke(scope.Dispose); }
        }
    }

    [Fact]
    public async Task MultiImageViewerPublishesTheFilteredAndLimitedListWithOneReset()
    {
        using var first = new ImageFile();
        using var second = new ImageFile();
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: false));
        try
        {
            var (initialLoad, initialPublication) = WpfTestHost.Invoke(() => LoadObservingImage(scope, [first.Path]));
            await initialLoad.WaitAsync(TimeSpan.FromSeconds(10));
            await initialPublication.WaitAsync(TimeSpan.FromSeconds(10));

            string[] expectedPaths = [second.Path, first.Path, .. Enumerable.Repeat(second.Path, 8)];
            List<string> paths = [Path.ChangeExtension(first.Path, ".txt"), .. expectedPaths, first.Path];
            var changes = new List<(NotifyCollectionChangedAction Action, string[] Paths)>();
            var (collection, request, publication) = WpfTestHost.Invoke(() =>
            {
                MultiImageViewer.Config.MaxDisplayCount = expectedPaths.Length;
                var items = scope.Viewer.ImageFiles;
                items.CollectionChanged += (_, args) => changes.Add((args.Action, items.Select(item => item.FilePath).ToArray()));
                var load = LoadObservingImage(scope, paths);
                return (items, load.Request, load.Published);
            });
            await request.WaitAsync(TimeSpan.FromSeconds(10));
            await publication.WaitAsync(TimeSpan.FromSeconds(10));

            WpfTestHost.Invoke(() =>
            {
                Assert.Same(collection, scope.Viewer.ImageFiles);
                var change = Assert.Single(changes);
                Assert.Equal(NotifyCollectionChangedAction.Reset, change.Action);
                Assert.Equal(expectedPaths, change.Paths);
                Assert.Equal(second.Path, scope.Viewer.GetSelectedFile()?.FilePath);
            });
        }
        finally { WpfTestHost.Invoke(scope.Dispose); }
    }

    [Fact]
    public async Task MultiImageViewerOpensInitialImageWhileThumbnailIsPending()
    {
        using var file = new ImageFile();
        using var provider = new GatedThumbnailProvider(file.Path);
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: true));
        Task? loading = null;
        try
        {
            var (request, published) = WpfTestHost.Invoke(() => LoadObservingImage(scope, [file.Path]));
            loading = request;
            await loading.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(scope.LayoutViewport);
            await provider.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            await published.WaitAsync(TimeSpan.FromSeconds(10));
            Task thumbnail = WpfTestHost.Invoke(() =>
            {
                Assert.Equal(file.Path, scope.Viewer.GetSelectedFile()?.FilePath);
                Assert.NotNull(scope.View.ViewBitmapSource);
                ImageFileInfo item = Assert.Single(scope.Viewer.ImageFiles);
                Assert.Null(item.Thumbnail);
                return WaitForItemState(item, state => state.Thumbnail != null);
            });

            provider.Complete();
            await thumbnail.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            provider.Complete();
            try { if (loading != null) await loading.WaitAsync(TimeSpan.FromSeconds(10)); }
            finally { WpfTestHost.Invoke(scope.Dispose); }
        }
    }

    [Fact]
    public async Task MultiImageViewerLoadsRealizedRowsAndDropsRecycledThumbnails()
    {
        using var first = new ImageFile();
        using var last = new ImageFile();
        using var provider = new GatedThumbnailProvider(first.Path, last.Path);
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: true));
        List<string> paths = [.. Enumerable.Repeat(first.Path, 160), last.Path];
        try
        {
            provider.Complete();
            var (request, published) = WpfTestHost.Invoke(() => LoadObservingImage(scope, paths));
            await request.WaitAsync(TimeSpan.FromSeconds(10));
            await published.WaitAsync(TimeSpan.FromSeconds(10));
            var (firstRows, lastRow, firstRowsReady) = WpfTestHost.Invoke(() =>
            {
                scope.LayoutViewport();
                ImageFileInfo tail = scope.Viewer.ImageFiles[^1];
                Assert.Null(scope.List.ItemContainerGenerator.ContainerFromItem(tail));
                Assert.Null(tail.Thumbnail);
                ImageFileInfo[] oldRows = scope.Viewer.ImageFiles.Skip(1)
                    .Where(item => scope.List.ItemContainerGenerator.ContainerFromItem(item) != null).ToArray();
                Assert.NotEmpty(oldRows);
                return (oldRows, tail, Task.WhenAll(oldRows.Select(item => WaitForItemState(item, state => state.Thumbnail != null))));
            });
            await firstRowsReady.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.InRange(provider.StartedCount, 1, paths.Count - 1);
            Assert.False(provider.SecondEntered.IsCompleted);
            WpfTestHost.Invoke(() => ReportCounts("Initial viewport"));

            Task tailReady = WpfTestHost.Invoke(() =>
            {
                Task ready = WaitForItemState(lastRow, state => state.Thumbnail != null);
                scope.List.ScrollIntoView(lastRow);
                scope.LayoutViewport();
                return ready;
            });
            await tailReady.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(provider.SecondEntered.IsCompleted);
            WpfTestHost.Invoke(() =>
            {
                scope.LayoutViewport();
                Assert.NotNull(scope.List.ItemContainerGenerator.ContainerFromItem(lastRow));
                Assert.Equal(first.Path, scope.View.Config.FilePath);
                ImageFileInfo[] recycled = firstRows.Where(item => scope.List.ItemContainerGenerator.ContainerFromItem(item) == null).ToArray();
                Assert.NotEmpty(recycled);
                Assert.All(recycled, item => Assert.Null(item.Thumbnail));
                ReportCounts("After scrolling");
            });
        }
        finally { WpfTestHost.Invoke(scope.Dispose); }

        void ReportCounts(string phase)
        {
            int realized = scope.Viewer.ImageFiles.Count(item => scope.List.ItemContainerGenerator.ContainerFromItem(item) != null);
            int retained = scope.Viewer.ImageFiles.Count(item => item.Thumbnail != null);
            output.WriteLine($"{phase}: {realized} realized, {retained} retained, {provider.StartedCount} provider requests, {paths.Count} total items.");
        }
    }

    [Fact]
    public async Task MultiImageViewerReloadsTheSameSingleImageAfterClearingTheView()
    {
        using var file = new ImageFile();
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: false));
        try
        {
            var (firstRequest, firstPublication) = WpfTestHost.Invoke(() => LoadObservingImage(scope, [file.Path]));
            await firstRequest.WaitAsync(TimeSpan.FromSeconds(10));
            var first = await firstPublication.WaitAsync(TimeSpan.FromSeconds(10));

            var (secondRequest, secondPublication) = WpfTestHost.Invoke(() => LoadObservingImage(scope, [file.Path]));
            await secondRequest.WaitAsync(TimeSpan.FromSeconds(10));
            var second = await secondPublication.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(second.ImageRevision > first.ImageRevision);
            WpfTestHost.Invoke(() => Assert.Equal(file.Path, scope.View.Config.FilePath));
        }
        finally { WpfTestHost.Invoke(scope.Dispose); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultiImageViewerIdentifiesFailedSelectionAndRefreshesThatFile(bool firstSelectionFails)
    {
        using var first = new ImageFile();
        string unavailable = Path.Combine(Path.GetDirectoryName(first.Path)!, "unavailable.png");
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: false));
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        try
        {
            string[] paths = firstSelectionFails ? [unavailable, first.Path] : [first.Path, unavailable];
            await WpfTestHost.Invoke(() => scope.Viewer.LoadFromFilesAsync(paths)).WaitAsync(timeout);
            BitmapSource? previous = null;
            if (!firstSelectionFails)
            {
                await WpfTestHost.Invoke(() => scope.View.ImageOpenCompletion).WaitAsync(timeout);
                previous = WpfTestHost.Invoke(() => Assert.IsAssignableFrom<BitmapSource>(scope.View.ViewBitmapSource));
                WpfTestHost.Invoke(() => scope.Viewer.SelectFile(unavailable));
            }
            Task failed = WpfTestHost.Invoke(() => scope.View.ImageOpenCompletion);
            await Assert.ThrowsAsync<FileNotFoundException>(() => failed.WaitAsync(timeout));

            WpfTestHost.Invoke(() =>
            {
                PumpDispatcher();
                Assert.Equal(unavailable, scope.Viewer.GetSelectedFile()?.FilePath);
                Assert.Same(previous, scope.View.ViewBitmapSource);
                var status = scope.Viewer.FindName("PreviewStatusText") as TextBlock;
                Assert.True(status is { Visibility: Visibility.Visible }
                    && status.Text.Contains(Path.GetFileName(unavailable), StringComparison.Ordinal),
                    "A failed preview must identify the selected file even when old pixels remain visible.");
            });

            File.Copy(first.Path, unavailable);
            Task recovered = WpfTestHost.Invoke(() =>
            {
                TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
                scope.View.ImageSourceLoaded += (_, _) => ready.TrySetResult();
                var refresh = Assert.IsType<Button>(scope.Viewer.FindName("RefreshButton"));
                refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return ready.Task;
            });
            await recovered.WaitAsync(timeout);
            await WpfTestHost.Invoke(() => scope.View.ImageOpenCompletion).WaitAsync(timeout);
            WpfTestHost.Invoke(() =>
            {
                PumpDispatcher();
                Assert.Equal(unavailable, scope.Viewer.GetSelectedFile()?.FilePath);
                Assert.Equal(unavailable, scope.View.Config.FilePath);
                Assert.NotNull(scope.View.ViewBitmapSource);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<TextBlock>(scope.Viewer.FindName("PreviewStatusText")).Visibility);
                scope.Viewer.Clear();
                Assert.Equal(Visibility.Visible, Assert.IsType<TextBlock>(scope.Viewer.FindName("NoImageHint")).Visibility);
            });
        }
        finally
        {
            WpfTestHost.Invoke(scope.Dispose);
            File.Delete(unavailable);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultiImageViewerRetiresPendingThumbnailsWithoutPublishingStaleResults(bool replace)
    {
        using var oldFile = new ImageFile();
        using var replacement = new ImageFile();
        using var provider = new GatedThumbnailProvider(oldFile.Path);
        var scope = WpfTestHost.Invoke(() => new MultiImageScope(showThumbnails: true));
        try
        {
            Task oldLoad = WpfTestHost.Invoke(() => scope.Viewer.LoadFromFilesAsync([oldFile.Path]));
            await oldLoad.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(scope.LayoutViewport);
            await provider.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            var (retired, drained) = WpfTestHost.Invoke(() =>
            {
                ImageFileInfo item = Assert.Single(scope.Viewer.ImageFiles);
                Assert.True(item.IsLoadingThumbnail);
                return (item, WaitForItemState(item, state => !state.IsLoadingThumbnail));
            });

            if (replace)
            {
                var (request, published) = WpfTestHost.Invoke(() => LoadObservingImage(scope, [replacement.Path]));
                await request.WaitAsync(TimeSpan.FromSeconds(10));
                await published.WaitAsync(TimeSpan.FromSeconds(10));
            }
            else WpfTestHost.Invoke(scope.Viewer.Clear);

            provider.Complete();
            await drained.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, provider.StartedCount);
            WpfTestHost.Invoke(() =>
            {
                Assert.Null(retired.Thumbnail);
                if (replace)
                {
                    Assert.Equal(replacement.Path, Assert.Single(scope.Viewer.ImageFiles).FilePath);
                    Assert.Equal(replacement.Path, scope.View.Config.FilePath);
                }
                else
                {
                    Assert.Empty(scope.Viewer.ImageFiles);
                    Assert.Null(scope.View.ViewBitmapSource);
                }
            });
        }
        finally
        {
            provider.Complete();
            WpfTestHost.Invoke(scope.Dispose);
        }
    }

    private static Task WaitForItemState(ImageFileInfo item, Func<ImageFileInfo, bool> condition)
    {
        if (condition(item)) return Task.CompletedTask;
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        item.PropertyChanged += OnPropertyChanged;
        return ready.Task;

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (!condition(item)) return;
            item.PropertyChanged -= OnPropertyChanged;
            ready.TrySetResult();
        }
    }

    private static (Task Request, Task<ImageViewImageSourceLoadedEventArgs> Published) LoadObservingImage(MultiImageScope scope, List<string> paths)
    {
        Task<ImageViewImageSourceLoadedEventArgs> published = ObserveNextImagePublication(scope.View);
        return (scope.Viewer.LoadFromFilesAsync(paths), published);
    }

    private static Task<ImageViewImageSourceLoadedEventArgs> ObserveNextImagePublication(ImageView view)
    {
        TaskCompletionSource<ImageViewImageSourceLoadedEventArgs> published = new(TaskCreationOptions.RunContinuationsAsynchronously);
        view.ImageSourceLoaded += OnImageSourceLoaded;
        return published.Task;

        void OnImageSourceLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args)
        {
            view.ImageSourceLoaded -= OnImageSourceLoaded;
            published.TrySetResult(args);
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
            TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
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
            TestEnvironment.SetConfig(_previousConfig!);
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    }

    private sealed class ThumbnailMetadataQueryGate : IDisposable
    {
        private readonly string _databasePath;
        private readonly SqlSugar.SqlSugarClient _database;
        private readonly ManualResetEventSlim _release = new();

        public ThumbnailMetadataQueryGate(ImageFile file)
        {
            _databasePath = Path.Combine(Path.GetDirectoryName(file.Path)!, "ThumbnailCache.db");
            _database = new SqlSugar.SqlSugarClient(new SqlSugar.ConnectionConfig
            {
                ConnectionString = $"Data Source={_databasePath};Pooling=False", DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = true
            });
            try
            {
                _database.CodeFirst.InitTables<ThumbnailCacheEntry>();
                Cache = (ThumbnailCacheManager)RuntimeHelpers.GetUninitializedObject(typeof(ThumbnailCacheManager));
                typeof(ThumbnailCacheManager).GetField("_db", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Cache, _database);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public ThumbnailCacheManager Cache { get; }

        public void AddCachedDimensions(string path, int width, int height)
        {
            File.SetLastWriteTime(path, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local));
            var file = new FileInfo(path);
            _database.Insertable(new ThumbnailCacheEntry
            {
                FilePath = path, FileLastModified = file.LastWriteTime, FileSize = file.Length,
                OriginalWidth = width, OriginalHeight = height,
            }).ExecuteCommand();
        }

        public Task HoldQueries()
        {
            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _database.Aop.OnLogExecuting = (_, _) =>
            {
                started.TrySetResult();
                // Safety timeout only. Each test explicitly releases the query gate.
                if (!_release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Metadata query gate was not released.");
            };
            return started.Task;
        }

        public void Release() => _release.Set();

        public void Dispose()
        {
            _database.Dispose();
            _release.Dispose();
            foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(_databasePath + suffix)) File.Delete(_databasePath + suffix);
        }
    }

    private sealed class MultiImageScope : IDisposable
    {
        private static readonly FieldInfo CacheInstance = typeof(ThumbnailCacheManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly PresentationScope _presentation = new();
        private readonly object? _previousCache = CacheInstance.GetValue(null);
        private readonly Window? _window;

        public MultiImageScope(bool showThumbnails, ThumbnailCacheManager? cache = null)
        {
            // Cached dimensions can still be read with thumbnails disabled. Keep the fixtures off the user's cache.
            CacheInstance.SetValue(null, cache ?? RuntimeHelpers.GetUninitializedObject(typeof(ThumbnailCacheManager)));
            MultiImageViewer.Config.ShowThumbnail = showThumbnails;
            MultiImageViewer.Config.EnableThumbnailCache = false;
            Viewer = new MultiImageViewer();
            View = Assert.IsType<ImageView>(Viewer.FindName("ImageView"));
            List = Assert.IsAssignableFrom<ListBox>(Viewer.FindName("FileListBox"));
            if (showThumbnails)
            {
                _window = new Window { Content = Viewer };
                PlaceOffscreen(_window);
                _window.Height = 320;
                _window.Show();
                LayoutViewport();
            }
        }

        public MultiImageViewer Viewer { get; }
        public ImageView View { get; }
        public ListBox List { get; }

        public void LayoutViewport()
        {
            Viewer.UpdateLayout();
            PumpDispatcher();
            Viewer.UpdateLayout();
        }

        public void Dispose()
        {
            try
            {
                Viewer.Dispose();
                _window?.Close();
            }
            finally
            {
                CacheInstance.SetValue(null, _previousCache);
                _presentation.Dispose();
            }
        }
    }

    private sealed class GatedThumbnailProvider : IThumbnailProvider, IDisposable
    {
        private readonly string _path;
        private readonly string? _secondPath;
        private readonly BitmapSource _bitmap;
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<BitmapSource?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;

        public GatedThumbnailProvider(string path, string? secondPath = null)
        {
            _path = path;
            _secondPath = secondPath;
            _bitmap = WpfTestHost.Invoke(() =>
            {
                var bitmap = new WriteableBitmap(2, 1, 96, 96, PixelFormats.Gray8, null);
                bitmap.Freeze();
                return bitmap;
            });
            ThumbnailProviderFactory.RegisterProvider(this);
        }

        public int Order => 0;
        public Task Entered => _entered.Task;
        public Task SecondEntered => _secondEntered.Task;
        public int StartedCount => Volatile.Read(ref _started);
        public bool CanHandle(string filePath) => filePath == _path || filePath == _secondPath;
        public (int width, int height) GetImageDimensions(string filePath) => (64, 32);

        public Task<BitmapSource?> GenerateThumbnailAsync(string filePath, int maxSize)
        {
            if (Interlocked.Increment(ref _started) == 1) _entered.TrySetResult();
            if (filePath == _secondPath) _secondEntered.TrySetResult();
            return _result.Task;
        }

        public void Complete() => _result.TrySetResult(_bitmap);

        public void Dispose()
        {
            Complete();
            var providers = (List<IThumbnailProvider>)typeof(ThumbnailProviderFactory).GetField("_providers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            providers.Remove(this);
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
