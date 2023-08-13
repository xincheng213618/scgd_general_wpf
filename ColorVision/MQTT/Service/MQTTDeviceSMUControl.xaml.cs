using ColorVision.MQTT;
using ColorVision.MQTT.Service;
using ColorVision.MQTT.SMU;
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
    /// MQTTDeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTDeviceSMUControl : UserControl, IDisposable
    {
        public DeviceSMU MQTTDeviceSMU { get; set; }
        public ServiceControl ServiceControl { get; set; }

        private SMUService? device;
        private bool disposedValue;
        private bool disposedObj;

        public MQTTDeviceSMUControl(DeviceSMU mqttDeviceSMU)
        {
            this.disposedObj = false;
            this.MQTTDeviceSMU = mqttDeviceSMU;
            InitializeComponent();
            MQTTManager manager = MQTTManager.GetInstance();
            foreach (SMUService sp in manager.MQTTVISources)
            {
                if (sp.Device.SysResourceModel.Id == this.MQTTDeviceSMU.SysResourceModel.Id)
                {
                    device = sp;
                    break;
                }
            }

            if (device == null)
            {
                device = new SMUService(this.MQTTDeviceSMU);
                disposedObj = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this.MQTTDeviceSMU;
        }

        private void Button_Click_Edit(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_Submit(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (disposedObj && device != null)
                    {
                        device.Dispose();
                        device = null;
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~MQTTDeviceSMUControl()
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
