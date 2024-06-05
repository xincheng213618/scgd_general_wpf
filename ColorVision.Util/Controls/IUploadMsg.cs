using System;
using System.Collections.ObjectModel;

namespace ColorVision.Util.Controls
{
    public interface IUploadMsg
    {
        public string Msg { get; }

        public ObservableCollection<FileUploadInfo> UploadList { get; }


        public event EventHandler UploadClosed;
    }
}
