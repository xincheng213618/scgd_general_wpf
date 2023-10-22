using ColorVision.MVVM;
using ColorVision.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.RC
{
    /// <summary>
    /// RCServiceConnect.xaml 的交互逻辑
    /// </summary>
    public partial class RCServiceConnect : Window
    {
        public RCServiceConfig rcServiceConfig { get; set; }
        private RCServiceConfig rcServiceConfigBackUp { get; set; }

        public ObservableCollection<RCServiceConfig> rcServiceConfigs { get; set; }
        public RCServiceConnect()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            rcServiceConfig = GlobalSetting.GetInstance().SoftwareConfig.RcServiceConfig;
            GridRCService.DataContext = rcServiceConfig;
            rcServiceConfigBackUp = new RCServiceConfig();
            rcServiceConfig.CopyTo(rcServiceConfigBackUp);
            PasswordBox1.Password = rcServiceConfig.AppSecret;

            rcServiceConfigs = GlobalSetting.GetInstance().SoftwareConfig.RcServiceConfigs;
            ListViewRC.ItemsSource = rcServiceConfigs;

            rcServiceConfigs.Insert(0, rcServiceConfig);
            ListViewRC.SelectedIndex = 0;

            this.Closed += (s, e) =>
            {
                rcServiceConfigs.Remove(rcServiceConfig);
            };

            ListViewRCBorder.PreviewKeyUp += (s, e) =>
            {
                if (ListViewRC.SelectedIndex > -1)
                {
                    rcServiceConfigs.RemoveAt(ListViewRC.SelectedIndex);
                    ListViewRC.SelectedIndex = 0;
                }
            };
        }

        private void Button_Click_Ok(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(rcServiceConfig.Name))
            {
                rcServiceConfig.Name = rcServiceConfig.AppId + "_" + rcServiceConfig.RCName;
            }
            rcServiceConfig.AppSecret = PasswordBox1.Password;
            rcServiceConfigs.Remove(rcServiceConfig);

            GlobalSetting.GetInstance().SaveSoftwareConfig();
            Task.Run(() => ServiceManager.GetInstance().rcService.ReRegist());
            this.Close();
        }

        private void Button_Click_Cancel(object sender, RoutedEventArgs e)
        {
            rcServiceConfigBackUp.CopyTo(rcServiceConfig);
            this.Close();
        }

        private void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            rcServiceConfig.AppSecret = PasswordBox1.Password;
            Task.Run(() =>
            {
                bool IsConnect = ServiceManager.GetInstance().rcService.TryRegist(rcServiceConfig);
                MessageBox.Show($"连接{(IsConnect ? "成功" : "失败")}");
            });
        }

        private void Button_Click_ListShow(object sender, RoutedEventArgs e)
        {
            if (ListViewRCBorder.Visibility == Visibility.Visible)
            {
                ListViewRCBorder.Visibility = Visibility.Collapsed;
                this.Width -= 170;
            }
            else
            {
                ListViewRCBorder.Visibility = Visibility.Visible;
                this.Width += 170;
            }
        }



        private void MenuItem_Click_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is RCServiceConfig rcConfig)
            {
                rcServiceConfigs.Remove(rcConfig);
            }
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                rcServiceConfig = rcServiceConfigs[listView.SelectedIndex];
                GridRCService.DataContext = rcServiceConfig;
                PasswordBox1.Password = rcServiceConfig.AppSecret;
                GlobalSetting.GetInstance().SoftwareConfig.RcServiceConfig = rcServiceConfig;
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            RCServiceConfig rCServiceConfig = new RCServiceConfig();
            rcServiceConfigs.Add(rCServiceConfig);
            ListViewRC.SelectedValue = rCServiceConfig;
        }
        private void Button_Click_Copy(object sender, RoutedEventArgs e)
        {
            RCServiceConfig newCfg = new RCServiceConfig();
            rcServiceConfig.CopyTo(newCfg);

            newCfg.Name = newCfg.Name + "_1";
            rcServiceConfigs.Add(newCfg);
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            RCServiceConfig newCfg = new RCServiceConfig();
            newCfg.Name = "New Profile";
            rcServiceConfigs.Add(newCfg);
        }
    }
}
