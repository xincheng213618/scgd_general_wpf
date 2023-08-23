using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System;
using System.Windows.Documents;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Shapes;
using System.Net;
using System.Collections.ObjectModel;

namespace ColorVision.MQTT.Service
{
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class ServiceSettingWindow : Window
    {
        public ObservableCollection<MQTTDevice> MQTTDevices { get; set; }
        public ObservableCollection<MQTTDevice> MQTTDevices1 { get; set; }

        AdornerLayer mAdornerLayer { get; set; }
        public ServiceSettingWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialied(object sender, EventArgs e)
        {
            MQTTDevices = ServiceControl.GetInstance().MQTTDevices;
            MQTTDevices1 = new ObservableCollection<MQTTDevice>();

            SeriesExportTreeView1.ItemsSource = MQTTDevices;
            SeriesExportTreeView2.ItemsSource = MQTTDevices1;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

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
            if (SeriesExportTreeView2.SelectedItem is MQTTDevice mQTTDevice)
            {
                MQTTDevices1.Remove(mQTTDevice);
                MQTTDevices.Add(mQTTDevice);
            }
        }

        private void Button_Click_02(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView1.SelectedItem is MQTTDevice mQTTDevice)
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

        }



        private void Button_Click_0(object sender, RoutedEventArgs e)
        {

        }
    }

}
