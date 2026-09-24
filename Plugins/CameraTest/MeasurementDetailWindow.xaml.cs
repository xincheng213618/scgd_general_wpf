using CameraTest.Application;
using ColorVision.Core;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;

namespace CameraTest;

public partial class MeasurementDetailWindow : Window
{
    public MeasurementDetailSnapshot Snapshot { get; }

    public MeasurementDetailWindow(MeasurementDetailSnapshot snapshot, string channel)
    {
        Snapshot = snapshot;
        InitializeComponent();
        this.ApplyCaption();
        Width = Math.Min(Width, Math.Max(MinWidth, SystemParameters.WorkArea.Width - 24));
        Height = Math.Min(Height, Math.Max(MinHeight, SystemParameters.WorkArea.Height - 24));
        IdentityText.Text = $"{snapshot.Target.Id} · {MeasurementOverview.Direction(snapshot.Edge.Id.ToString())}边";
        RoiImage.Source = snapshot.Preview;
        RoiCanvas.Width = RoiImage.Width = snapshot.Preview.PixelWidth;
        RoiCanvas.Height = RoiImage.Height = snapshot.Preview.PixelHeight;
        FitLine.StrokeThickness = Math.Max(1, Math.Max(RoiCanvas.Width, RoiCanvas.Height) / 160);
        FrequencyLabel.Text = $"MTF @ {snapshot.Frequency:G} cy/pixel";
        var m = snapshot.Measurement;
        var options = m.Options;
        string encoding = options.InputEncoding switch
        {
            SfrInputEncoding.Linear => "线性信号（不做 Gamma 解码）",
            SfrInputEncoding.Srgb => "sRGB 分段解码",
            SfrInputEncoding.Power => $"幂函数解码 · 指数 {options.DecodeExponent:G}",
            _ => "未知（未做 Gamma 解码，仅供诊断）"
        };
        var roi = snapshot.IsSearchPreview ? snapshot.Target.SearchRoi : snapshot.Edge.Roi;
        ConditionsText.Text = $"原图 {m.Width} × {m.Height} · {m.BitDepth} bit · {m.Channels} 通道\n区域 ({roi.X}, {roi.Y}) · {roi.Width} × {roi.Height} px\n输入：{encoding}\n黑电平 {options.BlackLevel:G} · 白电平 {(options.WhiteLevel == 0 ? "类型满量程" : options.WhiteLevel.ToString("G"))}\n采集 {m.CapturedAt:yyyy-MM-dd HH:mm:ss.fff}\n算法 {snapshot.Edge.Analysis?.AlgorithmVersion ?? "未计算"}";
        ConditionsText.ToolTip = m.Source;
        LimitsText.Text = $"本次质量门限：跨度 ≥ {options.MinimumContrast:P1}，信噪比 ≥ {options.MinimumSnr:G}，拟合残差 ≤ {options.MaximumFitRms:G} px。MTF50 / MTF10 在 0–0.5 cy/pixel 内查询；未穿过阈值时保留空值。";
        QualityGrid.ItemsSource = snapshot.Rows.Select(row =>
        {
            var c = row.ChannelAnalysis;
            return new { row.Channel, Contrast = Format(c?.PlateausAvailable == true ? c.Contrast : null, "P1"), Snr = Format(c?.PlateausAvailable == true ? c.Snr : null),
                Angle = Format(c?.FitAvailable == true ? c.AngleDegrees : null), FitRms = Format(c?.FitAvailable == true ? c.FitRms : null, "F3"),
                Coverage = Format(c?.SamplingAvailable == true ? c.BinCoverage : null, "P1"), Clipped = Format(c?.ClippedFraction, "P1"), State = row.StatusText };
        }).ToArray();
        ChannelSelector.ItemsSource = snapshot.Rows.Select(r => r.Channel).ToArray();
        ChannelSelector.SelectedItem = snapshot.Rows.Any(r => r.Channel == channel) ? channel : snapshot.Rows.First().Channel;
        ThemeManager.Current.CurrentUIThemeChanged += ThemeChanged;
        Closed += (_, _) => ThemeManager.Current.CurrentUIThemeChanged -= ThemeChanged;
    }

    private static string Format(double? value, string format = "F2") => value is { } number && double.IsFinite(number) ? number.ToString(format) : "—";
    private void ThemeChanged(Theme theme)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(RefreshResult); return; }
        RefreshResult();
    }
    private void Channel_Changed(object sender, SelectionChangedEventArgs e) { if (IsInitialized) RefreshResult(); }

    private void RefreshResult()
    {
        var row = Snapshot.Rows.FirstOrDefault(r => r.Channel == ChannelSelector.SelectedItem as string);
        if (row == null) return;
        Mtf50Value.Text = MeasurementOverview.Format(row, 0, Snapshot.Frequency);
        Mtf10Value.Text = MeasurementOverview.Format(row, 1, Snapshot.Frequency);
        FrequencyValue.Text = MeasurementOverview.Format(row, 2, Snapshot.Frequency);
        NyquistValue.Text = MeasurementOverview.Format(row, 3, Snapshot.Frequency);
        DiagnosticText.Text = MeasurementOverview.Describe(row, 0, Snapshot.Frequency);
        if (row.ChannelAnalysis is { Valid: true, Mtf10: null }) DiagnosticText.Text += "\nMTF10：曲线在 0–0.5 cy/pixel 内未穿过 10%，不填零。";
        bool dark = ThemeManager.Current.CurrentUITheme == Theme.Dark;
        foreach (var (plot, mode) in new[] { (MtfPlot, 0), (EsfPlot, 1), (LsfPlot, 2) })
        {
            plot.SetDarkTheme(dark);
            if (row.Analysis is { } result) plot.ShowResult(result, mode, [row.Channel == "Y (L)" ? "L" : row.Channel], false);
            else plot.Clear();
        }
        UnavailablePlot.Visibility = row.ChannelAnalysis is { Valid: true } ? Visibility.Collapsed : Visibility.Visible;
        PreviewTitle.Text = Snapshot.IsSearchPreview ? "未生成单边框 · 显示搜索范围"
            : row.ChannelAnalysis is { FitAvailable: true } ? "原始测量框 · 橙线为当前通道拟合边" : "原始测量框 · 暂无可用拟合线";
        FitLine.Visibility = Visibility.Collapsed;
        // A failed quality gate may still contain a useful fitted edge. Do not hide that evidence.
        if (!Snapshot.IsSearchPreview && row.ChannelAnalysis is { FitAvailable: true } c)
        {
            double height = c.Rotated ? RoiCanvas.Width : RoiCanvas.Height;
            double start = c.EdgeIntercept, end = start + c.EdgeSlope * (height - 1);
            if (c.Rotated) { FitLine.X1 = RoiCanvas.Width - 1; FitLine.Y1 = start; FitLine.X2 = 0; FitLine.Y2 = end; }
            else { FitLine.X1 = start; FitLine.Y1 = 0; FitLine.X2 = end; FitLine.Y2 = height - 1; }
            FitLine.Visibility = Visibility.Visible;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
