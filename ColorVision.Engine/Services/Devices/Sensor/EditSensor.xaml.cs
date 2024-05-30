using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Services.Devices.Sensor
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
            var list1 = SysDictionaryModDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 5 } });

            var liss = new Dictionary<string, string>() {  };

            foreach (var item in list1)
            {
                if (item.Name !=null && item.Code !=null)
                    liss.Add(item.Name, item.Code);
            }


            pgCategory.ItemsSource = liss;

            IsComm.Checked += (s,e)=>
            {
                TextBlockPGIP.Text = "串口";
                TextBlockPGPort.Text = "波特率";
            };
            IsNet.Checked += (s,e)=> 
            {
                TextBlockPGIP.Text = "IP地址";
                TextBlockPGPort.Text = "端口";
            };

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
