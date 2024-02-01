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

namespace ColorVision.Services.Devices.Camera
{
    /// <summary>
    /// UploadMsg.xaml 的交互逻辑
    /// </summary>
    public partial class UploadMsg : Window
    {
        public DeviceCamera DeviceCamera { get; set; }
        public UploadMsg(DeviceCamera deviceCamera)
        {
            DeviceCamera = deviceCamera;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DeviceCamera;
        }
    }
}
