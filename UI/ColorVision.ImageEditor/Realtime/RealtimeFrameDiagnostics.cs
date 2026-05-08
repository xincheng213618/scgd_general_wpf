using ColorVision.Common.MVVM;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeFrameDiagnostics : ViewModelBase
    {
        public long SubmittedFrames
        {
            get => _submittedFrames;
            internal set
            {
                if (_submittedFrames == value) return;
                _submittedFrames = value;
                OnPropertyChanged();
            }
        }
        private long _submittedFrames;

        public long RenderedFrames
        {
            get => _renderedFrames;
            internal set
            {
                if (_renderedFrames == value) return;
                _renderedFrames = value;
                OnPropertyChanged();
            }
        }
        private long _renderedFrames;

        public long DroppedFrames
        {
            get => _droppedFrames;
            internal set
            {
                if (_droppedFrames == value) return;
                _droppedFrames = value;
                OnPropertyChanged();
            }
        }
        private long _droppedFrames;

        public double DisplayFps
        {
            get => _displayFps;
            internal set
            {
                if (_displayFps == value) return;
                _displayFps = value;
                OnPropertyChanged();
            }
        }
        private double _displayFps;

        public double LastCopyMilliseconds
        {
            get => _lastCopyMilliseconds;
            internal set
            {
                if (_lastCopyMilliseconds == value) return;
                _lastCopyMilliseconds = value;
                OnPropertyChanged();
            }
        }
        private double _lastCopyMilliseconds;

        public void Reset()
        {
            SubmittedFrames = 0;
            RenderedFrames = 0;
            DroppedFrames = 0;
            DisplayFps = 0;
            LastCopyMilliseconds = 0;
        }
    }
}
