using ColorVision.Common.Extension;
using ColorVision.Services.Devices.PG;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Spectrum;
using cvColorVision;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Text;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Common.MVVM;


namespace ColorVision.Services.Devices.PG
{
    /// <summary>
    /// EditCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditPG : Window
    {
        public DevicePG Device { get; set; }

        public ConfigPG EditConfig { get; set; }

        public EditPG(DevicePG devicePG)
        {
            Device = devicePG;
            InitializeComponent();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            Device.DeviceService.ReLoadCategoryLib();
            pgCategory.ItemsSource = Device.DeviceService.PGCategoryLib;

            foreach (var item in Device.DeviceService.PGCategoryLib)
            {
                if (item.Key.Equals(Device.Config.Category, StringComparison.Ordinal))
                {
                    pgCategory.SelectedItem = item;
                    break;
                }
            }

            IsNet.Checked += (s,e)=>
            {
                TextBlockPGIP.Text = "串口";
                TextBlockPGPort.Text = "波特率";
            };
            IsComm.Checked += (s,e)=> 
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
