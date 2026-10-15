using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatService
    {
        private async Task<CopilotChatStreamResult> StreamCodexReplyAsync(CopilotProfileConfig profile,
            IReadOnlyList<CopilotRequestMessage> messages, IReadOnlyList<CopilotImagePayload> images,
            string? systemContext, Action<CopilotStreamDelta> onDelta, Action<CopilotTokenUsage>? onUsageChanged,
            CancellationToken cancellationToken)
        {
            var history = messages.Select(m => new Microsoft.Extensions.AI.ChatMessage(new ChatRole(m.Role), m.Content)).ToList();
            if (images.Count > 0 && history.Count > 0)
                foreach (var image in images)
                    history[^1].Contents.Add(new DataContent(Convert.FromBase64String(image.Base64Data), image.MediaType));
            var timeouts = CopilotProviderInactivityPolicy.Resolve(profile, _firstResponseTimeoutOverride, _streamingUpdateTimeoutOverride);
            using var client = new CopilotProviderInactivityChatClient(new CopilotCodexChatClient(profile), timeouts.FirstResponseTimeout, timeouts.StreamingUpdateTimeout);
            var usage = default(CopilotTokenUsage);
            await foreach (var update in client.GetStreamingResponseAsync(history,
                new ChatOptions { Instructions = profile.EffectiveSystemPrompt + "\n\n" + systemContext,
                    Reasoning = CopilotMicrosoftAgentFrameworkRuntime.BuildReasoningOptions(profile) }, cancellationToken).ConfigureAwait(false))
            {
                foreach (var content in update.Contents)
                {
                    if (content is TextContent text) onDelta(new CopilotStreamDelta(string.Empty, text.Text));
                    else if (content is TextReasoningContent reasoning) onDelta(new CopilotStreamDelta(reasoning.Text, string.Empty));
                    else if (content is UsageContent tokens)
                    {
                        static int Count(long? value) => (int)Math.Clamp(value ?? 0, 0, int.MaxValue);
                        usage = new CopilotTokenUsage(Count(tokens.Details.InputTokenCount), Count(tokens.Details.OutputTokenCount),
                            Count(tokens.Details.TotalTokenCount), tokens.Details.CachedInputTokenCount.HasValue ? Count(tokens.Details.CachedInputTokenCount) : null);
                        onUsageChanged?.Invoke(usage);
                    }
                }
            }
            return new CopilotChatStreamResult(usage, CopilotChatFinishKind.Complete, "stop")
            { ImagePreparationNotice = BuildImagePreparationNotice(images) };
        }
    }
}
