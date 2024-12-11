using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Themes;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProjectKB
{
    /// <summary>
    /// MySql配置
    /// </summary>
    public class ModbusConfig : ViewModelBase
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        /// <summary>
        /// IP地址
        /// </summary>
        public string Host { get => _Host; set { _Host = value; NotifyPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        /// <summary>
        /// 端口地址
        /// </summary>
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged();
            }
        }
        private int _Port = 502;

        /// <summary>
        /// 账号
        /// </summary>
        public string UserName { get => _UserName; set { _UserName = value; NotifyPropertyChanged(); } }
        private string _UserName = "root";

        /// <summary>
        /// 密码
        /// </summary>
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; NotifyPropertyChanged(); } }
        private string _UserPwd = string.Empty;

        public ushort RegisterAddress{ get => _RegisterAddress; set { _RegisterAddress = value; NotifyPropertyChanged(); } }
        private ushort _RegisterAddress = 0xD0;
    }


    /// <summary>
    /// ModbusConnect.xaml 的交互逻辑
    /// </summary>
    public partial class ModbusConnect : Window
    {
        public ModbusConnect()
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
            if (string.IsNullOrEmpty(ModbusConfig.Name))
            {
                ModbusConfig.Name = ModbusConfig.Host +"_" +ModbusConfig.Port;
            }
            ModbusConfigs.Remove(ModbusConfig);
            ModbusControl.GetInstance().Connect();
            Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            ModbusConfigBackUp.CopyTo(ModbusConfig);
            Close();
        }


        public ModbusConfig ModbusConfig { get;set;}

        private ModbusConfig ModbusConfigBackUp { get; set; }

        public ObservableCollection<ModbusConfig> ModbusConfigs { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ModbusConfig= ModbusSetting.Instance.ModbusConfig;
            GridModbus.DataContext = ModbusConfig;
            ModbusConfigBackUp = new ModbusConfig();
            ModbusConfig.CopyTo(ModbusConfigBackUp);
            ModbusConfigs = ModbusSetting.Instance.ModbusConfigs;
            ListViewMySql.ItemsSource = ModbusConfigs;

            ModbusConfigs.Insert(0, ModbusConfig);
            ListViewMySql.SelectedIndex = 0;

            Closed += (s, e) =>
            {
                ModbusConfigs.Remove(ModbusConfig);
            };
        }

        private void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                bool IsConnect = ModbusControl.TestConnect(ModbusConfig);
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
                ModbusConfig = ModbusConfigs[listView.SelectedIndex];
                GridModbus.DataContext = ModbusConfig;
                ModbusSetting.Instance.ModbusConfig = ModbusConfig;
            }
        }


        private void MenuItem_Click_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ModbusConfig mySqlConfig)
            {
                ModbusConfigs.Remove(mySqlConfig);
            }
        }

        private void Button_Click_Copy(object sender, RoutedEventArgs e)
        {
            ModbusConfig mySqlConfig = new() { };
            mySqlConfig.Name = mySqlConfig.Name + "_1";

            ModbusConfig.CopyTo(mySqlConfig);
            ModbusConfigs.Add(mySqlConfig);
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            ModbusConfig newCfg = new();
            newCfg.Name = "New Profile";
            ModbusConfigs.Add(newCfg);
        }
    }
}
