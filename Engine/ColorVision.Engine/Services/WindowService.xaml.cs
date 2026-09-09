using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
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
            DetailHeader.Visibility = TreeView1.SelectedItem is DeviceService ? Visibility.Visible : Visibility.Collapsed;
            DetailTitle.Text = (TreeView1.SelectedItem as ServiceObjectBase)?.Name ?? Properties.Resources.WindowServiceTitle;
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
            WindowServiceConfig.Instance.ShowType2 = (WindowServiceConfig.Instance.ShowType2 + 1) % 3;
            ApplyServiceListMode();
        }

        private void ApplyServiceListMode()
        {
            int showType = ((WindowServiceConfig.Instance.ShowType2 % 3) + 3) % 3;
            WindowServiceConfig.Instance.ShowType2 = showType;
            StackPanelShow.Children.Clear();

            switch (showType)
            {
                case 0:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TypeServices;
                    ListModeText.Text = Properties.Resources.Type;
                    break;
                case 1:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TerminalServices;
                    ListModeText.Text = Properties.Resources.Terminal;
                    break;
                case 2:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().DeviceServices;
                    ListModeText.Text = Properties.Resources.Device;
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

    }
}
