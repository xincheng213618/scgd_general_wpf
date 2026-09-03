using System;
using ColorVision.UI;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// InfoSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class InfoSpectrum : UserControl, IDisposable
    {
        public DeviceSpectrum Device { get; set; }

        public InfoSpectrum(DeviceSpectrum device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
