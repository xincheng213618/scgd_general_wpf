using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;

namespace ColorVision.Copilot
{
    public sealed class CopilotComposerStash
    {
        public string Text { get; set; } = string.Empty;

        public int CaretIndex { get; set; }

        public CopilotAgentMode RequestMode { get; set; } = CopilotAgentMode.Auto;

        public ObservableCollection<CopilotAttachmentItem> Attachments { get; set; } = new();

        [JsonIgnore]
        public bool HasContent => !string.IsNullOrEmpty(Text) || Attachments?.Count > 0;

        public bool ShouldSerializeText() => !string.IsNullOrEmpty(Text);

        public bool ShouldSerializeCaretIndex() => CaretIndex > 0;

        public bool ShouldSerializeRequestMode() => RequestMode != CopilotAgentMode.Auto;

        public bool ShouldSerializeAttachments() => Attachments?.Count > 0;

        internal static CopilotComposerStash Capture(
            string? text,
            int caretIndex,
            CopilotAgentMode requestMode,
            IEnumerable<CopilotAttachmentItem>? attachments)
        {
            var normalizedText = CopilotComposerTextLimits.Bound(text);
            return new CopilotComposerStash
            {
                Text = normalizedText,
                CaretIndex = Math.Clamp(caretIndex, 0, normalizedText.Length),
                RequestMode = Enum.IsDefined(requestMode) ? requestMode : CopilotAgentMode.Auto,
                Attachments = new ObservableCollection<CopilotAttachmentItem>(
                    (attachments ?? Array.Empty<CopilotAttachmentItem>())
                        .Where(attachment => attachment != null)
                        .Select(attachment => attachment.CreateSnapshot())),
            };
        }

        internal bool EnsureValid()
        {
            var changed = false;
            var normalizedText = CopilotComposerTextLimits.Bound(Text);
            if (!string.Equals(Text, normalizedText, StringComparison.Ordinal))
            {
                Text = normalizedText;
                changed = true;
            }

            var normalizedCaretIndex = Math.Clamp(CaretIndex, 0, normalizedText.Length);
            if (CaretIndex != normalizedCaretIndex)
            {
                CaretIndex = normalizedCaretIndex;
                changed = true;
            }

            if (!Enum.IsDefined(RequestMode))
            {
                RequestMode = CopilotAgentMode.Auto;
                changed = true;
            }

            if (Attachments == null)
            {
                Attachments = new ObservableCollection<CopilotAttachmentItem>();
                changed = true;
            }
            for (var index = Attachments.Count - 1; index >= 0; index--)
            {
                var attachment = Attachments[index];
                if (attachment == null)
                {
                    Attachments.RemoveAt(index);
                    changed = true;
                    continue;
                }

                changed |= attachment.EnsureValid();
            }

            return changed;
        }

        internal IReadOnlyList<CopilotAttachmentItem> CreateAttachmentSnapshots()
        {
            return (Attachments ?? new ObservableCollection<CopilotAttachmentItem>())
                .Where(attachment => attachment != null)
                .Select(attachment => attachment.CreateSnapshot())
                .ToArray();
        }

    }
}
