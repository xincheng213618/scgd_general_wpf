﻿using System.Collections.Generic;
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
        public ObservableCollection<GroupService> GroupServices { get; set; } = new ObservableCollection<GroupService>();


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
            ComboBoxList.ItemsSource = GroupServices;
            foreach (var item in CalibrationService.VisualChildren)
            {
                if (item is GroupService groupService)
                {
                    groupService.SetCalibrationResource(CalibrationService);
                    GroupServices.Add(groupService);
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

                if (comboBox.SelectedValue is GroupService groupService)
                {
                    CalibrationParam.Normal.DarkNoise.FilePath = groupService.DarkNoise?.Name ?? string.Empty;
                    CalibrationParam.Normal.DarkNoise.Id = groupService.DarkNoise?.Id;
                    CalibrationParam.Normal.DefectPoint.FilePath = groupService.DefectPoint?.Name ?? string.Empty;
                    CalibrationParam.Normal.DefectPoint.Id = groupService.DefectPoint?.Id;
                    CalibrationParam.Normal.DSNU.FilePath = groupService.DSNU?.Name ?? string.Empty;
                    CalibrationParam.Normal.DSNU.Id = groupService.DSNU?.Id;
                    CalibrationParam.Normal.Distortion.FilePath = groupService.Distortion?.Name ?? string.Empty;
                    CalibrationParam.Normal.Distortion.Id = groupService.Distortion?.Id;
                    CalibrationParam.Normal.ColorShift.FilePath = groupService.ColorShift?.Name ?? string.Empty;
                    CalibrationParam.Normal.ColorShift.Id = groupService.ColorShift?.Id;
                    CalibrationParam.Normal.Uniformity.FilePath = groupService.Uniformity?.Name ?? string.Empty;
                    CalibrationParam.Normal.Uniformity.Id = groupService.Uniformity?.Id;
                    CalibrationParam.Color.Luminance.FilePath = groupService.Luminance?.Name ?? string.Empty;
                    CalibrationParam.Color.Luminance.Id = groupService.Luminance?.Id;
                    CalibrationParam.Color.LumFourColor.FilePath = groupService.LumFourColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumFourColor.Id = groupService.LumFourColor?.Id;
                    CalibrationParam.Color.LumMultiColor.FilePath = groupService.LumMultiColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumMultiColor.Id = groupService.LumMultiColor?.Id;
                    CalibrationParam.Color.LumOneColor.FilePath = groupService.LumOneColor?.Name ?? string.Empty;
                    CalibrationParam.Color.LumOneColor.Id = groupService.LumOneColor?.Id;
                }

            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CalibrationEdit CalibrationEdit = new CalibrationEdit(CalibrationService);
            CalibrationEdit.Closed += (s, e) =>
            {
                GroupServices.Clear();
                foreach (var item in CalibrationService.VisualChildren)
                {
                    if (item is GroupService groupService)
                        GroupServices.Add(groupService);
                }
            };
            CalibrationEdit.Show();


        }


    }


}