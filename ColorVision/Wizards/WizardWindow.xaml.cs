using ColorVision.Common.MVVM;
using ColorVision.Projects;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Wizards
{
    public sealed class BooleanToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && (bool)value ? Brushes.Green : Brushes.Red;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }


    public class WizardManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectManager));
        private static WizardManager _instance;
        private static readonly object _locker = new();
        public static WizardManager GetInstance() { lock (_locker) { _instance ??= new WizardManager(); return _instance; } }
        public List<IWizardStep> IWizardSteps { get; private set; } = new List<IWizardStep>();

        public void Initialized()
        {
            IWizardSteps.Clear();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IWizardStep).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IWizardStep fileHandler)
                    {
                        log.Debug(type);
                        IWizardSteps.Add(fileHandler);
                    }
                }
            }
            IWizardSteps = IWizardSteps.OrderBy(handler => handler.Order).ToList();
        }



    }

        /// <summary>
        /// WizardWindow.xaml 的交互逻辑
        /// </summary>
        public partial class WizardWindow : Window
    {
        public static WizardWindowConfig WindowConfig => WizardWindowConfig.Instance;

        public WizardWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            WindowConfig.SetWindow(this);
            this.SizeChanged +=(s,e)=> WindowConfig.SetConfig(this);
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            WizardManager.GetInstance().Initialized();
            this.DataContext = WindowConfig;
            List<IWizardStep> IWizardSteps = WizardManager.GetInstance().IWizardSteps;

            ListWizard.ItemsSource = IWizardSteps;
            ListWizard.SelectionChanged += (s, e) =>
            {
                if (ListWizard.SelectedIndex == -1) return;
                BorderContent.DataContext = IWizardSteps[ListWizard.SelectedIndex];
            };
            if (IWizardSteps.Count > 0) ListWizard.SelectedIndex = 0;
        }

        private void ConfigurationComplete_Click(object sender, RoutedEventArgs e)
        {
            bool result = true;
            foreach (var item in WizardManager.GetInstance().IWizardSteps)
            {
                result = result && item.ConfigurationStatus;
            }

            WindowConfig.WizardCompletionKey = result;
            ConfigHandler.GetInstance().SaveConfigs();

            if (!result)
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "还有未完成的配置，是否跳过", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (Application.Current.MainWindow == this)
                    {
                        WindowConfig.WizardCompletionKey = true;
                        //这里使用件的启动路径，启动主程序
                        Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"));
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        this.Close();
                    }
                }
                return;
            }

            if (Application.Current.MainWindow == this)
            {
                //这里使用件的启动路径，启动主程序
                Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"));
                Application.Current.Shutdown();
            }
            else
            {
                this.Close();
            }
            //如果第一次启动需要以管理员权限启动
            //Tool.RestartAsAdmin();
        }

        private void ComboBoxWizardType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
