using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeFrameOptions : ViewModelBase
    {
        public int MaxDisplayFps
        {
            get => _maxDisplayFps;
            set
            {
                int normalized = value < 0 ? 0 : value;
                if (_maxDisplayFps == normalized) return;
                _maxDisplayFps = normalized;
                OnPropertyChanged();
            }
        }
        private int _maxDisplayFps = 60;

        public bool AutoZoomOnFirstFrame
        {
            get => _autoZoomOnFirstFrame;
            set
            {
                if (_autoZoomOnFirstFrame == value) return;
                _autoZoomOnFirstFrame = value;
                OnPropertyChanged();
            }
        }
        private bool _autoZoomOnFirstFrame = true;

        public bool UpdateImageMetadata
        {
            get => _updateImageMetadata;
            set
            {
                if (_updateImageMetadata == value) return;
                _updateImageMetadata = value;
                OnPropertyChanged();
            }
        }
        private bool _updateImageMetadata = true;

        public bool EnableDiagnostics
        {
            get => _enableDiagnostics;
            set
            {
                if (_enableDiagnostics == value) return;
                _enableDiagnostics = value;
                OnPropertyChanged();
            }
        }
        private bool _enableDiagnostics = true;

        public ReferenceLineParam ReferenceLineParam
        {
            get => _referenceLineParam;
            set
            {
                if (ReferenceEquals(_referenceLineParam, value)) return;
                _referenceLineParam = value;
                OnPropertyChanged();
            }
        }
        private ReferenceLineParam _referenceLineParam = new();
    }
}
