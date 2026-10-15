using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    internal readonly record struct CameraAcquisitionDefaults(
        double ExpTime,
        double ExpTimeR,
        double ExpTimeG,
        double ExpTimeB,
        bool IsExpThree,
        float Gain,
        int AvgCount,
        CVImageFlipMode FlipMode,
        string CalibrationTemplateName);

    internal static class CameraNodeCreationDefaults
    {
        private const string DefaultCameraCode = "DEV.Camera.Default";

        internal static void InitializeFromCurrentCamera(STNode node)
        {
            if (!SupportsDirectParameters(node) || node is not IFlowDeviceNode deviceNode)
                return;

            DeviceCamera[] cameras = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>().ToArray() ?? [];
            DeviceCamera? camera = cameras.FirstOrDefault(item => string.Equals(item.Code, deviceNode.DeviceCode, StringComparison.Ordinal));
            if (camera == null && (string.IsNullOrWhiteSpace(deviceNode.DeviceCode) || deviceNode.DeviceCode == DefaultCameraCode))
                camera = cameras.FirstOrDefault();
            if (camera == null)
                return;

            CameraAcquisitionDefaults defaults = Capture(camera.DisplayConfig, camera.Config.IsExpThree,
                camera.PhyCamera?.CalibrationParams, camera.PhyCamera?.VisualChildren.OfType<GroupResource>() ?? []);
            if (!TryApply(node, defaults))
                return;

            deviceNode.DeviceCode = camera.Code;
        }

        internal static CameraAcquisitionDefaults Capture(
            DisplayCameraConfig displayConfig,
            bool isExpThree,
            IReadOnlyList<TemplateModel<CalibrationParam>>? calibrationTemplates,
            IEnumerable<GroupResource> calibrationGroups)
        {
            int templateIndex = displayConfig.CalibrationTemplateIndex - 1; // Index zero is the Empty option.
            TemplateModel<CalibrationParam>? selected = calibrationTemplates != null && templateIndex >= 0 && templateIndex < calibrationTemplates.Count
                ? calibrationTemplates[templateIndex]
                : null;
            CalibrationParam? calibration = selected != null && selected.Value.Id >= 0 && !string.IsNullOrWhiteSpace(selected.Key)
                ? selected.Value
                : null;
            string calibrationName = calibration == null ? string.Empty : selected!.Key;
            float gain = displayConfig.Gain;
            if (CalibrationGroupGainResolver.TryResolve(calibration, calibrationGroups, out float groupGain, out _))
                gain = groupGain;

            return new CameraAcquisitionDefaults(displayConfig.ExpTime, displayConfig.ExpTimeR,
                displayConfig.ExpTimeG, displayConfig.ExpTimeB, isExpThree, gain,
                displayConfig.AvgCount, displayConfig.FlipMode, calibrationName);
        }

        internal static bool TryApply(STNode node, CameraAcquisitionDefaults defaults)
        {
            if (!float.IsFinite(defaults.Gain) || defaults.Gain < 0 || defaults.AvgCount < 1)
                return false;

            if (node is CVCameraNode cvNode)
            {
                double red = defaults.IsExpThree ? defaults.ExpTimeR : defaults.ExpTime;
                double green = defaults.IsExpThree ? defaults.ExpTimeG : defaults.ExpTime;
                double blue = defaults.IsExpThree ? defaults.ExpTimeB : defaults.ExpTime;
                if (!IsValidExposure(red) || !IsValidExposure(green) || !IsValidExposure(blue))
                    return false;

                cvNode.TempR = (float)red;
                cvNode.TempG = (float)green;
                cvNode.TempB = (float)blue;
                cvNode.Gain = defaults.Gain;
                cvNode.AvgCount = defaults.AvgCount;
                cvNode.FlipMode = defaults.FlipMode;
                cvNode.CalibTempName = defaults.CalibrationTemplateName;
                return true;
            }

            if (!IsValidExposure(defaults.ExpTime))
                return false;

            float exposure = (float)defaults.ExpTime;
            switch (node)
            {
                case LVCameraNode lvNode:
                    lvNode.ExpTime = exposure;
                    lvNode.Gain = defaults.Gain;
                    lvNode.AvgCount = defaults.AvgCount;
                    lvNode.FlipMode = defaults.FlipMode;
                    lvNode.CaliTempName = defaults.CalibrationTemplateName;
                    return true;
                case LocalCameraNode localNode:
                    localNode.ExpTime = exposure;
                    localNode.Gain = defaults.Gain;
                    localNode.AvgCount = defaults.AvgCount;
                    localNode.FlipMode = defaults.FlipMode;
                    localNode.CalibTempName = defaults.CalibrationTemplateName;
                    return true;
                case AOILocatePixelsCameraNode locateNode:
                    locateNode.ExpTime = exposure;
                    locateNode.Gain = defaults.Gain;
                    locateNode.AvgCount = defaults.AvgCount;
                    locateNode.FlipMode = defaults.FlipMode;
                    locateNode.CaliTempName = defaults.CalibrationTemplateName;
                    return true;
                case AOIRegisterPixelsCameraNode registerNode:
                    registerNode.ExpTime = exposure;
                    registerNode.Gain = defaults.Gain;
                    registerNode.AvgCount = defaults.AvgCount;
                    registerNode.FlipMode = defaults.FlipMode;
                    registerNode.CaliTempName = defaults.CalibrationTemplateName;
                    return true;
                case AOILocAndRegPixelsCameraNode locateAndRegisterNode:
                    locateAndRegisterNode.ExpTime = exposure;
                    locateAndRegisterNode.Gain = defaults.Gain;
                    locateAndRegisterNode.AvgCount = defaults.AvgCount;
                    locateAndRegisterNode.FlipMode = defaults.FlipMode;
                    locateAndRegisterNode.CaliTempName = defaults.CalibrationTemplateName;
                    return true;
                default:
                    return false;
            }
        }

        private static bool SupportsDirectParameters(STNode node) => node is LVCameraNode or CVCameraNode or LocalCameraNode
            or AOILocatePixelsCameraNode or AOIRegisterPixelsCameraNode or AOILocAndRegPixelsCameraNode;

        private static bool IsValidExposure(double exposure) => double.IsFinite(exposure) && exposure > 0 && exposure <= float.MaxValue;
    }
}
