using ColorVision.Core;
using log4net;
using System;
using System.Threading;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Video
{
    // Image is borrowed for the duration of the synchronous delivery callback.
    // Consumers that retain pixels must copy them or acquire their own frame owner.
    internal readonly record struct VideoFrameDelivery(HImage Image, Guid SessionId, int FrameIndex, int TotalFrames, bool IsInitialFrame);

    internal sealed class VideoPlaybackSession : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VideoPlaybackSession));
        private const double AudioSyncDriftThresholdSeconds = 0.5;
        private readonly Dispatcher _dispatcher;
        private readonly Action<VideoFrameDelivery> _presentFrame;
        private readonly IVideoPlaybackBackend _backend;
        private readonly IVideoAudioTrack _audio;
        private int _handle = -1;
        private long _generation;
        private long _timeline;
        private PendingFrame? _pendingFrame;
        private OpenCVMediaHelper.VideoFrameCallback? _frameCallback;
        private OpenCVMediaHelper.VideoStatusCallback? _statusCallback;
        private int _uiUpdateCounter;
        private int _droppedFrameCount;
        private bool _disposed;

        public VideoPlaybackSession(Dispatcher dispatcher, Action<VideoFrameDelivery> presentFrame, IVideoPlaybackBackend? backend = null, IVideoAudioTrack? audio = null)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _presentFrame = presentFrame ?? throw new ArgumentNullException(nameof(presentFrame));
            _backend = backend ?? new NativeVideoPlaybackBackend();
            _audio = audio ?? new WpfVideoAudioTrack();
        }

        public event EventHandler? StateChanged;
        public OpenCVMediaHelper.VideoInfo Info { get; private set; }
        public Guid SessionId { get; private set; }
        public bool IsOpen => Volatile.Read(ref _handle) > 0;
        public bool IsPlaying { get; private set; }
        public int CurrentFrame { get; private set; }
        public double PlaybackSpeed { get; private set; } = 1;
        public double ResizeScale { get; private set; } = 1;
        public bool IsMuted { get; private set; }
        public int DroppedFrameCount => Volatile.Read(ref _droppedFrameCount);

        public bool Open(string filePath)
        {
            VerifyUsable();
            Close();
            int handle = _backend.Open(filePath, out OpenCVMediaHelper.VideoInfo info);
            if (handle <= 0) return false;

            _handle = handle;
            Info = info;
            SessionId = Guid.NewGuid();
            CurrentFrame = 0;
            PlaybackSpeed = 1;
            ResizeScale = 1;
            _uiUpdateCounter = 0;
            Interlocked.Exchange(ref _droppedFrameCount, 0);
            long generation = Volatile.Read(ref _generation);

            try
            {
                HImage firstFrame = default;
                try
                {
                    if (_backend.ReadFrame(handle, out firstFrame) == 0)
                        Deliver(firstFrame, 0, info.totalFrames, isInitialFrame: true);
                }
                finally
                {
                    _backend.ReleaseFrame(firstFrame);
                }

                if (!IsCurrent(handle, generation)) return false;
                _backend.Seek(handle, 0);
                TryAudio(() =>
                {
                    _audio.Open(filePath);
                    _audio.Speed = PlaybackSpeed;
                    _audio.IsMuted = IsMuted;
                });
                OnStateChanged();
                return true;
            }
            catch
            {
                Close();
                throw;
            }
        }

        public void Play()
        {
            VerifyUsable();
            if (!IsOpen || IsPlaying) return;
            int handle = _handle;
            long generation = Volatile.Read(ref _generation);
            Interlocked.Exchange(ref _droppedFrameCount, 0);
            _frameCallback = (int callbackHandle, ref HImage frame, int currentFrame, int totalFrames, IntPtr _) =>
                OnFrameReceived(callbackHandle, ref frame, currentFrame, totalFrames, generation);
            _statusCallback = (callbackHandle, status, _) => OnStatusChanged(callbackHandle, status, generation);
            int result = _backend.Play(handle, _frameCallback, _statusCallback);
            if (result != 0)
            {
                _frameCallback = null;
                _statusCallback = null;
                log.Warn($"Failed to start video playback. Native return code: {result}");
                OnStateChanged();
                return;
            }

            IsPlaying = true;
            TryAudio(_audio.Play);
            OnStateChanged();
        }

        public void Pause()
        {
            VerifyUsable();
            if (!IsOpen || !IsPlaying) return;
            _backend.Pause(_handle);
            IsPlaying = false;
            TryAudio(_audio.Pause);
            OnStateChanged();
        }

        public void Stop()
        {
            VerifyUsable();
            if (!IsOpen) return;
            Pause();
            Seek(0);
        }

        public void Seek(int frameIndex)
        {
            VerifyUsable();
            if (!IsOpen) return;
            _backend.Seek(_handle, frameIndex);
            SessionId = Guid.NewGuid();
            Interlocked.Increment(ref _timeline);
            Interlocked.Exchange(ref _pendingFrame, null)?.Dispose();
            CurrentFrame = frameIndex;
            TryAudio(() => _audio.Position = GetPosition(frameIndex));
            OnStateChanged();
        }

        public void SetPlaybackSpeed(double speed)
        {
            VerifyUsable();
            if (!IsOpen) return;
            _backend.SetPlaybackSpeed(_handle, speed);
            PlaybackSpeed = speed;
            TryAudio(() => _audio.Speed = speed);
            OnStateChanged();
        }

        public void SetResizeScale(double scale)
        {
            VerifyUsable();
            if (!IsOpen) return;
            _backend.SetResizeScale(_handle, scale);
            ResizeScale = scale;
            OnStateChanged();
        }

        public void SetMuted(bool isMuted)
        {
            VerifyUsable();
            IsMuted = isMuted;
            TryAudio(() => _audio.IsMuted = isMuted);
            OnStateChanged();
        }

        private void OnFrameReceived(int handle, ref HImage image, int currentFrame, int totalFrames, long generation)
        {
            if (!IsCurrent(handle, generation))
            {
                HImage staleImage = image;
                image = default;
                _backend.ReleaseFrame(staleImage);
                return;
            }

            PendingFrame frame = new(image, currentFrame, totalFrames, Volatile.Read(ref _timeline), _backend.ReleaseFrame);
            image = default;
            if (Interlocked.CompareExchange(ref _pendingFrame, frame, null) != null)
            {
                Interlocked.Increment(ref _droppedFrameCount);
                frame.Dispose();
                return;
            }

            if (!IsCurrent(handle, generation) || _dispatcher.HasShutdownStarted)
            {
                ReleasePending(frame);
                return;
            }

            try
            {
                DispatcherOperation operation = _dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!IsCurrent(handle, generation) || frame.Timeline != Volatile.Read(ref _timeline) || !frame.TryBeginDelivery()) return;
                        CurrentFrame = frame.Index;
                        try
                        {
                            Deliver(frame.Image, frame.Index, frame.TotalFrames, isInitialFrame: false);
                        }
                        finally
                        {
                            frame.EndDelivery();
                        }
                        if (!IsCurrent(handle, generation) || frame.Timeline != Volatile.Read(ref _timeline)) return;
                        if (++_uiUpdateCounter % 10 == 0)
                        {
                            CorrectAudioSync(frame.Index);
                            OnStateChanged();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("Error while delivering video frame", ex);
                    }
                    finally
                    {
                        ReleasePending(frame);
                    }
                }));
                operation.Aborted += (_, _) => ReleasePending(frame);
                if (operation.Status == DispatcherOperationStatus.Aborted) ReleasePending(frame);
            }
            catch (Exception ex)
            {
                ReleasePending(frame);
                log.Error("Error in video frame callback", ex);
            }
        }

        private void Deliver(HImage image, int frameIndex, int totalFrames, bool isInitialFrame)
        {
            image.isDispose = true;
            _presentFrame(new VideoFrameDelivery(image, SessionId, frameIndex, totalFrames, isInitialFrame));
        }

        private void ReleasePending(PendingFrame frame)
        {
            Interlocked.CompareExchange(ref _pendingFrame, null, frame);
            frame.Dispose();
        }

        private void OnStatusChanged(int handle, int status, long generation)
        {
            if (!IsCurrent(handle, generation) || _dispatcher.HasShutdownStarted) return;
            long timeline = Volatile.Read(ref _timeline);
            try
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!IsCurrent(handle, generation) || timeline != Volatile.Read(ref _timeline)) return;
                    try
                    {
                        switch (status)
                        {
                            case 0:
                                IsPlaying = false;
                                TryAudio(_audio.Pause);
                                break;
                            case 1:
                                IsPlaying = true;
                                break;
                            case 2:
                                IsPlaying = false;
                                TryAudio(_audio.Pause);
                                Seek(0);
                                return;
                            default:
                                return;
                        }
                        OnStateChanged();
                    }
                    catch (Exception ex)
                    {
                        log.Error("Error while handling video status", ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                log.Error("Error in video status callback", ex);
            }
        }

        private bool IsCurrent(int handle, long generation)
            => handle > 0 && handle == Volatile.Read(ref _handle) && generation == Volatile.Read(ref _generation);

        private TimeSpan GetPosition(int frameIndex) => TimeSpan.FromSeconds(frameIndex / (Info.fps > 0 ? Info.fps : 30.0));

        private void CorrectAudioSync(int frameIndex)
        {
            if (!IsPlaying) return;
            TryAudio(() =>
            {
                TimeSpan position = GetPosition(frameIndex);
                if (Math.Abs((_audio.Position - position).TotalSeconds) > AudioSyncDriftThresholdSeconds)
                    _audio.Position = position;
            });
        }

        private static void TryAudio(Action action)
        {
            try { action(); }
            catch (Exception ex) { log.Warn("Video audio operation failed", ex); }
        }

        public void Close()
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(Close);
                return;
            }

            int handle = Interlocked.Exchange(ref _handle, -1);
            Interlocked.Increment(ref _generation);
            try
            {
                if (handle > 0)
                {
                    try { if (IsPlaying) _backend.Pause(handle); }
                    finally { _backend.Close(handle); }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _pendingFrame, null)?.Dispose();
                _frameCallback = null;
                _statusCallback = null;
                IsPlaying = false;
                SessionId = Guid.Empty;
                TryAudio(_audio.Close);
                OnStateChanged();
            }
        }

        private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        private void VerifyUsable()
        {
            _dispatcher.VerifyAccess();
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            if (_disposed) return;
            try { Close(); }
            finally
            {
                _disposed = true;
                StateChanged = null;
            }
        }

        private sealed class PendingFrame(HImage image, int index, int totalFrames, long timeline, Action<HImage> release) : IDisposable
        {
            private readonly object _gate = new();
            private bool _disposeRequested;
            private bool _delivering;
            private bool _released;
            public HImage Image { get; } = image;
            public int Index { get; } = index;
            public int TotalFrames { get; } = totalFrames;
            public long Timeline { get; } = timeline;

            public bool TryBeginDelivery()
            {
                lock (_gate)
                {
                    if (_disposeRequested) return false;
                    _delivering = true;
                    return true;
                }
            }

            public void EndDelivery()
            {
                bool releaseNow;
                lock (_gate)
                {
                    _delivering = false;
                    releaseNow = _disposeRequested && !_released;
                    if (releaseNow) _released = true;
                }
                if (releaseNow) release(Image);
            }

            public void Dispose()
            {
                bool releaseNow;
                lock (_gate)
                {
                    _disposeRequested = true;
                    releaseNow = !_delivering && !_released;
                    if (releaseNow) _released = true;
                }
                if (releaseNow) release(Image);
            }
        }
    }
}
