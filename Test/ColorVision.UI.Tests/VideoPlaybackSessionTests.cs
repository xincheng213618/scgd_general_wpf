using ColorVision.Core;
using ColorVision.ImageEditor.Video;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class VideoPlaybackSessionTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(10);

    [Fact]
    public void OpenDeliversBorrowedInitialFrameAndWaitsForExplicitPlayback()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new();
            FakeAudioTrack audio = new();
            int deliveries = 0;
            Guid sessionId = Guid.Empty;
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, frame =>
            {
                deliveries++;
                sessionId = frame.SessionId;
                Assert.True(frame.IsInitialFrame);
                Assert.True(frame.Image.isDispose);
                Assert.Equal(0, frame.FrameIndex);
                Assert.Equal(120, frame.TotalFrames);
                Assert.Equal((byte)7, Marshal.ReadByte(frame.Image.pData));
                Assert.Equal(0, backend.ReleasedFrameCount);
            }, backend, audio);

            Assert.True(session.Open("C:\\synthetic\\video.mp4"));

            Assert.Equal(1, deliveries);
            Assert.NotEqual(Guid.Empty, sessionId);
            Assert.Equal(sessionId, session.SessionId);
            Assert.Equal(1, backend.ReleasedFrameCount);
            Assert.Equal(0, backend.OutstandingFrameCount);
            Assert.Equal(new[] { 0 }, backend.Seeks);
            Assert.False(session.IsPlaying);
            Assert.Equal(0, backend.PlayCalls);
            Assert.Equal(0, audio.PlayCalls);
        });
    }

    [Fact]
    public void PlaybackControlsKeepNativeAndAudioStateTogetherWithoutRereadingPausedFrames()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new();
            FakeAudioTrack audio = new();
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, _ => { }, backend, audio);
            session.Open("C:\\synthetic\\video.mp4");

            session.Play();
            session.SetPlaybackSpeed(2);
            session.SetResizeScale(0.5);
            session.SetMuted(true);
            session.Seek(60);
            Assert.Equal(TimeSpan.FromSeconds(2), audio.Position);
            session.Pause();
            session.Stop();

            Assert.False(session.IsPlaying);
            Assert.Equal(1, backend.PlayCalls);
            Assert.Equal(1, audio.PlayCalls);
            Assert.Equal(2, backend.Speed);
            Assert.Equal(2, audio.Speed);
            Assert.Equal(0.5, backend.ResizeScale);
            Assert.True(audio.IsMuted);
            Assert.Equal(new[] { 0, 60, 0 }, backend.Seeks);
            Assert.Equal(TimeSpan.Zero, audio.Position);
            Assert.Equal(1, backend.ReadCalls);
        });
    }

    [Fact]
    public void ReopeningAReusedNativeHandleRejectsOldQueuedAndLateCallbacks()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new();
            List<int> rendered = [];
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, frame => rendered.Add(frame.FrameIndex), backend, new FakeAudioTrack());
            session.Open("C:\\synthetic\\first.mp4");
            Guid firstSessionId = session.SessionId;
            session.Play();
            OpenCVMediaHelper.VideoFrameCallback oldFrameCallback = backend.FrameCallback!;
            OpenCVMediaHelper.VideoStatusCallback oldStatusCallback = backend.StatusCallback!;
            backend.EmitFrame(5);
            oldStatusCallback(backend.Handle, 1, IntPtr.Zero);

            session.Close();
            Assert.Equal(0, backend.OutstandingFrameCount);
            session.Open("C:\\synthetic\\second.mp4");
            Assert.NotEqual(firstSessionId, session.SessionId);
            backend.EmitFrame(oldFrameCallback, 9);
            oldStatusCallback(backend.Handle, 1, IntPtr.Zero);
            PumpDispatcher();

            Assert.Equal(new[] { 0, 0 }, rendered);
            Assert.False(session.IsPlaying);
            Assert.Equal(4, backend.ReleasedFrameCount);
            Assert.Equal(0, backend.OutstandingFrameCount);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeekingRejectsPendingFramesAndStatusesButAcceptsNewFramesFromTheSamePlayer(bool callbackSeek)
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new() { SupportsCallbackSeek = callbackSeek };
            List<int> rendered = [];
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, frame => rendered.Add(frame.FrameIndex), backend, new FakeAudioTrack());
            session.Open("C:\\synthetic\\video.mp4");
            session.Play();
            OpenCVMediaHelper.VideoFrameCallback frameCallback = backend.FrameCallback!;
            OpenCVMediaHelper.VideoStatusCallback statusCallback = backend.StatusCallback!;
            IntPtr oldToken = backend.CallbackToken;
            Guid previousTimelineId = session.SessionId;
            backend.EmitFrame(5);
            backend.EmitStatus(2);

            session.Seek(60);
            Assert.NotEqual(previousTimelineId, session.SessionId);
            Assert.Equal(0, backend.OutstandingFrameCount);
            if (callbackSeek)
            {
                Assert.Same(frameCallback, backend.FrameCallback);
                Assert.Same(statusCallback, backend.StatusCallback);
                Assert.NotEqual(oldToken, backend.CallbackToken);
                backend.EmitFrame(frameCallback, 58, 58, oldToken);
                backend.EmitStatus(0, oldToken);
                backend.EmitStatus(1, oldToken);
                backend.EmitStatus(2, oldToken);
                Assert.Equal(0, backend.OutstandingFrameCount);
            }
            PumpDispatcher();
            Assert.True(session.IsPlaying);
            Assert.Equal(60, session.CurrentFrame);
            backend.EmitFrame(61);
            if (callbackSeek)
            {
                backend.EmitFrame(frameCallback, 59, 59, oldToken);
                backend.EmitStatus(2, oldToken);
                Assert.Equal(1, backend.OutstandingFrameCount);
            }
            PumpDispatcher();

            Assert.Equal(new[] { 0, 61 }, rendered);
            Assert.Equal(new[] { 0, 60 }, backend.Seeks);
            Assert.Equal(callbackSeek ? 5 : 3, backend.ReleasedFrameCount);
            Assert.Equal(0, session.DroppedFrameCount);
            Assert.Equal(0, backend.OutstandingFrameCount);
            if (callbackSeek)
            {
                IntPtr currentToken = backend.CallbackToken;
                backend.EmitFrame(62, 62);
                Assert.Equal(1, backend.OutstandingFrameCount);
                session.Close();
                session.Close();
                Assert.Equal(0, backend.OutstandingFrameCount);
                backend.EmitFrame(frameCallback, 63, 63, currentToken);
                statusCallback(backend.Handle, 2, currentToken);
                PumpDispatcher();
                Assert.Equal(new[] { 0, 61 }, rendered);
                Assert.Equal(new[] { 0, 60 }, backend.Seeks);
                Assert.Equal(7, backend.ReleasedFrameCount);
                Assert.Equal(0, backend.OutstandingFrameCount);
                Assert.Equal(1, backend.CloseCalls);
            }
        });
    }

    [Fact]
    public void BusyUiDropsIncomingFramesAndCloseReleasesQueuedPixelsImmediately()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new();
            int deliveries = 0;
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, _ => deliveries++, backend, new FakeAudioTrack());
            session.Open("C:\\synthetic\\video.mp4");
            session.Play();
            backend.EmitFrame(1);
            backend.EmitFrame(2);

            Assert.Equal(1, session.DroppedFrameCount);
            Assert.Equal(1, backend.OutstandingFrameCount);
            session.Close();
            session.Close();
            Assert.Equal(0, backend.OutstandingFrameCount);
            PumpDispatcher();

            Assert.Equal(1, deliveries);
            Assert.Equal(3, backend.ReleasedFrameCount);
            Assert.Equal(1, backend.CloseCalls);
        });
    }

    [Fact]
    public void FrameConsumerFailureReleasesPixelsAndDoesNotBlockNextDelivery()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new();
            List<int> frames = [];
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, frame =>
            {
                if (frame.FrameIndex == 1) throw new InvalidOperationException("synthetic consumer failure");
                frames.Add(frame.FrameIndex);
            }, backend, new FakeAudioTrack());
            session.Open("C:\\synthetic\\video.mp4");
            session.Play();
            backend.EmitFrame(1);
            PumpDispatcher();
            backend.EmitFrame(2);
            PumpDispatcher();

            Assert.Equal(new[] { 0, 2 }, frames);
            Assert.Equal(3, backend.ReleasedFrameCount);
            Assert.Equal(0, backend.OutstandingFrameCount);
        });
    }

    [Fact]
    public void ClosingDuringDeliveryKeepsTheBorrowedPixelsAliveUntilCallbackReturns()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new();
            VideoPlaybackSession? session = null;
            bool pixelsAliveAfterClose = false;
            byte pixelAfterClose = 0;
            session = new VideoPlaybackSession(Dispatcher.CurrentDispatcher, frame =>
            {
                if (frame.IsInitialFrame) return;
                session!.Close();
                pixelsAliveAfterClose = backend.OutstandingFrameCount == 1;
                if (pixelsAliveAfterClose) pixelAfterClose = Marshal.ReadByte(frame.Image.pData);
            }, backend, new FakeAudioTrack());
            using (session)
            {
                session.Open("C:\\synthetic\\video.mp4");
                session.Play();
                backend.EmitFrame(1, 33);
                PumpDispatcher();

                Assert.Equal(2, backend.ReleasedFrameCount);
                Assert.Equal(0, backend.OutstandingFrameCount);
                Assert.False(session.IsOpen);
                Assert.True(pixelsAliveAfterClose);
                Assert.Equal((byte)33, pixelAfterClose);
            }
        });
    }

    [Fact]
    public void NativePlayFailureLeavesBothPlaybackAndAudioPaused()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new() { PlayResult = 5 };
            FakeAudioTrack audio = new();
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, _ => { }, backend, audio);
            session.Open("C:\\synthetic\\video.mp4");
            session.Play();

            Assert.False(session.IsPlaying);
            Assert.Equal(0, audio.PlayCalls);
        });
    }

    [Fact]
    public void EndStatusPausesAudioAndSeeksToStartWithoutPublishingAnotherFrame()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new();
            FakeAudioTrack audio = new();
            int deliveries = 0;
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, _ => deliveries++, backend, audio);
            session.Open("C:\\synthetic\\video.mp4");
            session.Play();
            session.Seek(90);
            backend.StatusCallback!(backend.Handle, 2, IntPtr.Zero);
            PumpDispatcher();

            Assert.False(session.IsPlaying);
            Assert.Equal(0, session.CurrentFrame);
            Assert.Equal(TimeSpan.Zero, audio.Position);
            Assert.Equal(1, deliveries);
            Assert.Equal(1, backend.ReadCalls);
        });
    }

    [Fact]
    public async Task BackgroundFramesArePreparedOffUiAndDeliverIndependentFrozenPixelsOnUi()
    {
        using FakeVideoBackend backend = new();
        TaskCompletionSource<bool> delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteableBitmap? prepared = null;
        WriteableBitmap? received = null;
        int preparationCalls = 0, preparationThread = 0, consumerThread = 0, borrowedPixel = 0;
        bool imageWasBorrowed = false;
        int uiThread = WpfTestHost.Invoke(() => Environment.CurrentManagedThreadId);
        VideoPlaybackSession session = WpfTestHost.Invoke(() => new VideoPlaybackSession(Dispatcher.CurrentDispatcher,
            frame =>
            {
                if (frame.IsInitialFrame) return;
                consumerThread = Environment.CurrentManagedThreadId;
                imageWasBorrowed = frame.Image.isDispose;
                borrowedPixel = Marshal.ReadByte(frame.Image.pData);
                received = frame.Source;
                delivered.TrySetResult(true);
            }, backend, new FakeAudioTrack(), image =>
            {
                WriteableBitmap source = VideoPlaybackSession.PrepareFrame(image);
                if (Interlocked.Increment(ref preparationCalls) != 1)
                {
                    preparationThread = Environment.CurrentManagedThreadId;
                    prepared = source;
                }
                return source;
            }));
        try
        {
            WpfTestHost.Invoke(() => { Assert.True(session.Open("C:\\synthetic\\video.mp4")); session.Play(); });
            await Task.Run(() => backend.EmitFrame(1, 33)).WaitAsync(Watchdog);
            await delivered.Task.WaitAsync(Watchdog);
            // A dispatcher turn after delivery observes its finally/release, rather than racing the consumer callback.
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(2, backend.ReleasedFrameCount);
                Assert.Equal(0, backend.OutstandingFrameCount);
            });
            Assert.Equal(2, preparationCalls);
            Assert.NotEqual(uiThread, preparationThread);
            Assert.Equal(uiThread, consumerThread);
            Assert.True(imageWasBorrowed);
            Assert.Equal(33, borrowedPixel);
            Assert.NotNull(received);
            Assert.Same(prepared, received);
            Assert.True(received.IsFrozen);
            byte[] pixels = new byte[6];
            received.CopyPixels(pixels, 6, 0);
            Assert.Equal(Enumerable.Repeat((byte)33, 6), pixels);
        }
        finally { WpfTestHost.Invoke(session.Dispose); }
    }

    [Fact]
    public async Task SeekingDuringPreparationKeepsOwnedPixelsAliveAndRejectsTheRetiredFrame()
    {
        using FakeVideoBackend backend = new();
        using ManualResetEventSlim preparationStarted = new();
        using ManualResetEventSlim finishPreparation = new();
        TaskCompletionSource<bool> nextDelivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<int> rendered = [];
        int preparationCalls = 0, pixelAfterSeek = 0;
        bool pixelsAliveAfterSeek = false;
        Task? preparing = null;
        VideoPlaybackSession session = WpfTestHost.Invoke(() => new VideoPlaybackSession(Dispatcher.CurrentDispatcher,
            frame =>
            {
                rendered.Add(frame.FrameIndex);
                if (frame.FrameIndex == 61) nextDelivered.TrySetResult(true);
            }, backend, new FakeAudioTrack(), image =>
            {
                if (Interlocked.Increment(ref preparationCalls) == 2)
                {
                    preparationStarted.Set();
                    finishPreparation.Wait();
                    pixelsAliveAfterSeek = backend.OwnsFrame(image.pData);
                    // Never dereference a pointer after observing a premature release in a failing implementation.
                    if (!pixelsAliveAfterSeek) throw new InvalidOperationException("Preparation lost its native frame owner during Seek.");
                    pixelAfterSeek = Marshal.ReadByte(image.pData);
                }
                return VideoPlaybackSession.PrepareFrame(image);
            }));
        try
        {
            WpfTestHost.Invoke(() => { Assert.True(session.Open("C:\\synthetic\\video.mp4")); session.Play(); });
            Guid previousTimeline = WpfTestHost.Invoke(() => session.SessionId);
            preparing = Task.Run(() => backend.EmitFrame(5, 33));
            Assert.True(await Task.Run(() => preparationStarted.Wait(Watchdog)), "Watchdog: preparation did not start.");
            WpfTestHost.Invoke(() =>
            {
                session.Seek(60);
                Assert.NotEqual(previousTimeline, session.SessionId);
                Assert.Equal(1, backend.ReleasedFrameCount);
                Assert.Equal(1, backend.OutstandingFrameCount);
                Assert.Equal(new[] { 0 }, rendered);
            });
            finishPreparation.Set();
            await preparing.WaitAsync(Watchdog);
            WpfTestHost.Invoke(() =>
            {
                PumpDispatcher();
                Assert.Equal(new[] { 0 }, rendered);
                Assert.Equal(60, session.CurrentFrame);
                Assert.Equal(2, backend.ReleasedFrameCount);
                Assert.Equal(0, backend.OutstandingFrameCount);
            });
            Assert.True(pixelsAliveAfterSeek);
            Assert.Equal(33, pixelAfterSeek);
            await Task.Run(() => backend.EmitFrame(61, 44)).WaitAsync(Watchdog);
            await nextDelivered.Task.WaitAsync(Watchdog);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(new[] { 0, 61 }, rendered);
                Assert.Equal(61, session.CurrentFrame);
                Assert.Equal(3, backend.ReleasedFrameCount);
                Assert.Equal(0, backend.OutstandingFrameCount);
                Assert.Equal(new[] { 0, 60 }, backend.Seeks);
            });
        }
        finally
        {
            finishPreparation.Set();
            try { if (preparing != null) await preparing.WaitAsync(Watchdog); }
            finally { WpfTestHost.Invoke(session.Dispose); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeekingAcceptsTheTargetFrameBeforeTheBackendCallReturnsAndRetiresOldPendingPixels(bool beforeFirstPlay)
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new() { SupportsCallbackSeek = beforeFirstPlay };
            FakeAudioTrack audio = new();
            List<int> rendered = [];
            WriteableBitmap? targetSource = null;
            Guid deliveredSession = Guid.Empty;
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, frame =>
            {
                rendered.Add(frame.FrameIndex);
                if (frame.FrameIndex == 73)
                {
                    targetSource = frame.Source;
                    deliveredSession = frame.SessionId;
                }
            }, backend, audio);
            Assert.True(session.Open("C:\\synthetic\\video.mp4"));
            if (!beforeFirstPlay)
            {
                session.Play();
                backend.EmitFrame(5, 5);
                session.Pause();
            }
            Guid previousSession = session.SessionId;
            Assert.Equal(beforeFirstPlay ? 0 : 1, backend.OutstandingFrameCount);
            backend.DuringSeek = frameIndex =>
            {
                if (frameIndex is 73 or 74) backend.EmitFrame(frameIndex, (byte)frameIndex);
            };

            session.Seek(73);
            PumpDispatcher();

            Assert.Equal(new[] { 0, 73 }, rendered);
            Assert.NotEqual(previousSession, session.SessionId);
            Assert.Equal(session.SessionId, deliveredSession);
            Assert.Equal(73, session.CurrentFrame);
            Assert.False(session.IsPlaying);
            Assert.Equal(new[] { 0, 73 }, backend.Seeks);
            Assert.Equal(beforeFirstPlay ? 2 : 3, backend.ReleasedFrameCount);
            Assert.Equal(0, backend.OutstandingFrameCount);
            Assert.NotNull(targetSource);
            Assert.True(targetSource.IsFrozen);
            byte[] pixels = new byte[6];
            targetSource.CopyPixels(pixels, 6, 0);
            Assert.Equal(Enumerable.Repeat((byte)73, 6), pixels);
            if (beforeFirstPlay)
            {
                Guid firstSeekSession = session.SessionId;
                session.Seek(74);
                backend.EmitStatus(1, backend.CallbackRegistrations[0].Token);
                PumpDispatcher();
                Assert.Equal(new[] { 0, 73, 74 }, rendered);
                Assert.Equal(74, session.CurrentFrame);
                Assert.NotEqual(firstSeekSession, session.SessionId);
                Assert.Equal(new[] { 0, 73, 74 }, backend.Seeks);
                Assert.Equal(0, backend.PlayCalls);
                Assert.Equal(0, audio.PlayCalls);
                Assert.False(session.IsPlaying);
                Assert.Equal(2, backend.CallbackRegistrations.Count);
                Assert.Same(backend.CallbackRegistrations[0].Frame, backend.CallbackRegistrations[1].Frame);
                Assert.Same(backend.CallbackRegistrations[0].Status, backend.CallbackRegistrations[1].Status);
                Assert.NotEqual(backend.CallbackRegistrations[0].Token, backend.CallbackRegistrations[1].Token);
                session.Close();
                Assert.Equal(3, backend.ReleasedFrameCount);
                Assert.Equal(0, backend.OutstandingFrameCount);
                targetSource.CopyPixels(pixels, 6, 0);
                Assert.Equal(Enumerable.Repeat((byte)73, 6), pixels);
            }
        });
    }

    [Fact]
    public void InvalidSeekKeepsThePlayingTimelineAndPendingFrameIntact()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new() { SupportsCallbackSeek = true };
            FakeAudioTrack audio = new();
            List<int> rendered = [];
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, frame => rendered.Add(frame.FrameIndex), backend, audio);
            Assert.True(session.Open("C:\\synthetic\\video.mp4"));
            session.Play();
            backend.EmitFrame(61, 61);
            Guid timeline = session.SessionId;
            OpenCVMediaHelper.VideoFrameCallback frameCallback = backend.FrameCallback!;
            OpenCVMediaHelper.VideoStatusCallback statusCallback = backend.StatusCallback!;
            IntPtr token = backend.CallbackToken;
            audio.Position = TimeSpan.FromMilliseconds(250);

            session.Seek(-1);
            session.Seek(session.Info.totalFrames);

            Assert.True(session.IsOpen);
            Assert.True(session.IsPlaying);
            Assert.Equal(timeline, session.SessionId);
            Assert.Equal(0, session.CurrentFrame);
            Assert.Equal(1, backend.OutstandingFrameCount);
            Assert.Equal(1, backend.ReleasedFrameCount);
            Assert.Equal(0, backend.TrySeekCalls);
            Assert.Single(backend.CallbackRegistrations);
            Assert.Same(frameCallback, backend.FrameCallback);
            Assert.Same(statusCallback, backend.StatusCallback);
            Assert.Equal(token, backend.CallbackToken);
            Assert.Equal(new[] { 0 }, backend.Seeks);
            Assert.Equal(TimeSpan.FromMilliseconds(250), audio.Position);
            Assert.Equal(1, backend.PlayCalls);
            Assert.Equal(1, audio.PlayCalls);
            PumpDispatcher();
            backend.EmitFrame(62, 62);
            PumpDispatcher();
            Assert.Equal(new[] { 0, 61, 62 }, rendered);
            Assert.Equal(62, session.CurrentFrame);
            Assert.Equal(3, backend.ReleasedFrameCount);
            Assert.Equal(0, backend.OutstandingFrameCount);
        });
    }

    [Fact]
    public void RejectedEnhancedSeekClosesWithoutFallingBackOrMovingAudio()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new() { SupportsCallbackSeek = true, TrySeekResult = 5 };
            FakeAudioTrack audio = new();
            List<int> rendered = [];
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, frame => rendered.Add(frame.FrameIndex), backend, audio);
            Assert.True(session.Open("C:\\synthetic\\video.mp4"));
            session.Play();
            backend.EmitFrame(61, 61);
            OpenCVMediaHelper.VideoFrameCallback frameCallback = backend.FrameCallback!;
            OpenCVMediaHelper.VideoStatusCallback statusCallback = backend.StatusCallback!;
            IntPtr token = backend.CallbackToken;
            audio.Position = TimeSpan.FromMilliseconds(250);
            int audioCloses = audio.CloseCalls;

            session.Seek(73);
            backend.EmitFrame(frameCallback, 63, 63, token);
            statusCallback(backend.Handle, 2, token);
            PumpDispatcher();

            Assert.False(session.IsOpen);
            Assert.False(session.IsPlaying);
            Assert.Equal(Guid.Empty, session.SessionId);
            Assert.Equal(0, session.CurrentFrame);
            Assert.Equal(new[] { 0 }, rendered);
            Assert.Equal(new[] { 0 }, backend.Seeks);
            Assert.Equal(1, backend.TrySeekCalls);
            Assert.Single(backend.CallbackRegistrations);
            Assert.Equal(1, backend.CloseCalls);
            Assert.Equal(3, backend.ReleasedFrameCount);
            Assert.Equal(0, backend.OutstandingFrameCount);
            Assert.Equal(TimeSpan.FromMilliseconds(250), audio.Position);
            Assert.Equal(1, audio.PlayCalls);
            Assert.Equal(audioCloses + 1, audio.CloseCalls);
        });
    }

    private static void PumpDispatcher()
    {
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class FakeVideoBackend : IVideoPlaybackBackend, IDisposable
    {
        private readonly HashSet<IntPtr> _frames = [];
        private readonly object _frameGate = new();
        private int _releasedFrameCount;
        public int Handle => 41;
        public int ReadCalls { get; private set; }
        public int PlayCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public int PlayResult { get; init; }
        public bool SupportsCallbackSeek { get; init; }
        public int TrySeekResult { get; init; }
        public int TrySeekCalls { get; private set; }
        public int ReleasedFrameCount { get { lock (_frameGate) return _releasedFrameCount; } }
        public int OutstandingFrameCount { get { lock (_frameGate) return _frames.Count; } }
        public bool OwnsFrame(IntPtr pointer) { lock (_frameGate) return _frames.Contains(pointer); }
        public double Speed { get; private set; } = 1;
        public double ResizeScale { get; private set; } = 1;
        public List<int> Seeks { get; } = [];
        public OpenCVMediaHelper.VideoFrameCallback? FrameCallback { get; private set; }
        public OpenCVMediaHelper.VideoStatusCallback? StatusCallback { get; private set; }
        public IntPtr CallbackToken { get; private set; }
        public List<(OpenCVMediaHelper.VideoFrameCallback Frame, OpenCVMediaHelper.VideoStatusCallback Status, IntPtr Token)> CallbackRegistrations { get; } = [];
        public Action<int>? DuringSeek { get; set; }

        public int Open(string filePath, out OpenCVMediaHelper.VideoInfo info)
        {
            info = new OpenCVMediaHelper.VideoInfo { width = 2, height = 1, fps = 30, totalFrames = 120 };
            return Handle;
        }

        public int ReadFrame(int handle, out HImage image)
        {
            ReadCalls++;
            image = CreateFrame(7);
            return 0;
        }

        public void ReleaseFrame(HImage image)
        {
            if (image.pData == IntPtr.Zero) return;
            lock (_frameGate)
            {
                Assert.True(_frames.Remove(image.pData), "Frame ownership must be released exactly once.");
                _releasedFrameCount++;
            }
            image.Dispose();
        }

        public int Play(int handle, OpenCVMediaHelper.VideoFrameCallback frameCallback, OpenCVMediaHelper.VideoStatusCallback statusCallback, IntPtr token)
        {
            PlayCalls++;
            FrameCallback = frameCallback;
            StatusCallback = statusCallback;
            CallbackToken = token;
            CallbackRegistrations.Add((frameCallback, statusCallback, token));
            return PlayResult;
        }

        public bool TrySeekWithCallbacks(int handle, int frameIndex, OpenCVMediaHelper.VideoFrameCallback frameCallback,
            OpenCVMediaHelper.VideoStatusCallback statusCallback, IntPtr token, out int result)
        {
            result = 0;
            TrySeekCalls++;
            if (!SupportsCallbackSeek) return false;
            result = TrySeekResult;
            if (result != 0) return true;
            FrameCallback = frameCallback;
            StatusCallback = statusCallback;
            CallbackToken = token;
            CallbackRegistrations.Add((frameCallback, statusCallback, token));
            result = Seek(handle, frameIndex);
            return true;
        }

        public void EmitFrame(int frameIndex, byte value = 11) => EmitFrame(FrameCallback!, frameIndex, value, CallbackToken);

        public void EmitFrame(OpenCVMediaHelper.VideoFrameCallback callback, int frameIndex, byte value = 11)
            => EmitFrame(callback, frameIndex, value, CallbackToken);

        public void EmitFrame(OpenCVMediaHelper.VideoFrameCallback callback, int frameIndex, byte value, IntPtr token)
        {
            HImage frame = CreateFrame(value);
            callback(Handle, ref frame, frameIndex, 120, token);
            Assert.Equal(IntPtr.Zero, frame.pData);
        }

        public void EmitStatus(int status) => EmitStatus(status, CallbackToken);
        public void EmitStatus(int status, IntPtr token) => StatusCallback!(Handle, status, token);

        public int Pause(int handle) => 0;
        public int Seek(int handle, int frameIndex) { Seeks.Add(frameIndex); DuringSeek?.Invoke(frameIndex); return 0; }
        public int SetPlaybackSpeed(int handle, double speed) { Speed = speed; return 0; }
        public int SetResizeScale(int handle, double scale) { ResizeScale = scale; return 0; }
        public int Close(int handle) { CloseCalls++; return 0; }

        private HImage CreateFrame(byte value)
        {
            IntPtr pointer = Marshal.AllocCoTaskMem(6);
            Marshal.Copy(Enumerable.Repeat(value, 6).ToArray(), 0, pointer, 6);
            lock (_frameGate) _frames.Add(pointer);
            return new HImage { cols = 2, rows = 1, channels = 3, depth = 8, stride = 6, pData = pointer };
        }

        public void Dispose()
        {
            lock (_frameGate)
            {
                foreach (IntPtr pointer in _frames) Marshal.FreeCoTaskMem(pointer);
                _frames.Clear();
            }
        }
    }

    private sealed class FakeAudioTrack : IVideoAudioTrack
    {
        public TimeSpan Position { get; set; }
        public double Speed { get; set; } = 1;
        public bool IsMuted { get; set; }
        public int PlayCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public void Open(string filePath) { }
        public void Play() => PlayCalls++;
        public void Pause() { }
        public void Close() => CloseCalls++;
    }
}
