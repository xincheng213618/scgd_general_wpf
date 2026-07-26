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
        Assert.Equal(2_048, CopilotSubagentRunner.MaximumFinalizationOutputTokens);
        Assert.Equal(["ReadLocalFile"], childRequest.RequiredSuccessfulToolNames);

        var contract = CopilotAgentExecutionContract.Create(childRequest, role.CreateTools());
        Assert.True(contract.IsRequired);
        Assert.Equal(CopilotAgentExecutionRequirement.SubagentEvidence, contract.Requirement);
        Assert.Contains("ReadLocalFile", contract.AcceptedToolNames);
        Assert.DoesNotContain("ListDirectory", contract.AcceptedToolNames);

        var readTool = Assert.IsType<CopilotReadLocalFileTool>(
            role.CreateTools().Single(tool => string.Equals(tool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(CopilotSubagentRunner.MaximumWorkspaceReadCharactersPerCall, readTool.MaximumReadCharacters);
        Assert.Equal(CopilotLocalFileToolSupport.MaxReadCharacters, new CopilotReadLocalFileTool().MaximumReadCharacters);
        Assert.Contains("GrepText", role.RuntimeInstructions, StringComparison.Ordinal);
        Assert.Contains("focused line ranges", role.RuntimeInstructions, StringComparison.Ordinal);
        Assert.Contains("first tool round must call ReadLocalFile exactly once with no path", role.RuntimeInstructions, StringComparison.Ordinal);
        Assert.Contains("do not begin with an individual file, SearchFiles", role.RuntimeInstructions, StringComparison.Ordinal);
        Assert.Contains("L<number>:", role.RuntimeInstructions, StringComparison.Ordinal);
        Assert.Contains("<full-path>:<line-or-range>", role.RuntimeInstructions, StringComparison.Ordinal);
        Assert.Contains("full-file traversal is required only", role.RuntimeInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamedWorkspaceFilesArePreselectedForOneBoundedBatchRead()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-child-file-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var fileNames = new[] { "Coordinator.cs", "Explore.cs", "RoleCatalog.cs" };
            var paths = fileNames.Select(fileName => Path.Combine(root, fileName)).ToArray();
            foreach (var path in paths)
                await File.WriteAllTextAsync(path, $"// evidence from {Path.GetFileName(path)}");
            await File.WriteAllTextAsync(
                paths[0],
                $"// preface{Environment.NewLine}{Environment.NewLine}// evidence from Coordinator.cs");

            var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
            var parentRequest = new CopilotAgentRequest
            {
                UserText = $"Use only DelegateExplore to inspect {root}.",
                Profile = CreateProfile(),
                SearchRootPaths = [root],
                TrustedProjectRootPaths = [root],
                Mode = CopilotAgentMode.Code,
            };
            var childRequest = CopilotSubagentRunner.CreateChildRequest(
                parentRequest,
                role,
                new CopilotSubagentRunRequest
                {
                    RunId = "explore-batch-test",
                    Task = $"Read {string.Join(", ", fileNames)} in {root}.",
                    RequestTokenBudget = 16_384,
                });

            Assert.True(childRequest.PreferBatchReadLocalFiles);
            Assert.Equal(paths, childRequest.ReadableLocalFilePaths, StringComparer.OrdinalIgnoreCase);
            Assert.Empty(childRequest.RequiredSuccessfulToolNames);

            var readTool = Assert.IsType<CopilotReadLocalFileTool>(
                role.CreateTools().Single(tool => string.Equals(tool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)));
            var contract = CopilotAgentExecutionContract.Create(childRequest, role.CreateTools());
            Assert.Equal(CopilotAgentExecutionRequirement.LocalFileEvidence, contract.Requirement);
            var result = await readTool.ExecuteAsync(childRequest, CopilotAgentToolInput.Empty, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(paths, result.SuccessfullyReadLocalFilePaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(paths, result.LocalFileReadScopes.Select(scope => scope.Path), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("L2: ", result.Content, StringComparison.Ordinal);
            Assert.Contains("L3: // evidence from Coordinator.cs", result.Content, StringComparison.Ordinal);
            Assert.Contains("L1: // evidence from Explore.cs", result.Content, StringComparison.Ordinal);
            Assert.Contains("L1: // evidence from RoleCatalog.cs", result.Content, StringComparison.Ordinal);
            var evaluation = contract.Evaluate(
            [
                new CopilotAgentStepRecord
                {
                    Round = 1,
                    ToolCall = new CopilotToolCall
                    {
                        ToolName = "ReadLocalFile",
                        ToolInput = CopilotAgentToolInput.Empty,
                    },
                    Observation = CopilotToolObservation.FromResult(result),
                    Execution = new CopilotToolExecutionInfo
                    {
                        CallId = "batch-read",
                        Round = 1,
                        ToolName = "ReadLocalFile",
                        State = CopilotToolExecutionState.Completed,
                    },
                },
            ]);
            Assert.True(evaluation.IsSatisfied);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NamedWorkspaceBatchSelectsLateTaskFocusedEvidenceFromEveryFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-child-focused-batch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var fileNames = new[]
            {
                "CopilotSubagentCoordinator.cs",
                "CopilotExploreSubagent.cs",
                "CopilotSubagentRoleCatalog.cs",
            };
            var paths = fileNames.Select(fileName => Path.Combine(root, fileName)).ToArray();
            var filler = Enumerable.Range(1, 140)
                .Select(index => $"// unrelated filler {index:D3} {new string('x', 72)}")
                .ToArray();
            var evidenceLines = new[]
            {
                "private const int MaximumRunTokenBudget = 16_384; // coordinator budget evidence",
                "var finalizationRequest = CreateBudgetFinalizationRequest(explorationBudget); // convergence evidence",
                "RuntimeInstructions = \"Use successful ReadLocalFile evidence during finalization.\";",
            };
            for (var index = 0; index < paths.Length; index++)
                await File.WriteAllLinesAsync(paths[index], filler.Append(evidenceLines[index]).Append("// trailing context"));

            var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
            var parentRequest = new CopilotAgentRequest
            {
                UserText = $"Use only DelegateExplore to inspect {root}.",
                Profile = CreateProfile(),
                SearchRootPaths = [root],
                TrustedProjectRootPaths = [root],
                Mode = CopilotAgentMode.Code,
            };
            var childRequest = CopilotSubagentRunner.CreateChildRequest(
                parentRequest,
                role,
                new CopilotSubagentRunRequest
                {
                    RunId = "explore-focused-batch-test",
                    Task = $"检查 {root} 下与子 Agent 预算和证据收束相关的实现，至少读取 {string.Join("、", fileNames)}。",
                    RequestTokenBudget = 16_384,
                });
            var readTool = Assert.IsType<CopilotReadLocalFileTool>(
                role.CreateTools().Single(tool => string.Equals(tool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)));

            var result = await readTool.ExecuteAsync(childRequest, CopilotAgentToolInput.Empty, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(paths, result.SuccessfullyReadLocalFilePaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(3, result.LocalFileReadScopes.Count);
            Assert.All(result.LocalFileReadScopes, scope =>
            {
                Assert.True(scope.StartLine > 100);
                Assert.True(scope.EndLine >= 141);
                Assert.False(scope.WasTruncated);
            });
            foreach (var evidenceLine in evidenceLines)
                Assert.Contains($"L141: {evidenceLine}", result.Content, StringComparison.Ordinal);
            Assert.Contains("[Selection] Task-focused evidence window", result.Content, StringComparison.Ordinal);
            Assert.Contains("3 task-focused evidence window(s)", result.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain("L1: // unrelated filler", result.Content, StringComparison.Ordinal);
            Assert.True(result.Content.Length < 13_000, $"Focused batch content was {result.Content.Length} characters.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
        var unreadLine = CopilotSubagentEvidencePolicy.FindUnobservedWorkspaceFileCitations(
            role,
            explorationResult.StepRecords,
            @"Finding: [Example.cs](<C:\workspace\Example.cs:142>) contains an unobserved branch.");

        Assert.Empty(grounded);
        Assert.Equal([@"C:\workspace\Invented.cs"], ungrounded);
        Assert.Equal([@"C:\workspace\Example.cs:142"], unreadLine);
    }

    [Fact]
    public async Task WorkspaceSubagentReadToolUsesBoundedObservationWindow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-subagent-read-window-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "Large.cs");
            await File.WriteAllTextAsync(
                path,
                new string('x', CopilotSubagentRunner.MaximumWorkspaceReadCharactersPerCall + 500));
            var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
            var readTool = Assert.IsType<CopilotReadLocalFileTool>(
                role.CreateTools().Single(tool => string.Equals(tool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)));

            var result = await CopilotReadLocalFileCapability.ReadAsync(
                [path],
                path,
                preferBatchReadAll: false,
                startLine: null,
                startColumn: null,
                endLine: null,
                readTool.MaximumReadCharacters,
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(
                $"kept the first {CopilotSubagentRunner.MaximumWorkspaceReadCharactersPerCall} characters",
                result.Content,
                StringComparison.Ordinal);
            Assert.Contains("L1: ", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("L2: ...<content truncated", result.Content, StringComparison.Ordinal);
            var scope = Assert.Single(result.LocalFileReadScopes);
            Assert.Equal(path, scope.Path);
            Assert.True(scope.WasTruncated);
            Assert.Equal(1, scope.StartLine);
            Assert.Equal(1, scope.ContinuationStartLine);
            Assert.Equal(CopilotSubagentRunner.MaximumWorkspaceReadCharactersPerCall + 1, scope.ContinuationStartColumn);
            var observationScope = Assert.Single(
                CopilotToolObservation.FromResult(result.ToToolResult("ReadLocalFile")).LocalFileReadScopes);
            Assert.Equal(scope.Path, observationScope.Path);
            Assert.Equal(scope.ContinuationStartColumn, observationScope.ContinuationStartColumn);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
        Assert.Contains("L<number>:", finalization.UserText, StringComparison.Ordinal);
        Assert.Contains("<full-path>:<line-or-range>", finalization.UserText, StringComparison.Ordinal);
        Assert.Contains("under 2,500 characters", finalization.UserText, StringComparison.Ordinal);
        Assert.Contains("complete: yes|no", finalization.UserText, StringComparison.Ordinal);
        Assert.Contains("omitted unrelated file text alone does not make the task incomplete", finalization.UserText, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchReadFinalizationRetainsBalancedEvidenceFromEveryFile()
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var childRequest = CreateChildRequest(role);
        var paths = new[]
        {
            @"C:\workspace\Coordinator.cs",
            @"C:\workspace\Explore.cs",
            @"C:\workspace\RoleCatalog.cs",
        };
        var sections = paths
            .Select((path, index) =>
                $"[File] {path}{Environment.NewLine}"
                + $"[Lines] 1-200{Environment.NewLine}"
                + $"[Read Scope]{Environment.NewLine}"
                + $"start_line: 1{Environment.NewLine}"
                + $"end_line: 200{Environment.NewLine}"
                + $"[Content with authoritative one-based line numbers]{Environment.NewLine}"
                + $"L1: HEAD_EVIDENCE_{index}{Environment.NewLine}"
                + new string((char)('a' + index), 6_000)
                + Environment.NewLine
                + $"L200: TAIL_EVIDENCE_{index}")
            .ToArray();
        var explorationResult = CreateExplorationResult(
            consumedTokens: 8_553,
            observation: new CopilotToolObservation
            {
                Success = true,
                Summary = "Read 3/3 local files.",
                Content = string.Join(Environment.NewLine + Environment.NewLine, sections),
                AttemptedLocalFilePaths = paths,
                SuccessfullyReadLocalFilePaths = paths,
                LocalFileReadScopes = paths.Select(path => new CopilotLocalFileReadScope
                {
                    Path = path,
                    StartLine = 1,
                    StartColumn = 1,
                    EndLine = 200,
                    EndColumn = 120,
                }).ToArray(),
            });

        var finalization = CopilotSubagentRunner.CreateBudgetFinalizationRequest(
            childRequest,
            role,
            explorationResult,
            totalTokenBudget: 16_384,
            elapsed: TimeSpan.FromSeconds(5));

        Assert.NotNull(finalization);
        foreach (var path in paths)
            Assert.Contains(Path.GetFileName(path), finalization.UserText, StringComparison.Ordinal);
        for (var index = 0; index < paths.Length; index++)
            Assert.Contains($"TAIL_EVIDENCE_{index}", finalization.UserText, StringComparison.Ordinal);
        Assert.Contains("middle of this file observation omitted for balanced batch evidence", finalization.UserText, StringComparison.Ordinal);
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
        string toolName = "ReadLocalFile",
        CopilotToolObservation? observation = null)
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
                    Observation = observation ?? new CopilotToolObservation
                    {
                        Success = true,
                        Summary = "Read Example.cs line 42.",
                        Content = "line 42: verified evidence",
                        SuccessfullyReadLocalFilePaths = [@"C:\workspace\Example.cs"],
                        LocalFileReadScopes =
                        [
                            new CopilotLocalFileReadScope
                            {
                                Path = @"C:\workspace\Example.cs",
                                StartLine = 40,
                                StartColumn = 1,
                                EndLine = 50,
                                EndColumn = 120,
                            },
                        ],
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
