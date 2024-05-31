using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;

namespace ColorVision.Engine.Controls
{

    public interface IUploadMsg
    {
        public string Msg { get; }

        public ObservableCollection<string> UploadList { get; }


        public event EventHandler UploadClosed;
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
