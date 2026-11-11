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
    private CalibrationJsonFileSnapshot? source;
    private (int Position, int Minimum, int Maximum, double Vid)? measurement;
    private VidFieldCalibration? preview;
    private CancellationTokenSource? focusStop;
    private bool busy;

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
        if (!IsInitialized) return;
        measurement = null;
        UpdatePreview();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = FileFilter, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) LoadSource(dialog.FileName);
    }

    private void LoadSource(string? path)
    {
        measurement = null;
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
        if (busy || source is not { } original || CameraPicker.SelectedItem is not DeviceCamera camera
            || TemplatePicker.SelectedItem is not TemplateModel<AutoFocusParam> template) return;
        if (!TryReference(out _))
        {
            StatusText.Text = "请输入非零的有限标准 VID，并确认与原映射使用同一单位。";
            ReferenceBox.Focus();
            return;
        }

        measurement = null;
        using var stop = new CancellationTokenSource();
        focusStop = stop;
        SetBusy(true);
        StatusText.Text = "正在自动对焦并读取电机位置…";
        try
        {
            var settings = LocalAutoFocusSettings.FromTemplate(template.Value, camera.PhyCamera?.Config.MotorConfig ?? camera.Config.MotorConfig);
            int position = await camera.MeasureVidCalibrationPositionAsync(settings, stop.Token);
            measurement = (position, settings.Minimum, settings.Maximum, VidFieldCalibration.ReadCurrent(original.Json, position));
            StatusText.Text = "已读取电机位置。";
        }
        catch (OperationCanceledException) { StatusText.Text = "已停止对焦。"; }
        catch (Exception error) { StatusText.Text = $"自动对焦或读位失败：{error.Message}"; }
        finally
        {
            focusStop = null;
            SetBusy(false);
        }
    }

    private void InputChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized) UpdatePreview();
    }

    private void UpdatePreview()
    {
        preview = null;
        MeasureButton.IsEnabled = !busy && source != null && CameraPicker.SelectedItem != null && TemplatePicker.SelectedItem != null;
        SaveButton.IsEnabled = false;
        if (measurement is not { } sample || source == null)
        {
            PreviewText.Text = "自动对焦完成后显示原 VID、修正量和校正值。";
            return;
        }

        string unit = UnitPicker.SelectedIndex == 1 ? "m" : "mm";
        string readout = $"电机位置 {sample.Position}    原 VID {sample.Vid:G9} {unit}";
        try
        {
            if (!TryReference(out double reference))
            {
                PreviewText.Text = readout + "\n请输入有效的标准 VID。";
                return;
            }
            preview = VidFieldCalibration.Preview(source.Json, sample.Position, sample.Minimum, sample.Maximum, reference, unit == "mm");
            double deviation = preview.Measured - preview.Reference;
            PreviewText.Text = $"{readout}\n标准 VID {preview.Reference:G9} {unit}    读数偏差 {deviation:G9} {unit}\n屈光度修正 {preview.OffsetDiopters:+0.######;-0.######;0} D    校正 VID {preview.Corrected:G9} {unit}";
            if (preview.HasInfinityInRange) PreviewText.Text += "\n校正映射在所选模板范围内经过无穷远，请在附近位置复测。";
            SaveButton.IsEnabled = !busy;
        }
        catch (Exception error) { PreviewText.Text = $"{readout}\n无法预览校正：{error.Message}"; }
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
