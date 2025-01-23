using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Devices.Calibration
{
    /// <summary>
    /// EditCalibration.xaml 的交互逻辑
    /// </summary>
    public partial class EditCalibration : Window
    {
        public DeviceCalibration DeviceCalibration { get; set; }

        public MQTTCalibration Service { get => DeviceCalibration.DService; }

        public ConfigCalibration EditConfig { get; set; }
        public EditCalibration(DeviceCalibration  deviceCalibration)
        {
            DeviceCalibration = deviceCalibration;
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
            DataContext = DeviceCalibration;

            CameraPhyID.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;
            CameraPhyID.SelectedItem = PhyCameraManager.GetInstance().GetPhyCamera(DeviceCalibration.Config.CameraCode);
            CameraPhyID.DisplayMemberPath = "Code";
            CameraPhyID.SelectedValuePath = "Name";
            EditConfig = DeviceCalibration.Config.Clone();
            EditContent.DataContext = EditConfig;
            EditStackPanel.Children.Add(UI.PropertyEditor.PropertyEditorHelper.GenPropertyEditorControl(EditConfig.FileServerCfg));


        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DeviceCalibration.PhyCamera?.ReleaseCalibration();
            EditConfig.CopyTo(DeviceCalibration.Config);
            Close();
        }
    }
}
