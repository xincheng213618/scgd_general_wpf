using ColorVision.Common.Extension;
using ColorVision.Services.Dao;
using ColorVision.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Camera.Calibrations
{



    /// <summary>
    /// CalibrationEdit.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationEdit : Window
    {
        public DeviceCamera DeviceCamera { get; set; }
        public CalibrationEdit(DeviceCamera deviceCamera)
        {
            DeviceCamera = deviceCamera;
            InitializeComponent();
        }

        public ObservableCollection<GroupService> GroupServices { get; set; } = new ObservableCollection<GroupService>();

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
            foreach (var item in DeviceCamera.VisualChildren)
            {
                if (item is GroupService groupService)
                    GroupServices.Add(groupService);
                if(item is CalibrationResource calibrationResource)
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
            this.DataContext = this;

            ListView1.ItemsSource = GroupServices;
            ListView1.SelectedIndex = 0;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                //StackPanelCab.DataContext = GroupServices[ListView1.SelectedIndex];
            }
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            string calue = NewCreateFileName("title");
            var group = GroupService.AddGroupService(DeviceCamera, calue);
            if (group != null)
            {
                GroupServices.Add(group);
            }
            else
            {
                MessageBox.Show("创建失败");
            }
        }

        public string NewCreateFileName(string FileName)
        {
            for (int i = 1; i < 9999; i++)
            {
                if (!DeviceCamera.Config.CalibrationRsourcesGroups.ContainsKey($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }
        SysResourceDao SysResourceDao = new SysResourceDao();

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                GroupService groupService = GroupServices[ListView1.SelectedIndex];
                SysResourceDao.DeleteById(groupService.SysResourceModel.Id);
                GroupServices.Remove(groupService);
                DeviceCamera.VisualChildren.Remove(groupService);
                MessageBox.Show("删除成功");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (var item in GroupServices)
            {
                item.Save();
            }
        }
    }
}
