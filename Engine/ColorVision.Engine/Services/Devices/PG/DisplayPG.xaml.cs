#pragma warning disable CA1816
using ColorVision.Engine.Messages;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.PG
{
    /// <summary>
    /// DisplayPG.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayPG : UserControl, IDisPlayControl, IDisposable
    {
        private MQTTPG PGService { get => Device.DService; }
        private DevicePG Device { get; set; }
        public string DisPlayName => Device.Config.Name;


        public DisplayPG(DevicePG devicePG)
        {
            Device = devicePG;
            InitializeComponent();
            DataContext = Device;
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            EnsureTimedButtonOperations();
            PGService_DeviceStatusChanged(sender, PGService.DeviceStatus);
            PGService.DeviceStatusChanged += PGService_DeviceStatusChanged;
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        private void PGService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }

            void HideAllButtons()
            {
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(StackPanelContent, Visibility.Collapsed);
                SetVisibility(TextBlockOffLine, Visibility.Collapsed);
            }

            HideAllButtons();
            switch (e)
            {

                case DeviceStatusType.Unknown:
                    SetVisibility(TextBlockUnknow, Visibility.Visible);
                    break;
                case DeviceStatusType.Unauthorized:
                    SetVisibility(ButtonUnauthorized, Visibility.Visible);
                    break;
                case DeviceStatusType.OffLine:
                    SetVisibility(TextBlockOffLine, Visibility.Visible);
                    break;
                case DeviceStatusType.UnInit:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
                case DeviceStatusType.Closing:
                case DeviceStatusType.Closed:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
                case DeviceStatusType.LiveOpened:
                case DeviceStatusType.Opening:
                case DeviceStatusType.Opened:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
                default:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
            }

            this.TryGetTimedButtonOperations()?.RefreshIdleState(btn_open);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void PGOpen(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            EnsureTimedButtonOperations();
            bool isClosing = IsPGOpenOrOpening(PGService.DeviceStatus);
            MsgRecord msgRecord = isClosing ? PGService.Close() : OpenPGFromConfig();
            SendTimedPGCommand(button, msgRecord, isClosing ? Properties.Resources.Close : Properties.Resources.Open);
        }

        private MsgRecord OpenPGFromConfig()
        {
            CommunicateType communicateType = PGService.Config.IsNet ? CommunicateType.Tcp : CommunicateType.Serial;
            return PGService.Open(communicateType, PGService.Config.Addr, PGService.Config.Port);
        }

        private void SendTimedPGCommand(Button button, MsgRecord msgRecord, string operationName)
        {
            TimedButtonOperationRegistry operations = EnsureTimedButtonOperations();
            TimedButtonOperationScope? operationScope = operations.Begin(button, runningText: operationName);

            void OnMsgRecordStateChanged(object? s, MsgRecordState state)
            {
                if (!IsTerminalMsgRecordState(state))
                {
                    return;
                }

                msgRecord.MsgRecordStateChanged -= OnMsgRecordStateChanged;
                operationScope?.Complete(state == MsgRecordState.Success);
                operationScope = null;
                operations.RefreshIdleState(button);

                if (state != MsgRecordState.Success)
                {
                    MessageBox1.Show(
                        Application.Current.GetActiveWindow(),
                        BuildOperationFailureMessage(msgRecord, state, operationName),
                        "ColorVision");
                }
            }

            msgRecord.MsgRecordStateChanged += OnMsgRecordStateChanged;
            OnMsgRecordStateChanged(msgRecord, msgRecord.MsgRecordState);
        }

        private TimedButtonOperationRegistry EnsureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(BuildButtonOperationKey);
            operations.Register(btn_open, "open-close", options =>
            {
                options.ContentFactory = stats => IsPGOpenOrOpening(PGService.DeviceStatus)
                    ? Properties.Resources.Close
                    : TimedButtonOperationTextFormatter.BuildCompactContent(Properties.Resources.Open, stats);
                options.ToolTipFactory = stats => IsPGOpenOrOpening(PGService.DeviceStatus)
                    ? Properties.Resources.Close
                    : TimedButtonOperationTextFormatter.BuildTooltip(Properties.Resources.Open, stats);
            });

            return operations;
        }

        private string BuildButtonOperationKey(string actionKey)
        {
            return $"pg:{Device.Config.Code}:{actionKey}";
        }

        private static bool IsPGOpenOrOpening(DeviceStatusType status)
        {
            return status == DeviceStatusType.Opened
                || status == DeviceStatusType.Opening
                || status == DeviceStatusType.LiveOpened;
        }

        private static bool IsTerminalMsgRecordState(MsgRecordState state)
        {
            return state == MsgRecordState.Success || state == MsgRecordState.Fail || state == MsgRecordState.Timeout;
        }

        private static string BuildOperationFailureMessage(MsgRecord msgRecord, MsgRecordState state, string operationName)
        {
            string status = state == MsgRecordState.Timeout ? Properties.Resources.Timeout : Properties.Resources.Failure;
            string message = msgRecord.MsgReturn?.Message;
            string operationFailure = $"{operationName} {status}";

            return string.IsNullOrWhiteSpace(message) ? operationFailure : $"{operationFailure}: {message}";
        }

        private void PGStartPG(object sender, RoutedEventArgs e) => PGService.PGStartPG();

        private void PGStopPG(object sender, RoutedEventArgs e) => PGService.PGStopPG();

        private void PGReSetPG(object sender, RoutedEventArgs e) => PGService.PGReSetPG();
        private void PGSwitchUpPG(object sender, RoutedEventArgs e) => PGService.PGSwitchUpPG();
        private void PGSwitchDownPG(object sender, RoutedEventArgs e) => PGService.PGSwitchDownPG();

        private void PGSwitchFramePG(object sender, RoutedEventArgs e) => PGService.PGSwitchFramePG(int.Parse(PGFrameText.Text));

        private void PGSendCmd(object sender, RoutedEventArgs e)
        {
            PGService.CustomPG(PGCmdMsg.Text);
        }

        public void Dispose()
        {
            PGService.DeviceStatusChanged -= PGService_DeviceStatusChanged;
            this.DisposeTimedButtonOperations();
        }
    }
}
