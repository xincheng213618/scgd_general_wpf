using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Shell;
using Dm.util;
using log4net;
using log4net.Layout;
using log4net.Repository.Hierarchy;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(StartWindow));
        public StartWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InitializeComponent();
            Left = SystemParameters.WorkArea.Right - Width;
            Top = SystemParameters.WorkArea.Bottom - Height;
        }
        TextBoxAppender TextBoxAppender { get; set; }
        Hierarchy Hierarchy { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            labelVersion.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString();


#if (DEBUG == true)
            string info= $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? "(Debug) " : "(Release)")}{(Debugger.IsAttached ? ColorVision.Properties.Resources.Debugging : "")} ({(IntPtr.Size == 4 ? "32" : "64")} {ColorVision.Properties.Resources.Bit} - {Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy.MM.dd}";
            #else
            string info= $"{(DebugBuild(Assembly.GetExecutingAssembly()) ? "(Debug)" : "")}{(Debugger.IsAttached ? ColorVision.Properties.Resources.Debugging : "")}{(IntPtr.Size == 4 ? "32" : "64")} {ColorVision.Properties.Resources.Bit} -  {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} - .NET Core {Environment.Version} Build {File.GetLastWriteTime(System.Windows.Forms.Application.ExecutablePath):yyyy/MM/dd}";
            #endif

            TextBoxMsg.Text = info;

            Hierarchy = (Hierarchy)LogManager.GetRepository();
            TextBoxAppender = new TextBoxAppender(TextBoxMsg, new TextBox(), 100);
            TextBoxAppender.Layout = new PatternLayout("%date{HH:mm:ss} %-5level %message%newline");
            Hierarchy.Root.AddAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);

            ThemeManager.Current.SystemThemeChanged += (e) => {
                Icon = new BitmapImage(new Uri($"pack://application:,,,/ColorVision;component/Assets/Image/{(e == Theme.Light ? "ColorVision.ico" : "ColorVision1.ico")}"));
            };
            if (ThemeManager.Current.SystemTheme == Theme.Dark)
                Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Assets/Image/ColorVision1.ico"));

            _IComponentInitializers = new List<IInitializer>();
            var parser = ArgumentParser.GetInstance();
            parser.AddArgument("skip", false, "skip");
            parser.Parse();
            string skipValue = parser.GetValue("skip");
            var skipNames = skipValue?.Split(',')
                         .Select(name => name.Trim())
                         .Where(name => !string.IsNullOrEmpty(name))
                         .ToList();

            skipNames = skipNames ?? new List<string>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IInitializer).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type, this) is IInitializer componentInitialize)
                        {
                            if (!skipNames.Contains(componentInitialize.Name))
                            {
                                _IComponentInitializers.Add(componentInitialize);
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                }
            }


            // 构建依赖图和入度表
            var dependencyGraph = new Dictionary<string, List<string>>();
            var inDegree = new Dictionary<string, int>();
            _IComponentInitializers.ForEach(init =>
            {
                dependencyGraph[init.Name] = init.Dependencies.ToList();
                inDegree[init.Name] = 0;
                foreach (var dep in init.Dependencies)
                {
                    if (!inDegree.TryGetValue(dep, out int value))
                    {
                        value = 0;
                        inDegree[dep] = value;
                    }
                    inDegree[dep] = ++value;
                }
            });

            // 拓扑排序
            var sortedInitializers = new List<IInitializer>();
            var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key).OrderBy(kvp => _IComponentInitializers.First(init => init.Name == kvp).Order));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                sortedInitializers.Add(_IComponentInitializers.First(init => init.Name == current));
                foreach (var neighbor in dependencyGraph[current])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // 检查是否有环
            if (sortedInitializers.Count != _IComponentInitializers.Count)
            {
                throw new Exception("Dependency cycle detected");
            }

            // 使用排序后的初始化器列表
            _IComponentInitializers = sortedInitializers;
            _IComponentInitializers = _IComponentInitializers.OrderBy(handler => handler.Order).ToList();
            Thread thread = new(async () => await InitializedOver()) { IsBackground =true};
            thread.Start();
        }
        private  List<IInitializer> _IComponentInitializers;


        public void Update(string message)
        {
            log.Info(message);
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
            Stopwatch stopwatch = new Stopwatch();
            foreach (var initializer in _IComponentInitializers)
            {
                stopwatch.Start();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    TextBoxMsg.Text += $"";
                });
                log.Info($"{Properties.Resources.Initializer} {initializer.GetType().Name}");
                try
                {
                    await initializer.InitializeAsync();
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                stopwatch.Stop();
                log.Info($"Initializer {initializer.GetType().Name} took {stopwatch.ElapsedMilliseconds} ms.");
                stopwatch.Reset();
            }
            Hierarchy.Root.RemoveAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var parser = ArgumentParser.GetInstance();
                    parser.AddArgument("feature", false, "e");
                    parser.Parse();

                    string feature = parser.GetValue("feature");
                    if (feature != null)
                    {
                        List<IFeatureLauncher> IFeatureLaunchers = new List<IFeatureLauncher>();
                        foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                        {
                            foreach (Type type in assembly.GetTypes().Where(t => typeof(IFeatureLauncher).IsAssignableFrom(t) && !t.IsAbstract))
                            {
                                if (Activator.CreateInstance(type) is IFeatureLauncher projects)
                                {
                                    IFeatureLaunchers.Add(projects);
                                }
                            }
                        }
                        if (IFeatureLaunchers.Find(a => a.Header == feature) is IFeatureLauncher project1)
                        {
                            project1.Execute();
                        }
                        else if (IFeatureLaunchers.Find(a => a.GetType().ToString().contains(feature)) is IFeatureLauncher project2)
                        {
                            project2.Execute();
                        }

                    }
                    else
                    {
                        MainWindow mainWindow = new MainWindow();
                        mainWindow.Show();
                    }
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("MainWindow Create Error:" + ex.Message);
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
