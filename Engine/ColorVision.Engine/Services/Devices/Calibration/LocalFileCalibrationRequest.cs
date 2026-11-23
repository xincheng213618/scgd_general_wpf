using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Calibration;

internal sealed record LocalFileCalibrationRequest(string DeviceCode, string? OutputRoot, string SourcePath, string SerialNumber,
    float[] Exposure, int TemplateId, string TemplateName, IReadOnlyList<DeviceCameraCalibrationFile> CalibrationFiles,
    LocalCalibrationCacheManager Cache, LocalCalibrationRoi FallbackRoi, MySqlConnectedEndpoint? PersistenceEndpoint)
{
    internal static LocalFileCalibrationRequest Capture(DeviceCalibration device, PhyCamera? physicalCamera,
        CalibrationParam calibration, string filePath, string serialNumber, float[] exposure)
    {
        MySqlConnectedEndpoint? endpoint = CapturePersistenceEndpoint(device, MySqlControl.GetInstance().ConnectedEndpoint);
        DeviceCamera? camera = physicalCamera?.DeviceCamera;
        if (camera == null || camera.IsDisposed || !ReferenceEquals(camera.PhyCamera, physicalCamera))
            throw new InvalidOperationException("本地校正需要关联相机设备；请先配置相机，或在显示配置中切换到 MQTT。");

        if (!camera.TryGetCalibrationTemplateFiles(calibration, out var files, out var error, checkFiles: false))
            throw new InvalidOperationException(error ?? "校正模板无效。");

        string? outputRoot = device.Config.FileServerCfg.DataBasePath;
        return new(device.Code, string.IsNullOrWhiteSpace(outputRoot) ? null : Path.GetFullPath(outputRoot), Path.GetFullPath(filePath),
            serialNumber, (float[])exposure.Clone(), calibration.Id, calibration.Name ?? string.Empty,
            files.Select(file => file with { FullPath = Path.GetFullPath(file.FullPath) }).ToArray(),
            camera.LocalCalibrationCacheManager, LocalCalibrationRoi.FromCameraConfig(physicalCamera!.Config.CameraCfg), endpoint);
    }

    internal static MySqlConnectedEndpoint? CapturePersistenceEndpoint(DeviceCalibration device, MySqlConnectedEndpoint? verifiedEndpoint)
        => verifiedEndpoint?.IsCurrent == true && (ReferenceEquals(device.ResultDatabaseEndpoint, verifiedEndpoint)
            || SysResourceDao.IsLocalId(device.SysResourceModel.Id)) ? verifiedEndpoint : null;
}
