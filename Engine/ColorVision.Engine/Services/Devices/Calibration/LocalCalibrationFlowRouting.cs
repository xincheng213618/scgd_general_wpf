using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Node.POI;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Calibration;

internal static class LocalCalibrationFlowRouting
{
    internal static DeviceCalibration? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DeviceCalibration>()
        .FirstOrDefault(device => string.Equals(device.Code, code, StringComparison.Ordinal));

    internal static bool UsesLocalCalibration(string code) => Find(code)?.DisplayConfig.UseLocalCalibration == true;

    // Resolve from the image branch, so two calibration services can keep independent preferences.
    internal static DeviceCalibration? FindForPoi(RealPOINode node)
    {
        HashSet<STNode> visited = new();
        STNode[] current = node.GetAllInputOptions().Where(option => option.Text == "IN_CIE")
            .SelectMany(option => option.GetConnectedOption()).Select(option => option.Owner).ToArray();
        while (current.Length > 0)
        {
            List<STNode> next = new();
            HashSet<DeviceCalibration> candidates = new();
            foreach (STNode upstream in current)
            {
                if (!visited.Add(upstream)) continue;
                if (upstream is CalibrationNode calibration)
                {
                    DeviceCalibration? device = Find(calibration.DeviceCode);
                    if (device == null) return null;
                    candidates.Add(device);
                    continue;
                }
                string? cameraCode = upstream switch
                {
                    BaseCameraNode camera => camera.DeviceCode,
                    LocalCameraNode camera => camera.DeviceCode,
                    LocalCalibrationNodeBase localCalibration => localCalibration.DeviceCode,
                    _ => null
                };
                if (cameraCode != null)
                {
                    DeviceCamera? camera = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
                        .FirstOrDefault(device => device.Code == cameraCode);
                    if (camera?.PhyCamera?.DeviceCalibration is { } device) candidates.Add(device);
                    continue;
                }
                next.AddRange(upstream.GetAllInputOptions().SelectMany(option => option.GetConnectedOption()).Select(option => option.Owner));
            }
            if (candidates.Count > 0) return candidates.Count == 1 ? candidates.Single() : null;
            current = next.ToArray();
        }

        DeviceCalibration[] devices = ServiceManager.Current?.DeviceServices.OfType<DeviceCalibration>().ToArray() ?? [];
        return devices.FirstOrDefault(device => device.Code == "DEV.Calibration.Default")
            ?? (devices.Length == 1 ? devices[0] : null);
    }
}
