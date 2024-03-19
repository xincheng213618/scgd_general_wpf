using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Algorithm
{
    /// <summary>
    /// EditAlgorithm.xaml 的交互逻辑
    /// </summary>
    public partial class EditAlgorithm : Window
    {
        public DeviceAlgorithm Device { get; set; }

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
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


    }
}
