using ColorVision.Common.MVVM;
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

namespace  ColorVision.UI.Desktop.Wizards
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
        private static readonly ILog log = LogManager.GetLogger(typeof(WizardManager));
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
        private int _currentStepIndex = 0;
        private List<IWizardStep> _wizardSteps;

        public WizardWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            WindowConfig.SetWindow(this);
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            WizardManager.GetInstance().Initialized();
            this.DataContext = WindowConfig;
            _wizardSteps = WizardManager.GetInstance().IWizardSteps;

            ListWizard.ItemsSource = _wizardSteps;
            
            if (_wizardSteps.Count > 0)
            {
                _currentStepIndex = 0;
                ShowCurrentStep();
            }
        }

        private void ShowCurrentStep()
        {
            if (_wizardSteps == null || _wizardSteps.Count == 0) return;

            // Update progress bar
            WizardProgress.Value = (_currentStepIndex + 1) * 100.0 / _wizardSteps.Count;

            // Show current step content
            BorderContent.DataContext = _wizardSteps[_currentStepIndex];
            
            // Update ListView selection to show which step we're on
            ListWizard.SelectedIndex = _currentStepIndex;

            // Update button states
            BtnPrevious.IsEnabled = _currentStepIndex > 0;
            
            if (_currentStepIndex < _wizardSteps.Count - 1)
            {
                // Not on last step - show Next button
                BtnNext.Visibility = Visibility.Visible;
                BtnFinish.Visibility = Visibility.Collapsed;
            }
            else
            {
                // On last step - show Finish button
                BtnNext.Visibility = Visibility.Collapsed;
                BtnFinish.Visibility = Visibility.Visible;
            }
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStepIndex > 0)
            {
                _currentStepIndex--;
                ShowCurrentStep();
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStepIndex < _wizardSteps.Count - 1)
            {
                _currentStepIndex++;
                ShowCurrentStep();
            }
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
                if (MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.SkipIncompleteConfigPrompt, "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (Application.Current.MainWindow == this)
                    {
                        WindowConfig.WizardCompletionKey = true;
                        ConfigHandler.GetInstance().SaveConfigs();  
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
