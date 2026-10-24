#pragma warning disable CA1816
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.UI;
using System;
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
            Device.ConfigChanged += Device_ConfigChanged;

            DService_DeviceStatusChanged(sender, DService.DeviceStatus);
            DService.DeviceStatusChanged += DService_DeviceStatusChanged; ;
            Loaded += Display_Loaded;
        }

        private void Display_Loaded(object sender, RoutedEventArgs e)
        {
            if (Device.TryBeginLocalAutoConnect()) RunCommand(DService.Open);
        }

        private void Device_ConfigChanged(object? sender, EventArgs e) => RefreshPortMappings();

        private void RefreshPortMappings()
        {
            int? port = Device.DisplayConfig.PortNum;
            CombPort.ItemsSource = Device.FilterWheelConfig.HoleMapping;
            CombPort.DisplayMemberPath = "HoleName";
            CombPort.SetCurrentValue(ComboBox.SelectedValueProperty, port);
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => DService_DeviceStatusChanged(sender, e)); return; }
            StackPanelOpen.IsEnabled = e is DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Free;
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
                SetVisibility(StackPanelOpen, Visibility.Collapsed);
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
                    SetVisibility(StackPanelOpen, Visibility.Visible);
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
            if (CombPort.SelectedItem is HoleMap holeMap)
            {
                RunCommand(() => DService.SetPort(holeMap.HoleIndex), _ =>
                {
                    ApplyBoundSpectrumCalibration(holeMap.HoleIndex, true);
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.Success, "ColorVision");
                });
            }
        }

        private void CameraOffline_Click(object sender, RoutedEventArgs e) => RunCommand(DService.Open);

        private void GetPort_Click(object sender, RoutedEventArgs e)
        {
            RunCommand(DService.GetPort, record =>
            {
                int port = record.MsgReturn.Data.Port;
                Device.DisplayConfig.PortNum = port;
                ApplyBoundSpectrumCalibration(port, false);
            });
        }

        private void RunCommand(Func<MsgRecord> command, Action<MsgRecord>? success = null)
        {
            try
            {
                var record = command();
                int completed = 0;
                void Complete(object? sender, MsgRecordState state)
                {
                    if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout) || Interlocked.Exchange(ref completed, 1) != 0) return;
                    record.MsgRecordStateChanged -= Complete;
                    if (state == MsgRecordState.Success) success?.Invoke(record);
                    else MessageBox.Show(Application.Current.GetActiveWindow(), record.MsgReturn?.Message ?? "滤色轮命令超时。", "ColorVision");
                }
                record.MsgRecordStateChanged += Complete;
                Complete(record, record.MsgRecordState);
            }
            catch (Exception ex) { MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision"); }
        }

        public void Dispose()
        {
            Device.ConfigChanged -= Device_ConfigChanged;
            DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            Loaded -= Display_Loaded;
        }

        private void ApplyBoundSpectrumCalibration(int holeIndex, bool forceRestart)
        {
            foreach (var spectrum in ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>())
            {
                if (!string.Equals(spectrum.Config.NDConfig.NDBindDeviceCode, Device.Code, StringComparison.Ordinal))
                    continue;

                spectrum.DisplayConfig.PortNum = holeIndex;
                spectrum.ApplyCalibrationGroupForND(holeIndex, forceRestart);
            }
        }
    }
}
