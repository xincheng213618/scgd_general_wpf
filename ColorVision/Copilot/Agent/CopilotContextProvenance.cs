using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotContextSourceKind
    {
        ConversationHistory,
        ActiveGoal,
        UserQuestion,
        ApplicationContext,
        AttachmentContext,
        ProjectInstructions,
        ToolObservations,
        AnswerRequirements,
    }

    internal enum CopilotContextSourceForm
    {
        Instructions,
        Snapshot,
        Recall,
    }

    internal enum CopilotContextTrustClass
    {
        HostPolicy,
        UserInstruction,
        ScopedGuidance,
        UntrustedData,
        ConversationRecall,
    }

    internal readonly record struct CopilotContextProvenanceEntry(
        CopilotContextSourceKind Source,
        CopilotContextSourceForm Form,
        CopilotContextTrustClass Trust,
        int ItemCount,
        int CharacterCount);

    internal sealed class CopilotContextProvenanceSnapshot
    {
        public static CopilotContextProvenanceSnapshot Empty { get; } = new([]);

        public CopilotContextProvenanceSnapshot(IEnumerable<CopilotContextProvenanceEntry>? entries)
        {
            var snapshot = (entries ?? Array.Empty<CopilotContextProvenanceEntry>()).ToArray();
            if (snapshot.Any(entry => entry.ItemCount <= 0 || entry.CharacterCount < 0))
                throw new ArgumentOutOfRangeException(nameof(entries), "Context provenance counts must be non-negative and name at least one item.");
            if (snapshot.Select(entry => entry.Source).Distinct().Count() != snapshot.Length)
                throw new ArgumentException("Context provenance may name each source kind only once.", nameof(entries));
            Entries = Array.AsReadOnly(snapshot);
        }

        public IReadOnlyList<CopilotContextProvenanceEntry> Entries { get; }

        public string FormatDiagnostic()
        {
            if (Entries.Count == 0)
                return "User-role context provenance · no model-visible request context.";

            var builder = new StringBuilder("User-role context provenance");
            foreach (var entry in Entries)
            {
                builder.Append(" · ")
                    .Append(GetFormToken(entry.Form))
                    .Append('/')
                    .Append(GetSourceToken(entry.Source))
                    .Append('[')
                    .Append(GetTrustToken(entry.Trust))
                    .Append("]=")
                    .Append(entry.ItemCount)
                    .Append(" item(s)/")
                    .Append(entry.CharacterCount)
                    .Append(" char(s)");
            }
            builder.Append(". Metadata only; no prompt bodies, paths, or secrets are logged.");
            return builder.ToString();
        }

        private static string GetSourceToken(CopilotContextSourceKind source) => source switch
        {
            CopilotContextSourceKind.ConversationHistory => "conversation_history",
            CopilotContextSourceKind.ActiveGoal => "active_goal",
            CopilotContextSourceKind.UserQuestion => "user_question",
            CopilotContextSourceKind.ApplicationContext => "application_context",
            CopilotContextSourceKind.AttachmentContext => "attachment_context",
            CopilotContextSourceKind.ProjectInstructions => "project_instructions",
            CopilotContextSourceKind.ToolObservations => "tool_observations",
            CopilotContextSourceKind.AnswerRequirements => "answer_requirements",
            _ => "unknown",
        };

        private static string GetFormToken(CopilotContextSourceForm form) => form switch
        {
            CopilotContextSourceForm.Instructions => "instructions",
            CopilotContextSourceForm.Snapshot => "snapshot",
            CopilotContextSourceForm.Recall => "recall",
            _ => "opaque",
        };

        private static string GetTrustToken(CopilotContextTrustClass trust) => trust switch
        {
            CopilotContextTrustClass.HostPolicy => "host_policy",
            CopilotContextTrustClass.UserInstruction => "user_instruction",
            CopilotContextTrustClass.ScopedGuidance => "scoped_guidance",
            CopilotContextTrustClass.UntrustedData => "untrusted_data",
            CopilotContextTrustClass.ConversationRecall => "conversation_recall",
            _ => "unknown",
        };
    }
}
