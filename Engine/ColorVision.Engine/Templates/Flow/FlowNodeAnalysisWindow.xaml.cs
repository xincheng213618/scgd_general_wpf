using ScottPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    public partial class FlowNodeAnalysisWindow : Window
    {
        private MeasureBatchModel _initialBatch;
        private const long TimeoutThresholdMs = 30000;

        public ObservableCollection<FlowNodeRecord> NodeRecords { get; set; } = new ObservableCollection<FlowNodeRecord>();

        public FlowNodeAnalysisWindow()
        {
            InitializeComponent();
        }

        public FlowNodeAnalysisWindow(MeasureBatchModel batch) : this()
        {
            _initialBatch = batch;
        }

        public FlowNodeAnalysisWindow(int batchId) : this()
        {
            _initialBatch = new MeasureBatchModel { Id = batchId };
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            NodeRecordListView.ItemsSource = NodeRecords;

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

        private void LoadBatchRecords(List<int> batchIds)
        {
            NodeRecords.Clear();
            var records = FlowNodeRecordDataBaseHelper.GetByBatchIds(batchIds);
            foreach (var record in records)
                NodeRecords.Add(record);

            DrawGanttChart(records, batchIds);
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
