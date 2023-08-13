using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.MQTT.Service;
using ColorVision.MQTT.Spectrum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Service
{
    /// <summary>
    /// MQTTDeviceSpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTDeviceSpectrumControl : UserControl, IDisposable
    {
        public MQTTDeviceSpectrum MQTTDeviceSp { get; set; }

        private SpectrumService? spectrum;
        private bool disposedValue;
        private bool disposedObj;

        public MQTTDeviceSpectrumControl(MQTTDeviceSpectrum mqttDeviceSp)
        {
            this.disposedObj = false;
            this.MQTTDeviceSp = mqttDeviceSp;
            MQTTManager manager = MQTTManager.GetInstance();
            foreach (SpectrumService sp in manager.MQTTSpectrums)
            {
                if(sp.Device.SysResourceModel.Id == MQTTDeviceSp.SysResourceModel.Id)
                {
                    spectrum = sp;
                    break;
                }
            }

            if(spectrum == null)
            {
                spectrum = new SpectrumService(this.MQTTDeviceSp);
                disposedObj = true;
            }

            spectrum.AutoParamHandlerEvent += Spectrum_AutoParamHandlerEvent;
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
            if (spectrum != null) spectrum.GetParam();
        }

        private void Button_Click_Submit(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
            if (spectrum != null) spectrum.SetParam(MQTTDeviceSp.Config.TimeLimit, MQTTDeviceSp.Config.TimeFrom);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (disposedObj && spectrum!=null)
                    {
                        spectrum.Dispose();
                        spectrum = null;
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~MQTTDeviceSpectrumControl()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
