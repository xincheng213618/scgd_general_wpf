using ColorVision.Common.MVVM;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeFrameStats : ViewModelBase
    {
        private long _submittedFrames;
        private long _acceptedFrames;
        private long _displayedFrames;
        private long _droppedFrames;
        private long _frozenDroppedFrames;
        private long _lastRateTimestamp = Stopwatch.GetTimestamp();
        private long _lastSubmittedFrames;
        private long _lastDisplayedFrames;

        public long SubmittedFrames => Interlocked.Read(ref _submittedFrames);

        public long AcceptedFrames => Interlocked.Read(ref _acceptedFrames);

        public long DisplayedFrames => Interlocked.Read(ref _displayedFrames);

        public long DroppedFrames => Interlocked.Read(ref _droppedFrames);

        public long FrozenDroppedFrames => Interlocked.Read(ref _frozenDroppedFrames);

        public double SubmittedFps { get; private set; }

        public double DisplayedFps { get; private set; }

        public double LastUiLatencyMilliseconds { get; private set; }

        public bool IsFrozen { get; private set; }

        public void RecordSubmitted()
        {
            Interlocked.Increment(ref _submittedFrames);
        }

        public void RecordAccepted()
        {
            Interlocked.Increment(ref _acceptedFrames);
        }

        public void RecordDropped(bool becauseFrozen = false)
        {
            Interlocked.Increment(ref _droppedFrames);
            if (becauseFrozen)
            {
                Interlocked.Increment(ref _frozenDroppedFrames);
            }
        }

        public void RecordDisplayed(DateTime submittedUtc, bool isFrozen)
        {
            Interlocked.Increment(ref _displayedFrames);
            LastUiLatencyMilliseconds = Math.Max(0, (DateTime.UtcNow - submittedUtc).TotalMilliseconds);
            IsFrozen = isFrozen;
            RefreshRates(force: false);
        }

        public void Refresh(bool isFrozen)
        {
            IsFrozen = isFrozen;
            RefreshRates(force: true);
        }

        public void Reset(bool isFrozen)
        {
            Interlocked.Exchange(ref _submittedFrames, 0);
            Interlocked.Exchange(ref _acceptedFrames, 0);
            Interlocked.Exchange(ref _displayedFrames, 0);
            Interlocked.Exchange(ref _droppedFrames, 0);
            Interlocked.Exchange(ref _frozenDroppedFrames, 0);
            _lastSubmittedFrames = 0;
            _lastDisplayedFrames = 0;
            _lastRateTimestamp = Stopwatch.GetTimestamp();
            SubmittedFps = 0;
            DisplayedFps = 0;
            LastUiLatencyMilliseconds = 0;
            IsFrozen = isFrozen;
            RaiseAll();
        }

        private void RefreshRates(bool force)
        {
            long now = Stopwatch.GetTimestamp();
            double elapsedSeconds = (now - _lastRateTimestamp) / (double)Stopwatch.Frequency;
            if (!force && elapsedSeconds < 0.5)
            {
                return;
            }

            long submitted = SubmittedFrames;
            long displayed = DisplayedFrames;

            if (elapsedSeconds > 0)
            {
                SubmittedFps = (submitted - _lastSubmittedFrames) / elapsedSeconds;
                DisplayedFps = (displayed - _lastDisplayedFrames) / elapsedSeconds;
            }

            _lastSubmittedFrames = submitted;
            _lastDisplayedFrames = displayed;
            _lastRateTimestamp = now;

            RaiseAll();
        }

        private void RaiseAll([CallerMemberName] string? _ = null)
        {
            OnPropertyChanged(nameof(SubmittedFrames));
            OnPropertyChanged(nameof(AcceptedFrames));
            OnPropertyChanged(nameof(DisplayedFrames));
            OnPropertyChanged(nameof(DroppedFrames));
            OnPropertyChanged(nameof(FrozenDroppedFrames));
            OnPropertyChanged(nameof(SubmittedFps));
            OnPropertyChanged(nameof(DisplayedFps));
            OnPropertyChanged(nameof(LastUiLatencyMilliseconds));
            OnPropertyChanged(nameof(IsFrozen));
        }
    }
}
