using ColorVision.Copilot.Mcp;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotMcpToolDiscoveryBatch(
        IReadOnlyList<Tool> Tools,
        int DiscoveredToolCount,
        int PageCount,
        int DuplicateToolCount,
        int RejectedToolCount,
        bool Truncated);

    internal static class CopilotMcpToolDiscoveryPaginator
    {
        public const int MaximumToolDefinitions = 512;
        public const int MaximumPages = 32;
        public const int MaximumRemoteToolNameLength = 128;
        public const int MaximumSerializedToolDefinitionBytes = 128 * 1024;
        public const int MaximumTotalToolDefinitionBytes = 2 * 1024 * 1024;

        public static async Task<CopilotMcpToolDiscoveryBatch> DiscoverAsync(
            Func<ListToolsRequestParams, CancellationToken, ValueTask<ListToolsResult>> listPageAsync,
            int maximumToolDefinitions = MaximumToolDefinitions,
            int maximumPages = MaximumPages,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listPageAsync);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumToolDefinitions, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPages, 1);

            var tools = new List<Tool>(Math.Min(maximumToolDefinitions, 64));
            var seenCursors = new HashSet<string>(StringComparer.Ordinal);
            var seenToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var discoveredToolCount = 0;
            var duplicateToolCount = 0;
            var rejectedToolCount = 0;
            var serializedToolDefinitionBytes = 0;
            var pageCount = 0;
            string? cursor = null;
            while (pageCount < maximumPages && tools.Count < maximumToolDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageTask = listPageAsync(
                        new ListToolsRequestParams { Cursor = cursor },
                        cancellationToken)
                    .AsTask();
                ListToolsResult page;
                try
                {
                    page = await pageTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    CopilotCancellationBoundary.ObserveLateFault(pageTask);
                    throw;
                }
                pageCount++;
                var pageTools = page?.Tools ?? Array.Empty<Tool>();
                discoveredToolCount = discoveredToolCount > int.MaxValue - pageTools.Count
                    ? int.MaxValue
                    : discoveredToolCount + pageTools.Count;
                foreach (var tool in pageTools)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (tool == null)
                    {
                        rejectedToolCount++;
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(tool.Name)
                        || tool.Name.Length > MaximumRemoteToolNameLength
                        || tool.Name.Any(char.IsControl))
                    {
                        rejectedToolCount++;
                        continue;
                    }
                    if (seenToolNames.Contains(tool.Name))
                    {
                        duplicateToolCount++;
                        continue;
                    }
                    int definitionBytes;
                    try
                    {
                        definitionBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(tool));
                    }
                    catch
                    {
                        rejectedToolCount++;
                        continue;
                    }
                    if (definitionBytes > MaximumSerializedToolDefinitionBytes)
                    {
                        rejectedToolCount++;
                        continue;
                    }
                    if (serializedToolDefinitionBytes > MaximumTotalToolDefinitionBytes - definitionBytes)
                    {
                        rejectedToolCount++;
                        return new CopilotMcpToolDiscoveryBatch(
                            tools,
                            discoveredToolCount,
                            pageCount,
                            duplicateToolCount,
                            rejectedToolCount,
                            Truncated: true);
                    }
                    if (tools.Count >= maximumToolDefinitions)
                    {
                        return new CopilotMcpToolDiscoveryBatch(
                            tools,
                            discoveredToolCount,
                            pageCount,
                            duplicateToolCount,
                            rejectedToolCount,
                            Truncated: true);
                    }
                    seenToolNames.Add(tool.Name);
                    tools.Add(tool);
                    serializedToolDefinitionBytes += definitionBytes;
                }

                var nextCursor = page?.NextCursor;
                if (string.IsNullOrWhiteSpace(nextCursor))
                {
                    return new CopilotMcpToolDiscoveryBatch(
                        tools,
                        discoveredToolCount,
                        pageCount,
                        duplicateToolCount,
                        rejectedToolCount,
                        Truncated: false);
                }

                if (tools.Count >= maximumToolDefinitions)
                    return new CopilotMcpToolDiscoveryBatch(tools, discoveredToolCount, pageCount, duplicateToolCount, rejectedToolCount, Truncated: true);
                if (!seenCursors.Add(nextCursor))
                    throw new InvalidOperationException("External MCP server repeated a tools/list pagination cursor.");
                cursor = nextCursor;
            }

            return new CopilotMcpToolDiscoveryBatch(tools, discoveredToolCount, pageCount, duplicateToolCount, rejectedToolCount, Truncated: true);
        }
    }

}
