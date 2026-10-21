using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.FlowPoi.Client;
using Microsoft.Win32;

namespace ColorVision.FlowPoi.Demo
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource pending;
        private FlowPoiResponse response;
        private bool closed;

        public MainWindow()
        {
            InitializeComponent();
            Selector_Changed(null, null);
        }

        private void Selector_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (FlowNameTextBox == null || FlowIdTextBox == null) return;
            bool byId = SelectorComboBox.SelectedIndex == 1;
            FlowNameTextBox.Visibility = byId ? Visibility.Collapsed : Visibility.Visible;
            FlowIdTextBox.Visibility = byId ? Visibility.Visible : Visibility.Collapsed;
            SelectorLabel.Text = byId ? "模板实际 ID（支持负数）" : "流程名称";
        }

        private async void Run_Click(object sender, RoutedEventArgs e)
        {
            if (pending != null) return;
            ClearResponse();
            CancellationTokenSource operation = null;
            try
            {
                int port = ReadInteger(PortTextBox.Text, "端口");
                int seconds = ReadInteger(TimeoutTextBox.Text, "超时秒数");
                var client = new FlowPoiClient(HostTextBox.Text, port);
                bool byId = SelectorComboBox.SelectedIndex == 1;
                int id = byId ? ReadInteger(FlowIdTextBox.Text, "模板 ID") : 0;
                string name = FlowNameTextBox.Text.Trim();
                if (byId && id == 0) throw new ArgumentException("模板 ID 不能为 0。");
                if (!byId && name.Length == 0) throw new ArgumentException("请填写流程名称。");
                if (seconds <= 0 || seconds > int.MaxValue / 1000)
                    throw new ArgumentException("超时秒数必须大于 0 且不超过 2147483。");
                string serialNumber = SerialNumberTextBox.Text.Trim();
                operation = new CancellationTokenSource();
                pending = operation;
                SetBusy(true);
                SetStatus("正在等待主程序完成流程和后处理…", false);
                FlowPoiResponse result = byId
                    ? await client.RunByIdAsync(id, serialNumber, TimeSpan.FromSeconds(seconds), operation.Token)
                    : await client.RunByNameAsync(name, serialNumber, TimeSpan.FromSeconds(seconds), operation.Token);
                if (!closed) ShowResponse(result, "联机响应");
            }
            catch (OperationCanceledException)
            {
                if (!closed) SetStatus("已取消客户端等待。主程序流程可能仍在运行，请在主程序中确认。", true);
            }
            catch (Exception ex)
            {
                if (!closed) SetStatus(ex.Message, true);
            }
            finally
            {
                pending = null;
                if (operation != null) operation.Dispose();
                if (!closed) SetBusy(false);
            }
        }

        private static int ReadInteger(string text, string name)
        {
            int value;
            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new ArgumentException(name + "必须是整数。");
            return value;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (pending != null) pending.Cancel();
        }

        private void Sample_Click(object sender, RoutedEventArgs e)
        {
            LoadResponseFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", "flow-poi-response.json"), "离线样例");
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "JSON 响应 (*.json)|*.json|所有文件 (*.*)|*.*", Title = "打开 FlowRunPoi 响应" };
            if (dialog.ShowDialog(this) == true) LoadResponseFile(dialog.FileName, "离线文件");
        }

        private void LoadResponseFile(string path, string source)
        {
            ClearResponse();
            try
            {
                ShowResponse(FlowPoiResponse.Parse(File.ReadAllText(path, Encoding.UTF8)), source);
            }
            catch (Exception ex)
            {
                SetStatus("读取响应失败：" + ex.Message, true);
            }
        }

        private void ShowResponse(FlowPoiResponse value, string source)
        {
            response = value;
            ResultTitle.Text = source;
            ResponseTextBox.Text = value.ResponseJson;
            FlowPoiData data = value.Data;
            PointsGrid.ItemsSource = data == null ? null : data.Poi;
            int count = data == null || data.Poi == null ? 0 : data.Poi.Count;
            SetStatus(source + " · Code=" + value.Code + " · " + value.Msg, !value.IsSuccess);
            SummaryText.Text = data == null ? "服务端未返回批次数据"
                : string.Format(CultureInfo.InvariantCulture,
                    "流程：{0}（ID {1}）    批次：{2}    状态：{3}    流程用时：{4} ms    POI：{5}",
                    data.FlowName, data.FlowId, data.BatchId, data.Status, data.TotalTime, count);
            IdentityText.Text = "请求 MsgID：" + value.MsgID + "    关联编号：" + value.SerialNumber
                + (data == null ? "" : "\n执行编号：" + data.ExecutionSerialNumber);
            ExportJsonButton.IsEnabled = true;
            ExportCsvButton.IsEnabled = count > 0;
        }

        private void ClearResponse()
        {
            response = null;
            PointsGrid.ItemsSource = null;
            ResponseTextBox.Clear();
            SummaryText.Text = "尚无本次结果";
            IdentityText.Text = string.Empty;
            ResultTitle.Text = "本次响应";
            ExportJsonButton.IsEnabled = false;
            ExportCsvButton.IsEnabled = false;
        }

        private void SetBusy(bool busy)
        {
            InputPanel.IsEnabled = !busy;
            RunButton.IsEnabled = !busy;
            CancelButton.IsEnabled = busy;
            SampleButton.IsEnabled = !busy;
            OpenButton.IsEnabled = !busy;
        }

        private void SetStatus(string text, bool error)
        {
            StatusText.Text = text;
            StatusText.Foreground = error ? Brushes.Firebrick : new SolidColorBrush(Color.FromRgb(45, 80, 120));
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e) { Export(false); }
        private void ExportCsv_Click(object sender, RoutedEventArgs e) { Export(true); }

        private void Export(bool csv)
        {
            if (response == null) return;
            var dialog = new SaveFileDialog
            {
                Filter = csv ? "POI 结果表 (*.csv)|*.csv" : "完整响应 (*.json)|*.json",
                FileName = "flow-poi-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + (csv ? ".csv" : ".json")
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (csv) DemoResultFiles.SaveCsv(response, dialog.FileName);
                else DemoResultFiles.SaveJson(response, dialog.FileName);
                SetStatus("已导出：" + dialog.FileName, false);
            }
            catch (Exception ex)
            {
                SetStatus("导出失败：" + ex.Message, true);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            closed = true;
            if (pending != null) pending.Cancel();
            base.OnClosed(e);
        }
    }
}
