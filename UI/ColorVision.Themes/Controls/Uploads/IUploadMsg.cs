using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Themes.Controls.Uploads
{

    public class UploadMsgManager : IUploadMsg
    {
        public string Msg { get; set; }

        public ObservableCollection<FileUploadInfo> UploadList { get; set; } = new ObservableCollection<FileUploadInfo>();

        public event EventHandler UploadClosed;

        public void Close()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UploadClosed?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    public interface IUploadMsg
    {
        public string Msg { get; }

        public ObservableCollection<FileUploadInfo> UploadList { get; }


        public event EventHandler UploadClosed;
    }
}
