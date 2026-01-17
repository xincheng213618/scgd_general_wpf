using System;
using System.Collections.Generic;
using System.Windows;
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
            DataContext = Device;

            EditConfig = Device.Config.Clone();
            EditContent.DataContext = EditConfig;

            Device.DService.ReLoadCategoryLib();

            pgCategory.SelectionChanged += (s, e) =>
            {
                if (pgCategory.SelectedIndex == 4)
                {
                    EditConfig.Addr = "0";
                    EditConfig.Port = 0x08;
                    RegisterAddressDockPanel.Visibility = Visibility.Visible;

                    TextBlockPGIP.Text = "串口id";
                    TextBlockPGPort.Text = "设备地址";
                }
                else
                {
                    RegisterAddressDockPanel.Visibility = Visibility.Collapsed;
                }
            };

            pgCategory.ItemsSource = Device.DService.PGCategoryLib;

            foreach (var item in Device.DService.PGCategoryLib)
            {
                if (item.Key.Equals(Device.Config.Category, StringComparison.Ordinal))
                {
                    pgCategory.SelectedItem = item;
                    break;
                }
            }


            if(EditConfig.Category == "CH431.I2C")
            {
                TextBlockPGIP.Text = "串口id";
                TextBlockPGPort.Text = "设备地址";
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
