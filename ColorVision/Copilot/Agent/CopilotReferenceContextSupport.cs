using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotReferenceContextSupport
    {
        public static bool HasReference(
            CopilotAgentRequest request,
            string sourcePrefix,
            string contextMarker)
        {
            ArgumentNullException.ThrowIfNull(request);
            return EnumerateReferenceContents(request, sourcePrefix, contextMarker).Any();
        }

        public static IEnumerable<string> EnumerateReferenceContents(
            CopilotAgentRequest request,
            string sourcePrefix,
            string contextMarker)
        {
            ArgumentNullException.ThrowIfNull(request);
            var normalizedPrefix = sourcePrefix ?? string.Empty;
            var normalizedMarker = contextMarker ?? string.Empty;

            foreach (var item in request.ContextItems)
            {
                if (item == null)
                    continue;
                if (MatchesApplicationContext(
                    item.Id,
                    item.Content,
                    normalizedPrefix,
                    normalizedMarker))
                {
                    yield return item.Content ?? string.Empty;
                }
            }

            foreach (var attachment in request.Attachments)
            {
                if (attachment == null
                    || attachment.Type != CopilotAttachmentType.Context
                    || string.IsNullOrWhiteSpace(normalizedPrefix)
                    || !(attachment.Source ?? string.Empty).StartsWith(
                        normalizedPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return attachment.Value ?? string.Empty;
            }
        }

        private static bool MatchesApplicationContext(
            string? sourceId,
            string? content,
            string sourcePrefix,
            string contextMarker)
        {
            return (!string.IsNullOrWhiteSpace(sourcePrefix)
                    && (sourceId ?? string.Empty).StartsWith(
                        sourcePrefix,
                        StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(contextMarker)
                    && (content ?? string.Empty).Contains(
                        contextMarker,
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}
