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
                SetVisibility(ButtonOpen, Visibility.Collapsed);
                SetVisibility(ButtonClose, Visibility.Collapsed);
                SetVisibility(PGOperationPanel, Visibility.Collapsed);
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
                    SetVisibility(ButtonOpen, Visibility.Visible);
                    SetVisibility(PGOperationPanel, Visibility.Visible);
                    break;
                case DeviceStatusType.Closing:
                case DeviceStatusType.Closed:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    SetVisibility(ButtonOpen, Visibility.Visible);
                    SetVisibility(PGOperationPanel, Visibility.Visible);
                    break;
                case DeviceStatusType.LiveOpened:
                case DeviceStatusType.Opening:
                case DeviceStatusType.Opened:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    SetVisibility(ButtonClose, Visibility.Visible);
                    SetVisibility(PGOperationPanel, Visibility.Visible);
                    break;
                default:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    SetVisibility(ButtonOpen, Visibility.Visible);
                    SetVisibility(PGOperationPanel, Visibility.Visible);
                    break;
            }

            TimedButtonOperationRegistry? operations = this.TryGetTimedButtonOperations();
            operations?.RefreshIdleState(OpenButton);
            operations?.RefreshIdleState(CloseButton);
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
            MsgRecord msgRecord = OpenPGFromConfig();
            SendTimedPGCommand(button, msgRecord, Properties.Resources.Open, state =>
            {
                if (state == MsgRecordState.Success)
                    ShowOpenedState();
            });
        }

        private void PGClose(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            EnsureTimedButtonOperations();
            MsgRecord msgRecord = PGService.Close();
            SendTimedPGCommand(button, msgRecord, Properties.Resources.Close, state =>
            {
                if (state == MsgRecordState.Success)
                    ShowClosedState();
            });
        }

        private MsgRecord OpenPGFromConfig()
        {
            CommunicateType communicateType = PGService.Config.IsNet ? CommunicateType.Tcp : CommunicateType.Serial;
            return PGService.Open(communicateType, PGService.Config.Addr, PGService.Config.Port);
        }

        private void SendTimedPGCommand(Button button, MsgRecord msgRecord, string operationName, Action<MsgRecordState>? onTerminalStateChanged = null)
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
                onTerminalStateChanged?.Invoke(state);

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
            operations.Register(OpenButton, "open", options =>
            {
                options.ContentFactory = stats => TimedButtonOperationTextFormatter.BuildCompactContent(Properties.Resources.Open, stats);
                options.ToolTipFactory = stats => TimedButtonOperationTextFormatter.BuildTooltip(Properties.Resources.Open, stats);
            });
            operations.Register(CloseButton, "close", options =>
            {
                options.ContentFactory = stats => TimedButtonOperationTextFormatter.BuildCompactContent(Properties.Resources.Close, stats);
                options.ToolTipFactory = stats => TimedButtonOperationTextFormatter.BuildTooltip(Properties.Resources.Close, stats);
            });

            return operations;
        }

        private string BuildButtonOperationKey(string actionKey)
        {
            return $"pg:{Device.Config.Code}:{actionKey}";
        }

        private void ShowOpenedState()
        {
            ButtonOpen.Visibility = Visibility.Collapsed;
            ButtonClose.Visibility = Visibility.Visible;
            PGOperationPanel.Visibility = Visibility.Visible;
            TimedButtonOperationRegistry? operations = this.TryGetTimedButtonOperations();
            operations?.RefreshIdleState(OpenButton);
            operations?.RefreshIdleState(CloseButton);
        }

        private void ShowClosedState()
        {
            ButtonOpen.Visibility = Visibility.Visible;
            ButtonClose.Visibility = Visibility.Collapsed;
            PGOperationPanel.Visibility = Visibility.Visible;
            TimedButtonOperationRegistry? operations = this.TryGetTimedButtonOperations();
            operations?.RefreshIdleState(OpenButton);
            operations?.RefreshIdleState(CloseButton);
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
