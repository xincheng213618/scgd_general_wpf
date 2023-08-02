using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
            await Task.Delay(500);

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
