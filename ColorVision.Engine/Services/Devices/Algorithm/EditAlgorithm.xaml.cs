using ColorVision.Common.MVVM;
using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    /// <summary>
    /// EditAlgorithm.xaml 的交互逻辑
    /// </summary>
    public partial class EditAlgorithm : Window
    {
        public DeviceAlgorithm Device { get; set; }
        public ConfigAlgorithm EditConfig { get; set; }

        public EditAlgorithm(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            DataContext = Device;
            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Close();
        }


    }
}
