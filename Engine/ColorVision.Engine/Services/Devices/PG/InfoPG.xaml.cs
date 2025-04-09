using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.PG
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoPG : UserControl
    {
        public DevicePG DevicePG { get; set; }
        public InfoPG(DevicePG devicePG)
        {
            DevicePG = devicePG;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DevicePG;
            PropertyEditorHelper.GenCommand(DevicePG, CommandGrid);
        }
    }
}
