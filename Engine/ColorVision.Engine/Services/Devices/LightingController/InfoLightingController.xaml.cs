using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.LightingController
{
    public partial class InfoLightingController : UserControl
    {
        private DeviceLightingController Device { get; }

        public InfoLightingController(DeviceLightingController device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);
        }
    }
}
