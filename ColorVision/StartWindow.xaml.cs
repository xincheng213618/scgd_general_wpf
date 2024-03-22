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
using ColorVision.RC;
using System.Threading;
using ColorVision.Themes;
using System.Windows.Media.Imaging;
using System.Runtime.Versioning;
using ColorVision.Settings;

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
            labelVersion.Text = $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug) " : "(Release)")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) - {Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
#else
            labelVersion.Text = $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug)" : "")}{(Debugger.IsAttached ? " (调试中) " : "")} {(IntPtr.Size == 4 ? "32" : "64")}位 -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd}";
#endif

            ThemeManager.Current.SystemThemeChanged += (e) => {
                this.Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Assets/Image/{(e == Theme.Light ? "ColorVision.ico" : "ColorVision1.ico")}"));
            };
            if (ThemeManager.Current.SystemTheme == Theme.Dark)
                this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Assets/Image/ColorVision1.ico"));

            MQTTControl.GetInstance();
            MySqlControl.GetInstance();
            Thread thread = new Thread(async () => await InitializedOver()) { IsBackground =true};
            thread.Start();

        }
        public static string? GetTargetFrameworkVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var targetFrameworkAttribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            return targetFrameworkAttribute?.FrameworkName;
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
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;

            Application.Current.Dispatcher.Invoke(() =>
            {
                TextBoxMsg.Text += $"正在启动服务";
            });

            if (SoftwareConfig.IsUseMySql)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextBoxMsg.Text += $"{Environment.NewLine}正在检测MySQL数据库连接情况";
                });
                bool IsConnect = await MySqlControl.GetInstance().Connect();


                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextBoxMsg.Text += $"{Environment.NewLine}MySQL数据库连接{(MySqlControl.GetInstance().IsConnect ? Properties.Resource.Success : Properties.Resource.Failure)}";
                    if (!IsConnect)
                    {
                        MySqlConnect mySqlConnect = new MySqlConnect() { Owner = this };
                        mySqlConnect.ShowDialog();
                    }
                });

            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}已经跳过数据库连接"; });
                await Task.Delay(100);

            }
            if (SoftwareConfig.IsUseMQTT)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextBoxMsg.Text += $"{Environment.NewLine}正在检测MQTT服务器连接情况";
                });

                bool IsConnect = await MQTTControl.GetInstance().Connect();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextBoxMsg.Text += $"{Environment.NewLine}MQTT服务器连接{(MQTTControl.GetInstance().IsConnect ? Properties.Resource.Success : Properties.Resource.Failure)}";
                    if (!IsConnect)
                    {
                        MQTTConnect mQTTConnect = new MQTTConnect() { Owner = this };
                        mQTTConnect.ShowDialog();
                    }
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}已经跳过MQTT服务器连接"; });
                await Task.Delay(100);
            }

            if (MQTTControl.GetInstance().IsConnect)
            {
                if (SoftwareConfig.IsUseRCService)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TextBoxMsg.Text += $"{Environment.NewLine}正在检测注册中心连接情况";
                    });
                    bool IsConnect = await MQTTRCService.GetInstance().Connect();
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        TextBoxMsg.Text += $"{Environment.NewLine}注册中心: {(IsConnect ? Properties.Resource.Success : Properties.Resource.Failure)}";
                        if (!IsConnect)
                        {
                            RCServiceConnect rcServiceConnect = new RCServiceConnect() { Owner = this };
                            rcServiceConnect.ShowDialog();
                        }
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}已经跳过注册中心服务器连接"; });
                    await Task.Delay(100);
                }
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}注册中心需要MQTT连接成功，已经跳过注册中心服务器连接"; });
                await Task.Delay(200);
            }

            await Task.Delay(100);
            Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}正在加载工程"; });
            await Task.Delay(200);
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow mainWindow = new MainWindow();
                ServiceManager ServiceManager = ServiceManager.GetInstance();
                try
                {
                    if (!ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.IsDefaultOpenService)
                    {
                        TextBoxMsg.Text += $"{Environment.NewLine}初始化服务";
                        ServiceManager.GenDeviceDisplayControl();
                        new WindowDevices() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                    }
                    else
                    {
                        TextBoxMsg.Text += $"{Environment.NewLine}自动配置服务中";
                        ServiceManager.GenDeviceDisplayControl();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("窗口创建错误:" + ex.Message);
                    Environment.Exit(-1);
                }
                mainWindow.Show();
                this.Close();
            });
        }
        
        private void TextBoxMsg_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBoxMsg.ScrollToEnd();
        }
    }
}
