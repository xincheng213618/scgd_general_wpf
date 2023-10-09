using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ColorVision.MySql;
using ColorVision.MQTT;
using System.Reflection;
using ColorVision.Services;

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
            labelVersion.Text = $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug) " : "(Release)")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) - {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
#else
            labelVersion.Text = $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug)" : "")}{(Debugger.IsAttached ? " (调试中) " : "")} {(IntPtr.Size == 4 ? "32" : "64")}位) -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd}";
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

            TextBoxMsg.Text += $"{Environment.NewLine}Mysql服务连接: {(MySqlControl.GetInstance().IsConnect ? "成功" : "失败")}";
            if (!MySqlControl.GetInstance().IsConnect && SoftwareConfig.IsUseMySql)
            {
                MySqlConnect mySqlConnect = new MySqlConnect() { Owner = this };
                mySqlConnect.ShowDialog();
            }
            TextBoxMsg.Text += $"{Environment.NewLine}MQTT服务连接: {(MQTTControl.GetInstance().IsConnect ? "成功" : "失败")}";
            if (!MQTTControl.GetInstance().IsConnect && SoftwareConfig.IsUseMQTT)
            {
                MQTTConnect mQTTConnect = new MQTTConnect() { Owner = this };
                mQTTConnect.ShowDialog();
                TextBoxMsg.Text += $"{Environment.NewLine}MQTT服务连接: {(MQTTControl.GetInstance().IsConnect ? "成功" : "失败")}";
            }
            ServiceControl.GetInstance().RCRegist();
            await Task.Delay(100);
            TextBoxMsg.Text += Environment.NewLine + "初始化服务" + MySqlControl.GetInstance().IsConnect;
            try
            {
                if (!GlobalSetting.GetInstance().SoftwareConfig.SoftwareSetting.IsDeFaultOpenService)
                    new WindowDevices() { Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                else
                    ServiceControl.GetInstance().GenContorl();
            }
            catch
            {
                MessageBox.Show("窗口创建错误");
                Environment.Exit(-1);
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
