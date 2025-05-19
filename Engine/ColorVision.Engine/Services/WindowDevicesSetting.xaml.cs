using ColorVision.Themes;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services
{
    /// <summary>
    /// AvalonEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WindowDevicesSetting : Window
    {
        public ObservableCollection<DeviceService> MQTTDevices { get; set; }
        public ObservableCollection<DeviceService> MQTTDevices1 { get; set; }
        public WindowDevicesSetting(ObservableCollection<DeviceService> Devices)
        {
            MQTTDevices = new ObservableCollection<DeviceService>();
            MQTTDevices1 = new ObservableCollection<DeviceService>();

            foreach (var item in ServiceManager.GetInstance().DeviceServices)
            {
                MQTTDevices.Add(item);
            }
            foreach (var item in Devices)
            {
                MQTTDevices.Remove(item);
                MQTTDevices1.Add(item);
            }
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialied(object sender, EventArgs e)
        {
            SeriesExportTreeView1.ItemsSource = MQTTDevices;
            SeriesExportTreeView2.ItemsSource = MQTTDevices1;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is DeviceService deviceService)
            {
                MQTTDevices1.Remove(deviceService);
                MQTTDevices1.Insert(MQTTDevices1.Count, deviceService);
                deviceService.IsSelected = true;
            }
        }

        public static async void GetFocus(TreeView treeView,int index)
        {
            await Task.Delay(1);

            TreeViewItem firstNode = treeView.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;

            // 选中第一个节点
            if (firstNode != null)
            {
                firstNode.IsSelected = true;
                firstNode.Focus();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is DeviceService mQTTDevice)
            {
                int index = MQTTDevices1.IndexOf(mQTTDevice);
                if (index -1 >=0)
                {
                    MQTTDevices1.Remove(mQTTDevice);
                    MQTTDevices1.Insert(index - 1, mQTTDevice);
                    mQTTDevice.IsSelected = true;
                }

            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is DeviceService mQTTDevice)
            {
                int index = MQTTDevices1.IndexOf(mQTTDevice);
                if (index +1< MQTTDevices1.Count)
                {
                    MQTTDevices1.Remove(mQTTDevice);
                    MQTTDevices1.Insert(index+1, mQTTDevice);
                    mQTTDevice.IsSelected = true;
                }


            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is DeviceService mQTTDevice)
            {
                MQTTDevices1.Remove(mQTTDevice);
                MQTTDevices1.Insert(0,mQTTDevice);
                mQTTDevice.IsSelected = true;
            }
        }

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void Button_Click_04(object sender, RoutedEventArgs e)
        {
            foreach (var item in MQTTDevices1)
            {
                if (!MQTTDevices.Contains(item))
                    MQTTDevices.Add(item);
            }
            MQTTDevices1.Clear();
        }

        private void Button_Click_03(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is DeviceService mQTTDevice)
            {
                MQTTDevices1.Remove(mQTTDevice);
                MQTTDevices.Add(mQTTDevice);
            }
        }

        private void Button_Click_02(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView1.SelectedItem is DeviceService mQTTDevice)
            {
                MQTTDevices.Remove(mQTTDevice);
                MQTTDevices1.Add(mQTTDevice);
            }
        }

        private void Button_Click_01(object sender, RoutedEventArgs e)
        {
            foreach (var item in MQTTDevices)
            {

                if (!MQTTDevices1.Contains(item))
                    MQTTDevices1.Add(item);

            }
            MQTTDevices.Clear();

        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();


        }
    }

}
