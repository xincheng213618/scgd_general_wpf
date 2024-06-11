using System;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace ColorVision.Util.Controls
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
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = IUploadMsg1;
            IUploadMsg1.UploadClosed += (s, e) => Close();
        }
    }
}
