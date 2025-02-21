using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Extension;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    /// <summary>
    /// InfoAlgorithm.xaml 的交互逻辑s
    /// </summary>
    public partial class InfoThirdPartyAlgorithms : UserControl
    {
        public DeviceThirdPartyAlgorithms Device { get; set; }
        public ConfigThirdPartyAlgorithms EditConfig { get; set; }
        public MQTTThirdPartyAlgorithms DService { get => Device.DService; }

        public InfoThirdPartyAlgorithms(DeviceThirdPartyAlgorithms device)
        {
            Device = device;
            InitializeComponent();

        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);

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

        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UniformGrid uniformGrid)
            {
                uniformGrid.AutoUpdateLayout();
            }
        }
    }
}
