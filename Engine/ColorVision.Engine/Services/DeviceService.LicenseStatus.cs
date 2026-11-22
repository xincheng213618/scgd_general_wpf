using ColorVision.Database;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Services;

public abstract partial class DeviceService
{
    private static readonly TimeSpan LicenseRefreshInterval = TimeSpan.FromSeconds(30);
    private sealed record LicenseBinding(MySqlConnectedEndpoint Endpoint, string SerialNumber);

    private LicenseBinding? licenseBinding;
    private long lastLicenseRefresh;
    private bool licenseRefreshInProgress;
    private bool licenseExpired;

    [Browsable(false)]
    public string LicenseFailureText => licenseExpired && licenseBinding == GetLicenseBinding()
        ? Resources.LicenseListTagExpired : Resources.DeviceStatusUnauthorized;

    internal void RefreshLicenseFailure(MQTTServiceBase service, DeviceStatusType reportedStatus)
    {
        if (reportedStatus != DeviceStatusType.Unauthorized && licenseBinding == null) return;
        if (Application.Current?.Dispatcher is not { HasShutdownStarted: false } dispatcher) return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => RefreshLicenseFailure(service, reportedStatus));
            return;
        }

        if (!ReferenceEquals(service, GetMQTTService())) return;
        LicenseBinding? binding = reportedStatus == DeviceStatusType.Unauthorized && service.DeviceStatus == DeviceStatusType.Unauthorized
            ? GetLicenseBinding() : null;
        if (licenseBinding != binding)
        {
            licenseBinding = binding;
            lastLicenseRefresh = 0;
            SetLicenseExpired(false);
        }
        if (licenseBinding == null || licenseRefreshInProgress ||
            (lastLicenseRefresh != 0 && Stopwatch.GetElapsedTime(lastLicenseRefresh) < LicenseRefreshInterval)) return;

        lastLicenseRefresh = Stopwatch.GetTimestamp();
        licenseRefreshInProgress = true;
        _ = RefreshLicenseFailureAsync(licenseBinding, service, dispatcher);
    }

    private LicenseBinding? GetLicenseBinding()
    {
        if (IsConfigurationRetired || ResultDatabaseEndpoint is not { IsCurrent: true } endpoint) return null;

        string? serialNumber = GetConfig() switch
        {
            ConfigCamera camera => camera.CameraCode,
            ConfigCalibration calibration => calibration.CameraCode,
            DeviceServiceConfig config => config.SN,
            _ => null
        };
        return string.IsNullOrWhiteSpace(serialNumber) ? null : new(endpoint, serialNumber);
    }

    private async Task RefreshLicenseFailureAsync(LicenseBinding binding, MQTTServiceBase service, Dispatcher dispatcher)
    {
        bool expired = false;
        try
        {
            expired = await Task.Run(() =>
            {
                if (!binding.Endpoint.IsCurrent) return false;
                using var db = binding.Endpoint.CreateDbClient();
                return PhyLicenseDao.AreBoundLicensesExpired(db, binding.SerialNumber, DateTimeOffset.UtcNow);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ConfigurationLog.Warn("Could not check the expiry of the device's bound license.", ex);
        }

        if (dispatcher.HasShutdownStarted) return;
        _ = dispatcher.BeginInvoke(() =>
        {
            licenseRefreshInProgress = false;
            if (!ReferenceEquals(licenseBinding, binding) || binding != GetLicenseBinding()
                || !ReferenceEquals(service, GetMQTTService()) || service.DeviceStatus != DeviceStatusType.Unauthorized) return;
            SetLicenseExpired(expired);
        });
    }

    private void SetLicenseExpired(bool expired)
    {
        if (licenseExpired == expired) return;
        licenseExpired = expired;
        OnPropertyChanged(nameof(LicenseFailureText));
    }
}
