using Newtonsoft.Json;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    public partial class FlowNodeAnalysisWindow : Window
    {
        private MeasureBatchModel _initialBatch;
        private const long TimeoutThresholdMs = 30000;

        public ObservableCollection<FlowNodeRecord> NodeRecords { get; set; } = new ObservableCollection<FlowNodeRecord>();
        public ObservableCollection<FlowNodeMessage> NodeMessages { get; set; } = new ObservableCollection<FlowNodeMessage>();
        private List<FlowNodeMessage> _allMessages = new List<FlowNodeMessage>();

        public FlowNodeAnalysisWindow()
        {
            InitializeComponent();
        }

        public FlowNodeAnalysisWindow(MeasureBatchModel batch)
        {
            _initialBatch = batch;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            NodeRecordListView.ItemsSource = NodeRecords;
            MessageListView.ItemsSource = NodeMessages;

            var batchIds = FlowNodeRecordDataBaseHelper.GetDistinctBatchIds(100);
            BatchListView.ItemsSource = batchIds;

            if (_initialBatch != null)
            {
                if (batchIds.Contains(_initialBatch.Id))
                {
                    BatchListView.SelectedItem = _initialBatch.Id;
                }
                LoadBatchRecords(new List<int> { _initialBatch.Id });
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedBatchIds = BatchListView.SelectedItems.Cast<int>().ToList();
            if (selectedBatchIds.Count == 0)
            {
                MessageBox.Show("请选择至少一个批次", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            LoadBatchRecords(selectedBatchIds);
        }

        private void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedBatchIds = BatchListView.SelectedItems.Cast<int>().ToList();
            if (selectedBatchIds.Count < 2)
            {
                MessageBox.Show("请选择至少两个批次进行对比", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            LoadBatchRecords(selectedBatchIds);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodeRecords.Count == 0)
            {
                MessageBox.Show("没有数据可导出，请先加载批次数据", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv)|*.csv";
            dialog.FileName = "节点时间分析_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("BatchId,节点名称,节点类型,开始时间,结束时间,耗时(ms),SN");

            foreach (var record in NodeRecords)
            {
                csvBuilder.Append(record.BatchId).Append(',');
                csvBuilder.Append(CsvEscape(record.NodeName)).Append(',');
                csvBuilder.Append(CsvEscape(record.NodeType)).Append(',');
                csvBuilder.Append(record.StartTime.ToString("yyyy/MM/dd HH:mm:ss.fff")).Append(',');
                csvBuilder.Append(record.EndTime?.ToString("yyyy/MM/dd HH:mm:ss.fff") ?? string.Empty).Append(',');
                csvBuilder.Append(record.ElapsedMs).Append(',');
                csvBuilder.Append(CsvEscape(record.SerialNumber));
                csvBuilder.AppendLine();
            }

            // Export MQTT messages
            if (_allMessages.Count > 0)
            {
                csvBuilder.AppendLine();
                csvBuilder.AppendLine("MQTT消息追踪");
                csvBuilder.AppendLine("BatchId,节点,NodeId,EventName,MsgId,发送Topic,发送时间,接收Topic,接收时间,耗时(ms),状态码,状态消息,状态");
                foreach (var msg in _allMessages)
                {
                    csvBuilder.Append(msg.BatchId).Append(',');
                    csvBuilder.Append(CsvEscape(msg.NodeName)).Append(',');
                    csvBuilder.Append(CsvEscape(msg.NodeId)).Append(',');
                    csvBuilder.Append(CsvEscape(msg.EventName)).Append(',');
                    csvBuilder.Append(CsvEscape(msg.MsgId)).Append(',');
                    csvBuilder.Append(CsvEscape(msg.SendTopic)).Append(',');
                    csvBuilder.Append(msg.SendTime.ToString("yyyy/MM/dd HH:mm:ss.fff")).Append(',');
                    csvBuilder.Append(CsvEscape(msg.RecvTopic)).Append(',');
                    csvBuilder.Append(msg.RecvTime?.ToString("yyyy/MM/dd HH:mm:ss.fff") ?? string.Empty).Append(',');
                    csvBuilder.Append(msg.ElapsedMs).Append(',');
                    csvBuilder.Append(msg.StatusCode?.ToString() ?? string.Empty).Append(',');
                    csvBuilder.Append(CsvEscape(msg.StatusMessage)).Append(',');
                    csvBuilder.Append(msg.State);
                    csvBuilder.AppendLine();
                }
            }

            File.WriteAllText(dialog.FileName, csvBuilder.ToString(), new UTF8Encoding(true));
            MessageBox.Show("导出成功", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private void LoadBatchRecords(List<int> batchIds)
        {
            NodeRecords.Clear();
            var records = FlowNodeRecordDataBaseHelper.GetByBatchIds(batchIds);
            foreach (var record in records)
                NodeRecords.Add(record);

            // Load MQTT messages
            _allMessages = FlowNodeRecordDataBaseHelper.GetMessagesByBatchIds(batchIds);
            RefreshMessageFilter();

            DrawGanttChart(records, batchIds);
        }

        private void RefreshMessageFilter()
        {
            // Update node filter combo
            var nodeNames = _allMessages.Select(m => m.NodeName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            MessageNodeFilter.Items.Clear();
            MessageNodeFilter.Items.Add(new ComboBoxItem { Content = "全部", IsSelected = true });
            foreach (var name in nodeNames)
                MessageNodeFilter.Items.Add(new ComboBoxItem { Content = name });
            MessageNodeFilter.SelectedIndex = 0;

            ApplyMessageFilter();
        }

        private void ApplyMessageFilter()
        {
            NodeMessages.Clear();
            string selectedNode = null;
            if (MessageNodeFilter.SelectedItem is ComboBoxItem item && item.Content?.ToString() != "全部")
                selectedNode = item.Content?.ToString();

            string selectedState = null;
            if (MessageStateFilter?.SelectedItem is ComboBoxItem stateItem && stateItem.Content?.ToString() != "全部")
                selectedState = stateItem.Content?.ToString();

            var filtered = _allMessages.AsEnumerable();
            if (!string.IsNullOrEmpty(selectedNode))
                filtered = filtered.Where(m => m.NodeName == selectedNode);

            if (!string.IsNullOrEmpty(selectedState) && Enum.TryParse<FlowMessageState>(selectedState, out var state))
                filtered = filtered.Where(m => m.State == state);

            foreach (var msg in filtered)
                NodeMessages.Add(msg);
        }

        private void MessageNodeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allMessages != null)
                ApplyMessageFilter();
        }

        private void MessageStateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allMessages != null)
                ApplyMessageFilter();
        }

        private void MessageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MessageListView.SelectedItem is FlowNodeMessage msg)
            {
                var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
                SendPayloadTextBox.Text = FormatJsonSafe(msg.SendPayload);
                RecvPayloadTextBox.Text = FormatJsonSafe(msg.RecvPayload);
            }
            else
            {
                SendPayloadTextBox.Text = string.Empty;
                RecvPayloadTextBox.Text = string.Empty;
            }
        }

        private static string FormatJsonSafe(string json)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            try
            {
                var obj = JsonConvert.DeserializeObject(json);
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }

        private void OpenMessageListWindow_Click(object sender, RoutedEventArgs e)
        {
            new FlowMessageListWindow { Owner = this }.Show();
        }

        private static string DetectChineseFont()
        {
            return ScottPlot.Fonts.Detect("中文");
        }

        private void SetupChineseFonts()
        {
            string chineseFont = DetectChineseFont();
            GanttPlot.Plot.Axes.Title.Label.FontName = chineseFont;
            GanttPlot.Plot.Axes.Left.Label.FontName = chineseFont;
            GanttPlot.Plot.Axes.Bottom.Label.FontName = chineseFont;
            GanttPlot.Plot.Axes.Left.TickLabelStyle.FontName = chineseFont;
            GanttPlot.Plot.Axes.Bottom.TickLabelStyle.FontName = chineseFont;
            GanttPlot.Plot.Legend.FontName = chineseFont;
        }

        private void DrawGanttChart(List<FlowNodeRecord> records, List<int> batchIds)
        {
            GanttPlot.Plot.Clear();

            if (records.Count == 0)
            {
                GanttPlot.Refresh();
                return;
            }

            SetupChineseFonts();

            if (batchIds.Count == 1)
            {
                DrawSingleBatchGantt(records);
            }
            else
            {
                DrawMultiBatchComparison(records, batchIds);
            }

            GanttPlot.Refresh();
        }

        private void DrawSingleBatchGantt(List<FlowNodeRecord> records)
        {
            if (records.Count == 0) return;

            // Determine base time: use Batch.CreateDate if available, otherwise earliest node start
            DateTime baseTime;
            if (_initialBatch?.CreateDate != null)
            {
                baseTime = _initialBatch.CreateDate.Value;
            }
            else
            {
                baseTime = records.Min(r => r.StartTime);
            }

            var recordsWithEndTime = records.Where(r => r.EndTime.HasValue);
            long totalMs = recordsWithEndTime.Any()
                ? recordsWithEndTime.Max(r => (long)(r.EndTime.Value - baseTime).TotalMilliseconds)
                : 0;

            // If batch has TotalTime, use that for the total bar
            long batchTotalMs = _initialBatch?.TotalTime > 0 ? _initialBatch.TotalTime : totalMs;

            ScottPlot.Color[] palette = new ScottPlot.Color[]
            {
                ScottPlot.Color.FromHex("#4CAF50"),
                ScottPlot.Color.FromHex("#2196F3"),
                ScottPlot.Color.FromHex("#FF9800"),
                ScottPlot.Color.FromHex("#9C27B0"),
                ScottPlot.Color.FromHex("#00BCD4"),
                ScottPlot.Color.FromHex("#795548"),
                ScottPlot.Color.FromHex("#607D8B"),
                ScottPlot.Color.FromHex("#E91E63"),
            };

            ScottPlot.Color timeoutColor = ScottPlot.Color.FromHex("#F44336");
            ScottPlot.Color totalBarColor = ScottPlot.Color.FromHex("#37474F");

            List<ScottPlot.Bar> bars = new List<ScottPlot.Bar>();
            List<ScottPlot.Tick> ticks = new List<ScottPlot.Tick>();

            // Total row count = 1 (total bar) + records.Count
            int totalRows = records.Count + 1;

            // First bar: Total time (at the top)
            double totalYPos = totalRows - 1;
            bars.Add(new ScottPlot.Bar
            {
                Position = totalYPos,
                ValueBase = 0,
                Value = batchTotalMs > 0 ? batchTotalMs : totalMs,
                FillColor = totalBarColor,
                IsVisible = true,
                Orientation = ScottPlot.Orientation.Horizontal,
                Size = 0.7,
            });
            ticks.Add(new ScottPlot.Tick(totalYPos, "总时间"));

            // Node bars
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                double startOffset = (rec.StartTime - baseTime).TotalMilliseconds;
                double endOffset = rec.EndTime.HasValue ? (rec.EndTime.Value - baseTime).TotalMilliseconds : totalMs;
                double yPos = totalRows - 2 - i;

                bool isTimeout = !rec.EndTime.HasValue || rec.ElapsedMs > TimeoutThresholdMs;
                ScottPlot.Color barColor = isTimeout ? timeoutColor : palette[i % palette.Length];

                bars.Add(new ScottPlot.Bar
                {
                    Position = yPos,
                    ValueBase = startOffset,
                    Value = endOffset,
                    FillColor = barColor,
                    IsVisible = true,
                    Orientation = ScottPlot.Orientation.Horizontal,
                    Size = 0.6,
                });

                string label = rec.NodeName ?? "Unknown";
                ticks.Add(new ScottPlot.Tick(yPos, label));
            }

            var barPlot = GanttPlot.Plot.Add.Bars(bars.ToArray());
            barPlot.Horizontal = true;

            GanttPlot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());

            GanttPlot.Plot.Title("流程节点甘特图");
            GanttPlot.Plot.XLabel("时间 (ms)");
            GanttPlot.Plot.YLabel("");

            GanttPlot.Plot.Axes.AutoScale();
            GanttPlot.Plot.Axes.Margins(left: 0.05, bottom: 0.1);
        }

        private void DrawMultiBatchComparison(List<FlowNodeRecord> records, List<int> batchIds)
        {
            var grouped = records.GroupBy(r => r.NodeName ?? "Unknown").ToList();
            if (grouped.Count == 0) return;

            ScottPlot.Color[] batchColors = new ScottPlot.Color[]
            {
                ScottPlot.Color.FromHex("#2196F3"),
                ScottPlot.Color.FromHex("#4CAF50"),
                ScottPlot.Color.FromHex("#FF9800"),
                ScottPlot.Color.FromHex("#9C27B0"),
                ScottPlot.Color.FromHex("#00BCD4"),
                ScottPlot.Color.FromHex("#795548"),
                ScottPlot.Color.FromHex("#607D8B"),
                ScottPlot.Color.FromHex("#E91E63"),
            };

            ScottPlot.Color timeoutColor = ScottPlot.Color.FromHex("#F44336");

            List<ScottPlot.Bar> bars = new List<ScottPlot.Bar>();
            List<ScottPlot.Tick> ticks = new List<ScottPlot.Tick>();

            int nodeIndex = 0;
            double barHeight = 0.8 / batchIds.Count;

            foreach (var group in grouped)
            {
                double yBase = grouped.Count - 1 - nodeIndex;
                ticks.Add(new ScottPlot.Tick(yBase, group.Key));

                for (int bIdx = 0; bIdx < batchIds.Count; bIdx++)
                {
                    int batchId = batchIds[bIdx];
                    var batchRecord = group.FirstOrDefault(r => r.BatchId == batchId);
                    if (batchRecord == null) continue;

                    double elapsed = batchRecord.ElapsedMs;
                    double yPos = yBase + (bIdx - (batchIds.Count - 1) / 2.0) * barHeight;

                    bool isTimeout = !batchRecord.EndTime.HasValue || batchRecord.ElapsedMs > TimeoutThresholdMs;
                    ScottPlot.Color barColor = isTimeout ? timeoutColor : batchColors[bIdx % batchColors.Length];

                    bars.Add(new ScottPlot.Bar
                    {
                        Position = yPos,
                        ValueBase = 0,
                        Value = elapsed,
                        FillColor = barColor,
                        IsVisible = true,
                        Orientation = ScottPlot.Orientation.Horizontal,
                        Size = barHeight * 0.85,
                        Label = $"Batch {batchId}",
                    });
                }
                nodeIndex++;
            }

            var barPlot = GanttPlot.Plot.Add.Bars(bars.ToArray());
            barPlot.Horizontal = true;

            GanttPlot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());

            GanttPlot.Plot.Title("流程节点对比 (多批次)");
            GanttPlot.Plot.XLabel("耗时 (ms)");
            GanttPlot.Plot.YLabel("");

            // Add legend for batch colors
            for (int i = 0; i < batchIds.Count; i++)
            {
                GanttPlot.Plot.Legend.ManualItems.Add(new ScottPlot.LegendItem
                {
                    LabelText = $"Batch {batchIds[i]}",
                    FillColor = batchColors[i % batchColors.Length],
                });
            }
            GanttPlot.Plot.Legend.ManualItems.Add(new ScottPlot.LegendItem
            {
                LabelText = "超时",
                FillColor = timeoutColor,
            });
            GanttPlot.Plot.Legend.IsVisible = true;

            GanttPlot.Plot.Axes.AutoScale();
            GanttPlot.Plot.Axes.Margins(left: 0.05, bottom: 0.1);
        }
    }
}
