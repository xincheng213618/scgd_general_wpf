using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    public delegate Task<CopilotMcpToolCallResult> CopilotMcpToolHandler(
        IReadOnlyDictionary<string, JsonElement>? arguments,
        string callerSource,
        CancellationToken cancellationToken);

    public sealed class CopilotMcpToolRouter
    {
        private readonly Dictionary<string, CopilotMcpToolHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> ToolNames => _handlers.Keys;

        public CopilotMcpToolRouter Register(string toolName, CopilotMcpToolHandler handler)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("A tool name is required.", nameof(toolName));
            ArgumentNullException.ThrowIfNull(handler);

            if (!_handlers.TryAdd(toolName.Trim(), handler))
                throw new InvalidOperationException($"An MCP tool handler is already registered for {toolName}.");

            return this;
        }

        public Task<CopilotMcpToolCallResult> DispatchAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            string callerSource,
            CancellationToken cancellationToken)
        {
            if (!_handlers.TryGetValue(toolName ?? string.Empty, out var handler))
                return Task.FromResult(CopilotMcpToolCallResult.Fail("tool_not_found", $"Unknown MCP tool: {toolName}"));

            return handler(arguments, callerSource ?? string.Empty, cancellationToken);
        }
    }
}
