using ColorVision.Core;
using ColorVision.ImageEditor.Video;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class VideoPlaybackSessionTests
{
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

    [Fact]
    public void SeekingRejectsPendingFramesAndStatusesButAcceptsNewFramesFromTheSamePlayer()
    {
        WpfTestHost.Invoke(() =>
        {
            using FakeVideoBackend backend = new();
            List<int> rendered = [];
            using VideoPlaybackSession session = new(Dispatcher.CurrentDispatcher, frame => rendered.Add(frame.FrameIndex), backend, new FakeAudioTrack());
            session.Open("C:\\synthetic\\video.mp4");
            session.Play();
            Guid previousTimelineId = session.SessionId;
            backend.EmitFrame(5);
            backend.StatusCallback!(backend.Handle, 2, IntPtr.Zero);

            session.Seek(60);
            Assert.NotEqual(previousTimelineId, session.SessionId);
            Assert.Equal(0, backend.OutstandingFrameCount);
            PumpDispatcher();
            Assert.True(session.IsPlaying);
            Assert.Equal(60, session.CurrentFrame);
            backend.EmitFrame(61);
            PumpDispatcher();

            Assert.Equal(new[] { 0, 61 }, rendered);
            Assert.Equal(new[] { 0, 60 }, backend.Seeks);
            Assert.Equal(3, backend.ReleasedFrameCount);
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

    private static void PumpDispatcher()
    {
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class FakeVideoBackend : IVideoPlaybackBackend, IDisposable
    {
        private readonly HashSet<IntPtr> _frames = [];
        public int Handle => 41;
        public int ReadCalls { get; private set; }
        public int PlayCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public int PlayResult { get; init; }
        public int ReleasedFrameCount { get; private set; }
        public int OutstandingFrameCount => _frames.Count;
        public double Speed { get; private set; } = 1;
        public double ResizeScale { get; private set; } = 1;
        public List<int> Seeks { get; } = [];
        public OpenCVMediaHelper.VideoFrameCallback? FrameCallback { get; private set; }
        public OpenCVMediaHelper.VideoStatusCallback? StatusCallback { get; private set; }

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
            Assert.True(_frames.Remove(image.pData), "Frame ownership must be released exactly once.");
            ReleasedFrameCount++;
            image.Dispose();
        }

        public int Play(int handle, OpenCVMediaHelper.VideoFrameCallback frameCallback, OpenCVMediaHelper.VideoStatusCallback statusCallback)
        {
            PlayCalls++;
            FrameCallback = frameCallback;
            StatusCallback = statusCallback;
            return PlayResult;
        }

        public void EmitFrame(int frameIndex, byte value = 11) => EmitFrame(FrameCallback!, frameIndex, value);

        public void EmitFrame(OpenCVMediaHelper.VideoFrameCallback callback, int frameIndex, byte value = 11)
        {
            HImage frame = CreateFrame(value);
            callback(Handle, ref frame, frameIndex, 120, IntPtr.Zero);
            Assert.Equal(IntPtr.Zero, frame.pData);
        }

        public int Pause(int handle) => 0;
        public int Seek(int handle, int frameIndex) { Seeks.Add(frameIndex); return 0; }
        public int SetPlaybackSpeed(int handle, double speed) { Speed = speed; return 0; }
        public int SetResizeScale(int handle, double scale) { ResizeScale = scale; return 0; }
        public int Close(int handle) { CloseCalls++; return 0; }

        private HImage CreateFrame(byte value)
        {
            IntPtr pointer = Marshal.AllocCoTaskMem(6);
            Marshal.Copy(Enumerable.Repeat(value, 6).ToArray(), 0, pointer, 6);
            _frames.Add(pointer);
            return new HImage { cols = 2, rows = 1, channels = 3, depth = 8, stride = 6, pData = pointer };
        }

        public void Dispose()
        {
            foreach (IntPtr pointer in _frames) Marshal.FreeCoTaskMem(pointer);
            _frames.Clear();
        }
    }

    private sealed class FakeAudioTrack : IVideoAudioTrack
    {
        public TimeSpan Position { get; set; }
        public double Speed { get; set; } = 1;
        public bool IsMuted { get; set; }
        public int PlayCalls { get; private set; }
        public void Open(string filePath) { }
        public void Play() => PlayCalls++;
        public void Pause() { }
        public void Close() { }
    }
}
