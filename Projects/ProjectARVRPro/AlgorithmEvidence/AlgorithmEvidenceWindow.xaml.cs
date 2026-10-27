using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Algorithms.Evidence;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro.AlgorithmEvidence;

public partial class AlgorithmEvidenceWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly IReadOnlyList<ProjectARVRReuslt> _records;
    private readonly int? _initialRecordId;
    private readonly CancellationTokenSource _closing = new();
    private readonly List<JsonElement> _reports = [];
    private readonly Dictionary<ImageEvidenceObservation, IDisposable> _overlays = [];
    private readonly HashSet<ProjectARVRReuslt> _skipped = [];
    private IReadOnlyList<ProjectARVRReuslt> _sources = [];
    private IReadOnlyList<ImageEvidenceObservation> _observations = [];
    private int _index = -1;
    private double _overlayZoom = double.NaN;
    private bool _initialized, _busy, _closed;

    public AlgorithmEvidenceWindow(IEnumerable<ProjectARVRReuslt> records, int? initialRecordId = null,
        IReadOnlyList<ImageEvidenceAlgorithm>? algorithms = null)
    {
        _records = records.ToArray();
        _initialRecordId = initialRecordId;
        InitializeComponent();
        ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
        EvidenceImage.EditorContext.DrawEditorContext.Zoombox.ContentMatrixChanged += OverlayZoomChanged;
        AlgorithmsList.ItemsSource = (algorithms ?? LuminousAreaEvidenceAlgorithms.Create()).Select(algorithm => new AlgorithmChoice(algorithm)).ToArray();
        if (_records.FirstOrDefault(record => record.Id == initialRecordId) is { } initial && !ArvrEvidenceImages.IsW51(initial))
            SourceFilter.SelectedIndex = 1;
        _initialized = true;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_index < 0 && !_busy) await ResetAsync();
    }
    private async void SourceFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized && IsLoaded && !_busy) await ResetAsync();
    }
    internal async Task ResetAsync()
    {
        if (_busy || _closed) return;
        ClearResults();
        EvidenceImage.ReleaseImageContent();
        SourceDetails.Clear();
        _sources = ArvrEvidenceImages.Select(_records, SourceFilter.SelectedIndex == 0);
        _index = _initialRecordId.HasValue
            ? Math.Max(-1, _sources.ToList().FindIndex(record => record.Id == _initialRecordId.Value) - 1)
            : -1;
        await NavigateAsync(1);
    }
    private async void Previous_Click(object sender, RoutedEventArgs e) => await NavigateAsync(-1);
    private async void Next_Click(object sender, RoutedEventArgs e) => await NavigateAsync(1);
    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (!_busy && _index >= 0) await ShowAsync(_index);
    }

    internal async Task NavigateAsync(int direction)
    {
        if (_busy || _closed) return;
        for (int index = _index + direction; index >= 0 && index < _sources.Count; index += direction)
        {
            if (ArvrEvidenceImages.GetSources(_sources[index]).Count == 0)
            {
                _skipped.Add(_sources[index]);
                continue;
            }
            await ShowAsync(index);
            if (_closed) return;
            // A source can be removed between enumeration and opening.
            if (ArvrEvidenceImages.GetSources(_sources[index]).Count == 0)
            {
                _skipped.Add(_sources[index]);
                continue;
            }
            return;
        }
        StatusText.Text = _index < 0
            ? $"没有可读取的图像。匹配 {_sources.Count} 条，已跳过缺图 {_skipped.Count} 条。"
            : $"已到{(direction > 0 ? "最后" : "最前")}一张。已跳过缺图 {_skipped.Count} 条。";
    }

    private async Task ShowAsync(int index)
    {
        CancellationToken cancellationToken = _closing.Token;
        ImageEvidenceAlgorithm[] algorithms = ((AlgorithmChoice[])AlgorithmsList.ItemsSource)
            .Where(choice => choice.Selected).Select(choice => choice.Algorithm).ToArray();
        if (algorithms.Length == 0) { StatusText.Text = "请至少勾选一个算法。"; return; }
        _index = index;
        ProjectARVRReuslt record = _sources[index];
        SetBusy(true);
        ClearResults();
        EvidenceImage.ReleaseImageContent();
        SourceDetails.Text = $"记录 {index + 1} / {_sources.Count}\n\nID：{record.Id}\nSN：{record.SN}\n流程：{record.Model}\n时间：{record.CreateTime:yyyy-MM-dd HH:mm:ss}\n批次：{record.BatchId}";
        string? sourcePath = null;
        try
        {
            StatusText.Text = "正在读取原图…";
            List<string> errors = [];
            foreach (ResultImageFileCandidate source in ArvrEvidenceImages.GetSources(record))
            {
                try
                {
                    EvidenceImage.OpenImage(source.FilePath);
                    await EvidenceImage.ImageOpenCompletion.WaitAsync(cancellationToken);
                    using ImageFrameLease? probe = EvidenceImage.EditorContext.ProcessingContext.AcquireImageFrame();
                    if (probe == null) throw new InvalidDataException("图像没有可用的算法像素缓冲。");
                    sourcePath = source.FilePath;
                    break;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { errors.Add(ex.Message); EvidenceImage.ReleaseImageContent(); }
            }
            if (sourcePath == null)
            {
                if (ArvrEvidenceImages.GetSources(record).Count == 0) { _skipped.Add(record); return; }
                throw new InvalidDataException(string.Join(Environment.NewLine, errors));
            }

            StatusText.Text = "正在同图比对…";
            ImageEvidenceComparison comparison = await ImageEvidenceRunner.CompareAsync(
                EvidenceImage.EditorContext.ProcessingContext, algorithms, cancellationToken);
            IReadOnlyList<ImageEvidenceObservation> observations = comparison.Observations;
            if (_closed || !EvidenceImage.IsCurrentImageRevision(comparison.SourceRevision))
            {
                foreach (ImageEvidenceObservation observation in observations) observation.Result.Dispose();
                return;
            }
            SourceDetails.Text += $"\n尺寸：{comparison.Width} × {comparison.Height} px\n像素：{comparison.PixelFormat}\n来源：{sourcePath}";
            _observations = observations;
            foreach (ImageEvidenceObservation observation in observations) AddResult(observation);
            _reports.Add(JsonSerializer.SerializeToElement(new
            {
                RecordId = record.Id, record.SN, record.Model, record.BatchId,
                ImagePath = sourcePath, comparison.Width, comparison.Height, comparison.PixelFormat, VerifiedAt = DateTimeOffset.Now,
                Results = observations
            }, JsonOptions));
            StatusText.Text = $"已验证 {observations.Count} 个算法，接受 {observations.Count(item => item.Result.Status == AlgorithmResultStatus.Succeeded)} 个。已跳过缺图 {_skipped.Count} 条。请核对叠图。";
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception ex)
        {
            if (!_closed)
            {
                StatusText.Text = "图像验证失败，可继续下一张。";
                ResultsTabs.Items.Add(new TabItem { Header = "读取失败", Content = CreateText(ex.Message) });
                _reports.Add(JsonSerializer.SerializeToElement(new { RecordId = record.Id, ImagePath = sourcePath, Error = ex.Message }));
            }
        }
        finally { if (!_closed) SetBusy(false); }
    }

    private void AddResult(ImageEvidenceObservation observation)
    {
        AlgorithmResult result = observation.Result;
        StackPanel content = new();
        CheckBox overlay = new() { Content = "显示此算法叠图", IsChecked = true, Margin = new Thickness(8) };
        DrawObservation(observation);
        overlay.Checked += (_, _) => DrawObservation(observation);
        overlay.Unchecked += (_, _) => { if (_overlays.Remove(observation, out IDisposable? rendering)) rendering.Dispose(); };
        content.Children.Add(overlay);
        string status = result.Status == AlgorithmResultStatus.Succeeded ? "算法接受（待核对）" : "算法拒绝 / 调用失败";
        StringBuilder summary = new($"{status}  ·  算法调用 {observation.ElapsedMs:F1} ms\n");
        foreach (AlgorithmMeasurement measurement in result.Artifacts.OfType<AlgorithmMeasurementArtifact>().SelectMany(artifact => artifact.Measurements))
            summary.AppendLine($"{measurement.Name}：{measurement.Value:F4} {measurement.Unit}");
        foreach (AlgorithmGeometry geometry in result.Artifacts.OfType<AlgorithmGeometryArtifact>().SelectMany(artifact => artifact.Geometries))
            summary.AppendLine($"{geometry.Id}：" + string.Join("  ", geometry.Points.Select(point => $"({point.X:F2}, {point.Y:F2})")));
        foreach (AlgorithmFailure failure in result.Failures) summary.AppendLine(failure.Message);
        foreach (AlgorithmDiagnosticMessage diagnostic in result.Diagnostics.Messages) summary.AppendLine(diagnostic.Message);
        content.Children.Add(CreateText(summary.ToString()));
        Expander diagnostics = new() { Header = "完整诊断（JSON）", Margin = new Thickness(8), Content = CreateText(JsonSerializer.Serialize(result, JsonOptions)) };
        content.Children.Add(diagnostics);
        ResultsTabs.Items.Add(new TabItem { Header = observation.AlgorithmName, Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        ResultsTabs.SelectedIndex = 0;
    }
    private static TextBox CreateText(string text) => new()
    {
        Text = text, IsReadOnly = true, IsUndoEnabled = false, TextWrapping = TextWrapping.Wrap,
        BorderThickness = new Thickness(0), Margin = new Thickness(8)
    };
    private void DrawObservation(ImageEvidenceObservation observation)
    {
        if (_overlays.Remove(observation, out IDisposable? previous)) previous.Dispose();
        _overlays[observation] = AlgorithmOverlayRenderer.Apply(
            EvidenceImage.EditorContext.ProcessingContext, EvidenceImage.EditorContext.DrawEditorContext, observation.Result);
    }
    private void OverlayZoomChanged(object? sender, EventArgs e)
    {
        double zoom = EvidenceImage.EditorContext.DrawEditorContext.ZoomRatio;
        if (_closed || zoom == _overlayZoom) return;
        _overlayZoom = zoom;
        foreach (ImageEvidenceObservation observation in _overlays.Keys.ToArray()) DrawObservation(observation);
    }
    private void SetBusy(bool busy)
    {
        _busy = busy;
        NavigationPanel.IsEnabled = SourceFilter.IsEnabled = AlgorithmsList.IsEnabled = EvidenceImage.IsEnabled = !busy;
    }
    private void ClearResults()
    {
        foreach (IDisposable overlay in _overlays.Values) overlay.Dispose();
        _overlays.Clear();
        ResultsTabs.Items.Clear();
        foreach (ImageEvidenceObservation observation in _observations) observation.Result.Dispose();
        _observations = [];
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new() { Filter = "JSON 报告 (*.json)|*.json", FileName = $"AlgorithmEvidence_{DateTime.Now:yyyyMMdd_HHmmss}.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new
            {
                Schema = "ColorVision.AlgorithmEvidence.v1", ExportedAt = DateTimeOffset.Now,
                ProjectVersion = typeof(AlgorithmEvidenceWindow).Assembly.GetName().Version?.ToString(),
                ImageEditorVersion = typeof(ImageView).Assembly.GetName().Version?.ToString(),
                Samples = _reports,
                Skipped = _skipped.Select(record => new { RecordId = record.Id, record.SN, record.Model, Reason = "SourceImageMissing" })
            }, JsonOptions), Encoding.UTF8);
            StatusText.Text = "验证报告已导出。";
        }
        catch (Exception ex) { StatusText.Text = $"报告导出失败：{ex.Message}"; }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _closed = true;
        _closing.Cancel();
        _closing.Dispose();
        EvidenceImage.EditorContext.DrawEditorContext.Zoombox.ContentMatrixChanged -= OverlayZoomChanged;
        ClearResults();
        EvidenceImage.Dispose();
    }
    private sealed class AlgorithmChoice(ImageEvidenceAlgorithm algorithm)
    {
        public ImageEvidenceAlgorithm Algorithm { get; } = algorithm;
        public string Id => Algorithm.Id;
        public string Name => Algorithm.Name;
        public bool Selected { get; set; } = true;
    }
}
