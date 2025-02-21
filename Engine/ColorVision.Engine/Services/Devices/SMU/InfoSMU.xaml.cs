using ColorVision.Engine.Services.Devices.PG;
using ColorVision.UI;
using ColorVision.UI.Extension;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Engine.Services.Devices.SMU
{
    /// <summary>
    /// InfoSMU.xaml 的交互逻辑
    /// </summary>
    public partial class InfoSMU : UserControl
    {
        public DeviceSMU DeviceSMU { get; set; }
        public InfoSMU(DeviceSMU device)
        {
            DeviceSMU = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DeviceSMU;
            PropertyEditorHelper.GenCommand(DeviceSMU, CommandGrid);

        }

        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UniformGrid uniformGrid)
                uniformGrid.AutoUpdateLayout();
        }
    }
}
