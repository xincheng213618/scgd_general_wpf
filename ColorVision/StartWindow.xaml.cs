using ColorVision.MQTT;
using ColorVision.MySql;
using ColorVision.Services;
using ColorVision.Services.RC;
using ColorVision.Services.Templates;
using ColorVision.Solution;
using ColorVision.Themes;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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
            Left = SystemParameters.WorkArea.Right - Width;
            Top = SystemParameters.WorkArea.Bottom - Height;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            #if (DEBUG == true)
            labelVersion.Text = $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug) " : "(Release)")}{(Debugger.IsAttached ? " (调试中) " : "")} ({(IntPtr.Size == 4 ? "32" : "64")}位) - {Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
#else
            labelVersion.Text = $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? " (Debug)" : "")}{(Debugger.IsAttached ? " (调试中) " : "")} {(IntPtr.Size == 4 ? "32" : "64")}位 -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd}";
#endif

            ThemeManager.Current.SystemThemeChanged += (e) => {
                Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Assets/Image/{(e == Theme.Light ? "ColorVision.ico" : "ColorVision1.ico")}"));
            };
            if (ThemeManager.Current.SystemTheme == Theme.Dark)
                Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Assets/Image/ColorVision1.ico"));

            MQTTControl.GetInstance();
            MySqlControl.GetInstance();
            Thread thread = new(async () => await InitializedOver()) { IsBackground =true};
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextBoxMsg.Text += ColorVision.Properties.Resource.StartingService;
            });

            if (MySqlSetting.Instance.IsUseMySql)
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
                        MySqlConnect mySqlConnect = new() { Owner = this };
                        mySqlConnect.ShowDialog();
                    }
                });

            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}已经跳过数据库连接"; });
                await Task.Delay(10);

            }

            if (MQTTSetting.Instance.IsUseMQTT)
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
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            TextBoxMsg.Text += $"{Environment.NewLine}检测是否本地服务";
                        });

                        if (!RCManager.GetInstance().IsLocalServiceRunning())
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                TextBoxMsg.Text += $"{Environment.NewLine}打开本地服务管理";
                            });
                            if (RCManagerConfig.Instance.IsOpenCVWinSMS)
                            {
                                RCManager.GetInstance().OpenCVWinSMS();
                            }
                        }
                        RCManager.GetInstance();
                        MQTTConnect mQTTConnect = new() { Owner = this };
                        mQTTConnect.ShowDialog();
                    }
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}已经跳过MQTT服务器连接"; });
                await Task.Delay(10);
            }

            Application.Current.Dispatcher.Invoke(() => TemplateControl.GetInstance());

            if (MQTTControl.GetInstance().IsConnect)
            {
                if (RCSetting.Instance.IsUseRCService)
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
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                TextBoxMsg.Text += $"{Environment.NewLine}检测是否本地服务";
                            });

                            if (!RCManager.GetInstance().IsLocalServiceRunning())
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    TextBoxMsg.Text += $"{Environment.NewLine}打开本地服务管理";
                                });
                                RCManager.GetInstance().OpenCVWinSMS();
                            }

                            RCServiceConnect rcServiceConnect = new() { Owner = this };
                            rcServiceConnect.ShowDialog();
                        }
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}已经跳过注册中心服务器连接"; });
                    await Task.Delay(10);
                }
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}注册中心需要MQTT连接成功，已经跳过注册中心服务器连接"; });
                await Task.Delay(20);
            }

            await Task.Delay(10);
            Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}正在加载工程"; });
            Application.Current.Dispatcher.Invoke(() => SolutionManager.GetInstance());
            await Task.Delay(10);
            Application.Current.Dispatcher.Invoke(() => { TextBoxMsg.Text += $"{Environment.NewLine}正在打开主窗口"; });
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow mainWindow = new();
                ServiceManager ServiceManager = ServiceManager.GetInstance();
                if (MySqlControl.GetInstance().IsConnect)
                {
                    try
                    {
                        if (!ServicesConfig.Instance.IsDefaultOpenService)
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
                }
                else
                {
                    TextBoxMsg.Text += $"{Environment.NewLine}数据库连接失败，跳过服务配置";
                }
                mainWindow.Show();
                Close();
            });
        }
        
        private void TextBoxMsg_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBoxMsg.ScrollToEnd();
        }
    }
}
