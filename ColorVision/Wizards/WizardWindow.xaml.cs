using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Wizards
{
    public class WizardConfig : ViewModelBase ,IConfig
    {
        public static WizardConfig Instance =>ConfigHandler.GetInstance().GetRequiredService<WizardConfig>();
        public bool WizardCompletionKey { get => _WizardCompletionKey; set { _WizardCompletionKey = value; NotifyPropertyChanged(); } }
        private bool _WizardCompletionKey;
    }

    /// <summary>
    /// WizardWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WizardWindow : Window
    {
        public WizardWindow()
        {
            InitializeComponent();
        }
        private void Window_Initialized(object sender, System.EventArgs e)
        {
            var IWizardSteps = new List<IWizardStep>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IWizardStep).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IWizardStep fileHandler)
                    {
                        IWizardSteps.Add(fileHandler);
                    }
                }
            }
            IWizardSteps = IWizardSteps.OrderBy(handler => handler.Order).ToList();


            foreach (var step in IWizardSteps)
            {
                Button button = new Button() { Content = step.Title, Command = step.RelayCommand };
                WizardStackPanel.Children.Add(button);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            WizardConfig.Instance.WizardCompletionKey = true;
            ConfigHandler.GetInstance().SaveConfigs();
            //这里使用件的启动路径，启动主程序
            Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"), "-r");
            Application.Current.Shutdown();
        }
    }
}
