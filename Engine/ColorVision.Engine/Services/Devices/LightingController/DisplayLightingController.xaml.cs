#pragma warning disable CA1816
using ColorVision.Engine.Messages;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.LightingController
{
    public partial class DisplayLightingController : UserControl, IDisPlayControl, IDisposable
    {
        private DeviceLightingController Device { get; }
        private MQTTLightingController DService => Device.DService;

        public string DisPlayName => Device.Config.Name;

        public DisplayLightingController(DeviceLightingController device)
        {
            Device = device;
            InitializeComponent();
            DataContext = Device;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem { Header = Properties.Resources.Property, Command = Device.PropertyCommand });
            ApplyDeviceStatus(DService.DeviceStatus);
            DService.DeviceStatusChanged += DService_DeviceStatusChanged;
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType status) => ApplyDeviceStatus(status);

        private void ApplyDeviceStatus(DeviceStatusType status)
        {
            TextBlockUnknown.Visibility = Visibility.Collapsed;
            TextBlockOffline.Visibility = Visibility.Collapsed;
            ButtonUnauthorized.Visibility = Visibility.Collapsed;
            ControlPanel.Visibility = Visibility.Collapsed;
            ButtonOpen.Visibility = Visibility.Collapsed;
            ButtonClose.Visibility = Visibility.Collapsed;
            ChannelsPanel.IsEnabled = false;

            switch (status)
            {
                case DeviceStatusType.Unknown:
                    TextBlockUnknown.Visibility = Visibility.Visible;
                    break;
                case DeviceStatusType.Unauthorized:
                    ButtonUnauthorized.Visibility = Visibility.Visible;
                    break;
                case DeviceStatusType.OffLine:
                    TextBlockOffline.Visibility = Visibility.Visible;
                    break;
                case DeviceStatusType.LiveOpened:
                case DeviceStatusType.Opening:
                case DeviceStatusType.Opened:
                    ControlPanel.Visibility = Visibility.Visible;
                    ButtonClose.Visibility = Visibility.Visible;
                    ChannelsPanel.IsEnabled = true;
                    break;
                default:
                    ControlPanel.Visibility = Visibility.Visible;
                    ButtonOpen.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                SendCommand(button, DService.Open());
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                SendCommand(button, DService.Close());
        }

        private void SetValue_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetChannel(sender, out Button button, out PMChannelConfig channel))
                SendCommand(button, DService.SetValue(channel.Code, channel.Value));
        }

        private void GetValue_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetChannel(sender, out Button button, out PMChannelConfig channel))
                SendCommand(button, DService.GetValue(channel.Code));
        }

        private void TurnOn_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetChannel(sender, out Button button, out PMChannelConfig channel))
                SendCommand(button, DService.TurnOn(channel), () => channel.Value = channel.OnValue);
        }

        private void TurnOff_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetChannel(sender, out Button button, out PMChannelConfig channel))
                SendCommand(button, DService.TurnOff(channel), () => channel.Value = channel.OffValue);
        }

        private static bool TryGetChannel(object sender, out Button button, out PMChannelConfig channel)
        {
            button = sender as Button ?? null!;
            channel = button?.DataContext as PMChannelConfig ?? null!;
            return button != null && channel != null && !string.IsNullOrWhiteSpace(channel.Code);
        }

        private static void SendCommand(Button button, MsgRecord msgRecord, Action? onSuccess = null)
        {
            EventHandler<MsgRecordState>? handler = null;
            handler = (_, state) =>
            {
                if (state != MsgRecordState.Success && state != MsgRecordState.Fail && state != MsgRecordState.Timeout)
                    return;

                msgRecord.MsgRecordStateChanged -= handler;
                if (state == MsgRecordState.Success)
                {
                    onSuccess?.Invoke();
                    return;
                }

                string status = state == MsgRecordState.Timeout ? Properties.Resources.Timeout : Properties.Resources.Failure;
                string detail = msgRecord.MsgReturn?.Message;
                MessageBox1.Show(Application.Current.GetActiveWindow(), string.IsNullOrWhiteSpace(detail) ? status : $"{status}: {detail}", "ColorVision");
            };

            msgRecord.MsgRecordStateChanged += handler;
            ServicesHelper.SendCommand(button, msgRecord);
        }

        public event RoutedEventHandler? Selected;
        public event RoutedEventHandler? Unselected;
        public event EventHandler? SelectChanged;

        public bool IsSelected
        {
            get => _IsSelected;
            set
            {
                _IsSelected = value;
                SelectChanged?.Invoke(this, EventArgs.Empty);
                if (value)
                    Selected?.Invoke(this, new RoutedEventArgs());
                else
                    Unselected?.Invoke(this, new RoutedEventArgs());
            }
        }
        private bool _IsSelected;

        public void Dispose()
        {
            DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            GC.SuppressFinalize(this);
        }
    }
}
