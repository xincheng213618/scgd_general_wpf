using System;
using System.Windows;

namespace ColorVision.Themes.Controls
{
    /// <summary>
    /// UploadMsg.xaml 的交互逻辑
    /// </summary>
    public partial class UploadMsg : Window
    {
        public IUploadMsg IUploadMsg1 { get; set; }
        public UploadMsg(IUploadMsg iUploadMsg)
        {
            IUploadMsg1 = iUploadMsg;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = IUploadMsg1;
            IUploadMsg1.UploadClosed += (s, e) => Close();
        }
    }
}
