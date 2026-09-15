using ColorVision.Engine.Services.PhyCameras.Group;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera
{
    internal static class CalibrationGroupGainResolver
    {
        internal static void Synchronize(DisplayCameraConfig displayConfig, CalibrationParam? calibration, IEnumerable<GroupResource> groups)
        {
            ArgumentNullException.ThrowIfNull(displayConfig);
            ArgumentNullException.ThrowIfNull(groups);

            bool isControlled = TryResolve(calibration, groups, out float gain, out string groupName);
            displayConfig.IsGainControlledByCalibrationGroup = isControlled;
            displayConfig.GainSourceHint = isControlled ? CreateHint(groupName, gain) : string.Empty;
            if (isControlled) displayConfig.Gain = gain;
        }

        internal static bool TryResolve(DeviceCamera? device, string? calibrationTemplateName, out float gain, out string groupName)
        {
            gain = 0;
            groupName = string.Empty;
            if (device?.PhyCamera == null || string.IsNullOrWhiteSpace(calibrationTemplateName)) return false;

            CalibrationParam? calibration = device.PhyCamera.CalibrationParams
                .FirstOrDefault(item => string.Equals(item.Key, calibrationTemplateName, StringComparison.Ordinal))?.Value;
            return TryResolve(calibration, device.PhyCamera.VisualChildren.OfType<GroupResource>(), out gain, out groupName);
        }

        internal static bool TryResolve(CalibrationParam? calibration, IEnumerable<GroupResource> groups, out float gain, out string groupName)
        {
            gain = 0;
            groupName = string.Empty;
            if (calibration == null || calibration.Id == -1 || string.IsNullOrWhiteSpace(calibration.CalibrationMode)) return false;

            GroupResource? group = groups.FirstOrDefault(resource => string.Equals(resource.Name, calibration.CalibrationMode, StringComparison.Ordinal));
            if (group == null) return false;

            gain = group.Config.Gain;
            groupName = group.Name;
            return true;
        }

        internal static string CreateHint(string groupName, float gain) => $"增益由校正组“{groupName}”固定为 {gain:0.##}";
    }
}
