using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Camera
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoCamera : UserControl
    {
        public DeviceCamera Device { get; set; }

        public MQTTCamera DService { get => Device.DeviceService; }

        public InfoCamera(DeviceCamera deviceCamera)
        {
            Device = deviceCamera;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

        }


        private void ServiceCache_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (MessageBox1.Show(Application.Current.GetActiveWindow(), "文件删除后不可找回", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    var MsgRecord = DService.CacheClear();
                    MsgRecord.MsgSucessed += (s) =>
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), "文件服务清理完成", "ColorVison");
                        MsgRecord.ClearMsgRecordSucessChangedHandler();
                    };
                    ServicesHelper.SendCommand(button, MsgRecord);
                }
            }
        }


        private void TextBlock_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                Common.NativeMethods.Clipboard.SetText(textBlock.Text);
                MessageBox1.Show(textBlock.Text);
            }
        }
    }
}
