#pragma warning disable CA1863,CS8604
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Terminal
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTerminal : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CreateTerminal));
        private readonly TerminalConfigurationTarget configurationTarget;

        public TerminalService TerminalService { get; set; }
        public CreateTerminal(TerminalService terminalService)
        {
            TerminalService = terminalService;
            configurationTarget = terminalService.CaptureConfigurationTarget();
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CreateCode.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");
            var devices = ServiceManager.Current?.DeviceServices.AsEnumerable() ?? TerminalService.VisualChildren.OfType<DeviceService>();
            CreateName.Text = SuggestDeviceName(TerminalService.ServiceType, devices.Select(device => device.Name));
        }

        internal static string SuggestDeviceName(ServiceTypes serviceType, IEnumerable<string> existingNames)
        {
            var usedNames = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            string prefix = serviceType.ToString();
            string name = prefix;
            for (int suffix = 1; usedNames.Contains(name); suffix++)
                name = prefix + suffix.ToString(CultureInfo.InvariantCulture);

            return name;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var terminal = TerminalService;
            if (!configurationTarget.IsCurrent(terminal))
            {
                MessageBox1.Show(this, EngineLocalization.Get("终端或配置来源已变更，请重新打开创建设备窗口。"), "ColorVision");
                return;
            }
            if (!ServiceAuthorization.Can("device.create", terminal.SysResourceModel))
            {
                MessageBox1.Show(this, EngineLocalization.Get("当前权限不足，无法创建设备。"), "ColorVision");
                return;
            }

            string code = CreateCode.Text;
            var serviceType = terminal.ServiceType;
            if (ServiceManager.Current?.DeviceServices.Any(device => !device.IsConfigurationRetired && device.Code == code && HasSameSource(device)) == true)
            {
                MessageBox1.Show(this, Properties.Resources.DeviceIdentifierAlreadyExists, "ColorVision");
                return;
            }

            if (!DeviceServiceFactoryRegistry.TryGetFactory(serviceType, out IDeviceServiceFactory? factory) || factory == null)
            {
                MessageBox1.Show(this, string.Format(Properties.Resources.UnsupportedDeviceTypeCreation, serviceType), "ColorVision");
                return;
            }

            SysResourceModel? resource;
            try
            {
                ServiceAuthorization.Demand("device.create", terminal.SysResourceModel);
                if (!TerminalDeviceCreation.TryPersist(terminal, configurationTarget, factory, code, CreateName.Text, out resource))
                {
                    MessageBox1.Show(this, EngineLocalization.Get("设备未创建。请确认终端和配置来源仍有效，且设备标识可用。"), "ColorVision");
                    return;
                }
            }
            catch (Exception error)
            {
                log.Warn("Could not persist the new device configuration.", error);
                MessageBox1.Show(this, EngineLocalization.Get("设备配置保存失败，请刷新服务列表确认后重试。"), "ColorVision");
                return;
            }

            try
            {
                RegisterCreatedDevice(terminal, factory, serviceType, resource);
            }
            catch (Exception error)
            {
                log.Warn("The device was saved, but runtime registration did not complete.", error);
                MessageBox1.Show(this, EngineLocalization.Get("设备已保存到原配置库，请从该配置库刷新服务列表以显示新设备。"), "ColorVision");
            }
            Close();
        }

        private bool HasSameSource(DeviceService device) => configurationTarget.Endpoint is { } endpoint
            ? ReferenceEquals(device.ResultDatabaseEndpoint, endpoint)
            : ReferenceEquals(device.LocalConfigurationResourceDao, configurationTarget.LocalResources);

        private void RegisterCreatedDevice(TerminalService terminal, IDeviceServiceFactory factory, ServiceTypes serviceType, SysResourceModel resource)
        {
            var target = configurationTarget;
            if (!target.IsCurrent(terminal))
                throw new InvalidOperationException("Terminal configuration changed before device registration.");

            var displayConfigs = DisplayConfigManager.Instance;
            string code = resource.Code!;
            bool displayInitialized = DeviceServiceFactoryRegistry.TryInitializeNewDeviceDisplayConfig(serviceType, code, displayConfigs);
            var device = factory.CreateService(resource);
            device.ResultDatabaseEndpoint = target.Endpoint;
            device.LocalConfigurationResourceDao = target.LocalResources;
            var manager = ServiceManager.GetInstance();
            if (!target.IsCurrent(terminal))
            {
                device.Dispose();
                throw new InvalidOperationException("Terminal configuration changed during device construction.");
            }

            terminal.AddChild(device);
            manager.DeviceServices.Add(device);
            manager.GenDeviceDisplayControl();
            if (displayInitialized && !ConfigHandler.GetInstance().TrySave(displayConfigs, out string displayError))
                MessageBox1.Show(this, EngineLocalization.Format($"设备已创建，但本机显示配置保存失败：{displayError}"), "ColorVision");

            if (target.ReadRemoteRoute(terminal) is { } route)
            {
                var deviceTarget = device.CaptureConfigurationTarget();
                _ = MqttRCService.GetInstance().TryRestartServices(route.TypeCode, route.ServiceCode, code,
                    () => target.IsCurrent(terminal) && deviceTarget.IsCurrent(device) && ReferenceEquals(device.Parent, terminal));
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
