using ProjectARVRPro;
using ProjectARVRPro.SemiAuto.Automation;
using ProjectARVRPro.SemiAuto.GECS;
using ProjectARVRPro.Process.W51;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ProjectARVRPro.SemiAuto
{
    public partial class MainWindow : Window
    {
        private ParsedProjectArvrResult _currentResult;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private JsonStreamMessageReader _messageReader;
        private readonly HashSet<string> _confirmedMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _executedPgMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<PgActionMapping> _pgMappings = new ObservableCollection<PgActionMapping>();
        private readonly GecsClient _gecsClient = new GecsClient();
        private readonly SemiAutomaticWorkflow _semiAutomaticWorkflow;
        private readonly DispatcherTimer _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private PendingArvrAction _pendingArvrAction;
        private DateTime _nextHeartbeatUtc = DateTime.MaxValue;
        private int _pgActionRunning;

        public MainWindow()
        {
            InitializeComponent();
            SerialNumberTextBox.Text = "SN-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            ContractTextBox.Text = BuildContractSummary();
            FieldGuideTextBox.Text = OpticalParameterDescriptions.BuildGuideText();
            PgMappingsGrid.ItemsSource = _pgMappings;
            _semiAutomaticWorkflow = new SemiAutomaticWorkflow(_gecsClient);
            UpdateConnectionSummaries();
            SetArvrConnectionState(false);
            SetPgConnectionState(false);
            SetPendingControls(null);
            SetWorkflowStatus("待机", false);
            _gecsClient.Log += message => AppendLog("PG " + message);
            _heartbeatTimer.Tick += HeartbeatTimer_Tick;
            _heartbeatTimer.Start();
            Loaded += delegate
            {
                LoadInitialProfile();
                LoadSample("project-arvr-result.json");
            };
            Closed += delegate
            {
                _heartbeatTimer.Stop();
                Disconnect();
                _gecsClient.Dispose();
            };
        }

        private async void ConnectInitButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectAsync("init");
        }

        private async void ConnectOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectAsync(null);
        }

        private async void ConnectRunAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectAsync("runall");
        }

        private async void SendCommandButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string eventName = GetSelectedCommandEventName();
                string requestParams = eventName == "GetProcessEnable" ? string.Empty : CommandParamsTextBox.Text.Trim();
                if ((eventName == "SwitchGroup" || eventName == "SetProcessEnable") && string.IsNullOrWhiteSpace(requestParams))
                {
                    MessageBox.Show(this, eventName + " 需要填写 Params。", "缺少参数", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await SendEventAsync(eventName, requestParams);
            }
            catch (Exception ex)
            {
                AppendLog("Send command failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "发送失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void SwitchPgCompleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingArvrAction != null && string.Equals(_pendingArvrAction.EventName, "SwitchPG", StringComparison.OrdinalIgnoreCase))
                await ConfirmPendingArvrAsync();
            else
                await SendEventAsync("SwitchPGCompleted");
        }

        private async void AoiCompleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingArvrAction != null && string.Equals(_pendingArvrAction.EventName, "AoiSwitchPG", StringComparison.OrdinalIgnoreCase))
                await ConfirmPendingArvrAsync();
            else
                await SendEventAsync("AOITestSwitchImageComplete");
        }

        private async void ConnectPgButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectPgAsync();
        }

        private void DisconnectPgButton_Click(object sender, RoutedEventArgs e)
        {
            _gecsClient.Disconnect();
            _nextHeartbeatUtc = DateTime.MaxValue;
            SetPgConnectionState(false);
            SetWorkflowStatus(_networkStream == null ? "待机" : "ARVR 已连接", _networkStream != null);
            AppendLog("PG disconnected.");
        }

        private void ConnectionSettingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateConnectionSummaries();
        }

        private void LoadPgProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadProfile(PgProfilePathTextBox.Text.Trim());
            }
            catch (Exception ex)
            {
                AppendLog("Load profile failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "加载配置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SavePgProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommitPgMappingEdits();
                string path = PgProfilePathTextBox.Text.Trim();
                IntegrationProfileStore.Save(path, CreateProfileFromUi());
                PgProfilePathTextBox.Text = Path.GetFullPath(path);
                AppendLog("配置已保存。");
            }
            catch (Exception ex)
            {
                AppendLog("Save profile failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "保存配置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ExecutePgAndConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecutePendingPgActionAsync(true);
        }

        private async void ExecutePgOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecutePendingPgActionAsync(false);
        }

        private async void ConfirmPendingArvrButton_Click(object sender, RoutedEventArgs e)
        {
            await ConfirmPendingArvrAsync();
        }

        private void AddPgMappingButton_Click(object sender, RoutedEventArgs e)
        {
            var mapping = new PgActionMapping { Enabled = false };
            _pgMappings.Add(mapping);
            PgMappingsGrid.SelectedItem = mapping;
            PgMappingsGrid.ScrollIntoView(mapping);
        }

        private void DeletePgMappingButton_Click(object sender, RoutedEventArgs e)
        {
            PgActionMapping selected = PgMappingsGrid.SelectedItem as PgActionMapping;
            if (selected != null)
                _pgMappings.Remove(selected);
        }

        private async void SendManualPgCommandButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IntegrationProfile profile = CreateProfileFromUi();
                var mapping = new PgActionMapping
                {
                    EventName = "Manual",
                    ArvrTestType = "*",
                    CommandTemplate = PgManualCommandTextBox.Text.Trim(),
                    SuccessContains = PgManualSuccessTextBox.Text.Trim()
                };
                string command = profile.ExpandCommand(mapping, string.Empty, GetSerialNumber());
                if (!_gecsClient.IsConnected && !await ConnectPgAsync())
                    return;

                GecsCommandResult result = await _gecsClient.SendCommandAsync(
                    command,
                    mapping.SuccessContains,
                    checked((byte)profile.NetworkNumber),
                    TimeSpan.FromSeconds(profile.PgResponseTimeoutSeconds));
                AppendLog(result.IsSuccess ? "PG manual command succeeded: " + result.ResponseText : "PG manual command failed: " + result.ErrorMessage);
            }
            catch (Exception ex)
            {
                AppendLog("PG manual command failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "PG 指令失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
            AppendLog("Disconnected.");
        }

        private void LoadSampleButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSample("project-arvr-result.json");
        }

        private void OpenJsonButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) == true)
                LoadJson(File.ReadAllText(dialog.FileName, System.Text.Encoding.UTF8));
        }

        private void SaveCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResult == null)
            {
                MessageBox.Show(this, "请先加载或接收 ProjectARVRResult。", "无结果", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", FileName = "ProjectARVRResult_items.csv" };
            if (dialog.ShowDialog(this) == true)
            {
                ResultParser.WriteCsv(dialog.FileName, _currentResult.Items);
                AppendLog("Saved CSV: " + dialog.FileName);
            }
        }

        private async Task ConnectAsync(string mode)
        {
            try
            {
                await OpenConnectionAsync();
                if (!string.IsNullOrEmpty(mode))
                    await SendEventAsync(mode == "runall" ? "RunAll" : "ProjectARVRInit");
                _ = ReceiveLoopAsync();
            }
            catch (Exception ex)
            {
                AppendLog("Connect failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                Disconnect();
            }
        }

        private async Task OpenConnectionAsync()
        {
            Disconnect();
            _tcpClient = new TcpClient();
            string host = HostTextBox.Text.Trim();
            int port = ParsePositiveInt(PortTextBox.Text, "Port");
            await _tcpClient.ConnectAsync(host, port);
            _networkStream = _tcpClient.GetStream();
            _messageReader = new JsonStreamMessageReader(_networkStream);
            _confirmedMessages.Clear();
            SetArvrConnectionState(true);
            SetWorkflowStatus(_gecsClient.IsConnected ? "就绪" : "ARVR 已连接", true);
            AppendLog("Connected " + host + ":" + port.ToString(CultureInfo.InvariantCulture));
        }

        private async Task ReceiveLoopAsync()
        {
            int maxMessages = GetMaxMessages();
            TimeSpan timeout = TimeSpan.FromSeconds(GetTimeoutSeconds());

            try
            {
                for (int messageIndex = 0; messageIndex < maxMessages && _messageReader != null; messageIndex++)
                {
                    string json = await _messageReader.ReadMessageAsync(timeout);
                    if (json == null)
                    {
                        _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Connection closed by server.")));
                        break;
                    }

                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Received: " + json)));

                    Dictionary<string, object> root = ResultParser.DeserializeDictionary(json);
                    string eventName = ResultParser.GetString(root, "EventName");

                    if (eventName == "ProjectARVRResult")
                    {
                        _ = Dispatcher.BeginInvoke(new Action(() => SetWorkflowStatus("已完成", true)));
                        _ = Dispatcher.BeginInvoke(new Action(() => LoadReceivedJson(json)));
                        break;
                    }

                    if (eventName == "SwitchPG")
                    {
                        await HandleArvrSwitchRequestAsync(root, "SwitchPG", "SwitchPGCompleted");
                        if (GetCheckBoxValue(AutoSwitchPgCheckBox) && !GetCheckBoxValue(AutoExecutePgCheckBox))
                            await ConfirmPendingArvrAsync();
                        continue;
                    }

                    if (eventName == "AoiSwitchPG")
                    {
                        await HandleArvrSwitchRequestAsync(root, "AoiSwitchPG", "AOITestSwitchImageComplete");
                        if (GetCheckBoxValue(AutoAoiCheckBox) && !GetCheckBoxValue(AutoExecutePgCheckBox))
                            await ConfirmPendingArvrAsync();
                    }
                }
            }
            catch (TimeoutException ex)
            {
                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Receive timeout: " + ex.Message)));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException ex)
            {
                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Receive stopped: " + ex.Message)));
            }
            catch (Exception ex)
            {
                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Receive failed: " + ex.Message)));
            }
        }

        private async Task HandleArvrSwitchRequestAsync(Dictionary<string, object> root, string eventName, string replyEventName)
        {
            PendingArvrAction pending = Dispatcher.CheckAccess()
                ? CreatePendingArvrAction(root, eventName, replyEventName)
                : (PendingArvrAction)Dispatcher.Invoke(new Func<PendingArvrAction>(() => CreatePendingArvrAction(root, eventName, replyEventName)));

            _pendingArvrAction = pending;
            SetPendingControls(pending);
            SetWorkflowStatus(pending.Mapping == null ? "未配置映射" : "待执行", pending.Mapping != null);
            string mappingText = pending.Mapping == null
                ? "未找到启用映射"
                : pending.Mapping.Name + " / " + pending.Mapping.CommandTemplate;
            AppendLog("ARVR requested " + eventName + ", ARVRTestType=" + pending.ArvrTestType + ", " + mappingText);

            if (GetCheckBoxValue(AutoExecutePgCheckBox))
            {
                if (pending.Mapping == null)
                {
                    SetWorkflowStatus("未配置映射", false);
                    AppendLog("PG 自动执行已停止：当前请求没有启用映射，未确认 ARVR。 ");
                    return;
                }

                await ExecutePendingPgActionAsync(GetCheckBoxValue(AutoConfirmAfterPgCheckBox));
            }
        }

        private PendingArvrAction CreatePendingArvrAction(Dictionary<string, object> root, string eventName, string replyEventName)
        {
            Dictionary<string, object> data = ResultParser.GetDictionary(root, "Data");
            string testType = data == null ? string.Empty : ResultParser.GetString(data, "ARVRTestType");
            string serialNumber = ResultParser.GetString(root, "SerialNumber");
            if (string.IsNullOrWhiteSpace(serialNumber))
                serialNumber = GetSerialNumber();

            IntegrationProfile profile = CreateProfileFromUi();
            PgActionMapping mapping = profile.FindMapping(eventName, testType);
            var pending = new PendingArvrAction
            {
                Root = root,
                EventName = eventName,
                ReplyEventName = replyEventName,
                ArvrTestType = testType,
                SerialNumber = serialNumber,
                Key = ArvrClient.BuildMessageKey(root, eventName),
                Mapping = mapping == null ? null : mapping.Clone()
            };

            PendingPgActionTextBlock.Text = mapping == null
                ? eventName + " / TestType=" + DisplayValue(testType) + " / 未找到启用映射"
                : eventName + " / TestType=" + DisplayValue(testType) + " / " + mapping.Name + " / " + profile.ExpandCommand(mapping, testType, serialNumber);
            return pending;
        }

        private async Task ExecutePendingPgActionAsync(bool confirmOnSuccess)
        {
            if (Interlocked.Exchange(ref _pgActionRunning, 1) != 0)
            {
                AppendLog("PG action is already running.");
                return;
            }

            try
            {
                PendingArvrAction pending = _pendingArvrAction;
                if (pending == null)
                    throw new InvalidOperationException("当前没有待处理的 ARVR 切图请求。 ");
                if (pending.Mapping == null)
                    throw new InvalidOperationException("当前 ARVR 切图请求没有启用的 PG 映射。 ");

                if (_executedPgMessages.Contains(pending.Key))
                {
                    AppendLog("PG command already succeeded for this ARVR request; command will not be repeated.");
                    if (confirmOnSuccess)
                        await ConfirmPendingArvrAsync();
                    return;
                }

                IntegrationProfile profile = Dispatcher.CheckAccess()
                    ? CreateProfileFromUi()
                    : (IntegrationProfile)Dispatcher.Invoke(new Func<IntegrationProfile>(CreateProfileFromUi));
                string command = profile.ExpandCommand(pending.Mapping, pending.ArvrTestType, pending.SerialNumber);
                if (!_gecsClient.IsConnected && !await ConnectPgAsync())
                    return;

                SetWorkflowStatus("执行中", true);
                AppendLog("Executing mapped PG command: " + command);
                SemiAutomaticWorkflowResult result = await _semiAutomaticWorkflow.ExecuteAsync(
                    profile,
                    pending.Mapping,
                    pending.ArvrTestType,
                    pending.SerialNumber,
                    confirmOnSuccess,
                    ConfirmPendingArvrAsync,
                    () => _executedPgMessages.Add(pending.Key));
                if (!result.PgSucceeded)
                {
                    SetWorkflowStatus("PG 失败", false);
                    SetPgConnectionState(_gecsClient.IsConnected);
                    AppendLog("PG mapping failed; ARVR was not confirmed. " + result.ErrorMessage);
                    return;
                }

                AppendLog("PG mapping succeeded: " + result.PgResponseText);
                if (!result.ArvrConfirmed && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    SetWorkflowStatus("确认失败", false);
                    AppendLog("PG succeeded, but ARVR confirmation failed: " + result.ErrorMessage);
                }
                else if (!confirmOnSuccess)
                {
                    SetWorkflowStatus("待确认", true);
                    AppendLog("PG succeeded; waiting for operator to confirm ARVR.");
                }
                else
                    SetWorkflowStatus("已确认", true);
            }
            catch (Exception ex)
            {
                AppendLog("PG action failed; ARVR was not confirmed. " + ex.Message);
                if (Dispatcher.CheckAccess())
                    MessageBox.Show(this, ex.Message, "PG 联动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Interlocked.Exchange(ref _pgActionRunning, 0);
            }
        }

        private async Task<bool> ConfirmPendingArvrAsync()
        {
            PendingArvrAction pending = _pendingArvrAction;
            if (pending == null)
            {
                AppendLog("Cannot confirm ARVR: no pending switch request.");
                return false;
            }

            bool confirmed = await SendConfirmOnceAsync(pending.Root, pending.EventName, pending.ReplyEventName, pending.Key);
            SetWorkflowStatus(confirmed ? "已确认" : "确认失败", confirmed);
            return confirmed;
        }

        private async Task<bool> ConnectPgAsync()
        {
            try
            {
                IntegrationProfile profile = Dispatcher.CheckAccess()
                    ? CreateProfileFromUi()
                    : (IntegrationProfile)Dispatcher.Invoke(new Func<IntegrationProfile>(CreateProfileFromUi));
                await _gecsClient.ConnectAsync(profile.PgHost, profile.PgPort);
                ScheduleNextHeartbeat(profile.HeartbeatSeconds);
                SetPgConnectionState(true);
                SetWorkflowStatus(_networkStream == null ? "PG 已连接" : "就绪", true);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("PG connect failed: " + ex.Message);
                SetPgConnectionState(false);
                SetWorkflowStatus("PG 连接失败", false);
                if (Dispatcher.CheckAccess())
                    MessageBox.Show(this, ex.Message, "PG 连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private async void HeartbeatTimer_Tick(object sender, EventArgs e)
        {
            if (!_gecsClient.IsConnected || DateTime.UtcNow < _nextHeartbeatUtc)
                return;

            try
            {
                int heartbeatSeconds = ParseNonNegativeInt(PgHeartbeatTextBox.Text, "心跳间隔");
                if (heartbeatSeconds <= 0)
                {
                    _nextHeartbeatUtc = DateTime.MaxValue;
                    return;
                }

                int networkNumber = ParseNonNegativeInt(PgNetworkTextBox.Text, "Network Number");
                if (networkNumber > byte.MaxValue)
                    throw new ArgumentException("Network Number 必须在 0 到 255 之间。");

                bool sent = await _gecsClient.TrySendHeartbeatAsync(checked((byte)networkNumber));
                ScheduleNextHeartbeat(sent ? heartbeatSeconds : 1);
            }
            catch (Exception ex)
            {
                AppendLog("PG heartbeat stopped: " + ex.Message);
                SetPgConnectionState(false);
                SetWorkflowStatus("PG 已断开", false);
                _nextHeartbeatUtc = DateTime.MaxValue;
            }
        }

        private void ScheduleNextHeartbeat(int seconds)
        {
            _nextHeartbeatUtc = seconds <= 0 ? DateTime.MaxValue : DateTime.UtcNow.AddSeconds(seconds);
        }

        private void LoadInitialProfile()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string userProfilePath = Path.Combine(baseDirectory, "semi-auto-profile.json");
            string sampleProfilePath = Path.Combine(baseDirectory, "Profiles", "semi-auto-profile.sample.json");
            try
            {
                if (File.Exists(userProfilePath))
                    LoadProfile(userProfilePath);
                else if (File.Exists(sampleProfilePath))
                {
                    LoadProfile(sampleProfilePath);
                    PgProfilePathTextBox.Text = userProfilePath;
                    AppendLog("示例配置已加载，指令映射默认关闭。");
                }
                else
                {
                    PgProfilePathTextBox.Text = userProfilePath;
                    ApplyProfileToUi(new IntegrationProfile());
                    AppendLog("未找到示例配置，已使用默认配置。");
                }
            }
            catch (Exception ex)
            {
                PgProfilePathTextBox.Text = userProfilePath;
                ApplyProfileToUi(new IntegrationProfile());
                AppendLog("配置加载失败：" + ex.Message);
            }
        }

        private void LoadProfile(string filePath)
        {
            IntegrationProfile profile = IntegrationProfileStore.Load(filePath);
            ApplyProfileToUi(profile);
            PgProfilePathTextBox.Text = Path.GetFullPath(filePath);
            AppendLog("配置已加载。");
        }

        private void ApplyProfileToUi(IntegrationProfile profile)
        {
            HostTextBox.Text = profile.ArvrHost;
            PortTextBox.Text = profile.ArvrPort.ToString(CultureInfo.InvariantCulture);
            PgHostTextBox.Text = profile.PgHost;
            PgPortTextBox.Text = profile.PgPort.ToString(CultureInfo.InvariantCulture);
            PgNetworkTextBox.Text = profile.NetworkNumber.ToString(CultureInfo.InvariantCulture);
            PgChannelTextBox.Text = profile.Channel;
            PgTimeoutTextBox.Text = profile.PgResponseTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            PgHeartbeatTextBox.Text = profile.HeartbeatSeconds.ToString(CultureInfo.InvariantCulture);
            AutoExecutePgCheckBox.IsChecked = profile.AutoExecuteMappedPgCommand;
            AutoConfirmAfterPgCheckBox.IsChecked = profile.ConfirmArvrAfterPgSuccess;
            _pgMappings.Clear();
            foreach (PgActionMapping mapping in profile.Mappings ?? new List<PgActionMapping>())
                _pgMappings.Add(mapping);
            UpdateConnectionSummaries();
        }

        private IntegrationProfile CreateProfileFromUi()
        {
            CommitPgMappingEdits();
            var profile = new IntegrationProfile
            {
                ArvrHost = HostTextBox.Text.Trim(),
                ArvrPort = ParsePositiveInt(PortTextBox.Text, "ARVR Port"),
                PgHost = PgHostTextBox.Text.Trim(),
                PgPort = ParsePositiveInt(PgPortTextBox.Text, "PG Port"),
                NetworkNumber = ParseNonNegativeInt(PgNetworkTextBox.Text, "Network Number"),
                Channel = PgChannelTextBox.Text.Trim(),
                PgResponseTimeoutSeconds = ParsePositiveInt(PgTimeoutTextBox.Text, "PG 响应超时"),
                HeartbeatSeconds = ParseNonNegativeInt(PgHeartbeatTextBox.Text, "心跳间隔"),
                AutoExecuteMappedPgCommand = AutoExecutePgCheckBox.IsChecked == true,
                ConfirmArvrAfterPgSuccess = AutoConfirmAfterPgCheckBox.IsChecked == true,
                Mappings = _pgMappings.Select(mapping => mapping.Clone()).ToList()
            };
            profile.Validate();
            return profile;
        }

        private void CommitPgMappingEdits()
        {
            PgMappingsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            PgMappingsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private static string DisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
        }

        private void UpdateConnectionSummaries()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(UpdateConnectionSummaries));
                return;
            }
            if (ArvrEndpointSummaryTextBlock == null || HostTextBox == null || PortTextBox == null ||
                PgEndpointSummaryTextBlock == null || PgHostTextBox == null || PgPortTextBox == null || PgChannelTextBox == null)
                return;

            ArvrEndpointSummaryTextBlock.Text = HostTextBox.Text.Trim() + ":" + PortTextBox.Text.Trim();
            PgEndpointSummaryTextBlock.Text = PgHostTextBox.Text.Trim() + ":" + PgPortTextBox.Text.Trim() + " · CH " + PgChannelTextBox.Text.Trim();
        }

        private void SetArvrConnectionState(bool connected)
        {
            if (ArvrStatusTextBlock == null)
                return;
            ArvrStatusTextBlock.Text = connected ? "已连接" : "未连接";
            ArvrStatusTextBlock.Foreground = GetStatusBrush(connected);
        }

        private void SetPgConnectionState(bool connected)
        {
            if (PgStatusTextBlock == null)
                return;
            PgStatusTextBlock.Text = connected ? "已连接" : "未连接";
            PgStatusTextBlock.Foreground = GetStatusBrush(connected);
        }

        private void SetWorkflowStatus(string text, bool positive)
        {
            if (WorkflowStatusTextBlock == null)
                return;
            WorkflowStatusTextBlock.Text = text;
            WorkflowStatusTextBlock.Foreground = positive
                ? GetStatusBrush(true)
                : string.Equals(text, "待机", StringComparison.Ordinal) ? GetStatusBrush(false) : (Brush)FindResource("WarningBrush");
        }

        private void SetPendingControls(PendingArvrAction pending)
        {
            if (PendingPgActionTextBlock == null)
                return;

            bool hasPending = pending != null;
            bool canExecute = hasPending && pending.Mapping != null;
            if (!hasPending)
                PendingPgActionTextBlock.Text = "无待处理请求";
            ExecutePgAndConfirmButton.IsEnabled = canExecute;
            ExecutePgOnlyButton.IsEnabled = canExecute;
            ConfirmPendingArvrButton.IsEnabled = hasPending;
        }

        private Brush GetStatusBrush(bool connected)
        {
            return (Brush)FindResource(connected ? "SuccessBrush" : "SecondaryTextBrush");
        }

        private async Task<bool> SendConfirmOnceAsync(Dictionary<string, object> root, string sourceEventName, string replyEventName, string messageKey = null)
        {
            if (_networkStream == null)
            {
                AppendLog("Cannot confirm " + sourceEventName + ": ARVR is not connected.");
                return false;
            }

            string serialNumber = ResultParser.GetString(root, "SerialNumber");
            if (string.IsNullOrWhiteSpace(serialNumber))
                serialNumber = GetSerialNumber();

            string json = await ArvrClient.SendConfirmOnceAsync(_networkStream, _confirmedMessages, root, sourceEventName, replyEventName, serialNumber, messageKey);
            if (!string.IsNullOrEmpty(json))
                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Sent: " + json)));
            else
                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Skipped duplicate " + sourceEventName + " confirmation.")));
            return true;
        }

        private async Task SendEventAsync(string eventName, string requestParams = "")
        {
            if (_networkStream == null)
            {
                AppendLog("Cannot send " + eventName + ": not connected.");
                return;
            }

            string json = await ArvrClient.SendRequestAsync(_networkStream, eventName, GetSerialNumber(), requestParams);
            AppendLog("Sent: " + json);
        }

        private string GetSelectedCommandEventName()
        {
            var selectedItem = CommandEventNameComboBox.SelectedItem as ComboBoxItem;
            string eventName = selectedItem == null ? string.Empty : Convert.ToString(selectedItem.Content, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(eventName))
                throw new InvalidOperationException("请选择同步命令。");
            return eventName;
        }

        private void Disconnect()
        {
            try
            {
                if (_networkStream != null)
                    _networkStream.Dispose();
                if (_tcpClient != null)
                    _tcpClient.Close();
            }
            catch
            {
            }
            finally
            {
                _networkStream = null;
                _messageReader = null;
                _tcpClient = null;
                _confirmedMessages.Clear();
                _executedPgMessages.Clear();
                _pendingArvrAction = null;
                if (Dispatcher.CheckAccess())
                {
                    SetArvrConnectionState(false);
                    SetPendingControls(null);
                    SetWorkflowStatus(_gecsClient.IsConnected ? "PG 已连接" : "待机", _gecsClient.IsConnected);
                }
            }
        }

        private void LoadSample(string fileName)
        {
            string filePath = FindSamplePath(fileName);
            if (string.IsNullOrEmpty(filePath))
            {
                AppendLog("Sample not found: " + fileName);
                return;
            }

            LoadJson(File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            AppendLog("示例结果已加载。");
        }

        private static string FindSamplePath(string fileName)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDirectory, "Samples", fileName),
                Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "Samples", fileName)),
                Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Samples", fileName))
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private void LoadJson(string json)
        {
            try
            {
                ShowParsedResult(ResultParser.Parse(json));
            }
            catch (Exception ex)
            {
                AppendLog("Parse failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "解析失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadReceivedJson(string json)
        {
            try
            {
                ParsedProjectArvrResult parsed = ResultParser.ParseJson(json, GetOutputDirectory());
                ShowParsedResult(parsed);
                AppendLog("Saved JSON: " + parsed.SavedJsonPath);
                AppendLog("Saved CSV : " + parsed.SavedCsvPath);
            }
            catch (Exception ex)
            {
                AppendLog("Parse/export failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "解析或导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowParsedResult(ParsedProjectArvrResult parsed)
        {
            _currentResult = parsed;
            RawJsonTextBox.Text = parsed.RawJson;
            ItemsGrid.ItemsSource = null;
            ItemsGrid.ItemsSource = parsed.Items;
            SummaryTextBlock.Text = string.Format(CultureInfo.InvariantCulture,
                "EventName: {0}\r\nSN: {1}\r\nCode: {2}\r\nMsg: {3}\r\nTotalResult: {4}\r\nTotalResultString: {5}\r\nItems: {6}",
                parsed.EventName,
                parsed.SerialNumber,
                parsed.Code.HasValue ? parsed.Code.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                parsed.Msg,
                parsed.TotalResult.HasValue ? parsed.TotalResult.Value.ToString() : string.Empty,
                parsed.TotalResultString,
                parsed.Items.Count);
            UpdateW51(parsed.W51TestResult);
        }

        private void UpdateW51(W51TestResult w51)
        {
            W51HorizontalTextBox.Text = FormatItem(w51 == null ? null : w51.HorizontalFieldOfViewAngle);
            W51VerticalTextBox.Text = FormatItem(w51 == null ? null : w51.VerticalFieldOfViewAngle);
            W51DiagonalTextBox.Text = FormatItem(w51 == null ? null : w51.DiagonalFieldOfViewAngle);
        }

        private void AppendLog(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog(message)));
                return;
            }

            LogTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + message + Environment.NewLine);
            LogTextBox.ScrollToEnd();
        }

        private static string FormatItem(ObjectiveTestItem item)
        {
            if (item == null)
                return "未包含";

            string result = item.TestResult ? "PASS" : "FAIL";
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}  [{2}, {3}]  {4}", item.Value, item.Unit, item.LowLimit, item.UpLimit, result);
        }

        private int GetTimeoutSeconds()
        {
            return ParsePositiveInt(TimeoutTextBox.Text, "超时秒数");
        }

        private int GetMaxMessages()
        {
            return ParsePositiveInt(MaxMessagesTextBox.Text, "消息上限");
        }

        private string GetOutputDirectory()
        {
            string outputDirectory = OutputDirectoryTextBox.Text.Trim();
            return string.IsNullOrWhiteSpace(outputDirectory) ? "output" : outputDirectory;
        }

        private string GetSerialNumber()
        {
            return Dispatcher.CheckAccess() ? SerialNumberTextBox.Text.Trim() : (string)Dispatcher.Invoke(new Func<string>(GetSerialNumber));
        }

        private bool GetCheckBoxValue(System.Windows.Controls.CheckBox checkBox)
        {
            return Dispatcher.CheckAccess() ? checkBox.IsChecked == true : (bool)Dispatcher.Invoke(new Func<bool>(() => GetCheckBoxValue(checkBox)));
        }

        private static int ParsePositiveInt(string value, string name)
        {
            int number;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number <= 0)
                throw new ArgumentException(name + "必须是大于 0 的整数。");
            return number;
        }

        private static int ParseNonNegativeInt(string value, string name)
        {
            int number;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) || number < 0)
                throw new ArgumentException(name + "必须是大于或等于 0 的整数。");
            return number;
        }

        private static string BuildContractSummary()
        {
            return "public class ObjectiveTestResult : ViewModelBase\r\n" +
                   "{\r\n" +
                   "    public W51TestResult W51TestResult { get; set; }\r\n" +
                   "    public W255TestResult W255TestResult { get; set; }\r\n" +
                   "    public BlackTestResult BlackTestResult { get; set; }\r\n" +
                   "    public Dictionary<string, FieldOfViewTestResult> FieldOfViewTestResults { get; set; }\r\n" +
                   "    public Dictionary<string, LuminanceChromaticityTestResult> LuminanceChromaticityTestResults { get; set; }\r\n" +
                   "    public Dictionary<string, LuminanceChromaticityYWTestResult> LuminanceChromaticityYWTestResults { get; set; }\r\n" +
                   "    public ChessboardTestResult ChessboardTestResult { get; set; }\r\n" +
                   "    public Dictionary<string, ChessboardTestResult> ChessboardTestResults { get; set; }\r\n" +
                   "    public MTFHVTestResult MTFHVTestResult { get; set; }\r\n" +
                   "    public List<MTFHV048TestResult> MTFHV048TestResults { get; set; }\r\n" +
                   "    public List<MTFHV058TestResult> MTFHV058TestResults { get; set; }\r\n" +
                   "    public Dictionary<string, MTFHV058TestResult> DynamicMTFHV058TestResults { get; set; }\r\n" +
                   "    public Dictionary<string, MTFH07TestResult> MTFH07TestResults { get; set; }\r\n" +
                   "    public Dictionary<string, MTFV07TestResult> MTFV07TestResults { get; set; }\r\n" +
                   "    public DistortionTestResult DistortionTestResult { get; set; }\r\n" +
                   "    public OpticCenterTestResult OpticCenterTestResult { get; set; }\r\n" +
                   "    public Dictionary<string, ObservableCollection<ObjectiveTestItem>> DynamicTestResults { get; set; }\r\n" +
                   "    public Dictionary<string, ObservableCollection<PoixyuvData>> DynamicPoixyuvDatas { get; set; }\r\n" +
                   "    public Dictionary<string, ScreenDefectsData> DynamicScreenDefectResults { get; set; }\r\n" +
                   "    public string Msg { get; set; }\r\n" +
                   "    public bool TotalResult { get; set; }\r\n" +
                   "    public string TotalResultString { get; }\r\n" +
                   "}";
        }

        private sealed class PendingArvrAction
        {
            public Dictionary<string, object> Root { get; set; }

            public string EventName { get; set; }

            public string ReplyEventName { get; set; }

            public string ArvrTestType { get; set; }

            public string SerialNumber { get; set; }

            public string Key { get; set; }

            public PgActionMapping Mapping { get; set; }
        }
    }
}
