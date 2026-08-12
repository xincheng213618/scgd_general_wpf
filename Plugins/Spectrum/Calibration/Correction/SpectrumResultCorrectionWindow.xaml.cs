using Microsoft.Win32;
using ScottPlot;
using Spectrum.Menus;
using Spectrum.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Spectrum.Calibration.Correction;

public sealed class MenuSpectrumCorrection : SpectrumMenuIBase
{
    public override string OwnerGuid => ColorVision.UI.Menus.MenuItemConstants.Tool;
    public override string Header => "光谱修正";
    public override int Order => 2;

    public override void Execute()
    {
        MainWindow? mainWindow = MainWindow.Instance;
        if (mainWindow == null || !mainWindow.IsLoaded)
        {
            MessageBox.Show("请先打开 Spectrum 主窗口。", "光谱修正", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!mainWindow.TryGetCorrectionResult(out ViewResultSpectrum? result, out string reason))
        {
            MessageBox.Show(mainWindow, reason, "光谱修正", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            new SpectrumResultCorrectionWindow(result!, SpectrometerManager.Instance)
            {
                Owner = mainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(mainWindow, ex.GetBaseException().Message, "无法打开光谱修正",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

public partial class SpectrumResultCorrectionWindow : Window
{
    private readonly ViewResultSpectrum _result;
    private readonly SpectrometerManager _manager;
    private readonly MagnitudeCalibrationFile _sourceFile;
    private SpectrumCorrectionOutput? _preview;

    public ObservableCollection<SpectrumCorrectionRow> StandardRows { get; } = [];
    public ObservableCollection<SpectrumCorrectionRow> MeasuredRows { get; } = [];

    public SpectrumResultCorrectionWindow(ViewResultSpectrum result, SpectrometerManager manager)
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        if (string.IsNullOrWhiteSpace(manager.MaguideFile))
            throw new InvalidOperationException("当前标定组没有幅值 DAT 文件。");
        _sourceFile = MagnitudeCalibrationFile.Load(manager.MaguideFile);

        InitializeComponent();
        DataContext = this;
        InitializeResult();
        InitializePlot();
    }

    private void InitializeResult()
    {
        double[] absolute = SpectrumCorrectionCalculator.GetAbsoluteSpectrum(_result);
        for (int index = 0; index < absolute.Length; index++)
            MeasuredRows.Add(new SpectrumCorrectionRow(380d + 0.1d * index, absolute[index]));

        ResultText.Text = $"#{_result.Id} · {_result.CreateTime:yyyy-MM-dd HH:mm:ss}";
        GroupText.Text = string.IsNullOrWhiteSpace(_manager.ActiveCalibrationGroupName) ? "—" : _manager.ActiveCalibrationGroupName;
        SourceFileText.Text = _manager.MaguideFile;
        MeasuredBrightnessTextBox.Text = _result.fPh.ToString("G10", CultureInfo.CurrentCulture);
        UpdateBrightnessRatio();
        StatusText.Text = $"已载入当前选中的校正后光谱：{absolute.Length} 点。";
    }

    private void InitializePlot()
    {
        string font = Fonts.Detect("标准光谱 / 当前光谱 / 修正预测");
        PreviewPlot.Plot.Title("光谱修正预览");
        PreviewPlot.Plot.XLabel("波长 (nm)");
        PreviewPlot.Plot.YLabel("绝对光谱值");
        PreviewPlot.Plot.Axes.Title.Label.FontName = font;
        PreviewPlot.Plot.Axes.Left.Label.FontName = font;
        PreviewPlot.Plot.Axes.Bottom.Label.FontName = font;
        PlotMeasuredOnly();
    }

    private void PlotMeasuredOnly()
    {
        PreviewPlot.Plot.Clear();
        AddLine(MeasuredRows.Select(row => row.Wavelength).ToArray(), MeasuredRows.Select(row => row.Value).ToArray(),
            "当前校正后光谱", System.Drawing.Color.DodgerBlue);
        FinishPlot();
    }

    private void ImportStandardButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入标准绝对光谱",
            Filter = "光谱数据 (*.csv;*.txt;*.dat)|*.csv;*.txt;*.dat|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            SetStandardRows(ParseSpectrumText(File.ReadAllText(dialog.FileName)));
            StatusText.Text = $"已导入 {StandardRows.Count} 个标准谱点。";
        }
        catch (Exception ex)
        {
            ShowError("导入标准光谱失败", ex);
        }
    }

    private void StandardDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;
        try
        {
            SetStandardRows(ParseSpectrumText(Clipboard.GetText()));
            StatusText.Text = $"已粘贴 {StandardRows.Count} 个标准谱点。";
        }
        catch (Exception ex)
        {
            ShowError("粘贴标准光谱失败", ex);
        }
    }

    private void ClearStandardButton_Click(object sender, RoutedEventArgs e)
    {
        StandardRows.Clear();
        _preview = null;
        GeneratedFileText.Text = string.Empty;
        PlotMeasuredOnly();
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CorrectionTabs.SelectedIndex == 0)
            {
                double target = ParsePositive(TargetBrightnessTextBox.Text, "目标亮度");
                double factor = target / _result.fPh;
                SpectrumCorrectionCalculator.CorrectBrightness(_sourceFile, target, _result.fPh);
                double[] measured = SpectrumCorrectionCalculator.GetAbsoluteSpectrum(_result);
                PreviewPlot.Plot.Clear();
                AddLine(CreateWavelengths(), measured, "当前校正后光谱", System.Drawing.Color.DodgerBlue);
                AddLine(CreateWavelengths(), measured.Select(value => value * factor).ToArray(), "修正预测", System.Drawing.Color.OrangeRed);
                FinishPlot();
                StatusText.Text = $"亮度修正比例：{factor:G8}。";
                _preview = null;
            }
            else
            {
                _preview = CalculateFullSpectrum();
                PlotFullPreview(_preview);
                StatusText.Text = _preview.FilledFactorCount == 0
                    ? "完整光谱修正预览完成。"
                    : $"完整光谱修正预览完成；{_preview.FilledFactorCount} 个低信号点使用相邻修正倍率填补。";
            }
        }
        catch (Exception ex)
        {
            ShowError("无法计算修正预览", ex);
        }
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MagnitudeCalibrationFile corrected;
            string suffix;
            if (CorrectionTabs.SelectedIndex == 0)
            {
                corrected = SpectrumCorrectionCalculator.CorrectBrightness(
                    _sourceFile,
                    ParsePositive(TargetBrightnessTextBox.Text, "目标亮度"),
                    _result.fPh);
                suffix = "brightness";
            }
            else
            {
                _preview = CalculateFullSpectrum();
                corrected = _preview.CorrectedFile;
                suffix = "spectrum";
                PlotFullPreview(_preview);
            }

            string sourceDirectory = Path.GetDirectoryName(_manager.MaguideFile) ?? AppContext.BaseDirectory;
            string sourceName = Path.GetFileNameWithoutExtension(_manager.MaguideFile);
            var dialog = new SaveFileDialog
            {
                Title = "保存新的幅值标定文件",
                Filter = "幅值标定文件 (*.dat)|*.dat",
                InitialDirectory = sourceDirectory,
                FileName = $"{sourceName}_{suffix}_{DateTime.Now:yyyyMMdd_HHmmss}.dat",
                DefaultExt = ".dat",
                AddExtension = true,
                OverwritePrompt = false,
            };
            if (dialog.ShowDialog(this) != true) return;
            string saved = corrected.SaveNew(dialog.FileName);
            GeneratedFileText.Text = $"已生成：{saved}";
            StatusText.Text = "新 DAT 已生成；当前标定组和连接状态未改变。";
        }
        catch (Exception ex)
        {
            ShowError("生成新 DAT 失败", ex);
        }
    }

    private SpectrumCorrectionOutput CalculateFullSpectrum()
    {
        bool cellCommitted = StandardDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        bool rowCommitted = StandardDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (!cellCommitted || !rowCommitted || Validation.GetHasError(StandardDataGrid))
            throw new FormatException("标准光谱表格中有未通过校验的输入，请修正后重试。");

        return SpectrumCorrectionCalculator.CorrectSpectrum(
            _sourceFile,
            _result,
            StandardRows.Select(row => (row.Wavelength, row.Value)).ToArray());
    }

    private void PlotFullPreview(SpectrumCorrectionOutput output)
    {
        double[] predicted = output.MeasuredValues.Zip(output.CorrectionFactors, (value, factor) => value * factor).ToArray();
        PreviewPlot.Plot.Clear();
        AddLine(output.Wavelengths, output.MeasuredValues, "当前校正后光谱", System.Drawing.Color.DodgerBlue);
        AddLine(output.Wavelengths, output.StandardValues, "标准光谱", System.Drawing.Color.ForestGreen);
        AddLine(output.Wavelengths, predicted, "修正预测", System.Drawing.Color.OrangeRed);
        FinishPlot();
    }

    private void AddLine(double[] xs, double[] ys, string label, System.Drawing.Color color)
    {
        var scatter = PreviewPlot.Plot.Add.Scatter(xs, ys);
        scatter.LegendText = label;
        scatter.Color = ScottPlot.Color.FromColor(color);
        scatter.LineWidth = 1.4f;
        scatter.MarkerSize = 0;
    }

    private void FinishPlot()
    {
        PreviewPlot.Plot.ShowLegend();
        PreviewPlot.Plot.Axes.AutoScale();
        PreviewPlot.Refresh();
    }

    private void CorrectionTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != CorrectionTabs) return;
        _preview = null;
        GeneratedFileText.Text = string.Empty;
        PlotMeasuredOnly();
    }

    private void BrightnessInput_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateBrightnessRatio();
        _preview = null;
        GeneratedFileText.Text = string.Empty;
    }

    private void UpdateBrightnessRatio()
    {
        if (BrightnessRatioTextBox == null)
            return;

        BrightnessRatioTextBox.Text = TryParse(TargetBrightnessTextBox?.Text, out double target) && target > 0 && _result.fPh > 0
            ? (target / _result.fPh).ToString("G10", CultureInfo.CurrentCulture)
            : string.Empty;
    }

    private void SetStandardRows(IReadOnlyList<(double Wavelength, double Value)> rows)
    {
        StandardRows.Clear();
        foreach ((double wavelength, double value) in rows)
            StandardRows.Add(new SpectrumCorrectionRow(wavelength, value));
        _preview = null;
        GeneratedFileText.Text = string.Empty;
    }

    private static List<(double Wavelength, double Value)> ParseSpectrumText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("光谱文本为空。");
        List<(double Wavelength, double Value)> rows = [];
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim().TrimStart('\uFEFF');
            if (line.Length == 0) continue;
            string[] columns = line.Contains('\t')
                ? line.Split('\t', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : line.Contains(';')
                    ? line.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    : line.Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length != 2 || !TryParse(columns[0], out double wavelength) || !TryParse(columns[1], out double value))
                throw new FormatException($"第 {index + 1} 行必须包含有效的波长和值两列。");
            if (value < 0) throw new FormatException($"第 {index + 1} 行的光谱值不能为负数。");
            if (rows.Count > 0 && wavelength <= rows[^1].Wavelength)
                throw new FormatException("标准光谱波长必须严格递增。");
            rows.Add((wavelength, value));
        }
        if (rows.Count < 2) throw new FormatException("标准光谱至少需要两行数据。");
        return rows;
    }

    private static double[] CreateWavelengths() => Enumerable.Range(0, 4001).Select(index => 380d + 0.1d * index).ToArray();

    private static double ParsePositive(string text, string name)
    {
        if (!TryParse(text, out double value) || value <= 0) throw new FormatException($"{name}必须为有限正数。");
        return value;
    }

    private static bool TryParse(string? text, out double value) =>
        (double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)
         || double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        && double.IsFinite(value);

    private void ShowError(string title, Exception exception)
    {
        string message = exception.GetBaseException().Message;
        StatusText.Text = $"{title}：{message}";
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

public sealed class SpectrumCorrectionRow : INotifyPropertyChanged
{
    private double _wavelength;
    private double _value;

    public SpectrumCorrectionRow() { }
    public SpectrumCorrectionRow(double wavelength, double value) { _wavelength = wavelength; _value = value; }

    public double Wavelength
    {
        get => _wavelength;
        set { if (_wavelength.Equals(value)) return; _wavelength = value; PropertyChanged?.Invoke(this, new(nameof(Wavelength))); }
    }

    public double Value
    {
        get => _value;
        set { if (_value.Equals(value)) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
