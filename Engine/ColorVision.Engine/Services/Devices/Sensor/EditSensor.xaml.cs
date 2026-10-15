using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Ports;
using ColorVision.UI;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Devices.Sensor
{
    /// <summary>
    /// EditSensor.xaml 的交互逻辑
    /// </summary>
    public partial class EditSensor : Window
    {
        public DeviceSensor Device { get; set; }

        public ConfigSensor EditConfig { get; set; }

        public EditSensor(DeviceSensor device)
        {
            Device = device;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            var list1 = MySqlSetting.IsConnect ? SysDictionaryModMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 5 } }) : new List<SysDictionaryModModel>();

            var liss = new Dictionary<string, string>() {  };

            foreach (var item in list1)
            {
                if (item.Name !=null && item.Code !=null)
                    liss.Add(item.Name, item.Code);
            }
            liss.TryAdd(Device.Config.Category, Device.Config.Category);
            ComboBoxSensor.ItemsSource = liss;


            List<int> BaudRates = new() { 115200, 38400, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 57600 };
            var Serials = SerialPort.GetPortNames().Append(Device.Config.Addr).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray();
            ComboBoxPort.ItemsSource = BaudRates;
            ComboBoxSerial.ItemsSource = Serials;


            DataContext = Device;
            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;

            if (MySqlSetting.IsConnect) CameraPhyID.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;
            CameraPhyID.DisplayMemberPath = "Code";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Close();
        }
    }
}
