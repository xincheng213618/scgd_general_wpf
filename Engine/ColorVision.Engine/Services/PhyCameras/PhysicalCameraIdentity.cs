using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Types;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras;

internal static class PhysicalCameraIdentity
{
    private static readonly ILog log = LogManager.GetLogger(typeof(PhysicalCameraIdentity));

    internal static void RecoverFromLogical(PhyCamera physical, string? cameraCode, string? cameraId)
    {
        string legacyId = cameraId?.Trim() ?? string.Empty;
        if (legacyId.Length == 0 || !string.Equals(cameraCode, physical.Code, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(ColorVision.Common.Utilities.Tool.GetMD5(legacyId), physical.Code, StringComparison.OrdinalIgnoreCase)) return;
        string physicalId = physical.Config.CameraID?.Trim() ?? string.Empty;
        if (physicalId.Length == 0 || !string.Equals(ColorVision.Common.Utilities.Tool.GetMD5(physicalId), physical.Code, StringComparison.OrdinalIgnoreCase))
            physical.Config.CameraID = legacyId;
    }

    internal static string ReadCameraId(SysResourceModel resource, string? currentId = null)
    {
        string? savedId = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(resource.Value) && JObject.Parse(resource.Value)["CameraID"] is JValue value)
                savedId = value.Value<string>();
        }
        catch (JsonException ex) { log.Warn($"无法读取物理相机 {resource.Id} 的 CameraID，保留可用的旧身份。", ex); }
        // JSON is authoritative. Name is only a compatibility fallback for older records.
        if (!string.IsNullOrWhiteSpace(resource.Code))
        {
            string? matchingId = new[] { savedId, currentId, resource.Name }.Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!.Trim()).FirstOrDefault(id => string.Equals(ColorVision.Common.Utilities.Tool.GetMD5(id), resource.Code, StringComparison.OrdinalIgnoreCase));
            if (matchingId != null) return matchingId;
        }
        if (!string.IsNullOrWhiteSpace(savedId)) return savedId.Trim();
        if (!string.IsNullOrWhiteSpace(currentId)) return currentId.Trim();
        string legacyId = resource.Name?.Trim() ?? string.Empty;
        // Older saves sometimes put the display Code in Name. Never open that as a camera ID.
        return !string.IsNullOrWhiteSpace(resource.Code)
            && !string.Equals(ColorVision.Common.Utilities.Tool.GetMD5(legacyId), resource.Code, StringComparison.OrdinalIgnoreCase) ? string.Empty : legacyId;
    }

    internal static void SynchronizeDiscovered(cvCameraCSLib.CameraDiscoverySummary summary, IEnumerable<PhyCamera> physicalCameras,
        IReadOnlyList<SysResourceModel> resources, IEnumerable<DeviceService> devices, Func<SysResourceModel, int> save)
    {
        DeviceService[] loadedDevices = devices.ToArray();
        foreach (PhyCamera physical in physicalCameras)
        {
            string[] ids = summary.Cameras.Where(camera => string.Equals(camera.MD5Id, physical.Code, StringComparison.OrdinalIgnoreCase)
                    && summary.Models.Any(model => model.Success && model.CameraModel == camera.CameraModel))
                .Select(camera => camera.CameraId.Trim()).Where(id => id.Length > 0)
                .Where(id => string.Equals(ColorVision.Common.Utilities.Tool.GetMD5(id), physical.Code, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (ids.Length == 0) continue;
            if (ids.Length != 1) throw new InvalidOperationException($"物理相机 {physical.Code} 的搜索结果不唯一，未更新 CameraID。");

            if (SynchronizeResolvedIdentity(physical, ids[0], resources, loadedDevices, save))
                physical.SysResourceModel.Remark = "Online";
        }
    }

    internal static bool SynchronizeResolvedIdentity(PhyCamera physical, string cameraId, IReadOnlyList<SysResourceModel> resources,
        IEnumerable<DeviceService> devices, Func<SysResourceModel, int> save)
    {
        cameraId = cameraId.Trim();
        if (cameraId.Length == 0 || !string.Equals(ColorVision.Common.Utilities.Tool.GetMD5(cameraId), physical.Code, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"CameraID 与物理相机 {physical.Code} 不匹配，未更新配置。");
        SysResourceModel? physicalResource = resources.FirstOrDefault(row => row.Id == physical.Id && !row.IsDelete
            && row.Type == (int)ServiceTypes.PhyCamera && string.Equals(row.Code, physical.Code, StringComparison.OrdinalIgnoreCase));
        if (physicalResource == null || string.IsNullOrWhiteSpace(physicalResource.Value)) return false;
        SaveIdentity(physicalResource, cameraId, physical: true, save);
        physical.SysResourceModel = physicalResource;
        physical.Config.CameraID = cameraId;
        SynchronizeLogicalCameras(physical, resources, devices, save);
        return true;
    }

    private static void SynchronizeLogicalCameras(PhyCamera physical, IReadOnlyList<SysResourceModel> resources,
        IEnumerable<DeviceService> devices, Func<SysResourceModel, int> save)
    {
        string cameraId = physical.Config.CameraID?.Trim() ?? string.Empty;
        foreach (SysResourceModel resource in resources.Where(resource => !resource.IsDelete
            && (resource.Type == (int)ServiceTypes.Camera || resource.Type == (int)ServiceTypes.Calibration)
            && SysResourceDao.IsLocalId(resource.Id) == SysResourceDao.IsLocalId(physical.Id)))
        {
            if (string.IsNullOrWhiteSpace(resource.Value)) continue;
            JObject json;
            try { json = JObject.Parse(resource.Value); }
            catch (JsonException ex)
            {
                log.Warn($"无法读取配置资源 {resource.Id} 的相机绑定，跳过身份同步。", ex);
                continue;
            }
            if (string.Equals(json.Value<string>("CameraCode"), physical.Code, StringComparison.OrdinalIgnoreCase))
                SaveIdentity(resource, cameraId, physical: false, save);
        }
        foreach (DeviceService device in devices.Where(device => SysResourceDao.IsLocalId(device.SysResourceModel.Id) == SysResourceDao.IsLocalId(physical.Id)))
        {
            switch (device.GetConfig())
            {
                case ConfigCamera config when string.Equals(config.CameraCode, physical.Code, StringComparison.OrdinalIgnoreCase):
                    config.CameraID = cameraId;
                    break;
                case ConfigCalibration config when string.Equals(config.CameraCode, physical.Code, StringComparison.OrdinalIgnoreCase):
                    config.CameraID = cameraId;
                    break;
            }
        }
    }

    private static void SaveIdentity(SysResourceModel resource, string cameraId, bool physical, Func<SysResourceModel, int> save)
    {
        var json = string.IsNullOrWhiteSpace(resource.Value) ? new JObject() : JObject.Parse(resource.Value);
        if (string.Equals(json.Value<string>("CameraID"), cameraId, StringComparison.Ordinal)
            && (!physical || string.Equals(resource.Name, cameraId, StringComparison.Ordinal))) return;

        string? oldName = resource.Name;
        string? oldValue = resource.Value;
        json["CameraID"] = cameraId;
        if (physical) resource.Name = cameraId;
        resource.Value = json.ToString(Newtonsoft.Json.Formatting.None);
        try
        {
            if (save(resource) <= 0) throw new InvalidOperationException($"保存相机 {resource.Code} 的 CameraID 失败。");
        }
        catch
        {
            resource.Name = oldName;
            resource.Value = oldValue;
            throw;
        }
    }
}
