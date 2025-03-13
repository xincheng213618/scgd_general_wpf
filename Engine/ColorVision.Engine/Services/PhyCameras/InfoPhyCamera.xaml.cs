using ColorVision.UI;
using ColorVision.UI.Extension;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);
        }
    }
}
