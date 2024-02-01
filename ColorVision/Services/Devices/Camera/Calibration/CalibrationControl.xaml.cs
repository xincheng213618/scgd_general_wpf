using ColorVision.Services.Dao;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.Camera.Calibrations
{
    /// <summary>
    /// CalibrationControl.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationControl : UserControl
    {
        public CalibrationParam CalibrationParam { get => _CalibrationParam; set { _CalibrationParam = value;} }
        private CalibrationParam _CalibrationParam;

        public DeviceCamera DeviceCamera { get; set; }

        public CalibrationControl(DeviceCamera DeviceCamera)
        {
            this.DeviceCamera = DeviceCamera;
            InitializeComponent();
            this.CalibrationParam = new CalibrationParam();
            this.DataContext = CalibrationParam;
        }

        public Dictionary<string, List<ColorVisionVCalibratioItem>> CalibrationModeList { get; set; }

        public CalibrationControl(DeviceCamera DeviceCamera,CalibrationParam calibrationParam)
        {
            this.DeviceCamera = DeviceCamera;
            InitializeComponent();
            this.CalibrationParam = calibrationParam;
            this.DataContext = CalibrationParam;
        }
        public ObservableCollection<GroupService> GroupServices { get; set; } = new ObservableCollection<GroupService>();


        public void Initializedsss(DeviceCamera DeviceCamera, CalibrationParam calibrationParam)
        {
            this.DeviceCamera = DeviceCamera;
            this.CalibrationParam = calibrationParam;
            this.DataContext = CalibrationParam;
            string CalibrationMode = calibrationParam.CalibrationMode;
            ComboBoxList.SelectionChanged -= ComboBox_SelectionChanged;
            GroupServices.Clear();
            foreach (var item in DeviceCamera.VisualChildren)
            {
                if (item is GroupService groupService)
                {
                    groupService.SetCalibrationResource(DeviceCamera);
                    GroupServices.Add(groupService);
                }
            }
            ComboBoxList.Text = CalibrationMode;
            ComboBoxList.SelectionChanged += ComboBox_SelectionChanged;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            ComboBoxList.ItemsSource = GroupServices;
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                CalibrationParam.Normal.DarkNoise.FilePath = string.Empty;
                CalibrationParam.Normal.DefectPoint.FilePath = string.Empty;
                CalibrationParam.Normal.DSNU.FilePath = string.Empty;
                CalibrationParam.Normal.Distortion.FilePath = string.Empty;
                CalibrationParam.Normal.ColorShift.FilePath = string.Empty;
                CalibrationParam.Normal.Uniformity.FilePath = string.Empty;

                CalibrationParam.Color.Luminance.FilePath = string.Empty;
                CalibrationParam.Color.LumFourColor.FilePath = string.Empty;
                CalibrationParam.Color.LumMultiColor.FilePath = string.Empty;
                CalibrationParam.Color.LumOneColor.FilePath = string.Empty;

                CalibrationParam.Normal.DarkNoise.IsSelected = false;
                CalibrationParam.Normal.DefectPoint.IsSelected = false;
                CalibrationParam.Normal.DSNU.IsSelected = false;
                CalibrationParam.Normal.Distortion.IsSelected = false;
                CalibrationParam.Normal.ColorShift.IsSelected = false;
                CalibrationParam.Normal.Uniformity.IsSelected = false;

                CalibrationParam.Color.Luminance.IsSelected = false;
                CalibrationParam.Color.LumFourColor.IsSelected = false;
                CalibrationParam.Color.LumMultiColor.IsSelected = false;
                CalibrationParam.Color.LumOneColor.IsSelected = false;

                if (comboBox.SelectedValue is GroupService calibrationRsourcesGroup)
                {
                    CalibrationParam.Normal.DarkNoise.FilePath = calibrationRsourcesGroup.DarkNoise?.Name ?? string.Empty;
                    CalibrationParam.Normal.DarkNoise.Id = calibrationRsourcesGroup.DarkNoise?.Id;
                    CalibrationParam.Normal.DefectPoint.FilePath = calibrationRsourcesGroup.DefectPoint?.Name ?? string.Empty;
                    CalibrationParam.Normal.DefectPoint.Id = calibrationRsourcesGroup.DefectPoint?.Id;
                    CalibrationParam.Normal.DSNU.FilePath = calibrationRsourcesGroup.DSNU?.Name ?? string.Empty;
                    CalibrationParam.Normal.DSNU.Id = calibrationRsourcesGroup.DSNU?.Id;
                    CalibrationParam.Normal.Distortion.FilePath = calibrationRsourcesGroup.Distortion?.Name ?? string.Empty;
                    CalibrationParam.Normal.Distortion.Id = calibrationRsourcesGroup.Distortion?.Id;
                    CalibrationParam.Normal.ColorShift.FilePath = calibrationRsourcesGroup.ColorShift?.Name ?? string.Empty;
                    CalibrationParam.Normal.ColorShift.Id = calibrationRsourcesGroup.ColorShift?.Id;
                    CalibrationParam.Normal.Uniformity.FilePath = calibrationRsourcesGroup.Uniformity?.Name ?? string.Empty;
                    CalibrationParam.Normal.Uniformity.Id = calibrationRsourcesGroup.Uniformity?.Id;
                    CalibrationParam.Color.Luminance.FilePath = calibrationRsourcesGroup.Luminance?.Name ?? string.Empty;
                    CalibrationParam.Color.Luminance.Id = calibrationRsourcesGroup.Luminance?.Id;
                    CalibrationParam.Color.LumFourColor.FilePath = calibrationRsourcesGroup.LumFourColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumFourColor.Id = calibrationRsourcesGroup.LumFourColor?.Id;
                    CalibrationParam.Color.LumMultiColor.FilePath = calibrationRsourcesGroup.LumMultiColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumMultiColor.Id = calibrationRsourcesGroup.LumMultiColor?.Id;
                    CalibrationParam.Color.LumOneColor.FilePath = calibrationRsourcesGroup.LumOneColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumOneColor.Id = calibrationRsourcesGroup.LumOneColor?.Id;
                }

            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CalibrationEdit CalibrationEdit = new CalibrationEdit(DeviceCamera);
            CalibrationEdit.Closed += (s, e) =>
            {
                GroupServices.Clear();
                foreach (var item in DeviceCamera.VisualChildren)
                {
                    if (item is GroupService groupService)
                        GroupServices.Add(groupService);
                }
            };
            CalibrationEdit.Show();


        }


    }


}
