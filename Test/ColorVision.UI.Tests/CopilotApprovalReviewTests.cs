using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class CopilotApprovalReviewTestGroup
{
    public const string CollectionName = "Copilot approval review UI";
}

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotApprovalReviewTests
{
    [Fact]
    public async Task ShellApprovalPreservesCompleteCommandForHumanReview()
    {
        var command = "Write-Output '" + new string('x', 1800) + "-human-review-tail'";
        var input = CreateShellInput(command);
        var request = CreateRequest();
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = coordinator.RequestApproval(
            new CopilotShellCommandTool(),
            request,
            input,
            $"call-{Guid.NewGuid():N}",
            CancellationToken.None);

        try
        {
            Assert.True(handle.Action.HasReviewDetails);
            Assert.Contains("Complete command (review-escaped):" + Environment.NewLine + command, handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains("-human-review-tail", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("-human-review-tail", handle.Action.ArgumentsSummary, StringComparison.Ordinal);
            Assert.DoesNotContain("-human-review-tail", handle.Action.ConfirmActionPayloadJson, StringComparison.Ordinal);
            Assert.Equal(64, handle.Action.ArgumentsDigest.Length);
        }
        finally
        {
            coordinator.Cancel(handle);
            var decision = await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(CopilotFrameworkApprovalDecisionKind.Cancelled, decision.Kind);
            Assert.Empty(handle.Action.ReviewDetails);
        }
    }

    [Fact]
    public void ShellApprovalsWithSameVisiblePrefixExposeDifferentReviewTails()
    {
        var prefix = "Write-Output '" + new string('p', 1800);
        var firstInput = CreateShellInput(prefix + "-first-tail'");
        var secondInput = CreateShellInput(prefix + "-second-tail'");
        var request = CreateRequest();
        var first = CopilotShellCommandService.CreateApprovalPresentation(request, firstInput);
        var second = CopilotShellCommandService.CreateApprovalPresentation(request, secondInput);

        Assert.Equal(
            CopilotToolApprovalArgumentFormatter.Create(firstInput),
            CopilotToolApprovalArgumentFormatter.Create(secondInput));
        Assert.Contains("-first-tail", first.ReviewDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("-second-tail", first.ReviewDetails, StringComparison.Ordinal);
        Assert.Contains("-second-tail", second.ReviewDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("-first-tail", second.ReviewDetails, StringComparison.Ordinal);
    }

    [Fact]
    public void MaximumLengthShellCommandIsFullyReviewable()
    {
        const string sentinel = "MAXIMUM-COMMAND-TAIL";
        var command = new string('x', CopilotShellCommandService.MaximumCommandCharacters - sentinel.Length) + sentinel;
        var presentation = CopilotShellCommandService.CreateApprovalPresentation(
            CreateRequest(),
            CreateShellInput(command));

        Assert.EndsWith(command, presentation.ReviewDetails, StringComparison.Ordinal);
        Assert.Contains(
            $"Command characters: {CopilotShellCommandService.MaximumCommandCharacters}",
            presentation.ReviewDetails,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<truncated>", presentation.ReviewDetails, StringComparison.Ordinal);
        Assert.True(presentation.ReviewDetails.Length < CopilotMcpConfirmationStore.MaximumReviewDetailsCharacters);
    }

    [Fact]
    public void ShellApprovalEscapesInvisibleUnicodeWithoutChangingTheBoundCommand()
    {
        var command = @"Write-Output 'literal\u202E safe" + "\u202Ecod.exe\u200B\u00A0tail'";
        var presentation = CopilotShellCommandService.CreateApprovalPresentation(
            CreateRequest(),
            CreateShellInput(command));

        Assert.Contains(@"literal\\u202E safe\u202Ecod.exe\u200B\u00A0tail", presentation.ReviewDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", presentation.ReviewDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("\u200B", presentation.ReviewDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("\u00A0", presentation.ReviewDetails, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellApprovalEscapesSecuritySensitivePathAndTaskMetadata()
    {
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "ColorVisionApprovalReview-" + Guid.NewGuid().ToString("N") + "\u202Ecod.exe\u200B");
        Directory.CreateDirectory(workingDirectory);
        try
        {
            var request = CreateRequest(
                workingDirectory,
                "Inspect \u202Ecod.exe\u200B");
            var presentation = CopilotShellCommandService.CreateApprovalPresentation(
                request,
                CreateShellInput("Write-Output safe", workingDirectory));
            var context = CopilotConfirmationRequestContext.ForAgent(request, presentation);

            Assert.Contains(@"Working directory: ", presentation.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains(@"\u202Ecod.exe\u200B", presentation.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("\u202E", presentation.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("\u200B", presentation.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains(@"\u202Ecod.exe\u200B", context.WorkspaceLabel, StringComparison.Ordinal);
            Assert.Contains(@"Inspect \u202Ecod.exe\u200B", context.TaskScopeLabel, StringComparison.Ordinal);
            Assert.DoesNotContain("\u202E", context.WorkspaceLabel, StringComparison.Ordinal);
            Assert.DoesNotContain("\u200B", context.TaskScopeLabel, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task MaximumLengthInvisibleShellCommandRemainsReviewable()
    {
        var command = new string('\u200B', CopilotShellCommandService.MaximumCommandCharacters);
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = coordinator.RequestApproval(
            new CopilotShellCommandTool(),
            CreateRequest(),
            CreateShellInput(command),
            $"call-{Guid.NewGuid():N}",
            CancellationToken.None);

        try
        {
            Assert.DoesNotContain("\u200B", handle.Action.ReviewDetails, StringComparison.Ordinal);
            Assert.Equal(
                CopilotShellCommandService.MaximumCommandCharacters,
                (handle.Action.ReviewDetails.Length
                    - handle.Action.ReviewDetails.Replace(@"\u200B", string.Empty, StringComparison.Ordinal).Length)
                    / @"\u200B".Length);
            Assert.True(handle.Action.ReviewDetails.Length < CopilotMcpConfirmationStore.MaximumReviewDetailsCharacters);
        }
        finally
        {
            coordinator.Cancel(handle);
            var decision = await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(CopilotFrameworkApprovalDecisionKind.Cancelled, decision.Kind);
        }
    }

    [Fact]
    public async Task WorkspaceChangeSetApprovalPreservesEveryPathAndHashForHumanReview()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "ColorVisionApprovalReview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspacePath);
        try
        {
            var sentinel = "final-human-review-target.txt";
            var operations = Enumerable.Range(0, 8)
                .Select(index => new
                {
                    operation = "add",
                    path = index == 7
                        ? "final\u202Ecod.exe-" + sentinel
                        : new string((char)('a' + index), 120) + "-" + $"{index}.txt",
                    content = $"content-{index}",
                })
                .ToArray();
            var request = new CopilotAgentRequest
            {
                ConversationId = "workspace-approval-review-conversation",
                TaskId = "workspace-approval-review-task",
                WorkspacePath = workspacePath,
                UserText = "Create the requested workspace files.",
                TaskIntentText = "Create and verify the requested workspace files.",
                SearchRootPaths = [workspacePath],
                WritableLocalRootPaths = [workspacePath],
            };
            var store = new CopilotWorkspacePatchStore();
            var preview = await store.PreviewPatchEnvelopeAsync(
                request,
                new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["operations"] = operations,
                    },
                },
                CancellationToken.None);

            Assert.True(preview.Success, preview.ErrorMessage);
            var changeSetId = preview.Content
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("change_set_id:", StringComparison.Ordinal))
                ["change_set_id:".Length..]
                .Trim();
            var presentation = store.CreateChangeSetApprovalPresentation(
                new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["changeSetId"] = changeSetId,
                    },
                },
                rollback: false);

            Assert.Contains(sentinel, presentation.ReviewDetails, StringComparison.Ordinal);
            Assert.Contains(@"final\u202Ecod.exe-" + sentinel, presentation.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain("\u202E", presentation.ReviewDetails, StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, presentation.Description, StringComparison.Ordinal);
            Assert.Equal(8, presentation.ReviewDetails.Split("SHA-256:", StringSplitOptions.None).Length - 1);
            Assert.True(presentation.ReviewDetails.Length > 1000);
            Assert.True(presentation.ReviewDetails.Length < CopilotMcpConfirmationStore.MaximumReviewDetailsCharacters);
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public void ReviewWindowUsesReadOnlyScrollableDetailsAndRequiresAcknowledgement()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            ConfirmableAction? action = null;
            CopilotActionReviewWindow? window = null;
            try
            {
                var reviewDetails = "Complete command:\nWrite-Output '" + new string('r', 1800) + "-window-tail'";
                action = CopilotMcpConfirmationStore.Instance.Create(
                    "Run PowerShell command",
                    "Review the complete command before approving.",
                    "confirmation-required",
                    "RunShellCommand",
                    "{\"command\":\"<truncated>\"}",
                    _ => Task.FromResult(CopilotMcpToolCallResult.Ok("not executed")),
                    exactArgumentsBinding: reviewDetails,
                    reviewDetails: reviewDetails);
                window = new CopilotActionReviewWindow(action);
                var detailsTextBox = Assert.IsType<TextBox>(window.FindName("ReviewDetailsTextBox"));
                var acknowledgement = Assert.IsType<CheckBox>(window.FindName("ReviewAcknowledgementCheckBox"));
                var approveButton = Assert.IsType<Button>(window.FindName("ApproveButton"));

                detailsTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                Assert.True(detailsTextBox.IsReadOnly);
                Assert.Equal(reviewDetails, detailsTextBox.Text);
                Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(detailsTextBox));
                Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetVerticalScrollBarVisibility(detailsTextBox));
                Assert.False(approveButton.IsEnabled);

                acknowledgement.IsChecked = true;
                Assert.True(approveButton.IsEnabled);
                acknowledgement.IsChecked = false;
                Assert.False(approveButton.IsEnabled);

                Assert.True(CopilotMcpConfirmationStore.Instance.Cancel(
                    action.ActionId,
                    out _,
                    "UI contract test cancellation."));
                Assert.False(approveButton.IsEnabled);
                Assert.False(acknowledgement.IsEnabled);
                Assert.Empty(action.ReviewDetails);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                window?.Close();
                if (action != null)
                    CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "UI contract test cleanup.");
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The STA approval-window contract test did not finish.");

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static CopilotAgentRequest CreateRequest(
        string? workspacePath = null,
        string taskIntentText = "Run and verify the requested shell command.")
    {
        workspacePath = Path.GetFullPath(workspacePath ?? Path.GetTempPath());
        return new CopilotAgentRequest
        {
            ConversationId = "approval-review-conversation",
            TaskId = "approval-review-task",
            WorkspacePath = workspacePath,
            UserText = "Run the requested shell command.",
            TaskIntentText = taskIntentText,
            SearchRootPaths = [workspacePath],
            WritableLocalRootPaths = [workspacePath],
            PreferredShell = CopilotShellKind.PowerShell,
        };
    }

    private static CopilotAgentToolInput CreateShellInput(string command, string? workingDirectory = null)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["command"] = command,
                ["shell"] = "powershell",
                ["workingDirectory"] = Path.GetFullPath(workingDirectory ?? Path.GetTempPath()),
                ["timeoutSeconds"] = 60,
            },
        };
    }
}
