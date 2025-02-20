using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Services.RC;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.MQTT
{

    public class ExportMQTTMenuItem : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string GuidId => nameof(ExportMQTTMenuItem);
        public override string Header => "MQTT";
        public override int Order => 20;
        public override void Execute()
        {
            new MySqlConnect() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    public class ExportMQTTConnect : MenuItemBase
    {
        public override string OwnerGuid => nameof(ExportMQTTMenuItem);
        public override string GuidId => nameof(ExportMQTTConnect);
        public override string Header => Resources.MqttConnectionConfiguration;
        public override int Order => 2;
        public override void Execute()
        {
            new MySqlConnect() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }



    /// <summary>
    /// MySqlConnect.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTConnect : Window
    {
        public MQTTConnect()
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
            if (string.IsNullOrEmpty(MQTTConfig.Name))
            {
                MQTTConfig.Name = MQTTConfig.Host +"_" +MQTTConfig.Port;
            }
            MQTTConfigs.Remove(MQTTConfig);
            FlowEngineLib.MQTTHelper.SetDefaultCfg(MQTTConfig.Host, MQTTConfig.Port, MQTTConfig.UserName, MQTTConfig.UserPwd, false, null);
            Task.Run(() => MQTTControl.GetInstance().Connect(MQTTConfig));
            Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MQTTConfigBackUp.CopyTo(MQTTConfig);
            Close();
        }


        public MQTTConfig MQTTConfig { get;set;}

        private MQTTConfig MQTTConfigBackUp { get; set; }
        public ObservableCollection<MQTTConfig> MQTTConfigs { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTConfig= MQTTSetting.Instance.MQTTConfig;
            GridMQTT.DataContext = MQTTConfig;
            MQTTConfigBackUp = new MQTTConfig();
            MQTTConfig.CopyTo(MQTTConfigBackUp);

            MQTTConfigs = MQTTSetting.Instance.MQTTConfigs;
            ListViewMQTT.ItemsSource = MQTTConfigs;

            MQTTConfigs.Insert(0, MQTTConfig);
            ListViewMQTT.SelectedIndex = 0;
            Closed += (s, e) =>
            {
                MQTTConfigs.Remove(MQTTConfig);
            };
        }

        private void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            Task.Run( async () =>
            {
                bool IsConnect = await MQTTControl.GetInstance().TestConnect(MQTTConfig);
                await Dispatcher.BeginInvoke(() =>
                {
                    Task.Run(() =>
                    {
                        MqttRCService.GetInstance().QueryServices();
                        MqttRCService.GetInstance().ReRegist();
                    });
                    MessageBox1.Show($"连接{(IsConnect ? "成功" : "失败")}", "ColorVision");
                });
            });

        }

        private void Button_Click_Test1(object sender, RoutedEventArgs e)
        {
            if (ListViewMQTTBorder.Visibility == Visibility.Visible)
            {
                ListViewMQTTBorder.Visibility = Visibility.Collapsed;
                Width -= 170;
            }
            else
            {
                ListViewMQTTBorder.Visibility = Visibility.Visible;
                Width += 170;
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
                MQTTSetting.Instance.MQTTConfig = MQTTConfig;
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
            MQTTConfig  mQTTConfig = new() { };
            mQTTConfig.Name = mQTTConfig.Name + "_1";

            MQTTConfig.CopyTo(mQTTConfig);
            MQTTConfigs.Add(mQTTConfig);
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            MQTTConfig newCfg = new();
            newCfg.Name = "New Profile";
            MQTTConfigs.Add(newCfg);
        }
    }
}
