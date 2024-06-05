using ColorVision.Common.MVVM;
using System;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Devices.Motor
{
    /// <summary>
    /// EditMotor.xaml 的交互逻辑
    /// </summary>
    public partial class EditMotor : Window
    {
        public DeviceMotor Device { get; set; }

        public ConfigMotor EditConfig { get; set; }

        public EditMotor(DeviceMotor device)
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
