using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Services.Devices.Spectrum.Configs;

namespace ColorVision.Services.Devices.Spectrum
{
    /// <summary>
    /// DeviceSpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceSpectrumControl : UserControl, IDisposable
    {
        public DeviceSpectrum MQTTDeviceSp { get; set; }

        private MQTTSpectrum? SpectrumService;
        private bool disposedValue;
        private bool disposedObj;

        public bool IsCanEdit { get; set; }
        public DeviceSpectrumControl(DeviceSpectrum mqttDeviceSp, bool isCanEdit = true)
        {
            this.disposedObj = false;
            this.MQTTDeviceSp = mqttDeviceSp;
            SpectrumService = mqttDeviceSp.DeviceService;
            SpectrumService.AutoParamHandlerEvent += Spectrum_AutoParamHandlerEvent;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = this.MQTTDeviceSp;

            List<string> Serials = new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            TextSerial.ItemsSource = Serials;
            List<int> BaudRates = new List<int> { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            TextBaudRate.ItemsSource = BaudRates;

        }

        private void Spectrum_AutoParamHandlerEvent(AutoIntTimeParam colorPara)
        {
            MQTTDeviceSp.Config.BeginIntegralTime = colorPara.fTimeB;
            MQTTDeviceSp.Config.MaxIntegralTime = colorPara.iLimitTime;
        }





        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
            ButtonEdit.Visibility = Visibility.Collapsed;
            if (SpectrumService != null) SpectrumService.GetParam();
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SpectrumService?.SetParam(MQTTDeviceSp.Config.MaxIntegralTime, MQTTDeviceSp.Config.BeginIntegralTime);
        }
    }
}
