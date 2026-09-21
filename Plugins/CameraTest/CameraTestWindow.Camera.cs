using cvColorVision;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace CameraTest;

public partial class CameraTestWindow
{
    private bool _syncingCameraControls;
    private readonly DispatcherTimer _acquisitionTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private float? _pendingExposure, _pendingGain;
    private bool _exposureInputDirty, _gainInputDirty;

    private void InitializeCameraControls()
    {
        _syncingCameraControls = true;
        CameraModels.ItemsSource = Enum.GetValues<CameraModel>();
        CameraModes.ItemsSource = new[] { CameraMode.BV_MODE, CameraMode.LV_MODE };
        CameraDepths.ItemsSource = new[] { 8, 16 };
        _syncingCameraControls = false;
        _acquisitionTimer.Tick += ApplyAcquisition_Tick;
        RefreshCameraControls();
    }

    private void RefreshCameraControls()
    {
        _syncingCameraControls = true;
        try
        {
            CameraModels.SelectedItem = _profile.Camera.Model;
            CameraModes.SelectedItem = _profile.Camera.Mode;
            CameraDepths.SelectedItem = _profile.Camera.BitDepth;
            CameraModels.IsEnabled = CameraModes.IsEnabled = CameraDepths.IsEnabled = !_busy && !_closing && !_stopping && !_camera.IsConnected;
            ExposureInput.IsEnabled = GainInput.IsEnabled = ExposureSlider.IsEnabled = GainSlider.IsEnabled = !_closing && !_stopping;
            if (_pendingExposure == null && !ExposureInput.IsKeyboardFocused)
            {
                ExposureInput.Text = _profile.Camera.ExposureMilliseconds.ToString("G7", CultureInfo.CurrentCulture);
                ExposureSlider.Value = Math.Log(_profile.Camera.ExposureMilliseconds);
            }
            if (_pendingGain == null && !GainInput.IsKeyboardFocused)
            {
                GainInput.Text = _profile.Camera.Gain.ToString("G7", CultureInfo.CurrentCulture);
                GainSlider.Value = _profile.Camera.Gain;
            }
        }
        finally { _syncingCameraControls = false; }
    }

    private void ConnectionSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncingCameraControls || _camera.IsConnected) return;
        if (CameraModels.SelectedItem is CameraModel model && model != _profile.Camera.Model)
        {
            _profile.Camera.Model = model;
            CameraIds.ItemsSource = null;
            _profile.Camera.CameraId = string.Empty;
        }
        if (CameraModes.SelectedItem is CameraMode mode) _profile.Camera.Mode = mode;
        if (CameraDepths.SelectedItem is int depth) _profile.Camera.BitDepth = depth;
        ScheduleSettingsSave();
        Refresh();
    }

    private void AcquisitionInput_Commit(object sender, KeyboardFocusChangedEventArgs e) => QueueAcquisitionInput(sender);
    private void AcquisitionInput_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_ready || _syncingCameraControls) return;
        if (ReferenceEquals(sender, ExposureInput)) _exposureInputDirty = true;
        else _gainInputDirty = true;
    }
    private void AcquisitionInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        QueueAcquisitionInput(sender);
        e.Handled = true;
    }
    private void QueueAcquisitionInput(object sender)
    {
        if (!_ready || _syncingCameraControls || _closing || sender is not TextBox input) return;
        bool exposure = ReferenceEquals(input, ExposureInput);
        if (!(exposure ? _exposureInputDirty : _gainInputDirty)) return;
        if (!float.TryParse(input.Text, out float value) || !float.IsFinite(value) || (exposure ? value <= 0 : value < 0))
        {
            StatusText.Text = exposure ? "曝光时间必须大于 0。" : "增益必须大于或等于 0。";
            return;
        }
        if (exposure) _exposureInputDirty = false;
        else _gainInputDirty = false;
        QueueAcquisition(exposure, value);
    }
    private void AcquisitionSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _syncingCameraControls || _closing) return;
        bool exposure = ReferenceEquals(sender, ExposureSlider);
        float value = (float)(exposure ? Math.Exp(e.NewValue) : e.NewValue);
        (exposure ? ExposureInput : GainInput).Text = value.ToString("G5", CultureInfo.CurrentCulture);
        if (exposure) _exposureInputDirty = false;
        else _gainInputDirty = false;
        QueueAcquisition(exposure, value);
    }
    private void QueueAcquisition(bool exposure, float value)
    {
        float current = exposure ? _pendingExposure ?? _profile.Camera.ExposureMilliseconds : _pendingGain ?? _profile.Camera.Gain;
        if (current == value) return;
        if (!_camera.IsConnected)
        {
            if (exposure) _profile.Camera.ExposureMilliseconds = value;
            else _profile.Camera.Gain = value;
            ScheduleSettingsSave();
            Refresh();
            return;
        }
        if (exposure) _pendingExposure = value;
        else _pendingGain = value;
        ExposureInput.ToolTip = GainInput.ToolTip = "正在应用参数…";
        _acquisitionTimer.Stop();
        _acquisitionTimer.Start();
    }
    private async void ApplyAcquisition_Tick(object? sender, EventArgs e)
    {
        if (_busy) return;
        _acquisitionTimer.Stop();
        if (_closing) return;
        await PerformAsync(ApplyPendingAcquisitionAsync);
    }

    private async Task ApplyPendingAcquisitionAsync()
    {
        _acquisitionTimer.Stop();
        var exposure = _pendingExposure;
        var gain = _pendingGain;
        if (!exposure.HasValue && !gain.HasValue) return;
        _pendingExposure = _pendingGain = null;
        try
        {
            if (_camera.IsConnected)
            {
                if (exposure.HasValue) await _camera.SetAcquisitionParameterAsync(true, exposure.Value);
                if (gain.HasValue) await _camera.SetAcquisitionParameterAsync(false, gain.Value);
            }
            else
            {
                if (exposure.HasValue) _profile.Camera.ExposureMilliseconds = exposure.Value;
                if (gain.HasValue) _profile.Camera.Gain = gain.Value;
            }
            ResetFocus();
            StatusText.Text = "曝光 / 增益已应用。";
        }
        finally
        {
            if (_camera.RequestedSettings is { } applied)
            {
                _profile.Camera.ExposureMilliseconds = applied.ExposureMilliseconds;
                _profile.Camera.Gain = applied.Gain;
            }
            ScheduleSettingsSave();
        }
    }
}
