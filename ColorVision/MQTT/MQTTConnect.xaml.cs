using ColorVision.MVVM;
using ColorVision.SettingUp;
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

namespace ColorVision.MQTT
{
    /// <summary>
    /// MySqlConnect.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTConnect : Window
    {
        public MQTTConnect()
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
            if (string.IsNullOrEmpty(MQTTConfig.Name))
            {
                MQTTConfig.Name = MQTTConfig.Host +"_" +MQTTConfig.Port;
            }
            MQTTConfigs.Remove(MQTTConfig);
            GlobalSetting.GetInstance().SaveSoftwareConfig();
            Task.Run(() => MQTTControl.GetInstance().Connect());
            this.Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MQTTConfigBackUp.CopyTo(MQTTConfig);
            this.Close();
        }


        public MQTTConfig MQTTConfig { get;set;}

        private MQTTConfig MQTTConfigBackUp { get; set; }
        public ObservableCollection<MQTTConfig> MQTTConfigs { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTConfig= GlobalSetting.GetInstance().SoftwareConfig.MQTTConfig;
            GridMQTT.DataContext = MQTTConfig;
            MQTTConfigBackUp = new MQTTConfig();
            MQTTConfig.CopyTo(MQTTConfigBackUp);

            MQTTConfigs = GlobalSetting.GetInstance().SoftwareConfig.MQTTConfigs;
            ListViewMQTT.ItemsSource = MQTTConfigs;

            MQTTConfigs.Insert(0, MQTTConfig);
            this.Closed += (s, e) =>
            {
                MQTTConfigs.Remove(MQTTConfig);
            };
        }

        private async void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            bool IsConnect = await MQTTControl.TestConnect(MQTTConfig);
            MessageBox.Show($"连接{(IsConnect ? "成功" : "失败")}");

        }

        private void Button_Click_Test1(object sender, RoutedEventArgs e)
        {
            if (ListViewMQTTBorder.Visibility == Visibility.Visible)
            {
                ListViewMQTTBorder.Visibility = Visibility.Collapsed;
                this.Width -= 170;
            }
            else
            {
                ListViewMQTTBorder.Visibility = Visibility.Visible;
                this.Width += 170;
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                MQTTConfig = MQTTConfigs[listView.SelectedIndex];
                GridMQTT.DataContext = MQTTConfig;
                GlobalSetting.GetInstance().SoftwareConfig.MQTTConfig = MQTTConfig;
            }

        }

        private void MenuItem_Click_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MQTTConfig config)
            {
                MQTTConfigs.Remove(config);
            }
        }

        private void Button_Click_Test2(object sender, RoutedEventArgs e)
        {
            MQTTConfig config = new MQTTConfig() { };
            MQTTConfig.CopyTo(config);
            MQTTConfigs.Add(config);
        }
    }
}
