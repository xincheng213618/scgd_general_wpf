using ColorVision.UI;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// InfoSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class InfoSpectrum : UserControl, IDisposable
    {
        public DeviceSpectrum Device { get; set; }

        private MQTTSpectrum? SpectrumService;
        private bool disposedValue;
        private bool disposedObj;

        public InfoSpectrum(DeviceSpectrum mqttDeviceSp)
        {
            disposedObj = false;
            Device = mqttDeviceSp;
            SpectrumService = mqttDeviceSp.DService;
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);
            Device.RefreshEmptySpectrum();
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (disposedObj && SpectrumService != null)
                    {
                        SpectrumService.Dispose();
                        SpectrumService = null;
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
