using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Engine.Services
{
    public class ExportWindowService : MenuItemBase
    {
        public override string OwnerGuid => "Tool";
        public override string GuidId => "WindowService";
        public override string Header => Properties.Resources.MenuService;
        public override int Order => 3;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new WindowService() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
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
        private void Window_Initialized(object sender, EventArgs e)
        {
            int i = ServicesConfig.Instance.ShowType;
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


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MqttRCService.GetInstance().RestartServices();
            MessageBox.Show(Application.Current.MainWindow,"命令已经发送","ColorVision");
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            ServicesConfig.Instance.ShowType = (ServicesConfig.Instance.ShowType +1) % 3;
            switch (ServicesConfig.Instance.ShowType)
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
    }
}
