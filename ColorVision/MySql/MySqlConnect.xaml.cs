using ColorVision.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.MySql
{
    /// <summary>
    /// MySqlConnect.xaml 的交互逻辑
    /// </summary>
    public partial class MySqlConnect : Window
    {
        public MySqlConnect()
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
            if (string.IsNullOrEmpty(MySqlConfig.Name))
            {
                MySqlConfig.Name = MySqlConfig.Host +"_" +MySqlConfig.Port;
            }
            MySqlConfig.UserPwd = PasswordBox1.Password;
            MySqlConfigs.Remove(MySqlConfig);

            GlobalSetting.GetInstance().SaveSoftwareConfig();
            Task.Run(() => MySqlControl.GetInstance().Connect());
            this.Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MySqlConfigBackUp.CopyTo(MySqlConfig);
            this.Close();
        }


        public MySqlConfig MySqlConfig { get;set;}

        private MySqlConfig MySqlConfigBackUp { get; set; }

        public ObservableCollection<MySqlConfig> MySqlConfigs { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MySqlConfig= GlobalSetting.GetInstance().SoftwareConfig.MySqlConfig;
            GridMQTT.DataContext = MySqlConfig;
            MySqlConfigBackUp = new MySqlConfig();
            MySqlConfig.CopyTo(MySqlConfigBackUp);
            PasswordBox1.Password = MySqlConfig.UserPwd;
            MySqlConfigs = GlobalSetting.GetInstance().SoftwareConfig.MySqlConfigs;
            ListViewMySql.ItemsSource = MySqlConfigs;

            MySqlConfigs.Insert(0, MySqlConfig);
            ListViewMySql.SelectedIndex = 0;

            this.Closed += (s, e) =>
            {
                MySqlConfigs.Remove(MySqlConfig);
            };
        }

        private void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            MySqlConfig.UserPwd = PasswordBox1.Password;
            Task.Run(() =>
            {
                bool IsConnect = MySqlControl.TestConnect(MySqlConfig);
                MessageBox.Show(Application.Current.MainWindow, $"连接{(IsConnect ? "成功" : "失败")}","ColorVision");
            });


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
                MySqlConfig = MySqlConfigs[listView.SelectedIndex];
                GridMQTT.DataContext = MySqlConfig;
                PasswordBox1.Password = MySqlConfig.UserPwd;
                GlobalSetting.GetInstance().SoftwareConfig.MySqlConfig = MySqlConfig;
            }
        }


        private void MenuItem_Click_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MySqlConfig mySqlConfig)
            {
                MySqlConfigs.Remove(mySqlConfig);
            }
        }

        private void Button_Click_Copy(object sender, RoutedEventArgs e)
        {
            MySqlConfig mySqlConfig = new MySqlConfig() { };
            mySqlConfig.Name = mySqlConfig.Name + "_1";

            MySqlConfig.CopyTo(mySqlConfig);
            MySqlConfigs.Add(mySqlConfig);
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            MySqlConfig newCfg = new MySqlConfig();
            newCfg.Name = "New Profile";
            MySqlConfigs.Add(newCfg);
        }
    }
}
