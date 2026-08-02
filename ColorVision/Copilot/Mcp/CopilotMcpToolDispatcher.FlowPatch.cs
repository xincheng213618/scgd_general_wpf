using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private async Task<CopilotMcpToolCallResult> PreviewFlowPatchAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            if (!TryBuildFlowPatchRequest(arguments, out var request, out var error))
                return CopilotMcpToolCallResult.Fail("invalid_flow_patch", error);
            return await _environment.PreviewFlowPatchHandler(request, cancellationToken);
        }

        private async Task<CopilotMcpToolCallResult> ApplyFlowPatchAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            if (!TryBuildFlowPatchRequest(arguments, out var request, out var error))
                return CopilotMcpToolCallResult.Fail("invalid_flow_patch", error);

            if (IsInAppAgentFrameworkApproved(executionScope))
                return await _environment.ApplyFlowPatchHandler(request, cancellationToken);

            var preview = await _environment.PreviewFlowPatchHandler(request, cancellationToken);
            if (!preview.Success)
                return preview;

            var normalizedArguments = BuildFlowPatchArguments(request);
            return CreateConfirmableActionResult(
                "Confirm Flow graph change",
                DescribeFlowPatch(request),
                "apply_flow_patch",
                normalizedArguments,
                preview.Text + Environment.NewLine + "Does not save or run the flow.",
                token => _environment.ApplyFlowPatchHandler(request, token),
                executeOnApproval: IsInAppAgent(executionScope),
                executionScope: executionScope);
        }

        private static bool TryBuildFlowPatchRequest(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            out CopilotFlowPatchRequest request,
            out string error)
        {
            request = new CopilotFlowPatchRequest();
            var operation = GetString(arguments, "operation").Trim().ToLowerInvariant();
            var expectedRevision = GetString(arguments, "expected_revision").Trim();
            if (string.IsNullOrWhiteSpace(expectedRevision))
            {
                error = "A current expected_revision from get_flow_graph is required.";
                return false;
            }

            request = new CopilotFlowPatchRequest
            {
                Operation = operation,
                ExpectedRevision = expectedRevision,
                TypeKey = GetString(arguments, "type_key").Trim(),
                Left = GetInt(arguments, "left") ?? 0,
                Top = GetInt(arguments, "top") ?? 0,
                NodeId = GetString(arguments, "node_id").Trim(),
                PropertyName = GetString(arguments, "property_name").Trim(),
                Value = GetString(arguments, "value"),
                SourceNodeId = GetString(arguments, "source_node_id").Trim(),
                SourcePortId = GetString(arguments, "source_port_id").Trim(),
                TargetNodeId = GetString(arguments, "target_node_id").Trim(),
                TargetPortId = GetString(arguments, "target_port_id").Trim(),
            };

            error = operation switch
            {
                "add_node" when string.IsNullOrWhiteSpace(request.TypeKey) => "add_node requires type_key.",
                "add_node" when GetInt(arguments, "left") == null || GetInt(arguments, "top") == null => "add_node requires integer left and top coordinates.",
                "set_property" when string.IsNullOrWhiteSpace(request.NodeId) => "set_property requires node_id.",
                "set_property" when string.IsNullOrWhiteSpace(request.PropertyName) => "set_property requires property_name.",
                "set_property" when arguments == null || !arguments.ContainsKey("value") => "set_property requires value; an empty string is allowed.",
                "connect" when string.IsNullOrWhiteSpace(request.SourceNodeId) => "connect requires source_node_id.",
                "connect" when string.IsNullOrWhiteSpace(request.SourcePortId) => "connect requires source_port_id.",
                "connect" when string.IsNullOrWhiteSpace(request.TargetNodeId) => "connect requires target_node_id.",
                "connect" when string.IsNullOrWhiteSpace(request.TargetPortId) => "connect requires target_port_id.",
                "add_node" or "set_property" or "connect" => string.Empty,
                _ => "operation must be add_node, set_property, or connect.",
            };
            return string.IsNullOrEmpty(error);
        }

        private static IReadOnlyDictionary<string, JsonElement> BuildFlowPatchArguments(CopilotFlowPatchRequest request)
        {
            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["operation"] = JsonSerializer.SerializeToElement(request.Operation),
                ["expected_revision"] = JsonSerializer.SerializeToElement(request.ExpectedRevision),
            };
            if (request.Operation == "add_node")
            {
                arguments["type_key"] = JsonSerializer.SerializeToElement(request.TypeKey);
                arguments["left"] = JsonSerializer.SerializeToElement(request.Left);
                arguments["top"] = JsonSerializer.SerializeToElement(request.Top);
            }
            else if (request.Operation == "set_property")
            {
                arguments["node_id"] = JsonSerializer.SerializeToElement(request.NodeId);
                arguments["property_name"] = JsonSerializer.SerializeToElement(request.PropertyName);
                arguments["value"] = JsonSerializer.SerializeToElement(request.Value);
            }
            else
            {
                arguments["source_node_id"] = JsonSerializer.SerializeToElement(request.SourceNodeId);
                arguments["source_port_id"] = JsonSerializer.SerializeToElement(request.SourcePortId);
                arguments["target_node_id"] = JsonSerializer.SerializeToElement(request.TargetNodeId);
                arguments["target_port_id"] = JsonSerializer.SerializeToElement(request.TargetPortId);
            }
            return arguments;
        }

        private static string DescribeFlowPatch(CopilotFlowPatchRequest request)
        {
            return request.Operation switch
            {
                "add_node" => $"Add Flow node {request.TypeKey} at ({request.Left}, {request.Top}).",
                "set_property" => $"Set Flow node {request.NodeId} property {request.PropertyName} to the previewed value.",
                "connect" => $"Connect {request.SourceNodeId}/{request.SourcePortId} to {request.TargetNodeId}/{request.TargetPortId}.",
                _ => "Apply Flow graph change.",
            };
        }
    }
}
