using ColorVision.Services.Dao;
using ColorVision.Services.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Calibration.Templates
{
    /// <summary>
    /// CalibrationEdit.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationEdit : Window
    {
        public ICalibrationService<BaseResourceObject> CalibrationService { get; set; }
        public CalibrationEdit(ICalibrationService<BaseResourceObject> calibrationService)
        {
            CalibrationService = calibrationService;
            InitializeComponent();
        }

        public ObservableCollection<GroupResource> groupResources { get; set; } = new ObservableCollection<GroupResource>();

        public ObservableCollection<CalibrationResource> DarkNoiseList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DefectPointList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DSNUList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> UniformityList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> DistortionList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> ColorShiftList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LuminanceList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumOneColorList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumFourColorList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LumMultiColorList { get; set; } = new ObservableCollection<CalibrationResource>();


        private void Window_Initialized(object sender, EventArgs e)
        {
            foreach (var item in CalibrationService.VisualChildren)
            {
                if (item is GroupResource groupResource)
                {
                    groupResource.SetCalibrationResource(CalibrationService);
                    groupResources.Add(groupResource);
                }
                if (item is CalibrationResource calibrationResource)
                {
                    switch ((ResouceType)calibrationResource.SysResourceModel.Type)
                    {
                        case ResouceType.DarkNoise:
                            DarkNoiseList.Add(calibrationResource);
                            break;
                        case ResouceType.DefectPoint:
                            DefectPointList.Add(calibrationResource);
                            break;
                        case ResouceType.DSNU:
                            DSNUList.Add(calibrationResource);
                            break;
                        case ResouceType.Uniformity:
                            UniformityList.Add(calibrationResource);
                            break;
                        case ResouceType.Distortion:
                            DistortionList.Add(calibrationResource);
                            break;
                        case ResouceType.ColorShift:
                            ColorShiftList.Add(calibrationResource);
                            break;
                        case ResouceType.Luminance:
                            LuminanceList.Add(calibrationResource);
                            break;
                        case ResouceType.LumOneColor:
                            LumOneColorList.Add(calibrationResource);
                            break;
                        case ResouceType.LumFourColor:
                            LumFourColorList.Add(calibrationResource);
                            break;
                        case ResouceType.LumMultiColor:
                            LumMultiColorList.Add(calibrationResource);
                            break;
                        default:
                            break;
                    }
                }
            }

            ListView1.ItemsSource = groupResources;
            if (groupResources.Count > 0)
            {
                ListView1.SelectedIndex = 0;
                StackPanelCab.DataContext = groupResources[0];
            }

            ComboBoxDarkNoise.ItemsSource = DarkNoiseList;
            ComboBoxDefectPoint.ItemsSource = DefectPointList;
            ComboBoxDSNU.ItemsSource = DSNUList;
            ComboBoxUniformity.ItemsSource = UniformityList;
            ComboBoxDistortion.ItemsSource = DistortionList;
            ComboBoxColorShift.ItemsSource = ColorShiftList;
            ComboBoxLuminance.ItemsSource = LuminanceList;
            ComboBoxLumOneColor.ItemsSource = LumOneColorList;
            ComboBoxLumFourColor.ItemsSource = LumFourColorList;
            ComboBoxLumMultiColor.ItemsSource = LumMultiColorList;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                StackPanelCab.DataContext = groupResources[ListView1.SelectedIndex];
            }
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            string calue = NewCreateFileName("title");
            var group = GroupResource.AddGroupResource(CalibrationService, calue);
            if (group != null)
            {
                groupResources.Add(group);
            }
            else
            {
                MessageBox.Show("创建失败");
            }
        }

        public string NewCreateFileName(string FileName)
        {
            var list = groupResources.Select(g => g.Name).Distinct().ToList();
            for (int i = 1; i < 9999; i++)
            {
                if (!list.Contains($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }


        SysResourceDao SysResourceDao = new SysResourceDao();

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                GroupResource groupResource = groupResources[ListView1.SelectedIndex];
                SysResourceDao.DeleteById(groupResource.SysResourceModel.Id,false);
                groupResources.Remove(groupResource);
                CalibrationService.VisualChildren.Remove(groupResource);
                MessageBox.Show("删除成功");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (var item in groupResources)
            {
                item.Save();
            }
        }
    }
}
