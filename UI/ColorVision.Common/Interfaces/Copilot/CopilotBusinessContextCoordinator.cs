using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.UI
{
    public sealed class CopilotBusinessContextBundle
    {
        public string SourceId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string AttachmentTitle { get; init; } = string.Empty;

        public IReadOnlyList<CopilotContextItem> Items { get; init; } = Array.Empty<CopilotContextItem>();

        public static CopilotBusinessContextBundle FromItem(string sourceId, CopilotContextItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return new CopilotBusinessContextBundle
            {
                SourceId = sourceId ?? string.Empty,
                Title = item.Title,
                Summary = item.Summary,
                AttachmentTitle = item.Title,
                Items = new[] { item },
            };
        }

        public static CopilotBusinessContextBundle FromLiveContext(CopilotLiveContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new CopilotBusinessContextBundle
            {
                SourceId = context.SourceId,
                Title = context.Title,
                Summary = context.Summary,
                AttachmentTitle = context.AttachmentTitle,
                Items = context.SnapshotItems,
            };
        }
    }

    public interface ICopilotBusinessContextSource
    {
        CopilotBusinessContextBundle CaptureCopilotContext();
    }

    public static class CopilotBusinessContextCoordinator
    {
        public static void Publish(CopilotBusinessContextBundle bundle)
        {
            Validate(bundle);
            CopilotLiveContextRegistry.Publish(new CopilotLiveContext
            {
                SourceId = bundle.SourceId,
                Title = bundle.Title,
                Summary = bundle.Summary,
                AttachmentTitle = string.IsNullOrWhiteSpace(bundle.AttachmentTitle) ? bundle.Title : bundle.AttachmentTitle,
                SnapshotItems = bundle.Items,
            });
        }

        public static CopilotPromptDispatchResult DispatchDiagnosis(
            CopilotBusinessContextBundle bundle,
            string prompt,
            bool startNewConversation = true,
            bool sendNow = true,
            ICopilotService? service = null)
        {
            Validate(bundle);
            Publish(bundle);
            var request = CopilotPromptRequestHelper.CreateRequest(new CopilotPromptRequestOptions
            {
                Prompt = prompt ?? string.Empty,
                Mode = CopilotPromptMode.Diagnose,
                StartNewConversation = startNewConversation,
                SendNow = sendNow,
                AttachContextSnapshot = true,
                ContextAttachmentTitle = string.IsNullOrWhiteSpace(bundle.AttachmentTitle) ? bundle.Title : bundle.AttachmentTitle,
                ContextAttachmentSourceId = bundle.SourceId,
                ContextItems = bundle.Items,
            });
            return CopilotPromptRequestHelper.Dispatch(request, service);
        }

        public static string BuildFlowDiagnosisPrompt(CopilotFlowContextSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var builder = new StringBuilder();
            builder.AppendLine("Return a structured ColorVision flow diagnosis with these sections:");
            builder.AppendLine("1. Current state and focused node");
            builder.AppendLine("2. Evidence from the attached snapshot and recent failure lines");
            builder.AppendLine("3. Ranked probable causes, each with confidence and supporting evidence");
            builder.AppendLine("4. Read-only verification steps in execution order");
            builder.AppendLine("5. Template adjustment candidates, naming exact fields only when supported by the snapshot");
            builder.AppendLine("6. Risks and operator confirmation required");
            builder.AppendLine();
            builder.AppendLine("Do not claim that the flow was rerun, a device was controlled, or a template was changed.");
            builder.AppendLine("This diagnosis turn is read-only: report template adjustment candidates for operator review, but do not create a template preview or alter the active template. Any template operation requires a separate explicit user request outside this diagnosis.");

            if (!string.IsNullOrWhiteSpace(snapshot.FocusedNodeSummary))
                builder.AppendLine($"Diagnosis focus: {snapshot.FocusedNodeSummary}");
            if (snapshot.FailureEvidence.Count > 0)
                builder.AppendLine($"Failure evidence lines available: {snapshot.FailureEvidence.Count}.");

            return builder.ToString().TrimEnd();
        }

        public static string BuildDeviceDiagnosisPrompt(CopilotDeviceContextSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return string.Join(Environment.NewLine, new[]
            {
                "Diagnose the attached ColorVision device/service snapshot.",
                "Separate observed facts from hypotheses, rank likely causes, and provide read-only verification steps first.",
                "Call out configuration fields that deserve review, but do not claim that configuration, service state, or hardware state was changed.",
                "Do not request or reveal passwords, tokens, serial numbers, licenses, or other sensitive fields.",
                $"Diagnosis focus: {FirstNonEmpty(snapshot.ServiceName, snapshot.ServiceCode, snapshot.ServiceType, "current device")}.",
            });
        }

        private static void Validate(CopilotBusinessContextBundle bundle)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            if (string.IsNullOrWhiteSpace(bundle.SourceId))
                throw new ArgumentException("A business context source id is required.", nameof(bundle));
            if (bundle.Items == null || bundle.Items.Count == 0 || bundle.Items.All(item => item == null))
                throw new ArgumentException("At least one business context item is required.", nameof(bundle));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
