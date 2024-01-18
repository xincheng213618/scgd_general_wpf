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

        public ObservableCollection<CalibrationRsourcesGroup> CalibrationRsourcesGroups { get; set; } = new ObservableCollection<CalibrationRsourcesGroup>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            foreach (var item in DeviceCamera.Config.CalibrationRsourcesGroups)
            {
                CalibrationRsourcesGroups.Add(item.Value);
            }
            ListView1.ItemsSource = CalibrationRsourcesGroups;
            ListView1.SelectedIndex = 0;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                StackPanelCab.DataContext = CalibrationRsourcesGroups[ListView1.SelectedIndex];
            }
        }


        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            string calue = NewCreateFileName("title");
            CalibrationRsourcesGroup calibrationRsourcesGroup = new CalibrationRsourcesGroup() { Title = calue };
            DeviceCamera.Config.CalibrationRsourcesGroups.Add(calue, calibrationRsourcesGroup);
            CalibrationRsourcesGroups.Add(calibrationRsourcesGroup);
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


        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                CalibrationRsourcesGroup ss = CalibrationRsourcesGroups[ListView1.SelectedIndex];
                CalibrationRsourcesGroups.Remove(ss);
                DeviceCamera.Config.CalibrationRsourcesGroups.Remove(ss.Title);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            DeviceCamera.Save();
        }
    }
}
