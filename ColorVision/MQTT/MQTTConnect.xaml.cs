using ColorVision.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            FlowEngineLib.MQTTHelper.SetDefaultCfg(MQTTConfig.Host, MQTTConfig.Port, MQTTConfig.UserName, MQTTConfig.UserPwd, false, null);
            Task.Run(() => MQTTControl.GetInstance().Connect(MQTTConfig));
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
            ListViewMQTT.SelectedIndex = 0;
            this.Closed += (s, e) =>
            {
                MQTTConfigs.Remove(MQTTConfig);
            };
        }

        private async void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            bool IsConnect = await MQTTControl.TestConnect(MQTTConfig);
            MessageBox.Show($"连接{(IsConnect ? "成功" : "失败")}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);

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
        private void Button_Click_Copy(object sender, RoutedEventArgs e)
        {
            MQTTConfig  mQTTConfig = new MQTTConfig() { };
            mQTTConfig.Name = mQTTConfig.Name + "_1";

            MQTTConfig.CopyTo(mQTTConfig);
            MQTTConfigs.Add(mQTTConfig);
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            MQTTConfig newCfg = new MQTTConfig();
            newCfg.Name = "New Profile";
            MQTTConfigs.Add(newCfg);
        }
    }
}
