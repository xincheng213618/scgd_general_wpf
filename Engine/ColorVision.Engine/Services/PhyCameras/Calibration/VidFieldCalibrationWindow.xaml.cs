using ColorVision.Common.ThirdPartyApps;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.PhyCameras.Calibration.Editing;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhyCameras.Calibration;

public partial class VidFieldCalibrationWindow : Window
{
    private const string FileFilter = "VID 映射文件 (*.dat;*.json)|*.dat;*.json|所有文件 (*.*)|*.*";
    private static readonly int[] RepeatCounts = [1, 2, 3, 5];
    private CalibrationJsonFileSnapshot? source;
    private readonly List<MeasuredPoint> points = [];
    private VidFieldCalibration? preview;
    private CancellationTokenSource? focusStop;
    private bool busy;

    // Runs holds each repeated autofocus position; Position is their median.
    private sealed record MeasuredPoint(int Position, double Reference, int[] Runs, int Minimum, int Maximum);

    public sealed record PointRow(int Index, string Reference, int Position, string Spread, string Measured, string Corrected, string Residual);

    public VidFieldCalibrationWindow()
    {
        InitializeComponent();
        this.ApplyCaption();
        TemplatePicker.ItemsSource = TemplateAutoFocus.Params;
        DeviceCamera[] cameras = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
            .Where(camera => (camera.DisplayConfig.UseLocalCamera || camera.CameraBackend.LocalOwned)
                && (camera.PhyCamera?.Config.MotorConfig ?? camera.Config.MotorConfig).IsUseMotor).ToArray();
        CameraPicker.ItemsSource = cameras;
        if (cameras.Length == 1) CameraPicker.SelectedIndex = 0;
        else if (cameras.Length == 0) StatusText.Text = "当前没有启用电机的本地相机。请在设备设置中启用本地取图后重新打开工具。";
        UpdatePreview();
    }

    private void CameraChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        // Motor positions belong to one camera's motor; switching cameras starts a new point set.
        points.Clear();
        if (CameraPicker.SelectedItem is DeviceCamera camera)
        {
            int index = camera.DisplayConfig.AutoFocusTemplateIndex;
            TemplatePicker.SelectedIndex = index >= 0 && index < TemplatePicker.Items.Count ? index : -1;
            LoadSource((camera.PhyCamera?.Config.MotorConfig ?? camera.Config.MotorConfig).VID?.MappingFileName);
        }
        else LoadSource(null);
    }

    private void TemplateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized) UpdatePreview();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = FileFilter, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) LoadSource(dialog.FileName);
    }

    // Measured motor positions do not depend on the mapping, so existing points are re-evaluated with the new file.
    private void LoadSource(string? path)
    {
        source = null;
        SourcePath.Text = path ?? string.Empty;
        StatusText.Text = string.Empty;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                var loaded = CalibrationJsonFileStore.Load(path);
                VidFieldCalibration.ValidateSource(loaded.Json);
                source = loaded;
            }
            catch (Exception error) { StatusText.Text = $"无法读取原映射：{error.Message}"; }
        }
        UpdatePreview();
    }

    private bool TryReference(out double reference) =>
        double.TryParse(ReferenceBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out reference)
        && double.IsFinite(reference) && reference != 0;

    private async void Measure_Click(object sender, RoutedEventArgs e)
    {
        if (busy || source == null || CameraPicker.SelectedItem is not DeviceCamera camera
            || TemplatePicker.SelectedItem is not TemplateModel<AutoFocusParam> template) return;
        if (!TryReference(out double reference))
        {
            StatusText.Text = "请输入非零的有限标准 VID，并确认与原映射使用同一单位。";
            ReferenceBox.Focus();
            return;
        }

        int count = RepeatCounts[Math.Clamp(RepeatPicker.SelectedIndex, 0, RepeatCounts.Length - 1)];
        using var stop = new CancellationTokenSource();
        focusStop = stop;
        SetBusy(true);
        try
        {
            var settings = LocalAutoFocusSettings.FromTemplate(template.Value, camera.PhyCamera?.Config.MotorConfig ?? camera.Config.MotorConfig);
            var runs = new int[count];
            for (int i = 0; i < count; i++)
            {
                StatusText.Text = $"正在第 {i + 1}/{count} 次自动对焦并读取电机位置…";
                runs[i] = await camera.MeasureVidCalibrationPositionAsync(settings, stop.Token);
            }
            stop.Token.ThrowIfCancellationRequested();
            Array.Sort(runs);
            int median = count % 2 == 1 ? runs[count / 2] : (int)Math.Round(((long)runs[count / 2 - 1] + runs[count / 2]) / 2.0, MidpointRounding.AwayFromZero);
            points.Add(new(median, reference, runs, settings.Minimum, settings.Maximum));
            StatusText.Text = count == 1 ? $"已添加测点，电机位置 {median}。" : $"已添加测点，{count} 次对焦的电机位置中位数为 {median}。";
        }
        catch (OperationCanceledException) { StatusText.Text = "已停止对焦，本测点未添加。"; }
        catch (Exception error) { StatusText.Text = $"自动对焦或读位失败，本测点未添加：{error.Message}"; }
        finally
        {
            focusStop = null;
            SetBusy(false);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        int index = PointList.SelectedIndex;
        if (busy || index < 0 || index >= points.Count) return;
        points.RemoveAt(index);
        UpdatePreview();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        points.Clear();
        UpdatePreview();
    }

    private void PointSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RemoveButton.IsEnabled = !busy && PointList.SelectedIndex >= 0;

    private void InputChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized) UpdatePreview();
    }

    private static string ModelName(VidCorrectionModel model) => model switch
    {
        VidCorrectionModel.Diopter => "屈光度偏移",
        VidCorrectionModel.Distance => "距离偏移",
        VidCorrectionModel.Position => "电机位置偏移",
        _ => "自动",
    };

    private static string Parameter(VidModelFit fit, string unit) => fit.Model switch
    {
        VidCorrectionModel.Diopter => $"{fit.Parameter:+0.######;-0.######;0} D",
        VidCorrectionModel.Distance => $"{fit.Parameter:+0.######;-0.######;0} {unit}",
        _ => $"{fit.Parameter:+0.##;-0.##;0} 位置单位",
    };

    private void UpdatePreview()
    {
        preview = null;
        MeasureButton.IsEnabled = !busy && source != null && CameraPicker.SelectedItem != null && TemplatePicker.SelectedItem != null;
        UnitPicker.IsEnabled = points.Count == 0;
        ClearButton.IsEnabled = !busy && points.Count > 0;
        RemoveButton.IsEnabled = !busy && PointList.SelectedIndex >= 0;
        SaveButton.IsEnabled = false;
        string unit = UnitPicker.SelectedIndex == 1 ? "m" : "mm";
        double scale = unit == "mm" ? 1000 : 1;

        string Spread(MeasuredPoint point)
        {
            if (point.Runs.Length < 2) return "单次";
            int range = point.Runs[^1] - point.Runs[0];
            try
            {
                var json = source?.Json;
                if (json == null) return $"{range} 位置";
                double diopters = Math.Abs(1 / VidFieldCalibration.ReadCurrent(json, point.Runs[^1]) - 1 / VidFieldCalibration.ReadCurrent(json, point.Runs[0])) * scale;
                return $"{range} 位置 / {diopters:0.####} D";
            }
            catch (FormatException) { return $"{range} 位置"; }
        }

        var model = (VidCorrectionModel)Math.Clamp(ModelPicker.SelectedIndex, 0, 3);
        int selected = PointList.SelectedIndex;
        string previewError = string.Empty;
        try
        {
            if (points.Count > 0 && source != null)
            {
                preview = VidFieldCalibration.Preview(source.Json, points.Select(point => new VidCalibrationPoint(point.Position, point.Reference)).ToArray(),
                    points.Min(point => point.Minimum), points.Max(point => point.Maximum), unit == "mm", model);
            }
        }
        catch (Exception error) when (error is FormatException or ArgumentException) { previewError = $"无法预览校正：{error.Message}"; }

        PointList.ItemsSource = points.Select((point, i) =>
        {
            var result = preview?.Points[i];
            return new PointRow(i + 1, $"{point.Reference:G9}", point.Position, Spread(point),
                result == null ? "—" : $"{result.Measured:G9}", result == null ? "—" : $"{result.Corrected:G9}",
                result == null ? "—" : $"{result.ResidualDiopters:+0.####;-0.####;0} D");
        }).ToArray();
        if (selected >= 0 && selected < points.Count) PointList.SelectedIndex = selected;

        if (source == null || points.Count == 0)
        {
            PreviewText.Text = source == null ? "请先选择有效的原映射文件。" : "输入标准 VID 后点击“自动对焦并添加测点”。添加测点后显示原 VID、修正量和校正值。";
            return;
        }
        if (preview == null)
        {
            PreviewText.Text = previewError;
            return;
        }

        var chosen = preview.Fits.First(fit => fit.Model == preview.Model);
        string text = $"模型 {ModelName(preview.Model)}{(model == VidCorrectionModel.Auto ? "（自动选择）" : string.Empty)}    修正量 {Parameter(chosen, unit)}";
        if (points.Count == 1)
            text += "\n单点只能确定修正量，三种模型在该点结果相同。请在其他标准距离再添加测点，比较模型并检查校正效果。";
        else
        {
            text += $"    残差 RMS {chosen.RmsDiopters:0.####} D";
            text += "\n各模型残差 RMS：" + string.Join("    ", preview.Fits.Select(fit =>
                $"{ModelName(fit.Model)} {(fit.Error == null ? $"{fit.RmsDiopters:0.####} D" : "无法拟合")}"));
        }
        if (preview.HasInfinityInRange) text += "\n校正映射在所选模板范围内经过无穷远，请在附近位置复测。";
        PreviewText.Text = text;
        SaveButton.IsEnabled = !busy;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (busy || source is not { } original || preview is not { } result) return;
        var dialog = new SaveFileDialog
        {
            Filter = "VID 映射文件 (*.dat)|*.dat|JSON 文件 (*.json)|*.json",
            FileName = Path.GetFileNameWithoutExtension(original.Path) + "_field_calibrated.dat",
            AddExtension = true,
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != true) return;
        SetBusy(true);
        try
        {
            await Task.Run(() => result.SaveAs(dialog.FileName, original));
            StatusText.Text = $"映射已另存至 {dialog.FileName}";
        }
        catch (Exception error) { StatusText.Text = $"另存失败：{error.Message}"; }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        InputPanel.IsEnabled = !busy;
        CloseButton.IsEnabled = !busy;
        StopButton.Visibility = focusStop == null ? Visibility.Collapsed : Visibility.Visible;
        StopButton.IsEnabled = focusStop != null;
        UpdatePreview();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        focusStop?.Cancel();
        StopButton.IsEnabled = false;
        StatusText.Text = "正在停止对焦，等待当前设备操作结束…";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (busy) e.Cancel = true;
    }
}

public sealed class VidFieldCalibrationAppProvider : IThirdPartyAppProvider
{
    public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
    {
        yield return new ThirdPartyAppInfo
        {
            Name = "VID 现场校正",
            Group = "ColorVision",
            Category = ThirdPartyAppCategory.Internal,
            RequiredPermission = PermissionMode.Administrator,
            Order = 13,
            IconGlyph = ThirdPartyAppIconGlyphs.DataFile,
            LaunchAction = () =>
            {
                var existing = Application.Current.Windows.OfType<VidFieldCalibrationWindow>().FirstOrDefault();
                if (existing != null) { existing.Activate(); return; }
                new VidFieldCalibrationWindow { Owner = Application.Current.GetActiveWindow() }.Show();
            },
        };
    }
}
