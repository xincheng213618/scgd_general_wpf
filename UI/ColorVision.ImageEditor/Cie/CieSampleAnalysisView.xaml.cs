using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace ColorVision.ImageEditor.Cie;

public partial class CieSampleAnalysisView : UserControl
{
    private readonly List<CieAnalysisSample> _samples = new();
    private readonly List<CheckBox> _gamutOptions = new();
    private List<CieAnalysisRow> _rows = new();
    private CieAnalysisSettings _settings = new();
    private Guid? _referenceId;
    private CieAnalysisSample? _sourceSample;
    private bool _ready;
    private bool _refreshing;
    private bool _dirty;
    private string? _sessionPath;
    private static readonly Color[] MarkerColors = { Colors.RoyalBlue, Colors.OrangeRed, Colors.SeaGreen, Colors.MediumVioletRed, Colors.DarkOrange, Colors.Teal, Colors.SlateBlue, Colors.Sienna };

    public CieSampleAnalysisView()
    {
        InitializeComponent();
        WhitePresetCombo.ItemsSource = CieIlluminants.Defaults;
        foreach (CieGamut gamut in CieGamuts.Defaults)
        {
            var check = new CheckBox { Content = gamut.Name, Tag = gamut, IsChecked = gamut.Name == "sRGB" };
            check.Checked += Display_Changed;
            check.Unchecked += Display_Changed;
            GamutOptions.Children.Add(check);
            _gamutOptions.Add(check);
        }
        AnalysisDiagram.CursorTextChanged += (_, text) => CursorText.Text = string.IsNullOrEmpty(text) ? "双击样品点可选中；双击空白色度位置可填入 xy。" : text;
        AnalysisDiagram.PointPicked += Diagram_PointPicked;
        _ready = true;
        ApplySettingsToControls();
        System.Windows.Automation.AutomationProperties.SetName(WhiteX, "参考白 x");
        System.Windows.Automation.AutomationProperties.SetName(WhiteY, "参考白 y");
        System.Windows.Automation.AutomationProperties.SetName(WhiteLuminance, "绝对参考白亮度 cd/m²");
        System.Windows.Automation.AutomationProperties.SetName(Threshold, "Delta E 00 阈值");
        System.Windows.Automation.AutomationProperties.SetName(JncdStep, "JNCD 步长");
        RefreshRows();
    }

    public event Action<string, CieChromaticity>? PrimarySelected;
    public event EventHandler? SessionChanged;
    public bool HasUnsavedChanges => _dirty;
    private void MarkChanged()
    {
        _dirty = true;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
    public IReadOnlyList<CieAnalysisRow> Rows => _rows;
    public CieDiagramView Diagram => AnalysisDiagram;
    internal CieAnalysisSettings CalculationSettings => _settings;
    public CieAnalysisSession GetSession() => new() { Settings = _settings, Samples = _samples.ToList(), ReferenceId = _referenceId };

    internal void SetReferenceWhite(CieChromaticity white, double luminance)
    {
        CieAnalysisSettings settings = _settings with { WhiteX = white.X, WhiteY = white.Y, AbsoluteWhiteLuminance = luminance };
        settings.Validate();
        if (_settings.White == white && _settings.AbsoluteWhiteLuminance == luminance) return;
        _settings = settings;
        _ready = false;
        WhiteX.Text = white.X.ToString("G17", CultureInfo.InvariantCulture);
        WhiteY.Text = white.Y.ToString("G17", CultureInfo.InvariantCulture);
        WhiteLuminance.Text = luminance.ToString("G17", CultureInfo.InvariantCulture);
        WhitePresetCombo.SelectedItem = CieIlluminants.Defaults.FirstOrDefault(w => CieAnalysisMath.Distance(w.Chromaticity, white) < 1e-9);
        _ready = true;
        MarkChanged();
        RefreshRows();
        Status("CIE 计算参考白已更新；样品 XYZ 原值保持不变。");
    }

    public void LoadSession(CieAnalysisSession session)
    {
        session.Validate();
        _samples.Clear();
        _samples.AddRange(session.Samples);
        _settings = session.Settings;
        _referenceId = session.ReferenceId;
        ApplySettingsToControls();
        RefreshRows();
        _dirty = false;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddSamples(IEnumerable<CieAnalysisSample> samples)
    {
        var batch = samples.ToList();
        if (_samples.Count + batch.Count > CieAnalysisSession.MaximumSamples)
            throw new ArgumentException($"最多支持 {CieAnalysisSession.MaximumSamples} 个样品，请分批建立会话。");
        foreach (CieAnalysisSample sample in batch) sample.Validate();
        if (_samples.Concat(batch).Select(s => s.Id).Distinct().Count() != _samples.Count + batch.Count)
            throw new ArgumentException("样品 ID 重复。");
        _samples.AddRange(batch);
        MarkChanged();
        RefreshRows(batch.LastOrDefault()?.Id);
    }

    public void SetReference(Guid? id)
    {
        if (id.HasValue && !_samples.Any(s => s.Id == id)) throw new ArgumentException("参考样品不存在。");
        _referenceId = id;
        MarkChanged();
        RefreshRows();
    }

    public void SetSourceSample(CieAnalysisSample? sample)
    {
        _sourceSample = sample;
        CaptureSourceButton.IsEnabled = sample != null;
        CaptureSourceButton.ToolTip = sample == null ? "色度图暂无有效当前点" : $"{sample.Name} · {sample.Source}";
    }

    private CieAnalysisRow? Selected => SamplesGrid.SelectedItem as CieAnalysisRow;
    private CieInputSpace InputSpace => Enum.Parse<CieInputSpace>((string)((ComboBoxItem)InputSpaceCombo.SelectedItem).Tag);
    private static double Number(TextBox box)
    {
        if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            throw new ArgumentException($"“{box.Text}”不是有效数字，请使用小数点 .。");
        return value;
    }

    private void Execute(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusText.Foreground = Brushes.IndianRed;
            StatusText.Text = ex.Message;
        }
    }

    private void Status(string text)
    {
        StatusText.ClearValue(TextBlock.ForegroundProperty);
        StatusText.Text = text;
    }

    private CieAnalysisSample ReadInput() => CieAnalysisSample.Create(SampleName.Text, SampleGroup.Text, "手动输入", InputSpace,
        Number(Value1), Number(Value2), InputSpace == CieInputSpace.Xy ? 0 : Number(Value3),
        BasisCombo.SelectedIndex == 1 ? CieSampleBasis.Absolute : CieSampleBasis.Relative, _settings);

    private void AddSample_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        CieAnalysisSample sample = ReadInput();
        AddSamples(new[] { sample });
        SampleName.Text = $"样品 {_samples.Count + 1}";
        Status($"已添加 {sample.Name}。可在下方设定参考样品。");
    });

    private void UpdateSample_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        if (Selected is not { } row) throw new ArgumentException("请先选择要更新的样品。");
        CieAnalysisSample replacement = ReadInput() with { Id = row.Sample.Id };
        _samples[_samples.FindIndex(s => s.Id == row.Sample.Id)] = replacement;
        MarkChanged();
        RefreshRows(replacement.Id);
        Status($"已更新 {replacement.Name}。");
    });

    private void LoadSelected_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) { Status("请先选中一个样品。"); return; }
        SampleName.Text = row.Name;
        SampleGroup.Text = row.Group;
        InputSpaceCombo.SelectedIndex = row.Sample.Basis == CieSampleBasis.ChromaticityOnly ? 6 : 1;
        BasisCombo.SelectedIndex = row.Sample.Basis == CieSampleBasis.Absolute ? 1 : 0;
        Value1.Text = (row.Sample.Basis == CieSampleBasis.ChromaticityOnly ? row.Xy.X : row.Sample.Xyz.X).ToString("G17", CultureInfo.InvariantCulture);
        Value2.Text = (row.Sample.Basis == CieSampleBasis.ChromaticityOnly ? row.Xy.Y : row.Sample.Xyz.Y).ToString("G17", CultureInfo.InvariantCulture);
        Value3.Text = row.Sample.Xyz.Z.ToString("G17", CultureInfo.InvariantCulture);
    }

    private void InputSpace_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        string[] labels = InputSpace switch
        {
            CieInputSpace.XYZ => new[] { "X", "Y", "Z" }, CieInputSpace.UvY => new[] { "u′", "v′", "Y" },
            CieInputSpace.Lab => new[] { "L*", "a*", "b*" }, CieInputSpace.Luv => new[] { "L*", "u*", "v*" },
            CieInputSpace.SRgb => new[] { "R", "G", "B" }, _ => new[] { "x", "y", "Y" }
        };
        LabelV1.Text = labels[0]; LabelV2.Text = labels[1]; LabelV3.Text = labels[2];
        System.Windows.Automation.AutomationProperties.SetName(Value1, labels[0]);
        System.Windows.Automation.AutomationProperties.SetName(Value2, labels[1]);
        System.Windows.Automation.AutomationProperties.SetName(Value3, labels[2]);
        Value3.IsEnabled = InputSpace != CieInputSpace.Xy;
        BasisCombo.IsEnabled = InputSpace != CieInputSpace.Xy && InputSpace != CieInputSpace.SRgb;
        InputHint.Text = InputSpace switch
        {
            CieInputSpace.SRgb => "sRGB / D65 推算，相对 Y；不是仪器测量值。",
            CieInputSpace.Xy => "仅色坐标，不假定实际亮度；可比较 Δu′v′。",
            CieInputSpace.Lab or CieInputSpace.Luv => "输入以当前参考白解释，再保存为 XYZ；切换白点不会重解释原始输入。",
            _ => "相对样品使用参考白 Y=100；绝对样品使用配置的参考白亮度。"
        };
    }

    private void CaptureSource_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        if (_sourceSample == null) return;
        string name = _sourceSample.Name[..Math.Min(_sourceSample.Name.Length, 180)];
        AddSamples(new[] { _sourceSample with { Id = Guid.NewGuid(), Name = $"{name} {_samples.Count + 1}" } });
        Status("已加入色度图当前点快照；后续取点不会修改已加入的样品。");
    });

    private void RefreshRows(Guid? selectedId = null)
    {
        selectedId ??= Selected?.Sample.Id;
        CieAnalysisSample? reference = _samples.FirstOrDefault(s => s.Id == _referenceId);
        _rows = _samples.Select(sample => new CieAnalysisRow(sample, reference, _settings)).ToList();
        RefreshFilter(selectedId);
        EmptyHint.Visibility = _samples.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        double[] differences = _rows.Where(r => !r.IsReference && r.DeltaE00.HasValue).Select(r => r.DeltaE00!.Value).ToArray();
        BatchSummary.Text = $"共 {_samples.Count} 个样品 · 参考：{reference?.Name ?? "未设置"}" + (differences.Length == 0 ? " · 尚无可比较的完整色差" :
            $" · 可比较 {differences.Length} 个 · ΔE00 平均 {differences.Average():F3} / 最大 {differences.Max():F3} · 超阈值 {differences.Count(v => v > _settings.DeltaEThreshold)} 个");
    }

    private void RefreshFilter(Guid? selectedId)
    {
        _refreshing = true;
        string query = SampleFilter.Text.Trim();
        var visible = _rows.Where(r => query.Length == 0 || r.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || r.Group.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        SamplesGrid.ItemsSource = visible;
        SamplesGrid.SelectedItem = visible.FirstOrDefault(r => r.Sample.Id == selectedId) ?? visible.FirstOrDefault();
        _refreshing = false;
        RefreshDiagram();
        RefreshSelection();
    }

    private void Filter_Changed(object sender, TextChangedEventArgs e)
    {
        if (_ready) RefreshFilter(Selected?.Sample.Id);
    }

    private void Samples_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && !_refreshing) RefreshSelection();
    }

    private Color ColorFor(CieAnalysisSample sample) => sample.Id == _referenceId ? Colors.Black : MarkerColors[Math.Max(0, _samples.FindIndex(s => s.Id == sample.Id)) % MarkerColors.Length];

    private void RefreshDiagram()
    {
        AnalysisDiagram.SetReferenceWhite(_settings.White);
        if (AnalysisDiagram.DiagramKind != _settings.DiagramKind) AnalysisDiagram.SetDiagram(_settings.DiagramKind);
        AnalysisDiagram.ShowCctReference = _settings.ShowCct;
        AnalysisDiagram.ShowDaylightReference = _settings.ShowDaylight;
        AnalysisDiagram.SetGamuts(CieGamuts.Defaults.Where(g => _settings.Gamuts.Contains(g.Name)));
        bool referenceAtWhite = _samples.Any(s => s.Id == _referenceId && CieAnalysisMath.Distance(s.Xy, _settings.White) < 1e-6);
        AnalysisDiagram.SetReferenceMarkers(referenceAtWhite ? Array.Empty<CieMarker>() : new[] { new CieMarker("参考白", _settings.White, Colors.DimGray) });
        var visible = SamplesGrid.Items.Cast<CieAnalysisRow>().ToList();
        AnalysisDiagram.SetMarkers(visible.Where(r => r.Xy.IsFinite).Select(r => new CieMarker(visible.Count <= 40 ? r.Name : "", r.Xy, ColorFor(r.Sample))));
    }

    private void RefreshSelection()
    {
        CieAnalysisRow? row = Selected;
        UpdateSampleButton.IsEnabled = SetReferenceButton.IsEnabled = row != null;
        UseAsPrimaryMenu.IsEnabled = row?.Xy.IsFinite == true;
        AnalysisDiagram.SetSegments(Array.Empty<CieDiagramSegment>());
        if (row == null)
        {
            AnalysisDiagram.ClearSelection();
            SelectedTitle.Text = "尚未选中样品";
            SelectedCoordinates.Text = SelectedSpectrum.Text = DifferenceDetails.Text = "";
            DifferenceHeadline.Text = "ΔE00 —";
            DifferenceNote.Text = "在样品表中设置参考点，查看色差与批量统计。";
            return;
        }
        string F(double? number, string format = "F5") => CieAnalysisRow.Format(number, format);
        SelectedTitle.Text = $"{row.Name} · {row.BasisText}\n{row.Source}";
        SelectedCoordinates.Text = $"xy  {row.XText}  {row.YText}\nu′v′  {F(row.Uv.X)}  {F(row.Uv.Y)}";
        if (row.Lab.HasValue)
            SelectedCoordinates.Text += $"\nXYZ  {F(row.Sample.Xyz.X, "F3")}  {F(row.Sample.Xyz.Y, "F3")}  {F(row.Sample.Xyz.Z, "F3")}\nLab  {F(row.Lab.Value.L, "F3")}  {F(row.Lab.Value.A, "F3")}  {F(row.Lab.Value.B, "F3")}\nLuv  {F(row.Luv!.Value.L, "F3")}  {F(row.Luv.Value.U, "F3")}  {F(row.Luv.Value.V, "F3")}\nC*ab {F(CieAnalysisMath.Chroma(row.Lab.Value), "F3")}  hab {F(CieAnalysisMath.Hue(row.Lab.Value), "F2")}°";
        SelectedSpectrum.Text = row.Cct.IsFinite ? $"CCT≈{row.CctText} K  Duv {row.DuvText}" : "CCT / Duv：不适用或超出近似范围";
        var segments = new List<CieDiagramSegment>();
        CieWavelengthResult? wave = CieGamutGeometry.Wavelength(row.Xy, _settings.White, CieSpectrumLocus.Points);
        if (wave.HasValue)
        {
            SelectedSpectrum.Text += $"\n{(wave.Value.IsComplementary ? "补色波长" : "主波长")} {wave.Value.Wavelength:F1} nm\n{(wave.Value.IsComplementary ? "紫线纯度" : "激发纯度")} {wave.Value.Purity:P2}";
            if (_settings.ShowWavelength)
            {
                segments.Add(new(_settings.White, wave.Value.Boundary, Colors.DimGray, true));
                if (wave.Value.IsComplementary) segments.Add(new(_settings.White, wave.Value.SpectralPoint, Colors.Purple, true));
            }
        }
        else SelectedSpectrum.Text += "\n波长 / 纯度：不适用";
        CieAnalysisSample? reference = _samples.FirstOrDefault(s => s.Id == _referenceId);
        if (reference != null && reference.Id != row.Sample.Id) segments.Add(new(reference.Xy, row.Xy, Colors.DarkSlateBlue));
        AnalysisDiagram.SetSegments(segments);
        AnalysisDiagram.SetSelectedXy(row.Xy, ColorFor(row.Sample), row.Name);
        DifferenceHeadline.Text = $"ΔE00 {row.DeltaEText}";
        DifferenceDetails.Text = $"ΔE76 {F(row.DeltaE76, "F4")}   ΔE94 {F(row.DeltaE94, "F4")}\nΔEuv {F(row.DeltaLuv, "F4")}\nCMC 1:1 {F(row.Cmc11, "F4")}   2:1 {F(row.Cmc21, "F4")}\nΔu′v′ {row.DeltaUvText}   JNCD {row.JncdText}";
        DifferenceNote.Text = $"{row.Result} · 参考：{reference?.Name ?? "未设置"}\nΔE00 阈值 {_settings.DeltaEThreshold:G}；JNCD 步长 {_settings.JncdStep:G}。";
    }

    private void Diagram_PointPicked(object? sender, CieChromaticity xy)
    {
        CieAnalysisRow? nearest = SamplesGrid.Items.Cast<CieAnalysisRow>().Where(r => r.Xy.IsFinite)
            .OrderBy(r => CieAnalysisMath.Distance(AnalysisDiagram.Profile.ToDiagramPoint(r.Xy), AnalysisDiagram.Profile.ToDiagramPoint(xy))).FirstOrDefault();
        if (nearest != null && CieAnalysisMath.Distance(AnalysisDiagram.Profile.ToDiagramPoint(nearest.Xy), AnalysisDiagram.Profile.ToDiagramPoint(xy)) < 0.012)
        {
            SamplesGrid.SelectedItem = nearest;
            SamplesGrid.ScrollIntoView(nearest);
            return;
        }
        InputSpaceCombo.SelectedIndex = 6;
        Value1.Text = xy.X.ToString("F6", CultureInfo.InvariantCulture);
        Value2.Text = xy.Y.ToString("F6", CultureInfo.InvariantCulture);
        Status("已填入双击位置的 xy；点击“添加样品”锁定。该位置不是仪器测量数据。");
    }

    private void SetReference_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } row) { SetReference(row.Sample.Id); Status($"已设 {row.Name} 为参考样品。"); }
    }
    private void ClearReference_Click(object sender, RoutedEventArgs e) => SetReference(null);
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        HashSet<Guid> ids = SamplesGrid.SelectedItems.Cast<CieAnalysisRow>().Select(r => r.Sample.Id).ToHashSet();
        if (ids.Count == 0) return;
        _samples.RemoveAll(s => ids.Contains(s.Id));
        if (_referenceId.HasValue && ids.Contains(_referenceId.Value)) _referenceId = null;
        MarkChanged();
        RefreshRows();
        Status($"已删除 {ids.Count} 个样品。");
    }

    private void WhitePreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || WhitePresetCombo.SelectedItem is not CieMarker white) return;
        WhiteX.Text = white.Chromaticity.X.ToString("G", CultureInfo.InvariantCulture);
        WhiteY.Text = white.Chromaticity.Y.ToString("G", CultureInfo.InvariantCulture);
    }

    private void ApplySettings_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        CieAnalysisSettings settings = _settings with { WhiteX = Number(WhiteX), WhiteY = Number(WhiteY),
            AbsoluteWhiteLuminance = Number(WhiteLuminance), DeltaEThreshold = Number(Threshold), JncdStep = Number(JncdStep) };
        settings.Validate();
        _settings = settings;
        MarkChanged();
        RefreshRows();
        Status("计算条件已应用；样品 XYZ 原值保持不变。");
    });

    private void ApplySettingsToControls()
    {
        _ready = false;
        WhiteX.Text = _settings.WhiteX.ToString("G17", CultureInfo.InvariantCulture);
        WhiteY.Text = _settings.WhiteY.ToString("G17", CultureInfo.InvariantCulture);
        WhiteLuminance.Text = _settings.AbsoluteWhiteLuminance.ToString("G17", CultureInfo.InvariantCulture);
        Threshold.Text = _settings.DeltaEThreshold.ToString("G17", CultureInfo.InvariantCulture);
        JncdStep.Text = _settings.JncdStep.ToString("G17", CultureInfo.InvariantCulture);
        WhitePresetCombo.SelectedItem = CieIlluminants.Defaults.FirstOrDefault(w => CieAnalysisMath.Distance(w.Chromaticity, _settings.White) < 1e-9);
        DiagramKindCombo.SelectedIndex = (int)_settings.DiagramKind;
        ShowCct.IsChecked = _settings.ShowCct; ShowDaylight.IsChecked = _settings.ShowDaylight; ShowWavelength.IsChecked = _settings.ShowWavelength;
        foreach (CheckBox check in _gamutOptions) check.IsChecked = _settings.Gamuts.Contains(((CieGamut)check.Tag).Name);
        _ready = true;
    }

    private void Display_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings = _settings with { DiagramKind = (CieDiagramKind)DiagramKindCombo.SelectedIndex,
            ShowCct = ShowCct.IsChecked == true, ShowDaylight = ShowDaylight.IsChecked == true, ShowWavelength = ShowWavelength.IsChecked == true,
            Gamuts = _gamutOptions.Where(c => c.IsChecked == true).Select(c => ((CieGamut)c.Tag).Name).ToArray() };
        MarkChanged();
        RefreshDiagram();
        RefreshSelection();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => AnalysisDiagram.Zoom(0.8);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => AnalysisDiagram.Zoom(1.25);
    private void Fit_Click(object sender, RoutedEventArgs e) => AnalysisDiagram.ZoomUniform();
    private void ToggleInspector_Click(object sender, RoutedEventArgs e)
    {
        bool hide = Inspector.Visibility == Visibility.Visible;
        Inspector.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        InspectorColumn.Width = new GridLength(hide ? 0 : 280);
    }

    private void Demo_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        IReadOnlyList<CieAnalysisSample> samples = CieAnalysisIO.ImportSamples(CieAnalysisIO.CsvTemplate, _settings);
        AddSamples(samples);
        if (!_referenceId.HasValue) SetReference(samples[0].Id);
        SamplesGrid.SelectedItem = _rows.First(r => r.Sample.Id == samples[1].Id);
        Status("已添加标记为“示例”的两组数据；可更改参考白、阈值或继续添加样品。");
    });

    private static string ReadSmallFile(string path)
    {
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new ArgumentException("文件超过 16 MiB，请分批导入。");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private void Import_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        var dialog = new OpenFileDialog { Filter = "CSV / TSV|*.csv;*.tsv|文本|*.txt" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var samples = CieAnalysisIO.ImportSamples(ReadSmallFile(dialog.FileName), _settings);
        AddSamples(samples);
        Status($"已导入 {samples.Count} 个样品。");
    });

    private void Paste_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        if (!Clipboard.ContainsText()) throw new ArgumentException("剪贴板中没有文本。请复制包含表头的 Excel 区域。");
        var samples = CieAnalysisIO.ImportSamples(Clipboard.GetText(), _settings);
        AddSamples(samples);
        Status($"已从剪贴板加入 {samples.Count} 个样品。");
    });

    private void SaveText(string filter, string fileName, string content)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = fileName, AddExtension = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        File.WriteAllText(dialog.FileName, content, new UTF8Encoding(true));
        Status($"已保存：{dialog.FileName}");
    }
    private void Template_Click(object sender, RoutedEventArgs e) => Execute(() => SaveText("CSV|*.csv", "CieSamplesTemplate.csv", CieAnalysisIO.CsvTemplate));
    private void OpenActionMenu_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }
    private void ExportCsv_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        if (_rows.Count == 0) throw new ArgumentException("暂无样品可导出。");
        SaveText("CSV|*.csv", $"CieSamples_{DateTime.Now:yyyyMMdd_HHmmss}.csv", CieAnalysisIO.ExportSamples(_rows));
    });

    public byte[] CaptureDiagramPng()
    {
        AnalysisDiagram.UpdateLayout();
        int width = (int)Math.Ceiling(AnalysisDiagram.ActualWidth), height = (int)Math.Ceiling(AnalysisDiagram.ActualHeight);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("请先显示色度分析页面。");
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(AnalysisDiagram.Background, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(AnalysisDiagram), null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width * 2, height * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private void ExportImage_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        var dialog = new SaveFileDialog { Filter = "PNG|*.png", FileName = $"CieDiagram_{DateTime.Now:yyyyMMdd_HHmmss}.png" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        File.WriteAllBytes(dialog.FileName, CaptureDiagramPng());
        Status($"已保存当前图表视图：{dialog.FileName}");
    });
    private void ExportReport_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        if (_rows.Count == 0) throw new ArgumentException("暂无样品可导出。");
        SaveText("HTML 离线报告|*.html", $"CieReport_{DateTime.Now:yyyyMMdd_HHmmss}.html",
            CieAnalysisIO.ExportReport(GetSession(), _rows, CaptureDiagramPng()));
    });

    private bool SaveSession()
    {
        var dialog = new SaveFileDialog { Filter = "色度分析会话|*.cie-session.json", FileName = _sessionPath ?? "Analysis.cie-session.json" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return false;
        string json = CieAnalysisIO.SaveSession(GetSession());
        File.WriteAllText(dialog.FileName, json, new UTF8Encoding(false));
        _sessionPath = dialog.FileName;
        _dirty = false;
        Status($"已保存会话：{dialog.FileName}");
        return true;
    }
    private void SaveSession_Click(object sender, RoutedEventArgs e) => Execute(() => SaveSession());

    private bool ConfirmReplace()
    {
        if (!_dirty) return true;
        MessageBoxResult choice = MessageBox.Show(Window.GetWindow(this), "当前分析尚未保存。是否先保存会话？", "色度分析", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return choice == MessageBoxResult.No || choice == MessageBoxResult.Yes && SaveSession();
    }
    private void OpenSession_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        var dialog = new OpenFileDialog { Filter = "色度分析会话|*.cie-session.json;*.json" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        CieAnalysisSession session = CieAnalysisIO.LoadSession(ReadSmallFile(dialog.FileName));
        if (!ConfirmReplace()) return;
        LoadSession(session);
        _sessionPath = dialog.FileName;
        Status($"已恢复会话，共 {_samples.Count} 个样品。");
    });
    private void New_Click(object sender, RoutedEventArgs e) => Execute(() =>
    {
        if (!ConfirmReplace()) return;
        LoadSession(new());
        _sessionPath = null;
        Status("已创建空白分析会话。");
    });
    public bool ConfirmClose()
    {
        try { return ConfirmReplace(); }
        catch (Exception ex) { Status(ex.Message); return false; }
    }

    private void UseAsPrimary_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Xy.IsFinite: true } row) return;
        PrimarySelected?.Invoke((string)((MenuItem)sender).Tag, row.Xy);
        Status($"已将 {row.Name} 用作色域 {((MenuItem)sender).Tag} 原色。其他原色保持不变。");
    }
    private void ReferenceLink_Click(object sender, RequestNavigateEventArgs e)
    {
        Execute(() => Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }));
        e.Handled = true;
    }
}
