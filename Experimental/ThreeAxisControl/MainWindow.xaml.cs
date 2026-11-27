using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ThreeAxisControl
{
    public partial class MainWindow : Window
    {
        private const int MaxXyBaud = 1000000;
        private const int MaxZSpeed = 2000000;
        private const int MaxZChannel = 8;

        private readonly ControllerPanel xy;
        private readonly ControllerPanel z;
        private readonly AxisRow[] axes;
        private readonly DispatcherTimer pollTimer;
        private readonly Brush neutral;
        private readonly Brush connected;
        private readonly Brush attention;
        private readonly Brush busy;
        private readonly SettingsStore settingsStore = new SettingsStore();
        private ControlSettings settings;
        private bool settingsDirty;
        private bool stopping;
        private bool closing;
        private bool canClose;
        private HeldMove heldMove;

        public MainWindow()
        {
            InitializeComponent();

            neutral = XyStatus.Foreground;
            connected = CreateBrush(89, 214, 167);
            attention = CreateBrush(255, 136, 142);
            busy = CreateBrush(121, 190, 255);

            xy = new ControllerPanel("PMC100-2", 2, XyConnect, XyStatus, XyPort, XyBaud);
            z = new ControllerPanel("MC06-004A", 1, ZConnect, ZStatus, ZLocalIp, ZControllerIp, ZUdp, ZChannel);
            axes = new[]
            {
                new AxisRow("X", xy, 0, XPosition, XStatus, XStep, XSpeed, XStartSpeed, XAcceleration,
                    XMinPosition, XMaxPosition, XMinus, XPlus, "向左", "向右"),
                new AxisRow("Y", xy, 1, YPosition, YStatus, YStep, YSpeed, YStartSpeed, YAcceleration,
                    YMinPosition, YMaxPosition, YMinus, YPlus, "后退", "前进"),
                new AxisRow("Z", z, 0, ZPosition, ZAxisStatus, ZStep, ZSpeed, ZStartSpeed, ZAcceleration,
                    ZMinPosition, ZMaxPosition, ZMinus, ZPlus, "向上", "向下")
            };
            RestoreSettings();

            XyConnect.Click += async (_, __) => await ChangeConnectionAsync(xy);
            ZConnect.Click += async (_, __) => await ChangeConnectionAsync(z);
            XyPort.DropDownOpened += (_, __) => RefreshPorts();
            XyPort.DropDownClosed += (_, __) => SaveSettings();
            XyAutoConnect.Checked += (_, __) => EnableAutoConnect(XyAutoConnect,
                HasXySettings(XyPort.Text, XyBaud.Text), xy.Name);
            ZAutoConnect.Checked += (_, __) => EnableAutoConnect(ZAutoConnect,
                HasZSettings(ZLocalIp.Text, ZControllerIp.Text, ZUdp.Text, ZChannel.Text), z.Name);
            XyAutoConnect.Unchecked += (_, __) => SaveSettings();
            ZAutoConnect.Unchecked += (_, __) => SaveSettings();
            foreach (Control field in xy.Fields.Concat(z.Fields).Concat(axes.SelectMany(axis => axis.Fields)))
                field.LostKeyboardFocus += (_, __) => SaveSettings();
            Simulation.Checked += (_, __) => UpdateControls();
            Simulation.Unchecked += (_, __) =>
            {
                RefreshPorts();
                UpdateControls();
            };
            Stop.Click += async (_, __) => await StopAsync();
            CopyLog.Click += (_, __) => CopyOperationLog();
            ContinuousMode.Checked += (_, __) => SetContinuousNotice(null);
            ContinuousMode.Unchecked += (_, __) => SetContinuousNotice(null);
            ConfigureLimits.Click += (_, __) => MainTabs.SelectedItem = SettingsTab;
            foreach (var axis in axes)
            {
                WireDirection(axis, axis.Minus, -1);
                WireDirection(axis, axis.Plus, 1);
            }
            Deactivated += async (_, __) => await StopHeldMoveAsync();
            MainTabs.SelectionChanged += async (sender, args) =>
            {
                if (args.OriginalSource == sender && MainTabs.SelectedItem != ControlTab)
                    await StopHeldMoveAsync();
            };

            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            pollTimer.Tick += async (_, __) => await PollBothAsync();
            Loaded += StartOnLoaded;
            Closing += CloseControllers;
            Closed += (_, __) => pollTimer.Stop();

            Rect workArea = SystemParameters.WorkArea;
            MinWidth = Math.Min(MinWidth, workArea.Width);
            MinHeight = Math.Min(MinHeight, workArea.Height);
            Width = Math.Min(Width, workArea.Width);
            Height = Math.Min(Height, workArea.Height);
            UpdateControls();
        }

        private bool InSimulation => Simulation.IsChecked == true;

        private async void StartOnLoaded(object sender, RoutedEventArgs args)
        {
            Loaded -= StartOnLoaded;
            pollTimer.Start();
            await Task.WhenAll(
                XyAutoConnect.IsChecked == true ? ChangeConnectionAsync(xy) : Task.CompletedTask,
                ZAutoConnect.IsChecked == true ? ChangeConnectionAsync(z) : Task.CompletedTask);
        }

        private void EnableAutoConnect(CheckBox checkBox, bool valid, string controllerName)
        {
            if (!valid)
            {
                WriteLog($"{controllerName} · 请填写有效的连接参数再启用启动连接。");
                checkBox.IsChecked = false;
                return;
            }

            SaveSettings();
        }

        private void RestoreSettings()
        {
            try
            {
                settings = settingsStore.Load();
            }
            catch (Exception ex) when (IsSettingsException(ex))
            {
                settings = new ControlSettings();
                WriteLog($"读取操作参数失败 · {ex.Message}");
            }

            XyPort.Text = NormalizePort(settings.XyPort) ?? XyPort.Text;
            XyBaud.Text = NormalizeNumber(settings.XyBaud, MaxXyBaud) ?? XyBaud.Text;
            ZLocalIp.Text = NormalizeIp(settings.ZLocalIp) ?? ZLocalIp.Text;
            ZControllerIp.Text = NormalizeIp(settings.ZControllerIp) ?? ZControllerIp.Text;
            ZUdp.Text = NormalizeNumber(settings.ZUdp, ushort.MaxValue) ?? ZUdp.Text;
            ZChannel.Text = NormalizeNumber(settings.ZChannel, MaxZChannel) ?? ZChannel.Text;
            // Saved XY motion parameters use the physical PMC100 channel names.
            XStep.Text = NormalizeNumber(settings.YStep) ?? XStep.Text;
            YStep.Text = NormalizeNumber(settings.XStep) ?? YStep.Text;
            ZStep.Text = NormalizeNumber(settings.ZStep) ?? ZStep.Text;
            XSpeed.Text = NormalizeXySpeed(settings.YSpeed) ?? XSpeed.Text;
            YSpeed.Text = NormalizeXySpeed(settings.XSpeed) ?? YSpeed.Text;
            ZSpeed.Text = NormalizeNumber(settings.ZSpeed, MaxZSpeed) ?? ZSpeed.Text;
            RestoreAxisSettings(axes[0], settings.YStartSpeed, settings.YAcceleration, settings.YMinPosition, settings.YMaxPosition);
            RestoreAxisSettings(axes[1], settings.XStartSpeed, settings.XAcceleration, settings.XMinPosition, settings.XMaxPosition);
            RestoreAxisSettings(axes[2], settings.ZStartSpeed, settings.ZAcceleration, settings.ZMinPosition, settings.ZMaxPosition);
            XyAutoConnect.IsChecked = RestoreAutoConnect(settings.XyAutoConnect,
                HasXySettings(settings.XyPort, settings.XyBaud), xy.Name);
            ZAutoConnect.IsChecked = RestoreAutoConnect(settings.ZAutoConnect,
                HasZSettings(settings.ZLocalIp, settings.ZControllerIp, settings.ZUdp, settings.ZChannel), z.Name);
            if (XyAutoConnect.IsChecked == true || ZAutoConnect.IsChecked == true)
                Simulation.IsChecked = false;

            CaptureSettings();
            settingsDirty = false;
        }

        private bool RestoreAutoConnect(bool requested, bool valid, string controllerName)
        {
            if (requested && !valid)
                WriteLog($"{controllerName} · 启动连接已跳过，请检查保存的连接参数。");
            return requested && valid;
        }

        private void SaveSettings()
        {
            CaptureSettings();
            if (!settingsDirty)
                return;

            try
            {
                settingsStore.Save(settings);
                settingsDirty = false;
            }
            catch (Exception ex) when (IsSettingsException(ex))
            {
                WriteLog($"保存操作参数失败 · {ex.Message}");
            }
        }

        private void CaptureSettings()
        {
            settings.XyPort = CaptureSetting(settings.XyPort, NormalizePort(XyPort.Text));
            settings.XyBaud = CaptureSetting(settings.XyBaud, NormalizeNumber(XyBaud.Text, MaxXyBaud));
            string localIp = NormalizeIp(ZLocalIp.Text);
            string controllerIp = NormalizeIp(ZControllerIp.Text);
            if (localIp != null && controllerIp != null && localIp != controllerIp)
            {
                settings.ZLocalIp = CaptureSetting(settings.ZLocalIp, localIp);
                settings.ZControllerIp = CaptureSetting(settings.ZControllerIp, controllerIp);
            }
            settings.ZUdp = CaptureSetting(settings.ZUdp, NormalizeNumber(ZUdp.Text, ushort.MaxValue));
            settings.ZChannel = CaptureSetting(settings.ZChannel, NormalizeNumber(ZChannel.Text, MaxZChannel));
            settings.XStep = CaptureSetting(settings.XStep, NormalizeNumber(YStep.Text));
            settings.YStep = CaptureSetting(settings.YStep, NormalizeNumber(XStep.Text));
            settings.ZStep = CaptureSetting(settings.ZStep, NormalizeNumber(ZStep.Text));
            settings.XSpeed = CaptureSetting(settings.XSpeed, NormalizeXySpeed(YSpeed.Text));
            settings.YSpeed = CaptureSetting(settings.YSpeed, NormalizeXySpeed(XSpeed.Text));
            settings.ZSpeed = CaptureSetting(settings.ZSpeed, NormalizeNumber(ZSpeed.Text, MaxZSpeed));
            settings.XStartSpeed = CaptureSetting(settings.XStartSpeed, NormalizeOptionalNumber(YStartSpeed.Text));
            settings.YStartSpeed = CaptureSetting(settings.YStartSpeed, NormalizeOptionalNumber(XStartSpeed.Text));
            settings.ZStartSpeed = CaptureSetting(settings.ZStartSpeed, NormalizeOptionalNumber(ZStartSpeed.Text, MaxZSpeed, true));
            settings.XAcceleration = CaptureSetting(settings.XAcceleration, NormalizeOptionalNumber(YAcceleration.Text));
            settings.YAcceleration = CaptureSetting(settings.YAcceleration, NormalizeOptionalNumber(XAcceleration.Text));
            settings.ZAcceleration = CaptureSetting(settings.ZAcceleration, NormalizeOptionalNumber(ZAcceleration.Text));
            (settings.XMinPosition, settings.XMaxPosition) = CaptureLimits(axes[1], settings.XMinPosition, settings.XMaxPosition);
            (settings.YMinPosition, settings.YMaxPosition) = CaptureLimits(axes[0], settings.YMinPosition, settings.YMaxPosition);
            (settings.ZMinPosition, settings.ZMaxPosition) = CaptureLimits(axes[2], settings.ZMinPosition, settings.ZMaxPosition);

            bool xyAutoConnect = XyAutoConnect.IsChecked == true;
            bool zAutoConnect = ZAutoConnect.IsChecked == true;
            settingsDirty |= settings.XyAutoConnect != xyAutoConnect || settings.ZAutoConnect != zAutoConnect;
            settings.XyAutoConnect = xyAutoConnect;
            settings.ZAutoConnect = zAutoConnect;
        }

        private string CaptureSetting(string current, string candidate)
        {
            if (candidate == null || candidate == current)
                return current;

            settingsDirty = true;
            return candidate;
        }

        private static bool HasXySettings(string port, string baud)
        {
            return !string.IsNullOrEmpty(NormalizePort(port)) && TryReadNumber(baud, MaxXyBaud, out _);
        }

        private static bool HasZSettings(string localIp, string controllerIp, string udp, string channel)
        {
            return Mc06Controller.TryParseIpv4(localIp, out var local) && Mc06Controller.TryParseIpv4(controllerIp, out var controller)
                && !local.Equals(controller) && TryReadNumber(udp, ushort.MaxValue, out _) && TryReadNumber(channel, MaxZChannel, out _);
        }

        private static string NormalizePort(string input)
        {
            string port = input?.Trim();
            if (string.IsNullOrEmpty(port))
                return port;
            if (!port.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || !int.TryParse(port.Substring(3), NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number <= 0)
                return null;

            return port.ToUpperInvariant();
        }

        private static string NormalizeIp(string input)
        {
            return Mc06Controller.TryParseIpv4(input, out var ip) ? ip.ToString() : null;
        }

        private static string NormalizeNumber(string input, int maximum = int.MaxValue)
        {
            return TryReadNumber(input, maximum, out int value) ? value.ToString(CultureInfo.InvariantCulture) : null;
        }

        private static string NormalizeOptionalNumber(string input, int maximum = int.MaxValue, bool allowZero = false)
        {
            if (input != null && string.IsNullOrWhiteSpace(input))
                return string.Empty;
            if (allowZero && int.TryParse(input?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value == 0)
                return "0";
            return NormalizeNumber(input, maximum);
        }

        private static string NormalizeXySpeed(string input) => NormalizeOptionalNumber(input);

        private void RestoreAxisSettings(AxisRow axis, string start, string acceleration, string minimum, string maximum)
        {
            bool isZ = axis.Controller == z;
            axis.StartSpeed.Text = NormalizeOptionalNumber(start, isZ ? MaxZSpeed : int.MaxValue, isZ) ?? axis.StartSpeed.Text;
            axis.Acceleration.Text = NormalizeOptionalNumber(acceleration) ?? axis.Acceleration.Text;
            if (TryReadPosition(minimum, out int? lower) && TryReadPosition(maximum, out int? upper) && !(lower >= upper))
            {
                axis.Minimum.Text = PositionText(lower);
                axis.Maximum.Text = PositionText(upper);
            }
        }

        private (string Minimum, string Maximum) CaptureLimits(AxisRow axis, string savedMinimum, string savedMaximum)
        {
            if (!TryReadPosition(axis.Minimum.Text, out int? lower) || !TryReadPosition(axis.Maximum.Text, out int? upper) || lower >= upper)
                return (savedMinimum, savedMaximum);

            return (CaptureSetting(savedMinimum, PositionText(lower)), CaptureSetting(savedMaximum, PositionText(upper)));
        }

        private static bool TryReadPosition(string text, out int? position)
        {
            position = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            if (!int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                return false;
            position = value;
            return true;
        }

        private static string PositionText(int? position) => position?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        private static bool IsSettingsException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException
                || exception is InvalidOperationException || exception is System.Xml.XmlException;
        }

        protected override async void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key != Key.Escape || !Stop.IsEnabled)
                return;

            e.Handled = true;
            if (!e.IsRepeat)
                await StopAsync();
        }

        private void RefreshPorts()
        {
            try
            {
                string selected = XyPort.Text;
                string[] ports = SerialPort.GetPortNames();
                Array.Sort(ports, StringComparer.OrdinalIgnoreCase);
                XyPort.ItemsSource = ports;
                XyPort.Text = string.IsNullOrWhiteSpace(selected) && ports.Length == 1 ? ports[0] : selected;
            }
            catch (Exception ex)
            {
                WriteLog($"读取串口列表失败 · {ex.Message}");
            }
        }

        private IMotionController CreateController(ControllerPanel panel)
        {
            if (InSimulation)
                return new SimulatedController(panel.AxisCount);

            if (panel == xy)
            {
                if (string.IsNullOrWhiteSpace(XyPort.Text))
                    throw new InvalidOperationException("请选择或输入串口号。");

                return new Pmc100Controller(XyPort.Text.Trim(), ReadNumber(XyBaud, "波特率", MaxXyBaud));
            }

            return new Mc06Controller(ZLocalIp.Text, ZControllerIp.Text, ReadNumber(ZUdp, "本机 UDP 端口", ushort.MaxValue),
                ReadNumber(ZChannel, "Z 轴通道", MaxZChannel));
        }

        private async Task ChangeConnectionAsync(ControllerPanel panel)
        {
            bool disconnecting = panel.Session.IsConnected;
            string action = disconnecting ? "断开" : "连接";
            panel.Operation = action + "中…";
            panel.ConnectionError = null;
            panel.ReadError = null;
            panel.InvalidateStates();
            UpdateControls();
            try
            {
                if (disconnecting)
                    await panel.Session.DisconnectAsync();
                else
                    await panel.Session.ConnectAsync(CreateController(panel));

                WriteLog($"{panel.Name} · {action}成功{(InSimulation ? "（模拟）" : "")}");
            }
            catch (Exception ex)
            {
                panel.ConnectionError = disconnecting ? "停止未确认" : "连接失败";
                WriteLog($"{panel.Name} · {ex.Message}");
            }
            finally
            {
                panel.Operation = null;
                UpdateControls();
            }

            await PollAsync(panel);
        }

        private Task PollBothAsync() => Task.WhenAll(PollAsync(xy), PollAsync(z));

        private async Task PollAsync(ControllerPanel panel)
        {
            if (closing || stopping || panel.Polling || panel.Operation != null || !panel.Session.IsConnected)
                return;

            panel.Polling = true;
            int revision = panel.Revision;
            try
            {
                AxisState[] states = await panel.Session.ReadAxesAsync();
                if (states == null || panel.Revision != revision)
                    return;

                panel.States = states;
                if (panel.ReadError != null)
                    WriteLog($"{panel.Name} · 状态读取恢复");
                panel.ReadError = null;
                var active = heldMove;
                if (active?.Axis.Controller == panel)
                {
                    var state = states[active.Axis.Index];
                    if (state.Alarm == true)
                        _ = StopHeldMoveAsync("收到轴报警，已请求停止持续运动。");
                    else if ((active.Direction > 0 ? state.PositiveLimit : state.NegativeLimit) == true)
                        _ = StopHeldMoveAsync("收到方向限位信号，已请求停止持续运动。");
                }
            }
            catch (Exception ex)
            {
                if (panel.Revision != revision)
                    return;

                panel.States = null;
                if (panel.ReadError != ex.Message)
                    WriteLog($"{panel.Name} · 读取状态失败 · {ex.Message}");
                panel.ReadError = ex.Message;
                if (heldMove?.Axis.Controller == panel)
                    _ = StopHeldMoveAsync("状态读取失败，已请求停止持续运动。");
            }
            finally
            {
                panel.Polling = false;
                if (!closing)
                    UpdateControls();
            }
        }

        private void WireDirection(AxisRow axis, Button button, int direction)
        {
            button.Click += async (_, __) =>
            {
                if (ContinuousMode.IsChecked != true)
                    await MoveAsync(axis, direction, false, CancellationToken.None);
            };
            button.PreviewMouseLeftButtonDown += async (_, args) =>
            {
                if (ContinuousMode.IsChecked != true)
                    return;
                args.Handled = true;
                if (heldMove != null || stopping || closing || !button.IsEnabled)
                    return;
                if (!button.CaptureMouse())
                {
                    string message = $"{axis.Name} 轴未能捕获鼠标，请重新按住方向按钮。";
                    SetContinuousNotice(message);
                    WriteLog(message);
                    return;
                }

                var active = new HeldMove(axis, button, direction);
                heldMove = active;
                SetContinuousNotice($"{axis.Name} 轴正在准备持续运动，松开停止。");
                bool sent = await MoveAsync(axis, direction, true, active.Cancellation.Token);
                if (!sent && heldMove == active)
                    await StopHeldMoveAsync();
            };
            button.PreviewMouseLeftButtonUp += async (_, args) =>
            {
                if (heldMove?.Button != button)
                    return;
                args.Handled = true;
                await StopHeldMoveAsync();
            };
            button.LostMouseCapture += async (_, __) =>
            {
                if (heldMove?.Button == button)
                    await StopHeldMoveAsync();
            };
        }

        private async Task StopHeldMoveAsync(string reason = null)
        {
            if (heldMove == null || stopping || closing)
                return;

            var active = heldMove;
            var panel = active.Axis.Controller;
            if (reason != null)
            {
                active.Failed = true;
                SetContinuousNotice($"{active.Axis.Name} 轴 · {reason}");
                WriteLog($"{active.Axis.Name} 轴 · {reason}");
            }
            stopping = true;
            CancelHeldMove();
            panel.InvalidateStates();
            UpdateControls();
            try
            {
                bool confirmed = await StopControllerAsync(panel);
                if (!confirmed)
                    SetContinuousNotice($"{active.Axis.Name} 轴未确认停止，请检查设备和操作记录。");
                else if (!active.Failed)
                    SetContinuousNotice($"{active.Axis.Name} 轴停止指令已确认。再次按住可继续移动。");
            }
            finally
            {
                stopping = false;
                UpdateControls();
            }

            await PollBothAsync();
        }

        private void SetContinuousNotice(string message)
        {
            ContinuousNotice.Visibility = ContinuousMode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ContinuousNoticeText.Text = message ?? "按住方向按钮移动，松开停止。该方向限位留空时，按步长逐段移动。";
        }

        private void CancelHeldMove()
        {
            var active = heldMove;
            if (active == null)
                return;

            heldMove = null;
            active.Cancellation.Cancel();
            active.Cancellation.Dispose();
            if (active.Button.IsMouseCaptured)
                active.Button.ReleaseMouseCapture();
        }

        private async Task<bool> MoveAsync(AxisRow axis, int direction, bool continuous, CancellationToken cancellationToken)
        {
            int step;
            MotionProfile profile;
            MotionLimits limits;
            var active = continuous ? heldMove : null;
            try
            {
                bool isZ = axis.Controller == z;
                int? speed = isZ ? ReadNumber(axis.Speed, $"{axis.Name} 轴最高速度", MaxZSpeed)
                    : ReadOptionalNumber(axis.Speed, $"{axis.Name} 轴最高速度");
                int? start = ReadOptionalNumber(axis.StartSpeed, $"{axis.Name} 轴初速度", isZ ? MaxZSpeed : int.MaxValue, isZ);
                profile = new MotionProfile(start, speed, ReadOptionalNumber(axis.Acceleration, $"{axis.Name} 轴加速度"));
                if (!TryReadPosition(axis.Minimum.Text, out int? lower) || !TryReadPosition(axis.Maximum.Text, out int? upper))
                    throw new ArgumentException($"请在设置页输入 {axis.Name} 轴整数软限位。");
                limits = new MotionLimits(lower, upper);
                int? boundary = direction > 0 ? limits.Maximum : limits.Minimum;
                step = continuous && boundary.HasValue ? 1 : ReadNumber(axis.Step, $"{axis.Name} 轴步长");
            }
            catch (ArgumentException ex)
            {
                if (active != null)
                {
                    active.Failed = true;
                    SetContinuousNotice(ex.Message);
                }
                WriteLog(ex.Message);
                return false;
            }

            var panel = axis.Controller;
            string directionLabel = direction > 0 ? axis.PositiveDirection : axis.NegativeDirection;
            if (!continuous)
                panel.Operation = "发送移动…";
            panel.InvalidateStates();
            UpdateControls();
            bool sent = false;
            try
            {
                string speedLabel = profile.MaxSpeed.HasValue ? $"最高速度 {profile.MaxSpeed.Value}" : "沿用当前速度";
                if (continuous)
                {
                    var progress = new Progress<int>(pulses =>
                    {
                        if (heldMove != active || cancellationToken.IsCancellationRequested)
                            return;
                        SetContinuousNotice($"{axis.Name} 轴{directionLabel}，保持按住，松开停止。");
                        WriteLog($"{axis.Name} · {directionLabel}持续运动 · 首段 {pulses:+#;-#} 脉冲 · {speedLabel}");
                    });
                    await panel.Session.MoveWhileHeldAsync(axis.Index, direction, step, profile, limits, cancellationToken, progress);
                }
                else
                {
                    int pulses = await panel.Session.MoveRelativeAsync(axis.Index, direction, step, profile, limits, cancellationToken);
                    WriteLog($"{axis.Name} · {directionLabel}点动 · {pulses:+#;-#} 脉冲 · {speedLabel}");
                }
                sent = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (active != null && heldMove == active)
                {
                    active.Failed = true;
                    SetContinuousNotice($"{axis.Name} 轴 · {ex.Message}");
                }
                WriteLog($"{axis.Name} · {directionLabel} · {ex.Message}");
            }
            finally
            {
                if (!continuous)
                    panel.Operation = null;
                UpdateControls();
            }

            await PollAsync(panel);
            return sent;
        }

        private async Task StopAsync()
        {
            if (stopping || closing)
                return;

            stopping = true;
            CancelHeldMove();
            SetContinuousNotice(null);
            xy.InvalidateStates();
            z.InvalidateStates();
            UpdateControls();
            try
            {
                await Task.WhenAll(StopControllerAsync(xy), StopControllerAsync(z));
            }
            finally
            {
                stopping = false;
                UpdateControls();
            }

            await PollBothAsync();
        }

        private async Task<bool> StopControllerAsync(ControllerPanel panel)
        {
            if (!panel.Session.IsConnected)
                return false;
            try
            {
                await panel.Session.StopAsync();
                WriteLog($"{panel.Name} · 停止指令已确认");
                return true;
            }
            catch (Exception ex)
            {
                WriteLog($"{panel.Name} · 停止未确认 · {ex.Message}");
                return false;
            }
        }

        private async void CloseControllers(object sender, CancelEventArgs args)
        {
            if (canClose)
                return;

            args.Cancel = true;
            if (closing)
                return;

            closing = true;
            CancelHeldMove();
            xy.InvalidateStates();
            z.InvalidateStates();
            pollTimer.Stop();
            UpdateControls();
            bool[] results = await Task.WhenAll(DisconnectOnCloseAsync(xy), DisconnectOnCloseAsync(z));
            if (results.All(success => success))
            {
                SaveSettings();
                canClose = true;
                _ = Dispatcher.BeginInvoke(new Action(Close));
                return;
            }

            closing = false;
            pollTimer.Start();
            UpdateControls();
            MessageBox.Show(this, "停止命令未得到确认，请检查设备和操作记录。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async Task<bool> DisconnectOnCloseAsync(ControllerPanel panel)
        {
            try
            {
                await panel.Session.DisconnectAsync();
                return true;
            }
            catch (Exception ex)
            {
                panel.ConnectionError = "停止未确认";
                WriteLog($"{panel.Name} · {ex.Message}");
                return false;
            }
        }

        private void UpdateControls()
        {
            bool anyConnected = xy.Session.IsConnected || z.Session.IsConnected;
            Simulation.IsEnabled = !anyConnected && !closing && xy.Operation == null && z.Operation == null;
            XyAutoConnect.IsEnabled = !InSimulation && !closing;
            ZAutoConnect.IsEnabled = !InSimulation && !closing;
            ContinuousMode.IsEnabled = heldMove == null && !stopping && !closing;
            Stop.IsEnabled = anyConnected && !stopping && !closing;
            UpdateConnection(xy);
            UpdateConnection(z);
            foreach (var axis in axes)
                UpdateAxis(axis);
        }

        private void UpdateConnection(ControllerPanel panel)
        {
            bool isConnected = panel.Session.IsConnected;
            bool available = !closing && !stopping && heldMove == null && panel.Operation == null;
            panel.Connect.IsEnabled = available;
            panel.Connect.Content = isConnected ? "断开" : "连接";
            foreach (var field in panel.Fields)
                field.IsEnabled = available && !isConnected && !InSimulation;

            panel.Status.Text = panel.Operation ?? (panel.ReadError != null ? "读取失败" : panel.ConnectionError)
                ?? (isConnected ? (InSimulation ? "模拟已连接" : "已连接") : "未连接");
            panel.Status.Foreground = panel.Operation != null ? busy : panel.ReadError != null || panel.ConnectionError != null
                ? attention : isConnected ? connected : neutral;
        }

        private void UpdateAxis(AxisRow axis)
        {
            var panel = axis.Controller;
            var state = panel.States?[axis.Index];
            axis.Position.Text = state?.Position?.ToString("N0", CultureInfo.InvariantCulture) ?? "—";
            axis.Status.Foreground = neutral;
            if (state == null)
            {
                axis.Status.Text = panel.ReadError != null ? "读取失败" : panel.Session.IsConnected ? "等待状态" : "未连接";
                axis.Minus.IsEnabled = heldMove?.Button == axis.Minus;
                axis.Plus.IsEnabled = heldMove?.Button == axis.Plus;
                return;
            }

            string text = state.Moving.HasValue ? (state.Moving.Value ? "移动中" : "已停止")
                : state.Position.HasValue ? "位置已读取" : "等待位置回复";
            if (state.PositiveLimit == true)
                text += " · 正限位";
            if (state.NegativeLimit == true)
                text += " · 负限位";
            if (state.Alarm == true)
                text += " · 报警";
            axis.Status.Text = text;
            if (state.Alarm == true || state.PositiveLimit == true || state.NegativeLimit == true)
                axis.Status.Foreground = attention;
            else if (state.Moving == true)
                axis.Status.Foreground = busy;

            bool ready = !closing && !stopping && heldMove == null && panel.Operation == null && state.Moving != true && state.Alarm != true
                && panel.States.All(value => value.Position.HasValue);
            axis.Minus.IsEnabled = heldMove?.Button == axis.Minus || (ready && state.NegativeLimit != true);
            axis.Plus.IsEnabled = heldMove?.Button == axis.Plus || (ready && state.PositiveLimit != true);
        }

        private void WriteLog(string message)
        {
            if (Log.Items.Count >= 150)
                Log.Items.RemoveAt(0);

            string entry = $"{DateTime.Now:HH:mm:ss}   {message}";
            Log.Items.Add(entry);
            LogEmpty.Visibility = Visibility.Collapsed;
            CopyLog.Content = "复制记录";
            CopyLog.IsEnabled = true;
            Log.ScrollIntoView(entry);
        }

        private void CopyOperationLog()
        {
            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, Log.Items.Cast<string>()));
                CopyLog.Content = "已复制";
            }
            catch (ExternalException)
            {
                WriteLog("剪贴板暂不可用，请重试复制。");
            }
        }

        private static int ReadNumber(TextBox field, string name, int maximum = int.MaxValue)
        {
            if (TryReadNumber(field.Text, maximum, out int value))
                return value;

            field.Focus();
            field.SelectAll();
            throw new ArgumentException($"{name}请输入 1 至 {maximum} 的整数。");
        }

        private static int? ReadOptionalNumber(TextBox field, string name, int maximum = int.MaxValue, bool allowZero = false)
        {
            if (string.IsNullOrWhiteSpace(field.Text))
                return null;
            if (allowZero && NormalizeOptionalNumber(field.Text, maximum, true) == "0")
                return 0;
            return ReadNumber(field, name, maximum);
        }

        private static bool TryReadNumber(string text, int maximum, out int value)
        {
            return int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value)
                && value > 0 && value <= maximum;
        }

        private static Brush CreateBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private sealed class ControllerPanel
        {
            public ControllerPanel(string name, int axisCount, Button connect, TextBlock status, params Control[] fields)
            {
                Name = name;
                AxisCount = axisCount;
                Session = new MotionSession(axisCount);
                Connect = connect;
                Status = status;
                Fields = fields;
            }

            public readonly string Name;
            public readonly int AxisCount;
            public readonly MotionSession Session;
            public readonly Button Connect;
            public readonly TextBlock Status;
            public readonly Control[] Fields;
            public AxisState[] States;
            public string Operation;
            public string ReadError;
            public string ConnectionError;
            public bool Polling;
            public int Revision;

            public void InvalidateStates()
            {
                States = null;
                Revision++;
            }
        }

        private sealed class AxisRow
        {
            public AxisRow(string name, ControllerPanel controller, int index, TextBlock position, TextBlock status,
                TextBox step, TextBox speed, TextBox startSpeed, TextBox acceleration, TextBox minimum, TextBox maximum,
                Button minus, Button plus, string negativeDirection, string positiveDirection)
            {
                Name = name;
                Controller = controller;
                Index = index;
                Position = position;
                Status = status;
                Step = step;
                Speed = speed;
                StartSpeed = startSpeed;
                Acceleration = acceleration;
                Minimum = minimum;
                Maximum = maximum;
                Fields = new Control[] { step, speed, startSpeed, acceleration, minimum, maximum };
                Minus = minus;
                Plus = plus;
                NegativeDirection = negativeDirection;
                PositiveDirection = positiveDirection;
                Minus.Content = $"− {NegativeDirection}";
                Plus.Content = $"+ {PositiveDirection}";
                AutomationProperties.SetName(Minus, $"{Name} 轴负向，{NegativeDirection}");
                AutomationProperties.SetName(Plus, $"{Name} 轴正向，{PositiveDirection}");
            }

            public readonly string Name;
            public readonly ControllerPanel Controller;
            public readonly int Index;
            public readonly TextBlock Position;
            public readonly TextBlock Status;
            public readonly TextBox Step;
            public readonly TextBox Speed;
            public readonly TextBox StartSpeed;
            public readonly TextBox Acceleration;
            public readonly TextBox Minimum;
            public readonly TextBox Maximum;
            public readonly Control[] Fields;
            public readonly Button Minus;
            public readonly Button Plus;
            public readonly string NegativeDirection;
            public readonly string PositiveDirection;
        }

        private sealed class HeldMove
        {
            public HeldMove(AxisRow axis, Button button, int direction)
            {
                Axis = axis;
                Button = button;
                Direction = direction;
            }

            public readonly AxisRow Axis;
            public readonly Button Button;
            public readonly int Direction;
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public bool Failed;
        }
    }
}
