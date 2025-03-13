using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    /// <summary>
    /// CalibrationEdit.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationEdit : Window
    {
        public ICalibrationService<ServiceObjectBase> CalibrationService { get; set; }

        private int Index;

        public CalibrationEdit(ICalibrationService<ServiceObjectBase> calibrationService , int index = 0)
        {
            CalibrationService = calibrationService;
            Index = index;

            InitializeComponent();
            this.ApplyCaption();
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
        public ObservableCollection<CalibrationResource> ColorDiffList { get; set; } = new ObservableCollection<CalibrationResource>();
        public ObservableCollection<CalibrationResource> LineArityList { get; set; } = new ObservableCollection<CalibrationResource>();


        private void Window_Initialized(object sender, EventArgs e)
        {
            foreach (var item in CalibrationService.VisualChildren)
            {
                if (item is GroupResource groupResource)
                {
                    groupResource.SetCalibrationResource();
                    groupResources.Add(groupResource);
                }
                if (item is CalibrationResource calibrationResource)
                {
                    switch ((ServiceTypes)calibrationResource.SysResourceModel.Type)
                    {
                        case ServiceTypes.DarkNoise:
                            DarkNoiseList.Add(calibrationResource);
                            break;
                        case ServiceTypes.DefectPoint:
                            DefectPointList.Add(calibrationResource);
                            break;
                        case ServiceTypes.DSNU:
                            DSNUList.Add(calibrationResource);
                            break;
                        case ServiceTypes.Uniformity:
                            UniformityList.Add(calibrationResource);
                            break;
                        case ServiceTypes.Distortion:
                            DistortionList.Add(calibrationResource);
                            break;
                        case ServiceTypes.ColorShift:
                            ColorShiftList.Add(calibrationResource);
                            break;
                        case ServiceTypes.Luminance:
                            LuminanceList.Add(calibrationResource);
                            break;
                        case ServiceTypes.LumOneColor:
                            LumOneColorList.Add(calibrationResource);
                            break;
                        case ServiceTypes.LumFourColor:
                            LumFourColorList.Add(calibrationResource);
                            break;
                        case ServiceTypes.LumMultiColor:
                            LumMultiColorList.Add(calibrationResource);
                            break;
                        case ServiceTypes.ColorDiff:
                            ColorDiffList.Add(calibrationResource);
                            break;
                        case ServiceTypes.LineArity:
                            LineArityList.Add(calibrationResource);
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
            ComboBoxColorDiff.ItemsSource = ColorDiffList;
            ComboBoxLineArity.ItemsSource = LineArityList;

            ListView1.SelectedIndex = Index;

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

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedItems.Count > 0)
            {
                // Create a SysDictionaryModDetaiModels to hold the items to be removed
                List<GroupResource> itemsToRemove = new List<GroupResource>();

                foreach (var selectedItem in ListView1.SelectedItems)
                {
                    GroupResource groupResource = selectedItem as GroupResource;
                    if (groupResource != null)
                    {
                        SysResourceDao.Instance.DeleteById(groupResource.SysResourceModel.Id, false);
                        itemsToRemove.Add(groupResource);
                    }
                }

                // Remove the items from the original SysDictionaryModDetaiModels and the visual children
                foreach (var item in itemsToRemove)
                {
                    groupResources.Remove(item);
                    CalibrationService.VisualChildren.Remove(item);
                }

                MessageBox.Show("删除成功");
            }
            else
            {
                MessageBox.Show("请选择要删除的项");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (var item in groupResources)
            {
                item.Save();
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is GroupResource groupResource)
            {
                groupResource.IsEditMode = false;
            }
        }
    }
}
