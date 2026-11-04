using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Algorithms.Evidence;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.Rbac.CloudDrive;
using ColorVision.UI;
using ColorVision.UI.PropertyEditor;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ProjectARVRPro.AlgorithmEvidence;

public partial class AlgorithmEvidenceWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly IReadOnlyList<ProjectARVRReuslt> _records;
    private readonly int? _initialRecordId;
    private readonly IReadOnlyList<ImageEvidenceAlgorithm>? _customAlgorithms;
    private RgbCrossRegistrationParameters _crossParameters = new();
    private readonly GridDistortionOptions _distortionOptions = new();
    private readonly EvidenceSearchRegion _crossRegion = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly List<JsonElement> _reports = [];
    private readonly Dictionary<ImageEvidenceObservation, IDisposable> _overlays = [];
    private readonly HashSet<ProjectARVRReuslt> _skipped = [];
    private readonly HashSet<string> _skippedFiles = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _localPaths = [];
    private string? _currentSourcePath;
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
        _customAlgorithms = algorithms;
        InitializeComponent();
        // Keep toolbar, Ctrl+O and context-menu opening within the evidence source lifecycle.
        CommandManager.AddPreviewExecutedHandler(this, ImageOpen_PreviewExecuted);
        ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
        EvidenceImage.EditorContext.DrawEditorContext.Zoombox.ContentMatrixChanged += OverlayZoomChanged;
        if (algorithms == null)
        {
            ServerAaLibrary.Text = ServerAaEvidenceSettings.FindLibrary();
            ServerAaParameters.ItemsSource = ServerAaEvidenceSettings.ReadParameters();
            ServerAaParameters.SelectedIndex = 0;
            ServerDistortionLibrary.Text = ServerAaLibrary.Text;
            ServerDistortionParameters.ItemsSource = ServerDistortionEvidenceSettings.ReadParameters();
            ServerDistortionParameters.SelectedIndex = 0;
            if (_records.FirstOrDefault(record => record.Id == initialRecordId) is { } selected)
            {
                if (ArvrEvidenceImages.Matches(selected, EvidenceCategory.RgbCross)) AlgorithmCategory.SelectedIndex = 1;
                else if (ArvrEvidenceImages.Matches(selected, EvidenceCategory.Distortion)) AlgorithmCategory.SelectedIndex = 2;
            }
        }
        else AlgorithmCategory.Visibility = Visibility.Collapsed;
        ConfigureAlgorithms();
        if (_records.FirstOrDefault(record => record.Id == initialRecordId) is { } initial && !ArvrEvidenceImages.Matches(initial, Category))
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
    private EvidenceCategory Category => (EvidenceCategory)Math.Max(0, AlgorithmCategory.SelectedIndex);
    private async void AlgorithmCategory_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _busy) return;
        ConfigureAlgorithms();
        // Keep the filter's meaning consistent when switching from W51 to pattern images.
        if (SourceFilter.SelectedIndex == 1) SourceFilter.SelectedIndex = 0;
        else if (IsLoaded) await ResetAsync();
    }
    private void ConfigureAlgorithms()
    {
        IReadOnlyList<ImageEvidenceAlgorithm> algorithms = _customAlgorithms ?? (Category switch
        {
            EvidenceCategory.RgbCross => [RgbCrossEvidenceAlgorithm.Create(_crossParameters, _crossRegion.ToRoi())],
            EvidenceCategory.Distortion => [GridDistortionEvidenceAlgorithms.CreateLocal(_distortionOptions), ServerDistortionEvidenceAlgorithm.Create("", "{}", "")],
            _ => [.. LuminousAreaEvidenceAlgorithms.Create(), ServerAaLuminousEvidenceAlgorithm.Create("", "{}", "")]
        });
        AlgorithmsList.ItemsSource = algorithms.Select(algorithm => new AlgorithmChoice(algorithm)).ToArray();
        bool builtIn = _customAlgorithms == null;
        ServerAaPanel.Visibility = builtIn && Category == EvidenceCategory.LuminousArea ? Visibility.Visible : Visibility.Collapsed;
        ServerDistortionPanel.Visibility = builtIn && Category == EvidenceCategory.Distortion ? Visibility.Visible : Visibility.Collapsed;
        PatternPanel.Visibility = builtIn && Category != EvidenceCategory.LuminousArea ? Visibility.Visible : Visibility.Collapsed;
        CrossRegionButton.Visibility = Category == EvidenceCategory.RgbCross ? Visibility.Visible : Visibility.Collapsed;
        AlgorithmLegend.Text = Category switch
        {
            EvidenceCategory.RgbCross => "R/G/B 分色叠图与候选框。点击开始验证后运行。",
            EvidenceCategory.Distortion => "分别查看本地点阵与服务端旧版的点位和指标；可独立开关叠图。",
            _ => "红色：RobustV2；蓝色：经典算法；黄色：服务端 AA。点击开始验证后运行。"
        };
        RefreshPatternDetails();
    }
    private void RefreshPatternDetails() => PatternDetails.Text = Category == EvidenceCategory.RgbCross
        ? $"{_crossParameters.Rows} × {_crossParameters.Columns}；边缘阈值 {_crossParameters.TargetThreshold:G}；搜索：{(_crossRegion.Enabled ? $"({_crossRegion.X}, {_crossRegion.Y}, {_crossRegion.Width}, {_crossRegion.Height})" : "整图")}"
        : $"{_distortionOptions.ExpectedRows} × {_distortionOptions.ExpectedCols}；{(_distortionOptions.BrightTarget ? "亮点" : "暗点")}";

    private void ConfigurePattern_Click(object sender, RoutedEventArgs e)
    {
        if (Category == EvidenceCategory.RgbCross)
        {
            RgbCrossConfigurationWindow editor = new(RgbCrossConfigurationDraft.SerializeParameters(_crossParameters)) { Owner = this };
            if (editor.ShowDialog() == true && editor.ResultParameters is { } parameters) _crossParameters = parameters;
        }
        else
        {
            PropertyEditorWindow editor = new(_distortionOptions, PropertyEditorEditMode.Transactional) { Owner = this };
            editor.Submitting += (_, args) =>
            {
                if (((GridDistortionOptions)editor.EditConfig).TryValidate(out string error)) return;
                args.Cancel = true; args.ErrorMessage = error;
            };
            editor.ShowDialog();
        }
        RefreshPatternDetails();
    }
    private void ConfigureCrossRegion_Click(object sender, RoutedEventArgs e)
    {
        PropertyEditorWindow editor = new(_crossRegion, PropertyEditorEditMode.Transactional) { Owner = this };
        editor.Submitting += (_, args) =>
        {
            var value = (EvidenceSearchRegion)editor.EditConfig;
            if (!value.Enabled || value.X >= 0 && value.Y >= 0 && value.Width >= 32 && value.Height >= 32) return;
            args.Cancel = true; args.ErrorMessage = "搜索坐标不得为负，宽高至少为 32 像素。";
        };
        editor.ShowDialog();
        RefreshPatternDetails();
    }
    internal async Task ResetAsync()
    {
        if (_busy || _closed) return;
        int selectedLocalIndex = IsLocalSelection ? Math.Max(0, _index) : 0;
        ClearResults();
        EvidenceImage.ReleaseImageContent();
        SourceDetails.Clear();
        _sources = ArvrEvidenceImages.Select(_records, SourceFilter.SelectedIndex == 0, Category);
        _index = IsLocalSelection ? Math.Min(selectedLocalIndex, Math.Max(0, _localPaths.Count - 1)) - 1 : _initialRecordId.HasValue
            ? Math.Max(-1, _sources.ToList().FindIndex(record => record.Id == _initialRecordId.Value) - 1)
            : -1;
        await NavigateAsync(1);
    }

    private bool IsLocalSelection => SourceFilter.SelectedIndex == 2;
    private int SourceCount => IsLocalSelection ? _localPaths.Count : _sources.Count;
    private int SkippedCount => _skipped.Count + _skippedFiles.Count;
    internal string? CurrentSourcePath => File.Exists(_currentSourcePath)
        && string.Equals(_currentSourcePath, EvidenceImage.Config.FilePath, StringComparison.OrdinalIgnoreCase) ? _currentSourcePath : null;

    private IReadOnlyList<ResultImageFileCandidate> GetSources(int index) => IsLocalSelection
        ? File.Exists(_localPaths[index]) ? [new(_localPaths[index], ResultImageFileKind.Original)] : []
        : ArvrEvidenceImages.GetSources(_sources[index]);

    private void SkipSource(int index)
    {
        if (IsLocalSelection) _skippedFiles.Add(_localPaths[index]);
        else _skipped.Add(_sources[index]);
    }

    private async void ImageOpen_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Open) return;
        e.Handled = true;
        await SelectImagesWithDialogAsync();
    }

    private async void SelectImages_Click(object sender, RoutedEventArgs e) => await SelectImagesWithDialogAsync();

    private async Task SelectImagesWithDialogAsync()
    {
        if (_busy || _closed) return;
        OpenFileDialog dialog = new()
        {
            Title = DisplayText.Get("选择用于算法实证的原图"), Multiselect = true,
            Filter = "图像|*.cvraw;*.cvcie;*.tif;*.tiff;*.png;*.bmp;*.jpg;*.jpeg;*.webp;*.ico;*.gif"
        };
        if (dialog.ShowDialog(this) == true) await SelectImagesAsync(dialog.FileNames);
    }

    internal async Task SelectImagesAsync(IEnumerable<string> paths)
    {
        if (_busy || _closed) return;
        string[] selected = paths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Length == 0) return;
        _localPaths = selected;
        _index = -1;
        _initialized = false;
        SourceFilter.SelectedIndex = 2;
        _initialized = true;
        await ResetAsync();
    }

    private void OpenCloudDrive_Click(object sender, RoutedEventArgs e)
    {
        try { CloudDriveWindow.ShowCloudDrive(); }
        catch (Exception ex) { StatusText.Text = "打开云盘失败：" + ex.Message; }
    }

    private void UploadCurrentImage_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSourcePath is not string path) { StatusText.Text = "当前原图文件不存在，请重新选择图像。"; return; }
        try { CloudDriveWindow.ShowCloudDrive([path]); }
        catch (Exception ex) { StatusText.Text = "添加原图到云盘失败：" + ex.Message; }
    }
    private async void Previous_Click(object sender, RoutedEventArgs e) => await NavigateAsync(-1);
    private async void Next_Click(object sender, RoutedEventArgs e) => await NavigateAsync(1);
    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        await RunCurrentAsync();
    }
    internal Task RunCurrentAsync() => !_busy && !_closed && _index >= 0 ? ShowAsync(_index, true) : Task.CompletedTask;

    private void SelectServerAaLibrary_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Filter = "AA 算法库 (CV_algorithm.dll)|CV_algorithm.dll|DLL (*.dll)|*.dll", FileName = "CV_algorithm.dll" };
        if (dialog.ShowDialog(this) == true) ServerAaLibrary.Text = dialog.FileName;
    }

    private void LoadServerAaParameters_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Filter = "AA 参数 (*.json;*.txt)|*.json;*.txt|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            string json = File.ReadAllText(dialog.FileName);
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("AA 参数必须是 JSON 对象。");
            AaEvidenceParameters choice = new("文件 · " + dialog.FileName, json);
            ServerAaParameters.ItemsSource = ((IReadOnlyList<AaEvidenceParameters>)ServerAaParameters.ItemsSource).Append(choice).ToArray();
            ServerAaParameters.SelectedItem = choice;
        }
        catch (Exception ex) { StatusText.Text = "加载 AA 参数失败：" + ex.Message; }
    }

    private ImageEvidenceAlgorithm SnapshotAlgorithm(ImageEvidenceAlgorithm algorithm) => algorithm.Id switch
    {
        ServerAaLuminousEvidenceAlgorithm.Id when ServerAaPanel.Visibility == Visibility.Visible =>
            ServerAaLuminousEvidenceAlgorithm.Create(ServerAaLibrary.Text,
                ((AaEvidenceParameters)ServerAaParameters.SelectedItem).Json, ((AaEvidenceParameters)ServerAaParameters.SelectedItem).Name),
        RgbCrossEvidenceAlgorithm.Id when PatternPanel.Visibility == Visibility.Visible => RgbCrossEvidenceAlgorithm.Create(_crossParameters, _crossRegion.ToRoi()),
        GridDistortionEvidenceAlgorithms.LocalId when PatternPanel.Visibility == Visibility.Visible => GridDistortionEvidenceAlgorithms.CreateLocal(_distortionOptions),
        ServerDistortionEvidenceAlgorithm.Id when ServerDistortionPanel.Visibility == Visibility.Visible =>
            ServerDistortionEvidenceAlgorithm.Create(ServerDistortionLibrary.Text,
                ((DistortionEvidenceParameters)ServerDistortionParameters.SelectedItem).Json, ((DistortionEvidenceParameters)ServerDistortionParameters.SelectedItem).Name),
        _ => algorithm
    };

    private void SelectServerDistortionLibrary_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Filter = "服务端畸变库 (CV_algorithm.dll)|CV_algorithm.dll|DLL (*.dll)|*.dll", FileName = "CV_algorithm.dll" };
        if (dialog.ShowDialog(this) == true) ServerDistortionLibrary.Text = dialog.FileName;
    }
    private void LoadServerDistortionParameters_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Filter = "畸变参数 (*.json;*.txt)|*.json;*.txt|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            string json = File.ReadAllText(dialog.FileName);
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("畸变参数必须是 JSON 对象。");
            DistortionEvidenceParameters choice = new("文件 · " + dialog.FileName, json);
            ServerDistortionParameters.ItemsSource = ((IReadOnlyList<DistortionEvidenceParameters>)ServerDistortionParameters.ItemsSource).Append(choice).ToArray();
            ServerDistortionParameters.SelectedItem = choice;
        }
        catch (Exception ex) { StatusText.Text = "加载畸变参数失败：" + ex.Message; }
    }

    internal async Task NavigateAsync(int direction)
    {
        if (_busy || _closed) return;
        for (int index = _index + direction; index >= 0 && index < SourceCount; index += direction)
        {
            if (GetSources(index).Count == 0)
            {
                SkipSource(index);
                continue;
            }
            await ShowAsync(index);
            if (_closed) return;
            // A source can be removed between enumeration and opening.
            if (GetSources(index).Count == 0)
            {
                SkipSource(index);
                continue;
            }
            return;
        }
        StatusText.Text = IsLocalSelection && SourceCount == 0 ? "请点击“选择图像”添加用于比对的原图。" : _index < 0
            ? $"没有可读取的图像。匹配 {SourceCount} 条，已跳过缺图 {SkippedCount} 条。"
            : $"已到{(direction > 0 ? "最后" : "最前")}一张。已跳过缺图 {SkippedCount} 条。";
    }

    private async Task ShowAsync(int index, bool runAlgorithms = false)
    {
        CancellationToken cancellationToken = _closing.Token;
        ImageEvidenceAlgorithm[] algorithms = runAlgorithms ? ((AlgorithmChoice[])AlgorithmsList.ItemsSource)
            .Where(choice => choice.Selected).Select(choice => SnapshotAlgorithm(choice.Algorithm)).ToArray() : [];
        if (runAlgorithms && algorithms.Length == 0) { StatusText.Text = "请至少勾选一个算法。"; return; }
        string? loadedPath = runAlgorithms && _index == index && _currentSourcePath != null && EvidenceImage.ViewBitmapSource != null
            && string.Equals(_currentSourcePath, EvidenceImage.Config.FilePath, StringComparison.OrdinalIgnoreCase) ? _currentSourcePath : null;
        _index = index;
        ProjectARVRReuslt? record = IsLocalSelection ? null : _sources[index];
        SetBusy(true);
        ClearResults();
        if (loadedPath == null) EvidenceImage.ReleaseImageContent();
        string sourceDetails = record == null ? $"自选图像 {index + 1} / {SourceCount}\n\n文件：{Path.GetFileName(_localPaths[index])}"
            : $"记录 {index + 1} / {SourceCount}\n\nID：{record.Id}\nSN：{record.SN}\n流程：{record.Model}\n时间：{record.CreateTime:yyyy-MM-dd HH:mm:ss}\n批次：{record.BatchId}";
        SourceDetails.Text = sourceDetails;
        string? sourcePath = loadedPath;
        _currentSourcePath = loadedPath;
        try
        {
            StatusText.Text = "正在读取原图…";
            List<string> errors = [];
            foreach (ResultImageFileCandidate source in loadedPath == null ? GetSources(index) : [])
            {
                try
                {
                    EvidenceImage.OpenImage(source.FilePath);
                    await EvidenceImage.ImageOpenCompletion.WaitAsync(cancellationToken);
                    using ImageFrameLease? probe = EvidenceImage.EditorContext.ProcessingContext.AcquireImageFrame();
                    if (probe == null) throw new InvalidDataException("图像没有可用的算法像素缓冲。");
                    sourcePath = source.FilePath;
                    _currentSourcePath = sourcePath;
                    break;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { errors.Add(ex.Message); EvidenceImage.ReleaseImageContent(); }
            }
            if (sourcePath == null)
            {
                if (GetSources(index).Count == 0) { SkipSource(index); return; }
                throw new InvalidDataException(string.Join(Environment.NewLine, errors));
            }

            if (!runAlgorithms)
            {
                if (EvidenceImage.ViewBitmapSource is BitmapSource bitmap)
                    SourceDetails.Text += $"\n尺寸：{bitmap.PixelWidth} × {bitmap.PixelHeight} px\n像素：{bitmap.Format}";
                SourceDetails.Text += $"\n来源：{sourcePath}";
                StatusText.Text = $"原图已加载，尚未计算。点击“开始验证”运行所选算法。已跳过缺图 {SkippedCount} 条。";
                return;
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
            SourceDetails.Text = sourceDetails + $"\n尺寸：{comparison.Width} × {comparison.Height} px\n像素：{comparison.PixelFormat}\n来源：{sourcePath}";
            _observations = observations;
            foreach (ImageEvidenceObservation observation in observations) AddResult(observation);
            _reports.Add(JsonSerializer.SerializeToElement(new
            {
                SourceKind = record == null ? "SelectedFile" : "History", RecordId = record?.Id, SN = record?.SN, Model = record?.Model, BatchId = record?.BatchId,
                ImagePath = sourcePath, comparison.Width, comparison.Height, comparison.PixelFormat, VerifiedAt = DateTimeOffset.Now,
                Results = observations
            }, JsonOptions));
            StatusText.Text = $"已验证 {observations.Count} 个算法，接受 {observations.Count(item => item.Result.Status == AlgorithmResultStatus.Succeeded)} 个。已跳过缺图 {SkippedCount} 条。请核对叠图。";
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception ex)
        {
            if (!_closed)
            {
                StatusText.Text = "图像验证失败，可继续下一张。";
                ResultsTabs.Items.Add(new TabItem { Header = "读取失败", Content = CreateText(ex.Message) });
                _reports.Add(JsonSerializer.SerializeToElement(new { SourceKind = record == null ? "SelectedFile" : "History", RecordId = record?.Id, ImagePath = sourcePath, Error = ex.Message }));
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
        if (result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation") is { } cross)
            foreach (var row in cross.Rows)
            {
                string Pixel(string key) => row[key].ValueKind == JsonValueKind.Number ? row[key].GetDouble().ToString("F3") : "—";
                summary.AppendLine($"{row["point"].GetString()}：R-G {Pixel("rToGMaximumEdge_px")} px  B-G {Pixel("bToGMaximumEdge_px")} px  {(row["valid"].GetBoolean() ? "可测" : "无效")}");
                string warning = row["warning"].GetString() ?? "";
                if (warning.Contains("partial_arm_coverage", StringComparison.Ordinal)) summary.AppendLine("  部分十字臂覆盖不足，请核对采样位置。");
                if (warning.Contains("saturated_samples", StringComparison.Ordinal)) summary.AppendLine("  存在饱和像素，阈值边缘可能偏移。");
            }
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
        NavigationPanel.IsEnabled = SourceFilter.IsEnabled = AlgorithmCategory.IsEnabled = AlgorithmsList.IsEnabled = ServerAaPanel.IsEnabled
            = ServerDistortionPanel.IsEnabled = PatternPanel.IsEnabled = EvidenceImage.IsEnabled = ImageSourceActions.IsEnabled = !busy;
        UploadCurrentImageButton.IsEnabled = !busy && CurrentSourcePath != null;
    }
    private void ClearResults()
    {
        _currentSourcePath = null;
        UploadCurrentImageButton.IsEnabled = false;
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
            File.WriteAllText(dialog.FileName, SerializeReport(), Encoding.UTF8);
            StatusText.Text = "验证报告已导出。";
        }
        catch (Exception ex) { StatusText.Text = $"报告导出失败：{ex.Message}"; }
    }
    internal string SerializeReport() => JsonSerializer.Serialize(new
    {
        Schema = "ColorVision.AlgorithmEvidence.v1", ExportedAt = DateTimeOffset.Now,
        ProjectVersion = typeof(AlgorithmEvidenceWindow).Assembly.GetName().Version?.ToString(),
        ImageEditorVersion = typeof(ImageView).Assembly.GetName().Version?.ToString(),
        Samples = _reports,
        Skipped = _skipped.Select(record => new { RecordId = record.Id, record.SN, record.Model, Reason = "SourceImageMissing" }),
        SkippedFiles = _skippedFiles.Select(path => new { ImagePath = path, Reason = "SourceImageMissing" })
    }, JsonOptions);
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
