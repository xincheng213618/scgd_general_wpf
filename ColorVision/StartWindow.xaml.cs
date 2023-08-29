using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ColorVision.MySql;
using ColorVision.MQTT;
using ColorVision.SettingUp;
using System.Reflection;

namespace ColorVision
{
    /// <summary>
    /// StartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class StartWindow : Window
    {
        public StartWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InitializeComponent();
            Left = SystemParameters.WorkArea.Right - this.Width;
            Top = SystemParameters.WorkArea.Bottom - this.Height;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            #if (DEBUG == true)
            labelVersion.Content = $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug) " : "(Release)")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) - {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
            #else
            labelVersion.Content = $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug)" : "")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd})";
            #endif
            Dispatcher.BeginInvoke(new Action(async () => await InitializedOver()));
        }
        private static bool DebugBuild(Assembly assembly)
        {
            foreach (object attribute in assembly.GetCustomAttributes(false))
            {
                if (attribute is DebuggableAttribute _attribute)
                {
                    return _attribute.IsJITTrackingEnabled;
                }
            }
            return false;
        }

        private async Task InitializedOver()
        {
            //检测服务连接情况，需要在界面启动之后，否则会出现问题。因为界面启动之后才会初始化MQTTControl和MySqlControl，所以代码上问题不大
            MQTTControl.GetInstance();
            MySqlControl.GetInstance();

            SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            TextBoxMsg.Text += Environment.NewLine + "检测服务连接情况";
            await Task.Delay(100);

            TextBoxMsg.Text += Environment.NewLine + "MQTT连接" + MQTTControl.GetInstance().IsConnect;
            TextBoxMsg.Text += Environment.NewLine + "MySql连接" + MySqlControl.GetInstance().IsConnect;

            if (!MySqlControl.GetInstance().IsConnect && SoftwareConfig.IsUseMySql)
            {
                MySqlConnect mySqlConnect = new MySqlConnect() { Owner = this };
                mySqlConnect.ShowDialog();
            }
            if (!MQTTControl.GetInstance().IsConnect && SoftwareConfig.IsUseMQTT)
            {
                MQTTConnect mQTTConnect = new MQTTConnect() { Owner = this };
                mQTTConnect.ShowDialog();
            }
            await Task.Delay(100);

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
        
        private void TextBoxMsg_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBoxMsg.ScrollToEnd();
        }
    }
}
