using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public partial class StartWindow : Window, IMessageUpdater
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

            _IComponentInitializers = new List<UI.IInitializer>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IInitializer).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type,this) is IInitializer componentInitialize)
                    {
                        _IComponentInitializers.Add(componentInitialize);
                    }
                }
            }
            _IComponentInitializers = _IComponentInitializers.OrderBy(handler => handler.Order).ToList();
            Thread thread = new(async () => await InitializedOver()) { IsBackground =true};
            thread.Start();
        }
        private  List<IInitializer> _IComponentInitializers;


        public void UpdateMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextBoxMsg.Text += $"{Environment.NewLine}{message}";
            });
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
            UpdateMessage(ColorVision.Properties.Resources.StartingService);
            foreach (var initializer in _IComponentInitializers)
            {
                await initializer.InitializeAsync();
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    MainWindow mainWindow = new();
                    mainWindow.Show();
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("窗口创建错误:" + ex.Message);
                    Environment.Exit(-1);
                }
            });
        }

        private void TextBoxMsg_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBoxMsg.ScrollToEnd();
        }
    }
}
