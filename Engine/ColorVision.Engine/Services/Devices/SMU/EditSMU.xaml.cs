using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Devices.SMU
{
    /// <summary>
    /// EditSMU.xaml 的交互逻辑
    /// </summary>
    public partial class EditSMU : Window
    {
        public DeviceSMU Device { get; set; }
        public ConfigSMU EditConfig {  get; set; }
        public EditSMU(DeviceSMU  deviceSMU)
        {
            Device = deviceSMU;
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
            List<string> Serials = new() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
            TextSerial.ItemsSource = Serials;
            List<string> devTypes = new() { "Keithley_2400", "Keithley_2600", "Precise_S100" , "Vxi11Protocol", "VictualPss" };
            SMUType.ItemsSource = devTypes;
            DataContext = Device;

            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;

            EditStackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(EditConfig));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(Device.Config);
            Close();
        }
    }
}
