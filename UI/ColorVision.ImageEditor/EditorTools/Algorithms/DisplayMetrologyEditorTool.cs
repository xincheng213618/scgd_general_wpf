using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms;

/// <summary>One analysis lifecycle for single images, binocular pairs and an explicitly ordered scan manifest.</summary>
internal sealed class DisplayMetrologyEditorTool(ImageProcessingContext image, DrawEditorContext? draw)
{
    internal sealed class ScanManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public EyeboxScanParameters Parameters { get; set; } = new();
        public string[] Frames { get; set; } = [];
    }

    internal static ScanManifest ReadManifest(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024) throw new InvalidDataException("扫描清单不得超过 64 KiB。");
        var manifest = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(path), AlgorithmJson.Options)
            ?? throw new InvalidDataException("扫描清单为空。");
        if (manifest.SchemaVersion != 1 || manifest.Parameters == null || !manifest.Parameters.Validate().IsValid
            || manifest.Frames == null || manifest.Frames.Length != manifest.Parameters.Columns * manifest.Parameters.Rows)
            throw new InvalidDataException("扫描清单须为 schemaVersion=1，并提供与行列数一致的帧列表及有效参数。");
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        manifest.Frames = manifest.Frames.Select(f => string.IsNullOrWhiteSpace(f)
            ? throw new InvalidDataException("扫描清单存在空路径。") : Path.GetFullPath(f, directory)).ToArray();
        if (manifest.Frames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Frames.Length)
            throw new InvalidDataException("扫描位置不得重复使用同一文件。");
        return manifest;
    }

    public async Task ExecuteAsync(AlgorithmDescriptor descriptor)
    {
        var owner = AlgorithmAnalysisWindowOwner.Capture();
        var inputs = new List<AlgorithmInput>();
        AlgorithmInvocation? invocation = null;
        CancellationTokenSource? cancellation = null;
        ImageAlgorithmProgressWindow? progress = null;
        bool presented = false;
        try
        {
            IAlgorithmParameters parameters = descriptor.ParameterSchema.Defaults.Deserialize(descriptor.ParameterType, AlgorithmJson.Options) as IAlgorithmParameters
                ?? throw new InvalidOperationException("参数无法创建。");
            string[] paths = [];
            if (descriptor.Id == DisplayMetrologyIds.Eyebox)
            {
                var dialog = new OpenFileDialog { Title = "选择 Eyebox 扫描清单（行优先排列）", Filter = "扫描清单|*.json", CheckFileExists = true };
                if (dialog.ShowDialog(owner.Current) != true) return;
                ScanManifest manifest = ReadManifest(dialog.FileName);
                parameters = manifest.Parameters; paths = manifest.Frames;
            }
            bool submitted = false;
            var editor = new PropertyEditorWindow(parameters, PropertyEditorEditMode.Transactional) { Title = descriptor.Name, Owner = owner.Current };
            editor.Submitted += (_, _) => submitted = true;
            editor.ShowDialog();
            if (!submitted) return;
            if (!parameters.Validate().IsValid) throw new InvalidDataException(string.Join("; ", parameters.Validate().Issues.Select(i => i.Message)));
            if (descriptor.Id == DisplayMetrologyIds.Binocular)
            {
                var dialog = new OpenFileDialog { Title = "当前图像为左眼：选择右眼图像", Filter = "图像|*.png;*.tif;*.tiff;*.bmp", CheckFileExists = true };
                if (dialog.ShowDialog(owner.Current) != true) return;
                paths = [dialog.FileName];
            }
            if (parameters is EyeboxScanParameters scan && paths.Length != scan.Columns * scan.Rows)
                throw new InvalidDataException("编辑后的扫描行列数与清单文件数不一致。");
            Guid document = image.DocumentInstanceId;
            var source = ImageAlgorithmInputFactory.Acquire(image, descriptor.Id == DisplayMetrologyIds.Binocular ? "left" : "source");
            inputs.Add(source);
            if (!long.TryParse(source.SourceRevision, NumberStyles.Integer, CultureInfo.InvariantCulture, out long revision))
                throw new InvalidDataException("无法取得当前图像版本。");
            if (descriptor.Id == DisplayMetrologyIds.Eyebox) { source.Image.Dispose(); inputs.Clear(); }
            invocation = AlgorithmInvocation.Create(descriptor.Id, parameters);
            cancellation = ImageAlgorithmAnalysisSession.Begin(image, document, revision, Guid.NewGuid(), invocation.InvocationId);
            progress = new ImageAlgorithmProgressWindow(descriptor.Name, cancellation);
            if (!owner.TryAssign(progress)) return;
            progress.Show();
            long pixels = inputs.Sum(i => (long)i.Image.Width * i.Image.Height);
            for (int i = 0; i < paths.Length; i++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                AlgorithmImageBuffer buffer = await Task.Run(() => LoadBounded(paths[i], DisplayMetrologyProvider.MaximumTotalPixels - pixels), cancellation.Token);
                inputs.Add(new AlgorithmInput { Name = descriptor.Id == DisplayMetrologyIds.Binocular ? "right" : $"sample-{i}", Image = buffer,
                    Ownership = AlgorithmInputOwnership.Transferred, SourceUri = paths[i], ColorSpace = "encoded-device-values" });
                pixels += (long)buffer.Width * buffer.Height;
            }
            if (!ImageAlgorithmAnalysisSession.IsCurrent(image, document, revision, invocation.InvocationId)) return;
            var result = await image.AlgorithmRuntime.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = invocation, Inputs = inputs,
                RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local
                    | (inputs.Count > 1 ? AlgorithmHostCapabilities.MultiInput : 0),
                Progress = new Progress<AlgorithmProgress>(value => progress.Report(value)),
            }, cancellation.Token);
            progress.Complete();
            ImageAlgorithmAnalysisSession.CompleteRun(image, invocation.InvocationId, cancellation);
            if (result.Status == AlgorithmResultStatus.Cancelled || cancellation.IsCancellationRequested
                || !ImageAlgorithmAnalysisSession.CanPresent(image, document, revision, invocation.InvocationId, out var previous))
            { result.Dispose(); return; }
            if (result.Status != AlgorithmResultStatus.Succeeded)
            {
                string message = string.Join(Environment.NewLine, result.Failures.Select(f => $"[{f.Code}] {f.Message}"));
                result.Dispose(); throw new InvalidDataException(message);
            }
            presented = AlgorithmAnalysisResultWindowTransaction.TryShow(result, owner,
                value => new DisplayMetrologyResultWindow(value, descriptor.Name, image, descriptor.Id == DisplayMetrologyIds.Eyebox ? null : draw),
                window => ImageAlgorithmAnalysisSession.Present(image, invocation.InvocationId, window),
                () => ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId), previous, out var failure);
            if (failure != null) throw failure;
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (owner.IsAvailable && cancellation?.IsCancellationRequested != true)
                AlgorithmAnalysisMessageBox.Show(owner, error.Message, descriptor.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            foreach (var input in inputs) input.Image.Dispose();
            progress?.Complete();
            if (invocation != null)
            {
                if (cancellation != null) ImageAlgorithmAnalysisSession.CompleteRun(image, invocation.InvocationId, cancellation);
                if (!presented) ImageAlgorithmAnalysisSession.Release(image, invocation.InvocationId);
            }
            cancellation?.Dispose();
        }
    }

    internal static AlgorithmImageBuffer LoadBounded(string path, long remainingPixels)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        if (decoder.Frames.Count != 1) throw new InvalidDataException("每个扫描文件必须只有一帧。");
        var frame = decoder.Frames[0];
        long pixels = (long)frame.PixelWidth * frame.PixelHeight;
        if (pixels > DisplayMetrologyProvider.MaximumFramePixels || pixels > remainingPixels)
            throw new InvalidDataException("导入图像超出显示计量像素预算。");
        return ImageAlgorithmInputFactory.Copy(frame);
    }
}

internal sealed class DisplayMetrologyResultWindow : Window, IDisposable
{
    private readonly AlgorithmResult _result;
    private AlgorithmAnalysisResultPresentation? _presentation;
    private IDisposable? _overlay;
    private bool _disposed;

    public DisplayMetrologyResultWindow(AlgorithmResult result, string title, ImageProcessingContext image, DrawEditorContext? draw)
    {
        _result = result;
        try
        {
            Title = title; Width = 1050; Height = 720; MinWidth = 650; MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.ApplyCaption();
            _presentation = DefaultAlgorithmAnalysisResultPresenter.CreatePresentation(result, title);
            var content = (DockPanel)DefaultAlgorithmAnalysisResultPresenter.CreateContent(_presentation);
            content.Background = SystemColors.WindowBrush;
            var tabs = content.Children.OfType<TabControl>().Single();
            var summary = new DataTable();
            summary.Columns.Add("指标"); summary.Columns.Add("数值"); summary.Columns.Add("单位");
            foreach (var metric in result.Artifacts.OfType<AlgorithmMeasurementArtifact>().SelectMany(a => a.Measurements))
                summary.Rows.Add(Label(metric.Name), metric.Value.ToString("G8", CultureInfo.InvariantCulture), metric.Unit);
            tabs.Items.Insert(0, new TabItem { Header = "测量汇总", Content = CreateTable(summary) });
            int position = 1;
            foreach (var artifact in result.Artifacts.OfType<AlgorithmTableArtifact>())
            {
                var table = new DataTable();
                foreach (var column in artifact.Columns) table.Columns.Add(column.Name);
                foreach (var row in artifact.Rows)
                    table.Rows.Add(artifact.Columns.Select(column => row.TryGetValue(column.Name, out var value) && value.ValueKind != JsonValueKind.Null
                        ? value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number.ToString("G8", CultureInfo.InvariantCulture) : value.ToString() : "—").ToArray());
                tabs.Items.Insert(position++, new TabItem { Header = Label(artifact.Name), Content = CreateTable(table) });
            }
            tabs.SelectedIndex = 0;
            RenderOptions.SetBitmapScalingMode(content, BitmapScalingMode.NearestNeighbor);
            Content = content;
            if (draw != null) _overlay = AlgorithmOverlayRenderer.Apply(image, draw, result);
            Closed += (_, _) => Dispose();
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _overlay?.Dispose(); _presentation?.Dispose(); _result.Dispose();
        if (IsVisible) Close();
    }

    private static DataGrid CreateTable(DataTable table) => new()
    {
        ItemsSource = table.DefaultView, IsReadOnly = true, AutoGenerateColumns = true,
        CanUserAddRows = false, CanUserDeleteRows = false, EnableRowVirtualization = true,
        EnableColumnVirtualization = true, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        HeadersVisibility = DataGridHeadersVisibility.Column, Margin = new Thickness(8), FontSize = 13, MinRowHeight = 26,
    };

    private static string Label(string name) => name switch
    {
        "bright_point_candidates" => "亮点候选数", "dark_point_candidates" => "暗点候选数",
        "line_candidates" => "线缺陷候选数", "mura_candidates" => "Mura 候选数",
        "excluded_border" => "排除边界宽度", "analyzed_pixels" => "有效分析像素数",
        "valid_pairs" => "有效通道对应点", "invalid_pairs" => "无效通道对应点",
        "maximum_color_displacement" => "最大套色偏移", "rms_color_displacement" => "套色偏移均方根",
        "valid_crosses" => "有效十字数", "invalid_crosses" => "无效十字数",
        "passed_crosses" => "OK 十字数", "failed_crosses" => "NG 十字数",
        "maximum_cross_axis_separation" => "十字轴线最大 RGB 分离", "maximum_cross_edge_separation" => "十字边缘最大 RGB 分离",
        "rms_cross_edge_separation" => "十字边缘分离均方根", "configured_edge_separation_limit" => "配置的边缘分离上限",
        "overall_threshold_result" => "九点总体判定（1=OK，0=NG）",
        "valid_alignment_cells" => "有效对准分区", "invalid_alignment_cells" => "无效对准分区",
        "mean_horizontal_disparity" => "平均水平视差", "mean_vertical_disparity" => "平均垂直视差",
        "right_over_left_scale" => "右眼 / 左眼倍率", "right_rotation_clockwise" => "右眼相对旋转（顺时针）",
        "similarity_residual_rms" => "相似变换残差均方根", "mean_right_over_left_signal" => "平均右眼 / 左眼信号比",
        "valid_signal_cells" => "有效信号分区", "candidate_count" => "杂散光候选数",
        "primary_peak_above_background" => "扣背景后主像峰值", "outside_integral_over_primary_region" => "外部积分 / 主像积分",
        "outside_mean_over_primary_peak" => "外部平均值 / 主像峰值", "accepted_sample_count" => "满足阈值的采样点",
        "reference_valid_pixels" => "参考帧有效像素", "four_corner_accepted_mesh_area" => "四角满足阈值的网格面积",
        "sampled_span_x" => "X 扫描跨度", "sampled_span_y" => "Y 扫描跨度",
        "valid_edge_cells" => "有效斜边分区", "invalid_edge_cells" => "无效斜边分区",
        "mtf50_crossing_cells" => "测得 MTF50 的分区", "minimum_field_mtf50" => "视场最小 MTF50",
        "maximum_field_mtf50" => "视场最大 MTF50", "RGB-displacement" => "RGB 位移明细", "RGB-cross-separation" => "九点十字 RGB 分离明细",
        "binocular-field" => "左右眼分区明细", "binocular-channel-consistency" => "左右眼颜色通道",
        "defect-candidates" => "缺陷候选明细", "stray-light-candidates" => "杂散光候选明细",
        "eyebox-scan" => "Eyebox 采样点", "field-sfr" => "视场清晰度", "sfr-curves" => "SFR 曲线数据",
        _ => name,
    };
}
