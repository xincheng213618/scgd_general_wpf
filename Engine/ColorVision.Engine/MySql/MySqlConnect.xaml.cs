using ColorVision.Common.MVVM;
using ColorVision.Engine.Properties;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.MySql
{
    public class ExportMySqlMenuItem : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string GuidId => nameof(ExportMySqlMenuItem);
        public override string Header => "MySql";
        public override int Order => 20;
        public override void Execute()
        {
            new MySqlConnect() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

    public class ExportMySqlConnect : MenuItemBase
    {
        public override string OwnerGuid => nameof(ExportMySqlMenuItem);
        public override string GuidId => nameof(ExportMySqlConnect);
        public override string Header => Resources.MysqlConnectionConfiguration;
        public override int Order => 2;

        public override void Execute()
        {
            new MySqlConnect() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
    /// <summary>
    /// MySqlConnect.xaml 的交互逻辑
    /// </summary>
    public partial class MySqlConnect : Window
    {
        public MySqlConnect()
        {
            InitializeComponent();
            this.ApplyCaption();
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

            Task.Run(() => MySqlControl.GetInstance().Connect());
            Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MySqlConfigBackUp.CopyTo(MySqlConfig);
            Close();
        }

        public MySqlConfig MySqlConfig { get;set;}

        private MySqlConfig MySqlConfigBackUp { get; set; }

        public ObservableCollection<MySqlConfig> MySqlConfigs { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MySqlConfig= MySqlSetting.Instance.MySqlConfig;
            GridMQTT.DataContext = MySqlConfig;
            MySqlConfigBackUp = new MySqlConfig();
            MySqlConfig.CopyTo(MySqlConfigBackUp);
            PasswordBox1.Password = MySqlConfig.UserPwd;
            MySqlConfigs = MySqlSetting.Instance.MySqlConfigs;
            ListViewMySql.ItemsSource = MySqlConfigs;

            MySqlConfigs.Insert(0, MySqlConfig);
            ListViewMySql.SelectedIndex = 0;

            Closed += (s, e) =>
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
                Dispatcher.BeginInvoke(() => MessageBox.Show($"连接{(IsConnect ? "成功" : "失败")}", "ColorVision"));
            });
        }

        private void Button_Click_Test1(object sender, RoutedEventArgs e)
        {
            if (ListViewMySqlBorder.Visibility == Visibility.Visible)
            {
                ListViewMySqlBorder.Visibility = Visibility.Collapsed;
                Width -= 170;
            }
            else
            {
                ListViewMySqlBorder.Visibility = Visibility.Visible;
                Width += 170;
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
                MySqlSetting.Instance.MySqlConfig = MySqlConfig;
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
            MySqlConfig mySqlConfig = new() { };
            mySqlConfig.Name = mySqlConfig.Name + "_1";

            MySqlConfig.CopyTo(mySqlConfig);
            MySqlConfigs.Add(mySqlConfig);
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            MySqlConfig newCfg = new();
            newCfg.Name = "New Profile";
            MySqlConfigs.Add(newCfg);
        }
    }
}
