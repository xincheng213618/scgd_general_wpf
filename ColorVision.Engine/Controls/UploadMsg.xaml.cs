using ColorVision.Common.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace ColorVision.Engine.Controls
{
    public sealed class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isconnect)
            {
                return isconnect ? Brushes.Blue : Brushes.Red;
            }
            return Brushes.Black; ;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting from a string to a memory size is not supported.");
        }
    }

    public interface IUploadMsg
    {
        public string Msg { get; }

        public ObservableCollection<FileUploadInfo> UploadList { get; }


        public event EventHandler UploadClosed;
    }

    public enum UploadStatus
    {
        Uploading,    // 上传中
        Waiting,      // 等待中
        Completed,    // 上传完成
        Failed,       // 失败
        CheckingMD5   // 检查 MD5
    }

    public class FileUploadInfo:ViewModelBase
    {
        public RelayCommand OpenFilePathCommand { get; set; }

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




    /// <summary>
    /// UploadMsg.xaml 的交互逻辑
    /// </summary>
    public partial class UploadMsg : Window
    {
        public IUploadMsg IUploadMsg { get; set; }
        public UploadMsg(IUploadMsg iUploadMsg)
        {
            IUploadMsg = iUploadMsg;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = IUploadMsg;
            IUploadMsg.UploadClosed += (s, e) => Close();
        }
    }
}
