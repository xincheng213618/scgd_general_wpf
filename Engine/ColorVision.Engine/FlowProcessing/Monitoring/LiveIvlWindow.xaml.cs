using ColorVision.Common.MVVM;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Microsoft.Win32;
using ScottPlot;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.Engine.FlowProcessing.Monitoring;

public sealed class LiveIvlMenuItem : MenuItemBase
{
    public override string OwnerGuid => MenuItemConstants.Tool;
    public override string Header => EngineLocalization.Get("IV / IVL 实时曲线");
    public override int Order => 20;
    public override void Execute()
    {
        var existing = Application.Current.Windows.OfType<LiveIvlWindow>().FirstOrDefault();
        if (existing != null)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }
        new LiveIvlWindow { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
    }
}

public sealed class LiveIvlSeries : ViewModelBase
{
    public string Name { get; init; } = string.Empty;
    public bool Visible { get => visible; set { visible = value; OnPropertyChanged(); } }
    private bool visible = true;
}

public partial class LiveIvlWindow : Window
{
    private readonly LiveIvlSession session = new(LiveIvlReader.ReadAsync);
    private readonly ObservableCollection<LiveIvlSeries> series = [];
    private readonly ThemeManager themeManager = ThemeManager.Current;
    private FlowEngineManager? manager;
    private int refreshQueued;
    private bool closed;

    public LiveIvlWindow()
    {
        InitializeComponent();
        this.ApplyCaption();
        session.Updated += Session_Updated;
        SeriesList.ItemsSource = series;
        themeManager.CurrentUIThemeChanged += Theme_Changed;
        Draw();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (manager != null) return;
        manager = FlowEngineManager.GetInstance();
        manager.BatchRecord += Batch_Changed;
        manager.View.NodeExecutionCompleted += Node_Completed;
        manager.View.RunFinalized += Run_Finalized;
        FlowControls.DataContext = manager;
        FollowCurrentBatch();
        await RefreshAsync();
    }

    private void Batch_Changed(object? sender, MeasureBatchModel batch) => QueueRefresh();

    private void Node_Completed(object? sender, FlowNodeExecutionCompletedEventArgs completed)
    {
        if (completed.SerialNumber == (manager?.Batch?.Code ?? manager?.Batch?.Name)) QueueRefresh();
    }

    private void Run_Finalized(object? sender, FlowRunFinalizedData completed) => QueueRefresh();

    // Post without waiting: curve reads/rendering never hold up a flow node's completion callback.
    private void QueueRefresh()
    {
        if (Dispatcher.HasShutdownStarted || Interlocked.Exchange(ref refreshQueued, 1) != 0) return;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            Interlocked.Exchange(ref refreshQueued, 0);
            if (closed) return;
            FollowCurrentBatch();
            await RefreshAsync();
        }, DispatcherPriority.Background);
    }

    private void FollowCurrentBatch()
    {
        SelectBatch(manager?.Batch?.Id ?? 0);
        if (manager?.Batch is { Id: <= 0 })
            Status.Text = EngineLocalization.Get("当前流程没有已保存的批次，请启用流程结果保存并连接 MySQL。");
    }

    private void SelectBatch(int id)
    {
        if (id == session.BatchId) return;
        session.SelectBatch(id);
        BatchInfo.Text = id > 0 ? EngineLocalization.Format($"批次 #{id}") : EngineLocalization.Get("等待流程批次…");
        Status.Text = EngineLocalization.Get("等待读取已保存结果…");
        series.Clear();
        Values.ItemsSource = null;
        Summary.Text = EngineLocalization.Get("等待已保存的源表、相机 POI 或光谱结果");
        Draw();
    }

    private Task RefreshAsync() => session.RefreshAsync();

    private void Session_Updated(object? sender, EventArgs e)
    {
        if (closed) return;
        if (session.Error != null) { Status.Text = EngineLocalization.Format($"读取失败，下一节点完成时重试，也可重新读取：{session.Error}"); return; }
        var snapshot = session.Snapshot!;
        foreach (string name in snapshot.Points.Select(p => p.Series).Distinct())
            if (!series.Any(s => s.Name == name)) series.Add(new() { Name = name });
        FlowStatus batchStatus = snapshot.Status;
        if (batchStatus == FlowStatus.Ready && manager?.Batch?.Id == snapshot.BatchId && manager.FlowControl.IsFlowRun)
            batchStatus = FlowStatus.Runing;
        string state = batchStatus switch
        {
            FlowStatus.Ready => EngineLocalization.Get("就绪"), FlowStatus.Runing => EngineLocalization.Get("执行中"), FlowStatus.Paused => EngineLocalization.Get("已暂停"),
            FlowStatus.Completed => EngineLocalization.Get("已完成"), FlowStatus.Canceled => EngineLocalization.Get("已取消"), FlowStatus.OverTime => EngineLocalization.Get("已超时"), _ => EngineLocalization.Get("失败")
        };
        BatchInfo.Text = EngineLocalization.Format($"批次 #{snapshot.BatchId} · {snapshot.Name}\n状态：{state}");
        Summary.Text = EngineLocalization.Format($"{snapshot.Points.Count:N0} 个数据点 · {series.Count} 个系列 · 更新于 {DateTime.Now:HH:mm:ss}");
        Status.Text = EngineLocalization.Format($"正在监控 · 未配对亮度 {snapshot.Unpaired} · 无效记录 {snapshot.Invalid}")
            + (snapshot.Truncated ? EngineLocalization.Get(" · 已达到显示上限，仅显示最近记录") : "");
        Draw();
    }

    private void Draw()
    {
        if (Chart == null) return;
        int mode = Mode?.SelectedIndex ?? 0;
        string[] titles = [EngineLocalization.Get("IV 曲线"), EngineLocalization.Get("I-L 曲线"), EngineLocalization.Get("V-L 曲线"), EngineLocalization.Get("P-L 曲线"), EngineLocalization.Get("电流随时间变化"), EngineLocalization.Get("电压随时间变化"), EngineLocalization.Get("亮度随时间变化")];
        string[] xLabels = ["Voltage (V)", "Current (mA)", "Voltage (V)", "Power (mW)", "Elapsed time (s)", "Elapsed time (s)", "Elapsed time (s)"];
        string[] yLabels = ["Current (mA)", "Luminance (cd/m²)", "Luminance (cd/m²)", "Luminance (cd/m²)", "Current (mA)", "Voltage (V)", "Luminance (cd/m²)"];
        Chart.Plot.Clear();
        SpectrumPlotStyle.ApplyTheme(Chart.Plot, themeManager.CurrentUITheme == Theme.Dark);
        Chart.Plot.XLabel(xLabels[mode]); Chart.Plot.YLabel(yLabels[mode]);
        Chart.Plot.Legend.FontName = Fonts.Detect("中文");
        ChartTitle.Text = titles[mode];
        var snapshot = session.Snapshot;
        if (snapshot != null)
        {
            var visible = series.Where(s => s.Visible).Select(s => s.Name).ToHashSet();
            var rows = snapshot.Points.Where(p => visible.Contains(p.Series)).ToArray();
            foreach (var group in rows.GroupBy(p => p.Series))
            {
                // A scan has only its completion timestamp; it cannot supply a time trend.
                var coordinates = group.Where(p => mode < 4 || !p.IsScan).Select(p =>
                {
                    double elapsed = (p.Time - snapshot.Start).TotalSeconds;
                    return mode switch
                    {
                        0 => (p.Voltage, p.Current),
                        1 => (p.Current, p.Luminance),
                        2 => (p.Voltage, p.Luminance),
                        3 => (p.Voltage * p.Current, p.Luminance),
                        4 => ((double?)elapsed, p.Current),
                        5 => ((double?)elapsed, p.Voltage),
                        _ => ((double?)elapsed, p.Luminance)
                    };
                }).ToArray();
                if (!coordinates.Any(p => p.Item1.HasValue && p.Item2.HasValue)) continue;
                var scatter = Chart.Plot.Add.Scatter(coordinates.Select(p => p.Item1 ?? double.NaN).ToArray(), coordinates.Select(p => p.Item2 ?? double.NaN).ToArray());
                scatter.LegendText = group.Key; scatter.LineWidth = 2; scatter.MarkerSize = 4;
            }
            // Table stays bounded independently of the complete export.
            Values.ItemsSource = rows.OrderByDescending(p => p.Time).Take(500).ToArray();
        }
        Chart.Plot.ShowLegend();
        if (AutoScale?.IsChecked == true) Chart.Plot.Axes.AutoScale();
        Chart.Refresh();
    }

    private void FlowSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        manager?.View.SelectRuntimeFlowTemplate(FlowSelection.SelectedItem as TemplateModel<FlowParam>);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        FollowCurrentBatch(); await RefreshAsync();
    }
    private void PlotOption_Changed(object sender, SelectionChangedEventArgs e)
    {
        Draw();
        if (Chart != null) { Chart.Plot.Axes.AutoScale(); Chart.Refresh(); }
    }
    private void PlotOption_Click(object sender, RoutedEventArgs e) => Draw();
    private void ResetAxes_Click(object sender, RoutedEventArgs e) { Chart.Plot.Axes.AutoScale(); Chart.Refresh(); }
    private void Theme_Changed(Theme theme) => Dispatcher.InvokeAsync(Draw);

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (session.Snapshot is not { } snapshot) { Status.Text = EngineLocalization.Get("尚无可导出的数据。"); return; }
        var dialog = new SaveFileDialog { Filter = EngineLocalization.Get("CSV 文件|*.csv"), FileName = $"Live_IVL_{snapshot.BatchId}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var csv = new StringBuilder("BatchId,Series,Time,Voltage(V),Current(mA),Luminance(cd/m2),Scan\r\n");
            foreach (var p in snapshot.Points)
                csv.AppendLine(FormattableString.Invariant($"{snapshot.BatchId},\"{p.Series.Replace("\"", "\"\"")}\",{p.Time:O},{p.Voltage:R},{p.Current:R},{p.Luminance:R},{p.IsScan}"));
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            Status.Text = EngineLocalization.Get("当前批次显示数据已导出。");
        }
        catch (Exception ex) { Status.Text = EngineLocalization.Format($"导出失败：{ex.Message}"); }
    }
    private void SavePlot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = EngineLocalization.Get("PNG 图片|*.png"), FileName = $"Live_IVL_{session.BatchId}.png" };
        if (dialog.ShowDialog(this) != true) return;
        try { Chart.Plot.SavePng(dialog.FileName, Math.Max(640, (int)Chart.ActualWidth), Math.Max(400, (int)Chart.ActualHeight)); Status.Text = EngineLocalization.Get("曲线图片已保存。"); }
        catch (Exception ex) { Status.Text = EngineLocalization.Format($"保存失败：{ex.Message}"); }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        closed = true;
        if (manager != null)
        {
            manager.BatchRecord -= Batch_Changed;
            manager.View.NodeExecutionCompleted -= Node_Completed;
            manager.View.RunFinalized -= Run_Finalized;
        }
        themeManager.CurrentUIThemeChanged -= Theme_Changed;
        session.Updated -= Session_Updated;
        session.Dispose();
    }
}
