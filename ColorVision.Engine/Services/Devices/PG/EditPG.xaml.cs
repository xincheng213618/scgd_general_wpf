using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Spectrum;
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
using ColorVision.Themes;
using ColorVision.Engine.Services.PhyCameras;


namespace ColorVision.Engine.Services.Devices.PG
{
    /// <summary>
    /// EditPG.xaml 的交互逻辑
    /// </summary>
    public partial class EditPG : Window
    {
        public DevicePG Device { get; set; }

        public ConfigPG EditConfig { get; set; }

        public EditPG(DevicePG devicePG)
        {
            Device = devicePG;
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
