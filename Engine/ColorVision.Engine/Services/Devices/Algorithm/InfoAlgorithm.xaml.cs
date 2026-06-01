using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    /// <summary>
    /// InfoAlgorithm.xaml 的交互逻辑s
    /// </summary>
    public partial class InfoAlgorithm : UserControl
    {
        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public InfoAlgorithm(DeviceAlgorithm device)
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
                if (MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.FileDeletionPermanent, "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    var MsgRecord = DService.CacheClear();
                    MsgRecord.MsgSucessed += (s,e) =>
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.FileServiceCleanupComplete, "ColorVison");
                    };
                    ServicesHelper.SendCommand(button, MsgRecord);
                }
            }
        }
    }
}
