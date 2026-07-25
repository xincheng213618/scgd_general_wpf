#pragma warning disable CA1822,CA1859,CS8602
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ST.Library.UI.NodeEditor;

namespace ColorVision.Engine.FlowProcessing.Integration
{
    /// <summary>
    /// Applies guarded edits to the active Flow graph.
    /// </summary>
    public sealed class FlowCopilotGraphEditor
    {
        private readonly FlowEngineManager _manager;
        private readonly FlowCopilotContextService _context;

        private ViewFlow View => _manager.View;
        private FlowControl FlowControl => _manager.FlowControl;

        public FlowCopilotGraphEditor(FlowEngineManager manager, FlowCopilotContextService context)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public CopilotFlowNodeContextSnapshot PreviewNodeAddition(string typeKey, int left, int top, string? expectedRevision)
        {
            EnsureMutationAllowed(expectedRevision);
            var node = CreateNode(typeKey, left, top);
            return FlowCopilotContextService.BuildNodeSnapshot(node, isActive: false);
        }

        public CopilotFlowContextSnapshot AddNode(string typeKey, int left, int top, string? expectedRevision)
        {
            EnsureMutationAllowed(expectedRevision);
            var editor = View?.STNodeEditorMain ?? throw new InvalidOperationException("No active Flow editor is available.");
            var node = CreateNode(typeKey, left, top);
            try
            {
                if (node is CVBaseServerNode serverNode)
                {
                    var matchedService = MqttRCService.GetInstance().ServiceTokens.FirstOrDefault(service => service.Devices.Any(device => device.Key == serverNode.DeviceCode));
                    if (matchedService != null)
                        serverNode.Token = matchedService.Token;
                }
                else if (node is MQTTStartNode startNode)
                {
                    startNode.Server = MQTTControl.Config.Host;
                    startNode.Port = MQTTControl.Config.Port;
                }

                editor.Nodes.Add(node);
                return _context.CaptureSnapshot();
            }
            catch
            {
                if (editor.Nodes.Contains(node))
                    editor.Nodes.Remove(node);
                throw;
            }
        }

        public (CopilotFlowNodeContextSnapshot Node, string PropertyName, string OldValue, string NewValue) PreviewNodePropertyChange(
            string nodeId,
            string propertyName,
            string value,
            string? expectedRevision)
        {
            EnsureMutationAllowed(expectedRevision);
            var node = FindNode(nodeId);
            var property = ResolveProperty(node, propertyName);
            var clone = Activator.CreateInstance(node.GetType()) as STNode
                ?? throw new InvalidOperationException($"The Flow node type cannot be created: {FlowCopilotContextService.GetNodeTypeKey(node.GetType())}");
            clone.Create();
            clone.OnLoadNode(ParseNodeSaveData(node.GetSaveData()));
            var oldValue = FormatPropertyValue(property.GetValue(clone));
            clone.OnLoadNode(new Dictionary<string, byte[]> { [property.Name] = Encoding.UTF8.GetBytes(value ?? string.Empty) });
            return (FlowCopilotContextService.BuildNodeSnapshot(clone, isActive: false), property.Name, oldValue, FormatPropertyValue(property.GetValue(clone)));
        }

        public CopilotFlowContextSnapshot SetNodeProperty(
            string nodeId,
            string propertyName,
            string value,
            string? expectedRevision)
        {
            EnsureMutationAllowed(expectedRevision);
            var editor = View.STNodeEditorMain;
            var node = FindNode(nodeId);
            var property = ResolveProperty(node, propertyName);
            var oldValue = property.GetValue(node);
            try
            {
                node.OnLoadNode(new Dictionary<string, byte[]> { [property.Name] = Encoding.UTF8.GetBytes(value ?? string.Empty) });
                editor.Invalidate();
                return _context.CaptureSnapshot();
            }
            catch
            {
                property.SetValue(node, oldValue);
                editor.Invalidate();
                throw;
            }
        }

        public CopilotFlowEdgeContextSnapshot PreviewConnection(
            string sourceNodeId,
            string sourcePortId,
            string targetNodeId,
            string targetPortId,
            string? expectedRevision)
        {
            EnsureMutationAllowed(expectedRevision);
            var (output, input) = ResolveConnection(sourceNodeId, sourcePortId, targetNodeId, targetPortId);
            EnsureConnectionAllowed(output, input);
            return BuildEdgeSnapshot(output, input);
        }

        public CopilotFlowContextSnapshot ConnectNodes(
            string sourceNodeId,
            string sourcePortId,
            string targetNodeId,
            string targetPortId,
            string? expectedRevision)
        {
            EnsureMutationAllowed(expectedRevision);
            var (output, input) = ResolveConnection(sourceNodeId, sourcePortId, targetNodeId, targetPortId);
            EnsureConnectionAllowed(output, input);
            var status = output.ConnectOption(input);
            if (status != ConnectionStatus.Connected)
                throw new InvalidOperationException($"Flow connection was rejected: {status}.");

            try
            {
                return _context.CaptureSnapshot();
            }
            catch
            {
                output.DisConnectOption(input);
                throw;
            }
        }

        private STNode FindNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new InvalidOperationException("A stable Flow node instance id is required.");

            var node = View?.STNodeEditorMain?.Nodes.Cast<STNode>().FirstOrDefault(candidate =>
                string.Equals(candidate.Guid.ToString(), nodeId, StringComparison.OrdinalIgnoreCase)
                || candidate is CVCommonNode commonNode && string.Equals(commonNode.NodeID, nodeId, StringComparison.OrdinalIgnoreCase));
            return node ?? throw new InvalidOperationException($"The Flow node was not found: {nodeId}");
        }

        private static PropertyInfo ResolveProperty(STNode node, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new InvalidOperationException("An exact Flow property name is required.");
            if (IsSensitivePropertyName(propertyName))
                throw new InvalidOperationException($"Copilot cannot read or change the sensitive Flow property: {propertyName}");

            var property = node.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The Flow property was not found: {propertyName}");
            var attribute = property.GetCustomAttribute<STNodePropertyAttribute>();
            if (attribute == null || attribute.IsHide || attribute.IsReadOnly || property.SetMethod?.IsPublic != true)
                throw new InvalidOperationException($"The Flow property is not writable through Copilot: {propertyName}");
            return property;
        }

        private (STNodeOption Output, STNodeOption Input) ResolveConnection(
            string sourceNodeId,
            string sourcePortId,
            string targetNodeId,
            string targetPortId)
        {
            var sourceNode = FindNode(sourceNodeId);
            var targetNode = FindNode(targetNodeId);
            var output = ResolvePort(sourceNode.GetAllOutputOptions(), sourcePortId, "out");
            var input = ResolvePort(targetNode.GetAllInputOptions(), targetPortId, "in");
            return (output, input);
        }

        private static STNodeOption ResolvePort(STNodeOption[] options, string portId, string expectedDirection)
        {
            var prefix = expectedDirection + ":";
            if (string.IsNullOrWhiteSpace(portId)
                || !portId.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(portId[prefix.Length..], out var index)
                || index < 0
                || index >= options.Length)
            {
                throw new InvalidOperationException($"The Flow port id is invalid or unavailable: {portId}");
            }

            return options[index] ?? throw new InvalidOperationException($"The Flow port is unavailable: {portId}");
        }

        private static void EnsureConnectionAllowed(STNodeOption output, STNodeOption input)
        {
            var outputStatus = output.CanConnect(input);
            if (outputStatus != ConnectionStatus.Connected)
                throw new InvalidOperationException($"The source port cannot connect to the target port: {outputStatus}.");
            var inputStatus = input.CanConnect(output);
            if (inputStatus != ConnectionStatus.Connected)
                throw new InvalidOperationException($"The target port cannot accept the source port: {inputStatus}.");
        }

        private static CopilotFlowEdgeContextSnapshot BuildEdgeSnapshot(STNodeOption output, STNodeOption input)
        {
            return new CopilotFlowEdgeContextSnapshot
            {
                SourceNodeId = output.Owner.Guid.ToString(),
                SourcePortId = $"out:{Array.IndexOf(output.Owner.GetAllOutputOptions(), output)}",
                SourcePortName = output.Text ?? string.Empty,
                TargetNodeId = input.Owner.Guid.ToString(),
                TargetPortId = $"in:{Array.IndexOf(input.Owner.GetAllInputOptions(), input)}",
                TargetPortName = input.Text ?? string.Empty,
                DataType = output.DataType?.FullName ?? output.DataType?.Name ?? "System.Object",
            };
        }

        private static Dictionary<string, byte[]> ParseNodeSaveData(byte[] data)
        {
            var position = 0;
            var typeLength = data[position++];
            position += typeLength;
            var guidTypeLength = data[position++];
            position += guidTypeLength;
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            while (position < data.Length)
            {
                if (position + sizeof(int) > data.Length)
                    throw new InvalidDataException("The Flow node save data is incomplete.");
                var keyLength = BitConverter.ToInt32(data, position);
                position += sizeof(int);
                if (keyLength < 0 || position + keyLength + sizeof(int) > data.Length)
                    throw new InvalidDataException("The Flow node save data contains an invalid property name.");
                var key = Encoding.UTF8.GetString(data, position, keyLength);
                position += keyLength;
                var valueLength = BitConverter.ToInt32(data, position);
                position += sizeof(int);
                if (valueLength < 0 || position + valueLength > data.Length)
                    throw new InvalidDataException("The Flow node save data contains an invalid property value.");
                var value = new byte[valueLength];
                Array.Copy(data, position, value, 0, valueLength);
                position += valueLength;
                result[key] = value;
            }
            return result;
        }

        private static string FormatPropertyValue(object? value)
        {
            if (value is Array array)
                return string.Join(",", array.Cast<object?>().Select(item => item?.ToString() ?? string.Empty));
            return value?.ToString() ?? string.Empty;
        }

        private static bool IsSensitivePropertyName(string propertyName)
        {
            return new[] { "password", "passwd", "pwd", "secret", "token", "apikey", "api_key", "accesskey", "privatekey", "license", "sn" }
                .Any(term => propertyName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureMutationAllowed(string? expectedRevision)
        {
            if (FlowControl?.IsFlowRun == true)
                throw new InvalidOperationException("The active flow is running. Stop it before editing the graph.");
            if (View?.STNodeEditorMain == null)
                throw new InvalidOperationException("No active Flow editor is available.");

            if (!string.IsNullOrWhiteSpace(expectedRevision))
            {
                var currentRevision = _context.CaptureSnapshot().Revision;
                if (!string.Equals(currentRevision, expectedRevision, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"The flow changed after preview. Expected revision {expectedRevision}, current revision {currentRevision}.");
            }
        }

        private STNode CreateNode(string typeKey, int left, int top)
        {
            if (string.IsNullOrWhiteSpace(typeKey))
                throw new InvalidOperationException("An exact Flow node type key is required.");
            if (left is < -100000 or > 100000 || top is < -100000 or > 100000)
                throw new InvalidOperationException("Flow node position must be between -100000 and 100000.");

            var type = View?.STNodeEditorMain?.GetTypes()
                .FirstOrDefault(candidate => FlowCopilotContextService.IsVisibleFlowNodeType(candidate)
                    && string.Equals(FlowCopilotContextService.GetNodeTypeKey(candidate), typeKey, StringComparison.Ordinal));
            if (type == null || type.IsAbstract || !typeof(STNode).IsAssignableFrom(type))
                throw new InvalidOperationException($"The Flow node type is not loaded: {typeKey}");

            var node = Activator.CreateInstance(type) as STNode
                ?? throw new InvalidOperationException($"The Flow node type cannot be created: {typeKey}");
            node.Create();
            node.Left = left;
            node.Top = top;
            return node;
        }

    }
}
