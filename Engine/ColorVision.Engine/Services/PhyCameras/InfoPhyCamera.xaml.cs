using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoPhyCamera : UserControl
    {
        public PhyCamera Device { get; set; }
        public InfoPhyCamera(PhyCamera deviceCamera)
        {
            Device = deviceCamera;
            InitializeComponent();
            DataContext = Device;
        }
    }
}
