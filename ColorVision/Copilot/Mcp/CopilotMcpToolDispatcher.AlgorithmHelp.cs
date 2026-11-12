using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> ReadAlgorithmHelpAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var topic = arguments!["topic_id"].GetString()!;
        if (topic.EndsWith('\n'))
            return CopilotMcpToolCallResult.Fail("invalid_arguments", "topic_id must be the exact algorithm help identifier without a trailing line feed.", CopilotToolFailureKind.Validation);
        var cursor = arguments.TryGetValue("cursor", out var continuation) ? continuation.GetString() : null;
        var maximum = arguments.TryGetValue("max_characters", out var count) ? count.GetInt32() : 6000;
        var result = await _environment.AlgorithmHelpReader(topic, cursor, maximum, cancellationToken);
        return ToMcpResult(result, "algorithm_help_read_failed", contentOnly: true);
    }
}
