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

    internal delegate Task<CopilotMcpToolCallResult> CopilotScopedMcpToolHandler(
        IReadOnlyDictionary<string, JsonElement>? arguments,
        CopilotExecutionScope executionScope,
        CancellationToken cancellationToken);

    public sealed class CopilotMcpToolRouter
    {
        private readonly Dictionary<string, CopilotScopedMcpToolHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> ToolNames => _handlers.Keys;

        public CopilotMcpToolRouter Register(string toolName, CopilotMcpToolHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            return RegisterScoped(
                toolName,
                (arguments, scope, cancellationToken) =>
                    handler(arguments, scope.CallerIdentity, cancellationToken));
        }

        internal CopilotMcpToolRouter RegisterScoped(string toolName, CopilotScopedMcpToolHandler handler)
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
            return DispatchAsync(
                toolName,
                arguments,
                CopilotExecutionScope.ForInProcess(callerSource ?? string.Empty),
                cancellationToken);
        }

        internal Task<CopilotMcpToolCallResult> DispatchAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            if (!_handlers.TryGetValue(toolName ?? string.Empty, out var handler))
                return Task.FromResult(CopilotMcpToolCallResult.Fail("tool_not_found", $"Unknown MCP tool: {toolName}"));

            return handler(arguments, executionScope ?? CopilotExecutionScope.Empty, cancellationToken);
        }
    }
}
