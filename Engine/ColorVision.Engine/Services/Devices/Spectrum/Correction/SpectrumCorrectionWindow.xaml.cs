using Microsoft.Win32;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Spectrum.Correction;

public partial class SpectrumCorrectionWindow : Window
{
    private readonly SpectrumCorrectionHost _host;
    private readonly CancellationTokenSource _lifetimeCts;
    private SpectrumMeasurementSnapshot? _snapshot;
    private ServiceSpectrumMeasurement? _measurement;
    private MagnitudeCalibrationFile? _currentFile;
    private SpectrumCorrectionResult? _previewResult;
    private string? _generatedFilePath;
    private string _sourceMagnitudeSha256 = string.Empty;
    private bool _operationInProgress;

    public ObservableCollection<SpectrumTableRow> StandardRows { get; } = [];
    public ObservableCollection<SpectrumTableRow> MeasuredRows { get; } = [];

    public SpectrumCorrectionWindow(SpectrumCorrectionHost host, CancellationToken cancellationToken)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        InitializeComponent();
        DataContext = this;
        StandardRows.CollectionChanged += StandardRows_CollectionChanged;
        InitializePlot();
        SetBusy(false);
    }

    private void InitializePlot()
    {
        string fontName = Fonts.Detect("标准光谱 / 实测光谱 / 校正预测");
        PreviewPlot.Plot.Title("光谱校正预览");
        PreviewPlot.Plot.XLabel("波长 (nm)");
        PreviewPlot.Plot.YLabel("绝对光谱值");
        PreviewPlot.Plot.Axes.Title.Label.FontName = fontName;
        PreviewPlot.Plot.Axes.Left.Label.FontName = fontName;
        PreviewPlot.Plot.Axes.Bottom.Label.FontName = fontName;
        PreviewPlot.Plot.Legend.FontName = fontName;
        PreviewPlot.Refresh();
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_operationInProgress)
            return;

        try
        {
            ClearCapturedMeasurement();
            SetBusy(true, "正在通过服务采集实测光谱……");
            SpectrumMeasurementSnapshot snapshot = await _host.CaptureAsync(_lifetimeCts.Token);
            _lifetimeCts.Token.ThrowIfCancellationRequested();

            ServiceSpectrumMeasurement measurement = new(
                snapshot.StartWavelength,
                snapshot.EndWavelength,
                snapshot.Interval,
                snapshot.RelativeSpectrum,
                snapshot.AbsoluteScale);

            if (string.IsNullOrWhiteSpace(snapshot.MagnitudeFilePath))
                throw new InvalidOperationException("当前标定组没有幅值标定文件路径。");
            if (!File.Exists(snapshot.MagnitudeFilePath))
                throw new FileNotFoundException("找不到服务本次测量使用的幅值标定文件。", snapshot.MagnitudeFilePath);

            MagnitudeCalibrationFile currentFile = MagnitudeCalibrationFile.Load(snapshot.MagnitudeFilePath);
            string actualHash = ComputeSha256(snapshot.MagnitudeFilePath);
            if (!string.IsNullOrWhiteSpace(snapshot.MagnitudeFileSha256)
                && !string.Equals(actualHash, snapshot.MagnitudeFileSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("当前 DAT 已在采集后发生变化。请重新采集，确保实测数据和旧系数来自同一份文件。");
            }

            _snapshot = snapshot;
            _measurement = measurement;
            _currentFile = currentFile;
            _previewResult = null;
            _generatedFilePath = null;
            _sourceMagnitudeSha256 = actualHash;
            ApplyButton.IsEnabled = false;

            DeviceText.Text = EmptyAsDash(snapshot.DeviceCode);
            SerialNumberText.Text = EmptyAsDash(snapshot.SerialNumber);
            CalibrationGroupText.Text = EmptyAsDash(snapshot.CalibrationGroupName);
            ResultText.Text = $"#{snapshot.ResultId} · {snapshot.MeasuredAt:yyyy-MM-dd HH:mm:ss}";
            MagnitudeFileText.Text = snapshot.MagnitudeFilePath;
            MeasuredBrightnessTextBox.Text = snapshot.PhotometricValue.ToString("G10", CultureInfo.CurrentCulture);
            UpdateBrightnessRatio();
            PopulateMeasuredRows(measurement.ToAbsoluteSpectrum());
            PlotMeasurementOnly();

            StatusText.Text = $"采集完成：{measurement.Count} 点，{snapshot.StartWavelength:G6}–{snapshot.EndWavelength:G6} nm，间隔 {snapshot.Interval:G6} nm。";
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
            StatusText.Text = "采集已取消。";
        }
        catch (Exception ex)
        {
            ShowError("采集实测数据失败", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PopulateMeasuredRows(SpectrumSeries spectrum)
    {
        MeasuredRows.Clear();
        for (int index = 0; index < spectrum.Count; index++)
            MeasuredRows.Add(new SpectrumTableRow(spectrum.Wavelengths[index], spectrum.Values[index]));
    }

    private void ImportStandardButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入标准绝对光谱",
            Filter = "光谱数据 (*.csv;*.txt;*.dat)|*.csv;*.txt;*.dat|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            SetStandardSpectrum(SpectrumTextParser.Parse(File.ReadAllText(dialog.FileName)));
            StatusText.Text = $"已导入 {StandardRows.Count} 个标准谱点：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            ShowError("导入标准光谱失败", ex);
        }
    }

    private void StandardDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        e.Handled = true;
        try
        {
            if (!Clipboard.ContainsText())
                throw new FormatException("剪贴板中没有文本数据。");
            SetStandardSpectrum(SpectrumTextParser.Parse(Clipboard.GetText()));
            StatusText.Text = $"已从剪贴板粘贴 {StandardRows.Count} 个标准谱点。";
        }
        catch (Exception ex)
        {
            ShowError("粘贴标准光谱失败", ex);
        }
    }

    private void SetStandardSpectrum(SpectrumSeries spectrum)
    {
        StandardRows.Clear();
        for (int index = 0; index < spectrum.Count; index++)
            StandardRows.Add(new SpectrumTableRow(spectrum.Wavelengths[index], spectrum.Values[index]));
        InvalidateCorrectionOutput();
    }

    private void StandardRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (SpectrumTableRow row in e.OldItems)
                row.PropertyChanged -= StandardRow_PropertyChanged;
        }
        if (e.NewItems != null)
        {
            foreach (SpectrumTableRow row in e.NewItems)
                row.PropertyChanged += StandardRow_PropertyChanged;
        }
        InvalidateCorrectionOutput();
    }

    private void StandardRow_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        InvalidateCorrectionOutput();

    private void ClearStandardButton_Click(object sender, RoutedEventArgs e)
    {
        StandardRows.Clear();
        InvalidateCorrectionOutput();
        StatusText.Text = "标准光谱已清空。";
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SpectrumCorrectionResult result = CalculateCorrection();
            _previewResult = result;
            PlotPreview(result);
            StatusText.Text = BuildPreviewStatus(result);
        }
        catch (Exception ex)
        {
            ShowError("无法计算校正预览", ex);
        }
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SpectrumCorrectionResult result = CalculateCorrection();
            _previewResult = result;
            PlotPreview(result);

            string sourcePath = _snapshot!.MagnitudeFilePath;
            string sourceDirectory = Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory;
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            string suffix = result.Mode == SpectrumCorrectionMode.BrightnessOnly ? "brightness" : "spectrum";
            var dialog = new SaveFileDialog
            {
                Title = "保存新幅值标定文件",
                Filter = "幅值标定文件 (*.dat)|*.dat",
                InitialDirectory = sourceDirectory,
                FileName = $"{sourceName}_{suffix}_{DateTime.Now:yyyyMMdd_HHmmss}.dat",
                AddExtension = true,
                DefaultExt = ".dat",
                OverwritePrompt = false,
            };
            if (dialog.ShowDialog(this) != true)
                return;

            string savedPath = result.CorrectedFile.SaveNew(dialog.FileName);
            _generatedFilePath = savedPath;
            GeneratedFileText.Text = $"已生成：{savedPath}";
            ApplyButton.IsEnabled = true;
            StatusText.Text = $"新 DAT 已生成，原文件未修改。{BuildFilledPointWarning(result)}";
        }
        catch (Exception ex)
        {
            ShowError("生成新 DAT 失败", ex);
        }
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_operationInProgress || _snapshot == null || string.IsNullOrWhiteSpace(_generatedFilePath))
            return;

        try
        {
            SetBusy(true, "正在应用新 DAT……");
            SpectrumCorrectionApplyResult result = await _host.ApplyMagnitudeFileAsync(
                new SpectrumCorrectionApplyRequest(
                    _generatedFilePath,
                    _snapshot.CalibrationGroupName,
                    _sourceMagnitudeSha256),
                _lifetimeCts.Token);

            if (!result.IsAccepted)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Message) ? "服务未能应用新 DAT。" : result.Message);

            bool restartRequested = result.Status == SpectrumCorrectionApplyStatus.RestartRequested;
            ClearCapturedMeasurement();
            GeneratedFileText.Text = string.IsNullOrWhiteSpace(result.AppliedMagnitudeFilePath)
                ? restartRequested ? "已应用，服务正在重启。" : "已应用。"
                : $"当前 DAT：{result.AppliedMagnitudeFilePath}";
            StatusText.Text = string.IsNullOrWhiteSpace(result.Message)
                ? restartRequested
                    ? "服务恢复后请重新采集验证。"
                    : "新 DAT 已应用。"
                : result.Message;
            if (!string.IsNullOrWhiteSpace(result.AppliedMagnitudeFilePath))
                MagnitudeFileText.Text = result.AppliedMagnitudeFilePath;
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
            StatusText.Text = "应用已取消。";
        }
        catch (Exception ex)
        {
            ShowError("应用新 DAT 失败", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private SpectrumCorrectionResult CalculateCorrection()
    {
        CommitStandardGridEdits();
        MagnitudeCalibrationFile currentFile = _currentFile
            ?? throw new InvalidOperationException("请先点击“采集实测”，获取本次服务结果和当前 DAT。");
        ServiceSpectrumMeasurement measurement = _measurement
            ?? throw new InvalidOperationException("请先点击“采集实测”。");

        if (CorrectionTabs.SelectedIndex == 0)
        {
            double target = ParsePositiveNumber(TargetBrightnessTextBox.Text, "目标亮度/光度值");
            double measured = _snapshot?.PhotometricValue ?? 0;
            return SpectrumMagnitudeCorrector.CorrectBrightness(currentFile, target, measured);
        }

        if (StandardRows.Count < 2)
            throw new InvalidOperationException("请导入、粘贴或编辑至少两个标准绝对光谱点。");

        SpectrumSeries standard = new(
            StandardRows.Select(row => row.Wavelength).ToArray(),
            StandardRows.Select(row => row.Value).ToArray());
        return SpectrumMagnitudeCorrector.CorrectFullSpectrum(currentFile, measurement, standard);
    }

    private void CommitStandardGridEdits()
    {
        StandardDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        StandardDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void PlotMeasurementOnly()
    {
        if (_measurement == null)
            return;

        SpectrumSeries measured = _measurement.ToAbsoluteSpectrum();
        PreviewPlot.Plot.Clear();
        AddLine(measured.Wavelengths, measured.Values, "服务实测", System.Drawing.Color.DodgerBlue);
        FinishPlot();
    }

    private void PlotPreview(SpectrumCorrectionResult result)
    {
        if (_currentFile == null || _measurement == null)
            return;

        double[] wavelengths = _currentFile.Wavelengths.ToArray();
        PreviewPlot.Plot.Clear();

        if (result.Mode == SpectrumCorrectionMode.BrightnessOnly)
        {
            SpectrumSeries measured = _measurement.ToAbsoluteSpectrum();
            double factor = result.UniformCorrectionFactor!.Value;
            double[] predicted = measured.Values.Select(value => value * factor).ToArray();
            AddLine(measured.Wavelengths, measured.Values, "服务实测", System.Drawing.Color.DodgerBlue);
            AddLine(measured.Wavelengths, predicted, "校正预测", System.Drawing.Color.OrangeRed);
        }
        else
        {
            double[] predicted = new double[result.MeasuredValues.Count];
            for (int index = 0; index < predicted.Length; index++)
                predicted[index] = result.MeasuredValues[index] * result.CorrectionFactors[index];

            AddLine(wavelengths, result.MeasuredValues, "服务实测", System.Drawing.Color.DodgerBlue);
            AddLine(wavelengths, result.StandardValues, "标准光谱", System.Drawing.Color.ForestGreen);
            AddLine(wavelengths, predicted, "校正预测", System.Drawing.Color.OrangeRed);
        }

        FinishPlot();
    }

    private void AddLine(IReadOnlyList<double> xs, IReadOnlyList<double> ys, string label, System.Drawing.Color color)
    {
        var scatter = PreviewPlot.Plot.Add.Scatter(xs.ToArray(), ys.ToArray());
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

    private static string BuildPreviewStatus(SpectrumCorrectionResult result)
    {
        if (result.Mode == SpectrumCorrectionMode.BrightnessOnly)
            return $"亮度校正比例：{result.UniformCorrectionFactor:G8}。点击“导出 DAT”后写入文件。";
        return $"完整光谱校正预览完成，共 {result.CorrectionFactors.Count} 点。{BuildFilledPointWarning(result)}";
    }

    private static string BuildFilledPointWarning(SpectrumCorrectionResult result) =>
        result.FilledFactorCount == 0
            ? string.Empty
            : $"注意：{result.FilledFactorCount} 个低/零实测点未参与除法，校正比例由相邻有效点插值或延伸。";

    private void CorrectionTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != CorrectionTabs)
            return;
        InvalidateCorrectionOutput();
        if (_measurement != null)
            PlotMeasurementOnly();
    }

    private void CorrectionInput_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateBrightnessRatio();
        InvalidateCorrectionOutput();
    }

    private void UpdateBrightnessRatio()
    {
        if (_snapshot != null
            && TryParseNumber(TargetBrightnessTextBox.Text, out double target)
            && target > 0
            && _snapshot.PhotometricValue > 0)
        {
            BrightnessRatioTextBox.Text = (target / _snapshot.PhotometricValue).ToString("G10", CultureInfo.CurrentCulture);
        }
        else
        {
            BrightnessRatioTextBox.Text = string.Empty;
        }
    }

    private void InvalidateCorrectionOutput()
    {
        _previewResult = null;
        _generatedFilePath = null;
        GeneratedFileText.Text = string.Empty;
        if (!_operationInProgress)
            ApplyButton.IsEnabled = false;
    }

    private void ClearCapturedMeasurement()
    {
        _snapshot = null;
        _measurement = null;
        _currentFile = null;
        _sourceMagnitudeSha256 = string.Empty;
        InvalidateCorrectionOutput();
        MeasuredRows.Clear();
        MeasuredBrightnessTextBox.Text = string.Empty;
        BrightnessRatioTextBox.Text = string.Empty;
        DeviceText.Text = "采集后显示";
        SerialNumberText.Text = "—";
        CalibrationGroupText.Text = "—";
        ResultText.Text = "—";
        MagnitudeFileText.Text = "采集后自动读取当前标定组";
        PreviewPlot.Plot.Clear();
        FinishPlot();
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _operationInProgress = busy;
        bool hasCapturedMeasurement = _snapshot != null && _measurement != null && _currentFile != null;
        CaptureButton.IsEnabled = !busy;
        PreviewButton.IsEnabled = !busy && hasCapturedMeasurement;
        GenerateButton.IsEnabled = !busy && hasCapturedMeasurement;
        ApplyButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(_generatedFilePath);
        if (status != null)
            StatusText.Text = status;
    }

    private void ShowError(string title, Exception exception)
    {
        string message = exception.GetBaseException().Message;
        StatusText.Text = $"{title}：{message}";
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string ComputeSha256(string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static double ParsePositiveNumber(string text, string fieldName)
    {
        if (!TryParseNumber(text, out double value) || !double.IsFinite(value) || value <= 0)
            throw new FormatException($"{fieldName}必须是有限正数。");
        return value;
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text?.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value)
        || double.TryParse(text?.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);

    private static string EmptyAsDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_operationInProgress)
        {
            e.Cancel = true;
            StatusText.Text = "当前操作完成前不能关闭窗口。";
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        base.OnClosed(e);
    }
}

public sealed class SpectrumTableRow : INotifyPropertyChanged
{
    private double _wavelength;
    private double _value;

    public SpectrumTableRow()
    {
    }

    public SpectrumTableRow(double wavelength, double value)
    {
        _wavelength = wavelength;
        _value = value;
    }

    public double Wavelength
    {
        get => _wavelength;
        set
        {
            if (_wavelength.Equals(value))
                return;
            _wavelength = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Wavelength)));
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            if (_value.Equals(value))
                return;
            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
