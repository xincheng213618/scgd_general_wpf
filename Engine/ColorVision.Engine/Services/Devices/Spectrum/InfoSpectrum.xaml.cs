using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.UI.Extension;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
            SpectrumService.AutoParamHandlerEvent += Spectrum_AutoParamHandlerEvent;
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            Device.RefreshDeviceId();
        }

        private void Spectrum_AutoParamHandlerEvent(AutoIntTimeParam colorPara)
        {
            Device.Config.BeginIntegralTime = colorPara.fTimeB;
            Device.Config.MaxIntegralTime = colorPara.iLimitTime;
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


        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UniformGrid uniformGrid)
                uniformGrid.AutoUpdateLayout();
        }
    }
}
