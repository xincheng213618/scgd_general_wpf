using ColorVision.Services.Dao;
using System;
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

        private void Window_Initialized(object sender, EventArgs e)
        {
            foreach (var item in DeviceCamera.VisualChildren)
            {
                if (item is GroupService groupService)
                    GroupServices.Add(groupService);
            }
            ListView1.ItemsSource = GroupServices;
            ListView1.SelectedIndex = 0;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                StackPanelCab.DataContext = GroupServices[ListView1.SelectedIndex];
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
                SysResourceDao.DeleteById(GroupServices[ListView1.SelectedIndex].SysResourceModel.Id);
                GroupServices.RemoveAt(ListView1.SelectedIndex);
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
