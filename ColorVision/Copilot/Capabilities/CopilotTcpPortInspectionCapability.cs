using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotTcpPortInspectionService
    {
        private const int MaximumBindings = 64;
        private readonly CopilotShellCommandService _shellService;

        public CopilotTcpPortInspectionService()
            : this(new CopilotShellCommandService())
        {
        }

        public CopilotTcpPortInspectionService(CopilotShellCommandService shellService)
        {
            _shellService = shellService ?? throw new ArgumentNullException(nameof(shellService));
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            input ??= CopilotAgentToolInput.Empty;
            if (!TryGetPort(input, out var port))
            {
                return Failure(CopilotToolFailureKind.Validation,
                    "The TCP port is invalid.", "port must be an integer from 1 through 65535.");
            }

            var shellResult = await _shellService.ExecuteAsync(request, new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["command"] = BuildInspectionCommand(port),
                    ["shell"] = "powershell",
                    ["timeoutSeconds"] = 30,
                },
            }, cancellationToken);
            if (!shellResult.Success)
            {
                return Failure(shellResult.FailureKind,
                    $"TCP port {port} could not be inspected.", shellResult.ErrorMessage, shellResult.Content);
            }
            if (!TryExtractExitCode(shellResult.Content, out var exitCode) || exitCode != 0)
            {
                return Failure(CopilotToolFailureKind.Internal,
                    $"TCP port {port} could not be inspected.",
                    exitCode == 0
                        ? "The fixed PowerShell diagnostic did not report an exit code."
                        : $"The fixed PowerShell diagnostic exited with code {exitCode}.",
                    shellResult.Content);
            }
            if (!TryExtractStandardOutput(shellResult.Content, out var standardOutput)
                || !TryParseInspection(standardOutput, port, out var occupied, out var bindingCount, out var normalizedJson))
            {
                return Failure(CopilotToolFailureKind.Internal,
                    $"TCP port {port} returned an invalid inspection result.",
                    "The fixed PowerShell diagnostic did not return the expected structured JSON.",
                    shellResult.Content);
            }

            return new CopilotToolResult
            {
                ToolName = "InspectTcpPort",
                Success = true,
                Summary = occupied
                    ? $"TCP port {port} is currently in use by {bindingCount} binding(s)."
                    : $"TCP port {port} is not currently in use.",
                Content = $"[TCP Port Inspection]\nport: {port}\noccupied: {occupied.ToString().ToLowerInvariant()}\nbinding_count: {bindingCount}\nresult_json: {normalizedJson}",
            };
        }

        internal static string BuildInspectionCommand(int port)
        {
            if (port is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));

            return $$"""
                $ErrorActionPreference = 'Stop'
                if (-not (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue)) { throw 'Get-NetTCPConnection is unavailable.' }
                $connections = @(Get-NetTCPConnection -LocalPort {{port}} -ErrorAction SilentlyContinue | Sort-Object State,LocalAddress,RemoteAddress,RemotePort)
                $bindings = @($connections | Select-Object -First {{MaximumBindings}} | ForEach-Object {
                    $owner = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue
                    [ordered]@{
                        local_address = $_.LocalAddress
                        local_port = [int]$_.LocalPort
                        remote_address = $_.RemoteAddress
                        remote_port = [int]$_.RemotePort
                        state = [string]$_.State
                        process_id = [int]$_.OwningProcess
                        process_name = if ($null -eq $owner) { '' } else { [string]$owner.ProcessName }
                    }
                })
                [ordered]@{
                    port = {{port}}
                    occupied = ($connections.Count -gt 0)
                    binding_count = $connections.Count
                    truncated = ($connections.Count -gt {{MaximumBindings}})
                    bindings = $bindings
                } | ConvertTo-Json -Compress -Depth 4
                """;
        }

        private static bool TryGetPort(CopilotAgentToolInput input, out int port)
        {
            port = 0;
            if (input.Arguments == null
                || !input.Arguments.TryGetValue("port", out var raw)
                || raw == null)
            {
                return false;
            }

            switch (raw)
            {
                case int intValue:
                    port = intValue;
                    break;
                case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                    port = (int)longValue;
                    break;
                case JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var jsonValue):
                    port = jsonValue;
                    break;
                default:
                    return false;
            }
            return port is >= 1 and <= 65535;
        }

        private static bool TryExtractStandardOutput(string content, out string standardOutput)
        {
            standardOutput = string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                return false;

            const string startMarker = "stdout:";
            const string endMarker = "stderr:";
            var start = content.IndexOf(startMarker, StringComparison.Ordinal);
            if (start < 0)
                return false;
            start += startMarker.Length;
            var end = content.IndexOf(endMarker, start, StringComparison.Ordinal);
            if (end < 0)
                return false;
            standardOutput = content[start..end].Trim();
            return standardOutput.Length > 0 && !string.Equals(standardOutput, "<empty>", StringComparison.Ordinal);
        }

        private static bool TryExtractExitCode(string content, out int exitCode)
        {
            exitCode = 0;
            if (string.IsNullOrWhiteSpace(content))
                return false;

            const string marker = "exit_code:";
            var start = content.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return false;
            start += marker.Length;
            var end = content.IndexOf('\n', start);
            var value = (end < 0 ? content[start..] : content[start..end]).Trim();
            return int.TryParse(value, out exitCode);
        }

        private static bool TryParseInspection(
            string json,
            int expectedPort,
            out bool occupied,
            out int bindingCount,
            out string normalizedJson)
        {
            occupied = false;
            bindingCount = 0;
            normalizedJson = string.Empty;
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("port", out var portElement)
                    || !portElement.TryGetInt32(out var port)
                    || port != expectedPort
                    || !root.TryGetProperty("occupied", out var occupiedElement)
                    || occupiedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                    || !root.TryGetProperty("binding_count", out var countElement)
                    || !countElement.TryGetInt32(out bindingCount)
                    || bindingCount < 0
                    || !root.TryGetProperty("bindings", out var bindingsElement)
                    || bindingsElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                occupied = occupiedElement.GetBoolean();
                if (occupied != (bindingCount > 0))
                    return false;
                normalizedJson = root.GetRawText();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static CopilotToolResult Failure(
            CopilotToolFailureKind kind,
            string summary,
            string error,
            string content = "")
        {
            return new CopilotToolResult
            {
                ToolName = "InspectTcpPort",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
                Content = content,
            };
        }
    }
}
