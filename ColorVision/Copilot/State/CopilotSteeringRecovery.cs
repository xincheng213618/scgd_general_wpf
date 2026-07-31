using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotSteeringRecovery
    {
        private const string RecoveryHeading = "以下运行中指令尚未送达，请检查后重新发送：";

        internal static bool RestoreToDraft(
            CopilotConversationRecord conversation,
            IEnumerable<string>? messages)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            var recoveryMessages = CopilotSteeringMessagePolicy.SelectForRecovery(messages);
            if (recoveryMessages.Count == 0)
                return false;

            var existingDraft = (conversation.DraftText ?? string.Empty).TrimEnd();
            var restoredDraft = string.IsNullOrWhiteSpace(existingDraft) && recoveryMessages.Count == 1
                ? recoveryMessages[0]
                : FormatRecoveryNotice(recoveryMessages);
            conversation.DraftText = string.IsNullOrWhiteSpace(existingDraft)
                ? restoredDraft
                : existingDraft + Environment.NewLine + Environment.NewLine + restoredDraft;
            return true;
        }

        internal static string FormatRecoveryNotice(IReadOnlyList<string> messages)
        {
            ArgumentNullException.ThrowIfNull(messages);
            if (messages.Count == 0)
                return string.Empty;

            return RecoveryHeading + Environment.NewLine + Environment.NewLine
                + string.Join(
                    Environment.NewLine + Environment.NewLine,
                    messages.Select((message, index) => $"{index + 1}. {message}"));
        }
    }
}
