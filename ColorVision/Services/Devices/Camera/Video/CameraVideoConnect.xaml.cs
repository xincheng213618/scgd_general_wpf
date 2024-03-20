using ColorVision.Common.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.Camera.Video
{
    /// <summary>
    /// MySqlConnect.xaml 的交互逻辑
    /// </summary>
    public partial class CameraVideoConnect : Window
    {

        public CameraVideoConfig CameraVideoConfig { get; set; }

        private CameraVideoConfig CameraVideoConfigCopy { get; set; }

        public CameraVideoConnect(CameraVideoConfig config)
        {
            CameraVideoConfig = config;
            CameraVideoConfigCopy = config.CloneValuesTo();
            InitializeComponent();
        }

        public ObservableCollection<CameraVideoConfig> CameraVideoConfigs { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            GridMQTT.DataContext = CameraVideoConfigCopy;
            CameraVideoConfigs = new ObservableCollection<CameraVideoConfig>();
            ListViewMySql.ItemsSource = CameraVideoConfigs;
            CameraVideoConfigs.Insert(0, CameraVideoConfig);
            ListViewMySql.SelectedIndex = 0;
            this.Closed += (s, e) =>
            {
                CameraVideoConfigs.Remove(CameraVideoConfig);
            };
        }

        public void NumberValidationTextBox(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Back || e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = false;
                return;
            }
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CameraVideoConfigCopy.Name))
            {
                CameraVideoConfigCopy.Name = CameraVideoConfigCopy.Host +"_" + CameraVideoConfigCopy.Port;
            }
            CameraVideoConfigCopy.CloneValuesTo(CameraVideoConfig);
            this.Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click_Test1(object sender, RoutedEventArgs e)
        {
            if (ListViewMySqlBorder.Visibility == Visibility.Visible)
            {
                ListViewMySqlBorder.Visibility = Visibility.Collapsed;
                this.Width -= 170;
            }
            else
            {
                ListViewMySqlBorder.Visibility = Visibility.Visible;
                this.Width += 170;
            }           
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {

        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                CameraVideoConfigCopy = CameraVideoConfigs[listView.SelectedIndex];
                GridMQTT.DataContext = CameraVideoConfigCopy;
            }
        }

        private void Button_Click_Test2(object sender, RoutedEventArgs e)
        {
            CameraVideoConfig cameraVideoConfig = new CameraVideoConfig() {};
            CameraVideoConfig.CloneValuesTo(cameraVideoConfig);
            CameraVideoConfigs.Add(cameraVideoConfig);

        }

        private void MenuItem_Click_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is CameraVideoConfig config)
            {
                CameraVideoConfigs.Remove(config);
            }
        }
    }
}
