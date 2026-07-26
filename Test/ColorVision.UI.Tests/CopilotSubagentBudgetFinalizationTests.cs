using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotSubagentBudgetFinalizationTests
{
    [Fact]
    public void FullSubagentBudgetReservesASeparateFinalizationPhase()
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var childRequest = CreateChildRequest(role);

        Assert.Equal(
            16_384 - CopilotSubagentRunner.PhasedFinalizationTokenReserve,
            childRequest.RunBudgetOverride?.RequestTokenBudget);
        Assert.Equal(CopilotSubagentRunner.MaximumExplorationOutputTokens, childRequest.Profile.MaxTokens);
        Assert.Equal(["ReadLocalFile"], childRequest.RequiredSuccessfulToolNames);

        var contract = CopilotAgentExecutionContract.Create(childRequest, role.CreateTools());
        Assert.True(contract.IsRequired);
        Assert.Equal(CopilotAgentExecutionRequirement.SubagentEvidence, contract.Requirement);
        Assert.Contains("ReadLocalFile", contract.AcceptedToolNames);
        Assert.DoesNotContain("ListDirectory", contract.AcceptedToolNames);
    }

    [Fact]
    public void SmallerSubagentBudgetsRemainAvailableToExploration()
    {
        Assert.Equal(12_000, CopilotSubagentRunner.ResolveExplorationRequestTokenBudget(12_000));
    }

    [Fact]
    public void ChildRunDoesNotInheritTheParentDirectToolSuppression()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-child-tool-surface-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
            var parentRequest = new CopilotAgentRequest
            {
                UserText = "Use only DelegateExplore.",
                Profile = CreateProfile(),
                SearchRootPaths = [root],
                TrustedProjectRootPaths = [root],
                Mode = CopilotAgentMode.Auto,
                RequiresDelegatedWorkspaceEvidence = true,
            };

            var childRequest = CopilotSubagentRunner.CreateChildRequest(
                parentRequest,
                role,
                new CopilotSubagentRunRequest
                {
                    RunId = "explore-test",
                    Task = "Read the requested source file.",
                    RequestTokenBudget = 16_384,
                });

            Assert.False(childRequest.RequiresDelegatedWorkspaceEvidence);
            Assert.True(new CopilotReadLocalFileTool().IsAvailable(childRequest));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DirectoryDiscoveryAloneCannotEnterBudgetFinalization()
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var childRequest = CreateChildRequest(role);
        var explorationResult = CreateExplorationResult(consumedTokens: 8_000, toolName: "ListDirectory");

        Assert.False(CopilotSubagentRunner.HasSuccessfulRequiredEvidence(role, explorationResult.StepRecords));
        Assert.Null(CopilotSubagentRunner.CreateBudgetFinalizationRequest(
            childRequest,
            role,
            explorationResult,
            totalTokenBudget: 16_384,
            elapsed: TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void WorkspaceAnswerRejectsFileCitationsMissingFromSuccessfulReads()
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var explorationResult = CreateExplorationResult(consumedTokens: 8_000);

        var grounded = CopilotSubagentEvidencePolicy.FindUnobservedWorkspaceFileCitations(
            role,
            explorationResult.StepRecords,
            @"Finding: [Example.cs](<C:/workspace/Example.cs:42>) contains the verified branch.");
        var ungrounded = CopilotSubagentEvidencePolicy.FindUnobservedWorkspaceFileCitations(
            role,
            explorationResult.StepRecords,
            @"Finding: `C:\workspace\Invented.cs:17` contains the verified branch.");

        Assert.Empty(grounded);
        Assert.Equal([@"C:\workspace\Invented.cs"], ungrounded);
    }

    [Fact]
    public void RequestTokenExhaustionUsesOnlyTheUnspentBudgetForNoToolsFinalization()
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var childRequest = CreateChildRequest(role);
        var explorationResult = CreateExplorationResult(consumedTokens: 8_553);

        var finalization = CopilotSubagentRunner.CreateBudgetFinalizationRequest(
            childRequest,
            role,
            explorationResult,
            totalTokenBudget: 16_384,
            elapsed: TimeSpan.FromSeconds(5));

        Assert.NotNull(finalization);
        Assert.Equal(7_831, finalization.RunBudgetOverride?.RequestTokenBudget);
        Assert.Equal(CopilotSubagentRunner.MaximumFinalizationOutputTokens, finalization.Profile.MaxTokens);
        Assert.Empty(finalization.SearchRootPaths);
        Assert.Empty(finalization.WritableLocalRootPaths);
        Assert.Equal(CopilotAgentHarnessFeatures.None, finalization.HarnessFeatures);
        Assert.Contains("# Delegated task", finalization.UserText, StringComparison.Ordinal);
        Assert.Contains("ReadLocalFile", finalization.UserText, StringComparison.Ordinal);
        Assert.Contains("line 42", finalization.UserText, StringComparison.Ordinal);
        Assert.Contains("Tools are unavailable", finalization.UserText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(12_500)]
    [InlineData(16_384)]
    public void FinalizationIsSkippedWhenNoBoundedReserveRemains(long consumedTokens)
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var childRequest = CreateChildRequest(role);
        var explorationResult = CreateExplorationResult(consumedTokens);

        var finalization = CopilotSubagentRunner.CreateBudgetFinalizationRequest(
            childRequest,
            role,
            explorationResult,
            totalTokenBudget: 16_384,
            elapsed: TimeSpan.FromSeconds(5));

        Assert.Null(finalization);
    }

    [Fact]
    public void CompletedFinalizationCombinesUsageWithoutReportingTheExplorationGateAsTotalExhaustion()
    {
        var exploration = CreateExplorationResult(consumedTokens: 8_553).Budget;
        var finalization = new CopilotAgentBudgetSnapshot
        {
            RequestTokenBudget = 7_831,
            ConsumedTokens = 3_100,
            ProviderCalls = 1,
            BudgetExhausted = false,
            RequestTokenBudgetExhausted = false,
            MaxToolCalls = 1,
            MaxAgentPasses = 1,
            TotalDurationMs = 80_000,
        };

        var combined = CopilotSubagentRunner.CombineBudgets(
            exploration,
            finalization,
            totalTokenBudget: 16_384,
            elapsed: TimeSpan.FromSeconds(14),
            finalizationCompleted: true);

        Assert.Equal(11_653, combined.ConsumedTokens);
        Assert.Equal(3, combined.ProviderCalls);
        Assert.Equal(16_384, combined.RequestTokenBudget);
        Assert.Equal(4, combined.ToolCalls);
        Assert.False(combined.BudgetExhausted);
        Assert.False(combined.RequestTokenBudgetExhausted);
        Assert.Equal(14_000, combined.ElapsedMs);
    }

    private static CopilotAgentRequest CreateChildRequest(CopilotSubagentRoleDescriptor role)
    {
        var parentRequest = new CopilotAgentRequest
        {
            UserText = @"只读审计 C:\workspace，列出 1 条可验证的问题；不要修改文件。",
            Profile = CreateProfile(),
            SearchRootPaths = [@"C:\workspace"],
            TrustedProjectRootPaths = [@"C:\workspace"],
            Mode = CopilotAgentMode.Code,
        };
        return CopilotSubagentRunner.CreateChildRequest(
            parentRequest,
            role,
            new CopilotSubagentRunRequest
            {
                RunId = "explore-test",
                Task = "Inspect the workspace and return one verified finding.",
                RequestTokenBudget = 16_384,
            });
    }

    private static CopilotAgentRunResult CreateExplorationResult(
        long consumedTokens,
        string toolName = "ReadLocalFile")
    {
        return new CopilotAgentRunResult
        {
            StopReason = CopilotAgentStopReason.BudgetExhausted,
            Budget = new CopilotAgentBudgetSnapshot
            {
                RequestTokenBudget = 16_384,
                ConsumedTokens = consumedTokens,
                ProviderCalls = 2,
                BudgetExhausted = true,
                RequestTokenBudgetExhausted = true,
                MaxToolCalls = 8,
                ToolCalls = 4,
                MaxAgentPasses = 2,
                TotalDurationMs = 90_000,
            },
            StepRecords =
            [
                new CopilotAgentStepRecord
                {
                    Round = 1,
                    ToolCall = new CopilotToolCall
                    {
                        ToolName = toolName,
                        ToolInput = new CopilotAgentToolInput
                        {
                            Arguments = new Dictionary<string, object?>
                            {
                                ["path"] = @"C:\workspace\Example.cs",
                            },
                        },
                    },
                    Observation = new CopilotToolObservation
                    {
                        Success = true,
                        Summary = "Read Example.cs line 42.",
                        Content = "line 42: verified evidence",
                        SuccessfullyReadLocalFilePaths = [@"C:\workspace\Example.cs"],
                    },
                },
            ],
        };
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
    }
}
