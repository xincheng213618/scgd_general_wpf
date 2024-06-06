using ColorVision.Engine.Services.Devices.Spectrum.Configs;
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

        public bool IsCanEdit { get; set; }
        public InfoSpectrum(DeviceSpectrum mqttDeviceSp, bool isCanEdit = true)
        {
            disposedObj = false;
            Device = mqttDeviceSp;
            SpectrumService = mqttDeviceSp.DeviceService;
            SpectrumService.AutoParamHandlerEvent += Spectrum_AutoParamHandlerEvent;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = Device;
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
            {
                uniformGrid.Columns = uniformGrid.ActualWidth > 0 ? (int)(uniformGrid.ActualWidth / 200) : 1;
                uniformGrid.Rows = (int)Math.Ceiling(uniformGrid.Children.Count / (double)uniformGrid.Columns);
            }
        }
    }
}
