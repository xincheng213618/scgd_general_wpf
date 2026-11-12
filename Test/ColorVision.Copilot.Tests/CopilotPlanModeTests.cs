namespace ColorVision.Copilot.Tests;

public sealed class CopilotPlanModeTests
{
    [Theory]
    [InlineData("/plan", "")]
    [InlineData("/plan fix the auth bug", "fix the auth bug")]
    public void PlanCommandAcceptsAnOptionalTask(string input, string expectedTask)
    {
        var invocation = CopilotLocalCommandCatalog.Parse(input);

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Plan, invocation.Command.Kind);
        Assert.True(invocation.Command.AcceptsArguments);
        Assert.Equal(expectedTask, invocation.Arguments);
    }

    [Fact]
    public void PlanModeSuppressesEveryWriteExecutionIntent()
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Plan,
            UserText = "Implement a new flow, create files, update the database, run tests, and execute the migration command.",
            WritableLocalRootPaths = [@"C:\workspace"],
            SearchRootPaths = [@"C:\workspace"],
        };

        Assert.True(CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode));
        Assert.True(CopilotToolIntentPolicy.ExplicitlyDisallowsWriteAccess(request));
        Assert.True(CopilotToolIntentPolicy.NeedsTaskLedger(request));
        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceEdit(request));
        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceCreate(request));
        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceRollback(request));
        Assert.False(CopilotToolIntentPolicy.NeedsWorkspaceValidation(request));
        Assert.False(CopilotToolIntentPolicy.NeedsFlowMutation(request));
        Assert.False(CopilotToolIntentPolicy.NeedsDatabaseWrite(request));
        Assert.False(CopilotToolIntentPolicy.NeedsShellExecution(request));
        Assert.False(CopilotToolIntentPolicy.NeedsBatchImageProcessing(request));
    }

    [Fact]
    public void PlanModeKeepsReadToolsAndRejectsWriteToolsAtTheRegistryBoundary()
    {
        var request = new CopilotAgentRequest { Mode = CopilotAgentMode.Plan };

        Assert.True(CopilotToolRegistry.IsAllowedForMode(new CopilotTemplatePatchTool(), request));
        Assert.False(CopilotToolRegistry.IsAllowedForMode(new CopilotApplyTemplatePatchTool(), request));
    }

    [Fact]
    public void PlanModeStartsAndCompletesAsAnExplicitPlanningRun()
    {
        Assert.Equal("plan", CopilotMicrosoftAgentFrameworkRuntime.ResolveInitialHarnessMode(CopilotAgentMode.Plan));
        Assert.Equal("execute", CopilotMicrosoftAgentFrameworkRuntime.ResolveInitialHarnessMode(CopilotAgentMode.Auto));

        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "plan",
            Items =
            [
                new CopilotAgentTaskItem { Id = 1, Title = "Implement the change" },
                new CopilotAgentTaskItem { Id = 2, Title = "Run focused tests" },
            ],
        };
        var budget = new CopilotAgentBudgetSnapshot();

        Assert.Equal(
            CopilotAgentStopReason.Completed,
            CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
                ledger,
                budget,
                Array.Empty<CopilotAgentStepRecord>(),
                hasModelFinalAnswer: true,
                requestMode: CopilotAgentMode.Plan));
        Assert.Equal(
            CopilotAgentStopReason.AwaitingUser,
            CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
                ledger,
                budget,
                Array.Empty<CopilotAgentStepRecord>(),
                hasModelFinalAnswer: true));
    }

}
