using ColorVision.Copilot;
using System.Collections.Generic;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotContextProvenanceTests
{
    [Fact]
    public void PreparedPromptDerivesOrderedMetadataFromActualUserRoleComposition()
    {
        const string sensitive = "TOP_SECRET_PROMPT_BODY_9281";
        var request = new CopilotAgentRequest
        {
            UserText = $"Explain {sensitive}",
            ActiveGoalText = $"Finish {sensitive}",
            History = [new CopilotRequestMessage("assistant", $"Earlier {sensitive}")],
            ContextItems =
            [
                new CopilotContextItem { Title = "Live state", Content = sensitive },
                new CopilotContextItem(),
            ],
            Attachments =
            [
                new CopilotAttachmentItem
                {
                    Type = CopilotAttachmentType.Context,
                    Title = "Selection",
                    Value = sensitive,
                },
                new CopilotAttachmentItem
                {
                    Type = CopilotAttachmentType.File,
                    Value = $@"C:\{sensitive}\source.cs",
                },
            ],
            ProjectInstructions =
            [
                new CopilotProjectInstructionDocument
                {
                    Path = $@"C:\workspace\{sensitive}\AGENTS.md",
                    Content = sensitive,
                },
            ],
            CodexCustomSubagents =
            [
                new CopilotCodexCustomSubagentDefinition
                {
                    Name = "scout",
                    Description = sensitive,
                },
            ],
        };

        var prepared = new CopilotAgentContextBuilder().BuildAnswerMessages(
            request,
            Array.Empty<CopilotAgentStepRecord>());
        var entries = prepared.ContextProvenance.Entries;

        Assert.Equal(
            [
                CopilotContextSourceKind.ConversationHistory,
                CopilotContextSourceKind.ActiveGoal,
                CopilotContextSourceKind.UserQuestion,
                CopilotContextSourceKind.CustomSubagentCatalog,
                CopilotContextSourceKind.ApplicationContext,
                CopilotContextSourceKind.AttachmentContext,
                CopilotContextSourceKind.ProjectInstructions,
                CopilotContextSourceKind.AnswerRequirements,
            ],
            entries.Select(entry => entry.Source));
        Assert.Equal(CopilotContextSourceForm.Recall, entries[0].Form);
        Assert.Equal(CopilotContextTrustClass.ConversationRecall, entries[0].Trust);
        Assert.Equal(CopilotContextTrustClass.UserInstruction, entries[1].Trust);
        Assert.Equal(CopilotContextSourceForm.Catalog, entries[3].Form);
        Assert.Equal(CopilotContextTrustClass.TrustedConfiguration, entries[3].Trust);
        Assert.Equal(CopilotContextTrustClass.UntrustedData, entries[4].Trust);
        Assert.Equal(CopilotContextTrustClass.ScopedGuidance, entries[6].Trust);
        Assert.Equal(CopilotContextTrustClass.HostPolicy, entries[7].Trust);
        Assert.All(entries, entry =>
        {
            Assert.True(entry.ItemCount > 0);
            Assert.True(entry.CharacterCount > 0);
        });

        var diagnostic = prepared.ContextProvenance.FormatDiagnostic();
        Assert.Contains("recall/conversation_history[conversation_recall]", diagnostic, StringComparison.Ordinal);
        Assert.Contains("instructions/project_instructions[scoped_guidance]", diagnostic, StringComparison.Ordinal);
        Assert.Contains("Metadata only", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitive, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("AGENTS.md", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProvenanceReportsRetainedContextRatherThanRawCandidateCounts()
    {
        var request = new CopilotAgentRequest
        {
            UserText = "Summarize the bounded context.",
            ContextItems = Enumerable.Range(1, 30)
                .Select(index => new CopilotContextItem
                {
                    Title = $"Item {index}",
                    Content = "value",
                })
                .ToArray(),
            Attachments =
            [
                new CopilotAttachmentItem
                {
                    Type = CopilotAttachmentType.WebPage,
                    Source = "https://example.test/page",
                    Value = "page",
                },
                new CopilotAttachmentItem
                {
                    Type = CopilotAttachmentType.File,
                    Value = @"C:\workspace\source.cs",
                },
            ],
        };

        var prepared = new CopilotAgentContextBuilder().BuildAnswerMessages(
            request,
            Array.Empty<CopilotAgentStepRecord>());
        var entries = prepared.ContextProvenance.Entries.ToDictionary(entry => entry.Source);

        Assert.Equal(24, entries[CopilotContextSourceKind.ApplicationContext].ItemCount);
        Assert.Equal(1, entries[CopilotContextSourceKind.AttachmentContext].ItemCount);
    }

    [Fact]
    public void HarnessDeduplicationIsReflectedInProvenance()
    {
        var request = new CopilotAgentRequest { UserText = "Answer." };
        var builder = new CopilotAgentContextBuilder();

        var full = builder.BuildAnswerMessages(request, Array.Empty<CopilotAgentStepRecord>());
        var harness = builder.BuildHarnessMessages(
            request,
            Array.Empty<CopilotAgentStepRecord>(),
            minimalDelegatedFinalization: false);

        Assert.Contains(
            full.ContextProvenance.Entries,
            entry => entry.Source == CopilotContextSourceKind.AnswerRequirements);
        Assert.DoesNotContain(
            harness.ContextProvenance.Entries,
            entry => entry.Source == CopilotContextSourceKind.AnswerRequirements);
    }

    [Fact]
    public void ToolObservationMetadataIsDerivedWithoutLoggingObservationContent()
    {
        const string sensitive = "PRIVATE_TOOL_RESULT_2718";
        var prepared = new CopilotAgentContextBuilder().BuildAnswerMessages(
            new CopilotAgentRequest { UserText = "Use the observation." },
            [
                new CopilotAgentStepRecord
                {
                    Round = 1,
                    ToolCall = new CopilotToolCall { ToolName = "ReadLocalFile" },
                    Observation = new CopilotToolObservation
                    {
                        Success = true,
                        Summary = sensitive,
                        Content = sensitive,
                    },
                },
            ]);

        var observation = Assert.Single(
            prepared.ContextProvenance.Entries,
            entry => entry.Source == CopilotContextSourceKind.ToolObservations);
        Assert.Equal(CopilotContextSourceForm.Snapshot, observation.Form);
        Assert.Equal(CopilotContextTrustClass.UntrustedData, observation.Trust);
        Assert.Equal(1, observation.ItemCount);
        Assert.DoesNotContain(
            sensitive,
            prepared.ContextProvenance.FormatDiagnostic(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotRejectsAmbiguousSourcesAndExposesReadOnlyEntries()
    {
        var entry = new CopilotContextProvenanceEntry(
            CopilotContextSourceKind.UserQuestion,
            CopilotContextSourceForm.Instructions,
            CopilotContextTrustClass.UserInstruction,
            1,
            10);

        Assert.Throws<ArgumentException>(() => new CopilotContextProvenanceSnapshot([entry, entry]));
        var snapshot = new CopilotContextProvenanceSnapshot([entry]);
        var collection = Assert.IsAssignableFrom<ICollection<CopilotContextProvenanceEntry>>(snapshot.Entries);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(entry));
    }
}
