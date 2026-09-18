using ColorVision.Core;
using ColorVision.Themes;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

public partial class SfrSimplePlotWindow : Window
{
    private readonly ImageFrameLease _lease;
    private readonly RoiRect _roi;
    private readonly Guid _documentId;
    private readonly DateTimeOffset _capturedAt = DateTimeOffset.Now;
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;
    private readonly int _sourceDepth;
    private SfrAnalysisOptions _options = new();
    private SfrAnalysisOptions? _measuredOptions;
    private SfrAnalysisResult? _result;
    private bool _busy;
    private bool _closed;
    private double _frequency = 0.25;
    private double _threshold = 0.5;

    public SfrSimplePlotWindow(ImageFrameLease lease, RoiRect roi, Guid documentId)
    {
        _lease = lease;
        _roi = roi;
        _documentId = documentId;
        HImage image = lease.Image;
        _sourceWidth = image.cols;
        _sourceHeight = image.rows;
        _sourceDepth = image.depth;
        InitializeComponent();
        this.ApplyCaption();
        Width = Math.Min(Width, Math.Max(MinWidth, SystemParameters.WorkArea.Width - 24));
        Height = Math.Min(Height, Math.Max(MinHeight, SystemParameters.WorkArea.Height - 24));
        SourceText.Text = $"ROI ({roi.X}, {roi.Y}) · {roi.Width} × {roi.Height} px · 输入 {image.depth} bit · 快照 {_capturedAt:HH:mm:ss}";
        RoiText.Text = "一条完整斜边，两侧保留均匀平台。橙色线为 L 通道拟合位置。";
        RoiImage.Source = CreatePreview(image, roi, _options);
        RoiCanvas.Width = RoiImage.Width = roi.Width;
        RoiCanvas.Height = RoiImage.Height = roi.Height;
        FitLine.StrokeThickness = Math.Max(1, Math.Max(roi.Width, roi.Height) / 160.0);
        Plot.CursorReadout += text => PlotHelp.Text = text;
        Loaded += async (_, _) => { if (_result == null) await AnalyzeAsync(); };
        Closed += (_, _) =>
        {
            _closed = true;
            if (!_busy) _lease.Dispose();
        };
    }

    private async Task AnalyzeAsync()
    {
        if (_busy || _closed) return;
        _busy = true;
        _result = null;
        Plot.Clear();
        PlotHelp.Text = string.Empty;
        MetricsGrid.ItemsSource = null;
        QualityGrid.ItemsSource = null;
        SamplesGrid.ItemsSource = null;
        FitLine.Visibility = Visibility.Collapsed;
        AnalyzeButton.IsEnabled = SettingsButton.IsEnabled = ExportCsvButton.IsEnabled = ExportJsonButton.IsEnabled = false;
        StatusText.Text = "正在分析固定图像快照...";
        try
        {
            SfrAnalysisOptions options = _options with { };
            _result = await Task.Run(() => SfrAnalyzer.Analyze(_lease.Image, _roi, options));
            if (_closed) return;
            _measuredOptions = options;
            int valid = _result.Channels.Count(c => c.Valid);
            var warnings = _result.Channels.SelectMany(c => c.Warnings).Distinct().Select(Explain);
            StatusText.Text = $"可计算通道 {valid}/{_result.Channels.Count}；{(valid == 0 ? "当前 ROI 不适合单斜边测量，请查看状态与质量诊断。" : "质量检查通过不等于产品达标。")}";
            string warningText = string.Join("；", warnings);
            if (warningText.Length > 0) StatusText.Text += "\n" + warningText;
            ShowR.Visibility = ShowG.Visibility = ShowB.Visibility = _result.Channels.Count == 1 ? Visibility.Collapsed : Visibility.Visible;
            QualityGrid.ItemsSource = _result.Channels.Select(c => new QualityRow(c.Channel,
                Format(c.PlateausAvailable ? c.Contrast : null), Format(c.PlateausAvailable ? c.Noise : null),
                c.PlateausAvailable ? c.Snr.ToString("G4", CultureInfo.CurrentCulture) : "—",
                Format(c.FitAvailable ? c.AngleDegrees : null), Format(c.FitAvailable ? c.FitRms : null),
                Format(c.SamplingAvailable ? c.BinCoverage : null, true), Format(c.ClippedFraction, true))).ToArray();
            SamplesGrid.ItemsSource = _result.Channels.Where(c => c.Valid).SelectMany(c => c.Frequencies.Select((f, i) => new SampleRow(c.Channel, f, c.Mtf[i]))).ToArray();
            RenderMetrics();
            RenderPlot();
            DrawFit();
            ExportCsvButton.IsEnabled = ExportJsonButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            if (!_closed) StatusText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            if (_closed) _lease.Dispose();
            else AnalyzeButton.IsEnabled = SettingsButton.IsEnabled = true;
        }
    }

    private void DrawFit()
    {
        var c = _result?.Channels.FirstOrDefault(c => c.Channel == "L" && c.Valid);
        if (c == null) return;
        double height = c.Rotated ? _roi.Width : _roi.Height;
        double x0 = c.EdgeIntercept, x1 = c.EdgeIntercept + c.EdgeSlope * (height - 1);
        if (c.Rotated)
        {
            FitLine.X1 = _roi.Width - 1; FitLine.Y1 = x0;
            FitLine.X2 = 0; FitLine.Y2 = x1;
        }
        else { FitLine.X1 = x0; FitLine.Y1 = 0; FitLine.X2 = x1; FitLine.Y2 = height - 1; }
        FitLine.Visibility = Visibility.Visible;
        RoiText.Text = $"测量方向：{(c.Rotated ? "垂直" : "水平")}；边缘偏离{(c.Rotated ? "水平" : "竖直")} {c.AngleDegrees:F2}°。\n输入编码：{_measuredOptions?.InputEncoding}；未自动平滑或重采样测量像素。";
    }

    private void RenderMetrics()
    {
        if (_result == null) return;
        MetricsGrid.ItemsSource = _result.Channels.Select(c => new MetricRow(
            c.Channel, Format(c.Valid ? c.Mtf50 : null), Format(c.Valid ? c.Mtf10 : null),
            Format(c.Valid ? SfrCurveQueries.AtFrequency(c.Frequencies, c.Mtf, _frequency) : null, true),
            Format(c.Valid ? SfrCurveQueries.AtFrequency(c.Frequencies, c.Mtf, 0.5) : null, true),
            Format(c.Valid ? SfrCurveQueries.Crossing(c.Frequencies, c.Mtf, _threshold) : null), ChannelState(c))).ToArray();
        AtFrequencyColumn.Header = $"MTF @ {_frequency:G3}";
        AtThresholdColumn.Header = $"频率 @ {_threshold:P0}";
    }

    private void RenderPlot()
    {
        if (_result == null || Plot == null || ShowL == null) return;
        var channels = new HashSet<string>();
        if (ShowL.IsChecked == true) channels.Add("L");
        if (ShowR.IsChecked == true) channels.Add("R");
        if (ShowG.IsChecked == true) channels.Add("G");
        if (ShowB.IsChecked == true) channels.Add("B");
        Plot.ShowResult(_result, ViewSelector.SelectedIndex, channels, ShowExtended.IsChecked == true);
        PlotHelp.Text = ViewSelector.SelectedIndex switch
        {
            1 => "ESF：沿边缘法线的亮暗过渡。平台起伏可能来自像素纹理、噪声或光照不均。",
            2 => "LSF：本图为用于傅里叶变换的加窗导数。多峰、拖尾和振铃可用于诊断。",
            _ => "圆点为 MTF50/10 交点；虚线标示 Nyquist 0.5 cy/pixel。展开后的右半区仅供诊断。"
        };
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e) => await AnalyzeAsync();
    private void View_Changed(object sender, RoutedEventArgs e) => RenderPlot();
    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var next = _options with { };
        bool submitted = false;
        var editor = new PropertyEditorWindow(next, PropertyEditorEditMode.Transactional) { Owner = this, Title = "斜边测量参数" };
        editor.Submitted += (_, _) => submitted = true;
        editor.ShowDialog();
        if (!submitted) return;
        try { next.Validate(); }
        catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, "参数无效"); return; }
        _options = next;
        RoiImage.Source = CreatePreview(_lease.Image, _roi, _options);
        await AnalyzeAsync();
    }

    private void Query_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(FrequencyInput.Text, out double frequency) || !double.IsFinite(frequency) || frequency < 0 || frequency > 0.5
            || !double.TryParse(ThresholdInput.Text, out double threshold) || !double.IsFinite(threshold) || threshold <= 0 || threshold >= 1)
        {
            QueryError.Text = "频率应为 0..0.5；响应应大于 0、小于 1。";
            return;
        }
        QueryError.Text = string.Empty;
        _frequency = frequency;
        _threshold = threshold;
        RenderMetrics();
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_result == null) return;
        var dialog = new SaveFileDialog { Filter = "完整测量 (*.json)|*.json", FileName = $"SFR_{_capturedAt:yyyyMMdd_HHmmss}.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var optionsJson = JsonDocument.Parse(_measuredOptions!.ToJson());
            var measurement = new
            {
                capturedAt = _capturedAt, documentId = _documentId, sourceRevision = _lease.Revision,
                sourceWidth = _sourceWidth, sourceHeight = _sourceHeight, sourceBitDepth = _sourceDepth,
                roi = new { x = _roi.X, y = _roi.Y, width = _roi.Width, height = _roi.Height },
                options = optionsJson.RootElement, targetFrequency = _frequency, targetResponse = _threshold,
                result = _result
            };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(measurement, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "保存失败"); }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_result == null) return;
        var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"SFR_{_capturedAt:yyyyMMdd_HHmmss}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true));
            writer.WriteLine($"# SFR 2.0; captured={_capturedAt:O}; source={_sourceWidth}x{_sourceHeight}; revision={_lease.Revision}; ROI={_roi.X}/{_roi.Y}/{_roi.Width}/{_roi.Height}");
            writer.WriteLine("# options=" + _measuredOptions!.ToJson());
            writer.WriteLine("Channel,Valid,Reason,MTF50_cyclesPerPixel,MTF10_cyclesPerPixel,MTF_at_target,Target_cyclesPerPixel,MTF_at_Nyquist,Target_response,Frequency_at_response,Warnings,Contrast,Plateau_noise,Snr,Angle_degrees,Fit_rms_pixels,Bin_coverage,Clipped_fraction");
            foreach (var c in _result.Channels)
                writer.WriteLine(string.Join(",", c.Channel, c.Valid, c.Reason, Number(c.Valid ? c.Mtf50 : null), Number(c.Valid ? c.Mtf10 : null),
                    Number(c.Valid ? SfrCurveQueries.AtFrequency(c.Frequencies, c.Mtf, _frequency) : null), Number(_frequency),
                    Number(c.Valid ? SfrCurveQueries.AtFrequency(c.Frequencies, c.Mtf, .5) : null), Number(_threshold),
                    Number(c.Valid ? SfrCurveQueries.Crossing(c.Frequencies, c.Mtf, _threshold) : null), string.Join(";", c.Warnings),
                    Number(c.PlateausAvailable ? c.Contrast : null), Number(c.PlateausAvailable ? c.Noise : null), Number(c.PlateausAvailable ? c.Snr : null),
                    Number(c.FitAvailable ? c.AngleDegrees : null), Number(c.FitAvailable ? c.FitRms : null), Number(c.SamplingAvailable ? c.BinCoverage : null), Number(c.ClippedFraction)));
            writer.WriteLine();
            writer.WriteLine("Channel,Series,X,Value");
            foreach (var c in _result.Channels.Where(c => c.Valid))
            {
                WriteSeries(writer, c.Channel, "MTF_cyclesPerPixel", c.Frequencies, c.Mtf);
                WriteSeries(writer, c.Channel, "ESF_pixel", c.EdgePositions, c.Esf);
                WriteSeries(writer, c.Channel, "LSF_pixel", c.LsfPositions, c.Lsf);
            }
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "导出失败"); }
    }

    private static void WriteSeries(StreamWriter writer, string channel, string series, double[] x, double[] y)
    {
        for (int i = 0; i < x.Length; i++) writer.WriteLine($"{channel},{series},{Number(x[i])},{Number(y[i])}");
    }
    private static string Number(double? value) => value?.ToString("G17", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string Format(double? value, bool percent = false) => value?.ToString(percent ? "P1" : "F4", CultureInfo.CurrentCulture) ?? "—";
    private static string ChannelState(SfrChannelAnalysis c)
    {
        if (!c.Valid) return "无效：" + Explain(c.Reason);
        string missing = !c.Mtf50.HasValue ? "；范围内未达到 MTF50/10" : !c.Mtf10.HasValue ? "；范围内未达到 MTF10" : string.Empty;
        return (c.Warnings.Length == 0 ? "有效" : "需注意：" + string.Join("、", c.Warnings.Select(Explain))) + missing;
    }
    internal static string Explain(string code) => code switch
    {
        "roi_too_small" => "ROI 太小（定向后至少 40×32 px）",
        "low_contrast_or_no_edge" => "对比度不足或没有完整边缘",
        "textured_or_noisy_plateaus" => "平台纹理/噪声过强；检查 RGB 子像素、摩尔纹和曝光",
        "multiple_or_noisy_edges" => "存在多条边缘或明显周期纹理/噪声",
        "edge_fit_failed" => "无法拟合边缘",
        "insufficient_edge_support" => "边缘离 ROI 边界过近",
        "edge_angle_out_of_range" => "倾角不在 1°..15°；建议约 5°",
        "edge_fit_residual_too_large" => "边缘弯曲、破碎或拟合不稳定",
        "insufficient_subpixel_coverage" => "亚像素采样覆盖不足",
        "invalid_lsf_dc" => "没有可归一化的有效边缘信号",
        "unknown_input_encoding" => "输入编码未知，仅供诊断",
        "clipped_pixels" => "存在削顶风险",
        "display_target_measurement_chain" => "显示屏系统响应；需复核像素栅格影响",
        _ => code
    };
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private sealed record MetricRow(string Channel, string Mtf50, string Mtf10, string AtFrequency, string AtNyquist, string AtThreshold, string State);
    private sealed record QualityRow(string Channel, string Contrast, string Noise, string Snr, string AngleDegrees, string FitRms, string BinCoverage, string ClippedFraction);
    private sealed record SampleRow(string Channel, double CyclesPerPixel, double Mtf);

    // Display-only nearest-neighbour thumbnail. Measurement always reads the retained full-precision frame.
    internal static unsafe BitmapSource CreatePreview(HImage image, RoiRect roi, SfrAnalysisOptions options)
    {
        if (image.depth is not (8 or 16 or 32 or 64) || image.channels is not (1 or 3 or 4)) throw new ArgumentException("不支持的 SFR 像素格式。");
        if (image.pData == IntPtr.Zero || roi.X < 0 || roi.Y < 0 || roi.Width <= 0 || roi.Height <= 0 || roi.Width > image.cols || roi.Height > image.rows
            || roi.X > image.cols - roi.Width || roi.Y > image.rows - roi.Height) throw new ArgumentException("无效的 SFR 预览区域。");
        int width = Math.Min(600, roi.Width), height = Math.Min(600, roi.Height);
        int pixelBytes = image.channels * image.depth / 8;
        int stride = image.stride > 0 ? image.stride : checked(image.cols * pixelBytes);
        if (image.stride < 0 || stride < (long)image.cols * pixelBytes) throw new ArgumentException("无效的 SFR 像素行跨度。");
        double white = options.WhiteLevel == 0 ? image.depth == 8 ? 255 : image.depth == 16 ? 65535 : 1 : options.WhiteLevel;
        double range = white - options.BlackLevel;
        byte[] pixels = new byte[width * height * 3];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            byte* source = (byte*)image.pData + (long)(roi.Y + y * roi.Height / height) * stride + (roi.X + x * roi.Width / width) * pixelBytes;
            for (int c = 0; c < 3; c++)
            {
                int channel = image.channels == 1 ? 0 : c;
                double raw = image.depth switch { 8 => source[channel], 16 => ((ushort*)source)[channel], 32 => ((float*)source)[channel], _ => ((double*)source)[channel] };
                double value = (raw - options.BlackLevel) / range;
                pixels[(y * width + x) * 3 + c] = double.IsFinite(value) ? (byte)Math.Round(Math.Clamp(value, 0, 1) * 255) : (byte)0;
            }
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, width * 3);
        bitmap.Freeze();
        return bitmap;
    }
}
