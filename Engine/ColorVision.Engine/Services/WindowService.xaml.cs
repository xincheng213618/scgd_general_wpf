using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Engine.Services
{
    public class ExportWindowService : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Properties.Resources.MenuService;
        public override int Order => 1;

        public override void Execute()
        {
            new WindowService() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
    public class WindowServiceConfig : ViewModelBase, IConfig
    {
        public static WindowServiceConfig Instance => ConfigService.Instance.GetRequiredService<WindowServiceConfig>();
        /// <summary>
        /// 获取用于编辑属性的命令
        /// </summary>
        public RelayCommand EditCommand { get; set; }
        public WindowServiceConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        public int ShowType2 { get; set; } = 2;

    }

    /// <summary>
    /// WindowService.xaml 的交互逻辑
    /// </summary>
    public partial class WindowService : Window
    {
        public WindowService()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        public WindowServiceConfig WindowServiceConfig { get; set; }

        private void Window_Initialized(object sender, EventArgs e)
        {
            WindowServiceConfig = WindowServiceConfig.Instance;
            this.DataContext = this;
            int i = WindowServiceConfig.Instance.ShowType2;
            switch (i % 3)
            {
                case 0:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TypeServices;
                    break;
                case 1:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TerminalServices;
                    break;
                case 2:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().DeviceServices;
                    break;
                default:
                    break;
            }
            ServicesHelper.SelectAndFocusFirstNode(TreeView1);
        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            StackPanelShow.Children.Clear();
            if (TreeView1.SelectedItem is DeviceService baseObject)
                StackPanelShow.Children.Add(baseObject.GetDeviceInfo());

            if (TreeView1.SelectedItem is TerminalServiceBase baseService)
                StackPanelShow.Children.Add(baseService.GenDeviceControl());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServiceManager.GetInstance().GenDeviceDisplayControl();
            Close();
        }

        private void TreeView1_Loaded(object sender, RoutedEventArgs e)
        {

        }


        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            WindowServiceConfig.Instance.ShowType2 = (WindowServiceConfig.Instance.ShowType2 +1) % 3;
            switch (WindowServiceConfig.Instance.ShowType2)
            {
                case 0:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TypeServices;
                    ServicesHelper.SelectAndFocusFirstNode(TreeView1);
                    break;
                case 1:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TerminalServices;
                    ServicesHelper.SelectAndFocusFirstNode(TreeView1);
                    break;
                case 2:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().DeviceServices;
                    ServicesHelper.SelectAndFocusFirstNode(TreeView1);
                    break;
                default:
                    break;
            }
        }

        private void ButtonLicenseManager_Click(object sender, RoutedEventArgs e)
        {
            new LicenseManagerWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void ButtonPhyCameraManager_Click(object sender, RoutedEventArgs e)
        {
            new PhyCameraManagerWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void ButtonArchiveManager_Click(object sender, RoutedEventArgs e)
        {
            var window = new Window
            {
                Title = Properties.Resources.Archive,
                Width = 800,
                Height = 600,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (System.Windows.Media.Brush)FindResource("GlobalBackground")
            };
            var frame = new System.Windows.Controls.Frame();
            window.Content = frame;
            frame.Navigate(new Archive.Dao.ArchivePage(frame));
            window.ShowDialog();
        }

        private void ButtonFlowManager_Click(object sender, RoutedEventArgs e)
        {
            new FlowEngineToolWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
