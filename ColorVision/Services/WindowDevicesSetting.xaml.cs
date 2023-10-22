using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System;
using System.Windows.Documents;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ColorVision.MQTT;

namespace ColorVision.Services
{
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class WindowDevicesSetting : Window
    {
        public ObservableCollection<BaseChannel> MQTTDevices { get; set; }
        public ObservableCollection<BaseChannel> MQTTDevices1 { get; set; }

        AdornerLayer mAdornerLayer { get; set; }
        public WindowDevicesSetting(ObservableCollection<BaseChannel> Devices)
        {
            MQTTDevices = new ObservableCollection<BaseChannel>();
            MQTTDevices1 = new ObservableCollection<BaseChannel>();

            foreach (var item in ServiceManager.GetInstance().MQTTDevices)
            {
                MQTTDevices.Add(item);
            }
            foreach (var item in Devices)
            {
                MQTTDevices.Remove(item);
                MQTTDevices1.Add(item);
            }
            InitializeComponent();
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
            if (SeriesExportTreeView2.SelectedItem is BaseChannel mQTTDevice)
            {
                MQTTDevices1.Remove(mQTTDevice);
                MQTTDevices1.Insert(MQTTDevices1.Count, mQTTDevice);
                mQTTDevice.IsSelected = true;
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
            if (SeriesExportTreeView2.SelectedItem is BaseChannel mQTTDevice)
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
            if (SeriesExportTreeView2.SelectedItem is BaseChannel mQTTDevice)
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
            if (SeriesExportTreeView2.SelectedItem is BaseChannel mQTTDevice)
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
            if (SeriesExportTreeView2.SelectedItem is BaseChannel mQTTDevice)
            {
                MQTTDevices1.Remove(mQTTDevice);
                MQTTDevices.Add(mQTTDevice);
            }
        }

        private void Button_Click_02(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView1.SelectedItem is BaseChannel mQTTDevice)
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
            this.Close();
        }



        private void Button_Click_0(object sender, RoutedEventArgs e)
        {

        }
    }

}
