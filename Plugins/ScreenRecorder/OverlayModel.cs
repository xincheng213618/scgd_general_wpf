using ScreenRecorderLib;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenRecorder
{
    /// <summary>
    /// 覆盖层模型，用于配置录制中的覆盖效果
    /// </summary>
    public class OverlayModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// 是否启用该覆盖层
        /// </summary>
        public bool IsEnabled { get => _IsEnabled; set { if (_IsEnabled == value) return; _IsEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsEnabled;

        /// <summary>
        /// 覆盖层对象（可以是视频、图片、文本等）
        /// </summary>
        public RecordingOverlayBase Overlay { get => _Overlay; set { if (_Overlay == value) return; _Overlay = value; NotifyPropertyChanged(); } }
        private RecordingOverlayBase _Overlay;
    }
}
