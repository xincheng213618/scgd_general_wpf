using ColorVision.MVVM;
using ColorVision.SettingUp;
using ColorVision.Util;
using HandyControl.Tools.Extension;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Video
{
    /// <summary>
    /// MySqlConnect.xaml 的交互逻辑
    /// </summary>
    public partial class CameraVideoConnect : Window
    {
        public CameraVideoConnect()
        {
            InitializeComponent();
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
            if (string.IsNullOrEmpty(CameraVideoConfig.Name))
            {
                CameraVideoConfig.Name = CameraVideoConfig.Host +"_" + CameraVideoConfig.Port;
            }

            GlobalSetting.GetInstance().SaveSoftwareConfig();
            this.Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            CameraVideoConfigBackUp.CopyTo(CameraVideoConfig);
            this.Close();
        }


        public CameraVideoConfig CameraVideoConfig { get;set;}

        private CameraVideoConfig CameraVideoConfigBackUp { get; set; }

        public ObservableCollection<CameraVideoConfig> CameraVideoConfigs { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CameraVideoConfig= GlobalSetting.GetInstance().SoftwareConfig.CameraVideoConfig;
            GridMQTT.DataContext = CameraVideoConfig;
            CameraVideoConfigBackUp = new CameraVideoConfig();
            CameraVideoConfig.CopyTo(CameraVideoConfigBackUp);
            CameraVideoConfigs = GlobalSetting.GetInstance().SoftwareConfig.CameraVideoConfigs;

            ListViewMySql.ItemsSource = CameraVideoConfigs;
            CameraVideoConfigs.Insert(0, CameraVideoConfig);
            ListViewMySql.SelectedIndex = 0;

            this.Closed += (s, e) =>
            {
                CameraVideoConfigs.Remove(CameraVideoConfig);
            };
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
                CameraVideoConfig = CameraVideoConfigs[listView.SelectedIndex];
                GridMQTT.DataContext = CameraVideoConfig;
                GlobalSetting.GetInstance().SoftwareConfig.CameraVideoConfig = CameraVideoConfig;
            }
        }

        private void Button_Click_Test2(object sender, RoutedEventArgs e)
        {
            CameraVideoConfig cameraVideoConfig = new CameraVideoConfig() {};
            CameraVideoConfig.CopyTo(cameraVideoConfig);
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
