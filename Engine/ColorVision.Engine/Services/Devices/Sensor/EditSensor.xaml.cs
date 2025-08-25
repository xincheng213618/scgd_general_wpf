using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ColorVision.Database;


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
            var list1 = SysDictionaryModMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 5 } });

            var liss = new Dictionary<string, string>() {  };

            foreach (var item in list1)
            {
                if (item.Name !=null && item.Code !=null)
                    liss.Add(item.Name, item.Code);
            }
            ComboBoxSensor.ItemsSource = liss;


            List<int> BaudRates = new() { 115200, 38400, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 57600 };
            List<string> Serials = new() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10" };
            ComboBoxPort.ItemsSource = BaudRates;
            ComboBoxSerial.ItemsSource = Serials;


            DataContext = Device;
            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;

            CameraPhyID.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;
            CameraPhyID.DisplayMemberPath = "Code";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Close();
        }
    }
}
