using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using FlowEngineLib;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;

namespace ProjectARVRPro.Process
{
    internal readonly record struct FlowCameraParameterOverrideResult(bool Applied, string Message);

    /// <summary>
    /// Applies external values directly to the supported acquisition node in
    /// the already loaded runtime Flow. It never saves the Flow template.
    /// </summary>
    internal static class FlowCameraParameterOverrideService
    {
        internal static FlowCameraParameterOverrideResult ApplyToLoadedFlow(
            FlowCameraParameterOverrideConfig config,
            IEnumerable<STNode> nodes,
            string? startNodeName)
        {
            ArgumentNullException.ThrowIfNull(config);
            if (!config.IsEnabled)
                return new FlowCameraParameterOverrideResult(false, string.Empty);

            try
            {
                return ApplyEnabledOverride(config, nodes, startNodeName);
            }
            catch (Exception ex)
            {
                return Skipped($"相机参数覆盖异常：{ex.Message}，已跳过。");
            }
        }

        private static FlowCameraParameterOverrideResult ApplyEnabledOverride(
            FlowCameraParameterOverrideConfig config,
            IEnumerable<STNode> nodes,
            string? startNodeName)
        {
            if (!float.IsFinite(config.ExposureTimeMs) || config.ExposureTimeMs <= 0)
                return Skipped("外部曝光时间无效，已跳过相机参数覆盖。");

            if (!TryFindSingleCameraNode(nodes, startNodeName, GetConnectedOutputNodes, out STNode? cameraNode, out string reason))
                return Skipped($"{reason}，已跳过外部相机参数覆盖。");

            string calibrationTemplateName = string.IsNullOrWhiteSpace(config.CalibrationTemplateName)
                ? string.Empty
                : config.CalibrationTemplateName;
            if (cameraNode is LocalCameraNode localCamera
                && !LocalCalibrationTemplateExists(localCamera, calibrationTemplateName))
                return Skipped($"相机设备中不存在校正模板“{calibrationTemplateName}”，已跳过外部相机参数覆盖。");

            switch (cameraNode)
            {
                case LVCameraNode camera:
                    if (config.ExposureTimeMs > (double)int.MaxValue - camera.MaxTime)
                        return Skipped("外部曝光时间超出 L/BV 相机节点可执行范围，已跳过相机参数覆盖。");

                    camera.ExpTime = config.ExposureTimeMs;
                    camera.CaliTempName = calibrationTemplateName;
                    return Applied(camera.Title);

                case LocalCameraNode camera:
                    camera.ExpTime = config.ExposureTimeMs;
                    camera.CalibTempName = calibrationTemplateName;
                    return Applied(camera.Title);

                default:
                    return Skipped("当前相机取图节点不受支持，已跳过外部相机参数覆盖。");
            }
        }

        internal static bool TryFindSingleCameraNode(
            IEnumerable<STNode> nodes,
            string? startNodeName,
            Func<STNode, IEnumerable<STNode>> getConnectedOutputNodes,
            out STNode? cameraNode,
            out string reason)
        {
            STNode[] loadedNodes = nodes.ToArray();
            BaseStartNode? startNode = loadedNodes
                .OfType<BaseStartNode>()
                .FirstOrDefault(node => string.Equals(node.NodeName, startNodeName, StringComparison.Ordinal));
            if (startNode == null)
            {
                cameraNode = null;
                reason = "未找到流程执行入口";
                return false;
            }

            var reachableNodes = new HashSet<STNode> { startNode };
            var pendingNodes = new Queue<STNode>();
            pendingNodes.Enqueue(startNode);
            while (pendingNodes.Count > 0)
            {
                STNode node = pendingNodes.Dequeue();
                foreach (STNode outputNode in getConnectedOutputNodes(node))
                {
                    if (reachableNodes.Add(outputNode))
                        pendingNodes.Enqueue(outputNode);
                }
            }

            STNode[] cameraNodes = loadedNodes
                .Where(node => node is LVCameraNode or LocalCameraNode)
                .Where(reachableNodes.Contains)
                .Take(2)
                .ToArray();
            if (cameraNodes.Length == 0)
            {
                cameraNode = null;
                reason = "未找到相机取图节点";
                return false;
            }
            if (cameraNodes.Length > 1)
            {
                cameraNode = null;
                reason = "存在多个相机取图节点";
                return false;
            }

            cameraNode = cameraNodes[0];
            reason = string.Empty;
            return true;
        }

        private static IEnumerable<STNode> GetConnectedOutputNodes(STNode node)
        {
            foreach (STNodeOption output in node.GetAllOutputOptions())
            {
                foreach (STNodeOption input in output.ConnectedOption)
                    yield return input.Owner;
            }
        }

        private static bool LocalCalibrationTemplateExists(
            LocalCameraNode cameraNode,
            string calibrationTemplateName)
        {
            if (calibrationTemplateName.Length == 0)
                return true;

            DeviceCamera? device = ServiceManager.GetInstance().DeviceServices
                .OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, cameraNode.DeviceCode, StringComparison.Ordinal));
            return device?.PhyCamera?.CalibrationParams.Any(template =>
                string.Equals(template.Key, calibrationTemplateName, StringComparison.Ordinal)) == true;
        }

        private static FlowCameraParameterOverrideResult Applied(string title) =>
            new(true, $"已修改本次运行的相机节点“{title}”。");

        private static FlowCameraParameterOverrideResult Skipped(string message) => new(false, message);
    }
}
