using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.Spectrum
{
    /// <summary>
    /// DeviceSpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceSpectrumControl : UserControl, IDisposable
    {
        public DeviceSpectrum MQTTDeviceSp { get; set; }

        private SpectrumService? SpectrumService;
        private bool disposedValue;
        private bool disposedObj;

        public DeviceSpectrumControl(DeviceSpectrum mqttDeviceSp)
        {
            this.disposedObj = false;
            this.MQTTDeviceSp = mqttDeviceSp;
            SpectrumService = mqttDeviceSp.DeviceService;
            SpectrumService.AutoParamHandlerEvent += Spectrum_AutoParamHandlerEvent;
            InitializeComponent();
        }

        private void Spectrum_AutoParamHandlerEvent(AutoIntTimeParam colorPara)
        {
            MQTTDeviceSp.Config.TimeFrom = colorPara.fTimeB;
            MQTTDeviceSp.Config.TimeLimit = colorPara.iLimitTime;
        }



        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this.MQTTDeviceSp;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
            if (SpectrumService != null) SpectrumService.GetParam();
        }

        private void Button_Click_Submit(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
            if (SpectrumService != null) SpectrumService.SetParam(MQTTDeviceSp.Config.TimeLimit, MQTTDeviceSp.Config.TimeFrom);
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
