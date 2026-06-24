using ProjectARVRPro;
using ProjectARVRPro.Process.W51;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace ProjectARVRPro.IntegrationDemo
{
    public partial class MainWindow : Window
    {
        private ParsedProjectArvrResult _currentResult;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private JsonStreamMessageReader _messageReader;
        private readonly HashSet<string> _confirmedMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            SerialNumberTextBox.Text = "SN-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            ContractTextBox.Text = BuildContractSummary();
            FieldGuideTextBox.Text = OpticalParameterDescriptions.BuildGuideText();
            Loaded += delegate { LoadSample("project-arvr-result.json"); };
            Closed += delegate { Disconnect(); };
        }

        private async void ConnectInitButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectAsync("init");
        }

        private async void ConnectRunAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectAsync("runall");
        }

        private async void SwitchPgCompleteButton_Click(object sender, RoutedEventArgs e)
        {
            await SendEventAsync("SwitchPGCompleted");
        }

        private async void AoiCompleteButton_Click(object sender, RoutedEventArgs e)
        {
            await SendEventAsync("AOITestSwitchImageComplete");
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
                Disconnect();
                _tcpClient = new TcpClient();
                string host = HostTextBox.Text.Trim();
                int port = ParsePositiveInt(PortTextBox.Text, "Port");
                await _tcpClient.ConnectAsync(host, port);
                _networkStream = _tcpClient.GetStream();
                _messageReader = new JsonStreamMessageReader(_networkStream);
                _confirmedMessages.Clear();

                AppendLog("Connected " + host + ":" + port.ToString(CultureInfo.InvariantCulture));
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
                        _ = Dispatcher.BeginInvoke(new Action(() => LoadReceivedJson(json)));
                        break;
                    }

                    if (eventName == "SwitchPG" && GetCheckBoxValue(AutoSwitchPgCheckBox))
                    {
                        await SendConfirmOnceAsync(root, "SwitchPG", "SwitchPGCompleted");
                        continue;
                    }

                    if (eventName == "AoiSwitchPG" && GetCheckBoxValue(AutoAoiCheckBox))
                    {
                        await SendConfirmOnceAsync(root, "AoiSwitchPG", "AOITestSwitchImageComplete");
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

        private async Task SendConfirmOnceAsync(Dictionary<string, object> root, string sourceEventName, string replyEventName)
        {
            if (_networkStream == null)
                return;

            string serialNumber = ResultParser.GetString(root, "SerialNumber");
            if (string.IsNullOrWhiteSpace(serialNumber))
                serialNumber = GetSerialNumber();

            string json = await ArvrClient.SendConfirmOnceAsync(_networkStream, _confirmedMessages, root, sourceEventName, replyEventName, serialNumber);
            if (!string.IsNullOrEmpty(json))
                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Sent: " + json)));
            else
                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("Skipped duplicate " + sourceEventName + " confirmation.")));
        }

        private async Task SendEventAsync(string eventName)
        {
            if (_networkStream == null)
            {
                AppendLog("Cannot send " + eventName + ": not connected.");
                return;
            }

            string json = await ArvrClient.SendRequestAsync(_networkStream, eventName, GetSerialNumber());
            AppendLog("Sent: " + json);
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
            AppendLog("Loaded sample: " + fileName);
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

        private static string BuildContractSummary()
        {
            return "public class ObjectiveTestResult : ViewModelBase\r\n" +
                   "{\r\n" +
                   "    public W25TestResult W25TestResult { get; set; }\r\n" +
                   "    public W51TestResult W51TestResult { get; set; }\r\n" +
                   "    public W255TestResult W255TestResult { get; set; }\r\n" +
                   "    public BlackTestResult BlackTestResult { get; set; }\r\n" +
                   "    public RedTestResult RedTestResult { get; set; }\r\n" +
                   "    public GreenTestResult GreenTestResult { get; set; }\r\n" +
                   "    public BlueTestResult BlueTestResult { get; set; }\r\n" +
                   "    public ChessboardTestResult ChessboardTestResult { get; set; }\r\n" +
                   "    public MTFHVTestResult MTFHVTestResult { get; set; }\r\n" +
                   "    public List<MTFHV048TestResult> MTFHV048TestResults { get; set; }\r\n" +
                   "    public List<MTFHV058TestResult> MTFHV058TestResults { get; set; }\r\n" +
                   "    public DistortionTestResult DistortionTestResult { get; set; }\r\n" +
                   "    public OpticCenterTestResult OpticCenterTestResult { get; set; }\r\n" +
                   "    public Dictionary<string, ObservableCollection<ObjectiveTestItem>> DynamicTestResults { get; set; }\r\n" +
                   "    public string Msg { get; set; }\r\n" +
                   "    public bool TotalResult { get; set; }\r\n" +
                   "    public string TotalResultString { get; }\r\n" +
                   "}";
        }
    }
}
