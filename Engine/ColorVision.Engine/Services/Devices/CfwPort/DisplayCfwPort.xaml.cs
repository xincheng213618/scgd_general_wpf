#pragma warning disable CA1816
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    /// <summary>
    /// DisplayCfwPort.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCfwPort : UserControl, IDisPlayControl, IDisposable
    {

        public DeviceCfwPort Device { get; set; }
        private MQTTCfwPort DService { get => Device.DService; }
        private DeviceCfwPort? subscribedDevice;
        private MQTTCfwPort? subscribedService;
        private bool disposed;
        private bool portCommandPending;
        private bool refreshingPortMappings;

        public string DisPlayName => Device.Config.Code;
        public string PersistenceKey => Device.Config.Code;

        public DisplayCfwPort(DeviceCfwPort device)
        {
            Device = device;
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            this.ApplyChangedSelectedColor(DisPlayBorder);
            RefreshPortMappings();
            subscribedDevice = Device;
            subscribedService = DService;
            subscribedDevice.ConfigChanged += Device_ConfigChanged;
            subscribedDevice.PropertyChanged += Device_PropertyChanged;

            DService_DeviceStatusChanged(sender, DService.DeviceStatus);
            subscribedService.DeviceStatusChanged += DService_DeviceStatusChanged;
            Loaded += Display_Loaded;
        }

        private void Display_Loaded(object sender, RoutedEventArgs e)
        {
            if (Device.TryBeginLocalAutoConnect()) RunCommand(DService.Open);
        }

        private void Device_ConfigChanged(object? sender, EventArgs e) => RefreshPortMappings();

        private void RefreshPortMappings()
        {
            refreshingPortMappings = true;
            try
            {
                CombPort.ItemsSource = Device.FilterWheelConfig.HoleMapping;
                CombPort.SelectedValue = Device.DisplayConfig.TargetPortNum ?? Device.ConfirmedPort;
            }
            finally { refreshingPortMappings = false; }
            RefreshCurrentPosition();
        }

        private void TargetHole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (refreshingPortMappings) return;
            if (CombPort.SelectedValue is int port) Device.DisplayConfig.TargetPortNum = port;
            RefreshPositionActions();
        }

        private void Device_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DeviceCfwPort.ConfirmedPort)) return;
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => Device_PropertyChanged(sender, e)); return; }
            if (!disposed && ReferenceEquals(sender, Device)) RefreshCurrentPosition();
        }

        private void RefreshCurrentPosition()
        {
            CurrentHoleText.Text = Device.ConfirmedPort is int port
                ? Device.FilterWheelConfig.HoleMapping.FirstOrDefault(hole => hole.HoleIndex == port)?.HoleName
                    ?? port.ToString(CultureInfo.CurrentCulture)
                : "—";
            if (CombPort.SelectedItem == null && Device.ConfirmedPort is int current)
            {
                refreshingPortMappings = true;
                try { CombPort.SelectedValue = current; }
                finally { refreshingPortMappings = false; }
            }
            RefreshPositionActions();
        }

        private void RefreshPositionActions()
        {
            var status = DService.DeviceStatus;
            PositionActions.IsEnabled = !portCommandPending && status != DeviceStatusType.Busy && DeviceCfwPort.HasOpenConnection(status);
            SwitchPositionButton.IsEnabled = CombPort.SelectedValue is int port && Device.ConfirmedPort != port;
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => DService_DeviceStatusChanged(sender, e)); return; }
            if (disposed || !ReferenceEquals(Device, subscribedDevice) || !ReferenceEquals(DService, subscribedService)) return;
            OpeningPositionText.Visibility = e == DeviceStatusType.Opening && Device.CfwBackend.OpensLocally
                ? Visibility.Visible : Visibility.Collapsed;
            RefreshPositionActions();
            ButtonClose.IsEnabled = e != DeviceStatusType.Closing;
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
            void HideAllButtons()
            {
                SetVisibility(ButtonOpen, Visibility.Collapsed);
                SetVisibility(ButtonClose, Visibility.Collapsed);
                SetVisibility(ButtonInit, Visibility.Collapsed);
                SetVisibility(ButtonOffline, Visibility.Collapsed);
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(PositionActions, Visibility.Collapsed);
            }
            HideAllButtons();

            switch (e)
            {
                case DeviceStatusType.Unauthorized:
                    SetVisibility(ButtonUnauthorized, Visibility.Visible);
                    break;
                case DeviceStatusType.Unknown:
                    SetVisibility(TextBlockUnknow, Visibility.Visible);
                    break;
                case DeviceStatusType.OffLine:
                    SetVisibility(ButtonOffline, Visibility.Visible);
                    break;
                case DeviceStatusType.UnInit:
                    SetVisibility(ButtonInit, Visibility.Visible);
                    break;
                case DeviceStatusType.Closing:
                    SetVisibility(ButtonClose, Visibility.Visible);
                    break;
                case DeviceStatusType.Closed:
                    SetVisibility(ButtonOpen, Visibility.Visible);
                    break;
                case DeviceStatusType.LiveOpened:
                case DeviceStatusType.Opening:
                case DeviceStatusType.Opened:
                case DeviceStatusType.Busy:
                case DeviceStatusType.Free:
                    SetVisibility(PositionActions, DeviceCfwPort.HasOpenConnection(e) ? Visibility.Visible : Visibility.Collapsed);
                    SetVisibility(ButtonClose, Visibility.Visible);
                    break;
                default:
                    break;
            }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void Open_Click(object sender, RoutedEventArgs e) => RunCommand(DService.Open);
        private void Close_Click(object sender, RoutedEventArgs e) => RunCommand(DService.Clsoe);

        private void SetPort_Click(object sender, RoutedEventArgs e)
        {
            if (CombPort.SelectedValue is int port && Device.ConfirmedPort != port) RunPortCommand(port);
        }

        private void CameraOffline_Click(object sender, RoutedEventArgs e) => RunCommand(DService.Open);

        private void GetPort_Click(object sender, RoutedEventArgs e) => RunPortCommand(null);

        private void RunPortCommand(int? requestedPort)
        {
            if (!PositionActions.IsEnabled) return;
            DeviceCfwPort wheel = Device;
            MQTTCfwPort service = wheel.DService;
            Func<bool> wheelOwnsCommand = wheel.CapturePortCommandOwnership(service);
            ServiceManager? manager = ServiceManager.Current;
            string wheelCode = wheel.Code;
            var recipients = manager?.DeviceServices.OfType<DeviceSpectrum>()
                .Where(spectrum => string.Equals(spectrum.Config.NDConfig.NDBindDeviceCode, wheelCode, StringComparison.Ordinal))
                .Select(spectrum => (Spectrum: spectrum, Target: spectrum.CaptureConfigurationTarget(), Binding: spectrum.Config.NDConfig,
                    Shared: spectrum.Config.NDConfig.IsBingNDDevice))
                .ToArray() ?? [];

            bool OwnsWheel() => !disposed && ReferenceEquals(wheel, Device) && wheelOwnsCommand();
            portCommandPending = true;
            RefreshPositionActions();
            RunCommand(() => requestedPort is int port ? service.SetPort(port) : service.GetPort(), record =>
            {
                int actualPort = MQTTCfwPort.ReadPortReply(record, requestedPort)
                    ?? throw new InvalidOperationException("滤色轮未返回有效孔位。");
                foreach (var (spectrum, target, binding, shared) in recipients)
                {
                    bool OwnsBinding() => OwnsWheel() && ReferenceEquals(ServiceManager.Current, manager)
                        && manager!.DeviceServices.Any(device => ReferenceEquals(device, spectrum))
                        && ReferenceEquals(spectrum.Config.NDConfig, binding)
                        && binding.IsBingNDDevice == shared
                        && string.Equals(binding.NDBindDeviceCode, wheelCode, StringComparison.Ordinal)
                        && spectrum.IsConfigurationTargetCurrent(target);
                    if (!OwnsBinding()) continue;
                    spectrum.DisplayConfig.PortNum = actualPort;
                    if (OwnsBinding()) spectrum.ApplyCalibrationGroupForND(actualPort, requestedPort.HasValue, target);
                }
            }, OwnsWheel, () =>
            {
                portCommandPending = false;
                if (!disposed) RefreshPositionActions();
            });
        }

        private void RunCommand(Func<MsgRecord> command, Action<MsgRecord>? success = null, Func<bool>? ownsReply = null,
            Action? finished = null)
        {
            DeviceCfwPort owner = Device;
            MQTTCfwPort service = owner.DService;
            bool OwnsReply() => !disposed && ReferenceEquals(Device, owner) && ReferenceEquals(owner.DService, service)
                && (ownsReply?.Invoke() ?? true);
            try
            {
                var record = command();
                int completed = 0;
                void Complete(object? sender, MsgRecordState state)
                {
                    if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout) || Interlocked.Exchange(ref completed, 1) != 0) return;
                    record.MsgRecordStateChanged -= Complete;
                    void Apply()
                    {
                        try
                        {
                            if (!OwnsReply()) return;
                            if (state == MsgRecordState.Success) success?.Invoke(record);
                            else MessageBox.Show(Application.Current.GetActiveWindow(), record.MsgReturn?.Message ?? "滤色轮命令超时。", "ColorVision");
                        }
                        catch (Exception ex) { MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision"); }
                        finally { finished?.Invoke(); }
                    }
                    if (Dispatcher.CheckAccess()) Apply();
                    else Dispatcher.BeginInvoke(Apply);
                }
                record.MsgRecordStateChanged += Complete;
                Complete(record, record.MsgRecordState);
            }
            catch (Exception ex)
            {
                finished?.Invoke();
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (subscribedDevice != null)
            {
                subscribedDevice.ConfigChanged -= Device_ConfigChanged;
                subscribedDevice.PropertyChanged -= Device_PropertyChanged;
            }
            if (subscribedService != null) subscribedService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            Loaded -= Display_Loaded;
        }
    }
}
