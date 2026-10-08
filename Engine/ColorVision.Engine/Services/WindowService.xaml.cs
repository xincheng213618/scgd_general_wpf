using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhySpectrums;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;

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
        [Browsable(false)]
        public RelayCommand EditCommand { get; set; }
        public WindowServiceConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        // Preserve the persisted service/device values; the former type view now opens services.
        public int ShowType2 { get; set; } = 1;

        internal void NormalizeListMode() => ShowType2 = ShowType2 == 2 ? 2 : 1;

        internal void ToggleListMode() => ShowType2 = ShowType2 == 2 ? 1 : 2;

    }

    /// <summary>
    /// WindowService.xaml 的交互逻辑
    /// </summary>
    public partial class WindowService : Window
    {
        private string? _copilotContextSourceId;
        private List<(ServiceObjectBase Service, string Configuration)>? _initialConfiguration;

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
            ApplyServiceListMode();
            _initialConfiguration = CaptureConfiguration(ServiceManager.GetInstance().TypeServices);
        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            StackPanelShow.Children.Clear();
            DetailHeader.DataContext = TreeView1.SelectedItem;
            DetailHeader.Visibility = TreeView1.SelectedItem is DeviceService or TerminalService ? Visibility.Visible : Visibility.Collapsed;
            RenameServiceButton.Visibility = TreeView1.SelectedItem is TerminalService ? Visibility.Visible : Visibility.Collapsed;
            DetailSubtitle.Text = TreeView1.SelectedItem switch
            {
                DeviceService device => device.Code,
                TerminalService terminal => terminal.Code,
                _ => string.Empty
            };
            DetailSubtitle.Visibility = string.IsNullOrEmpty(DetailSubtitle.Text) ? Visibility.Collapsed : Visibility.Visible;
            if (TreeView1.SelectedItem is DeviceService baseObject)
            {
                StackPanelShow.Children.Add(baseObject.GetDeviceInfo());
                PublishCopilotDeviceContext(baseObject);
            }
            else
            {
                ClearCopilotDeviceContext();
            }

            if (TreeView1.SelectedItem is TerminalServiceBase baseService)
                StackPanelShow.Children.Add(baseService.GenDeviceControl());
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (TreeView1.SelectedItem is DeviceService device)
                PublishCopilotDeviceContext(device);
            else
                ClearCopilotDeviceContext();
        }

        protected override void OnClosed(EventArgs e)
        {
            ClearCopilotDeviceContext();
            ServiceManager.GetInstance().PublishCurrentCopilotDisplayContext();
            base.OnClosed(e);
        }

        private void PublishCopilotDeviceContext(DeviceService device)
        {
            try
            {
                var bundle = device.CaptureCopilotContext();
                CopilotBusinessContextCoordinator.Publish(bundle);
                _copilotContextSourceId = bundle.SourceId;
            }
            catch
            {
                ClearCopilotDeviceContext();
            }
        }

        private void ClearCopilotDeviceContext()
        {
            var sourceId = _copilotContextSourceId;
            _copilotContextSourceId = null;
            if (!string.IsNullOrWhiteSpace(sourceId))
                CopilotLiveContextRegistry.Clear(sourceId);
        }

        // Compare only configuration and resource identity, never heartbeat/selection/runtime status.
        internal static List<(ServiceObjectBase Service, string Configuration)> CaptureConfiguration(IEnumerable<Types.TypeService> types)
        {
            var result = new List<(ServiceObjectBase, string)>();
            foreach (var type in types)
            {
                result.Add((type, type.Name));
                foreach (var terminal in type.VisualChildren.OfType<TerminalService>())
                {
                    result.Add((terminal, JsonConvert.SerializeObject(terminal.Config)));
                    foreach (var device in terminal.VisualChildren.OfType<DeviceService>())
                        result.Add((device, JsonConvert.SerializeObject(device.GetConfig())));
                }
            }
            return result;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel || _initialConfiguration == null) return;
            try
            {
                var current = CaptureConfiguration(ServiceManager.GetInstance().TypeServices);
                if (!_initialConfiguration.SequenceEqual(current))
                    ServiceManager.GetInstance().GenDeviceDisplayControl();
                _initialConfiguration = current;
            }
            catch (Exception exception)
            {
                e.Cancel = true;
                MessageBox.Show(this, exception.Message, Properties.Resources.MenuService, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ButtonToggleList_Click(object sender, RoutedEventArgs e)
        {
            WindowServiceConfig.Instance.ToggleListMode();
            ApplyServiceListMode();
        }

        private void ApplyServiceListMode()
        {
            WindowServiceConfig.Instance.NormalizeListMode();
            StackPanelShow.Children.Clear();

            switch (WindowServiceConfig.Instance.ShowType2)
            {
                case 1:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TerminalServices;
                    ListModeText.Text = Properties.Resources.ServiceConfigurations;
                    break;
                default:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().DeviceServices;
                    ListModeText.Text = Properties.Resources.Device;
                    break;
            }

            ServicesHelper.SelectAndFocusFirstNode(TreeView1);
        }

        private void ButtonPhyCameraManager_Click(object sender, RoutedEventArgs e)
        {
            new PhyCameraManagerWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void ButtonCreateDevice_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }

        private void CreateDeviceMenu_Opened(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender;
            if (!AccessControl.Check(PermissionMode.Administrator))
            {
                menu.IsOpen = false;
                return;
            }
            menu.Items.Clear();
            PopulateCreateDeviceMenu(menu, ServiceManager.GetInstance().TypeServices, CreateServiceAndDevice);
        }

        private void CreateServiceAndDevice(Types.TypeService type)
        {
            if (!AccessControl.Check(PermissionMode.Administrator)) return;
            var dialog = new Types.CreateType(type) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            dialog.ShowDialog();
            if (dialog.CreatedTerminal is TerminalService terminal && terminal.OpenCreateWindowCommand.CanExecute(null))
                terminal.OpenCreateWindowCommand.Execute(null);
        }

        internal static ContextMenu BuildCreateDeviceMenu(IEnumerable<Types.TypeService> types, Action<Types.TypeService> createService)
        {
            var menu = new ContextMenu();
            PopulateCreateDeviceMenu(menu, types, createService);
            return menu;
        }

        private static void PopulateCreateDeviceMenu(ContextMenu menu, IEnumerable<Types.TypeService> types, Action<Types.TypeService> createService)
        {
            foreach (var type in types)
            {
                if (!DeviceServiceFactoryRegistry.TryGetFactory(type.ServiceTypes, out _))
                    continue;
                var typeItem = new MenuItem
                {
                    Header = Properties.Resources.ResourceManager.GetString($"ServiceType_{type.ServiceTypes}", Properties.Resources.Culture) ?? type.Name
                };
                foreach (var terminal in type.VisualChildren.OfType<TerminalService>())
                {
                    var item = new MenuItem { Header = terminal.Name, Command = terminal.OpenCreateWindowCommand };
                    typeItem.Items.Add(item);
                }
                if (typeItem.Items.Count > 0) typeItem.Items.Add(new Separator());
                var createTerminal = new MenuItem { Header = Properties.Resources.CreateServiceAndDevice };
                createTerminal.Click += (_, _) => createService(type);
                typeItem.Items.Add(createTerminal);
                menu.Items.Add(typeItem);
            }
        }

        private void ButtonPhySpectrumManager_Click(object sender, RoutedEventArgs e)
        {
            var device = TreeView1.SelectedItem as DeviceSpectrum;
            int port = device != null && int.TryParse(device.Config.ComPort, out int parsed) ? parsed : 0;
            new PhySpectrumManagerWindow(device?.Config.SN, port) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
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
            window.ApplyCaption();
            var frame = new System.Windows.Controls.Frame();
            window.Content = frame;
            frame.Navigate(new Archive.Dao.ArchivePage(frame));
            window.ShowDialog();
        }

    }
}
