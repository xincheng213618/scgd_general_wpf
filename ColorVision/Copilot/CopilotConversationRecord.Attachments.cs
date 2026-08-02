using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotConversationRecord
    {
        internal IEnumerable<CopilotAttachmentItem> EnumerateReferencedAttachments()
        {
            foreach (var attachment in Attachments?.Where(attachment => attachment != null) ?? Enumerable.Empty<CopilotAttachmentItem>())
                yield return attachment;

            foreach (var attachment in ComposerStash?.Attachments?.Where(attachment => attachment != null) ?? Enumerable.Empty<CopilotAttachmentItem>())
                yield return attachment;

            foreach (var message in Messages?.Where(message => message != null) ?? Enumerable.Empty<CopilotChatMessage>())
            {
                foreach (var attachment in message.Attachments?.Where(attachment => attachment != null) ?? Enumerable.Empty<CopilotAttachmentItem>())
                    yield return attachment;
            }
        }
    }
}
