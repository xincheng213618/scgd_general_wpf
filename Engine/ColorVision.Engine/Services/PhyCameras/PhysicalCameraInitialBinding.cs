using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.Types;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.PhyCameras;

internal static class PhysicalCameraInitialBinding
{
    internal static void BindDevices(bool requiresCreation, int physicalCameraCount, SysResourceModel physicalCamera,
        ConfigPhyCamera physicalConfig, IEnumerable<DeviceService> devices)
    {
        if (!requiresCreation || physicalCameraCount != 1 || string.IsNullOrWhiteSpace(physicalCamera.Code)) return;

        bool local = SysResourceDao.IsLocalId(physicalCamera.Id);
        foreach (DeviceService device in devices)
        {
            if (SysResourceDao.IsLocalId(device.SysResourceModel.Id) != local
                || !device.SysResourceModel.IsEnable || device.SysResourceModel.IsDelete
                || device.ServiceTypes == ServiceTypes.Spectrum
                || device.GetConfig() is not DeviceServiceConfig config) continue;

            if (Apply(config, physicalCamera.Code, physicalConfig)) device.Save();
        }
    }

    // First-camera setup binds identity, even when its license has not been imported yet.
    internal static bool Apply(DeviceServiceConfig config, string cameraCode, ConfigPhyCamera physicalConfig)
    {
        if (config is ConfigSpectrum || string.IsNullOrWhiteSpace(cameraCode)) return false;
        if (!string.IsNullOrWhiteSpace(config.SN) && !string.Equals(config.SN, cameraCode, StringComparison.OrdinalIgnoreCase)) return false;

        string? binding = config switch
        {
            ConfigCamera camera => camera.CameraCode,
            ConfigCalibration calibration => calibration.CameraCode,
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(binding) && !string.Equals(binding, cameraCode, StringComparison.OrdinalIgnoreCase)) return false;

        bool changed = false;
        if (string.IsNullOrWhiteSpace(config.SN))
        {
            config.SN = cameraCode;
            changed = true;
        }
        if (config is ConfigCamera cameraConfig)
        {
            if (string.IsNullOrWhiteSpace(cameraConfig.CameraCode))
            {
                cameraConfig.CameraCode = cameraCode;
                physicalConfig.ApplyTo(cameraConfig, includeCameraId: false);
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(cameraConfig.CameraID) && !string.IsNullOrWhiteSpace(physicalConfig.CameraID))
            {
                cameraConfig.CameraID = physicalConfig.CameraID;
                changed = true;
            }
        }
        else if (config is ConfigCalibration calibrationConfig)
        {
            if (string.IsNullOrWhiteSpace(calibrationConfig.CameraCode))
            {
                calibrationConfig.CameraCode = cameraCode;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(calibrationConfig.CameraID) && !string.IsNullOrWhiteSpace(physicalConfig.CameraID))
            {
                calibrationConfig.CameraID = physicalConfig.CameraID;
                changed = true;
            }
        }
        return changed;
    }
}
