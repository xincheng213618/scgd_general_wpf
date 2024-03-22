using ColorVision.Common.MVVM;
using ColorVision.Services.Devices.Camera;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Devices.Algorithm
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
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {

            ObservableCollection<string> Devices = new ObservableCollection<string>();
            foreach (var item in ServiceManager.GetInstance().DeviceServices)
            {
                if (item is DeviceCamera camera)
                {
                    if (!Devices.Contains(camera.Code))
                        Devices.Add(camera.Code);
                }
            }
            TextBox_BindDevice.ItemsSource = Devices;

            this.DataContext = Device;
            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            this.Close();
        }


    }
}
