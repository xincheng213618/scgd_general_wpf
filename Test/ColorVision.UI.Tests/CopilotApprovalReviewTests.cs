using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

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
            Assert.Contains("Complete command:" + Environment.NewLine + command, handle.Action.ReviewDetails, StringComparison.Ordinal);
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
    public void ReviewWindowUsesReadOnlyScrollableDetailsAndRequiresAcknowledgement()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            ConfirmableAction? action = null;
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
                var window = new CopilotActionReviewWindow(action);
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
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                if (action != null)
                    CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "UI contract test cleanup.");
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static CopilotAgentRequest CreateRequest()
    {
        var workspacePath = Path.GetFullPath(Path.GetTempPath());
        return new CopilotAgentRequest
        {
            ConversationId = "approval-review-conversation",
            TaskId = "approval-review-task",
            WorkspacePath = workspacePath,
            UserText = "Run the requested shell command.",
            TaskIntentText = "Run and verify the requested shell command.",
            SearchRootPaths = [workspacePath],
            WritableLocalRootPaths = [workspacePath],
            PreferredShell = CopilotShellKind.PowerShell,
        };
    }

    private static CopilotAgentToolInput CreateShellInput(string command)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["command"] = command,
                ["shell"] = "powershell",
                ["workingDirectory"] = Path.GetFullPath(Path.GetTempPath()),
                ["timeoutSeconds"] = 60,
            },
        };
    }
}
