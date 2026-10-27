using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        internal async Task<CopilotMcpToolCallResult> PreviewFlowPatchAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            if (!TryBuildFlowPatchRequest(arguments, out var request, out var error))
                return CopilotMcpToolCallResult.Fail("invalid_flow_patch", error);
            return await _environment.PreviewFlowPatchHandler(request, cancellationToken);
        }

        internal async Task<CopilotMcpToolCallResult> ApplyFlowPatchAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            if (!TryBuildFlowPatchRequest(arguments, out var request, out var error))
                return CopilotMcpToolCallResult.Fail("invalid_flow_patch", error);

            if (request.InheritCameraDefaults && string.IsNullOrEmpty(request.CameraDefaultsFingerprint))
                return CopilotMcpToolCallResult.Fail("invalid_flow_patch", "Applying inherited camera settings requires camera_defaults_fingerprint from preview_flow_patch. Preview the camera node first.");

            if (IsInAppAgentFrameworkApproved(executionScope))
                return await _environment.ApplyFlowPatchHandler(request, cancellationToken);

            var preview = await _environment.PreviewFlowPatchHandler(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!preview.Success)
                return preview;

            var normalizedArguments = BuildFlowPatchArguments(request);
            return CreateConfirmableActionResult(
                "Confirm Flow graph change",
                DescribeFlowPatch(request),
                CopilotSharedCapabilityCatalog.ApplyFlowPatch.McpToolName,
                normalizedArguments,
                preview.Text + Environment.NewLine + "Does not save or run the flow.",
                token => _environment.ApplyFlowPatchHandler(request, token),
                executeOnApproval: IsInAppAgent(executionScope),
                executionScope: executionScope,
                reviewDetails: request.InheritCameraDefaults || request.Operation == "disconnect" ? CopilotApprovalReviewTextEncoder.Encode(preview.Text) : null);
        }

        private static bool TryBuildFlowPatchRequest(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            out CopilotFlowPatchRequest request,
            out string error)
        {
            request = new CopilotFlowPatchRequest();
            var operation = GetString(arguments, "operation").Trim().ToLowerInvariant();
            var rawExpectedRevision = GetString(arguments, "expected_revision");
            var expectedRevision = rawExpectedRevision.Trim();
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
                InheritCameraDefaults = GetBool(arguments, "inherit_camera_defaults") ?? false,
                CameraDefaultsFingerprint = GetString(arguments, "camera_defaults_fingerprint").Trim().ToLowerInvariant(),
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
                _ when operation != "add_node" && arguments != null && (arguments.ContainsKey("inherit_camera_defaults") || arguments.ContainsKey("camera_defaults_fingerprint")) => "Camera defaults arguments are only supported by add_node.",
                _ when !request.InheritCameraDefaults && !string.IsNullOrEmpty(request.CameraDefaultsFingerprint) => "camera_defaults_fingerprint requires inherit_camera_defaults=true.",
                _ when !string.IsNullOrEmpty(request.CameraDefaultsFingerprint) && (request.CameraDefaultsFingerprint.Length != 64 || !request.CameraDefaultsFingerprint.All(Uri.IsHexDigit)) => "camera_defaults_fingerprint must be the 64-character hexadecimal fingerprint returned by preview_flow_patch.",
                "add_node" when string.IsNullOrWhiteSpace(request.TypeKey) => "add_node requires type_key.",
                "add_node" when GetInt(arguments, "left") == null || GetInt(arguments, "top") == null => "add_node requires integer left and top coordinates.",
                "set_property" when string.IsNullOrWhiteSpace(request.NodeId) => "set_property requires node_id.",
                "set_property" when string.IsNullOrWhiteSpace(request.PropertyName) => "set_property requires property_name.",
                "set_property" when arguments == null || !arguments.ContainsKey("value") => "set_property requires value; an empty string is allowed.",
                "connect" or "disconnect" when string.IsNullOrWhiteSpace(request.SourceNodeId) => $"{operation} requires source_node_id.",
                "connect" or "disconnect" when string.IsNullOrWhiteSpace(request.SourcePortId) => $"{operation} requires source_port_id.",
                "connect" or "disconnect" when string.IsNullOrWhiteSpace(request.TargetNodeId) => $"{operation} requires target_node_id.",
                "connect" or "disconnect" when string.IsNullOrWhiteSpace(request.TargetPortId) => $"{operation} requires target_port_id.",
                "disconnect" when rawExpectedRevision.Length != 64 || !rawExpectedRevision.All(Uri.IsHexDigit) => "disconnect requires the exact 64-character hexadecimal expected_revision.",
                "disconnect" when !IsExactFlowInstanceId(GetString(arguments, "source_node_id")) => "disconnect requires a nonempty source_node_id GUID in D format, not a node alias.",
                "disconnect" when !IsExactFlowInstanceId(GetString(arguments, "target_node_id")) => "disconnect requires a nonempty target_node_id GUID in D format, not a node alias.",
                "disconnect" when !IsExactFlowPortId(GetString(arguments, "source_port_id"), "out:") => "disconnect requires source_port_id in canonical out:<nonnegative integer> format.",
                "disconnect" when !IsExactFlowPortId(GetString(arguments, "target_port_id"), "in:") => "disconnect requires target_port_id in canonical in:<nonnegative integer> format.",
                "add_node" or "set_property" or "connect" or "disconnect" => string.Empty,
                _ => "operation must be add_node, set_property, connect, or disconnect.",
            };
            return string.IsNullOrEmpty(error);
        }

        private static bool IsExactFlowInstanceId(string value) =>
            value.Length == 36 && Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty;

        private static bool IsExactFlowPortId(string value, string prefix)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var digits = value.AsSpan(prefix.Length);
            if (digits.Length is < 1 or > 10 || digits.Length > 1 && digits[0] == '0') return false;
            foreach (var character in digits)
                if (!char.IsAsciiDigit(character)) return false;
            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out _);
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
                if (request.InheritCameraDefaults)
                {
                    arguments["inherit_camera_defaults"] = JsonSerializer.SerializeToElement(true);
                    arguments["camera_defaults_fingerprint"] = JsonSerializer.SerializeToElement(request.CameraDefaultsFingerprint);
                }
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
                "add_node" => $"Add Flow node {request.TypeKey} at ({request.Left}, {request.Top}).{(request.InheritCameraDefaults ? " Inherit the previewed camera acquisition settings." : string.Empty)}",
                "set_property" => $"Set Flow node {request.NodeId} property {request.PropertyName} to the previewed value.",
                "connect" => $"Connect {request.SourceNodeId}/{request.SourcePortId} to {request.TargetNodeId}/{request.TargetPortId}.",
                "disconnect" => $"断开连接 (Disconnect): {request.SourceNodeId}/{request.SourcePortId} → {request.TargetNodeId}/{request.TargetPortId}.",
                _ => "Apply Flow graph change.",
            };
        }
    }
}
