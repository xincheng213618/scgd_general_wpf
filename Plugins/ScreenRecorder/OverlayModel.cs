using ScreenRecorderLib;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenRecorder
{
    public class OverlayModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 消息通知事件
        /// </summary>
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public bool IsEnabled { get => _IsEnabled; set { if (_IsEnabled == value) return; _IsEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsEnabled;

        public RecordingOverlayBase Overlay { get => _Overlay; set { if (_Overlay == value) return; _Overlay = value; NotifyPropertyChanged(); } }
        private RecordingOverlayBase _Overlay;
    }
}
