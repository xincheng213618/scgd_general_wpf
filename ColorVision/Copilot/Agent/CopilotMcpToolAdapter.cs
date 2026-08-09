using ColorVision.Copilot.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotMcpToolAdapter : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation, ICopilotCapabilityCatalogIdentity
    {
        private const int MaximumResultBlocks = 256;
        private const int MaximumResultLength = 65_536;
        private const string ResultTruncationMarker = "...<MCP result truncated>";
        private readonly CopilotMcpClientServerConfig _server;
        private readonly McpClientTool _remoteTool;

        public CopilotMcpToolAdapter(
            CopilotMcpClientServerConfig server,
            McpClientTool remoteTool,
            CopilotMcpClientAccessPolicy accessPolicy)
        {
            _server = server?.Clone() ?? throw new ArgumentNullException(nameof(server));
            _remoteTool = remoteTool ?? throw new ArgumentNullException(nameof(remoteTool));
            Name = CopilotMcpToolIdentity.BuildLocalName(_server.Name, remoteTool.Name);
            Description = BuildDescription(_server.Name, remoteTool.Description);
            InputSchema = CopilotToolInputSchema.FromJsonSchema(remoteTool.JsonSchema);
            Capability = CopilotMcpClientCapabilityPolicy.Create(
                accessPolicy,
                TimeSpan.FromSeconds(_server.ToolTimeoutSeconds),
                remoteTool.ProtocolTool.Annotations);
        }

        public string Name { get; }

        public string Description { get; }

        public string CatalogCapabilityKey => CopilotMcpToolIdentity.BuildCatalogKey(_remoteTool.ProtocolTool.Name);

        public CopilotToolCapabilityDescriptor Capability { get; }

        public CopilotToolAccess Access => Capability.Access;

        public CopilotToolRiskLevel RiskLevel => Capability.RiskLevel;

        public CopilotToolApprovalMode ApprovalMode => Capability.ApprovalMode;

        public CopilotToolIdempotency Idempotency => Capability.Idempotency;

        public CopilotToolConcurrencyMode ConcurrencyMode => Capability.ConcurrencyMode;

        public CopilotToolInputSchema InputSchema { get; }

        public TimeSpan ExecutionTimeout => Capability.ExecutionTimeout;

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && !string.IsNullOrWhiteSpace(request.UserText)
                && CopilotToolIntentPolicy.CanExposeExternalTool(
                    request,
                    _remoteTool.ProtocolTool.Name,
                    _remoteTool.Description);
        }

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => $"mcp:{_server.Name}:{_remoteTool.ProtocolTool.Name}";

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            if (Access == CopilotToolAccess.ReadOnly)
                return InvokeRemoteAsync(toolInput, cancellationToken);

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = $"{Name} requires explicit approval.",
                ErrorMessage = "External MCP tools configured with the approval policy can run only after the exact call is approved.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
            => InvokeRemoteAsync(toolInput, cancellationToken);

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
            => CopilotMcpClientApprovalPresentation.Create(_server.Name, _remoteTool.ProtocolTool.Name, toolInput);

        private async Task<CopilotToolResult> InvokeRemoteAsync(CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            var result = await _remoteTool.CallAsync(
                toolInput.Arguments,
                options: CreateRequestOptionsForCurrentInvocation(),
                cancellationToken: cancellationToken);
            var content = BuildResultContent(result);
            var isError = result.IsError == true;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = !isError,
                Summary = isError
                    ? $"External MCP tool {_server.Name}/{_remoteTool.ProtocolTool.Name} returned an error."
                    : $"External MCP tool {_server.Name}/{_remoteTool.ProtocolTool.Name} completed.",
                Content = isError ? string.Empty : content,
                ErrorMessage = isError ? content : string.Empty,
                FailureKind = isError ? CopilotToolFailureKind.Unspecified : CopilotToolFailureKind.None,
            };
        }

        internal static RequestOptions? CreateRequestOptionsForCurrentInvocation()
        {
            var callId = CopilotToolInvocationContext.Current?.CallId;
            if (string.IsNullOrWhiteSpace(callId))
                return null;

            return new RequestOptions
            {
                Meta = new JsonObject
                {
                    ["callId"] = callId,
                },
            };
        }

        private static string BuildResultContent(CallToolResult result)
        {
            var builder = new StringBuilder();
            var maximumPayloadLength = MaximumResultLength - Environment.NewLine.Length - ResultTruncationMarker.Length;
            var wasTruncated = result.Content.Count > MaximumResultBlocks;
            foreach (var block in result.Content.Take(MaximumResultBlocks))
            {
                if (block is not TextContentBlock textBlock)
                    continue;

                if (!TryAppendResultPart(builder, textBlock.Text, maximumPayloadLength))
                {
                    wasTruncated = true;
                    break;
                }
            }

            if (result.StructuredContent.HasValue)
            {
                if (!TryAppendStructuredResult(builder, result.StructuredContent.Value, maximumPayloadLength))
                    wasTruncated = true;
            }

            if (builder.Length == 0 && result.Content.Count > 0)
            {
                if (!TryAppendResultPart(builder, $"MCP returned {result.Content.Count} non-text content block(s).", maximumPayloadLength))
                    wasTruncated = true;
            }

            if (wasTruncated)
            {
                if (builder.Length > 0)
                    builder.AppendLine();
                builder.Append(ResultTruncationMarker);
            }
            return builder.ToString();
        }

        private static bool TryAppendStructuredResult(
            StringBuilder builder,
            JsonElement structuredContent,
            int maximumLength)
        {
            var separatorLength = builder.Length == 0 ? 0 : Environment.NewLine.Length * 2;
            var availableLength = maximumLength - builder.Length - separatorLength;
            if (availableLength <= 0)
                return false;

            var content = SerializeStructuredContentBounded(structuredContent, availableLength, out var wasTruncated);
            return TryAppendResultPart(builder, content, maximumLength) && !wasTruncated;
        }

        private static string SerializeStructuredContentBounded(
            JsonElement structuredContent,
            int maximumCharacters,
            out bool wasTruncated)
        {
            using var output = new CopilotMcpResultWriteStream(checked(maximumCharacters * 4));
            var writer = new Utf8JsonWriter(output);
            wasTruncated = false;
            try
            {
                structuredContent.WriteTo(writer);
                writer.Flush();
                writer.Dispose();
            }
            catch (CopilotMcpResultSizeLimitException)
            {
                wasTruncated = true;
            }
            return Encoding.UTF8.GetString(output.ToArray());
        }

        private static bool TryAppendResultPart(StringBuilder builder, string? value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            var start = 0;
            var end = value.Length;
            while (start < end && char.IsWhiteSpace(value[start]))
                start++;
            while (end > start && char.IsWhiteSpace(value[end - 1]))
                end--;
            if (start == end)
                return true;

            var separatorLength = builder.Length == 0 ? 0 : Environment.NewLine.Length * 2;
            var availableLength = maximumLength - builder.Length - separatorLength;
            if (availableLength <= 0)
                return false;

            if (separatorLength > 0)
                builder.AppendLine().AppendLine();

            var valueLength = end - start;
            if (valueLength <= availableLength)
            {
                builder.Append(value, start, valueLength);
                return true;
            }

            var retainedLength = availableLength;
            if (retainedLength > 0
                && start + retainedLength < end
                && char.IsHighSurrogate(value[start + retainedLength - 1])
                && char.IsLowSurrogate(value[start + retainedLength]))
            {
                retainedLength--;
            }
            if (retainedLength > 0)
                builder.Append(value, start, retainedLength);
            return false;
        }

        private sealed class CopilotMcpResultSizeLimitException : IOException
        {
        }

        private sealed class CopilotMcpResultWriteStream(int maximumBytes) : MemoryStream
        {
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (count > maximumBytes - Length)
                    throw new CopilotMcpResultSizeLimitException();
                base.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                if (buffer.Length > maximumBytes - Length)
                    throw new CopilotMcpResultSizeLimitException();
                base.Write(buffer);
            }
        }

        private static string BuildDescription(string serverName, string? description)
        {
            var normalized = (description ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (normalized.Length > 800)
                normalized = normalized[..800] + "...";
            return $"External MCP tool from configured server '{serverName}'. {normalized}".TrimEnd();
        }
    }

}
