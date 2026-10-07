using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.UI;
using cvColorVision;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public partial class DeviceCamera
    {
        internal CameraBackendState CameraBackend { get; }
        private readonly object previewSync = new();
        private (ViewCamera View, MeasureResultImgModel? Model, LocalCameraPreview Preview, bool Force)? pendingPreview;
        private bool previewQueued;
        private long previewVersion;

        internal void PublishLocalPreview(LocalFlowFrame frame, MeasureResultImgModel? model, bool forceDisplay)
        {
            ViewCamera? view = ExistingView;
            if (IsDisposed || Application.Current == null || view == null) return;
            // Skip the full RAW/CIE snapshot for automatic captures while refresh is disabled.
            if (!forceDisplay && !ViewCameraConfig.Instance.AutoRefreshView) return;
            long version = Interlocked.Increment(ref previewVersion);
            try
            {
                LocalCameraPreview preview = LocalCameraPreview.Create(frame);
                lock (previewSync)
                {
                    if (IsDisposed || !ReferenceEquals(ExistingView, view) || version != Volatile.Read(ref previewVersion)) return;
                    pendingPreview = (view, model, preview, forceDisplay);
                    if (previewQueued) return;
                    previewQueued = true;
                }
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    (ViewCamera View, MeasureResultImgModel? Model, LocalCameraPreview Preview, bool Force)? pending;
                    lock (previewSync)
                    {
                        pending = pendingPreview;
                        pendingPreview = null;
                        previewQueued = false;
                    }
                    if (IsDisposed || pending == null || !ReferenceEquals(ExistingView, pending.Value.View)) return;
                    try { pending.Value.View.ShowLocalResult(pending.Value.Model, pending.Value.Preview, pending.Value.Force); }
                    catch (Exception ex) { MQTTServiceBase.log.Error("本地相机预览显示失败。", ex); }
                });
            }
            catch (Exception ex) { MQTTServiceBase.log.Error("本地相机预览转换失败；采集与持久化结果不受影响。", ex); }
        }

        private void DisplayConfig_BackendPreferenceChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsDisposed) return;
            if (e.PropertyName == nameof(DisplayCameraConfig.UseLocalCamera))
                CameraBackend.SetPreference(DisplayConfig.UseLocalCamera);
            else if (e.PropertyName != nameof(DisplayCameraConfig.UseHikMvs) && e.PropertyName != nameof(DisplayCameraConfig.HikBayerQuality) && e.PropertyName != nameof(DisplayCameraConfig.HikOutputBgr)) return;
            ConfigHandler.GetInstance().Save<DisplayConfigManager>();
        }

        [Browsable(false)]
        public bool RoutesLocally => CameraBackend.RoutesLocally;
        [Browsable(false)]
        public bool ServiceControlsEnabled => !RoutesLocally && !CameraBackend.VideoOwned;

        private void CameraBackend_Changed(object? sender, EventArgs e)
        {
            // Never synchronously enter the dispatcher from a native/session lock.
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (IsDisposed) return;
                DService.RefreshBackendStatus();
                OnPropertyChanged(nameof(RoutesLocally));
                OnPropertyChanged(nameof(ServiceControlsEnabled));
            });
        }

        internal void EnsureLocalCameraAvailable(string? cameraId = null)
        {
            CameraBackend.EnsureLocalAvailable();
            if (CameraBackend.LocalOwned) return;
            foreach (var other in ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>())
            {
                if (ReferenceEquals(other, this)) continue;
                bool samePhysical = (!string.IsNullOrEmpty(Config.CameraCode) && Config.CameraCode == other.Config.CameraCode)
                    || (!string.IsNullOrEmpty(cameraId ?? Config.CameraID) && string.Equals(cameraId ?? Config.CameraID, other.Config.CameraID, StringComparison.OrdinalIgnoreCase));
                if (samePhysical && (other.CameraBackend.LocalOwned || other.CameraBackend.VideoOwned || other.CameraBackend.ServiceMayOwnCamera))
                    throw new InvalidOperationException($"同一物理相机已由设备 {other.Code} 占用，请先关闭该设备。");
            }
        }

        internal void EnsureServiceCameraAvailable()
        {
            foreach (var other in ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>())
            {
                if (ReferenceEquals(other, this)) continue;
                bool samePhysical = (!string.IsNullOrEmpty(Config.CameraCode) && Config.CameraCode == other.Config.CameraCode)
                    || (!string.IsNullOrEmpty(Config.CameraID) && Config.CameraID == other.Config.CameraID);
                if (samePhysical && (other.CameraBackend.LocalOwned || other.CameraBackend.VideoOwned))
                    throw new InvalidOperationException($"同一物理相机已由设备 {other.Code} 的本地会话占用。");
            }
        }

        internal void EnsureLocalMeasurementConnected(bool autoConnect)
        {
            EnsureLocalCameraAvailable();
            try { LocalCameraSession.EnsureMeasurement(autoConnect); }
            catch (LocalCameraLicenseException ex) when (autoConnect)
            {
                Application.Current?.Dispatcher.BeginInvoke(() => MessageBox.Show(WindowHelpers.GetActiveWindow(), ex.Message, "ColorVision"));
                throw;
            }
        }

        internal void EnsureLocalCameraLicense() => LocalCameraLicense.EnsureAvailable(
            PhyCamera?.CameraLicenseModel?.ExpiryDate, ExperimentalFeaturesConfig.Instance.IsEnabled, DateTime.Now);

        internal CameraRunParam BuildLocalCameraParameters(double[]? exposure = null, CalibrationParam? calibration = null)
        {
            CameraRunParam parameters = new()
            {
                Gain = DisplayConfig.Gain,
                AvgCount = DisplayConfig.AvgCount,
                ExpTime = (float)(exposure?[0] ?? DisplayConfig.ExpTime),
                ExpTimeR = (float)(exposure?[0] ?? DisplayConfig.ExpTimeR),
                ExpTimeG = (float)(exposure is { Length: > 1 } ? exposure[1] : DisplayConfig.ExpTimeG),
                ExpTimeB = (float)(exposure is { Length: > 2 } ? exposure[2] : DisplayConfig.ExpTimeB)
            };
            if (CalibrationGroupGainResolver.TryResolve(calibration, PhyCamera?.VisualChildren.OfType<GroupResource>() ?? Enumerable.Empty<GroupResource>(), out float gain, out _))
                parameters.Gain = gain;
            return parameters;
        }
    }
}
