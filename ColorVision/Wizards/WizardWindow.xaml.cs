using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Wizards
{
    public enum WizardShowType
    {
        List,
        Tile
    }

    public class WizardConfig : ViewModelBase, IConfig
    {
        public static WizardConfig Instance => ConfigService.Instance.GetRequiredService<WizardConfig>();
        public bool WizardCompletionKey { get => _WizardCompletionKey; set { _WizardCompletionKey = value; NotifyPropertyChanged(); } }
        private bool _WizardCompletionKey;

        public WizardShowType WizardShowType { get => _WizardShowType; set { _WizardShowType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsList)); } }
        private WizardShowType _WizardShowType;

        public bool IsList => WizardShowType == WizardShowType.List;
    }

    public class WizardWindowConfig:UI.WindowConfig { }

    /// <summary>
    /// WizardWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WizardWindow : Window
    {
        public static WizardWindowConfig WindowConfig => ConfigService.Instance.GetRequiredService<WizardWindowConfig>();

        public WizardWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            WindowConfig.SetWindow(this);
            this.SizeChanged +=(s,e)=> WindowConfig.SetConfig(this);
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            ComboBoxWizardType.ItemsSource = Enum.GetValues(typeof(WizardShowType)).Cast<WizardShowType>();
            this.DataContext = WizardConfig.Instance;

            var IWizardSteps = new List<IWizardStep>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
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
            ListWizard.ItemsSource = IWizardSteps;
            ListWizard.SelectionChanged += (s, e) =>
            {
                if (ListWizard.SelectedIndex == -1) return;
                BorderContent.DataContext = IWizardSteps[ListWizard.SelectedIndex];
            };
            if (IWizardSteps.Count > 0) ListWizard.SelectedIndex = 0;

            foreach (var step in IWizardSteps)
            {
                Border border = new Border() { Margin = new Thickness(5, 5, 5, 5) };
                border.Child = new Button() { Content = step.Header, Command = step.RelayCommand };
                WizardStackPanel.Children.Add(border);
            }


        }

        private void ConfigurationComplete_Click(object sender, RoutedEventArgs e)
        {
            WizardConfig.Instance.WizardCompletionKey = true;
            ConfigHandler.GetInstance().SaveConfigs();

            //这里使用件的启动路径，启动主程序
            Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"), "-r");
            Application.Current.Shutdown();

            //如果第一次启动需要以管理员权限启动
            //Tool.RestartAsAdmin();
        }

        private void ComboBoxWizardType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
