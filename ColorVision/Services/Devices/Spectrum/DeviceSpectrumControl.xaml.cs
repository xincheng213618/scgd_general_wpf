using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.SettingUp;
using ColorVision.Templates;

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

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control menuItem)
            {
                SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show("数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (menuItem.Tag?.ToString() ?? string.Empty)
                {
                    case "SpectrumResourceParam":
                        SpectrumResourceControl calibration = Device.SpectrumResourceParams.Count == 0 ? new SpectrumResourceControl(Device) : new SpectrumResourceControl(Device, Device.SpectrumResourceParams[0].Value);
                        windowTemplate = new WindowTemplate(TemplateType.SpectrumResourceParam, calibration, Device);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                }
            }
        }
    }
}
