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
        private bool _pgPowerOnForCurrentRun;
        private bool _integratedRunActive;
        private bool _integratedRunStarting;
        private bool _integratedRunPaused;
        private bool _integratedRunCancellationRequested;
        private long _arvrConnectionGeneration;

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
            UpdateIntegratedRunControls();
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

        private async void PgIntegratedRunButton_Click(object sender, RoutedEventArgs e)
        {
            await StartIntegratedRunAsync();
        }

        private async void PauseIntegratedRunButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_integratedRunActive || _integratedRunCancellationRequested)
                return;

            _integratedRunPaused = !_integratedRunPaused;
            UpdateIntegratedRunControls();
            if (_integratedRunPaused)
            {
                SetWorkflowStatus(_pgActionRunning == 0 ? "已暂停" : "暂停中", false);
                AppendLog(_pgActionRunning == 0
                    ? "PG 联动已暂停；恢复后再处理下一条待确认的切图请求。"
                    : "已请求暂停；当前 PG 指令会安全完成，暂停从下一条切图请求生效。");
                return;
            }

            AppendLog("PG 联动已继续。");
            PendingArvrAction pending = _pendingArvrAction;
            if (pending != null && !pending.IsConfirmed && Interlocked.CompareExchange(ref _pgActionRunning, 0, 0) == 0)
            {
                if (pending.Mapping == null)
                {
                    await StopIntegratedRunAsync("当前切图请求没有启用的 PG 映射", false);
                    return;
                }

                bool succeeded = await ExecutePendingPgActionAsync(true);
                if (!succeeded && _integratedRunActive)
                    await StopIntegratedRunAsync("PG 联动执行失败", false);
            }
            else
            {
                SetWorkflowStatus("PG 联动运行", true);
            }
        }

        private async void CancelIntegratedRunButton_Click(object sender, RoutedEventArgs e)
        {
            await RequestIntegratedCancellationAsync("用户取消");
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
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

        private async void DisconnectPgButton_Click(object sender, RoutedEventArgs e)
        {
            if (_integratedRunActive)
            {
                await RequestIntegratedCancellationAsync("用户断开 PG");
                return;
            }

            await PowerOffPgAsync("断开 PG");
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

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_integratedRunActive)
            {
                await RequestIntegratedCancellationAsync("用户断开");
                return;
            }

            await PowerOffPgAsync("断开 ARVR");
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

        private async Task StartIntegratedRunAsync()
        {
            if (_integratedRunActive)
                return;
            if (Interlocked.CompareExchange(ref _pgActionRunning, 0, 0) != 0)
            {
                SetWorkflowStatus("PG 指令执行中", false);
                AppendLog("当前 PG 指令尚未结束，不能启动新的 PG 联动运行。");
                return;
            }

            Disconnect();
            _integratedRunActive = true;
            _integratedRunStarting = true;
            _integratedRunPaused = false;
            _integratedRunCancellationRequested = false;
            _pendingArvrAction = null;
            SetPendingControls(null);
            UpdateIntegratedRunControls();
            SetWorkflowStatus("准备 PG 联动", true);
            AppendLog("PG 联动运行开始：PG 上电 -> 初始化 ARVR -> 按请求切图并确认 -> 最终结果后 PG 下电。");

            try
            {
                if (!await PowerOffPgAsync("开始新的 PG 联动运行"))
                {
                    await StopIntegratedRunAsync("启动前 PG 下电失败", false);
                    return;
                }

                if (_integratedRunCancellationRequested)
                {
                    await CompleteIntegratedCancellationAsync("用户取消");
                    return;
                }

                IntegrationProfile profile = CreateProfileFromUi();
                if (!_gecsClient.IsConnectedTo(profile.PgHost, profile.PgPort) && !await ConnectPgAsync())
                {
                    await StopIntegratedRunAsync("PG 连接失败", false);
                    return;
                }

                SetWorkflowStatus("PG 上电中", true);
                if (!await EnsurePgPowerOnAsync(profile, GetSerialNumber()))
                {
                    await StopIntegratedRunAsync("PG 上电失败", false);
                    return;
                }

                if (_integratedRunCancellationRequested)
                {
                    await CompleteIntegratedCancellationAsync("用户取消");
                    return;
                }

                SetWorkflowStatus("初始化 ARVR", true);
                await OpenConnectionAsync();
                if (_integratedRunCancellationRequested)
                {
                    await CompleteIntegratedCancellationAsync("用户取消");
                    return;
                }

                await SendEventAsync("ProjectARVRInit");
                _integratedRunStarting = false;
                SetWorkflowStatus("PG 联动运行", true);
                AppendLog("ARVR 初始化请求已发送，等待 SwitchPG 切图请求。");
                StartReceiveLoop();
            }
            catch (Exception ex)
            {
                AppendLog("PG 联动启动失败: " + ex.Message);
                await StopIntegratedRunAsync("PG 联动启动失败", false);
                MessageBox.Show(this, ex.Message, "PG 联动启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ConnectAsync(string mode)
        {
            if (_integratedRunActive)
            {
                AppendLog("PG 联动运行期间不能启动其他 ARVR 运行。");
                return;
            }

            try
            {
                if (!await PowerOffPgAsync("开始新的 ARVR 运行"))
                    return;
                await OpenConnectionAsync();
                if (!string.IsNullOrEmpty(mode))
                    await SendEventAsync(mode == "runall" ? "RunAll" : "ProjectARVRInit");
                StartReceiveLoop();
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

        private void StartReceiveLoop()
        {
            JsonStreamMessageReader reader = _messageReader;
            if (reader == null)
                return;

            long connectionGeneration = Interlocked.Read(ref _arvrConnectionGeneration);
            _ = ReceiveLoopAsync(connectionGeneration, reader);
        }

        private async Task ReceiveLoopAsync(long connectionGeneration, JsonStreamMessageReader reader)
        {
            int maxMessages = GetMaxMessages();
            TimeSpan timeout = TimeSpan.FromSeconds(GetTimeoutSeconds());
            bool powerOffAttempted = false;

            try
            {
                for (int messageIndex = 0; messageIndex < maxMessages && IsCurrentArvrConnection(connectionGeneration); messageIndex++)
                {
                    string json = await reader.ReadMessageAsync(timeout);
                    if (!IsCurrentArvrConnection(connectionGeneration))
                        break;
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
                        bool wasIntegratedRun = _integratedRunActive;
                        powerOffAttempted = true;
                        bool powerOffSucceeded = await PowerOffPgAsync("ARVR 已返回最终结果");
                        if (wasIntegratedRun)
                        {
                            if (_integratedRunCancellationRequested)
                                AppendLog("ARVR 已在取消生效前返回最终结果，按正常完成收尾。");
                            FinishIntegratedRunState();
                        }
                        _ = Dispatcher.BeginInvoke(new Action(() => SetWorkflowStatus(powerOffSucceeded ? "已完成" : "结束，关电失败", powerOffSucceeded)));
                        _ = Dispatcher.BeginInvoke(new Action(() => LoadReceivedJson(json)));
                        break;
                    }

                    if (eventName == "SwitchPG")
                    {
                        bool handledByIntegratedRun = _integratedRunActive;
                        await HandleArvrSwitchRequestAsync(root, "SwitchPG", "SwitchPGCompleted");
                        if (!handledByIntegratedRun && GetCheckBoxValue(AutoSwitchPgCheckBox) && !GetCheckBoxValue(AutoExecutePgCheckBox))
                            await ConfirmPendingArvrAsync();
                        continue;
                    }

                    if (eventName == "AoiSwitchPG")
                    {
                        bool handledByIntegratedRun = _integratedRunActive;
                        await HandleArvrSwitchRequestAsync(root, "AoiSwitchPG", "AOITestSwitchImageComplete");
                        if (!handledByIntegratedRun && GetCheckBoxValue(AutoAoiCheckBox) && !GetCheckBoxValue(AutoExecutePgCheckBox))
                            await ConfirmPendingArvrAsync();
                    }
                }
            }
            catch (TimeoutException ex)
            {
                if (IsCurrentArvrConnection(connectionGeneration))
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Receive timeout: " + ex.Message)));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException ex)
            {
                if (IsCurrentArvrConnection(connectionGeneration))
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Receive stopped: " + ex.Message)));
            }
            catch (Exception ex)
            {
                if (IsCurrentArvrConnection(connectionGeneration))
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Receive failed: " + ex.Message)));
            }
            finally
            {
                if (IsCurrentArvrConnection(connectionGeneration))
                {
                    if (!powerOffAttempted && _pgPowerOnForCurrentRun)
                        await PowerOffPgAsync("ARVR 接收已结束");
                    if (_integratedRunActive && IsCurrentArvrConnection(connectionGeneration))
                    {
                        bool wasCancelled = _integratedRunCancellationRequested;
                        Disconnect();
                        FinishIntegratedRunState();
                        SetWorkflowStatus(wasCancelled ? "已取消" : "联动已停止", false);
                    }
                }
            }
        }

        private bool IsCurrentArvrConnection(long connectionGeneration)
        {
            return connectionGeneration == Interlocked.Read(ref _arvrConnectionGeneration);
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
                : pending.Mapping.Name + " / " + pending.CommandText;
            AppendLog("ARVR requested " + eventName + ", ARVRTestType=" + pending.ArvrTestType + ", " + mappingText);
            if (pending.Mapping != null)
                _gecsClient.LogCommandPreview(pending.CommandText, pending.NetworkNumber);

            if (_integratedRunActive)
            {
                if (_integratedRunCancellationRequested)
                {
                    await CompleteIntegratedCancellationAsync("已到达 ARVR 切图边界");
                    return;
                }

                if (_integratedRunPaused)
                {
                    SetWorkflowStatus("已暂停", false);
                    AppendLog("PG 联动已停在当前切图请求；点击“继续”后再发送 PG 指令和 ARVR 确认。");
                    return;
                }

                if (pending.Mapping == null)
                {
                    await StopIntegratedRunAsync("当前切图请求没有启用的 PG 映射", false);
                    return;
                }

                bool succeeded = await ExecutePendingPgActionAsync(true);
                if (!succeeded && _integratedRunActive)
                    await StopIntegratedRunAsync("PG 联动执行失败", false);
                return;
            }

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
            string commandText = mapping == null ? string.Empty : profile.ExpandCommand(mapping, testType, serialNumber);
            var pending = new PendingArvrAction
            {
                Root = root,
                EventName = eventName,
                ReplyEventName = replyEventName,
                ArvrTestType = testType,
                SerialNumber = serialNumber,
                Key = ArvrClient.BuildMessageKey(root, eventName),
                Mapping = mapping == null ? null : mapping.Clone(),
                CommandText = commandText,
                NetworkNumber = checked((byte)profile.NetworkNumber)
            };

            PendingPgActionTextBlock.Text = mapping == null
                ? eventName + " / TestType=" + DisplayValue(testType) + " / 未找到启用映射"
                : eventName + " / TestType=" + DisplayValue(testType) + " / " + mapping.Name + " / " + commandText;
            return pending;
        }

        private async Task<bool> ExecutePendingPgActionAsync(bool confirmOnSuccess)
        {
            if (Interlocked.Exchange(ref _pgActionRunning, 1) != 0)
            {
                AppendLog("PG action is already running.");
                return false;
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
                        return await ConfirmPendingArvrAsync();
                    return true;
                }

                IntegrationProfile profile = Dispatcher.CheckAccess()
                    ? CreateProfileFromUi()
                    : (IntegrationProfile)Dispatcher.Invoke(new Func<IntegrationProfile>(CreateProfileFromUi));
                string command = profile.ExpandCommand(pending.Mapping, pending.ArvrTestType, pending.SerialNumber);
                if (!_gecsClient.IsConnected && !await ConnectPgAsync())
                    return false;

                if (profile.ManagePgPowerForRun && !await EnsurePgPowerOnAsync(profile, pending.SerialNumber))
                {
                    SetWorkflowStatus("开电失败", false);
                    AppendLog("PG POWER ON failed; pattern command and ARVR confirmation were not sent.");
                    return false;
                }

                SetWorkflowStatus("执行中", true);
                AppendLog("Executing mapped PG command: " + command);
                SemiAutomaticWorkflowResult result = await _semiAutomaticWorkflow.ExecuteAsync(
                    profile,
                    pending.Mapping,
                    pending.ArvrTestType,
                    pending.SerialNumber,
                    confirmOnSuccess,
                    ConfirmPendingArvrForWorkflowAsync,
                    () => _executedPgMessages.Add(pending.Key));
                if (!result.PgSucceeded)
                {
                    SetWorkflowStatus("PG 失败", false);
                    SetPgConnectionState(_gecsClient.IsConnected);
                    AppendLog("PG mapping failed; ARVR was not confirmed. " + result.ErrorMessage);
                    await PowerOffPgAsync("PG 切图失败");
                    return false;
                }

                AppendLog("PG mapping succeeded: " + result.PgResponseText);
                if (_integratedRunActive && _integratedRunCancellationRequested)
                {
                    AppendLog("取消已生效：PG 指令已安全结束，未向 ARVR 发送本次切图确认。");
                    await CompleteIntegratedCancellationAsync("用户取消");
                    return false;
                }

                if (!result.ArvrConfirmed && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    SetWorkflowStatus("确认失败", false);
                    AppendLog("PG succeeded, but ARVR confirmation failed: " + result.ErrorMessage);
                    await PowerOffPgAsync("ARVR 确认失败");
                    return false;
                }
                else if (!confirmOnSuccess)
                {
                    SetWorkflowStatus("待确认", true);
                    AppendLog("PG succeeded; waiting for operator to confirm ARVR.");
                }
                else
                    SetWorkflowStatus("已确认", true);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("PG action failed; ARVR was not confirmed. " + ex.Message);
                if (Dispatcher.CheckAccess())
                    MessageBox.Show(this, ex.Message, "PG 联动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
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
            pending.IsConfirmed = confirmed;
            SetWorkflowStatus(confirmed ? "已确认" : "确认失败", confirmed);
            return confirmed;
        }

        private Task<bool> ConfirmPendingArvrForWorkflowAsync()
        {
            if (_integratedRunActive && _integratedRunCancellationRequested)
                return Task.FromResult(false);
            return ConfirmPendingArvrAsync();
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

        private async Task<bool> EnsurePgPowerOnAsync(IntegrationProfile profile, string serialNumber)
        {
            if (_pgPowerOnForCurrentRun)
                return true;

            AppendLog("PG power sequence: sending POWER ON before the first pattern.");
            GecsCommandResult result = await _semiAutomaticWorkflow.SetPowerAsync(profile, true, serialNumber);
            SetPgConnectionState(_gecsClient.IsConnected);
            if (!result.IsSuccess)
            {
                AppendLog("PG POWER ON failed: " + result.ErrorMessage);
                return false;
            }

            _pgPowerOnForCurrentRun = true;
            AppendLog("PG POWER ON succeeded: " + result.ResponseText);
            return true;
        }

        private async Task<bool> PowerOffPgAsync(string reason)
        {
            if (!_pgPowerOnForCurrentRun)
                return true;

            try
            {
                IntegrationProfile profile = Dispatcher.CheckAccess()
                    ? CreateProfileFromUi()
                    : (IntegrationProfile)Dispatcher.Invoke(new Func<IntegrationProfile>(CreateProfileFromUi));
                AppendLog("PG power sequence: sending POWER OFF (" + reason + ").");
                GecsCommandResult result = await _semiAutomaticWorkflow.SetPowerAsync(profile, false, string.Empty);
                SetPgConnectionState(_gecsClient.IsConnected);
                if (!result.IsSuccess)
                {
                    AppendLog("PG POWER OFF failed: " + result.ErrorMessage);
                    return false;
                }

                _pgPowerOnForCurrentRun = false;
                AppendLog("PG POWER OFF succeeded: " + result.ResponseText);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("PG POWER OFF failed: " + ex.Message);
                return false;
            }
        }

        private async Task RequestIntegratedCancellationAsync(string reason)
        {
            if (!_integratedRunActive || _integratedRunCancellationRequested)
                return;

            _integratedRunCancellationRequested = true;
            _integratedRunPaused = false;
            UpdateIntegratedRunControls();
            SetWorkflowStatus("取消中", false);
            AppendLog("已请求取消 PG 联动；不会再发送下一条切图确认。当前指令如已发送，会等待其安全结束后下电。");

            PendingArvrAction pending = _pendingArvrAction;
            bool commandIsRunning = Interlocked.CompareExchange(ref _pgActionRunning, 0, 0) != 0;
            bool arvrFlowIsRunning = pending != null && pending.IsConfirmed;
            if (!_integratedRunStarting && !commandIsRunning && !arvrFlowIsRunning)
                await CompleteIntegratedCancellationAsync(reason);
        }

        private Task CompleteIntegratedCancellationAsync(string reason)
        {
            return StopIntegratedRunAsync(reason, true);
        }

        private async Task StopIntegratedRunAsync(string reason, bool cancelled)
        {
            if (!_integratedRunActive)
                return;

            bool powerOffSucceeded = await PowerOffPgAsync(reason);
            Disconnect();
            FinishIntegratedRunState();
            string status = cancelled ? "已取消" : "联动已停止";
            SetWorkflowStatus(powerOffSucceeded ? status : status + "，关电失败", false);
            AppendLog((cancelled ? "PG 联动已取消：" : "PG 联动已停止：") + reason + (powerOffSucceeded ? "。" : "；PG 下电失败，请现场确认。"));
        }

        private void FinishIntegratedRunState()
        {
            _integratedRunActive = false;
            _integratedRunStarting = false;
            _integratedRunPaused = false;
            _integratedRunCancellationRequested = false;
            _pendingArvrAction = null;
            SetPendingControls(null);
            UpdateIntegratedRunControls();
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
            ManagePgPowerCheckBox.IsChecked = profile.ManagePgPowerForRun;
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
                ManagePgPowerForRun = ManagePgPowerCheckBox.IsChecked == true,
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

        private void UpdateIntegratedRunControls()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(UpdateIntegratedRunControls));
                return;
            }
            if (PgIntegratedRunButton == null)
                return;

            bool active = _integratedRunActive;
            PgIntegratedRunButton.IsEnabled = !active;
            PauseIntegratedRunButton.IsEnabled = active && !_integratedRunCancellationRequested && !_integratedRunStarting;
            PauseIntegratedRunButton.Content = _integratedRunPaused ? "继续" : "暂停";
            CancelIntegratedRunButton.IsEnabled = active && !_integratedRunCancellationRequested;
            ArvrConnectButton.IsEnabled = !active;
            ArvrInitButton.IsEnabled = !active;
            ArvrRunAllButton.IsEnabled = !active;
            PgConnectButton.IsEnabled = !active;
            PgDisconnectButton.IsEnabled = !active;
            ManagePgPowerCheckBox.IsEnabled = !active;
            AutoExecutePgCheckBox.IsEnabled = !active;
            AutoConfirmAfterPgCheckBox.IsEnabled = !active;
            SetPendingControls(_pendingArvrAction);
        }

        private void SetPendingControls(PendingArvrAction pending)
        {
            if (PendingPgActionTextBlock == null)
                return;

            bool hasPending = pending != null;
            bool manualActionsEnabled = !_integratedRunActive;
            bool canExecute = manualActionsEnabled && hasPending && pending.Mapping != null;
            if (!hasPending)
                PendingPgActionTextBlock.Text = "无待处理请求";
            ExecutePgAndConfirmButton.IsEnabled = canExecute;
            ExecutePgOnlyButton.IsEnabled = canExecute;
            ConfirmPendingArvrButton.IsEnabled = manualActionsEnabled && hasPending;
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
            Interlocked.Increment(ref _arvrConnectionGeneration);
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

            public string CommandText { get; set; }

            public byte NetworkNumber { get; set; }

            public bool IsConfirmed { get; set; }
        }
    }
}
