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
            ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = this.MQTTDeviceSp;
        }

        private void Spectrum_AutoParamHandlerEvent(AutoIntTimeParam colorPara)
        {
            MQTTDeviceSp.Config.TimeFrom = colorPara.fTimeB;
            MQTTDeviceSp.Config.TimeLimit = colorPara.iLimitTime;
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
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
            ButtonEdit.Visibility = Visibility.Visible;
            if (SpectrumService != null) SpectrumService.SetParam(MQTTDeviceSp.Config.TimeLimit, MQTTDeviceSp.Config.TimeFrom);
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            ButtonEdit.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }
    }
}
