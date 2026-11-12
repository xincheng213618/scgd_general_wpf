using ColorVision.Copilot.Mcp;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotTemplatePatchReviewTests
{
    public static IEnumerable<object[]> CompleteValueCases()
    {
        var prefix = new string('x', 300);
        yield return [JsonSerializer.Serialize(new { Value = prefix + "-old" }), JsonSerializer.Serialize(new { Value = prefix + "-new" }), 1];
        yield return [JsonSerializer.Serialize(new { Value = Enumerable.Range(0, 100).Append(1) }), JsonSerializer.Serialize(new { Value = Enumerable.Range(0, 100).Append(2) }), 1];
        yield return [JsonSerializer.Serialize(new { Value = prefix }), JsonSerializer.Serialize(new { Value = prefix }), 0];
        yield return ["{\"Value\": {\"A\": 1, \"B\": [2,3]}}", "{\"Value\":{\"B\":[2,3],\"A\":1}}", 0];
        yield return ["{}", "{\"Value\":null}", 1];
        yield return ["{\"Value\":null}", "{\"Value\":null}", 0];
    }

    [Theory]
    [MemberData(nameof(CompleteValueCases))]
    public void PreviewCountsCompleteValuesRatherThanTheirDisplaySummaries(string currentJson, string proposedJson, int expectedChanges)
    {
        var (_, preview, result) = CreatePreview(currentJson, proposedJson);

        Assert.Equal(expectedChanges, preview.Changes.Count);
        Assert.Contains($"Changed key fields: {expectedChanges}", result.Text, StringComparison.Ordinal);
        using var proposed = JsonDocument.Parse(proposedJson);
        using var patched = JsonDocument.Parse(preview.PatchedJson);
        Assert.True(JsonElement.DeepEquals(proposed.RootElement.GetProperty("Value"), patched.RootElement.GetProperty("Value")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollidingDisplaySummariesExplicitlyDiscloseTheirLimit(bool redacted)
    {
        var prefix = redacted ? "token=" : new string('x', 300);
        var (_, preview, result) = CreatePreview(
            JsonSerializer.Serialize(new { Value = prefix + "old" }),
            JsonSerializer.Serialize(new { Value = prefix + "new" }));

        var change = Assert.Single(preview.Changes);
        Assert.Contains(redacted ? "redacted" : "truncated", change, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("differ", change, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Changed key fields: 1", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewRejectsReplacingAParentThatContainsExistingSensitiveFields()
    {
        const string replacedSecret = "CHANGED-PARENT-OLD-SECRET";
        const string untouchedSecret = "UNTOUCHED-SECRET";
        var dispatcher = CreateDispatcher(JsonSerializer.Serialize(new
        {
            Settings = new { ApiKey = replacedSecret, Value = 1 },
            Other = new { ApiKey = untouchedSecret },
        }));
        var result = dispatcher.PreviewTemplatePatch(new Dictionary<string, JsonElement>
        {
            ["template_identifier"] = JsonSerializer.SerializeToElement("review-template"),
            ["proposed_changes"] = JsonSerializer.SerializeToElement(new { Settings = new { Value = 2 } }),
        });

        Assert.False(result.Success);
        Assert.Equal("sensitive_template_field_not_allowed", result.ErrorCode);
        Assert.DoesNotContain("preview_id:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(replacedSecret, result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(untouchedSecret, result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalApprovalPreservesEveryChangedFieldBeyondTheSummaryLimit()
    {
        var current = Enumerable.Range(0, 81).ToDictionary(index => $"Field{index:D3}", _ => 0);
        var proposed = Enumerable.Range(0, 81).ToDictionary(index => $"Field{index:D3}", _ => 1);
        var (dispatcher, preview, result) = CreatePreview(JsonSerializer.Serialize(current), JsonSerializer.Serialize(proposed));
        Assert.Contains("Changed key fields: 81", result.Text, StringComparison.Ordinal);
        Assert.Contains("omitted", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("- Field080:", result.Text, StringComparison.Ordinal);

        var apply = await dispatcher.CallAsync("apply_template_patch", ApplyArguments(preview.PreviewId), CancellationToken.None);
        var action = GetAction(apply);
        try
        {
            Assert.Contains("omitted", apply.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Field080", action.ReviewDetails, StringComparison.Ordinal);
            Assert.True(action.HasReviewDetails);
            Assert.DoesNotContain("Field080", action.ArgumentsSummary, StringComparison.Ordinal);
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "Template patch review test cleanup.");
        }
        Assert.Empty(action.ReviewDetails);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothApprovalSurfacesExposeCompleteChangedValuesWithoutUnchangedTemplateData(bool native)
    {
        const string oldTail = "OLD-REVIEW-TAIL";
        const string newTail = "NEW-REVIEW-TAIL";
        const string untouched = "UNCHANGED-TEMPLATE-DATA";
        var prefix = new string('x', 1800);
        var (dispatcher, preview, _) = CreatePreview(
            "{\"Value\":\"" + prefix + oldTail + "\u202E\",\"Untouched\":{\"ApiKey\":\"" + untouched + "\"}}",
            "{\"Value\":\"" + prefix + newTail + "\u202E\"}");
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        CopilotFrameworkApprovalHandle? handle = null;
        ConfirmableAction action;
        if (native)
        {
            handle = coordinator.RequestApproval(
                new CopilotApplyTemplatePatchTool(),
                CreateRequest(),
                ApplyInput(preview.PreviewId),
                $"template-review-{Guid.NewGuid():N}",
                CancellationToken.None);
            action = handle.Action;
        }
        else
        {
            action = GetAction(await dispatcher.CallAsync("apply_template_patch", ApplyArguments(preview.PreviewId), CancellationToken.None));
        }

        try
        {
            Assert.True(action.HasReviewDetails);
            Assert.Contains(oldTail, action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains(newTail, action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains(@"\u202E", action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("\u202E", action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain(untouched, action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain(newTail, action.ArgumentsSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(newTail, action.ConfirmActionPayloadJson, StringComparison.Ordinal);
        }
        finally
        {
            if (handle != null)
            {
                coordinator.Cancel(handle);
                var decision = await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.Equal(CopilotFrameworkApprovalDecisionKind.Cancelled, decision.Kind);
            }
            else
            {
                CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "Template patch review test cleanup.");
            }
        }
        Assert.Empty(action.ReviewDetails);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedCompleteReviewIsRejectedWithoutCreatingPendingAction(bool native)
    {
        var valueLength = CopilotMcpConfirmationStore.MaximumReviewDetailsCharacters / 4;
        var (dispatcher, preview, _) = CreatePreview(
            JsonSerializer.Serialize(new { Value = new string('\\', valueLength) }),
            JsonSerializer.Serialize(new { Value = new string('b', valueLength) }));
        var before = CopilotMcpConfirmationStore.Instance.GetPendingActions().Select(action => action.ActionId).ToHashSet(StringComparer.Ordinal);

        if (native)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new CopilotFrameworkApprovalCoordinator().RequestApproval(
                new CopilotApplyTemplatePatchTool(),
                CreateRequest(),
                ApplyInput(preview.PreviewId),
                $"template-review-{Guid.NewGuid():N}",
                CancellationToken.None));
            Assert.Contains(CopilotMcpConfirmationStore.MaximumReviewDetailsCharacters.ToString(), exception.Message, StringComparison.Ordinal);
        }
        else
        {
            var result = await dispatcher.CallAsync("apply_template_patch", ApplyArguments(preview.PreviewId), CancellationToken.None);
            Assert.False(result.Success);
            Assert.False(result.RequiresApproval);
            Assert.Equal("template_patch_review_too_large", result.ErrorCode);
            Assert.Contains(CopilotMcpConfirmationStore.MaximumReviewDetailsCharacters.ToString(), result.Text, StringComparison.Ordinal);
        }

        Assert.All(CopilotMcpConfirmationStore.Instance.GetPendingActions(), action => Assert.Contains(action.ActionId, before));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeApprovalRejectsMissingOrExpiredPreviewBeforeCreatingAction(bool expired)
    {
        var previewId = "missing-template-preview-" + Guid.NewGuid().ToString("N");
        if (expired)
        {
            var store = CopilotMcpTemplatePatchPreviewStore.Instance;
            var previousLifetime = store.PreviewLifetime;
            try
            {
                store.PreviewLifetime = TimeSpan.FromTicks(-1);
                var (_, preview, _) = CreatePreview("{\"Value\":1}", "{\"Value\":2}", readStoredPreview: false);
                previewId = preview.PreviewId;
            }
            finally
            {
                store.PreviewLifetime = previousLifetime;
            }
        }

        var before = CopilotMcpConfirmationStore.Instance.GetPendingActions().Select(action => action.ActionId).ToHashSet(StringComparer.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new CopilotFrameworkApprovalCoordinator().RequestApproval(
            new CopilotApplyTemplatePatchTool(),
            CreateRequest(),
            ApplyInput(previewId),
            $"template-review-{Guid.NewGuid():N}",
            CancellationToken.None));
        Assert.All(CopilotMcpConfirmationStore.Instance.GetPendingActions(), action => Assert.Contains(action.ActionId, before));
    }

    private static (CopilotMcpToolDispatcher Dispatcher, CopilotMcpTemplatePatchPreview Preview, CopilotMcpToolCallResult Result) CreatePreview(
        string currentJson,
        string proposedJson,
        bool readStoredPreview = true)
    {
        var dispatcher = CreateDispatcher(currentJson);
        using var proposed = JsonDocument.Parse(proposedJson);
        var result = dispatcher.PreviewTemplatePatch(new Dictionary<string, JsonElement>
        {
            ["template_identifier"] = JsonSerializer.SerializeToElement("review-template"),
            ["proposed_changes"] = proposed.RootElement.Clone(),
        });
        Assert.True(result.Success, result.Text);
        const string prefix = "preview_id: ";
        var previewId = Assert.Single(result.Text.Split('\n'), line => line.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..].Trim();
        if (!readStoredPreview)
            return (dispatcher, new CopilotMcpTemplatePatchPreview { PreviewId = previewId }, result);
        Assert.True(CopilotMcpTemplatePatchPreviewStore.Instance.TryGet(previewId, out var preview, out var error), error);
        return (dispatcher, preview, result);
    }

    private static CopilotMcpToolDispatcher CreateDispatcher(string currentJson)
    {
        var sourceId = "template-json-editor:review-" + Guid.NewGuid().ToString("N");
        return new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot { SolutionDirectoryPath = Path.GetFullPath(Path.GetTempPath()) },
            LiveContextProvider = () => new CopilotLiveContext
            {
                SourceId = sourceId,
                SnapshotItems = [new CopilotContextItem { Content = "```json\n" + currentJson + "\n```" }],
            },
        });
    }

    private static Dictionary<string, JsonElement> ApplyArguments(string previewId) => new()
    {
        ["preview_id"] = JsonSerializer.SerializeToElement(previewId),
    };

    private static CopilotAgentToolInput ApplyInput(string previewId) => new()
    {
        Arguments = new Dictionary<string, object?> { ["preview_id"] = previewId },
    };

    private static ConfirmableAction GetAction(CopilotMcpToolCallResult result)
    {
        Assert.True(result.RequiresApproval, result.Text);
        return Assert.Single(CopilotMcpConfirmationStore.Instance.GetPendingActions(), action => action.ActionId == result.ApprovalActionId);
    }

    private static CopilotAgentRequest CreateRequest() => new()
    {
        ConversationId = "template-review-conversation",
        TaskId = "template-review-task-" + Guid.NewGuid().ToString("N"),
        WorkspacePath = Path.GetFullPath(Path.GetTempPath()),
        UserText = "Apply the previewed template changes.",
    };
}
