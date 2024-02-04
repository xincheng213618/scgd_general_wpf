using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Services.Devices
{

    public interface IUploadMsg
    {
        public string Msg { get; }

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
            this.DataContext = IUploadMsg;
            IUploadMsg.UploadClosed += (s, e) => this.Close();
        }
    }
}
