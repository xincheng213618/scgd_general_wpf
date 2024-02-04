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
        public DeviceSpectrum Device { get; set; }

        private MQTTSpectrum? SpectrumService;
        private bool disposedValue;
        private bool disposedObj;

        public bool IsCanEdit { get; set; }
        public DeviceSpectrumControl(DeviceSpectrum mqttDeviceSp, bool isCanEdit = true)
        {
            this.disposedObj = false;
            this.Device = mqttDeviceSp;
            SpectrumService = mqttDeviceSp.DeviceService;
            SpectrumService.AutoParamHandlerEvent += Spectrum_AutoParamHandlerEvent;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = this.Device;

            if (IsCanEdit)
            {
                UserControl userControl = Device.GetEditControl();
                if (userControl.Parent is Panel grid)
                    grid.Children.Remove(userControl);
                MQTTEditContent.Children.Add(userControl);
            }
        }

        private void Spectrum_AutoParamHandlerEvent(AutoIntTimeParam colorPara)
        {
            Device.Config.BeginIntegralTime = colorPara.fTimeB;
            Device.Config.MaxIntegralTime = colorPara.iLimitTime;
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


    }
}
