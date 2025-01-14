using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ColorVision.Themes.Controls.Uploads
{
    public class FileUploadInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 消息通知事件
        /// </summary>
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public FileUploadInfo()
        {

        }

        public string FileName { get => _FileName; set { _FileName = value; NotifyPropertyChanged(); } }
        private string _FileName;

        public string FilePath { get => _FilePath; set { _FilePath = value; NotifyPropertyChanged(); } }
        private string _FilePath;

        public string FileSize { get => _FileSize; set { _FileSize = value; NotifyPropertyChanged(); } }
        private string _FileSize;
        public string FileProgressValue { get => _FileProgressValue; set { _FileProgressValue = value; NotifyPropertyChanged(); } }
        private string _FileProgressValue;
        public UploadStatus UploadStatus { get => _UploadStatus; set { _UploadStatus = value; NotifyPropertyChanged(); } }
        private UploadStatus _UploadStatus;

    }
}
