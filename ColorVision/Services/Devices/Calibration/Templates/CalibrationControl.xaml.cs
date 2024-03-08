using ColorVision.Services.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.Calibration.Templates
{
    /// <summary>
    /// CalibrationControl.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationControl : UserControl
    {
        public CalibrationParam CalibrationParam { get => _CalibrationParam; set { _CalibrationParam = value;} }
        private CalibrationParam _CalibrationParam;

        public ICalibrationService<BaseResourceObject> CalibrationService { get; set; }

        public CalibrationControl(ICalibrationService<BaseResourceObject> calibrationService)
        {
            this.CalibrationService = calibrationService;

            InitializeComponent();
            this.CalibrationParam = new CalibrationParam();
            this.DataContext = CalibrationParam;
        }

        public Dictionary<string, List<ColorVisionVCalibratioItem>> CalibrationModeList { get; set; }

        public CalibrationControl(ICalibrationService<BaseResourceObject> calibrationService, CalibrationParam calibrationParam)
        {
            this.CalibrationService = calibrationService;
            InitializeComponent();
            this.CalibrationParam = calibrationParam;
            this.DataContext = CalibrationParam;
        }
        public ObservableCollection<GroupResource> groupResources { get; set; } = new ObservableCollection<GroupResource>();


        public void Initializedsss(ICalibrationService<BaseResourceObject> calibrationService, CalibrationParam calibrationParam)
        {
            ComboBoxList.SelectionChanged -= ComboBox_SelectionChanged;

            this.CalibrationService = calibrationService;
            this.CalibrationParam = calibrationParam;
            this.DataContext = CalibrationParam;

            string CalibrationMode = calibrationParam.CalibrationMode;

            ComboBoxList.Text = CalibrationMode;
            ComboBoxList.SelectionChanged += ComboBox_SelectionChanged;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            ComboBoxList.ItemsSource = groupResources;
            foreach (var item in CalibrationService.VisualChildren)
            {
                if (item is GroupResource groupResource)
                {
                    groupResource.SetCalibrationResource(CalibrationService);
                    groupResources.Add(groupResource);
                }
            }
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

                if (comboBox.SelectedValue is GroupResource groupResource)
                {
                    CalibrationParam.Normal.DarkNoise.FilePath = groupResource.DarkNoise?.Name ?? string.Empty;
                    CalibrationParam.Normal.DarkNoise.Id = groupResource.DarkNoise?.Id;
                    CalibrationParam.Normal.DefectPoint.FilePath = groupResource.DefectPoint?.Name ?? string.Empty;
                    CalibrationParam.Normal.DefectPoint.Id = groupResource.DefectPoint?.Id;
                    CalibrationParam.Normal.DSNU.FilePath = groupResource.DSNU?.Name ?? string.Empty;
                    CalibrationParam.Normal.DSNU.Id = groupResource.DSNU?.Id;
                    CalibrationParam.Normal.Distortion.FilePath = groupResource.Distortion?.Name ?? string.Empty;
                    CalibrationParam.Normal.Distortion.Id = groupResource.Distortion?.Id;
                    CalibrationParam.Normal.ColorShift.FilePath = groupResource.ColorShift?.Name ?? string.Empty;
                    CalibrationParam.Normal.ColorShift.Id = groupResource.ColorShift?.Id;
                    CalibrationParam.Normal.Uniformity.FilePath = groupResource.Uniformity?.Name ?? string.Empty;
                    CalibrationParam.Normal.Uniformity.Id = groupResource.Uniformity?.Id;
                    CalibrationParam.Color.Luminance.FilePath = groupResource.Luminance?.Name ?? string.Empty;
                    CalibrationParam.Color.Luminance.Id = groupResource.Luminance?.Id;
                    CalibrationParam.Color.LumFourColor.FilePath = groupResource.LumFourColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumFourColor.Id = groupResource.LumFourColor?.Id;
                    CalibrationParam.Color.LumMultiColor.FilePath = groupResource.LumMultiColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumMultiColor.Id = groupResource.LumMultiColor?.Id;
                    CalibrationParam.Color.LumOneColor.FilePath = groupResource.LumOneColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumOneColor.Id = groupResource.LumOneColor?.Id;
                }

            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CalibrationEdit CalibrationEdit = new CalibrationEdit(CalibrationService);
            CalibrationEdit.Closed += (s, e) =>
            {
                groupResources.Clear();
                foreach (var item in CalibrationService.VisualChildren)
                {
                    if (item is GroupResource groupResource)
                        groupResources.Add(groupResource);
                }
            };
            CalibrationEdit.Show();


        }


    }


}
