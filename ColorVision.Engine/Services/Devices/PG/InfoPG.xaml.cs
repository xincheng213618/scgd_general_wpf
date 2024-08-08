using ColorVision.UI.Extension;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
        }

        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UniformGrid uniformGrid)
            {
                uniformGrid.AutoUpdateLayout();
            }
        }
    }
}
