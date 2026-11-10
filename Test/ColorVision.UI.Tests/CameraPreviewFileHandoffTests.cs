using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CameraPreviewFileHandoffTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"cv-preview-handoff-{Guid.NewGuid():N}");
    private readonly IConfigService previousConfig = ConfigService.Instance;
    private readonly ImageView view;
    private readonly CVRawOpen opener;
    private readonly CameraRealtimeFramePipeline pipeline;

    public CameraPreviewFileHandoffTests()
    {
        Directory.CreateDirectory(root);
        CVFileReadCache.Release();
        ConfigService.SetInstance(new ConfigHandler());
        (view, opener, pipeline) = WpfTestHost.Invoke(() =>
        {
            Application app = Application.Current;
            app.Resources["TextBox.Small"] = new Style(typeof(TextBox));
            app.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            app.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            app.Resources["ToolBarImage"] = new Style(typeof(Image));
            app.Resources["BaseStyle"] = new Style(typeof(Control));
            app.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            app.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            DefaultRealtimeCameraConfig.Current.IsCalArtculation = false;
            ViewCameraConfig.Instance.AutoRefreshView = false;
            CvcieDisplayConfig.Current.EnableTrueColor = false;
            ImageView created = new() { EnableEditorImageServices = false };
            created.IEditorToolFactory.IEditorTools.Clear();
            created.EditorContext.ProcessingContext.DisplayEffects.PseudoColor.IsEnabled = false;
            CVRawOpen raw = new(created.EditorContext);
            created.IEditorToolFactory.IImageOpens[".cvraw"] = raw;
            created.IEditorToolFactory.IImageOpens[".cvcie"] = raw;
            return (created, raw, new CameraRealtimeFramePipeline());
        });
    }

    [Fact]
    public async Task VideoStartRetiresPendingFileOpenBeforeItCanOverwriteAFrame()
    {
        string path = WriteRaw("pending.cvraw", 13);
        SemaphoreSlim gate = OpenGate;
        await gate.WaitAsync();
        bool held = true;
        try
        {
            WpfTestHost.Invoke(() => { view.OpenImage(path); pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false); });
            await PresentAsync([71, 72, 73, 74, 75, 76], 8, 1);
            BitmapSource video = WpfTestHost.Invoke(() => (BitmapSource)view.ViewBitmapSource);
            gate.Release();
            held = false;
            await gate.WaitAsync();
            held = true;
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(video, view.ViewBitmapSource);
                Assert.Equal(new byte[] { 71, 72, 73, 74, 75, 76 }, Pixels(video));
                Assert.True(string.IsNullOrEmpty(view.Config.FilePath));
                Assert.Null(view.EditorContext.IImageOpen);
            });
        }
        finally { if (held) gate.Release(); }
    }

    [Fact]
    public async Task VideoStartDetachesOldFileCalibrationAndMeasurementTools()
    {
        string path = Path.Combine(root, "calibrated.cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 2, 16, 3);
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        ColorCalibrationSnapshot.Create(RawColorTransformV1.Create(), 3, 2, 16, raw.Exp, "previous-file").Save(path, true);
        await OpenAsync(path);
        PoiMeasurementBuffer previous = WpfTestHost.Invoke(() =>
        {
            Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
            return Measurement!;
        });
        WpfTestHost.Invoke(() => pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false));
        await PresentAsync([71, 72, 73, 74, 75, 76], 8, 1);
        WpfTestHost.Invoke(() =>
        {
            Assert.False(view.Config.GetProperties<bool>("HasCieMeasurements"));
            Assert.False(view.Config.GetProperties<bool>("IsCVCIE"));
            Assert.Null(view.EditorContext.IImageOpen);
            Assert.Null(Measurement);
            Assert.Empty(view.ComboBoxLayers.Items);
            Assert.Null(view.EditorContext.ProcessingContext.ProfileMeasurementSources);
        });
        Assert.Throws<ObjectDisposedException>(() => previous.RawSource!.CreateChannel(0));
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    [InlineData(16, 1)]
    [InlineData(16, 3)]
    public async Task VideoPixelsStayIndependentOfProducerAndFileCacheAndStoppingPreservesNewFile(int bpp, int channels)
    {
        string path = WriteRaw("result.cvraw", 29);
        WpfTestHost.Invoke(() => pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false));
        long allocations = CVFileReadCache.GetSnapshot().AllocationCount;
        byte[] samples = Enumerable.Range(0, 6 * channels * (bpp / 8)).Select(i => (byte)(i * 5 + 3)).ToArray();
        byte[] expected = (byte[])samples.Clone();
        await PresentAsync(samples, bpp, channels, clearProducer: true);
        BitmapSource video = WpfTestHost.Invoke(() => (BitmapSource)view.ViewBitmapSource);
        Assert.True(video.IsFrozen);
        Assert.Equal(expected, Pixels(video));
        Assert.Equal(allocations, CVFileReadCache.GetSnapshot().AllocationCount);
        var released = await LocalCalibrationCacheService.ReleaseAllAsync(Array.Empty<DeviceCamera>());
        Assert.True(released.Succeeded, string.Join("; ", released.Errors));
        Assert.Equal(expected, Pixels(video));
        WpfTestHost.Invoke(() => pipeline.Stop(resetRealtime: true));
        await OpenAsync(path);
        WpfTestHost.Invoke(() =>
        {
            var file = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
            Assert.NotSame(video, file);
            Assert.All(Pixels(file), value => Assert.Equal(29, value));
            pipeline.Stop(resetRealtime: true, clearImageSource: true);
            Assert.Same(file, view.Presentation.DisplaySource);
        });
        Assert.Equal(expected, Pixels(video));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectCaptureSnapshotSurvivesLateFileReadCacheReleaseAndFrameDisposal(bool withCie)
    {
        await OpenAsync(WriteRaw("initial.cvraw", 11));
        string late = WriteRaw("late.cvraw", 29);
        SemaphoreSlim gate = OpenGate;
        await gate.WaitAsync();
        bool held = true;
        try
        {
            WpfTestHost.Invoke(() => view.OpenImage(late));
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 3, Height = 2, Channels = 1, SourceBpp = 8, CieBpp = 32, Exposure = [10],
                PrimaryBufferKind = withCie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
            }, 6, withCie ? 24 : 0);
            byte[] raw = [51, 52, 53, 54, 55, 56];
            float[] cie = [1, 2, 3, 4, 5, 6];
            using (LocalFlowFrameLease lease = frame.Acquire())
            {
                Marshal.Copy(raw, 0, lease.RawPointer, raw.Length);
                if (withCie) Marshal.Copy(cie, 0, lease.CiePointer, cie.Length);
            }
            LocalCameraPreview preview = WpfTestHost.Invoke(() => LocalCameraPreview.Create(frame));
            using (LocalFlowFrameLease lease = frame.Acquire())
            {
                Marshal.Copy(new byte[6], 0, lease.RawPointer, 6);
                if (withCie) Marshal.Copy(new byte[24], 0, lease.CiePointer, 24);
            }
            frame.Dispose();
            WpfTestHost.Invoke(() => preview.Show(view));
            WriteRaw("other.cvraw", 99);
            CVFileReadCache.Release();
            gate.Release();
            held = false;
            await gate.WaitAsync();
            held = true;
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(raw, Pixels((BitmapSource)view.ViewBitmapSource));
                Assert.True(string.IsNullOrEmpty(view.Config.FilePath));
                if (withCie)
                {
                    Assert.Same(opener, view.EditorContext.IImageOpen);
                    Assert.True(view.Config.GetProperties<bool>("IsCVCIE"));
                    float[] measured = Measurement!.Borrow((pointer, _) =>
                    {
                        float[] copy = new float[6];
                        Marshal.Copy(pointer, copy, 0, copy.Length);
                        return copy;
                    });
                    Assert.Equal(cie, measured);
                }
                else Assert.Null(Measurement);
            });
            gate.Release();
            held = false;
            await OpenAsync(late);
            WpfTestHost.Invoke(() => Assert.All(Pixels((BitmapSource)view.ViewBitmapSource), value => Assert.Equal(29, value)));
            Assert.Equal(raw, Pixels(preview.Bitmap));
        }
        finally { if (held) gate.Release(); }
    }

    [Fact]
    public async Task VideoRestartDiscardsQueuedFrameBeforeNewSessionPublishes()
    {
        WriteableBitmap previous = WpfTestHost.Invoke(() =>
        {
            WriteableBitmap bitmap = new(3, 2, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 3, 2), new byte[] { 41, 42, 43, 44, 45, 46 }, 3, 0);
            view.OpenImage(bitmap);
            pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
            pipeline.SubmitFrame(new byte[] { 11, 12, 13, 14, 15, 16 }, 6, 3, 2, 1, 8, 3);
            // The first session's Background presentation is queued, but has not run on this dispatcher yet.
            pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
            return bitmap;
        });
        await view.Realtime.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        WpfTestHost.Invoke(() =>
        {
            Assert.Same(previous, view.ViewBitmapSource);
            Assert.False(view.EditorContext.ProcessingContext.StreamPresentation.IsActive);
        });
        await PresentAsync([71, 72, 73, 74, 75, 76], 8, 1);
        WpfTestHost.Invoke(() => Assert.Equal(new byte[] { 71, 72, 73, 74, 75, 76 }, Pixels((BitmapSource)view.ViewBitmapSource)));
    }

    [Fact]
    public async Task DelayedVideoStopDoesNotResetRestartedPreview()
    {
        byte[] samples = [71, 72, 73, 74, 75, 76];
        Task newFrame = WpfTestHost.Invoke(() =>
        {
            using var processing = view.Dispatcher.DisableProcessing();
            pipeline.Start(view);
            Task.Run(() => pipeline.Stop(resetRealtime: true)).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

            // The old stop has returned, but its UI cleanup cannot run until we leave this callback.
            pipeline.Start(view);
            pipeline.SubmitFrame(samples, samples.Length, 3, 2, 1, 8, 3);
            return view.Realtime.Completion;
        });

        await newFrame.WaitAsync(TimeSpan.FromSeconds(10));
        WpfTestHost.Invoke(() =>
        {
            Assert.Equal(1, pipeline.CurrentDisplayedFrameCount);
            Assert.Equal(samples, Pixels(Assert.IsAssignableFrom<BitmapSource>(view.ViewBitmapSource)));
            Assert.True(view.EditorContext.ProcessingContext.StreamPresentation.IsActive);
            var visuals = Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(view.ImageShow))
                .Select(index => VisualTreeHelper.GetChild(view.ImageShow, index));
            Assert.Single(visuals.OfType<RealtimeCameraOverlayVisual>());
        });
    }

    [Fact]
    public async Task PresenterSubmissionWaitingForCopyCannotJoinTheSourceCreatedByReset()
    {
        TimeSpan watchdog = TimeSpan.FromSeconds(10);
        RealtimeFramePresenter presenter = WpfTestHost.Invoke(() => view.Realtime);
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        object submitGate = typeof(RealtimeFramePresenter).GetField("_submitGate", privateInstance)!.GetValue(presenter)!;
        TaskCompletionSource<bool> submitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread callback = new(() =>
        {
            try { submitted.SetResult(presenter.SubmitFrame([17], 1, 1, PixelFormats.Gray8)); }
            catch (Exception ex) { submitted.SetException(ex); }
        }) { IsBackground = true };

        lock (submitGate)
        {
            callback.Start();
            Assert.True(SpinWait.SpinUntil(() => (callback.ThreadState & ThreadState.WaitSleepJoin) != 0, watchdog));
            WpfTestHost.Invoke(() => presenter.Reset(clearImageSource: true));
        }

        Assert.False(await submitted.Task.WaitAsync(watchdog));
        Assert.True(presenter.SubmitFrame([42], 1, 1, PixelFormats.Gray8));
        await presenter.Completion.WaitAsync(watchdog);
        WpfTestHost.Invoke(() => Assert.Equal(new byte[] { 42 }, Pixels(Assert.IsAssignableFrom<BitmapSource>(view.ViewBitmapSource))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CameraCallbackAlreadyInFocusSubmissionCannotPublishAfterStop(bool restart)
    {
        TimeSpan watchdog = TimeSpan.FromSeconds(10);
        CameraFocusFrameProcessor? focus = null;
        var focusPipeline = WpfTestHost.Invoke(() => new CameraRealtimeFramePipeline(createFocusProcessor: publish =>
            focus = new CameraFocusFrameProcessor(publish, (_, _) => default)));
        TaskCompletionSource submitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread callback = new(() =>
        {
            try
            {
                focusPipeline.SubmitFrame([17], 1, 1, 1, 1, 8, 1);
                submitted.SetResult();
            }
            catch (Exception ex) { submitted.SetException(ex); }
        }) { IsBackground = true };

        try
        {
            WpfTestHost.Invoke(() =>
            {
                DefaultRealtimeCameraConfig.Current.IsCalArtculation = true;
                focusPipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
                focusPipeline.SubmitFrame([1], 1, 1, 1, 1, 8, 1);
            });
            await view.Realtime.Completion.WaitAsync(watchdog);
            WpfTestHost.Invoke(() => Assert.Equal(new byte[] { 1 }, Pixels(Assert.IsAssignableFrom<BitmapSource>(view.ViewBitmapSource))));
            CameraFocusFrameProcessor startedFocus = Assert.IsType<CameraFocusFrameProcessor>(focus);
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            object focusGate = typeof(CameraFocusFrameProcessor).GetField("_gate", privateInstance)!.GetValue(startedFocus)!;

            lock (focusGate)
            {
                callback.Start();
                Assert.True(SpinWait.SpinUntil(() => (callback.ThreadState & ThreadState.WaitSleepJoin) != 0, watchdog));
                WpfTestHost.Invoke(() =>
                {
                    focusPipeline.Stop(resetRealtime: restart, clearImageSource: restart);
                    if (!restart) return;
                    focusPipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
                    focusPipeline.SubmitFrame([42], 1, 1, 1, 1, 8, 1);
                });
            }

            await submitted.Task.WaitAsync(watchdog);
            await view.Realtime.Completion.WaitAsync(watchdog);
            WpfTestHost.Invoke(() => Assert.Equal(new byte[] { restart ? (byte)42 : (byte)1 },
                Pixels(Assert.IsAssignableFrom<BitmapSource>(view.ViewBitmapSource))));
        }
        finally
        {
            WpfTestHost.Invoke(focusPipeline.Dispose);
            if (focus != null) await focus.Retirement.WaitAsync(watchdog);
        }
    }

    [Fact]
    public async Task LivePreviewRestartsWhilePreviousFocusFrameRetainsItsBuffer()
    {
        TimeSpan testWait = TimeSpan.FromSeconds(10);
        TaskCompletionSource enteredNative = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<double> nextScore = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim finishNative = new();
        CameraFocusFrameProcessor? first = null;
        CameraFocusFrameProcessor? second = null;
        int processorCount = 0;
        int oldSampleAfterStop = -1;
        List<double> scores = [];
        var focusPipeline = WpfTestHost.Invoke(() => new CameraRealtimeFramePipeline(createFocusProcessor: publish =>
        {
            if (Interlocked.Increment(ref processorCount) != 1)
                return second = new CameraFocusFrameProcessor(publish, (_, _) => new CameraFocusFrameResult(42));
            return first = new CameraFocusFrameProcessor(publish, (image, _) =>
            {
                enteredNative.SetResult();
                if (!finishNative.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Focus gate was not released.");
                oldSampleAfterStop = Marshal.ReadByte(image.pData);
                return new CameraFocusFrameResult(17);
            });
        }));

        void StartPreview() => focusPipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false, onArticulation: score =>
        {
            scores.Add(score);
            nextScore.TrySetResult(score);
        });

        try
        {
            WpfTestHost.Invoke(() =>
            {
                DefaultRealtimeCameraConfig.Current.IsCalArtculation = true;
                StartPreview();
                focusPipeline.SubmitFrame([17], 1, 1, 1, 1, 8, 1);
            });
            await enteredNative.Task.WaitAsync(testWait);

            await view.Dispatcher.InvokeAsync(() =>
            {
                focusPipeline.Stop(resetRealtime: true);
                StartPreview();
            }).Task.WaitAsync(testWait);

            byte[] restartedPixels = [71, 72, 73, 74, 75, 76];
            focusPipeline.SubmitFrame(restartedPixels, restartedPixels.Length, 3, 2, 1, 8, 3);
            await view.Realtime.Completion.WaitAsync(testWait);
            Assert.Equal(1, Volatile.Read(ref processorCount));
            var retiringProcessor = Assert.IsType<CameraFocusFrameProcessor>(first);
            Assert.False(retiringProcessor.Retirement.IsCompleted);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(1, focusPipeline.CurrentDisplayedFrameCount);
                Assert.Equal(restartedPixels, Pixels(Assert.IsAssignableFrom<BitmapSource>(view.ViewBitmapSource)));
                Assert.Empty(scores);
            });

            finishNative.Set();
            await retiringProcessor.Retirement.WaitAsync(testWait);
            Assert.Equal(17, oldSampleAfterStop);
            focusPipeline.SubmitFrame(restartedPixels, restartedPixels.Length, 3, 2, 1, 8, 3);
            Assert.Equal(42.0, await nextScore.Task.WaitAsync(testWait));
            Assert.Equal(2, Volatile.Read(ref processorCount));
            WpfTestHost.Invoke(() => Assert.Equal(new[] { 42.0 }, scores));
        }
        finally
        {
            finishNative.Set();
            WpfTestHost.Invoke(focusPipeline.Dispose);
            if (first != null) await first.Retirement.WaitAsync(testWait);
            if (second != null) await second.Retirement.WaitAsync(testWait);
        }
    }

    [Fact]
    public async Task LiveSessionsUseIndependentFocusRois()
    {
        byte[] samples = [10, 10, 0, 20, 10, 10, 0, 20];
        Rect left = new(0, 0, 2, 2);
        Rect right = new(2, 0, 2, 2);
        var (secondView, secondPipeline) = WpfTestHost.Invoke(() =>
            (new ImageView { EnableEditorImageServices = false }, new CameraRealtimeFramePipeline()));
        var firstScore = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondScore = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            WpfTestHost.Invoke(() =>
            {
                DefaultRealtimeCameraConfig.Current.EvaFunc = FocusAlgorithm.Variance;
                DefaultRealtimeCameraConfig.Current.IsCalArtculation = true;
                pipeline.Start(view, onArticulation: score => firstScore.TrySetResult(score), displayRoi: left);
                secondPipeline.Start(secondView, onArticulation: score => secondScore.TrySetResult(score));
                secondPipeline.SetPreviewGeometry(right, RealtimeFramePresenter.TransformNone);

                pipeline.SubmitFrame(samples, samples.Length, 4, 2, 1, 8, 4);
                secondPipeline.SubmitFrame(samples, samples.Length, 4, 2, 1, 8, 4);
            });

            Assert.Equal(0, await firstScore.Task.WaitAsync(TimeSpan.FromSeconds(10)), 6);
            Assert.Equal(100, await secondScore.Task.WaitAsync(TimeSpan.FromSeconds(10)), 6);
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                secondPipeline.Dispose();
                secondView.Dispose();
            });
        }
    }

    [Theory]
    [InlineData(RealtimeFramePresenter.TransformFlipX, 36)]
    [InlineData(RealtimeFramePresenter.TransformFlipY, 16)]
    [InlineData(RealtimeFramePresenter.TransformFlipXY, 64)]
    public async Task LiveFocusSamplesTheVisibleOffCenterRoiAfterFlip(int transform, int expectedVariance)
    {
        byte[] samples =
        [
            0, 4, 0, 8,
            0, 4, 0, 8,
            0, 12, 0, 16,
            0, 12, 0, 16
        ];
        var score = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        WpfTestHost.Invoke(() =>
        {
            DefaultRealtimeCameraConfig.Current.EvaFunc = FocusAlgorithm.Variance;
            DefaultRealtimeCameraConfig.Current.IsCalArtculation = true;
            pipeline.Start(view, transform, onArticulation: value => score.TrySetResult(value), displayRoi: new Rect(0, 0, 2, 2));
            GCHandle pinned = GCHandle.Alloc(samples, GCHandleType.Pinned);
            try { pipeline.SubmitFrame(pinned.AddrOfPinnedObject(), samples.Length, 4, 4, 1, 8, 4); }
            finally { pinned.Free(); }
        });

        Assert.Equal(expectedVariance, await score.Task.WaitAsync(TimeSpan.FromSeconds(10)), 6);
    }

    [Fact]
    public async Task DisplayedFrameCountSkipsRepaintsAndSurvivesViewReattachment()
    {
        WpfTestHost.Invoke(() => pipeline.Start(view));
        Assert.Equal(0, pipeline.CurrentDisplayedFrameCount);
        await PresentAsync([11, 12, 13, 14, 15, 16], 8, 1);
        WpfTestHost.Invoke(() =>
        {
            Assert.Equal(1, pipeline.CurrentDisplayedFrameCount);
            var stream = view.EditorContext.ProcessingContext.StreamPresentation;
            var source = view.Presentation.DisplaySource;
            stream.RefreshCurrent();
            Assert.Same(source, view.Presentation.DisplaySource);
            Assert.Equal(1, pipeline.CurrentDisplayedFrameCount);

            pipeline.Stop(resetRealtime: true);
            pipeline.Start(view, preserveDisplayedFrameCount: true);
            Assert.Equal(1, pipeline.CurrentDisplayedFrameCount);
        });
        await PresentAsync([71, 72, 73, 74, 75, 76], 8, 1);
        WpfTestHost.Invoke(() =>
        {
            Assert.Equal(2, pipeline.CurrentDisplayedFrameCount);
            pipeline.Start(view);
            Assert.Equal(0, pipeline.CurrentDisplayedFrameCount);
        });
    }

    [Fact]
    public async Task FrameRateNotificationUsesThePublishedOverlaySample()
    {
        var clock = new FrameRateTestClock();
        var framePipeline = WpfTestHost.Invoke(() => new CameraRealtimeFramePipeline(clock));
        List<(double Fps, double PublishedFps, bool OnDispatcher)> samples = [];
        TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            WpfTestHost.Invoke(() => framePipeline.Start(view, onFrameRateChanged: metrics =>
            {
                samples.Add((metrics.Fps, framePipeline.CurrentMetrics.Fps, view.Dispatcher.CheckAccess()));
                if (samples.Count == 2) completed.TrySetResult();
            }));
            await Task.Run(() =>
            {
                byte[] frame = [1, 2, 3, 4, 5, 6];
                framePipeline.SubmitFrame(frame, frame.Length, 3, 2, 1, 8, 3);
                clock.Timestamp += TimeSpan.TicksPerSecond;
                framePipeline.SubmitFrame(frame, frame.Length, 3, 2, 1, 8, 3);
                clock.Timestamp += TimeSpan.TicksPerSecond;
                framePipeline.SubmitFrame(frame, frame.Length, 3, 2, 1, 8, 3);
            });
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new[] { (2.0, 2.0, true), (1.0, 1.0, true) }, samples);
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                framePipeline.Stop(resetRealtime: true);
                framePipeline.Dispose();
            });
        }
    }

    private sealed class FrameRateTestClock : TimeProvider
    {
        public long Timestamp { get; set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Timestamp;
    }

    [Theory]
    [InlineData(8, 1, RealtimeFramePresenter.TransformNone)]
    [InlineData(16, 1, RealtimeFramePresenter.TransformFlipX)]
    [InlineData(8, 3, RealtimeFramePresenter.TransformFlipY)]
    [InlineData(16, 3, RealtimeFramePresenter.TransformFlipXY)]
    public async Task BackgroundVideoCopyPreservesPixelLayoutAndFlipWithPaddedBorrowedRows(int depth, int channels, int transform)
    {
        int pixelBytes = channels * (depth / 8);
        int rowBytes = 3 * pixelBytes;
        int stride = rowBytes + 5;
        byte[] samples = Enumerable.Range(0, stride * 2).Select(i => (byte)(i + 1)).ToArray();
        byte[] expected = new byte[rowBytes * 2];
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
            {
                int sourceY = (transform & RealtimeFramePresenter.TransformFlipX) != 0 ? 1 - y : y;
                int sourceX = (transform & RealtimeFramePresenter.TransformFlipY) != 0 ? 2 - x : x;
                Array.Copy(samples, sourceY * stride + sourceX * pixelBytes, expected, y * rowBytes + x * pixelBytes, pixelBytes);
            }
        IntPtr borrowed = Marshal.AllocCoTaskMem(samples.Length);
        try
        {
            Marshal.Copy(samples, 0, borrowed, samples.Length);
            WpfTestHost.Invoke(() =>
            {
                pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
                Assert.True(view.Realtime.SubmitFrame(borrowed, 3, 2, RealtimeFramePresenter.GetPixelFormat(channels, depth), stride, samples.Length, transform));
            });
        }
        finally { Marshal.FreeCoTaskMem(borrowed); }
        await view.Realtime.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        WpfTestHost.Invoke(() =>
        {
            var source = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
            Assert.True(source.IsFrozen);
            Assert.Equal(expected, Pixels(source));
            Assert.Equal(depth, view.Config.GetProperties<int>(ImageViewPropertyKeys.Depth));
            Assert.Equal(channels, view.Config.GetProperties<int>(ImageViewPropertyKeys.Channel));
        });
    }

    [Theory]
    [InlineData(1, 8, false)]
    [InlineData(1, 16, true)]
    [InlineData(3, 8, true)]
    [InlineData(3, 16, true)]
    public async Task LargeFrameDisplayCopyDrawsTheSamePixelsWithoutReplacingTheSource(int channels, int depth, bool expectsProxy)
    {
        PixelFormat format = RealtimeFramePresenter.GetPixelFormat(channels, depth);
        int stride = RealtimeFramePresenter.GetDefaultStride(3, format) + 5;
        byte[] samples = Enumerable.Range(0, stride * 2).Select(i => (byte)(i * 11 + 1)).ToArray();
        WpfTestHost.Invoke(() =>
        {
            pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
            Assert.True(view.Realtime.SubmitFrame(samples, 3, 2, format, stride, samples.Length, RealtimeFramePresenter.TransformFlipY));
        });
        await view.Realtime.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        WpfTestHost.Invoke(() =>
        {
            // Pixel consumers read Source, so the copy must never change its format or bit depth.
            var source = Assert.IsType<WriteableBitmap>(view.ViewBitmapSource);
            Assert.Same(source, view.Presentation.DisplaySource);
            Assert.Equal(format, source.Format);
            if (!expectsProxy)
            {
                Assert.Null(view.ImageShow.ActiveRenderProxy);
                return;
            }
            BitmapSource proxy = Assert.IsAssignableFrom<BitmapSource>(view.ImageShow.ActiveRenderProxy);
            Assert.True(proxy.IsFrozen);
            Assert.Equal(Pixels(new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0)), Pixels(proxy));

            // Another publication retires the copy even before the next render pass.
            WriteableBitmap other = new(3, 2, 96, 96, PixelFormats.Gray8, null);
            view.Presentation.Publish(other, null);
            Assert.Null(view.ImageShow.ActiveRenderProxy);
        });
    }

    [Fact]
    public async Task BusyUiKeepsOnlyLatestPendingVideoFrameAndDoesNotMutatePresentedSnapshots()
    {
        using ManualResetEventSlim prepared = new();
        List<WriteableBitmap> presented = [];
        void OnFrame(Guid id, WriteableBitmap source) => presented.Add(source);
        WpfTestHost.Invoke(() =>
        {
            pipeline.Start(view, showOverlayRoi: false, showOverlayMetrics: false);
            view.EditorContext.ProcessingContext.StreamPresentation.FramePresented += OnFrame;
            int uiThread = Environment.CurrentManagedThreadId;
            void OnPosted(object? sender, DispatcherHookEventArgs args)
            {
                if (args.Operation.Priority == DispatcherPriority.Background && Environment.CurrentManagedThreadId != uiThread)
                    prepared.Set();
            }
            view.Dispatcher.Hooks.OperationPosted += OnPosted;
            try
            {
                pipeline.SubmitFrame(new byte[] { 11 }, 1, 1, 1, 1, 8, 1);
                // Wait for background preparation while this UI invocation still owns the
                // dispatcher. Its completed frozen bitmap is waiting to be published.
                Assert.True(prepared.Wait(TimeSpan.FromSeconds(10)));
                pipeline.SubmitFrame(new byte[] { 22 }, 1, 1, 1, 1, 8, 1);
                pipeline.SubmitFrame(new byte[] { 33 }, 1, 1, 1, 1, 8, 1);
            }
            finally { view.Dispatcher.Hooks.OperationPosted -= OnPosted; }
        });
        try
        {
            await view.Realtime.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(2, presented.Count);
                Assert.Equal(2, pipeline.CurrentDisplayedFrameCount);
                Assert.Equal(new byte[] { 11 }, Pixels(presented[0]));
                Assert.Equal(new byte[] { 33 }, Pixels(presented[1]));
                Assert.All(presented, source => Assert.True(source.IsFrozen));
            });
        }
        finally { WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext.StreamPresentation.FramePresented -= OnFrame); }
    }

    private SemaphoreSlim OpenGate => (SemaphoreSlim)typeof(CalibratedImageCapability).GetField("_fileOpenGate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(opener)!;
    private PoiMeasurementBuffer? Measurement => (PoiMeasurementBuffer?)typeof(CalibratedImageCapability).GetField("_measurementBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(opener);
    private string WriteRaw(string name, byte value)
    {
        string path = Path.Combine(root, name);
        using CVCIEFile raw = new() { Version = 1, Cols = 3, Rows = 2, Bpp = 8, Channels = 1, Exp = [10], Data = Enumerable.Repeat(value, 6).ToArray() };
        Assert.True(CVFileUtil.WriteCVRaw(path, raw));
        return path;
    }
    private async Task PresentAsync(byte[] samples, int bpp, int channels, bool clearProducer = false)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFrame(Guid id, WriteableBitmap source) => completion.TrySetResult();
        WpfTestHost.Invoke(() =>
        {
            view.EditorContext.ProcessingContext.StreamPresentation.FramePresented += OnFrame;
            pipeline.SubmitFrame(samples, samples.Length, 3, 2, channels, bpp, 3 * channels * (bpp / 8));
            if (clearProducer) Array.Clear(samples);
        });
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { WpfTestHost.Invoke(() => view.EditorContext.ProcessingContext.StreamPresentation.FramePresented -= OnFrame); }
    }
    private async Task OpenAsync(string path)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args) => completion.TrySetResult();
        WpfTestHost.Invoke(() => { view.ImageSourceLoaded += OnLoaded; view.OpenImage(path); });
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { WpfTestHost.Invoke(() => view.ImageSourceLoaded -= OnLoaded); }
    }
    private static byte[] Pixels(BitmapSource source)
    {
        int stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
        byte[] pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return pixels;
    }
    public void Dispose()
    {
        WpfTestHost.Invoke(() => { pipeline.Dispose(); view.Dispose(); });
        ConfigService.SetInstance(previousConfig);
        CVFileReadCache.Release();
        foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
        Directory.Delete(root);
    }
}
