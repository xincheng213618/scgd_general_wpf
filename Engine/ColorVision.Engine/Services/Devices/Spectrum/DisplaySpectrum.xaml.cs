using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.UI;
using log4net;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.Spectrum
{


    /// <summary>
    /// DisplaySpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySpectrum : UserControl, IDisPlayControl,IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(MQTTServiceBase));
        public DeviceSpectrum Device { get; set; }
        public MQTTSpectrum DService { get => Device.DService; }

        public ViewSpectrum View { get => Device.View;}

        public string DisPlayName => Device.Config.Name;
        public string PersistenceKey => Device.Config.Code;
        private bool _isInitialized;

        public DisplaySpectrum(DeviceSpectrum DeviceSpectrum)
        {
            this.Device = DeviceSpectrum;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            Device.Config.EnsureCalibrationGroups();
            RefreshNDHoleMappings();
            EnsureTimedButtonOperations();

            Device.SelfAdaptionInitDarkStarted += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Overlay.Visibility=Visibility.Visible;
                    LoadingPanel.Visibility=Visibility.Visible;
                });
            };
            Device.SelfAdaptionInitDarkCompleted += () =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Overlay.Visibility = Visibility.Collapsed;
                    LoadingPanel.Visibility = Visibility.Collapsed;
                });
            };
            this.AddViewConfig(Device.ViewShell, DisPlayName);
            DService_DeviceStatusChanged(sender, DeviceStatusType.Closed);
            DService_DeviceStatusChanged(sender,DService.DeviceStatus);
            DService.DeviceStatusChanged += DService_DeviceStatusChanged;

            this.ApplyChangedSelectedColor(DisPlayBorder);
            _isInitialized = true;
        }

        public void RefreshNDHoleMappings()
        {
            ComboBoxNDPort.ItemsSource = Device.GetNDHoleMappings();
        }

        private void CalibrationGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized)
                return;

            if (ComboBoxCalibrationGroups.SelectedItem is not SpectrumCalibrationGroup group
                || string.Equals(group.GroupName, Device.Config.ActiveCalibrationGroupName, StringComparison.OrdinalIgnoreCase)
                || !Device.CalibrationGroupChangeGuard.TryBeginUserSwitch())
            {
                return;
            }

            if (group.NDHoleIndex >= 0 && Device.Config.NDConfig.IsBingNDDevice && Device.GetBoundCfwPort() == null
                && !(Device.Config.NDConfig.IsNDPort && Device.UsesLocalNd))
            {
                CompleteCalibrationGroupSwitch(group, Device.DisplayConfig.PortNum, null, null);
                return;
            }

            if (group.NDHoleIndex < 0 || (Device.SpectrumBackend.OpensLocally && !Device.Config.NDConfig.IsNDPort))
            {
                try
                {
                    Device.ApplyCalibrationGroup(group, true);
                    if (group.NDHoleIndex >= 0) Device.DisplayConfig.PortNum = group.NDHoleIndex;
                }
                finally
                {
                    Device.CalibrationGroupChangeGuard.CompleteUserSwitch();
                }
                return;
            }

            int previousPort = Device.DisplayConfig.PortNum;
            ComboBoxCalibrationGroups.IsEnabled = false;

            MsgRecord? msgRecord;
            string? error;
            try
            {
                if (!Device.TrySetNDPortForCalibrationGroup(group.NDHoleIndex, out msgRecord, out error) || msgRecord == null)
                {
                    CompleteCalibrationGroupSwitch(group, previousPort, null, error ?? "无法发送 ND 切换指令。");
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Failed to send the ND port command for a spectrum calibration group.", ex);
                CompleteCalibrationGroupSwitch(group, previousPort, null, ex.Message);
                return;
            }

            int completed = 0;
            EventHandler<MsgRecordState>? stateChanged = null;
            stateChanged = (_, state) =>
            {
                if (state != MsgRecordState.Success && state != MsgRecordState.Fail && state != MsgRecordState.Timeout)
                    return;

                if (Interlocked.Exchange(ref completed, 1) != 0)
                    return;

                msgRecord.MsgRecordStateChanged -= stateChanged;
                string? failure = state == MsgRecordState.Success ? null : GetNDPortSwitchFailure(msgRecord, state);
                CompleteCalibrationGroupSwitch(group, previousPort, state, failure);
            };

            msgRecord.MsgRecordStateChanged += stateChanged;
            stateChanged(msgRecord, msgRecord.MsgRecordState);
        }

        private void CompleteCalibrationGroupSwitch(SpectrumCalibrationGroup group, int previousPort, MsgRecordState? state, string? failure)
        {
            void Complete()
            {
                try
                {
                    if (state == MsgRecordState.Success)
                    {
                        Device.DisplayConfig.PortNum = group.NDHoleIndex;
                        RefreshNDHoleMappings();
                        Device.ApplyCalibrationGroup(group, true);
                    }
                    else
                    {
                        Device.DisplayConfig.PortNum = previousPort;
                        ComboBoxCalibrationGroups.SetCurrentValue(ComboBox.SelectedValueProperty, Device.Config.ActiveCalibrationGroupName);
                        if (!string.IsNullOrWhiteSpace(failure)) MessageBox.Show(
                            Application.Current.GetActiveWindow(),
                            LocalizedText.Format($"切换标定分组对应的 ND 失败：{failure}"),
                            "ColorVision",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                finally
                {
                    ComboBoxCalibrationGroups.IsEnabled = true;
                    Device.CalibrationGroupChangeGuard.CompleteUserSwitch();
                }
            }

            if (Dispatcher.CheckAccess())
                Complete();
            else
                Dispatcher.Invoke(Complete);
        }

        private static string GetNDPortSwitchFailure(MsgRecord msgRecord, MsgRecordState state)
        {
            if (state == MsgRecordState.Timeout)
                return "指令超时。";

            return string.IsNullOrWhiteSpace(msgRecord.MsgReturn?.Message)
                ? "设备未返回具体错误信息。"
                : msgRecord.MsgReturn.Message;
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            // Command progress belongs to the button; intermediate or unsupported states keep the current layout.
            if (e is not (DeviceStatusType.Unknown or DeviceStatusType.Unauthorized or DeviceStatusType.OffLine
                or DeviceStatusType.UnInit or DeviceStatusType.Closed or DeviceStatusType.Opened
                or DeviceStatusType.Free or DeviceStatusType.SP_Continuous_Mode)) return;

            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
            void HideAllButtons()
            {
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(StackPanelContent, Visibility.Collapsed);
                SetVisibility(StackPanelOpen, Visibility.Collapsed);
                SetVisibility(TextBlockOffLine, Visibility.Collapsed);
            }

            HideAllButtons();
            btn_autoTest.Content = ColorVision.Engine.Properties.Resources.ContinuousMeasurement;
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
                    btn_connect.Content = ColorVision.Engine.Properties.Resources.Open;
                    break;
                case DeviceStatusType.Closed:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    btn_connect.Content = ColorVision.Engine.Properties.Resources.Open;
                    break;
                case DeviceStatusType.Opened:
                case DeviceStatusType.Free:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    SetVisibility(StackPanelOpen, Visibility.Visible);
                    btn_connect.Content = ColorVision.Engine.Properties.Resources.Close;
                    break;
                case DeviceStatusType.SP_Continuous_Mode:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    SetVisibility(StackPanelOpen, Visibility.Visible);
                    btn_autoTest.Content = ColorVision.Engine.Properties.Resources.CancelAutoTest;
                    break;
                default:
                    break;
            }
            this.TryGetTimedButtonOperations()?.RefreshIdleState();
        }

        private void Device_SelfAdaptionInitDarkStarted()
        {
            throw new NotImplementedException();
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }




        #region MQTT

        private TimedButtonOperationRegistry EnsureTimedButtonOperations()
        {
            var operations = this.GetTimedButtonOperations(action => $"spectrum:{Device.Config.Code}:{action}");
            operations.Register(btn_connect, options => options.ContentFactory = _ =>
                Device.SpectrumBackend.LocalOwned || DService.DeviceStatus is DeviceStatusType.Opened or DeviceStatusType.SP_Continuous_Mode
                    ? Properties.Resources.Close : Properties.Resources.Open);
            operations.Register(btn_autoTest, options => options.ContentFactory = _ =>
                DService.DeviceStatus == DeviceStatusType.SP_Continuous_Mode ? Properties.Resources.CancelAutoTest : Properties.Resources.ContinuousMeasurement);
            operations.Register(btn_oneTest);
            operations.Register(btn_stopTest);
            operations.Register(btn_oneInitDark);
            return operations;
        }

        private void ExecuteSpectrumCommand(object sender, Func<MsgRecord> command, Action<MsgRecord, MsgRecordState>? completed = null)
        {
            if (sender is not Button button) return;
            var operations = EnsureTimedButtonOperations();
            if (operations.Get(button)?.IsRunning == true) return;
            MsgRecord record = command();
            if (record.MsgRecordState is MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                completed?.Invoke(record, record.MsgRecordState);
            else
                ServicesHelper.SendTimedCommand(this, button, record, Properties.Resources.Processing, (reply, state) =>
                {
                    Dispatcher.BeginInvoke(() => this.TryGetTimedButtonOperations()?.RefreshIdleState(button));
                    completed?.Invoke(reply, state);
                });
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            bool close = Device.SpectrumBackend.LocalOwned || DService.DeviceStatus is DeviceStatusType.Opened or DeviceStatusType.SP_Continuous_Mode;
            ExecuteSpectrumCommand(sender, () => close ? DService.Close() : DService.Open(), (record, state) =>
            {
                if (state == MsgRecordState.Fail)
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
            });
        }
        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            ExecuteSpectrumCommand(sender, () => DService.GetData(), (record, state) =>
            {
                if (state == MsgRecordState.Fail) ShowGetDataFailure(record);
            });
        }

        private static void ShowGetDataFailure(MsgRecord msgRecord)
        {
            string detail = string.IsNullOrWhiteSpace(msgRecord.MsgReturn?.Message)
                ? "未返回具体错误信息。"
                : msgRecord.MsgReturn.Message;

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBoxResult result = MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    LocalizedText.Format($"光谱取图失败：{detail}\n\n是否打开光谱日志？"),
                    "ColorVision",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error,
                    MessageBoxResult.Yes);
                if (result == MessageBoxResult.Yes)
                    DeviceSpectrum.OpenSpectrumLog();
            });
        }

        private void Button_Click_AutoTest(object sender, RoutedEventArgs e)
        {
            bool stop = DService.DeviceStatus == DeviceStatusType.SP_Continuous_Mode;
            ExecuteSpectrumCommand(sender, () => stop ? DService.GetDataAutoStop() : DService.GetDataAuto(), (record, state) =>
            {
                if (state == MsgRecordState.Fail)
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
            });
        }
        private void Button_Click_StopTest(object sender, RoutedEventArgs e)
        {
            ExecuteSpectrumCommand(sender, () => DService.GetDataAutoStop(), (record, state) =>
            {
                if (state == MsgRecordState.Fail) ShowGetDataFailure(record);
            });
        }
        private void Button_Click_Init_Dark(object sender, RoutedEventArgs e)
        {
            ExecuteSpectrumCommand(sender, () => DService.InitDark(), (record, state) =>
            {
                if (state == MsgRecordState.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionComplete, "ColorVision");
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                }
            });
        }

        #endregion

        private void Button_Click_Shutter_Connect(object sender, RoutedEventArgs e)
        {
            ExecuteSpectrumCommand(sender, () => DService.ShutterConnect(), (record, state) =>
            {
                if (state == MsgRecordState.Fail)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                }
            });

        }

        private void Button_Click_Shutter_Doopen(object sender, RoutedEventArgs e)
        {
            ExecuteSpectrumCommand(sender, () => DService.ShutterDoopen(), (record, state) =>
            {
                if (state == MsgRecordState.Fail)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                }
                else if (state == MsgRecordState.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Sucess", "ColorVision");
                }
            });
        }

        private void Button_Click_Shutter_Doclose(object sender, RoutedEventArgs e)
        {
            ExecuteSpectrumCommand(sender, () => DService.ShutterDoclose(), (record, state) =>
            {
                if (state == MsgRecordState.Fail)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                }
                else if (state == MsgRecordState.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Sucess", "ColorVision");
                }
            });
        }

        private void NDport_Click(object sender, RoutedEventArgs e)
        {
            if (Device.Config.NDConfig.IsBingNDDevice && Device.GetBoundCfwPort() == null) return;
            ExecuteSpectrumCommand(sender, () => DService.SetPort(), (record, state) =>
            {
                if (state == MsgRecordState.Fail)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                }
                else if (state == MsgRecordState.Success)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RefreshNDHoleMappings();
                        Device.ApplyCalibrationGroupForND(Device.DisplayConfig.PortNum, true);
                    });
                }
            });
        }

        private void GetNDport_Click(object sender, RoutedEventArgs e)
        {
            ExecuteSpectrumCommand(sender, () => DService.GetPort(), (record, state) =>
            {
                if (state == MsgRecordState.Success)
                {
                    int port = record.MsgReturn.Data.Port;
                    Application.Current.Dispatcher.Invoke(() => Device.DisplayConfig.PortNum = port);
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{record.MsgReturn.Message}", "ColorVision");
                }
            });
        }

        public void Dispose()
        {
            this.TryGetTimedButtonOperations()?.Dispose();
            DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            GC.SuppressFinalize(this);
        }
    }
}
